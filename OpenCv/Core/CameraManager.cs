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
using System.Windows.Media.Imaging;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using DirectShowLib;

namespace OpenCv.Core
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
                            
                            _capture.Set(VideoCaptureProperties.FrameWidth, width);
                            _capture.Set(VideoCaptureProperties.FrameHeight, height);
                            _capture.Set(VideoCaptureProperties.Fps, 30);

                            // 获取实际分辨率
                            FrameWidth = (int)_capture.Get(VideoCaptureProperties.FrameWidth);
                            FrameHeight = (int)_capture.Get(VideoCaptureProperties.FrameHeight);
                            
                            System.Diagnostics.Debug.WriteLine($"CameraManager: 实际获得的分辨率为 {FrameWidth}x{FrameHeight}");

                            _currentFrame = new Mat();
                        }
                    });

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
                    
                    FrameWidth = actualWidth;
                    FrameHeight = actualHeight;

                    _currentFrame = new Mat();
                    _currentDeviceIndex = deviceIndex;
                    
                    // 测试读取一帧以验证摄像头工作正常
                    var testFrame = new Mat();
                    if (_capture.Read(testFrame) && !testFrame.Empty())
                    {
                        System.Diagnostics.Debug.WriteLine($"CameraManager: 成功读取测试帧，尺寸: {testFrame.Width}x{testFrame.Height}");
                        testFrame.Dispose();
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("CameraManager: 警告 - 无法读取测试帧");
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

                        // 尝试使用OpenCV获取设备的分辨率信息
                        try
                        {
                            using (var capture = new VideoCapture(i))
                            {
                                if (capture.IsOpened())
                                {
                                    device.Width = (int)capture.Get(VideoCaptureProperties.FrameWidth);
                                    device.Height = (int)capture.Get(VideoCaptureProperties.FrameHeight);
                                    device.DefaultWidth = device.Width;
                                    device.DefaultHeight = device.Height;
                                    device.DefaultFps = capture.Get(VideoCaptureProperties.Fps);
                                    
                                    // 获取支持的分辨率（常见分辨率）
                                    device.SupportedResolutions = GetSupportedResolutions();
                                    device.SupportedFrameRates = GetSupportedFrameRates();
                                    
                                    Console.WriteLine($"CameraManager: 设备 {i} 分辨率: {device.Width}x{device.Height}, FPS: {device.DefaultFps}");
                                }
                                else
                                {
                                    // 如果OpenCV无法打开，设置默认值
                                    device.Width = 640;
                                    device.Height = 480;
                                    device.DefaultWidth = 640;
                                    device.DefaultHeight = 480;
                                    device.DefaultFps = 30.0;
                                    device.IsConnected = false;
                                    device.Status = DeviceStatus.Error;
                                    device.ErrorMessage = "OpenCV无法打开设备";
                                    Console.WriteLine($"CameraManager: 设备 {i} OpenCV无法打开，使用默认配置");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // OpenCV检测失败，使用默认值
                            device.Width = 640;
                            device.Height = 480;
                            device.DefaultWidth = 640;
                            device.DefaultHeight = 480;
                            device.DefaultFps = 30.0;
                            device.ErrorMessage = $"OpenCV检测异常: {ex.Message}";
                            Console.WriteLine($"CameraManager: 设备 {i} OpenCV检测异常: {ex.Message}");
                        }

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
        public async Task<bool> StartCaptureByPathAsync(string devicePath)
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
                            
                            // 尝试强制设置分辨率
                            bool forceSuccess = await TryForceResolutionAsync(width, height);
                            
                            // 重新验证分辨率是否设置成功
                            resolutionMatches = forceSuccess && (FrameWidth == width && FrameHeight == height);
                            
                            if (!resolutionMatches)
                            {
                                System.Diagnostics.Debug.WriteLine($"CameraManager: 强制设置分辨率失败 - 请求: {width}x{height}, 最终: {FrameWidth}x{FrameHeight}");
                                
                                // 如果强制设置也失败，返回失败
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
                                
                                if (actualWidth == width && actualHeight == height)
                                {
                                    FrameWidth = actualWidth;
                                    FrameHeight = actualHeight;
                                    System.Diagnostics.Debug.WriteLine($"CameraManager: 强制设置成功！");
                                    return true;
                                }
                            }
                            
                            System.Diagnostics.Debug.WriteLine($"CameraManager: 强制设置失败，所有尝试均未成功");
                            return false;
                        }
                        return false;
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CameraManager: 强制设置分辨率异常: {ex.Message}");
                return false;
            }
        }





        #endregion

        #region 私有方法

        /// <summary>
        /// 摄像头捕获循环
        /// </summary>
        private void CaptureLoop()
        {
            var token = _cancellationTokenSource.Token;
            var frame = new Mat();
            
            while (IsCapturing && !token.IsCancellationRequested)
            {
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
                        }
                        else if (frameEmpty)
                        {
                            System.Diagnostics.Debug.WriteLine("CameraManager: 帧读取成功但帧为空");
                        }
                        else
                        {
                            // 每100帧输出一次成功信息，避免日志过多
                            if (_frameCount % 100 == 0)
                            {
                                System.Diagnostics.Debug.WriteLine($"CameraManager: 成功读取帧 #{_frameCount}, 尺寸: {frame.Width}x{frame.Height}, 通道: {frame.Channels()}");
                            }
                        }
                        
                        if (!readSuccess || frameEmpty)
                        {
                            // 检测设备断开
                            if (!_capture.IsOpened())
                            {
                                System.Diagnostics.Debug.WriteLine("CameraManager: 摄像头设备已断开");
                                ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(
                                    new InvalidOperationException("摄像头设备已断开"), "设备连接丢失"));
                                break;
                            }
                            continue;
                        }

                        // 更新当前帧
                        _currentFrame?.Dispose();
                        _currentFrame = frame.Clone();

                        // 录像处理
                        if (IsRecording)
                        {
                            _videoRecorder.WriteFrame(frame);
                        }
                    }

                    // 触发帧捕获事件 - 只传递Mat对象，BitmapSource转换在UI线程中进行
                    try
                    {
                        // 克隆Mat对象以避免线程安全问题
                        var clonedFrame = frame.Clone();
                        FrameCaptured?.Invoke(this, new FrameCapturedEventArgs(null, clonedFrame));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"帧捕获事件触发失败: {ex.Message}");
                        Debug.WriteLine($"异常详情: {ex}");
                        // 继续处理，不中断捕获循环
                    }

                    // 性能统计
                    _frameCount++;
                    
                    _frameProcessingStopwatch.Stop();
                    var processingTime = _frameProcessingStopwatch.ElapsedMilliseconds;
                    
                    // 更新性能监控
                    _performanceMonitor.RecordFrameProcessingTime(processingTime);
                    
                    UpdatePerformanceStats();
                    
                    // 动态帧率控制
                    var targetFrameTime = 1000.0 / 30.0; // 30fps
                    var sleepTime = Math.Max(0, (int)(targetFrameTime - processingTime));
                    
                    if (sleepTime > 0)
                    {
                        Thread.Sleep(sleepTime);
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
            // 尝试的后端列表，按优先级排序
            var backends = new[]
            {
                VideoCaptureAPIs.ANY,           // 自动选择
                VideoCaptureAPIs.DSHOW,         // DirectShow (Windows)
                VideoCaptureAPIs.MSMF,          // Microsoft Media Foundation
                //VideoCaptureAPIs.CAP_VFW        // Video for Windows (兼容性)
            };

            foreach (var backend in backends)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"CameraManager: 尝试使用后端 {backend} 初始化设备 {deviceIndex}");
                    
                    _capture = new VideoCapture(deviceIndex, backend);
                    
                    if (_capture.IsOpened())
                    {
                        System.Diagnostics.Debug.WriteLine($"CameraManager: 成功使用后端 {backend} 初始化设备 {deviceIndex}");
                        return true;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"CameraManager: 后端 {backend} 无法打开设备 {deviceIndex}");
                        _capture?.Release();
                        _capture?.Dispose();
                        _capture = null;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"CameraManager: 后端 {backend} 初始化异常: {ex.Message}");
                    _capture?.Release();
                    _capture?.Dispose();
                    _capture = null;
                }
            }

            System.Diagnostics.Debug.WriteLine($"CameraManager: 所有后端都无法初始化设备 {deviceIndex}");
            return false;
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
                    StopPreview();
                    
                    _capture?.Release();
                    _capture?.Dispose();
                    
                    _currentFrame?.Dispose();
                    
                    // 释放集成的管理器
                    _videoRecorder?.Dispose();
            _deviceManager?.Dispose();
            _performanceMonitor?.Dispose();
            _errorHandler?.Dispose();
                    
                    _cancellationTokenSource?.Dispose();
                    _fpsStopwatch?.Stop();
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