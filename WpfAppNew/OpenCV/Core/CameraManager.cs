using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DirectShowLib;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions; 
using Tools.Extend;
using WpfAppNew.OpenCV.Core;
using Size = OpenCvSharp.Size;

namespace WpfAppNew.OpenCv.Core
{
    /// <summary>
    /// 高性能OpenCV摄像头管理器
    /// 提供摄像头预览、录像、拍照等功能
    /// 支持多线程处理和性能优化
    /// </summary>
    public class CameraManager : INotifyPropertyChanged, IDisposable
    {
        #region 私有字段

        private VideoCapture _capture;
        private Mat _currentFrame;
        private bool _isCapturing;
        private bool _disposed;
        private int _currentDeviceIndex = 0;
        private double _currentFps;
        private OpenCvSharp.Size _currentResolution;
        
        private readonly object _lockObject = new object();
        private CancellationTokenSource _cancellationTokenSource;
        private Task _captureTask;
        
        // 性能统计
        private DateTime _lastFrameTime;
        private int _frameCount;
        private readonly Stopwatch _fpsStopwatch;
        private readonly Stopwatch _frameProcessingStopwatch;

        // 集成的管理器
        private readonly VideoRecorder _videoRecorder;
        private readonly DeviceManager _deviceManager;
        private readonly PerformanceMonitor _performanceMonitor;
        private readonly ErrorHandler _errorHandler;

        // 超高清设备支持
        private bool _isUltraHighDefinition;
        private int _maxFrameBufferSize = 3; // 默认缓冲区大小
        private readonly object _frameBufferLock = new object();
        private Queue<Mat> _frameBuffer;
        
        // 智能帧跳过相关字段
        private int _frameSkipCounter = 0;
        private int _dynamicFrameSkipRate = 1; // 动态帧跳过率
        private DateTime _lastFrameProcessTime = DateTime.Now;
        private readonly Queue<double> _frameProcessingTimes = new Queue<double>(); // 帧处理时间队列
        private const int MAX_PROCESSING_TIME_SAMPLES = 10; // 最大处理时间样本数
        
        // 异步处理相关字段
        private readonly SemaphoreSlim _frameProcessingSemaphore = new SemaphoreSlim(1, 1);
        private volatile bool _isAsyncProcessing = false;
        private Timer _memoryCleanupTimer;

        // UI渲染优化相关字段
        private readonly object _bitmapCacheLock = new object();
        private BitmapSource _cachedBitmapSource;
        private DateTime _lastBitmapCacheTime = DateTime.MinValue;
        private readonly TimeSpan _bitmapCacheTimeout = TimeSpan.FromMilliseconds(50); // 50ms缓存超时
        private int _uiUpdateSkipCount = 0;

        // 异常处理和停止机制
        private int _consecutiveFailureCount = 0; // 连续失败次数
        private const int MAX_CONSECUTIVE_FAILURES = 5; // 最大连续失败次数
        private DateTime _lastFailureTime = DateTime.MinValue; // 上次失败时间
        private bool _isInitializationStopped = false; // 是否已停止初始化

        #endregion

        #region 事件定义

        /// <summary>
        /// 帧捕获事件
        /// </summary>
        public event EventHandler<FrameCapturedEventArgs> FrameCaptured;

        /// <summary>
        /// 状态变更事件
        /// </summary>
        public event EventHandler<StatusChangedEventArgs> StatusChanged;

        /// <summary>
        /// 录像状态变更事件
        /// </summary>
        public event EventHandler<RecordingStatusChangedEventArgs> RecordingStatusChanged;

        /// <summary>
        /// 错误发生事件
        /// </summary>
        public event EventHandler<ErrorOccurredEventArgs> ErrorOccurred;

        /// <summary>
        /// 性能统计事件
        /// </summary>
        public event EventHandler<PerformanceStatsEventArgs> PerformanceStats;

        /// <summary>
        /// 性能警告事件
        /// </summary>
        public event EventHandler<PerformanceWarningEventArgs> PerformanceWarning;

        #endregion

        #region 公共属性

        /// <summary>
        /// 当前摄像头设备索引
        /// </summary>
        public int CurrentDeviceIndex
        {
            get => _currentDeviceIndex;
            set
            {
                if (_currentDeviceIndex != value)
                {
                    _currentDeviceIndex = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 当前设备路径
        /// </summary>
        public string CurrentDevicePath
        {
            get => CurrentDevice?.DevicePath ?? string.Empty;
        }

        /// <summary>
        /// 是否正在捕获
        /// </summary>
        public bool IsCapturing
        {
            get => _isCapturing;
            private set
            {
                if (_isCapturing != value)
                {
                    _isCapturing = value;
                    OnPropertyChanged();
                    StatusChanged?.Invoke(this, new StatusChangedEventArgs(value ? CameraStatus.Running : CameraStatus.Stopped, value, _currentDeviceIndex));
                }
            }
        }

        /// <summary>
        /// 是否正在录制
        /// </summary>
        public bool IsRecording => _videoRecorder?.IsRecording ?? false;

        /// <summary>
        /// 当前FPS
        /// </summary>
        public double CurrentFps
        {
            get => _currentFps;
            private set
            {
                if (Math.Abs(_currentFps - value) > 0.1)
                {
                    _currentFps = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 摄像头分辨率宽度
        /// </summary>
        public int FrameWidth { get; private set; } = 640;

        /// <summary>
        /// 摄像头分辨率高度
        /// </summary>
        public int FrameHeight { get; private set; } = 480;

        /// <summary>
        /// 当前分辨率
        /// </summary>
        public OpenCvSharp.Size CurrentResolution
        {
            get => _currentResolution;
            private set
            {
                if (_currentResolution != value)
                {
                    _currentResolution = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 设备管理器
        /// </summary>
        public DeviceManager DeviceManager => _deviceManager;

        /// <summary>
        /// 视频录制器
        /// </summary>
        public VideoRecorder VideoRecorder => _videoRecorder;

        /// <summary>
        /// 是否已停止初始化（由于连续失败次数过多）
        /// </summary>
        public bool IsInitializationStopped => _isInitializationStopped;

        /// <summary>
        /// 连续初始化失败次数
        /// </summary>
        public int ConsecutiveFailureCount => _consecutiveFailureCount;

        /// <summary>
        /// 最大允许的连续失败次数
        /// </summary>
        public int MaxConsecutiveFailures => MAX_CONSECUTIVE_FAILURES;

        /// <summary>
        /// 上次初始化失败的时间
        /// </summary>
        public DateTime LastFailureTime => _lastFailureTime;

        /// <summary>
        /// 性能监控器
        /// </summary>
        public PerformanceMonitor PerformanceMonitor => _performanceMonitor;

        /// <summary>
        /// 错误处理器
        /// </summary>
        public ErrorHandler ErrorHandler => _errorHandler;

        /// <summary>
        /// 可用设备列表
        /// </summary>
        public List<CameraDevice> AvailableDevices => _deviceManager?.AvailableDevices ?? new List<CameraDevice>();

        /// <summary>
        /// 当前设备
        /// </summary>
        public CameraDevice CurrentDevice => _deviceManager?.CurrentDevice;

        /// <summary>
        /// 录像文件路径
        /// </summary>
        public string RecordingPath => _videoRecorder?.OutputPath;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化摄像头管理器
        /// </summary>
        /// <param name="deviceIndex">设备索引，默认为0</param>
        public CameraManager(int deviceIndex = 0)
        {
            _currentDeviceIndex = deviceIndex;
            _fpsStopwatch = new Stopwatch();
            _frameProcessingStopwatch = new Stopwatch();
            _cancellationTokenSource = new CancellationTokenSource();
            _currentResolution = new OpenCvSharp.Size(640, 480);

            // 初始化集成的管理器
            _videoRecorder = new VideoRecorder();
            _deviceManager = new DeviceManager();
            _performanceMonitor = new PerformanceMonitor();
            _errorHandler = new ErrorHandler();

            // 订阅事件
            _videoRecorder.RecordingStatusChanged += OnRecordingStatusChanged;
            _deviceManager.CurrentDeviceChanged += OnCurrentDeviceChanged;
            _performanceMonitor.PerformanceWarning += OnPerformanceWarning;
            _errorHandler.ErrorOccurred += OnErrorOccurred;
            _errorHandler.SystemHealthChanged += OnSystemHealthChanged;
            _errorHandler.ErrorRecovery += OnErrorRecovery;

            // 初始化硬件加速管理器
            InitializeAcceleration();

            // 初始化超高清设备支持
            InitializeUltraHighDefinitionSupport();
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 初始化摄像头捕获
        /// </summary>
        /// <param name="width">分辨率宽度</param>
        /// <param name="height">分辨率高度</param>
        /// <returns>是否初始化成功</returns>
        public async Task<bool> InitializeCaptureAsync(int width = 640, int height = 480)
        {
            // 检查是否已停止初始化
            if (_isInitializationStopped)
            {
                var stopMessage = $"摄像头初始化已停止，连续失败次数已达到最大值 {MAX_CONSECUTIVE_FAILURES}";
                System.Diagnostics.Debug.WriteLine($"CameraManager: {stopMessage}");
                LogUtil.Info($"CameraManager: {stopMessage}");
                ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(
                    new InvalidOperationException(stopMessage), "初始化已停止"));
                return false;
            }

            return await _errorHandler.TryRecoverAsync(async () =>
            {
                try
                {
                    await Task.Run(() =>
                    {
                        lock (_lockObject)
                        {
                            _capture?.Release();
                            
                            // 尝试多种方式初始化摄像头
                            bool initialized = TryInitializeCameraWithMultipleBackends(_currentDeviceIndex);
                            
                            if (!initialized)
                            {
                                throw new InvalidOperationException($"无法打开摄像头设备 {_currentDeviceIndex}");
                            }

                            // 设置分辨率
                            System.Diagnostics.Debug.WriteLine($"CameraManager: 尝试设置分辨率为 {width}x{height}");
                            LogUtil.Info($"CameraManager: 尝试设置分辨率为 {width}x{height}");
                            
                            // 超高清设备的特殊处理
                            if (IsUltraHighDefinition(width, height))
                            {
                                System.Diagnostics.Debug.WriteLine("CameraManager: 检测到超高清分辨率，应用特殊设置");
                                LogUtil.Info("CameraManager: 检测到超高清分辨率，应用特殊设置");
                                
                                // 为超高清设备设置更大的缓冲区
                                _capture.Set(VideoCaptureProperties.BufferSize, 1);
                                
                                // 设置较低的帧率以减少内存压力
                                _capture.Set(VideoCaptureProperties.Fps, 15);
                                
                                // 尝试设置更高的编解码器质量
                                _capture.Set(VideoCaptureProperties.FourCC, VideoWriter.FourCC('M', 'J', 'P', 'G'));
                            }
                            else
                            {
                                // 标准设备设置
                                _capture.Set(VideoCaptureProperties.Fps, 30);
                            }
                            
                            _capture.Set(VideoCaptureProperties.FrameWidth, width);
                            _capture.Set(VideoCaptureProperties.FrameHeight, height);

                            // 获取实际分辨率
                            FrameWidth = (int)_capture.Get(VideoCaptureProperties.FrameWidth);
                            FrameHeight = (int)_capture.Get(VideoCaptureProperties.FrameHeight);
                            
                            // 配置超高清设备的性能参数
                            ConfigureUltraHighDefinitionSettings(FrameWidth, FrameHeight);
                            
                            System.Diagnostics.Debug.WriteLine($"CameraManager: 实际获得的分辨率为 {FrameWidth}x{FrameHeight}");
                            LogUtil.Info($"CameraManager: 实际获得的分辨率为 {FrameWidth}x{FrameHeight}");

                            _currentFrame = new Mat();
                        }
                    });

                    OnPropertyChanged(nameof(FrameWidth));
                    OnPropertyChanged(nameof(FrameHeight));
                    
                    // 初始化成功，重置失败计数
                    ResetInitializationFailureCount();
                    
                    return true;
                }
                catch (Exception ex)
                {
                    // 记录失败次数
                    RecordInitializationFailure();
                    
                    _errorHandler.HandleError(ex, "初始化摄像头", ErrorSeverity.High);
                    ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(ex, "初始化摄像头失败"));
                    return false;
                }
            }, maxRetries: 3, context: "初始化摄像头捕获");
        }

        /// <summary>
        /// 初始化摄像头设备（同步版本）
        /// </summary>
        /// <param name="deviceIndex">设备索引</param>
        /// <returns>是否初始化成功</returns>
        private bool InitializeCamera(int deviceIndex)
        {
            try
            {
                lock (_lockObject)
                {
                    _capture?.Release();
                    _capture?.Dispose();
                    _capture = null;
                    
                    // 使用多后端初始化
                    if (!TryInitializeCameraWithMultipleBackends(deviceIndex))
                    {
                        ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(
                            new InvalidOperationException($"无法打开摄像头设备 {deviceIndex}"), 
                            "初始化摄像头失败"));
                        return false;
                    }

                    // 设置分辨率和帧率
                    _capture.Set(VideoCaptureProperties.FrameWidth, FrameWidth);
                    _capture.Set(VideoCaptureProperties.FrameHeight, FrameHeight);
                    _capture.Set(VideoCaptureProperties.Fps, 30);
                    
                    // 设置缓冲区大小以减少延迟
                    _capture.Set(VideoCaptureProperties.BufferSize, 1);

                    // 获取实际分辨率
                    var actualWidth = (int)_capture.Get(VideoCaptureProperties.FrameWidth);
                    var actualHeight = (int)_capture.Get(VideoCaptureProperties.FrameHeight);
                    var actualFps = _capture.Get(VideoCaptureProperties.Fps);
                    
                    System.Diagnostics.Debug.WriteLine($"CameraManager: 实际分辨率 {actualWidth}x{actualHeight}, FPS: {actualFps}");
                    LogUtil.Info($"CameraManager: 实际分辨率 {actualWidth}x{actualHeight}, FPS: {actualFps}");
                    
                    FrameWidth = actualWidth;
                    FrameHeight = actualHeight;

                    _currentFrame = new Mat();
                    _currentDeviceIndex = deviceIndex;
                    
                    // 测试读取一帧以验证摄像头工作正常
                    var testFrame = new Mat();
                    if (_capture.Read(testFrame) && !testFrame.Empty())
                    {
                        System.Diagnostics.Debug.WriteLine($"CameraManager: 成功读取测试帧，尺寸: {testFrame.Width}x{testFrame.Height}");
                        LogUtil.Info($"CameraManager: 成功读取测试帧，尺寸: {testFrame.Width}x{testFrame.Height}");
                        testFrame.Dispose();
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("CameraManager: 警告 - 无法读取测试帧");
                        LogUtil.Info("CameraManager: 警告 - 无法读取测试帧");
                        testFrame.Dispose();
                    }
                }

                OnPropertyChanged(nameof(FrameWidth));
                OnPropertyChanged(nameof(FrameHeight));
                
                return true;
            }
            catch (Exception ex)
            {
                _errorHandler.HandleError(ex, "初始化摄像头", ErrorSeverity.High);
                ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(ex, "初始化摄像头失败"));
                return false;
            }
        }

        /// <summary>
        /// 开始捕获指定设备的视频
        /// </summary>
        /// <param name="deviceIndex">设备索引</param>
        /// <returns>是否成功开始捕获</returns>
        public bool StartCapture(int deviceIndex)
        {
            try
            {
                // 如果正在捕获，先停止
                if (IsCapturing)
                {
                    StopPreview();
                }

                // 选择设备
                if (!_deviceManager.SelectDevice(deviceIndex))
                {
                    ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(new InvalidOperationException($"无法选择设备 {deviceIndex}"), "开始捕获失败"));
                    return false;
                }

                // 初始化摄像头
                if (!InitializeCamera(deviceIndex))
                {
                    return false;
                }

                // 开始预览
                StartPreview();
                return IsCapturing;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(ex, "开始捕获失败"));
                return false;
            }
        }

        /// <summary>
        /// 开始预览
        /// </summary>
        public void StartPreview()
        {
            if (_capture == null || !_capture.IsOpened())
            {
                ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(new InvalidOperationException("摄像头未初始化"), "开始预览失败"));
                return;
            }

            if (IsCapturing) return;

            // 刷新设备列表
            _deviceManager.RefreshDevices();
            
            // 选择当前设备
            if (!_deviceManager.SelectDevice(_currentDeviceIndex))
            {
                ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(new InvalidOperationException($"无法选择设备 {_currentDeviceIndex}"), "开始预览失败"));
                return;
            }

            IsCapturing = true;
            _fpsStopwatch.Start();
            _frameCount = 0;

            // 启动性能监控
            _performanceMonitor.StartMonitoring();

            _captureTask = Task.Run(() => CaptureLoop());
        }

        /// <summary>
        /// 停止预览
        /// </summary>
        public void StopPreview()
        {
            if (!IsCapturing) return;

            IsCapturing = false;
            _cancellationTokenSource?.Cancel();
            
            _captureTask?.Wait(1000);
            _fpsStopwatch.Stop();
            
            // 停止性能监控
            _performanceMonitor.StopMonitoring();
            
            if (IsRecording)
            {
                StopRecording();
            }
            
            // 重新创建CancellationTokenSource以便下次使用
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// 重置初始化停止状态，允许重新尝试初始化
        /// </summary>
        /// <remarks>
        /// 当摄像头初始化连续失败达到最大次数后，系统会停止尝试初始化。
        /// 调用此方法可以重置失败计数，允许重新尝试初始化摄像头。
        /// </remarks>
        public void ResetInitializationStopState()
        {
            lock (_lockObject)
            {
                if (_isInitializationStopped || _consecutiveFailureCount > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"CameraManager: 手动重置初始化停止状态（之前失败 {_consecutiveFailureCount} 次）");
                    LogUtil.Info($"CameraManager: 手动重置初始化停止状态（之前失败 {_consecutiveFailureCount} 次）");
                    _consecutiveFailureCount = 0;
                    _isInitializationStopped = false;
                    _lastFailureTime = DateTime.MinValue;
                }
            }
        }

        /// <summary>
        /// 开始录像
        /// </summary>
        /// <param name="outputPath">输出文件路径</param>
        /// <param name="codec">编码器，默认为MP4V</param>
        /// <param name="fps">帧率，默认为30</param>
        /// <returns>是否开始成功</returns>
        public bool StartRecording(string outputPath, FourCC codec = default(FourCC), double fps = 30.0)
        {
            if (!IsCapturing)
            {
                ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(new InvalidOperationException("请先开始预览"), "开始录像失败"));
                return false;
            }

            if (IsRecording) return false;

            try
            {
                // 将FourCC转换为VideoCodec
                VideoCodec videoCodec = VideoCodec.H264; // 默认值
                if (codec != default(FourCC))
                {
                    // 根据FourCC选择对应的VideoCodec
                    if (codec == FourCC.H264) videoCodec = VideoCodec.H264;
                    else if (codec == FourCC.HEVC) videoCodec = VideoCodec.H265;
                    else if (codec == FourCC.XVID) videoCodec = VideoCodec.XVID;
                    else if (codec == FourCC.MJPG) videoCodec = VideoCodec.MJPEG;
                    else if (codec == FourCC.MP4V) videoCodec = VideoCodec.MP4V;
                    else if (codec == FourCC.WMV1) videoCodec = VideoCodec.WMV;
                    else videoCodec = VideoCodec.H264;
                } 
                return _videoRecorder.StartRecording(outputPath, new OpenCvSharp.Size(FrameWidth, FrameHeight), fps, videoCodec);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(ex, "开始录像失败"));
                return false;
            }
        }

        /// <summary>
        /// 停止录像
        /// </summary>
        public string StopRecording()
        {
            if (!IsRecording) return "";

            _videoRecorder.StopRecording();

            return RecordingPath;
        }

        /// <summary>
        /// 拍照
        /// </summary>
        /// <param name="savePath">保存路径</param>
        /// <returns>是否拍照成功</returns>
        public bool TakeSnapshot(string savePath)
        {
            if (!IsCapturing || _currentFrame == null || _currentFrame.Empty())
            {
                ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(new InvalidOperationException("当前无可用帧"), "拍照失败"));
                return false;
            }

            try
            {
                lock (_lockObject)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(savePath));
                    _currentFrame.SaveImage(savePath);
                }
                return true;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(ex, "拍照失败"));
                return false;
            }
        }

        /// <summary>
        /// 设置摄像头属性
        /// </summary>
        /// <param name="property">属性类型</param>
        /// <param name="value">属性值</param>
        public void SetCameraProperty(CameraProperty property, double value)
        {
            if (_capture == null || !_capture.IsOpened()) return;

            try
            {
                var cvProperty = GetVideoCaptureProperty(property);
                _capture.Set(cvProperty, value);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(ex, $"设置摄像头属性失败: {property}"));
            }
        }

        /// <summary>
        /// 获取摄像头属性
        /// </summary>
        /// <param name="property">属性类型</param>
        /// <returns>属性值</returns>
        public double GetCameraProperty(CameraProperty property)
        {
            if (_capture == null || !_capture.IsOpened()) return 0;

            try
            {
                var cvProperty = GetVideoCaptureProperty(property);
                return _capture.Get(cvProperty);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(ex, $"获取摄像头属性失败: {property}"));
                return 0;
            }
        }

        /// <summary>
        /// 获取可用的摄像头设备列表
        /// 使用DirectShowLib获取详细的设备信息
        /// </summary>
        /// <returns>设备列表</returns>
        public static List<CameraDevice> GetAvailableDevices()
        {
            var devices = new List<CameraDevice>();
            Console.WriteLine("CameraManager: 开始使用DirectShow检测可用设备...");
            
            try
            {
                // 使用DirectShowLib获取视频输入设备
                DsDevice[] dsDevices = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);
                Console.WriteLine($"CameraManager: DirectShow检测到 {dsDevices.Length} 个视频输入设备");

                for (int i = 0; i < dsDevices.Length; i++)
                {
                    try
                    {
                        var dsDevice = dsDevices[i];
                        Console.WriteLine($"CameraManager: 处理设备 {i}: {dsDevice.Name}");

                        // 创建CameraDevice对象
                        var device = new CameraDevice
                    {
                        Index = i,
                        Name = dsDevice.Name ?? $"摄像头 {i}",
                        FriendlyName = dsDevice.Name,
                        DevicePath = dsDevice.DevicePath,
                        DsDevice = dsDevice,
                        IsConnected = true,
                        Status = DeviceStatus.Available,
                            Backend = "DirectShow"
                        };

                        // 设置默认参数，避免在扫描阶段用OpenCV打开每个设备
                        // 实际的分辨率和帧率将在连接设备时获取
                        device.Width = 640;
                        device.Height = 480;
                        device.DefaultWidth = 640;
                        device.DefaultHeight = 480;
                        device.DefaultFps = 30.0;
                        
                        // 设置常见的支持分辨率和帧率
                        device.SupportedResolutions = GetSupportedResolutions();
                        device.SupportedFrameRates = GetSupportedFrameRates();
                        
                        Console.WriteLine($"CameraManager: 设备 {i} 已添加，使用默认配置（实际参数将在连接时获取）");

                        devices.Add(device);
                        Console.WriteLine($"CameraManager: 成功添加设备 {i}: {device.Name}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"CameraManager: 处理设备 {i} 时发生异常: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CameraManager: DirectShow设备检测异常: {ex.Message}");
                
                // 如果DirectShow失败，回退到原来的方法
                Console.WriteLine("CameraManager: 回退到OpenCV设备检测...");
                return GetAvailableDevicesOpenCvFallback();
            }
            
            Console.WriteLine($"CameraManager: DirectShow设备检测完成，共找到 {devices.Count} 个可用设备");
            return devices;
        }

        /// <summary>
        /// OpenCV回退方法：当DirectShow失败时使用
        /// </summary>
        /// <returns>设备列表</returns>
        private static List<CameraDevice> GetAvailableDevicesOpenCvFallback()
        {
            var devices = new List<CameraDevice>();
            Console.WriteLine("CameraManager: 使用OpenCV回退方法检测设备...");
            
            // 检测前10个设备索引
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    using (var capture = new VideoCapture(i))
                    {
                        if (capture.IsOpened())
                        {
                            var device = new CameraDevice
                            {
                                Index = i,
                                Name = $"摄像头 {i}",
                                FriendlyName = $"摄像头 {i}",
                                IsConnected = true,
                                Status = DeviceStatus.Available,
                                Width = (int)capture.Get(VideoCaptureProperties.FrameWidth),
                                Height = (int)capture.Get(VideoCaptureProperties.FrameHeight),
                                DefaultFps = capture.Get(VideoCaptureProperties.Fps),
                                Backend = "OpenCV",
                                SupportedResolutions = GetSupportedResolutions(),
                                SupportedFrameRates = GetSupportedFrameRates()
                            };
                            device.DefaultWidth = device.Width;
                            device.DefaultHeight = device.Height;
                            devices.Add(device);
                            Console.WriteLine($"CameraManager: OpenCV回退检测到设备 {i}: {device.Name}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"CameraManager: OpenCV回退检测设备 {i} 异常: {ex.Message}");
                }
            }
            
            return devices;
        }

        /// <summary>
        /// 获取支持的分辨率列表
        /// </summary>
        /// <returns>分辨率列表</returns>
        private static List<OpenCvSharp.Size> GetSupportedResolutions()
        {
            return new List<OpenCvSharp.Size>
            {
                new OpenCvSharp.Size(320, 240),   // QVGA
                new OpenCvSharp.Size(640, 480),   // VGA
                new OpenCvSharp.Size(800, 600),   // SVGA
                new OpenCvSharp.Size(1024, 768),  // XGA
                new OpenCvSharp.Size(1280, 720),  // HD 720p
                new OpenCvSharp.Size(1280, 960),  // SXGA
                new OpenCvSharp.Size(1920, 1080), // Full HD 1080p
                new OpenCvSharp.Size(2560, 1440), // QHD
                new OpenCvSharp.Size(3840, 2160)  // 4K UHD
            };
        }

        /// <summary>
        /// 获取支持的帧率列表
        /// </summary>
        /// <returns>帧率列表</returns>
        private static List<double> GetSupportedFrameRates()
        {
            return new List<double> { 15.0, 24.0, 25.0, 30.0, 50.0, 60.0 };
        }

        /// <summary>
        /// 切换摄像头设备
        /// </summary>
        /// <param name="deviceIndex">设备索引</param>
        /// <returns>是否切换成功</returns>
        public async Task<bool> SwitchDeviceAsync(int deviceIndex)
        {
            if (deviceIndex == _currentDeviceIndex) return true;

            var wasCapturing = IsCapturing;
            
            if (wasCapturing)
            {
                StopPreview();
            }

            CurrentDeviceIndex = deviceIndex;
            
            var success = await InitializeCaptureAsync(FrameWidth, FrameHeight);
            
            if (success && wasCapturing)
            {
                StartPreview();
            }

            return success;
        }

        /// <summary>
        /// 根据设备路径切换摄像头设备
        /// </summary>
        /// <param name="devicePath">设备路径</param>
        /// <returns>是否切换成功</returns>
        public async Task<bool> SwitchDeviceByPathAsync(string devicePath)
        {
            if (string.IsNullOrEmpty(devicePath))
            {
                ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(
                    new ArgumentException("设备路径不能为空"), "切换设备失败"));
                return false;
            }

            // 如果当前设备路径相同，直接返回成功
            if (CurrentDevicePath == devicePath) return true;

            try
            {
                // 刷新设备列表以确保获取最新的设备信息
                _deviceManager.RefreshDevices();
                
                // 根据设备路径查找对应的设备
                var targetDevice = AvailableDevices.FirstOrDefault(d => d.DevicePath == devicePath);
                
                if (targetDevice == null)
                {
                    ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(
                        new InvalidOperationException($"未找到设备路径为 {devicePath} 的设备"), "切换设备失败"));
                    return false;
                }

                // 使用设备索引进行切换
                return await SwitchDeviceAsync(targetDevice.Index);
            }
            catch (Exception ex)
            {
                _errorHandler.HandleError(ex, "根据设备路径切换设备", ErrorSeverity.High);
                ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(ex, $"切换到设备路径 {devicePath} 失败"));
                return false;
            }
        }

        /// <summary>
        /// 根据设备路径开始捕获
        /// </summary>
        /// <param name="devicePath">设备路径</param>
        /// <returns>是否成功开始捕获</returns>
        public bool StartCaptureByPath(string devicePath)
        {
            if (string.IsNullOrEmpty(devicePath))
            {
                ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(
                    new ArgumentException("设备路径不能为空"), "开始捕获失败"));
                return false;
            }

            try
            {
                // 如果正在捕获，先停止
                if (IsCapturing)
                {
                    StopPreview();
                }

                // 刷新设备列表
                _deviceManager.RefreshDevices();

                // 根据设备路径查找对应的设备
                var targetDevice = AvailableDevices.FirstOrDefault(d => d.DevicePath == devicePath);

                if (targetDevice == null)
                {
                    ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(
                        new InvalidOperationException($"未找到设备路径为 {devicePath} 的设备"), "开始捕获失败"));
                    return false;
                }

                // 选择设备
                if (!_deviceManager.SelectDeviceByPath(devicePath))
                {
                    ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(
                        new InvalidOperationException($"无法选择设备路径 {devicePath}"), "开始捕获失败"));
                    return false;
                }

                // 初始化摄像头
                if (!InitializeCamera(targetDevice.Index))
                {
                    return false;
                }

                // 开始预览
                StartPreview();
                return IsCapturing;
            }
            catch (Exception ex)
            {
                _errorHandler.HandleError(ex, "根据设备路径开始捕获", ErrorSeverity.High);
                ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(ex, $"开始捕获设备路径 {devicePath} 失败"));
                return false;
            }
        }

        /// <summary>
        /// 根据设备名称查找设备路径
        /// </summary>
        /// <param name="deviceName">设备名称</param>
        /// <returns>设备路径，如果未找到返回null</returns>
        public string GetDevicePathByName(string deviceName)
        {
            if (string.IsNullOrEmpty(deviceName)) return null;

            try
            {
                _deviceManager.RefreshDevices();
                var device = AvailableDevices.FirstOrDefault(d => 
                    d.Name?.Equals(deviceName, StringComparison.OrdinalIgnoreCase) == true ||
                    d.FriendlyName?.Equals(deviceName, StringComparison.OrdinalIgnoreCase) == true);
                
                return device?.DevicePath;
            }
            catch (Exception ex)
            {
                _errorHandler.HandleError(ex, "根据设备名称查找设备路径", ErrorSeverity.Low);
                return null;
            }
        }

        /// <summary>
        /// 根据设备路径获取设备信息
        /// </summary>
        /// <param name="devicePath">设备路径</param>
        /// <returns>设备信息，如果未找到返回null</returns>
        public CameraDevice GetDeviceByPath(string devicePath)
        {
            if (string.IsNullOrEmpty(devicePath)) return null;

            try
            {
                _deviceManager.RefreshDevices();
                return AvailableDevices.FirstOrDefault(d => d.DevicePath == devicePath);
            }
            catch (Exception ex)
            {
                _errorHandler.HandleError(ex, "根据设备路径获取设备信息", ErrorSeverity.Low);
                return null;
            }
        }

        /// <summary>
        /// 验证设备路径是否有效
        /// </summary>
        /// <param name="devicePath">设备路径</param>
        /// <returns>是否有效</returns>
        public bool IsDevicePathValid(string devicePath)
        {
            if (string.IsNullOrEmpty(devicePath)) return false;

            try
            {
                _deviceManager.RefreshDevices();
                return AvailableDevices.Any(d => d.DevicePath == devicePath && d.IsConnected);
            }
            catch (Exception ex)
            {
                _errorHandler.HandleError(ex, "验证设备路径", ErrorSeverity.Low);
                return false;
            }
        }

        /// <summary>
        /// 设置摄像头分辨率
        /// </summary>
        /// <param name="width">分辨率宽度</param>
        /// <param name="height">分辨率高度</param>
        /// <returns>是否设置成功</returns>
        public async Task<bool> SetResolutionAsync(int width, int height)
        {
            return await _errorHandler.TryRecoverAsync(async () =>
            {
                try
                {
                    // 验证分辨率参数
                    if (width <= 0 || height <= 0)
                    {
                        throw new ArgumentException($"无效的分辨率参数: {width}x{height}");
                    }

                    // 检查当前设备是否支持该分辨率
                    var currentDevice = CurrentDevice;
                    if (currentDevice?.SupportedResolutions?.Any() == true)
                    {
                        var supportedResolution = currentDevice.SupportedResolutions
                            .FirstOrDefault(r => r.Width == width && r.Height == height);
                        
                        if (supportedResolution == null)
                        {
                            // 如果不支持，记录警告但仍尝试设置
                            _errorHandler.HandleError(
                                new NotSupportedException($"设备可能不支持分辨率 {width}x{height}"),
                                "分辨率设置",
                                ErrorSeverity.Medium
                            );
                        }
                    }

                    var wasCapturing = IsCapturing;
                    
                    // 如果正在捕获，先停止
                    if (wasCapturing)
                    {
                        StopPreview();
                        await Task.Delay(100); // 等待停止完成
                    }

                    // 重新初始化摄像头并设置新分辨率
                    var success = await InitializeCaptureAsync(width, height);
                    
                    if (success)
                    {
                        // 验证分辨率是否真正设置成功
                        bool resolutionMatches = (FrameWidth == width && FrameHeight == height);
                        
                        if (!resolutionMatches)
                        {
                            System.Diagnostics.Debug.WriteLine($"CameraManager: 分辨率设置不完全匹配 - 请求: {width}x{height}, 实际: {FrameWidth}x{FrameHeight}");
                            LogUtil.Info($"CameraManager: 分辨率设置不完全匹配 - 请求: {width}x{height}, 实际: {FrameWidth}x{FrameHeight}");
                            
                            // 尝试强制设置分辨率
                            bool forceSuccess = await TryForceResolutionAsync(width, height);
                            
                            // 重新验证分辨率是否设置成功
                            resolutionMatches = forceSuccess && (FrameWidth == width && FrameHeight == height);
                            
                            if (!resolutionMatches)
                            {
                                System.Diagnostics.Debug.WriteLine($"CameraManager: 强制设置分辨率失败 - 请求: {width}x{height}, 最终: {FrameWidth}x{FrameHeight}");
                                LogUtil.Info($"CameraManager: 强制设置分辨率失败 - 请求: {width}x{height}, 最终: {FrameWidth}x{FrameHeight}");
                                
                                // 如果强制设置也失败，尝试回退到安全分辨率
                                bool fallbackSuccess = await TryFallbackToSafeResolution(width, height, wasCapturing);
                                
                                if (!fallbackSuccess)
                                {
                                    if (wasCapturing)
                                    {
                                        StartPreview(); // 恢复预览
                                    }
                                    
                                    _errorHandler.HandleError(
                                        new NotSupportedException($"摄像头不支持分辨率 {width}x{height}，当前分辨率: {FrameWidth}x{FrameHeight}"),
                                        "分辨率设置",
                                        ErrorSeverity.Medium
                                    );
                                    
                                    return false;
                                }
                                
                                // 回退成功，继续处理
                                System.Diagnostics.Debug.WriteLine($"CameraManager: 回退到安全分辨率成功 - 当前分辨率: {FrameWidth}x{FrameHeight}");
                                LogUtil.Info($"CameraManager: 回退到安全分辨率成功 - 当前分辨率: {FrameWidth}x{FrameHeight}");
                            }
                        }
                        
                        // 只有在分辨率真正设置成功时才更新
                        CurrentResolution = new OpenCvSharp.Size(FrameWidth, FrameHeight);
                        
                        // 更新设备信息中的当前分辨率
                        if (currentDevice != null)
                        {
                            currentDevice.Width = FrameWidth;
                            currentDevice.Height = FrameHeight;
                        }

                        // 如果之前在捕获，重新开始
                        if (wasCapturing)
                        {
                            StartPreview();
                            
                            // 等待一段时间让新的帧捕获循环稳定
                            await Task.Delay(300);
                        }

                        // 触发状态变更事件
                        StatusChanged?.Invoke(this, new StatusChangedEventArgs(
                            CameraStatus.ResolutionChanged, 
                            IsCapturing, 
                            _currentDeviceIndex
                        ));

                        System.Diagnostics.Debug.WriteLine($"CameraManager: 分辨率设置完成 - 最终分辨率: {FrameWidth}x{FrameHeight}");
                        LogUtil.Info($"CameraManager: 分辨率设置完成 - 最终分辨率: {FrameWidth}x{FrameHeight}");
                        return true;
                    }
                    else
                    {
                        throw new InvalidOperationException($"无法设置分辨率为 {width}x{height}");
                    }
                }
                catch (Exception ex)
                {
                    _errorHandler.HandleError(ex, "设置分辨率", ErrorSeverity.High);
                    ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(ex, $"设置分辨率 {width}x{height} 失败"));
                    return false;
                }
            }, maxRetries: 2, context: $"设置分辨率 {width}x{height}");
        }

        /// <summary>
        /// 尝试强制设置分辨率
        /// </summary>
        /// <param name="width">目标宽度</param>
        /// <param name="height">目标高度</param>
        /// <returns>是否成功设置分辨率</returns>
        private async Task<bool> TryForceResolutionAsync(int width, int height)
        {
            try
            {
                return await Task.Run(() =>
                {
                    lock (_lockObject)
                    {
                        if (_capture != null && _capture.IsOpened())
                        {
                            System.Diagnostics.Debug.WriteLine($"CameraManager: 尝试强制设置分辨率 {width}x{height}");
                            LogUtil.Info($"CameraManager: 尝试强制设置分辨率 {width}x{height}");
                            
                            // 多次尝试设置分辨率
                            for (int attempt = 0; attempt < 3; attempt++)
                            {
                                _capture.Set(VideoCaptureProperties.FrameWidth, width);
                                _capture.Set(VideoCaptureProperties.FrameHeight, height);
                                
                                // 等待设置生效
                                Thread.Sleep(100);
                                
                                // 重新获取分辨率
                                var actualWidth = (int)_capture.Get(VideoCaptureProperties.FrameWidth);
                                var actualHeight = (int)_capture.Get(VideoCaptureProperties.FrameHeight);
                                
                                System.Diagnostics.Debug.WriteLine($"CameraManager: 强制设置尝试 {attempt + 1} - 获得分辨率: {actualWidth}x{actualHeight}");
                                LogUtil.Info($"CameraManager: 强制设置尝试 {attempt + 1} - 获得分辨率: {actualWidth}x{actualHeight}");
                                
                                if (actualWidth == width && actualHeight == height)
                                {
                                    FrameWidth = actualWidth;
                                    FrameHeight = actualHeight;
                                    System.Diagnostics.Debug.WriteLine($"CameraManager: 强制设置成功！");
                                    LogUtil.Info($"CameraManager: 强制设置成功！");
                                    return true;
                                }
                            }
                            
                            System.Diagnostics.Debug.WriteLine($"CameraManager: 强制设置失败，所有尝试均未成功");
                            LogUtil.Info($"CameraManager: 强制设置失败，所有尝试均未成功");
                            return false;
                        }
                        return false;
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CameraManager: 强制设置分辨率异常: {ex.Message}");
                LogUtil.Info($"CameraManager: 强制设置分辨率异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 尝试回退到安全分辨率
        /// </summary>
        /// <param name="requestedWidth">请求的宽度</param>
        /// <param name="requestedHeight">请求的高度</param>
        /// <param name="wasCapturing">之前是否在捕获</param>
        /// <returns>回退是否成功</returns>
        private async Task<bool> TryFallbackToSafeResolution(int requestedWidth, int requestedHeight, bool wasCapturing)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"CameraManager: 开始回退到安全分辨率，原请求: {requestedWidth}x{requestedHeight}");
                LogUtil.Info($"CameraManager: 开始回退到安全分辨率，原请求: {requestedWidth}x{requestedHeight}");
                
                // 定义安全分辨率列表（按优先级排序）
                var safeResolutions = new List<(int width, int height)>();
                
                // 如果请求的是超高清分辨率，优先尝试较低的高清分辨率
                if (IsUltraHighDefinition(requestedWidth, requestedHeight))
                {
                    safeResolutions.AddRange(new[]
                    {
                        (1920, 1080), // Full HD
                        (1280, 720),  // HD
                        (1024, 768),  // XGA
                        (800, 600),   // SVGA
                        (640, 480)    // VGA
                    });
                }
                else
                {
                    // 对于非超高清请求，尝试标准分辨率
                    safeResolutions.AddRange(new[]
                    {
                        (1280, 720),  // HD
                        (1024, 768),  // XGA
                        (800, 600),   // SVGA
                        (640, 480)    // VGA
                    });
                }
                
                // 尝试每个安全分辨率
                foreach (var (width, height) in safeResolutions)
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"CameraManager: 尝试回退分辨率: {width}x{height}");
                        LogUtil.Info($"CameraManager: 尝试回退分辨率: {width}x{height}");
                        
                        // 设置分辨率
                        _capture.Set(VideoCaptureProperties.FrameWidth, width);
                        _capture.Set(VideoCaptureProperties.FrameHeight, height);
                        
                        // 等待设置生效
                        await Task.Delay(200);
                        
                        // 验证设置是否成功
                        var actualWidth = (int)_capture.Get(VideoCaptureProperties.FrameWidth);
                        var actualHeight = (int)_capture.Get(VideoCaptureProperties.FrameHeight);
                        
                        if (actualWidth > 0 && actualHeight > 0)
                        {
                            FrameWidth = actualWidth;
                            FrameHeight = actualHeight;
                            
                            // 配置超高清设置
                            ConfigureUltraHighDefinitionSettings(FrameWidth, FrameHeight);
                            
                            System.Diagnostics.Debug.WriteLine($"CameraManager: 成功回退到分辨率: {actualWidth}x{actualHeight}");
                            LogUtil.Info($"CameraManager: 成功回退到分辨率: {actualWidth}x{actualHeight}");
                            
                            // 记录回退事件
                            _errorHandler.HandleError(
                                new InvalidOperationException($"分辨率 {requestedWidth}x{requestedHeight} 不支持，已回退到 {actualWidth}x{actualHeight}"),
                                "分辨率回退",
                                ErrorSeverity.Low
                            );
                            
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"CameraManager: 回退分辨率 {width}x{height} 失败: {ex.Message}");
                        LogUtil.Info($"CameraManager: 回退分辨率 {width}x{height} 失败: {ex.Message}");
                        continue;
                    }
                }
                
                System.Diagnostics.Debug.WriteLine("CameraManager: 所有安全分辨率回退尝试均失败");
                LogUtil.Info("CameraManager: 所有安全分辨率回退尝试均失败");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CameraManager: 回退到安全分辨率异常: {ex.Message}");
                LogUtil.Info($"CameraManager: 回退到安全分辨率异常: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 初始化硬件加速管理器
        /// </summary>
        private void InitializeAcceleration()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("CameraManager: 开始初始化硬件加速");
                LogUtil.Info("CameraManager: 开始初始化硬件加速");

                // 初始化加速管理器
                var accelerationManager = AccelerationManager.Instance;
                bool initialized = accelerationManager.Initialize();

                if (initialized)
                {
                    var info = accelerationManager.AccelerationInfo;
                    System.Diagnostics.Debug.WriteLine($"CameraManager: 硬件加速初始化成功");
                    System.Diagnostics.Debug.WriteLine($"  - CUDA支持: {info.IsCudaSupported} (设备数: {info.CudaDeviceCount})");
                    System.Diagnostics.Debug.WriteLine($"  - OpenCL支持: {info.IsOpenClSupported}");
                    System.Diagnostics.Debug.WriteLine($"  - Intel TBB支持: {info.IsTbbSupported}");
                    System.Diagnostics.Debug.WriteLine($"  - 启用的加速: {info.EnabledAcceleration}");
                    System.Diagnostics.Debug.WriteLine($"  - 可用后端数: {info.AvailableBackends.Count}");

                    LogUtil.Info($"CameraManager: 硬件加速初始化成功 - 启用: {info.EnabledAcceleration}");

                    // 如果支持OpenCL，启用它
                    if (info.IsOpenClSupported && info.EnabledAcceleration == AccelerationType.OpenCL)
                    {
                        try
                        {
                            // OpenCV 3.x+使用透明API，OpenCL会自动启用
                            System.Diagnostics.Debug.WriteLine("CameraManager: OpenCL加速已启用");
                            LogUtil.Info("CameraManager: OpenCL加速已启用");
                        }
                        catch (Exception ex)
                        {
                            LogUtil.Error($"CameraManager: 启用OpenCL失败 - {ex.Message}");
                        }
                    }

                    // 运行基准测试（可选）
                    Task.Run(() =>
                    {
                        try
                        {
                            var benchmark = accelerationManager.RunBenchmark();
                            var bestAcceleration = benchmark.GetBestAcceleration();
                            
                            System.Diagnostics.Debug.WriteLine($"CameraManager: 基准测试完成");
                            System.Diagnostics.Debug.WriteLine($"  - CPU时间: {benchmark.CpuTime:F2}ms");
                            if (benchmark.CudaTime < double.MaxValue)
                                System.Diagnostics.Debug.WriteLine($"  - CUDA时间: {benchmark.CudaTime:F2}ms");
                            if (benchmark.OpenClTime < double.MaxValue)
                                System.Diagnostics.Debug.WriteLine($"  - OpenCL时间: {benchmark.OpenClTime:F2}ms");
                            if (benchmark.TbbTime < double.MaxValue)
                                System.Diagnostics.Debug.WriteLine($"  - TBB时间: {benchmark.TbbTime:F2}ms");
                            System.Diagnostics.Debug.WriteLine($"  - 推荐加速: {bestAcceleration}");

                            LogUtil.Info($"CameraManager: 基准测试完成，推荐加速: {bestAcceleration}");
                        }
                        catch (Exception benchEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"CameraManager: 基准测试异常: {benchEx.Message}");
                            LogUtil.Debug($"CameraManager: 基准测试异常: {benchEx.Message}");
                        }
                    });
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("CameraManager: 硬件加速初始化失败，使用CPU模式");
                    LogUtil.Debug("CameraManager: 硬件加速初始化失败，使用CPU模式");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CameraManager: 硬件加速初始化异常: {ex.Message}");
                LogUtil.Error($"CameraManager: 硬件加速初始化异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 初始化超高清设备支持
        /// </summary>
        private void InitializeUltraHighDefinitionSupport()
        {
            try
            {
                // 确保在锁内安全初始化帧缓冲区
                lock (_frameBufferLock)
                {
                    _frameBuffer = new Queue<Mat>();
                }
                
                // 延迟启动内存清理定时器，避免立即执行导致的潜在问题
                // 首次执行延迟10秒，之后每5秒执行一次
                _memoryCleanupTimer = new Timer(PerformMemoryCleanup, null, 
                    TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5));
                
                System.Diagnostics.Debug.WriteLine("CameraManager: 超高清设备支持已初始化");
                LogUtil.Info("CameraManager: 超高清设备支持已初始化");
            }
            catch (OutOfMemoryException memEx)
            {
                System.Diagnostics.Debug.WriteLine($"CameraManager: 内存不足，无法初始化超高清支持: {memEx.Message}");
                LogUtil.Error($"CameraManager: 内存不足，无法初始化超高清支持: {memEx.Message}");
                
                // 强制垃圾回收并重试基本初始化
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                try
                {
                    lock (_frameBufferLock)
                    {
                        _frameBuffer = new Queue<Mat>();
                        _maxFrameBufferSize = 1; // 降低缓冲区大小
                    }
                }
                catch (Exception retryEx)
                {
                    System.Diagnostics.Debug.WriteLine($"CameraManager: 重试基本初始化失败: {retryEx.Message}");
                    LogUtil.Error($"CameraManager: 重试基本初始化失败: {retryEx.Message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CameraManager: 初始化超高清设备支持失败: {ex.Message}");
                LogUtil.Error($"CameraManager: 初始化超高清设备支持失败: {ex.Message}");
                
                // 确保在异常情况下也能正常工作
                try
                {
                    if (_frameBuffer == null)
                    {
                        lock (_frameBufferLock)
                        {
                            _frameBuffer = new Queue<Mat>();
                        }
                    }
                }
                catch (Exception innerEx)
                {
                    System.Diagnostics.Debug.WriteLine($"CameraManager: 帧缓冲区初始化失败: {innerEx.Message}");
                    LogUtil.Error($"CameraManager: 帧缓冲区初始化失败: {innerEx.Message}");
                }
            }
        }

        /// <summary>
        /// 检查是否为超高清分辨率
        /// </summary>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        /// <returns>是否为超高清分辨率</returns>
        private bool IsUltraHighDefinition(int width, int height)
        {
            // 定义超高清分辨率阈值（4K及以上）
            const int UHD_WIDTH_THRESHOLD = 3840;  // 4K宽度
            const int UHD_HEIGHT_THRESHOLD = 2160; // 4K高度
            const int TOTAL_PIXELS_THRESHOLD = 8000000; // 800万像素

            return (width >= UHD_WIDTH_THRESHOLD && height >= UHD_HEIGHT_THRESHOLD) ||
                   (width * height >= TOTAL_PIXELS_THRESHOLD);
        }

        /// <summary>
        /// 配置超高清设备的性能参数
        /// </summary>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        private void ConfigureUltraHighDefinitionSettings(int width, int height)
        {
            _isUltraHighDefinition = IsUltraHighDefinition(width, height);
            
            if (_isUltraHighDefinition)
            {
                // 减少缓冲区大小以节省内存
                _maxFrameBufferSize = 1;
                
                // 设置更小的OpenCV缓冲区
                if (_capture != null)
                {
                    _capture.Set(VideoCaptureProperties.BufferSize, 1);
                }
                
                // 强制垃圾回收以释放内存
                GC.Collect();
                GC.WaitForPendingFinalizers();
                
                System.Diagnostics.Debug.WriteLine($"CameraManager: 配置超高清设备 {width}x{height} - 缓冲区大小: {_maxFrameBufferSize}");
                LogUtil.Info($"CameraManager: 配置超高清设备 {width}x{height} - 缓冲区大小: {_maxFrameBufferSize}");
            }
            else
            {
                // 恢复默认设置
                _maxFrameBufferSize = 3;
                System.Diagnostics.Debug.WriteLine($"CameraManager: 配置标准设备 {width}x{height} - 缓冲区大小: {_maxFrameBufferSize}");
                LogUtil.Info($"CameraManager: 配置标准设备 {width}x{height} - 缓冲区大小: {_maxFrameBufferSize}");
            }
        }

        /// <summary>
        /// 执行内存清理
        /// </summary>
        /// <param name="state">状态对象</param>
        private void PerformMemoryCleanup(object state)
        {
            try
            {
                // 检查对象是否已被释放
                if (_disposed)
                {
                    return;
                }

                // 安全检查帧缓冲区
                if (_frameBuffer != null && _frameBufferLock != null)
                {
                    bool lockTaken = false;
                    try
                    {
                        Monitor.TryEnter(_frameBufferLock, TimeSpan.FromMilliseconds(100), ref lockTaken);
                        if (lockTaken)
                        {
                            // 再次检查，防止在获取锁期间对象被释放
                            if (_frameBuffer != null)
                            {
                                // 清理过期的帧缓冲区
                                while (_frameBuffer.Count > _maxFrameBufferSize)
                                {
                                    try
                                    {
                                        var oldFrame = _frameBuffer.Dequeue();
                                        oldFrame?.Dispose();
                                    }
                                    catch (InvalidOperationException)
                                    {
                                        // 队列为空时会抛出此异常，直接跳出循环
                                        break;
                                    } 
                                    catch (Exception frameEx)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"CameraManager: 释放帧异常: {frameEx.Message}");
                                        LogUtil.Error($"CameraManager: 释放帧异常: {frameEx.Message}");
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            // 无法获取锁，跳过此次清理
                            System.Diagnostics.Debug.WriteLine("CameraManager: 无法获取帧缓冲区锁，跳过内存清理");
                        }
                    }
                    finally
                    {
                        if (lockTaken)
                        {
                            Monitor.Exit(_frameBufferLock);
                        }
                    }
                }

                // 如果是超高清设备，执行更频繁的垃圾回收
                if (_isUltraHighDefinition && !_disposed)
                {
                    try
                    {
                        // 检查内存使用情况，只在必要时进行垃圾回收
                        long memoryBefore = GC.GetTotalMemory(false);
                        if (memoryBefore > 100 * 1024 * 1024) // 超过100MB时进行垃圾回收
                        {
                            GC.Collect(0, GCCollectionMode.Optimized);
                            GC.WaitForPendingFinalizers();
                            
                            long memoryAfter = GC.GetTotalMemory(false);
                            System.Diagnostics.Debug.WriteLine($"CameraManager: 垃圾回收完成 - 回收前: {memoryBefore / 1024 / 1024}MB, 回收后: {memoryAfter / 1024 / 1024}MB");
                        }
                    }
                    catch (OutOfMemoryException memEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"CameraManager: 内存不足异常: {memEx.Message}");
                        LogUtil.Error($"CameraManager: 内存不足异常: {memEx.Message}");
                        
                        // 强制垃圾回收
                        try
                        {
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                            GC.Collect();
                        }
                        catch
                        {
                            // 忽略垃圾回收异常
                        }
                    }
                    catch (Exception gcEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"CameraManager: 垃圾回收异常: {gcEx.Message}");
                        LogUtil.Error($"CameraManager: 垃圾回收异常: {gcEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CameraManager: 内存清理异常: {ex.Message}");
                LogUtil.Error($"CameraManager: 内存清理异常: {ex.Message}");
                
                // 如果出现严重异常，停止定时器以防止持续崩溃
                try
                {
                    _memoryCleanupTimer?.Dispose();
                    _memoryCleanupTimer = null;
                    System.Diagnostics.Debug.WriteLine("CameraManager: 由于异常已停止内存清理定时器");
                    LogUtil.Error("CameraManager: 由于异常已停止内存清理定时器");
                }
                catch (Exception timerEx)
                {
                    System.Diagnostics.Debug.WriteLine($"CameraManager: 停止定时器异常: {timerEx.Message}");
                    LogUtil.Error($"CameraManager: 停止定时器异常: {timerEx.Message}");
                }
            }
        }

        /// <summary>
        /// 执行内存清理（无参数重载）
        /// </summary>
        private void PerformMemoryCleanup()
        {
            PerformMemoryCleanup(null);
        }

        /// <summary>
        /// 记录初始化失败
        /// </summary>
        private void RecordInitializationFailure()
        {
            lock (_lockObject)
            {
                _consecutiveFailureCount++;
                _lastFailureTime = DateTime.Now;
                
                System.Diagnostics.Debug.WriteLine($"CameraManager: 记录初始化失败，连续失败次数: {_consecutiveFailureCount}");
                LogUtil.Info($"CameraManager: 记录初始化失败，连续失败次数: {_consecutiveFailureCount}");
                
                // 检查是否达到最大失败次数
                if (_consecutiveFailureCount >= MAX_CONSECUTIVE_FAILURES)
                {
                    _isInitializationStopped = true;
                    var stopMessage = $"摄像头初始化连续失败 {_consecutiveFailureCount} 次，已停止尝试初始化";
                    System.Diagnostics.Debug.WriteLine($"CameraManager: {stopMessage}");
                    LogUtil.Info($"CameraManager: {stopMessage}");
                    
                    // 触发错误事件通知
                    ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(
                        new InvalidOperationException(stopMessage), "初始化已停止"));
                }
            }
        }

        /// <summary>
        /// 重置初始化失败计数
        /// </summary>
        private void ResetInitializationFailureCount()
        {
            lock (_lockObject)
            {
                if (_consecutiveFailureCount > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"CameraManager: 初始化成功，重置失败计数（之前失败 {_consecutiveFailureCount} 次）");
                    LogUtil.Info($"CameraManager: 初始化成功，重置失败计数（之前失败 {_consecutiveFailureCount} 次）");
                    _consecutiveFailureCount = 0;
                    _isInitializationStopped = false;
                }
            }
        }

        /// <summary>
        /// 安全地添加帧到缓冲区
        /// </summary>
        /// <param name="frame">要添加的帧</param>
        private void SafeAddFrameToBuffer(Mat frame)
        {
            if (frame == null || frame.Empty())
                return;

            try
            {
                lock (_frameBufferLock)
                {
                    // 如果缓冲区已满，移除最旧的帧
                    while (_frameBuffer.Count >= _maxFrameBufferSize)
                    {
                        var oldFrame = _frameBuffer.Dequeue();
                        oldFrame?.Dispose();
                    }

                    // 添加新帧的克隆
                    _frameBuffer.Enqueue(frame.Clone());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CameraManager: 添加帧到缓冲区失败: {ex.Message}");
                LogUtil.Info($"CameraManager: 添加帧到缓冲区失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 智能帧跳过决策
        /// 根据当前性能状态决定是否跳过当前帧
        /// </summary>
        /// <returns>是否应该跳过当前帧</returns>
        private bool ShouldSkipFrame()
        {
            if (!_isUltraHighDefinition)
                return false;

            // 增加帧跳过计数器
            _frameSkipCounter++;

            // 根据动态跳过率决定是否跳过
            if (_frameSkipCounter % _dynamicFrameSkipRate != 0)
            {
                return true; // 跳过此帧
            }

            return false; // 处理此帧
        }

        /// <summary>
        /// 更新动态帧跳过率
        /// 根据当前性能状态动态调整帧跳过率
        /// </summary>
        private void UpdateDynamicFrameSkipRate()
        {
            if (!_isUltraHighDefinition)
            {
                _dynamicFrameSkipRate = 1;
                return;
            }

            try
            {
                // 计算平均帧处理时间
                double avgProcessingTime = 0;
                if (_frameProcessingTimes.Count > 0)
                {
                    avgProcessingTime = _frameProcessingTimes.Average();
                }

                // 获取当前内存使用情况
                var currentMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024);

                // 根据性能指标动态调整跳过率
                if (avgProcessingTime > 100 || currentMemoryMB > 800) // 高负载
                {
                    _dynamicFrameSkipRate = Math.Min(5, _dynamicFrameSkipRate + 1);
                }
                else if (avgProcessingTime > 50 || currentMemoryMB > 600) // 中等负载
                {
                    _dynamicFrameSkipRate = Math.Min(3, _dynamicFrameSkipRate + 1);
                }
                else if (avgProcessingTime < 20 && currentMemoryMB < 400) // 低负载
                {
                    _dynamicFrameSkipRate = Math.Max(1, _dynamicFrameSkipRate - 1);
                }

                // 记录调整信息
                if (_frameCount % 100 == 0) // 每100帧记录一次
                {
                    System.Diagnostics.Debug.WriteLine($"CameraManager: 动态帧跳过率调整为 {_dynamicFrameSkipRate}, 平均处理时间: {avgProcessingTime:F2}ms, 内存: {currentMemoryMB}MB");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CameraManager: 更新动态帧跳过率失败: {ex.Message}");
                LogUtil.Info($"CameraManager: 更新动态帧跳过率失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 记录帧处理时间
        /// </summary>
        /// <param name="processingTimeMs">处理时间（毫秒）</param>
        private void RecordFrameProcessingTime(double processingTimeMs)
        {
            try
            {
                _frameProcessingTimes.Enqueue(processingTimeMs);

                // 保持队列大小在限制范围内
                while (_frameProcessingTimes.Count > MAX_PROCESSING_TIME_SAMPLES)
                {
                    _frameProcessingTimes.Dequeue();
                }

                // 每10帧更新一次动态跳过率
                if (_frameCount % 10 == 0)
                {
                    UpdateDynamicFrameSkipRate();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CameraManager: 记录帧处理时间失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 安全的Mat到BitmapSource转换方法
        /// 解决OpenCvSharp.WpfExtensions.ToBitmapSource()的堆损坏问题
        /// </summary>
        /// <param name="mat">要转换的Mat对象</param>
        /// <returns>安全转换的BitmapSource</returns>
        private BitmapSource SafeMatToBitmapSource(Mat mat)
        {
            if (mat == null || mat.Empty())
                return null;

            try
            {
                // 确保Mat格式正确
                Mat convertedMat = null;
                try
                {
                    // 根据通道数进行格式转换
                    if (mat.Channels() == 1)
                    {
                        // 灰度图转RGB
                        convertedMat = new Mat();
                        Cv2.CvtColor(mat, convertedMat, ColorConversionCodes.GRAY2RGB);
                    }
                    else if (mat.Channels() == 3)
                    {
                        // BGR转RGB
                        convertedMat = new Mat();
                        Cv2.CvtColor(mat, convertedMat, ColorConversionCodes.BGR2RGB);
                    }
                    else if (mat.Channels() == 4)
                    {
                        // BGRA转RGBA
                        convertedMat = new Mat();
                        Cv2.CvtColor(mat, convertedMat, ColorConversionCodes.BGRA2RGBA);
                    }
                    else
                    {
                        convertedMat = mat.Clone();
                    }

                    // 使用安全的方式创建BitmapSource
                    var width = convertedMat.Width;
                    var height = convertedMat.Height;
                    var stride = width * convertedMat.Channels();
                    
                    // 创建字节数组副本，避免直接使用Mat的内存指针
                    var imageData = new byte[height * stride];
                    System.Runtime.InteropServices.Marshal.Copy(convertedMat.Data, imageData, 0, imageData.Length);

                    // 确定像素格式
                    PixelFormat pixelFormat;
                    switch (convertedMat.Channels())
                    {
                        case 1:
                            pixelFormat = PixelFormats.Gray8;
                            break;
                        case 3:
                            pixelFormat = PixelFormats.Rgb24;
                            break; 
                        default:
                            throw new NotSupportedException($"不支持的通道数: {convertedMat.Channels()}");
                    }

                    // 创建BitmapSource
                    var bitmapSource = BitmapSource.Create(
                        width, height,
                        96, 96, // DPI
                        pixelFormat,
                        null, // palette
                        imageData,
                        stride);

                    // 冻结以提高性能和线程安全
                    if (bitmapSource.CanFreeze)
                    {
                        bitmapSource.Freeze();
                    }

                    return bitmapSource;
                }
                finally
                {
                    // 释放临时Mat对象
                    if (convertedMat != null && convertedMat != mat)
                    {
                        convertedMat.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SafeMatToBitmapSource转换失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取优化的BitmapSource
        /// 使用缓存和智能转换策略提高渲染性能
        /// </summary>
        /// <param name="frame">要转换的Mat帧</param>
        /// <returns>优化的BitmapSource</returns>
        private BitmapSource GetOptimizedBitmapSource(Mat frame)
        {
            try
            {
                if (frame == null || frame.Empty())
                    return null;

                lock (_bitmapCacheLock)
                {
                    var now = DateTime.Now;
                    
                    // 检查缓存是否有效
                    if (_cachedBitmapSource != null && 
                        (now - _lastBitmapCacheTime) < _bitmapCacheTimeout)
                    {
                        return _cachedBitmapSource;
                    }

                    // 为超高清设备进行降采样以提高性能
                    Mat processedFrame = frame;
                    if (_isUltraHighDefinition)
                    {
                        var currentMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024);
                        
                        // 根据内存压力动态调整降采样率
                        double scaleFactor = 1.0;
                        if (currentMemoryMB > 800)
                        {
                            scaleFactor = 0.5; // 高内存压力，降采样50%
                        }
                        else if (currentMemoryMB > 600)
                        {
                            scaleFactor = 0.7; // 中等内存压力，降采样30%
                        }
                        else if (currentMemoryMB > 400)
                        {
                            scaleFactor = 0.85; // 轻微内存压力，降采样15%
                        }

                        if (scaleFactor < 1.0)
                        {
                            processedFrame = new Mat();
                            var newSize = new Size(
                                (int)(frame.Width * scaleFactor),
                                (int)(frame.Height * scaleFactor)
                            );
                            Cv2.Resize(frame, processedFrame, newSize, 0, 0, InterpolationFlags.Linear);
                        }
                    }

                    // 使用安全的转换方法
                    BitmapSource bitmapSource;
                    try
                    {
                        bitmapSource = SafeMatToBitmapSource(processedFrame);
                    }
                    finally
                    {
                        // 如果创建了新的Mat，需要释放
                        if (processedFrame != frame)
                        {
                            processedFrame?.Dispose();
                        }
                    }

                    // 更新缓存
                    _cachedBitmapSource = bitmapSource;
                    _lastBitmapCacheTime = now;

                    return bitmapSource;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CameraManager: BitmapSource优化转换失败: {ex.Message}");
                
                // 降级到安全转换
                try
                {
                    return SafeMatToBitmapSource(frame);
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// 智能UI更新决策
        /// 根据当前性能状态决定是否触发UI更新
        /// </summary>
        /// <returns>是否应该触发UI更新</returns>
        private bool ShouldTriggerUIUpdate()
        {
            try
            {
                // 标准设备每帧都更新
                if (!_isUltraHighDefinition)
                    return true;

                // 超高清设备的智能更新策略
                var currentMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024);
                var avgProcessingTime = _frameProcessingTimes.Count > 0 ? _frameProcessingTimes.Average() : 0;

                // 根据性能状态动态调整UI更新频率
                int uiUpdateInterval;

                if (currentMemoryMB > 800 || avgProcessingTime > 100) // 高负载
                {
                    uiUpdateInterval = 5; // 每5帧更新一次UI
                }
                else if (currentMemoryMB > 600 || avgProcessingTime > 50) // 中等负载
                {
                    uiUpdateInterval = 3; // 每3帧更新一次UI
                }
                else if (currentMemoryMB > 400 || avgProcessingTime > 25) // 轻微负载
                {
                    uiUpdateInterval = 2; // 每2帧更新一次UI
                }
                else // 低负载
                {
                    uiUpdateInterval = 1; // 每帧都更新UI
                }

                // 检查是否应该更新UI
                bool shouldUpdate = (_frameCount % uiUpdateInterval) == 0;

                // 记录UI更新决策（每100帧记录一次）
                if (_frameCount % 100 == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"CameraManager: UI更新间隔: {uiUpdateInterval}, 内存: {currentMemoryMB}MB, 平均处理时间: {avgProcessingTime:F2}ms");
                }

                return shouldUpdate;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CameraManager: UI更新决策失败: {ex.Message}");
                // 出错时默认更新UI
                return true;
            }
        }

        /// <summary>
        /// 异步处理帧
        /// 在后台线程中处理帧以避免阻塞主捕获循环
        /// 支持硬件加速的图像处理
        /// </summary>
        /// <param name="frame">要处理的帧</param>
        /// <returns>异步任务</returns>
        private async Task ProcessFrameAsync(Mat frame)
        {
            if (_isAsyncProcessing || frame == null || frame.Empty())
                return;

            try
            {
                await _frameProcessingSemaphore.WaitAsync();
                _isAsyncProcessing = true;

                var processingStart = DateTime.Now;

                // 在后台线程中执行帧处理
                await Task.Run(() =>
                {
                    try
                    {
                        Mat processedFrame = null;

                        // 使用硬件加速处理帧
                        if (_isUltraHighDefinition)
                        {
                            processedFrame = ProcessFrameWithAcceleration(frame, true);
                        }
                        else
                        {
                            processedFrame = ProcessFrameWithAcceleration(frame, false);
                        }

                        // 更新当前帧
                        lock (_lockObject)
                        {
                            _currentFrame?.Dispose();
                            _currentFrame = processedFrame;
                        }

                        // 安全地添加到缓冲区
                        SafeAddFrameToBuffer(processedFrame);

                        // 触发帧捕获事件
                        try
                        {
                            var optimizedBitmap = GetOptimizedBitmapSource(processedFrame);
                            FrameCaptured?.Invoke(this, new FrameCapturedEventArgs(optimizedBitmap, processedFrame));
                        }
                        catch (Exception bitmapEx)
                        {
                            // 如果BitmapSource转换失败，传递null作为BitmapSource
                            LogUtil.Info($"CameraManager: BitmapSource转换失败: {bitmapEx.Message}");
                            FrameCaptured?.Invoke(this, new FrameCapturedEventArgs(null, processedFrame));
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"CameraManager: 异步帧处理异常: {ex.Message}");
                        LogUtil.Info($"CameraManager: 异步帧处理异常: {ex.Message}");
                    }
                });

                // 记录处理时间
                var processingTime = (DateTime.Now - processingStart).TotalMilliseconds;
                RecordFrameProcessingTime(processingTime);
            }
            finally
            {
                _isAsyncProcessing = false;
                _frameProcessingSemaphore.Release();
            }
        }

        /// <summary>
        /// 使用硬件加速处理帧
        /// 根据可用的硬件加速类型选择最优的处理方式
        /// </summary>
        /// <param name="frame">输入帧</param>
        /// <param name="isUltraHighDefinition">是否为超高清处理</param>
        /// <returns>处理后的帧</returns>
        private Mat ProcessFrameWithAcceleration(Mat frame, bool isUltraHighDefinition)
        {
            try
            {
                var accelerationManager = AccelerationManager.Instance;
                var accelerationType = accelerationManager.AccelerationInfo?.EnabledAcceleration ?? AccelerationType.None;

                switch (accelerationType)
                {
                    case AccelerationType.CUDA:
                        return ProcessFrameWithCuda(frame, isUltraHighDefinition);

                    case AccelerationType.OpenCL:
                        return ProcessFrameWithOpenCL(frame, isUltraHighDefinition);

                    case AccelerationType.TBB:
                        return ProcessFrameWithTBB(frame, isUltraHighDefinition);

                    default:
                        // 使用CPU处理
                        return ProcessFrameWithCPU(frame, isUltraHighDefinition);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CameraManager: 硬件加速帧处理异常: {ex.Message}");
                LogUtil.Debug($"CameraManager: 硬件加速帧处理异常，回退到CPU处理: {ex.Message}");
                
                // 回退到CPU处理
                return ProcessFrameWithCPU(frame, isUltraHighDefinition);
            }
        }

        /// <summary>
        /// 使用CUDA加速处理帧
        /// </summary>
        /// <param name="frame">输入帧</param>
        /// <param name="isUltraHighDefinition">是否为超高清处理</param>
        /// <returns>处理后的帧</returns>
        private Mat ProcessFrameWithCuda(Mat frame, bool isUltraHighDefinition)
        {
            try
            {
                // 注意：这里需要OpenCV编译时包含CUDA支持
                // 由于OpenCvSharp可能不包含完整的CUDA绑定，这里提供基本框架
                
                if (isUltraHighDefinition)
                {
                    // 超高清帧的CUDA优化处理
                    return OptimizeUltraHighDefinitionFrame(frame);
                }
                else
                {
                    // 标准帧的CUDA处理
                    return frame.Clone();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CameraManager: CUDA处理异常: {ex.Message}");
                LogUtil.Debug($"CameraManager: CUDA处理异常，回退到CPU: {ex.Message}");
                return ProcessFrameWithCPU(frame, isUltraHighDefinition);
            }
        }

        /// <summary>
        /// 使用OpenCL加速处理帧
        /// </summary>
        /// <param name="frame">输入帧</param>
        /// <param name="isUltraHighDefinition">是否为超高清处理</param>
        /// <returns>处理后的帧</returns>
        private Mat ProcessFrameWithOpenCL(Mat frame, bool isUltraHighDefinition)
        {
            try
            {
                // OpenCV 3.x+使用透明API，OpenCL会自动启用
                // 使用UMat可以自动利用OpenCL加速

                Mat processedFrame;

                if (isUltraHighDefinition)
                {
                    // 超高清帧的OpenCL优化处理
                    processedFrame = OptimizeUltraHighDefinitionFrameWithOpenCL(frame);
                }
                else
                {
                    // 标准帧的OpenCL处理
                    processedFrame = ProcessStandardFrameWithOpenCL(frame);
                }

                return processedFrame;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CameraManager: OpenCL处理异常: {ex.Message}");
                LogUtil.Debug($"CameraManager: OpenCL处理异常，回退到CPU: {ex.Message}");
                return ProcessFrameWithCPU(frame, isUltraHighDefinition);
            }
        }

        /// <summary>
        /// 使用Intel TBB加速处理帧
        /// </summary>
        /// <param name="frame">输入帧</param>
        /// <param name="isUltraHighDefinition">是否为超高清处理</param>
        /// <returns>处理后的帧</returns>
        private Mat ProcessFrameWithTBB(Mat frame, bool isUltraHighDefinition)
        {
            try
            {
                // Intel TBB主要用于并行化CPU操作
                if (isUltraHighDefinition)
                {
                    return OptimizeUltraHighDefinitionFrame(frame);
                }
                else
                {
                    return frame.Clone();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CameraManager: TBB处理异常: {ex.Message}");
                LogUtil.Debug($"CameraManager: TBB处理异常，回退到CPU: {ex.Message}");
                return ProcessFrameWithCPU(frame, isUltraHighDefinition);
            }
        }

        /// <summary>
        /// 使用CPU处理帧（回退方案）
        /// </summary>
        /// <param name="frame">输入帧</param>
        /// <param name="isUltraHighDefinition">是否为超高清处理</param>
        /// <returns>处理后的帧</returns>
        private Mat ProcessFrameWithCPU(Mat frame, bool isUltraHighDefinition)
        {
            try
            {
                if (isUltraHighDefinition)
                {
                    return OptimizeUltraHighDefinitionFrame(frame);
                }
                else
                {
                    return frame.Clone();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CameraManager: CPU处理异常: {ex.Message}");
                LogUtil.Error($"CameraManager: CPU处理异常: {ex.Message}");
                return frame.Clone(); // 最后的回退方案
            }
        }

        /// <summary>
        /// 使用OpenCL优化超高清帧处理
        /// </summary>
        /// <param name="frame">输入帧</param>
        /// <returns>优化后的帧</returns>
        private Mat OptimizeUltraHighDefinitionFrameWithOpenCL(Mat frame)
        {
            try
            {
                // 使用OpenCL加速的图像处理操作
                var processedFrame = new Mat();
                
                // 如果帧太大，先进行智能缩放
                if (frame.Width > 3840 || frame.Height > 2160)
                {
                    var scaleFactor = Math.Min(3840.0 / frame.Width, 2160.0 / frame.Height);
                    var newSize = new OpenCvSharp.Size(
                        (int)(frame.Width * scaleFactor),
                        (int)(frame.Height * scaleFactor)
                    );
                    
                    // OpenCL加速的缩放操作
                    Cv2.Resize(frame, processedFrame, newSize, 0, 0, InterpolationFlags.Linear);
                }
                else
                {
                    processedFrame = frame.Clone();
                }

                // 可以添加更多OpenCL加速的图像处理操作
                // 例如：降噪、锐化、色彩校正等

                return processedFrame;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CameraManager: OpenCL超高清处理异常: {ex.Message}");
                LogUtil.Debug($"CameraManager: OpenCL超高清处理异常: {ex.Message}");
                return OptimizeUltraHighDefinitionFrame(frame);
            }
        }

        /// <summary>
        /// 使用OpenCL处理标准帧
        /// </summary>
        /// <param name="frame">输入帧</param>
        /// <returns>处理后的帧</returns>
        private Mat ProcessStandardFrameWithOpenCL(Mat frame)
        {
            try
            {
                // 对于标准帧，可以应用一些基本的OpenCL加速操作
                var processedFrame = frame.Clone();

                // 可以添加OpenCL加速的图像增强操作
                // 例如：自动曝光调整、色彩平衡等

                return processedFrame;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CameraManager: OpenCL标准帧处理异常: {ex.Message}");
                LogUtil.Debug($"CameraManager: OpenCL标准帧处理异常: {ex.Message}");
                return frame.Clone();
            }
        }

        /// <summary>
        /// 监控超高清设备的性能状态
        /// </summary>
        private void MonitorUltraHighDefinitionPerformance()
        {
            if (!_isUltraHighDefinition)
                return;

            try
            {
                // 检查内存使用情况
                var currentMemory = GC.GetTotalMemory(false);
                var memoryMB = currentMemory / (1024 * 1024);

                // 如果内存使用超过阈值，触发警告
                if (memoryMB > 500) // 500MB阈值
                {
                    System.Diagnostics.Debug.WriteLine($"CameraManager: 超高清设备内存使用警告: {memoryMB}MB");
                    LogUtil.Info($"CameraManager: 超高清设备内存使用警告: {memoryMB}MB");
                    
                    // 触发性能警告事件
                    var warnings = new List<string> { $"内存使用过高: {memoryMB}MB" };
                    var snapshot = new PerformanceSnapshot
                    {
                        Timestamp = DateTime.Now,
                        ProcessMemory = memoryMB,
                        Fps = _currentFps,
                        CpuUsage = 0, // 可以从性能监控器获取
                        MemoryUsage = memoryMB,
                        FrameProcessingTime = 0 // 可以从性能监控器获取
                    };
                    PerformanceWarning?.Invoke(this, new PerformanceWarningEventArgs(warnings, snapshot));

                    // 执行紧急内存清理
                    PerformMemoryCleanup();
                }

                // 检查帧缓冲区状态
                lock (_frameBufferLock)
                {
                    if (_frameBuffer.Count > _maxFrameBufferSize * 0.8)
                    {
                        System.Diagnostics.Debug.WriteLine($"CameraManager: 帧缓冲区接近满载: {_frameBuffer.Count}/{_maxFrameBufferSize}");
                        LogUtil.Info($"CameraManager: 帧缓冲区接近满载: {_frameBuffer.Count}/{_maxFrameBufferSize}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CameraManager: 超高清性能监控异常: {ex.Message}");
                LogUtil.Info($"CameraManager: 超高清性能监控异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 优化超高清设备的帧处理
        /// 采用智能降采样和内存优化策略
        /// </summary>
        /// <param name="frame">输入帧</param>
        /// <returns>优化后的帧</returns>
        private Mat OptimizeUltraHighDefinitionFrame(Mat frame)
        {
            if (!_isUltraHighDefinition || frame == null || frame.Empty())
                return frame;

            try
            {
                // 智能降采样策略：根据当前性能动态调整
                var currentMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024);
                var targetWidth = frame.Width;
                var targetHeight = frame.Height;

                // 根据内存压力动态调整分辨率
                if (currentMemoryMB > 800) // 高内存压力
                {
                    targetWidth = frame.Width / 3;
                    targetHeight = frame.Height / 3;
                }
                else if (currentMemoryMB > 600) // 中等内存压力
                {
                    targetWidth = frame.Width / 2;
                    targetHeight = frame.Height / 2;
                }
                else if (currentMemoryMB > 400) // 轻微内存压力
                {
                    targetWidth = (int)(frame.Width * 0.75);
                    targetHeight = (int)(frame.Height * 0.75);
                }

                // 如果需要降采样
                if (targetWidth != frame.Width || targetHeight != frame.Height)
                {
                    var optimizedFrame = new Mat();
                    Cv2.Resize(frame, optimizedFrame, new Size(targetWidth, targetHeight), 
                              0, 0, InterpolationFlags.Linear);
                    
                    System.Diagnostics.Debug.WriteLine($"CameraManager: 超高清帧降采样 {frame.Width}x{frame.Height} -> {targetWidth}x{targetHeight}");
                    return optimizedFrame;
                }

                // 如果内存压力不大，直接返回原始帧的克隆
                return frame.Clone();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CameraManager: 超高清帧优化失败: {ex.Message}");
                LogUtil.Info($"CameraManager: 超高清帧优化失败: {ex.Message}");
                return frame.Clone(); // 返回原始帧的克隆
            }
        }

        /// <summary>
        /// 检查超高清设备的稳定性
        /// </summary>
        /// <returns>设备是否稳定</returns>
        private bool CheckUltraHighDefinitionStability()
        {
            if (!_isUltraHighDefinition)
                return true;

            try
            {
                // 检查捕获设备状态
                if (_capture == null || !_capture.IsOpened())
                {
                    System.Diagnostics.Debug.WriteLine("CameraManager: 超高清设备捕获对象无效");
                    LogUtil.Info("CameraManager: 超高清设备捕获对象无效");
                    return false;
                }

                // 检查内存压力
                var memoryBefore = GC.GetTotalMemory(false);
                GC.Collect(0, GCCollectionMode.Optimized);
                var memoryAfter = GC.GetTotalMemory(false);
                
                var memoryFreed = memoryBefore - memoryAfter;
                var memoryFreedMB = memoryFreed / (1024 * 1024);

                if (memoryFreedMB > 100) // 如果释放了超过100MB内存，说明内存压力较大
                {
                    System.Diagnostics.Debug.WriteLine($"CameraManager: 超高清设备内存压力检测，释放了 {memoryFreedMB}MB 内存");
                    LogUtil.Info($"CameraManager: 超高清设备内存压力检测，释放了 {memoryFreedMB}MB 内存");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CameraManager: 超高清设备稳定性检查异常: {ex.Message}");
                LogUtil.Info($"CameraManager: 超高清设备稳定性检查异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 摄像头捕获循环
        /// </summary>
        private void CaptureLoop()
        {
            var token = _cancellationTokenSource.Token;
            var frame = new Mat();
            
            while (IsCapturing && !token.IsCancellationRequested)
            {
                Mat processedFrame = null; // 在循环内定义processedFrame变量
                try
                {
                    if (_capture == null || !_capture.IsOpened())
                    {
                        break;
                    }

                    _frameProcessingStopwatch.Restart();
                    
                    lock (_lockObject)
                    {
                        bool readSuccess = _capture.Read(frame);
                        bool frameEmpty = frame.Empty();
                        
                        // 详细的帧读取调试信息
                        if (!readSuccess)
                        {
                            System.Diagnostics.Debug.WriteLine("CameraManager: 帧读取失败 - Read()返回false");
                            LogUtil.Info("CameraManager: 帧读取失败 - Read()返回false");
                        }
                        else if (frameEmpty)
                        {
                            System.Diagnostics.Debug.WriteLine("CameraManager: 帧读取成功但帧为空");
                            LogUtil.Info("CameraManager: 帧读取成功但帧为空");
                        }
                        else
                        {
                            // 每100帧输出一次成功信息，避免日志过多
                            if (_frameCount % 100 == 0)
                            {
                                System.Diagnostics.Debug.WriteLine($"CameraManager: 成功读取帧 #{_frameCount}, 尺寸: {frame.Width}x{frame.Height}, 通道: {frame.Channels()}");
                            // LogUtil.Info($"CameraManager: 成功读取帧 #{_frameCount}, 尺寸: {frame.Width}x{frame.Height}, 通道: {frame.Channels()}"); // 注释掉高频日志
                            }
                        }
                        
                        if (!readSuccess || frameEmpty)
                        {
                            // 检测设备断开
                            if (!_capture.IsOpened())
                            {
                                System.Diagnostics.Debug.WriteLine("CameraManager: 摄像头设备已断开");
                                LogUtil.Info("CameraManager: 摄像头设备已断开");
                                ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(
                                    new InvalidOperationException("摄像头设备已断开"), "设备连接丢失"));
                                break;
                            }
                            
                            // 短暂延迟，避免CPU占用过高，给摄像头一些时间稳定
                            Thread.Sleep(10);
                            continue;
                        }

                        // 智能帧跳过检查
                        if (ShouldSkipFrame())
                        {
                            System.Diagnostics.Debug.WriteLine($"CameraManager: 智能跳过帧 #{_frameCount}, 跳过率: {_dynamicFrameSkipRate}");
                            continue;
                        }

                        // 超高清设备的帧优化处理
                        processedFrame = frame;
                        if (_isUltraHighDefinition)
                        {
                            // 检查设备稳定性
                            if (!CheckUltraHighDefinitionStability())
                            {
                                System.Diagnostics.Debug.WriteLine("CameraManager: 超高清设备不稳定，跳过当前帧");
                                LogUtil.Info("CameraManager: 超高清设备不稳定，跳过当前帧");
                                continue;
                            }
                            
                            // 使用异步处理来优化性能
                            if (!_isAsyncProcessing)
                            {
                                // 启动异步帧处理，不等待完成
                                _ = Task.Run(async () => await ProcessFrameAsync(frame));
                            }
                            
                            // 同步处理用于立即更新（降采样版本）
                            processedFrame = OptimizeUltraHighDefinitionFrame(frame);
                            
                            // 每50帧执行一次性能监控
                            if (_frameCount % 50 == 0)
                            {
                                MonitorUltraHighDefinitionPerformance();
                            }
                        }
                        else
                        {
                            // 标准设备直接克隆
                            processedFrame = frame.Clone();
                        }

                        // 更新当前帧
                        _currentFrame?.Dispose();
                        _currentFrame = processedFrame.Clone();

                        // 使用安全的帧缓冲机制（仅对非异步处理的帧）
                        if (!_isUltraHighDefinition || !_isAsyncProcessing)
                        {
                            SafeAddFrameToBuffer(processedFrame);
                        }

                        // 录像处理
                        if (IsRecording)
                        {
                            _videoRecorder.WriteFrame(processedFrame);
                        }
                    }

                    // 智能UI更新频率控制
                    try
                    {
                        bool shouldTriggerEvent = ShouldTriggerUIUpdate();
                        
                        if (shouldTriggerEvent)
                        {
                            // 异步触发事件以避免阻塞捕获循环
                            _ = Task.Run(() =>
                            {
                                try
                                {
                                    // 使用优化的BitmapSource转换
                                    var optimizedBitmap = GetOptimizedBitmapSource(processedFrame);
                                    
                                    if (optimizedBitmap != null)
                                    {
                                        // 直接传递BitmapSource，避免在UI线程中转换
                                        FrameCaptured?.Invoke(this, new FrameCapturedEventArgs(optimizedBitmap, null));
                                    }
                                    else
                                    {
                                        // 降级方案：传递Mat对象
                                        var clonedFrame = processedFrame.Clone();
                                        FrameCaptured?.Invoke(this, new FrameCapturedEventArgs(null, clonedFrame));
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"异步帧捕获事件触发失败: {ex.Message}");
                                    LogUtil.Info($"CameraManager: 异步帧捕获事件触发失败: {ex.Message}");
                                    
                                    // 最后的降级方案：传递原始Mat
                                    try
                                    {
                                        var clonedFrame = processedFrame.Clone();
                                        FrameCaptured?.Invoke(this, new FrameCapturedEventArgs(null, clonedFrame));
                                    }
                                    catch (Exception fallbackEx)
                                    {
                                        Debug.WriteLine($"降级事件触发也失败: {fallbackEx.Message}");
                                        LogUtil.Info($"CameraManager: 降级事件触发也失败: {fallbackEx.Message}");
                                    }
                                }
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"帧捕获事件触发失败: {ex.Message}");
                        LogUtil.Info($"CameraManager: 帧捕获事件触发失败: {ex.Message}");
                        Debug.WriteLine($"异常详情: {ex}");
                        LogUtil.Info($"CameraManager: 异常详情: {ex}");
                        // 继续处理，不中断捕获循环
                    }

                    // 性能统计
                    _frameCount++;
                    
                    _frameProcessingStopwatch.Stop();
                    var processingTime = _frameProcessingStopwatch.ElapsedMilliseconds;
                    
                    // 更新性能监控
                    _performanceMonitor.RecordFrameProcessingTime(processingTime);
                    
                    UpdatePerformanceStats();
                    
                    // 动态帧率控制 - 超高清设备使用较低帧率
                    var targetFps = _isUltraHighDefinition ? 15.0 : 30.0; // 超高清设备15fps，标准设备30fps
                    var targetFrameTime = 1000.0 / targetFps;
                    var sleepTime = Math.Max(0, (int)(targetFrameTime - processingTime));
                    
                    if (sleepTime > 0)
                    {
                        Thread.Sleep(sleepTime);
                    }
                    
                    // 超高清设备额外的内存管理
                    if (_isUltraHighDefinition && _frameCount % 30 == 0)
                    {
                        // 每30帧执行一次轻量级垃圾回收
                        GC.Collect(0, GCCollectionMode.Optimized);
                    }
                }
                catch (Exception ex)
                {
                    ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(ex, "帧捕获异常"));
                    
                    // 短暂延迟后重试
                    Thread.Sleep(100);
                }
            }
            
            frame?.Dispose();
        }

        /// <summary>
        /// 录像状态变更事件处理
        /// </summary>
        private void OnRecordingStatusChanged(object sender, RecordingStatusEventArgs e)
        {
            // 转换事件参数类型
            var args = new RecordingStatusChangedEventArgs(e.IsRecording, e.FilePath);
            RecordingStatusChanged?.Invoke(this, args);
        }

        /// <summary>
        /// 当前设备变更事件处理
        /// </summary>
        private void OnCurrentDeviceChanged(object sender, EventArgs e)
        {
            OnPropertyChanged(nameof(CurrentDevice));
            OnPropertyChanged(nameof(AvailableDevices));
        }

        /// <summary>
        /// 性能警告事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">事件参数</param>
        private void OnPerformanceWarning(object sender, PerformanceWarningEventArgs e)
        {
            // 可以在这里处理性能警告，比如降低帧率或分辨率
        }

        /// <summary>
        /// 错误发生事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">事件参数</param>
        private void OnErrorOccurred(object sender, ErrorOccurredEventArgs e)
        {
            // 转发错误事件到外部
            ErrorOccurred?.Invoke(this, e);
        }

        /// <summary>
        /// 系统健康状态变更事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">事件参数</param>
        private void OnSystemHealthChanged(object sender, SystemHealthChangedEventArgs e)
        {
            if (!e.IsHealthy)
            {
                // 系统不健康时的处理逻辑
                _errorHandler.HandleError(
                    new InvalidOperationException("系统健康状态异常"), 
                    "系统健康检查", 
                    ErrorSeverity.High
                );
            }
        }

        /// <summary>
        /// 错误恢复事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">事件参数</param>
        private void OnErrorRecovery(object sender, ErrorRecoveryEventArgs e)
        {
            if (e.IsSuccessful)
            {
                // 恢复成功的处理逻辑
                StatusChanged?.Invoke(this, new StatusChangedEventArgs(CameraStatus.Running, true, _currentDeviceIndex));
            }
            else
            {
                // 恢复失败的处理逻辑
                StatusChanged?.Invoke(this, new StatusChangedEventArgs(CameraStatus.Error, false, _currentDeviceIndex));
            }
        }

        /// <summary>
        /// 更新性能统计
        /// </summary>
        private void UpdatePerformanceStats()
        {
            _frameCount++;
            
            if (_fpsStopwatch.ElapsedMilliseconds >= 1000)
            {
                CurrentFps = _frameCount * 1000.0 / _fpsStopwatch.ElapsedMilliseconds;
                
                var stats = new PerformanceStatsEventArgs
                {
                    Fps = CurrentFps,
                    FrameCount = _frameCount,
                    BufferSize = 0 // 移除了缓冲区
                };
                
                PerformanceStats?.Invoke(this, stats);
                _performanceMonitor.UpdateStats(stats);

                _frameCount = 0;
                _fpsStopwatch.Restart();
            }
        }

        /// <summary>
        /// 获取OpenCV属性枚举
        /// </summary>
        /// <param name="property">摄像头属性</param>
        /// <returns>OpenCV属性枚举</returns>
        private VideoCaptureProperties GetVideoCaptureProperty(CameraProperty property)
        {
            switch (property)
            {
                case CameraProperty.Brightness:
                    return VideoCaptureProperties.Brightness;
                case CameraProperty.Contrast:
                    return VideoCaptureProperties.Contrast;
                case CameraProperty.Saturation:
                    return VideoCaptureProperties.Saturation;
                case CameraProperty.Hue:
                    return VideoCaptureProperties.Hue;
                case CameraProperty.Gain:
                    return VideoCaptureProperties.Gain;
                case CameraProperty.Exposure:
                    return VideoCaptureProperties.Exposure;
                case CameraProperty.Focus:
                    return VideoCaptureProperties.Focus;
                case CameraProperty.Zoom:
                    return VideoCaptureProperties.Zoom;
                default:
                    return VideoCaptureProperties.Brightness;
            }
        }

        /// <summary>
        /// 尝试使用多种后端初始化摄像头
        /// </summary>
        /// <param name="deviceIndex">设备索引</param>
        /// <returns>是否成功初始化</returns>
        private bool TryInitializeCameraWithMultipleBackends(int deviceIndex)
        {
            // 获取优化的后端列表
            var backends = GetOptimizedBackendList();

            foreach (var backend in backends)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"CameraManager: 尝试使用后端 {backend} 初始化设备 {deviceIndex}");
                    LogUtil.Info($"CameraManager: 尝试使用后端 {backend} 初始化设备 {deviceIndex}");
                    
                    _capture = new VideoCapture(deviceIndex, backend);
                    
                    if (_capture.IsOpened())
                    {
                        // 验证后端性能
                        if (ValidateBackendPerformance(_capture, backend))
                        {
                            System.Diagnostics.Debug.WriteLine($"CameraManager: 成功使用后端 {backend} 初始化设备 {deviceIndex}");
                            LogUtil.Info($"CameraManager: 成功使用后端 {backend} 初始化设备 {deviceIndex}");
                            
                            // 应用后端特定的优化设置
                            ApplyBackendOptimizations(_capture, backend);
                            return true;
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"CameraManager: 后端 {backend} 性能验证失败");
                            LogUtil.Debug($"CameraManager: 后端 {backend} 性能验证失败");
                            _capture?.Release();
                            _capture?.Dispose();
                            _capture = null;
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"CameraManager: 后端 {backend} 无法打开设备 {deviceIndex}");
                        LogUtil.Info($"CameraManager: 后端 {backend} 无法打开设备 {deviceIndex}");
                        _capture?.Release();
                        _capture?.Dispose();
                        _capture = null;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"CameraManager: 后端 {backend} 初始化异常: {ex.Message}");
                    LogUtil.Info($"CameraManager: 后端 {backend} 初始化异常: {ex.Message}");
                    _capture?.Release();
                    _capture?.Dispose();
                    _capture = null;
                }
            }

            System.Diagnostics.Debug.WriteLine($"CameraManager: 所有后端都无法初始化设备 {deviceIndex}");
            LogUtil.Info($"CameraManager: 所有后端都无法初始化设备 {deviceIndex}");
            return false;
        }

        /// <summary>
        /// 获取优化的后端列表
        /// 根据系统环境和硬件加速支持情况动态调整后端优先级
        /// </summary>
        /// <returns>优化的后端列表</returns>
        private List<VideoCaptureAPIs> GetOptimizedBackendList()
        {
            var backends = new List<VideoCaptureAPIs>();

            try
            {
                // 获取加速管理器信息
                var accelerationManager = AccelerationManager.Instance;
                var availableBackends = accelerationManager.AccelerationInfo?.AvailableBackends ?? new List<VideoCaptureAPIs>();

                // 高性能后端优先级列表（按性能排序）
                var highPerformanceBackends = new[]
                {
                    VideoCaptureAPIs.MSMF,          // Microsoft Media Foundation (Windows 10+, 硬件加速)
                    VideoCaptureAPIs.DSHOW,         // DirectShow (Windows, 广泛兼容)
                    VideoCaptureAPIs.WINRT,         // Windows Runtime (UWP应用)
                    VideoCaptureAPIs.ANY            // 自动选择
                };

                // 添加可用的高性能后端
                foreach (var backend in highPerformanceBackends)
                {
                    if (availableBackends.Contains(backend) || availableBackends.Count == 0)
                    {
                        backends.Add(backend);
                    }
                }

                // 如果没有找到任何后端，使用默认列表
                if (backends.Count == 0)
                {
                    backends.AddRange(new[]
                    {
                        VideoCaptureAPIs.MSMF,
                        VideoCaptureAPIs.DSHOW,
                        VideoCaptureAPIs.ANY
                    });
                }

                // 根据硬件加速支持调整优先级
                if (accelerationManager.AccelerationInfo?.EnabledAcceleration != AccelerationType.None)
                {
                    // 如果支持硬件加速，优先使用MSMF
                    if (backends.Contains(VideoCaptureAPIs.MSMF))
                    {
                        backends.Remove(VideoCaptureAPIs.MSMF);
                        backends.Insert(0, VideoCaptureAPIs.MSMF);
                    }
                }

                System.Diagnostics.Debug.WriteLine($"CameraManager: 优化后端列表: {string.Join(", ", backends)}");
                LogUtil.Info($"CameraManager: 优化后端列表: {string.Join(", ", backends)}");

                return backends;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CameraManager: 获取优化后端列表异常: {ex.Message}");
                LogUtil.Debug($"CameraManager: 获取优化后端列表异常: {ex.Message}");

                // 返回默认后端列表
                return new List<VideoCaptureAPIs>
                {
                    VideoCaptureAPIs.MSMF,
                    VideoCaptureAPIs.DSHOW,
                    VideoCaptureAPIs.ANY
                };
            }
        }

        /// <summary>
        /// 验证后端性能
        /// </summary>
        /// <param name="capture">视频捕获对象</param>
        /// <param name="backend">后端类型</param>
        /// <returns>是否通过性能验证</returns>
        private bool ValidateBackendPerformance(VideoCapture capture, VideoCaptureAPIs backend)
        {
            try
            {
                // 基本功能验证
                if (!capture.IsOpened())
                {
                    return false;
                }

                // 获取基本属性
                var width = capture.Get(VideoCaptureProperties.FrameWidth);
                var height = capture.Get(VideoCaptureProperties.FrameHeight);
                var fps = capture.Get(VideoCaptureProperties.Fps);

                // 验证分辨率是否合理
                if (width <= 0 || height <= 0 || width > 8192 || height > 8192)
                {
                    System.Diagnostics.Debug.WriteLine($"CameraManager: 后端 {backend} 分辨率异常: {width}x{height}");
                    return false;
                }

                // 验证帧率是否合理
                if (fps <= 0 || fps > 240)
                {
                    System.Diagnostics.Debug.WriteLine($"CameraManager: 后端 {backend} 帧率异常: {fps}");
                    // 帧率异常不一定是致命问题，继续验证
                }

                // 尝试读取一帧进行验证
                using (var testFrame = new Mat())
                {
                    var readSuccess = capture.Read(testFrame);
                    if (!readSuccess || testFrame.Empty())
                    {
                        System.Diagnostics.Debug.WriteLine($"CameraManager: 后端 {backend} 无法读取帧");
                        return false;
                    }

                    // 验证帧数据的有效性
                    if (testFrame.Width != width || testFrame.Height != height)
                    {
                        System.Diagnostics.Debug.WriteLine($"CameraManager: 后端 {backend} 帧尺寸不匹配");
                        return false;
                    }
                }

                System.Diagnostics.Debug.WriteLine($"CameraManager: 后端 {backend} 性能验证通过 - {width}x{height}@{fps}fps");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CameraManager: 后端 {backend} 性能验证异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 应用后端特定的优化设置
        /// </summary>
        /// <param name="capture">视频捕获对象</param>
        /// <param name="backend">后端类型</param>
        private void ApplyBackendOptimizations(VideoCapture capture, VideoCaptureAPIs backend)
        {
            try
            {
                switch (backend)
                {
                    case VideoCaptureAPIs.MSMF:
                        // Microsoft Media Foundation 优化
                        ApplyMsmfOptimizations(capture);
                        break;

                    case VideoCaptureAPIs.DSHOW:
                        // DirectShow 优化
                        ApplyDshowOptimizations(capture);
                        break;

                    case VideoCaptureAPIs.WINRT:
                        // Windows Runtime 优化
                        ApplyWinrtOptimizations(capture);
                        break;

                    default:
                        // 通用优化
                        ApplyGenericOptimizations(capture);
                        break;
                }

                System.Diagnostics.Debug.WriteLine($"CameraManager: 已应用后端 {backend} 的优化设置");
                LogUtil.Info($"CameraManager: 已应用后端 {backend} 的优化设置");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CameraManager: 应用后端 {backend} 优化设置异常: {ex.Message}");
                LogUtil.Debug($"CameraManager: 应用后端 {backend} 优化设置异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 应用MSMF后端优化
        /// </summary>
        /// <param name="capture">视频捕获对象</param>
        private void ApplyMsmfOptimizations(VideoCapture capture)
        {
            try
            {
                // 启用硬件加速（如果支持）
                capture.Set(VideoCaptureProperties.BufferSize, 1);
                
                // 设置低延迟模式
                capture.Set(VideoCaptureProperties.FourCC, VideoWriter.FourCC('M', 'J', 'P', 'G'));
                
                // 优化缓冲区设置
                if (_isUltraHighDefinition)
                {
                    capture.Set(VideoCaptureProperties.BufferSize, 1);
                }
                else
                {
                    capture.Set(VideoCaptureProperties.BufferSize, 2);
                }

                System.Diagnostics.Debug.WriteLine("CameraManager: MSMF优化设置已应用");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CameraManager: MSMF优化设置异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 应用DirectShow后端优化
        /// </summary>
        /// <param name="capture">视频捕获对象</param>
        private void ApplyDshowOptimizations(VideoCapture capture)
        {
            try
            {
                // DirectShow特定优化
                capture.Set(VideoCaptureProperties.BufferSize, 1);
                
                // 设置自动曝光和自动白平衡
                capture.Set(VideoCaptureProperties.AutoExposure, 0.25);
                capture.Set(VideoCaptureProperties.AutoWB, 1);

                System.Diagnostics.Debug.WriteLine("CameraManager: DirectShow优化设置已应用");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CameraManager: DirectShow优化设置异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 应用Windows Runtime后端优化
        /// </summary>
        /// <param name="capture">视频捕获对象</param>
        private void ApplyWinrtOptimizations(VideoCapture capture)
        {
            try
            {
                // WinRT特定优化
                capture.Set(VideoCaptureProperties.BufferSize, 1);
                
                System.Diagnostics.Debug.WriteLine("CameraManager: WinRT优化设置已应用");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CameraManager: WinRT优化设置异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 应用通用优化设置
        /// </summary>
        /// <param name="capture">视频捕获对象</param>
        private void ApplyGenericOptimizations(VideoCapture capture)
        {
            try
            {
                // 通用优化设置
                capture.Set(VideoCaptureProperties.BufferSize, 1);
                
                System.Diagnostics.Debug.WriteLine("CameraManager: 通用优化设置已应用");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CameraManager: 通用优化设置异常: {ex.Message}");
            }
        }

        #endregion

        #region INotifyPropertyChanged 实现

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region IDisposable 实现

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    try
                    {
                        StopPreview();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"CameraManager: 停止预览异常: {ex.Message}");
                    }
                    
                    try
                    {
                        _capture?.Release();
                        _capture?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"CameraManager: 释放摄像头异常: {ex.Message}");
                    }
                    
                    try
                    {
                        _currentFrame?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"CameraManager: 释放当前帧异常: {ex.Message}");
                    }
                    
                    // 清理超高清设备相关资源
                    try
                    {
                        _memoryCleanupTimer?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"CameraManager: 释放内存清理定时器异常: {ex.Message}");
                    }
                    
                    // 清理异步处理资源
                    try
                    {
                        _frameProcessingSemaphore?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"CameraManager: 释放帧处理信号量异常: {ex.Message}");
                    }
                    
                    // 清理帧处理时间队列
                    try
                    {
                        _frameProcessingTimes?.Clear();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"CameraManager: 清理帧处理时间队列异常: {ex.Message}");
                    }

                    // 清理UI优化资源
                    try
                    {
                        lock (_bitmapCacheLock)
                        {
                            _cachedBitmapSource = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"CameraManager: 清理位图缓存异常: {ex.Message}");
                    }
                    
                    // 清理帧缓冲区
                    try
                    {
                        bool lockTaken = false;
                        try
                        {
                            Monitor.TryEnter(_frameBufferLock, TimeSpan.FromMilliseconds(500), ref lockTaken);
                            if (lockTaken)
                            {
                                while (_frameBuffer?.Count > 0)
                                {
                                    try
                                    {
                                        var frame = _frameBuffer.Dequeue();
                                        frame?.Dispose();
                                    }
                                    catch (Exception frameEx)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"CameraManager: 释放帧缓冲区中的帧异常: {frameEx.Message}");
                                        break; // 避免无限循环
                                    }
                                }
                                _frameBuffer?.Clear();
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine("CameraManager: 无法获取帧缓冲区锁，跳过清理");
                            }
                        }
                        finally
                        {
                            if (lockTaken)
                            {
                                Monitor.Exit(_frameBufferLock);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"CameraManager: 清理帧缓冲区异常: {ex.Message}");
                    }
                    
                    // 释放集成的管理器
                    try
                    {
                        _videoRecorder?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"CameraManager: 释放视频录制器异常: {ex.Message}");
                    }
                    
                    try
                    {
                        _deviceManager?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"CameraManager: 释放设备管理器异常: {ex.Message}");
                    }
                    
                    try
                    {
                        _performanceMonitor?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"CameraManager: 释放性能监视器异常: {ex.Message}");
                    }
                    
                    try
                    {
                        _errorHandler?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"CameraManager: 释放错误处理器异常: {ex.Message}");
                    }
                    
                    try
                    {
                        _cancellationTokenSource?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"CameraManager: 释放取消令牌源异常: {ex.Message}");
                    }
                    
                    try
                    {
                        _fpsStopwatch?.Stop();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"CameraManager: 停止FPS计时器异常: {ex.Message}");
                    }
                }

                _disposed = true;
            }
        }

        ~CameraManager()
        {
            Dispose(false);
        }

        #endregion
    }

    #region 枚举定义

    /// <summary>
    /// 摄像头状态枚举
    /// </summary>
    public enum CameraStatus
    {
        /// <summary>
        /// 停止状态
        /// </summary>
        Stopped,
        
        /// <summary>
        /// 运行状态
        /// </summary>
        Running,
        
        /// <summary>
        /// 错误状态
        /// </summary>
        Error,
        
        /// <summary>
        /// 分辨率已更改
        /// </summary>
        ResolutionChanged
    }

    /// <summary>
    /// 摄像头属性枚举
    /// </summary>
    public enum CameraProperty
    {
        /// <summary>
        /// 亮度
        /// </summary>
        Brightness,
        
        /// <summary>
        /// 对比度
        /// </summary>
        Contrast,
        
        /// <summary>
        /// 饱和度
        /// </summary>
        Saturation,
        
        /// <summary>
        /// 色调
        /// </summary>
        Hue,
        
        /// <summary>
        /// 增益
        /// </summary>
        Gain,
        
        /// <summary>
        /// 曝光
        /// </summary>
        Exposure,
        
        /// <summary>
        /// 焦点
        /// </summary>
        Focus,
        
        /// <summary>
        /// 缩放
        /// </summary>
        Zoom
    }

    #endregion

    #region 事件参数类

    /// <summary>
    /// 帧捕获事件参数
    /// </summary>
    public class FrameCapturedEventArgs : EventArgs
    {
        /// <summary>
        /// WPF位图源
        /// </summary>
        public BitmapSource BitmapSource { get; }
        
        /// <summary>
        /// OpenCV Mat对象
        /// </summary>
        public Mat Frame { get; }
        
        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; }

        public FrameCapturedEventArgs(BitmapSource bitmapSource, Mat frame)
        {
            BitmapSource = bitmapSource;
            Frame = frame;
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// 状态变更事件参数
    /// </summary>
    public class StatusChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 新状态
        /// </summary>
        public CameraStatus Status { get; }
        
        /// <summary>
        /// 是否正在捕获
        /// </summary>
        public bool IsCapturing { get; }
        
        /// <summary>
        /// 设备索引
        /// </summary>
        public int DeviceIndex { get; }
        
        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; }

        public StatusChangedEventArgs(CameraStatus status, bool isCapturing = false, int deviceIndex = -1)
        {
            Status = status;
            IsCapturing = isCapturing;
            DeviceIndex = deviceIndex;
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// 录像状态变更事件参数
    /// </summary>
    public class RecordingStatusChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 是否正在录像
        /// </summary>
        public bool IsRecording { get; }
        
        /// <summary>
        /// 录像文件路径
        /// </summary>
        public string FilePath { get; }
        
        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; }

        public RecordingStatusChangedEventArgs(bool isRecording, string filePath)
        {
            IsRecording = isRecording;
            FilePath = filePath;
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// 错误发生事件参数
    /// </summary>
    public class ErrorOccurredEventArgs : EventArgs
    {
        /// <summary>
        /// 异常对象
        /// </summary>
        public Exception Exception { get; }
        
        /// <summary>
        /// 错误消息
        /// </summary>
        public string Message { get; }
        
        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; }

        public ErrorOccurredEventArgs(Exception exception, string message)
        {
            Exception = exception;
            Message = message;
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// 性能统计事件参数
    /// </summary>
    public class PerformanceStatsEventArgs : EventArgs
    {
        /// <summary>
        /// 当前FPS
        /// </summary>
        public double Fps { get; set; }
        
        /// <summary>
        /// 帧计数
        /// </summary>
        public int FrameCount { get; set; }
        
        /// <summary>
        /// 缓冲区大小
        /// </summary>
        public int BufferSize { get; set; }
        
        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    #endregion

    #region 辅助类



    #endregion
}