using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using Tools.Extend;
using WpfAppNew.Utils;

namespace WpfAppNew.EmguPlugs
{
    /// <summary>
    /// 工业相机管理器
    /// 专门用于显微镜等工业相机设备的高精度图像采集和处理
    /// 支持高分辨率成像、精确对焦、多光谱成像等专业功能
    /// </summary>
    /// <remarks>
    /// 主要功能：
    /// 1. 工业相机设备管理和初始化
    /// 2. 高精度图像采集和预览
    /// 3. 专业拍照功能（支持RAW格式）
    /// 4. 高质量视频录制
    /// 5. 图像增强和处理算法
    /// 6. 测量和标注功能
    /// 7. 多光谱和偏振光成像
    /// 8. 自动对焦和曝光控制
    /// </remarks>
    public class IndustrialCameraManager : INotifyPropertyChanged, IDisposable
    {
        #region 私有字段

        /// <summary>
        /// 当前相机设备
        /// </summary>
        private VideoCapture _camera;

        /// <summary>
        /// 预览定时器
        /// </summary>
        private System.Timers.Timer _previewTimer;

        /// <summary>
        /// UI更新节流器
        /// </summary>
        private UIUpdateThrottler _uiUpdateThrottler;

        /// <summary>
        /// 性能监控器
        /// </summary>
        private WpfAppNew.Utils.PerformanceMonitor _performanceMonitor;

        /// <summary>
        /// 视频录制器
        /// </summary>
        private VideoWriter _videoWriter;

        /// <summary>
        /// 双缓冲帧存储 - 无锁机制
        /// </summary>
        private volatile Mat _frontBuffer;
        private volatile Mat _backBuffer;
        private volatile int _currentBufferIndex = 0; // 0表示使用frontBuffer，1表示使用backBuffer

        /// <summary>
        /// 当前帧（兼容性保留，实际使用双缓冲）
        /// </summary>
        private Mat _currentFrame => _currentBufferIndex == 0 ? _frontBuffer : _backBuffer;

        /// <summary>
        /// 处理后的帧
        /// </summary>
        private Mat _processedFrame;

        /// <summary>
        /// 是否正在预览
        /// </summary>
        private bool _isPreviewing;

        /// <summary>
        /// 是否正在录制
        /// </summary>
        private bool _isRecording;

        /// <summary>
        /// 录制开始时间
        /// </summary>
        private DateTime _recordStartTime;

        /// <summary>
        /// 录制帧计数
        /// </summary>
        private int _recordedFrameCount;



        /// <summary>
        /// 显微镜控制器
        /// </summary>
        private MicroscopeController _microscopeController;

        /// <summary>
        /// 性能监控
        /// </summary>
        private PerformanceCounter _performanceCounter;

        /// <summary>
        /// 帧数据同步锁（用于保护当前帧和处理后的帧）
        /// </summary>
        private readonly ReaderWriterLockSlim _frameLock = new ReaderWriterLockSlim();

        /// <summary>
        /// 录制同步锁（用于保护录制相关操作）
        /// </summary>
        private readonly ReaderWriterLockSlim _recordingLock = new ReaderWriterLockSlim();

        /// <summary>
        /// 录制帧队列（无锁队列，提高录制性能）
        /// </summary>
        private readonly ConcurrentQueue<Mat> _recordingQueue = new ConcurrentQueue<Mat>();

        /// <summary>
        /// 录制线程取消令牌
        /// </summary>
        private CancellationTokenSource _recordingCancellationToken;

        /// <summary>
        /// 录制处理任务
        /// </summary>
        private Task _recordingTask;

        /// <summary>
        /// 异步帧处理流水线
        /// </summary>
        private readonly ConcurrentQueue<Mat> _processingQueue = new ConcurrentQueue<Mat>();
        private readonly SemaphoreSlim _processingQueueSemaphore = new SemaphoreSlim(0);
        private CancellationTokenSource _processingCancellationToken;
        private Task[] _processingTasks;
        private const int ProcessingThreadCount = 16; // 处理线程数量
        private const int MaxProcessingQueueSize = 800; // 最大处理队列大小（增加容量）
        private const int MaxRecordingQueueSize = 5000; // 最大录像队列大小

        /// <summary>
        /// 内存管理定时器
        /// </summary>
        private Timer _memoryManagementTimer;
        private readonly object _memoryManagementLock = new object();
        private DateTime _lastMemoryCleanup = DateTime.Now;

        /// <summary>
        /// 预览帧率 - 优化为15FPS以减少UI卡顿
        /// </summary>
        private double _previewFps = 30.0;

        /// <summary>
        /// 录制帧率
        /// </summary>
        private double _recordFps = 30.0;

        /// <summary>
        /// 当前设备索引
        /// </summary>
        private int _currentDeviceIndex = -1;

        /// <summary>
        /// 帧计数器（用于性能监控）
        /// </summary>
        private int _frameCounter;

        /// <summary>
        /// 上次性能更新时间
        /// </summary>
        private DateTime _lastPerformanceUpdate = DateTime.Now;

        /// <summary>
        /// 是否已释放资源
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 当前帧率
        /// </summary>
        private double _currentFps;

        /// <summary>
        /// 当前分辨率
        /// </summary>
        private OpenCvSharp.Size _currentResolution;

        /// <summary>
        /// 取消令牌源
        /// </summary>
        private CancellationTokenSource _cancellationTokenSource;

        /// <summary>
        /// 捕获任务
        /// </summary>
        private Task _captureTask;

        /// <summary>
        /// 性能监控
        /// </summary>
        private readonly Stopwatch _fpsStopwatch;
        private int _frameCount;
        private DateTime _lastFrameTime;

        /// <summary>
        /// 自适应帧率控制
        /// </summary>
        private double _targetFps = 30.0;
        private double _adaptiveDelayMs = 0;
        private readonly Queue<double> _frameTimeHistory = new Queue<double>();
        private const int MaxFrameTimeHistory = 10;

        /// <summary>
        /// 性能优化参数
        /// </summary>
        private int _droppedFrameCount = 0;
        private double _averageProcessingTime = 0;
        private readonly object _performanceStatsLock = new object();

        /// <summary>
        /// 录制文件路径
        /// </summary>
        private string _recordingFilePath;

        /// <summary>
        /// 图像增强参数
        /// </summary>
        private ImageEnhancementSettings _enhancementSettings;

        /// <summary>
        /// 自动对焦状态
        /// </summary>
        private bool _isAutoFocusEnabled;

        /// <summary>
        /// 自动曝光状态
        /// </summary>
        private bool _isAutoExposureEnabled;

        /// <summary>
        /// 锁失败计数器，用于性能监控
        /// </summary>
        private long _lockFailureCount = 0;

        /// <summary>
        /// 性能监控字段
        /// </summary>
        private long _totalProcessingTime = 0; // 总处理时间（毫秒）
        private int _processedFrameCount = 0; // 已处理帧数
        private long _memoryUsage = 0; // 内存使用量
        private DateTime _lastMemoryCheck = DateTime.Now;
        private readonly Stopwatch _processingStopwatch = new Stopwatch();

        #endregion

        #region 属性

        /// <summary>
        /// 是否正在捕获
        /// </summary>
        public bool IsCapturing
        {
            get => _isPreviewing;
            private set
            {
                if (_isPreviewing != value)
                {
                    _isPreviewing = value;
                    OnPropertyChanged();
                    StatusChanged?.Invoke(this, new StatusChangedEventArgs(_isPreviewing));
                }
            }
        }

        /// <summary>
        /// 是否正在录制
        /// </summary>
        public bool IsRecording
        {
            get => _isRecording;
            private set
            {
                if (_isRecording != value)
                {
                    _isRecording = value;
                    OnPropertyChanged();
                    RecordingStatusChanged?.Invoke(this, new RecordingStatusEventArgs(_isRecording));
                }
            }
        }

        /// <summary>
        /// 当前帧率
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
        /// 图像增强设置
        /// </summary>
        public ImageEnhancementSettings EnhancementSettings
        {
            get => _enhancementSettings;
            set
            {
                if (_enhancementSettings != value)
                {
                    _enhancementSettings = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 自动对焦是否启用
        /// </summary>
        public bool IsAutoFocusEnabled
        {
            get => _isAutoFocusEnabled;
            set
            {
                if (_isAutoFocusEnabled != value)
                {
                    _isAutoFocusEnabled = value;
                    OnPropertyChanged();
                    SetAutoFocus(value);
                }
            }
        }

        /// <summary>
        /// 自动曝光是否启用
        /// </summary>
        public bool IsAutoExposureEnabled
        {
            get => _isAutoExposureEnabled;
            set
            {
                if (_isAutoExposureEnabled != value)
                {
                    _isAutoExposureEnabled = value;
                    OnPropertyChanged();
                    SetAutoExposure(value);
                }
            }
        }

        #endregion

        #region 事件

        /// <summary>
        /// 连接状态变更事件
        /// </summary>
        public event EventHandler<ConnectionStatusEventArgs> ConnectionStatusChanged;

        /// <summary>
        /// 预览状态变更事件
        /// </summary>
        public event EventHandler<PreviewStatusEventArgs> PreviewStatusChanged;

        /// <summary>
        /// 录制状态变更事件
        /// </summary>
        public event EventHandler<RecordingStatusEventArgs> RecordingStatusChanged;

        /// <summary>
        /// 帧捕获事件
        /// </summary>
        public event EventHandler<FrameCapturedEventArgs> FrameCaptured;

        /// <summary>
        /// 拍照完成事件
        /// </summary>
        public event EventHandler<PhotoCapturedEventArgs> PhotoCaptured;

        /// <summary>
        /// 录像完成事件
        /// </summary>
        public event EventHandler<VideoRecordedEventArgs> VideoRecorded;

        /// <summary>
        /// 状态变更事件
        /// </summary>
        public event EventHandler<StatusChangedEventArgs> StatusChanged;

        /// <summary>
        /// 错误发生事件
        /// </summary>
        public event EventHandler<ErrorOccurredEventArgs> ErrorOccurred;

        /// <summary>
        /// 性能统计事件
        /// </summary>
        public event EventHandler<PerformanceStatsEventArgs> PerformanceStats;

        /// <summary>
        /// 属性变更事件
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化工业相机管理器
        /// </summary>
        public IndustrialCameraManager()
        {
            _fpsStopwatch = new Stopwatch();
            _enhancementSettings = new ImageEnhancementSettings();
            
            // 初始化双缓冲区
            _frontBuffer = new Mat();
            _backBuffer = new Mat();
            _currentBufferIndex = 0;
            
            // 初始化异步处理流水线
            StartAsyncProcessingPipeline();
            
            // 初始化内存管理定时器（每30秒执行一次内存清理）
            _memoryManagementTimer = new Timer(PerformMemoryManagement, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
            
            // 初始化UI更新节流器（100ms间隔）
            _uiUpdateThrottler = new UIUpdateThrottler(Application.Current?.Dispatcher ?? System.Windows.Threading.Dispatcher.CurrentDispatcher, 100);
            
            // 初始化性能监控器
            _performanceMonitor = new WpfAppNew.Utils.PerformanceMonitor(1000);
            _performanceMonitor.StatsUpdated += OnPerformanceStatsUpdated;
            
            LogUtil.Info("IndustrialCameraManager: 工业相机管理器初始化完成，已启用无锁双缓冲机制、异步处理流水线、内存管理、UI更新节流和性能监控");
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 异步初始化相机
        /// </summary>
        /// <param name="deviceIndex">设备索引</param>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        /// <param name="fps">帧率</param>
        /// <returns>初始化是否成功</returns>
        public async Task<bool> InitializeCameraAsync(int deviceIndex = 0, int width = 1920, int height = 1080, double fps = 30.0)
        {
            try
            {
                LogUtil.Info($"IndustrialCameraManager: 开始初始化工业相机 - 设备{deviceIndex}, 分辨率{width}x{height}, 帧率{fps}");

                // 释放现有资源
                await StopCaptureAsync();

                // 创建新的捕获对象
                _camera = new VideoCapture(deviceIndex);
                if (!_camera.IsOpened())
                {
                    LogUtil.Error($"IndustrialCameraManager: 无法打开设备{deviceIndex}");
                    return false;
                }

                // 设置相机参数
                _camera.Set(VideoCaptureProperties.FrameWidth, width);
                _camera.Set(VideoCaptureProperties.FrameHeight, height);
                _camera.Set(VideoCaptureProperties.Fps, fps);

                // 设置工业相机专用参数
                SetIndustrialCameraParameters();

                // 验证设置
                var actualWidth = (int)_camera.Get(VideoCaptureProperties.FrameWidth);
                var actualHeight = (int)_camera.Get(VideoCaptureProperties.FrameHeight);
                var actualFps = _camera.Get(VideoCaptureProperties.Fps);

                CurrentResolution = new OpenCvSharp.Size(actualWidth, actualHeight);
                CurrentFps = actualFps;

                _currentDeviceIndex = deviceIndex;

                // 初始化预览定时器
                _previewTimer = new System.Timers.Timer();
                _previewTimer.Elapsed += OnPreviewTimerElapsed;
                _previewTimer.AutoReset = true;

                LogUtil.Info($"IndustrialCameraManager: 相机初始化成功 - 实际分辨率{actualWidth}x{actualHeight}, 实际帧率{actualFps:F2}");
                return true;
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraManager: 相机初始化失败 - {ex.Message}");
                ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(ex));
                return false;
            }
        }

        /// <summary>
        /// 开始预览
        /// </summary>
        /// <returns>是否成功开始预览</returns>
        public async Task<bool> StartPreviewAsync()
        {
            try
            {
                if (_camera == null || !_camera.IsOpened())
                {
                    LogUtil.Error("IndustrialCameraManager: 相机未初始化，无法开始预览");
                    return false;
                }

                if (_isPreviewing)
                {
                    LogUtil.Warning("IndustrialCameraManager: 预览已在运行中");
                    return true;
                }

                LogUtil.Info("IndustrialCameraManager: 启动预览");

                _isPreviewing = true;
                _frameCounter = 0;
                _lastPerformanceUpdate = DateTime.Now;

                // 设置预览定时器间隔
                _previewTimer.Interval = 1000.0 / _previewFps;
                _previewTimer.Start();

                _cancellationTokenSource = new CancellationTokenSource();
                _captureTask = Task.Run(() => CaptureLoop(_cancellationTokenSource.Token));

                IsCapturing = true;
                _fpsStopwatch.Start();

                LogUtil.Info("IndustrialCameraManager: 预览已开始");
                return true;
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraManager: 开始预览失败 - {ex.Message}");
                ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(ex));
                return false;
            }
        }

        /// <summary>
        /// 停止预览
        /// </summary>
        public async Task StopPreviewAsync()
        {
            try
            {
                if (!_isPreviewing)
                {
                    LogUtil.Info("IndustrialCameraManager: 预览未在运行中");
                    return;
                }

                LogUtil.Info("IndustrialCameraManager: 停止预览");

                _isPreviewing = false;
                _previewTimer?.Stop();

                await StopCaptureAsync();
                LogUtil.Info("IndustrialCameraManager: 预览已停止");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraManager: 停止预览失败 - {ex.Message}");
            }
        }

        /// <summary>
        /// 拍照
        /// </summary>
        /// <param name="filePath">保存路径</param>
        /// <param name="format">图像格式</param>
        /// <param name="quality">质量（0-100）</param>
        /// <returns>是否成功拍照</returns>
        public bool TakePhoto(string filePath, ImageFormat format = ImageFormat.JPEG, int quality = 95)
        {
            try
            {
                if (_currentFrame == null || _currentFrame.Empty())
                {
                    LogUtil.Error("IndustrialCameraManager: 当前无有效帧，无法拍照");
                    return false;
                }

                // 应用图像增强
                var enhancedFrame = ApplyImageEnhancement(_currentFrame.Clone());

                // 根据格式保存
                bool success = false;
                switch (format)
                {
                    case ImageFormat.JPEG:
                        success = enhancedFrame.SaveImage(filePath, new int[] { (int)ImwriteFlags.JpegQuality, quality });
                        break;
                    case ImageFormat.PNG:
                        success = enhancedFrame.SaveImage(filePath, new int[] { (int)ImwriteFlags.PngCompression, 9 });
                        break;
                    case ImageFormat.TIFF:
                        success = enhancedFrame.SaveImage(filePath);
                        break;
                    case ImageFormat.BMP:
                        success = enhancedFrame.SaveImage(filePath);
                        break;
                }

                enhancedFrame.Dispose();

                if (success)
                {
                    LogUtil.Info($"IndustrialCameraManager: 拍照成功 - {filePath}");
                }
                else
                {
                    LogUtil.Error($"IndustrialCameraManager: 拍照失败 - {filePath}");
                }

                return success;
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraManager: 拍照异常 - {ex.Message}");
                ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(ex));
                return false;
            }
        }

        /// <summary>
        /// 异步拍照
        /// </summary>
        /// <param name="filePath">保存路径</param>
        /// <param name="format">图像格式</param>
        /// <param name="quality">质量（0-100）</param>
        /// <returns>是否成功拍照</returns>
        public async Task<bool> CapturePhotoAsync(string filePath, ImageFormat format = ImageFormat.JPEG, int quality = 95)
        {
            try
            {
                LogUtil.Info($"IndustrialCameraManager: 开始异步拍照，保存到 {filePath}");

                if (_camera == null || !_camera.IsOpened())
                {
                    LogUtil.Warning("IndustrialCameraManager: 设备未连接，无法拍照");
                    return false;
                }

                Mat captureFrame = null;
                
                // 使用无锁机制获取当前帧，完全避免锁竞争
                captureFrame = GetCurrentFrameLockFree();

                // 如果无法获取当前帧，尝试直接从摄像头读取
                if (captureFrame == null || captureFrame.Empty())
                {
                    LogUtil.Info("IndustrialCameraManager: 无法获取当前帧，尝试直接从摄像头读取");
                    captureFrame = new Mat();
                    if (!_camera.Read(captureFrame) || captureFrame.Empty())
                    {
                        LogUtil.Warning("IndustrialCameraManager: 无法获取当前帧");
                        captureFrame?.Dispose();
                        return false;
                    }
                }

                // 确保目录存在
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 将整个图像处理和保存过程移到后台线程，避免UI卡死
                var success = await Task.Run(() =>
                {
                    Mat enhancedFrame = null;
                    try
                    {
                        // 使用优化版本的图像增强，提升处理速度
                        enhancedFrame = ApplyImageEnhancementOptimized(captureFrame);

                        // 保存图像 - 使用更高效的保存参数
                        switch (format)
                        {
                            case ImageFormat.JPEG:
                                // 降低JPEG质量以提升保存速度，同时保持合理的图像质量
                                var jpegQuality = Math.Min(quality, 85); // 限制最大质量为85，平衡速度和质量
                                return enhancedFrame.SaveImage(filePath, new int[] { (int)ImwriteFlags.JpegQuality, jpegQuality });
                            case ImageFormat.PNG:
                                // 使用较低的压缩级别以提升保存速度
                                return enhancedFrame.SaveImage(filePath, new int[] { (int)ImwriteFlags.PngCompression, 3 });
                            case ImageFormat.TIFF:
                                return enhancedFrame.SaveImage(filePath);
                            case ImageFormat.BMP:
                                return enhancedFrame.SaveImage(filePath);
                            default:
                                return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogUtil.Error($"IndustrialCameraManager: 后台图像处理失败 - {ex.Message}");
                        return false;
                    }
                    finally
                    {
                        enhancedFrame?.Dispose();
                    }
                });

                if (success)
                {
                    LogUtil.Info($"IndustrialCameraManager: 异步拍照成功，已保存到 {filePath}");
                    
                    // 触发拍照完成事件
                    PhotoCaptured?.Invoke(this, new PhotoCapturedEventArgs(filePath, success, format));
                }
                else
                {
                    LogUtil.Error($"IndustrialCameraManager: 异步拍照失败 - {filePath}");
                }

                captureFrame?.Dispose();
                return success;
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraManager: 异步拍照失败 - {ex.Message}");
                ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(ex));
                return false;
            }
        }

        /// <summary>
        /// 开始录像
        /// </summary>
        /// <param name="filePath">保存路径</param>
        /// <param name="codec">编码器</param>
        /// <param name="fps">帧率</param>
        /// <returns>是否成功开始录像</returns>
        public bool StartRecording(string filePath, FourCC? codec = null, double fps = 30.0)
        {
            try
            {
                if (IsRecording)
                {
                    LogUtil.Warning("IndustrialCameraManager: 录像已在进行中");
                    return false;
                }

                // 使用无锁机制获取当前帧
                Mat currentFrameCopy = GetCurrentFrameLockFree();
                
                if (currentFrameCopy == null || currentFrameCopy.Empty())
                {
                    LogUtil.Error("IndustrialCameraManager: 无法获取有效帧，开始录像失败");
                    currentFrameCopy?.Dispose();
                    return false;
                }

                _recordingFilePath = filePath;
                var actualCodec = codec ?? FourCC.MJPG;
                
                // 使用当前帧的尺寸创建视频写入器
                var frameSize = new OpenCvSharp.Size(currentFrameCopy.Width, currentFrameCopy.Height);
                _videoWriter = new VideoWriter(filePath, actualCodec, fps, CurrentResolution);

                // 释放帧副本
                currentFrameCopy.Dispose();

                if (!_videoWriter.IsOpened())
                {
                    LogUtil.Error($"IndustrialCameraManager: 无法创建视频写入器 - {filePath}");
                    _videoWriter?.Dispose();
                    _videoWriter = null;
                    // 确保录像状态为false
                    IsRecording = false;
                    return false;
                }

                // 设置录像相关参数
                _recordStartTime = DateTime.Now;
                _recordedFrameCount = 0;
                _recordFps = fps;
                
                // 启动专用录制线程
                _recordingCancellationToken = new CancellationTokenSource();
                _recordingTask = Task.Run(() => RecordingWorker(_recordingCancellationToken.Token));
                
                // 设置录像状态为true（直接使用属性，确保事件触发）
                IsRecording = true;  // 直接设置公共属性，这会触发事件通知
                
                LogUtil.Info($"IndustrialCameraManager: 录像已开始 - {filePath}, 分辨率: {frameSize.Width}x{frameSize.Height}");
                return true;
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraManager: 开始录像失败 - {ex.Message}");
                ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(ex));
                return false;
            }
        }

        /// <summary>
        /// 停止录像
        /// </summary>
        public async Task<string> StopRecording()
        {
            string filePath = null;
            try
            {
                if (!IsRecording)
                {
                    return string.Empty;
                }

                // 先设置录制状态为false，防止新的写入操作（直接使用属性，确保事件触发）
                IsRecording = false;

                // 停止录制线程
                if (_recordingCancellationToken != null)
                {
                    _recordingCancellationToken.Cancel();
                    try
                    {
                        await _recordingTask.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // 预期的取消异常，忽略
                    }
                    catch (Exception ex)
                    {
                        LogUtil.Error($"IndustrialCameraManager: 等待录制线程结束失败 - {ex.Message}");
                    }
                    finally
                    {
                        _recordingCancellationToken?.Dispose();
                        _recordingCancellationToken = null;
                        _recordingTask = null;
                    }
                }

                // 处理录制队列中剩余的帧
                while (_recordingQueue.TryDequeue(out Mat remainingFrame))
                {
                    remainingFrame?.Dispose();
                }

                // 安全地释放视频写入器，使用录制锁和超时机制避免死锁
                bool lockTaken = _recordingLock.TryEnterWriteLock(2000); // 2秒超时
                if (!lockTaken)
                {
                    LogUtil.Warning("IndustrialCameraManager: 无法获取写锁来释放视频写入器，强制释放");
                    // 即使无法获取锁，也要尝试释放资源
                    try
                    {
                        if (_videoWriter != null)
                        {
                            if (_videoWriter.IsOpened())
                            {
                                _videoWriter.Release();
                            }
                            _videoWriter.Dispose();
                            _videoWriter = null;
                        }
                    }
                    catch (Exception releaseEx)
                    {
                        LogUtil.Error($"IndustrialCameraManager: 强制释放视频写入器失败 - {releaseEx.Message}");
                    }
                }
                else
                {
                    try
                    {
                        try
                        {
                            if (_videoWriter != null)
                            {
                                if (_videoWriter.IsOpened())
                                {
                                    _videoWriter.Release();
                                }
                                _videoWriter.Dispose();
                                _videoWriter = null;
                            }
                        }
                        catch (Exception releaseEx)
                        {
                            LogUtil.Error($"IndustrialCameraManager: 释放视频写入器失败 - {releaseEx.Message}");
                        }
                    }
                    finally
                    {
                        if (lockTaken)
                        {
                            _recordingLock.ExitWriteLock();
                        }
                    }
                }

                var recordDuration = DateTime.Now - _recordStartTime;
                filePath = _recordingFilePath;
                LogUtil.Info($"IndustrialCameraManager: 录像已停止 - {filePath}, 时长{recordDuration.TotalSeconds:F2}秒，帧数{_recordedFrameCount}");
                return filePath;
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraManager: 停止录像失败 - {ex.Message}");
                ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(ex));
                return string.Empty;
            }
            finally
            {
                _recordingFilePath = null;
            }
        }

        /// <summary>
        /// 异步开始录制视频
        /// </summary>
        /// <param name="filePath">保存路径</param>
        /// <param name="codec">视频编码器</param>
        /// <param name="fps">帧率</param>
        /// <returns>录制是否成功启动</returns>
        public async Task<bool> StartRecordingAsync(string filePath, FourCC? codec = null, double fps = 30.0)
        {
            try
            {
                if (_camera == null || !_camera.IsOpened())
                {
                    LogUtil.Warning("IndustrialCameraManager: 设备未连接，无法开始录制");
                    return false;
                }

                if (_isRecording)
                {
                    LogUtil.Warning("IndustrialCameraManager: 录制已在进行中");
                    return false;
                }

                // 使用无锁机制获取当前帧
                Mat currentFrameCopy = GetCurrentFrameLockFree();
                
                if (currentFrameCopy == null || currentFrameCopy.Empty())
                {
                    LogUtil.Error("IndustrialCameraManager: 无法获取有效帧，开始录制失败");
                    currentFrameCopy?.Dispose();
                    return false;
                }

                LogUtil.Info($"IndustrialCameraManager: 开始录制视频到 {filePath}");

                // 确保目录存在
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 获取当前分辨率
                var width = (int)CurrentResolution.Width;// currentFrameCopy.Width;
                var height = (int)CurrentResolution.Height;// currentFrameCopy.Height;

                if (width <= 0 || height <= 0)
                {
                    LogUtil.Warning("IndustrialCameraManager: 无效的视频分辨率");
                    currentFrameCopy?.Dispose();
                    return false;
                }

                // 创建视频写入器
                var actualCodec = codec ?? FourCC.XVID;
                _videoWriter = new VideoWriter(filePath, actualCodec, fps, new OpenCvSharp.Size(width, height), true);

                if (!_videoWriter.IsOpened())
                {
                    LogUtil.Error("IndustrialCameraManager: 无法创建视频写入器");
                    _videoWriter?.Dispose();
                    _videoWriter = null;
                    currentFrameCopy?.Dispose();
                    // 确保录像状态为false
                    IsRecording = false;
                    return false;
                }

                // 设置录像相关参数
                _recordStartTime = DateTime.Now;
                _recordedFrameCount = 0;
                _recordFps = fps;
                _recordingFilePath = filePath;
                
                // 启动专用录制线程
                _recordingCancellationToken = new CancellationTokenSource();
                _recordingTask = Task.Run(() => RecordingWorker(_recordingCancellationToken.Token));
                
                // 设置录像状态为true（直接使用属性，确保事件触发）
                IsRecording = true;  // 直接设置公共属性，这会触发事件通知

                // 释放临时帧
                currentFrameCopy?.Dispose();

                LogUtil.Info($"IndustrialCameraManager: 视频录制已启动 - {width}x{height} @ {fps}fps");
                return true;
            }
            
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraManager: 启动录制失败 - {ex.Message}");
                // 确保录像状态为false
                IsRecording = false;
                _videoWriter?.Dispose();
                _videoWriter = null;
                ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(ex));
                return false;
            }
        }

        /// <summary>
        /// 异步停止录制视频
        /// </summary>
        /// <returns>录制是否成功停止</returns>
        public async Task<string> StopRecordingAsync()
        {
            try
            {
                string filePath = null;
                if (!_isRecording)
                {
                    LogUtil.Info("IndustrialCameraManager: 录制未在进行中");
                    return string.Empty;
                }

                LogUtil.Info("IndustrialCameraManager: 停止录制视频");

                // 先设置录制状态为false，防止新的写入操作（直接使用属性，确保事件触发）
                IsRecording = false;

                // 停止录制线程
                if (_recordingCancellationToken != null)
                {
                    _recordingCancellationToken.Cancel();
                    try
                    {
                        await _recordingTask.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // 预期的取消异常，忽略
                    }
                    catch (Exception ex)
                    {
                        LogUtil.Error($"IndustrialCameraManager: 等待录制线程结束失败 - {ex.Message}");
                    }
                    finally
                    {
                        _recordingCancellationToken?.Dispose();
                        _recordingCancellationToken = null;
                        _recordingTask = null;
                    }
                }

                // 处理录制队列中剩余的帧
                while (_recordingQueue.TryDequeue(out Mat remainingFrame))
                {
                    remainingFrame?.Dispose();
                }

                // 等待一小段时间，确保正在进行的写入操作完成
                await Task.Delay(50);

                // 释放视频写入器，使用超时机制避免死锁
                await Task.Run(() =>
                {
                    bool lockTaken = _recordingLock.TryEnterWriteLock(2000); // 2秒超时
                    if (!lockTaken)
                    {
                        LogUtil.Warning("IndustrialCameraManager: 无法获取写锁来异步释放视频写入器，强制释放");
                        // 即使无法获取锁，也要尝试释放资源
                        try
                        {
                            if (_videoWriter != null)
                            {
                                if (_videoWriter.IsOpened())
                                {
                                    _videoWriter.Release();
                                }
                                _videoWriter.Dispose();
                                _videoWriter = null;
                            }
                        }
                        catch (Exception releaseEx)
                        {
                            LogUtil.Error($"IndustrialCameraManager: 强制异步释放视频写入器失败 - {releaseEx.Message}");
                        }
                    }
                    else
                    {
                        try
                        {
                            try
                            {
                                if (_videoWriter != null)
                                {
                                    if (_videoWriter.IsOpened())
                                    {
                                        _videoWriter.Release();
                                    }
                                    _videoWriter.Dispose();
                                    _videoWriter = null;
                                }
                            }
                            catch (Exception releaseEx)
                            {
                                LogUtil.Error($"IndustrialCameraManager: 异步释放视频写入器失败 - {releaseEx.Message}");
                            }
                        }
                        finally
                        {
                            if (lockTaken)
                            {
                                _recordingLock.ExitWriteLock();
                            }
                        }
                    }
                });

                var recordDuration = DateTime.Now - _recordStartTime;
                filePath = _recordingFilePath;
                LogUtil.Info($"IndustrialCameraManager: 录制已停止 - 时长{recordDuration.TotalSeconds:F2}秒，帧数{_recordedFrameCount}");
                _recordingFilePath = null;

                return filePath;
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraManager: 停止录制失败 - {ex.Message}");
                ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(ex));
                return string.Empty;
            }
        }

        /// <summary>
        /// 专用录制工作线程方法
        /// 处理录制队列中的帧数据，避免与主线程的锁竞争
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        private async Task RecordingWorker(CancellationToken cancellationToken)
        {
            try
            {
                LogUtil.Debug("IndustrialCameraManager: 录制工作线程已启动");
                
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // 批量处理帧数据以提高效率
                        var framesToProcess = new List<Mat>();
                        int batchSize = Math.Min(10, _recordingQueue.Count); // 每次最多处理10帧
                        
                        // 收集一批帧进行处理
                        for (int i = 0; i < batchSize && _recordingQueue.TryDequeue(out Mat frame); i++)
                        {
                            if (frame != null && !frame.Empty())
                            {
                                framesToProcess.Add(frame);
                            }
                            else
                            {
                                frame?.Dispose();
                            }
                        }
                        
                        if (framesToProcess.Count > 0)
                        {
                            // 使用录制专用锁保护视频写入器
                            bool lockTaken = false;
                            try
                            {
                                lockTaken = _recordingLock.TryEnterWriteLock(200);
                                if (lockTaken && _videoWriter != null && _videoWriter.IsOpened())
                                {
                                    // 批量写入帧
                                    foreach (var frame in framesToProcess)
                                    {
                                        _videoWriter.Write(frame);
                                        Interlocked.Increment(ref _recordedFrameCount);
                                    }
                                }
                                else if (!lockTaken)
                                {
                                    LogUtil.Debug($"IndustrialCameraManager: 录制线程无法获取写入锁，跳过 {framesToProcess.Count} 帧");
                                }
                            }
                            finally
                            {
                                if (lockTaken)
                                {
                                    _recordingLock.ExitWriteLock();
                                }
                                // 释放所有帧资源
                                foreach (var frame in framesToProcess)
                                {
                                    frame?.Dispose();
                                }
                            }
                        }
                        else
                        {
                            // 队列为空时短暂等待，避免CPU占用过高
                            await Task.Delay(2, cancellationToken);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // 正常取消，退出循环
                        break;
                    }
                    catch (Exception ex)
                    {
                        LogUtil.Error($"IndustrialCameraManager: 录制工作线程处理帧时发生错误 - {ex.Message}");
                        // 继续处理下一帧，不中断录制
                    }
                }
                
                LogUtil.Debug("IndustrialCameraManager: 录制工作线程已停止");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraManager: 录制工作线程发生严重错误 - {ex.Message}");
                ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(ex));
            }
        }

        /// <summary>
        /// 设置相机属性
        /// </summary>
        /// <param name="property">属性类型</param>
        /// <param name="value">属性值</param>
        /// <returns>是否设置成功</returns>
        public bool SetCameraProperty(CameraProperty property, double value)
        {
            try
            {
                if (_camera == null || !_camera.IsOpened())
                {
                    return false;
                }

                var cvProperty = GetVideoCaptureProperty(property);
                _camera.Set(cvProperty, value);

                LogUtil.Debug($"IndustrialCameraManager: 设置相机属性 {property} = {value}");
                return true;
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraManager: 设置相机属性失败 - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取相机属性
        /// </summary>
        /// <param name="property">属性类型</param>
        /// <returns>属性值</returns>
        public double GetCameraProperty(CameraProperty property)
        {
            try
            {
                if (_camera == null || !_camera.IsOpened())
                {
                    return 0;
                }

                var cvProperty = GetVideoCaptureProperty(property);
                return _camera.Get(cvProperty);
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraManager: 获取相机属性失败 - {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// 设置图像增强参数
        /// </summary>
        /// <param name="settings">图像增强设置</param>
        public void SetImageEnhancementSettings(ImageEnhancementSettings settings)
        {
            try
            {
                if (settings == null)
                {
                    LogUtil.Warning("IndustrialCameraManager: 图像增强设置为空");
                    return;
                }

                _enhancementSettings = settings;
                LogUtil.Info("IndustrialCameraManager: 图像增强设置已更新");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraManager: 设置图像增强参数失败 - {ex.Message}");
                ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(ex));
            }
        }

        /// <summary>
        /// 获取图像增强参数
        /// </summary>
        /// <returns>当前图像增强设置</returns>
        public ImageEnhancementSettings GetImageEnhancementSettings()
        {
            return _enhancementSettings ?? new ImageEnhancementSettings();
        }

        /// <summary>
        /// 获取设备默认参数
        /// </summary>
        /// <returns>设备默认参数设置</returns>
        public CameraDefaultSettings GetDeviceDefaultSettings()
        {
            var defaultSettings = new CameraDefaultSettings();
            
            try
            {
                if (_camera != null && _camera.IsOpened())
                {
                    // 从设备获取默认参数值
                    defaultSettings.ExposureValue = GetCameraProperty(CameraProperty.Exposure);
                    defaultSettings.GainValue = GetCameraProperty(CameraProperty.Gain);
                    defaultSettings.ContrastValue = GetCameraProperty(CameraProperty.Contrast);
                    defaultSettings.SaturationValue = GetCameraProperty(CameraProperty.Saturation);
                    defaultSettings.BrightnessValue = GetCameraProperty(CameraProperty.Brightness);
                    
                    // 对于某些参数，如果设备返回0或无效值，使用合理的默认值
                    if (defaultSettings.ExposureValue <= 0) defaultSettings.ExposureValue = 0.5;
                    if (defaultSettings.GainValue <= 0) defaultSettings.GainValue = 0.5;
                    if (defaultSettings.ContrastValue <= 0) defaultSettings.ContrastValue = 1.0;
                    if (defaultSettings.SaturationValue <= 0) defaultSettings.SaturationValue = 1.0;
                    if (defaultSettings.BrightnessValue < 0) defaultSettings.BrightnessValue = 0.5;
                    
                    // 伽马校正通常默认为1.0（无校正）
                    defaultSettings.GammaValue = 1.0;
                    
                    // 布尔值参数的默认设置
                    defaultSettings.IsAutoWhiteBalance = true;
                    defaultSettings.IsNoiseReductionEnabled = false;
                    
                    LogUtil.Info($"IndustrialCameraManager: 已从设备获取默认参数 - 曝光:{defaultSettings.ExposureValue:F2}, 增益:{defaultSettings.GainValue:F2}, 对比度:{defaultSettings.ContrastValue:F2}");
                }
                else
                {
                    // 如果设备未连接，使用备用默认值
                    LogUtil.Warning("IndustrialCameraManager: 设备未连接，使用备用默认参数");
                    defaultSettings.ExposureValue = 0.5;
                    defaultSettings.GainValue = 0.5;
                    defaultSettings.ContrastValue = 1.0;
                    defaultSettings.SaturationValue = 1.0;
                    defaultSettings.BrightnessValue = 0.5;
                    defaultSettings.GammaValue = 1.0;
                    defaultSettings.IsAutoWhiteBalance = true;
                    defaultSettings.IsNoiseReductionEnabled = false;
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraManager: 获取设备默认参数失败 - {ex.Message}");
                // 发生异常时使用备用默认值
                defaultSettings.ExposureValue = 0.5;
                defaultSettings.GainValue = 0.5;
                defaultSettings.ContrastValue = 1.0;
                defaultSettings.SaturationValue = 1.0;
                defaultSettings.BrightnessValue = 0.5;
                defaultSettings.GammaValue = 1.0;
                defaultSettings.IsAutoWhiteBalance = true;
                defaultSettings.IsNoiseReductionEnabled = false;
            }
            
            return defaultSettings;
        }

        /// <summary>
        /// 获取录像持续时间
        /// </summary>
        /// <returns>录像持续时间</returns>
        public TimeSpan GetRecordingDuration()
        {
            if (_isRecording)
            {
                return DateTime.Now - _recordStartTime;
            }
            return TimeSpan.Zero;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                StopRecording().GetAwaiter().GetResult();
                StopCaptureAsync().GetAwaiter().GetResult();
                StopAsyncProcessingPipeline();
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraManager: Dispose中停止操作失败 - {ex.Message}");
            }

            _previewTimer?.Stop();
            _previewTimer?.Dispose();

            // 停止并释放内存管理定时器
            _memoryManagementTimer?.Dispose();

            // 释放UI更新节流器
            _uiUpdateThrottler?.Dispose();

            // 释放性能监控器
            _performanceMonitor?.Dispose();

            _camera?.Release();
            _camera?.Dispose();
            _currentFrame?.Dispose();
            _processedFrame?.Dispose();
            
            // 释放双缓冲区
            _frontBuffer?.Dispose();
            _backBuffer?.Dispose();

            _disposed = true;
            LogUtil.Info("IndustrialCameraManager: 资源已释放，包括双缓冲区、内存管理定时器、UI更新节流器和性能监控器");
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 设置工业相机专用参数
        /// 只设置必要的技术参数，不覆盖用户可调节的参数（曝光、增益、亮度、对比度、饱和度、锐度等）
        /// </summary>
        private void SetIndustrialCameraParameters()
        {
            try
            {
                // 设置缓冲区大小 - 这是技术参数，不影响图像效果
                _camera.Set(VideoCaptureProperties.BufferSize, 1);

                // 启用自动对焦 - 这是便利功能，不影响用户手动调节的参数
                try
                {
                    _camera.Set(VideoCaptureProperties.AutoFocus, 1);
                    LogUtil.Debug("IndustrialCameraManager: 启用自动对焦");
                }
                catch (Exception ex)
                {
                    LogUtil.Warning($"IndustrialCameraManager: 设置自动对焦失败 - {ex.Message}");
                }

                // 注意：不再设置以下参数，保持用户的手动设置：
                // - 曝光 (Exposure)
                // - 增益 (Gain) 
                // - 亮度 (Brightness)
                // - 对比度 (Contrast)
                // - 饱和度 (Saturation)
                // - 锐度 (Sharpness)
                // 这些参数将通过用户界面的滑块进行手动调节

                LogUtil.Debug("IndustrialCameraManager: 工业相机基础参数设置完成，用户可调节参数保持不变");
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"IndustrialCameraManager: 设置工业相机参数时出现警告 - {ex.Message}");
            }
        }

        /// <summary>
        /// 预览定时器事件处理 - 从双缓冲区读取已处理的帧
        /// 优化：使用低优先级调度和帧跳过机制减少UI线程压力
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">事件参数</param>
        private void OnPreviewTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (!_isPreviewing)
            {
                return;
            }

            // 开始性能监控帧记录
            _performanceMonitor?.StartFrame();

            try
            {
                // 检查UI线程是否繁忙，如果繁忙则跳过此帧
                if (Application.Current?.Dispatcher.HasShutdownStarted == true)
                {
                    return;
                }

                // 从无锁双缓冲区获取当前帧
                var currentFrame = GetCurrentFrameLockFree();
                if (currentFrame == null || currentFrame.Empty())
                {
                    return;
                }

                // 转换为位图源并使用节流器优化UI更新
                var bitmapSource = SafeMatToBitmapSource(currentFrame);
                if (bitmapSource != null)
                {
                    // 使用UI更新节流器，避免过于频繁的UI更新
                    _uiUpdateThrottler?.RequestUpdate(() =>
                    {
                        try
                        {
                            // 再次检查预览状态，避免在停止预览后仍然触发事件
                            if (_isPreviewing)
                            {
                                FrameCaptured?.Invoke(this, new FrameCapturedEventArgs(bitmapSource, currentFrame.Clone()));
                            }
                        }
                        catch (Exception ex)
                        {
                            LogUtil.Error($"IndustrialCameraManager: 预览事件触发失败 - {ex.Message}");
                        }
                    });
                }

            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraManager: 预览帧处理失败 - {ex.Message}");
                ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(ex));
            }
            finally
            {
                // 结束性能监控帧记录
                _performanceMonitor?.EndFrame();
            }
        }

        /// <summary>
        /// 处理帧
        /// </summary>
        /// <param name="frame">输入帧</param>
        /// <returns>处理后的帧</returns>
        private Mat ProcessFrame(Mat frame)
        {
            _processingStopwatch.Restart();
            
            try
            {
                // 优化：只在需要增强时才克隆，否则直接返回原帧
                bool needsEnhancement = _enhancementSettings != null && _enhancementSettings.IsEnabled;
                
                // 记录原始图像信息（减少频率以提高性能）
                if (_frameCount % 60 == 0) // 从30帧改为60帧，减少日志输出
                {
                    var meanBrightness = Cv2.Mean(frame);
                    LogUtil.Debug($"IndustrialCameraManager: 图像平均亮度 - R:{meanBrightness.Val2:F2}, G:{meanBrightness.Val1:F2}, B:{meanBrightness.Val0:F2}");
                }

                // 如果不需要增强，直接返回原帧（避免不必要的克隆）
                if (!needsEnhancement)
                {
                    UpdateProcessingStats(0); // 无处理时间
                    return frame;
                }

                // 只有在需要增强时才克隆和处理
                var processed = frame.Clone();
                try
                {
                    var enhanced = ApplyImageEnhancementOptimized(processed);
                    processed.Dispose(); // 立即释放中间结果
                    
                    _processingStopwatch.Stop();
                    UpdateProcessingStats(_processingStopwatch.ElapsedMilliseconds);
                    
                    return enhanced;
                }
                catch
                {
                    processed.Dispose();
                    throw;
                }
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"IndustrialCameraManager: 帧处理失败 - {ex.Message}");
                _processingStopwatch.Stop();
                UpdateProcessingStats(_processingStopwatch.ElapsedMilliseconds);
                return frame; // 直接返回原帧，避免额外克隆
            }
        }

        /// <summary>
        /// 更新性能统计
        /// </summary>
        private void UpdatePerformanceStats()
        {
            _frameCounter++;
            var now = DateTime.Now;
            var elapsed = (now - _lastPerformanceUpdate).TotalSeconds;

            if (elapsed >= 1.0) // 每秒更新一次
            {
                var fps = _frameCounter / elapsed;
                CurrentFps = fps;

                _frameCounter = 0;
                _lastPerformanceUpdate = now;

                LogUtil.Debug($"IndustrialCameraManager: 当前帧率 {fps:F2} FPS");
            }
        }

        /// <summary>
        /// 更新处理性能统计
        /// </summary>
        /// <param name="processingTimeMs">处理时间（毫秒）</param>
        private void UpdateProcessingStats(long processingTimeMs)
        {
            _totalProcessingTime += processingTimeMs;
            _processedFrameCount++;

            // 每100帧输出一次详细统计
            if (_processedFrameCount % 100 == 0)
            {
                var avgProcessingTime = _processedFrameCount > 0 ? (double)_totalProcessingTime / _processedFrameCount : 0;
                
                // 检查内存使用情况
                var now = DateTime.Now;
                if ((now - _lastMemoryCheck).TotalSeconds >= 5) // 每5秒检查一次内存
                {
                    GC.Collect(); // 强制垃圾回收
                    _memoryUsage = GC.GetTotalMemory(false);
                    _lastMemoryCheck = now;
                }

                LogUtil.Info($"IndustrialCameraManager: 性能统计 - 平均处理时间: {avgProcessingTime:F2}ms, " +
                           $"锁失败次数: {_lockFailureCount}, 内存使用: {_memoryUsage / 1024 / 1024:F2}MB");

                // 重置统计计数器，避免数值过大
                if (_processedFrameCount >= 1000)
                {
                    _totalProcessingTime = 0;
                    _processedFrameCount = 0;
                    _lockFailureCount = 0;
                }
            }
        }

        /// <summary>
        /// 无锁帧交换 - 使用双缓冲机制避免锁竞争
        /// </summary>
        /// <param name="newFrame">新帧</param>
        /// <returns>是否成功交换</returns>
        private bool SwapFrameBufferLockFree(Mat newFrame)
        {
            try
            {
                // 获取当前非活跃的缓冲区
                var targetBuffer = _currentBufferIndex == 0 ? _backBuffer : _frontBuffer;
                
                // 释放旧的缓冲区内容
                targetBuffer?.Dispose();
                
                // 将新帧克隆到目标缓冲区
                if (_currentBufferIndex == 0)
                {
                    _backBuffer = newFrame.Clone();
                    // 原子性地切换缓冲区索引
                    Interlocked.Exchange(ref _currentBufferIndex, 1);
                }
                else
                {
                    _frontBuffer = newFrame.Clone();
                    // 原子性地切换缓冲区索引
                    Interlocked.Exchange(ref _currentBufferIndex, 0);
                }
                
                return true;
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"IndustrialCameraManager: 无锁帧交换失败 - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取当前帧的安全副本 - 无锁读取
        /// </summary>
        /// <returns>当前帧的副本，如果失败返回null</returns>
        private Mat GetCurrentFrameLockFree()
        {
            try
            {
                var currentFrame = _currentFrame;
                return currentFrame?.Clone();
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"IndustrialCameraManager: 无锁帧读取失败 - {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 捕获循环
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        private async Task CaptureLoop(CancellationToken cancellationToken)
        {
            var frame = new Mat();

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (_camera == null || !_camera.IsOpened())
                    {
                        break;
                    }

                    // 读取帧
                    if (_camera.Read(frame) && !frame.Empty())
                    {
                        // 将原始帧添加到异步处理流水线
                        bool enqueued = EnqueueFrameForProcessing(frame);
                        if (!enqueued)
                        {
                            LogUtil.Debug("IndustrialCameraManager: 帧添加到处理队列失败，跳过当前帧");
                        }

                        // 录像处理 - 使用无锁队列方式，避免锁竞争


                        // 更新帧率
                        UpdateFrameRate();

                        // 应用自适应延迟控制，优化帧获取频率
                        if (_adaptiveDelayMs > 0)
                        {
                            await Task.Delay((int)_adaptiveDelayMs, cancellationToken);
                        }
                    }
                    else
                    {
                        // 帧读取失败时增加丢帧计数
                        Interlocked.Increment(ref _droppedFrameCount);
                        LogUtil.Debug("IndustrialCameraManager: 帧读取失败");
                        await Task.Delay(10, cancellationToken); // 避免CPU占用过高
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消，不记录错误
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraManager: 捕获循环异常 - {ex.Message}");
                ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(ex));
            }
            finally
            {
                frame.Dispose();
            }
        }

        /// <summary>
        /// 停止捕获
        /// </summary>
        private async Task StopCaptureAsync()
        {
            try
            {
                if (_cancellationTokenSource != null)
                {
                    _cancellationTokenSource.Cancel();
                    
                    if (_captureTask != null)
                    {
                        await _captureTask;
                    }

                    _cancellationTokenSource.Dispose();
                    _cancellationTokenSource = null;
                    _captureTask = null;
                }

                IsCapturing = false;
                _fpsStopwatch.Stop();
                _fpsStopwatch.Reset();
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraManager: 停止捕获失败 - {ex.Message}");
            }
        }

        /// <summary>
        /// 应用图像增强
        /// </summary>
        /// <param name="frame">输入帧</param>
        /// <returns>增强后的帧</returns>
        private Mat ApplyImageEnhancement(Mat frame)
        {
            if (_enhancementSettings == null || !_enhancementSettings.IsEnabled)
            {
                return frame.Clone();
            }

            var enhanced = frame.Clone();

            try
            {
                // 亮度和对比度调整
                if (_enhancementSettings.Brightness != 0 || _enhancementSettings.Contrast != 1.0)
                {
                    enhanced.ConvertTo(enhanced, MatType.CV_8UC3, _enhancementSettings.Contrast, _enhancementSettings.Brightness);
                }

                // 伽马校正
                if (Math.Abs(_enhancementSettings.Gamma - 1.0) > 0.01)
                {
                    var lookupTable = new Mat(1, 256, MatType.CV_8U);
                    var lookupTableData = lookupTable.GetGenericIndexer<byte>();
                    
                    for (int i = 0; i < 256; i++)
                    {
                        lookupTableData[0, i] = (byte)(Math.Pow(i / 255.0, 1.0 / _enhancementSettings.Gamma) * 255);
                    }
                    
                    Cv2.LUT(enhanced, lookupTable, enhanced);
                    lookupTable.Dispose();
                }

                // 锐化
                if (_enhancementSettings.Sharpness > 0)
                {
                    var kernel = new Mat(3, 3, MatType.CV_32F, new float[,]
                    {
                        { 0, -(float)_enhancementSettings.Sharpness, 0 },
                        { -(float)_enhancementSettings.Sharpness, 1 + 4 * (float)_enhancementSettings.Sharpness, -(float)_enhancementSettings.Sharpness },
                        { 0, -(float)_enhancementSettings.Sharpness, 0 }
                    });
                    
                    Cv2.Filter2D(enhanced, enhanced, -1, kernel);
                    kernel.Dispose();
                }

                // 降噪
                if (_enhancementSettings.IsNoiseReductionEnabled && _enhancementSettings.NoiseReduction > 0)
                {
                    if (enhanced.Channels() == 1)
                    {
                        Cv2.FastNlMeansDenoising(enhanced, enhanced, _enhancementSettings.NoiseReduction);
                    }
                    else
                    {
                        Cv2.FastNlMeansDenoisingColored(enhanced, enhanced, _enhancementSettings.NoiseReduction, _enhancementSettings.NoiseReduction);
                    }
                }

                // 直方图均衡化
                if (_enhancementSettings.HistogramEqualization)
                {
                    if (enhanced.Channels() == 1)
                    {
                        Cv2.EqualizeHist(enhanced, enhanced);
                    }
                    else
                    {
                        using (var lab = new Mat())
                        {
                            Cv2.CvtColor(enhanced, lab, ColorConversionCodes.BGR2Lab);
                            var channels = Cv2.Split(lab);
                            Cv2.EqualizeHist(channels[0], channels[0]);
                            Cv2.Merge(channels, lab);
                            Cv2.CvtColor(lab, enhanced, ColorConversionCodes.Lab2BGR);
                            foreach (var channel in channels)
                            {
                                channel.Dispose();
                            }
                        }
                    }
                }

                // 边缘增强
                if (_enhancementSettings.EdgeEnhancement)
                {
                    using (var gray = new Mat())
                    using (var edges = new Mat())
                    {
                        if (enhanced.Channels() == 3)
                        {
                            Cv2.CvtColor(enhanced, gray, ColorConversionCodes.BGR2GRAY);
                        }
                        else
                        {
                            enhanced.CopyTo(gray);
                        }
                        
                        Cv2.Canny(gray, edges, 50, 150);
                        
                        if (enhanced.Channels() == 3)
                        {
                            using (var edgesBgr = new Mat())
                            {
                                Cv2.CvtColor(edges, edgesBgr, ColorConversionCodes.GRAY2BGR);
                                Cv2.AddWeighted(enhanced, 0.8, edgesBgr, 0.2, 0, enhanced);
                            }
                        }
                        else
                        {
                            Cv2.AddWeighted(enhanced, 0.8, edges, 0.2, 0, enhanced);
                        }
                    }
                }

                // 饱和度调整
                if (Math.Abs(_enhancementSettings.Saturation - 1.0) > 0.01 && enhanced.Channels() == 3)
                {
                    using (var hsv = new Mat())
                    {
                        Cv2.CvtColor(enhanced, hsv, ColorConversionCodes.BGR2HSV);
                        var channels = Cv2.Split(hsv);
                        channels[1].ConvertTo(channels[1], -1, _enhancementSettings.Saturation, 0);
                        Cv2.Merge(channels, hsv);
                        Cv2.CvtColor(hsv, enhanced, ColorConversionCodes.HSV2BGR);
                        foreach (var channel in channels)
                        {
                            channel.Dispose();
                        }
                    }
                }

                return enhanced;
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"IndustrialCameraManager: 图像增强失败 - {ex.Message}");
                enhanced.Dispose();
                return frame.Clone();
            }
        }

        /// <summary>
        /// 应用图像增强（优化版本）
        /// 使用缓存和更高效的算法减少内存分配和计算时间
        /// </summary>
        /// <param name="frame">输入帧</param>
        /// <returns>增强后的帧</returns>
        private Mat ApplyImageEnhancementOptimized(Mat frame)
        {
            if (_enhancementSettings == null || !_enhancementSettings.IsEnabled)
            {
                return frame;
            }

            var enhanced = frame;
            Mat tempMat = null;

            try
            {
                // 亮度和对比度调整（最高效的操作，优先执行）
                if (_enhancementSettings.Brightness != 0 || Math.Abs(_enhancementSettings.Contrast - 1.0) > 0.01)
                {
                    if (enhanced == frame) // 第一次修改时才克隆
                    {
                        enhanced = frame.Clone();
                    }
                    enhanced.ConvertTo(enhanced, MatType.CV_8UC3, _enhancementSettings.Contrast, _enhancementSettings.Brightness);
                }

                // 伽马校正（使用缓存的查找表）
                if (Math.Abs(_enhancementSettings.Gamma - 1.0) > 0.01)
                {
                    if (enhanced == frame)
                    {
                        enhanced = frame.Clone();
                    }
                    
                    var lookupTable = GetCachedGammaLookupTable(_enhancementSettings.Gamma);
                    Cv2.LUT(enhanced, lookupTable, enhanced);
                }

                // 锐化（使用缓存的卷积核）
                if (_enhancementSettings.Sharpness > 0)
                {
                    if (enhanced == frame)
                    {
                        enhanced = frame.Clone();
                    }
                    
                    var kernel = GetCachedSharpnessKernel(_enhancementSettings.Sharpness);
                    Cv2.Filter2D(enhanced, enhanced, -1, kernel);
                }

                // 跳过耗时的降噪和复杂处理，专注于实时性能
                // 如果需要这些功能，可以在后台线程中异步处理

                return enhanced;
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"IndustrialCameraManager: 优化图像增强失败 - {ex.Message}");
                if (enhanced != frame)
                {
                    enhanced?.Dispose();
                }
                return frame;
            }
            finally
            {
                tempMat?.Dispose();
            }
        }

        /// <summary>
        /// 获取缓存的伽马查找表
        /// </summary>
        private static readonly Dictionary<double, Mat> _gammaLookupCache = new Dictionary<double, Mat>();
        private Mat GetCachedGammaLookupTable(double gamma)
        {
            if (!_gammaLookupCache.TryGetValue(gamma, out Mat lookupTable))
            {
                lookupTable = new Mat(1, 256, MatType.CV_8U);
                var lookupTableData = lookupTable.GetGenericIndexer<byte>();
                
                for (int i = 0; i < 256; i++)
                {
                    lookupTableData[0, i] = (byte)(Math.Pow(i / 255.0, 1.0 / gamma) * 255);
                }
                
                _gammaLookupCache[gamma] = lookupTable;
            }
            return lookupTable;
        }

        /// <summary>
        /// 获取缓存的锐化卷积核
        /// </summary>
        private static readonly Dictionary<double, Mat> _sharpnessKernelCache = new Dictionary<double, Mat>();
        private Mat GetCachedSharpnessKernel(double sharpness)
        {
            if (!_sharpnessKernelCache.TryGetValue(sharpness, out Mat kernel))
            {
                kernel = new Mat(3, 3, MatType.CV_32F, new float[,]
                {
                    { 0, -(float)sharpness, 0 },
                    { -(float)sharpness, 1 + 4 * (float)sharpness, -(float)sharpness },
                    { 0, -(float)sharpness, 0 }
                });
                
                _sharpnessKernelCache[sharpness] = kernel;
            }
            return kernel;
        }

        /// <summary>
        /// 更新帧率
        /// </summary>
        private void UpdateFrameRate()
        {
            _frameCount++;
            
            if (_fpsStopwatch.ElapsedMilliseconds >= 1000)
            {
                CurrentFps = _frameCount * 1000.0 / _fpsStopwatch.ElapsedMilliseconds;
                _frameCount = 0;
                _fpsStopwatch.Restart();
                
                // 触发自适应帧率调整
                AdjustAdaptiveFrameRate();
            }
        }

        /// <summary>
        /// 自适应帧率控制 - 根据系统性能动态调整帧率
        /// </summary>
        private void AdjustAdaptiveFrameRate()
        {
            try
            {
                lock (_performanceStatsLock)
                {
                    // 计算目标延迟时间（毫秒）
                    double targetDelayMs = 1000.0 / _targetFps;
                    
                    // 检查队列状态并进行自适应调整
                    int processingQueueSize = _processingQueue.Count;
                    int recordingQueueSize = _recordingQueue.Count;
                    
                    // 如果队列积压严重，增加延迟以减缓帧生成速度
                    if (processingQueueSize > MaxProcessingQueueSize * 0.8 || recordingQueueSize > MaxRecordingQueueSize * 0.8)
                    {
                        _adaptiveDelayMs = Math.Min(_adaptiveDelayMs + 2, 30); // 最大延迟30ms
                        LogUtil.Debug($"IndustrialCameraManager: 队列积压严重(处理:{processingQueueSize}, 录像:{recordingQueueSize}), 增加延迟到{_adaptiveDelayMs}ms");
                    }
                    // 如果当前帧率低于目标帧率的80%，增加延迟
                    else if (CurrentFps < _targetFps * 0.8)
                    {
                        _adaptiveDelayMs = Math.Min(_adaptiveDelayMs + 1, 20); // 最大延迟20ms
                        LogUtil.Debug($"IndustrialCameraManager: 帧率过低({CurrentFps:F1}), 增加延迟到{_adaptiveDelayMs}ms");
                    }
                    // 如果队列状态良好且帧率稳定，减少延迟
                    else if (CurrentFps > _targetFps * 0.95 && _adaptiveDelayMs > 0 && 
                             processingQueueSize < MaxProcessingQueueSize * 0.3 && 
                             recordingQueueSize < MaxRecordingQueueSize * 0.3)
                    {
                        _adaptiveDelayMs = Math.Max(_adaptiveDelayMs - 0.5, 0);
                        LogUtil.Debug($"IndustrialCameraManager: 帧率稳定({CurrentFps:F1}), 队列状态良好, 减少延迟到{_adaptiveDelayMs}ms");
                    }
                    
                    // 记录帧时间历史，用于平滑调整
                    _frameTimeHistory.Enqueue(CurrentFps);
                    if (_frameTimeHistory.Count > MaxFrameTimeHistory)
                    {
                        _frameTimeHistory.Dequeue();
                    }
                }
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"IndustrialCameraManager: 自适应帧率调整失败 - {ex.Message}");
            }
        }

        /// <summary>
        /// 设置目标帧率
        /// </summary>
        /// <param name="targetFps">目标帧率</param>
        public void SetTargetFrameRate(double targetFps)
        {
            if (targetFps > 0 && targetFps <= 120)
            {
                _targetFps = targetFps;
                LogUtil.Info($"IndustrialCameraManager: 目标帧率设置为 {targetFps:F1} FPS");
            }
        }

        /// <summary>
        /// 获取性能统计信息
        /// </summary>
        /// <returns>性能统计信息</returns>
        public PerformanceStats GetPerformanceStats()
        {
            lock (_performanceStatsLock)
            {
                double averageFps = 0;
                if (_frameTimeHistory.Count > 0)
                {
                    averageFps = _frameTimeHistory.Average();
                }

                return new PerformanceStats
                {
                    CurrentFps = CurrentFps,
                    AverageFps = averageFps,
                    DroppedFrames = _droppedFrameCount,
                    TargetFps = _targetFps,
                    AdaptiveDelayMs = _adaptiveDelayMs,
                    AverageProcessingTime = _averageProcessingTime,
                    ProcessingQueueSize = _processingQueue.Count,
                    RecordingQueueSize = _recordingQueue.Count
                };
            }
        }

        /// <summary>
        /// 重置性能统计
        /// </summary>
        public void ResetPerformanceStats()
        {
            lock (_performanceStatsLock)
            {
                _droppedFrameCount = 0;
                _frameTimeHistory.Clear();
                _averageProcessingTime = 0;
                _adaptiveDelayMs = 0;
                LogUtil.Info("IndustrialCameraManager: 性能统计已重置");
            }
        }

        /// <summary>
        /// 执行内存管理和清理
        /// </summary>
        /// <param name="state">定时器状态</param>
        private void PerformMemoryManagement(object state)
        {
            try
            {
                lock (_memoryManagementLock)
                {
                    var now = DateTime.Now;
                    var timeSinceLastCleanup = now - _lastMemoryCleanup;
                    
                    // 如果距离上次清理超过30秒，执行内存清理
                    if (timeSinceLastCleanup.TotalSeconds >= 30)
                    {
                        // 获取当前内存使用情况
                        long memoryBefore = GC.GetTotalMemory(false);
                        
                        // 清理队列中的过期帧
                        CleanupExpiredFrames();
                        
                        // 强制垃圾回收
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        GC.Collect();
                        
                        long memoryAfter = GC.GetTotalMemory(false);
                        long memoryFreed = memoryBefore - memoryAfter;
                        
                        _lastMemoryCleanup = now;
                        
                        LogUtil.Debug($"IndustrialCameraManager: 内存清理完成，释放 {memoryFreed / 1024 / 1024:F2} MB 内存");
                        
                        // 记录队列状态
                        LogUtil.Debug($"IndustrialCameraManager: 队列状态 - 处理队列: {_processingQueue.Count}/{MaxProcessingQueueSize}, 录像队列: {_recordingQueue.Count}/{MaxRecordingQueueSize}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"IndustrialCameraManager: 内存管理失败 - {ex.Message}");
            }
        }

        /// <summary>
        /// 清理过期的帧数据
        /// </summary>
        private void CleanupExpiredFrames()
        {
            try
            {
                // 如果处理队列过大，清理一些旧帧
                if (_processingQueue.Count > MaxProcessingQueueSize * 0.7)
                {
                    int framesToClean = (int)(_processingQueue.Count * 0.1); // 清理10%的帧
                    int cleanedCount = 0;
                    
                    for (int i = 0; i < framesToClean && _processingQueue.TryDequeue(out Mat frame); i++)
                    {
                        frame?.Dispose();
                        cleanedCount++;
                    }
                    
                    if (cleanedCount > 0)
                    {
                        LogUtil.Debug($"IndustrialCameraManager: 清理了 {cleanedCount} 个处理队列中的过期帧");
                    }
                }
                
                // 如果录像队列过大，清理一些旧帧
                if (_recordingQueue.Count > MaxRecordingQueueSize * 0.7)
                {
                    int framesToClean = (int)(_recordingQueue.Count * 0.1); // 清理10%的帧
                    int cleanedCount = 0;
                    
                    for (int i = 0; i < framesToClean && _recordingQueue.TryDequeue(out Mat frame); i++)
                    {
                        frame?.Dispose();
                        cleanedCount++;
                    }
                    
                    if (cleanedCount > 0)
                    {
                        LogUtil.Debug($"IndustrialCameraManager: 清理了 {cleanedCount} 个录像队列中的过期帧");
                    }
                }
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"IndustrialCameraManager: 清理过期帧失败 - {ex.Message}");
            }
        }

        /// <summary>
        /// 启动异步处理流水线
        /// </summary>
        private void StartAsyncProcessingPipeline()
        {
            try
            {
                _processingCancellationToken = new CancellationTokenSource();
                _processingTasks = new Task[ProcessingThreadCount];

                for (int i = 0; i < ProcessingThreadCount; i++)
                {
                    int threadIndex = i;
                    _processingTasks[i] = Task.Run(async () => await ProcessingWorker(threadIndex, _processingCancellationToken.Token));
                }

                LogUtil.Info($"IndustrialCameraManager: 异步处理流水线已启动，工作线程数: {ProcessingThreadCount}");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraManager: 启动异步处理流水线失败 - {ex.Message}");
            }
        }

        /// <summary>
        /// 停止异步处理流水线
        /// </summary>
        private async Task StopAsyncProcessingPipeline()
        {
            try
            {
                if (_processingCancellationToken != null)
                {
                    _processingCancellationToken.Cancel();
                    
                    if (_processingTasks != null)
                    {
                        await Task.WhenAll(_processingTasks);
                    }
                    
                    _processingCancellationToken.Dispose();
                    _processingCancellationToken = null;
                    _processingTasks = null;
                }

                // 清理处理队列中的剩余帧
                while (_processingQueue.TryDequeue(out Mat frame))
                {
                    frame?.Dispose();
                }

                LogUtil.Info("IndustrialCameraManager: 异步处理流水线已停止");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraManager: 停止异步处理流水线失败 - {ex.Message}");
            }
        }

        /// <summary>
        /// 异步处理工作线程
        /// </summary>
        /// <param name="threadIndex">线程索引</param>
        /// <param name="cancellationToken">取消令牌</param>
        private async Task ProcessingWorker(int threadIndex, CancellationToken cancellationToken)
        {
            LogUtil.Debug($"IndustrialCameraManager: 处理线程 {threadIndex} 已启动");

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    // 等待队列中有帧可处理
                    await _processingQueueSemaphore.WaitAsync(cancellationToken);

                    if (_processingQueue.TryDequeue(out Mat frame))
                    {
                        var processingStart = DateTime.Now;

                        try
                        {
                            // 异步处理帧（图像增强、分析等）
                            var processedFrame = await ProcessFrameAsync(frame, cancellationToken);
                            
                            if (processedFrame != null)
                            {
                                // 更新双缓冲区
                                SwapFrameBufferLockFree(processedFrame);
                                
                                // 如果正在录像，将帧添加到录像队列
                                if (IsRecording && _recordingQueue != null)
                                {
                                    try
                                    {
                                        // 克隆帧用于录像（避免与其他用途冲突）
                                        var frameToRecord = processedFrame.Clone();
                                        _recordingQueue.Enqueue(frameToRecord);
                                        
                                        // 控制录像队列大小，避免内存溢出
                                        if (_recordingQueue.Count > MaxRecordingQueueSize)
                                        {
                                            // 批量丢弃多个旧帧以快速释放内存
                                            int framesToDrop = Math.Min(50, _recordingQueue.Count - MaxRecordingQueueSize + 50);
                                            int droppedCount = 0;
                                            
                                            for (int i = 0; i < framesToDrop && _recordingQueue.TryDequeue(out Mat oldFrame); i++)
                                            {
                                                oldFrame?.Dispose();
                                                droppedCount++;
                                                Interlocked.Increment(ref _droppedFrameCount);
                                            }
                                            
                                            if (droppedCount > 0)
                                            {
                                                LogUtil.Debug($"IndustrialCameraManager: 录像队列已满，批量丢弃 {droppedCount} 个最旧的帧");
                                            }
                                        }
                                    }
                                    catch (Exception recordEx)
                                    {
                                        LogUtil.Warning($"IndustrialCameraManager: 添加帧到录像队列失败 - {recordEx.Message}");
                                    }
                                }
                                
                                // 触发帧捕获事件
                                var bitmapSource = SafeMatToBitmapSource(processedFrame);
                                FrameCaptured?.Invoke(this, new FrameCapturedEventArgs(bitmapSource, processedFrame.Clone()));
                                
                                processedFrame.Dispose();
                            }

                            // 更新处理时间统计
                            var processingTime = (DateTime.Now - processingStart).TotalMilliseconds;
                            lock (_performanceStatsLock)
                            {
                                _averageProcessingTime = (_averageProcessingTime + processingTime) / 2.0;
                            }
                        }
                        catch (Exception ex)
                        {
                            LogUtil.Warning($"IndustrialCameraManager: 线程 {threadIndex} 处理帧失败 - {ex.Message}");
                        }
                        finally
                        {
                            frame?.Dispose();
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消，不记录错误
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraManager: 处理线程 {threadIndex} 异常 - {ex.Message}");
            }

            LogUtil.Debug($"IndustrialCameraManager: 处理线程 {threadIndex} 已停止");
        }

        /// <summary>
        /// 异步处理单个帧
        /// </summary>
        /// <param name="frame">要处理的帧</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>处理后的帧</returns>
        private async Task<Mat> ProcessFrameAsync(Mat frame, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                if (frame == null || frame.Empty())
                    return null;

                try
                {
                    // 应用图像增强
                    var enhancedFrame = ApplyImageEnhancement(frame);
                    return enhancedFrame;
                }
                catch (Exception ex)
                {
                    LogUtil.Warning($"IndustrialCameraManager: 异步帧处理失败 - {ex.Message}");
                    return frame.Clone(); // 返回原始帧的副本
                }
            }, cancellationToken);
        }

        /// <summary>
        /// 将帧添加到异步处理队列
        /// </summary>
        /// <param name="frame">要处理的帧</param>
        /// <returns>是否成功添加到队列</returns>
        private bool EnqueueFrameForProcessing(Mat frame)
        {
            try
            {
                if (frame == null || frame.Empty())
                    return false;

                // 检查队列大小，避免内存溢出
                if (_processingQueue.Count >= MaxProcessingQueueSize)
                {
                    // 批量丢弃多个旧帧以快速释放内存
                    int framesToDrop = Math.Min(20, _processingQueue.Count - MaxProcessingQueueSize + 20);
                    int droppedCount = 0;
                    
                    for (int i = 0; i < framesToDrop && _processingQueue.TryDequeue(out Mat oldFrame); i++)
                    {
                        oldFrame?.Dispose();
                        droppedCount++;
                        Interlocked.Increment(ref _droppedFrameCount);
                    }
                    
                    if (droppedCount > 0)
                    {
                        LogUtil.Debug($"IndustrialCameraManager: 处理队列已满，批量丢弃 {droppedCount} 个最旧的帧");
                    }
                }

                // 添加新帧到队列
                _processingQueue.Enqueue(frame.Clone());
                _processingQueueSemaphore.Release();
                
                return true;
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"IndustrialCameraManager: 添加帧到处理队列失败 - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 设置自动对焦
        /// </summary>
        /// <param name="enabled">是否启用</param>
        private void SetAutoFocus(bool enabled)
        {
            try
            {
                if (_camera != null && _camera.IsOpened())
                {
                    _camera.Set(VideoCaptureProperties.AutoFocus, enabled ? 1 : 0);
                    LogUtil.Debug($"IndustrialCameraManager: 自动对焦设置为 {enabled}");
                }
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"IndustrialCameraManager: 设置自动对焦失败 - {ex.Message}");
            }
        }

        /// <summary>
        /// 设置自动曝光
        /// </summary>
        /// <param name="enabled">是否启用</param>
        private void SetAutoExposure(bool enabled)
        {
            try
            {
                if (_camera != null && _camera.IsOpened())
                {
                    _camera.Set(VideoCaptureProperties.AutoExposure, enabled ? 0.75 : 0.25);
                    LogUtil.Debug($"IndustrialCameraManager: 自动曝光设置为 {enabled}");
                }
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"IndustrialCameraManager: 设置自动曝光失败 - {ex.Message}");
            }
        }

        /// <summary>
        /// 安全的Mat到BitmapSource转换方法
        /// 解决OpenCvSharp.WpfExtensions.ToBitmapSource()的颜色转换问题
        /// </summary>
        /// <param name="mat">要转换的Mat对象</param>
        /// <returns>安全转换的BitmapSource</returns>
        private BitmapSource SafeMatToBitmapSource(Mat mat)
        {
            if (mat == null || mat.Empty())
            {
                LogUtil.Warning("IndustrialCameraManager: SafeMatToBitmapSource - 输入Mat为空或无效");
                return null;
            }

            try
            {
                // 验证Mat的基本属性
                if (mat.Width <= 0 || mat.Height <= 0)
                {
                    LogUtil.Warning($"IndustrialCameraManager: SafeMatToBitmapSource - Mat尺寸无效: {mat.Width}x{mat.Height}");
                    return null;
                }

                if (mat.Channels() <= 0 || mat.Channels() > 4)
                {
                    LogUtil.Warning($"IndustrialCameraManager: SafeMatToBitmapSource - Mat通道数无效: {mat.Channels()}");
                    return null;
                }

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
                        //LogUtil.Debug("IndustrialCameraManager: 转换灰度图像为RGB格式");
                    }
                    else if (mat.Channels() == 3)
                    {
                        // BGR转RGB - 这是关键的颜色修复
                        convertedMat = new Mat();
                        Cv2.CvtColor(mat, convertedMat, ColorConversionCodes.BGR2RGB);
                        //LogUtil.Debug("IndustrialCameraManager: 转换BGR图像为RGB格式（修复颜色显示）");
                    }
                    else if (mat.Channels() == 4)
                    {
                        // BGRA转RGBA
                        convertedMat = new Mat();
                        Cv2.CvtColor(mat, convertedMat, ColorConversionCodes.BGRA2RGBA);
                        //LogUtil.Debug("IndustrialCameraManager: 转换BGRA图像为RGBA格式");
                    }
                    else
                    {
                        convertedMat = mat.Clone();
                        //LogUtil.Debug($"IndustrialCameraManager: 使用原始图像格式（{mat.Channels()}通道）");
                    }

                    // 验证转换后的Mat
                    if (convertedMat == null || convertedMat.Empty())
                    {
                        LogUtil.Warning("IndustrialCameraManager: SafeMatToBitmapSource - 颜色转换后Mat为空");
                        return null;
                    }

                    // 使用安全的方式创建BitmapSource
                    var width = convertedMat.Width;
                    var height = convertedMat.Height;
                    var channels = convertedMat.Channels();
                    var stride = width * channels;
                    
                    // 验证数据大小
                    var expectedDataSize = height * stride;
                    if (expectedDataSize <= 0 || expectedDataSize > int.MaxValue / 2)
                    {
                        LogUtil.Warning($"IndustrialCameraManager: SafeMatToBitmapSource - 数据大小异常: {expectedDataSize}");
                        return null;
                    }

                    // 验证Mat数据指针
                    if (convertedMat.Data == IntPtr.Zero)
                    {
                        LogUtil.Warning("IndustrialCameraManager: SafeMatToBitmapSource - Mat数据指针为空");
                        return null;
                    }
                    
                    // 创建字节数组副本，避免直接使用Mat的内存指针
                    var imageData = new byte[expectedDataSize];
                    try
                    {
                        System.Runtime.InteropServices.Marshal.Copy(convertedMat.Data, imageData, 0, expectedDataSize);
                    }
                    catch (Exception copyEx)
                    {
                        LogUtil.Error($"IndustrialCameraManager: SafeMatToBitmapSource - 内存拷贝失败: {copyEx.Message}");
                        return null;
                    }

                    // 确定像素格式
                    System.Windows.Media.PixelFormat pixelFormat;
                    switch (channels)
                    {
                        case 1:
                            pixelFormat = System.Windows.Media.PixelFormats.Gray8;
                            break;
                        case 3:
                            pixelFormat = System.Windows.Media.PixelFormats.Rgb24;
                            break; 
                        default:
                            LogUtil.Warning($"IndustrialCameraManager: SafeMatToBitmapSource - 不支持的通道数: {channels}");
                            return null;
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
                LogUtil.Error($"IndustrialCameraManager: SafeMatToBitmapSource转换失败 - 类型:{ex.GetType().Name}, 消息:{ex.Message}, 堆栈:{ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// 获取OpenCV属性枚举
        /// </summary>
        /// <param name="property">相机属性</param>
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
        /// 属性变更通知
        /// </summary>
        /// <param name="propertyName">属性名称</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 性能统计更新事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">性能统计事件参数</param>
        private void OnPerformanceStatsUpdated(object sender, WpfAppNew.Utils.PerformanceStatsEventArgs e)
        {
            try
            {
                var stats = e.Stats;
                
                // 记录性能统计信息（仅在调试模式下）
                #if DEBUG
                LogUtil.Debug($"性能统计 - FPS: {stats.FPS:F1}, 帧时间: {stats.AverageFrameTime:F1}ms, " +
                             $"内存: {stats.MemoryUsage}MB, CPU: {stats.CpuUsage:F1}%, UI繁忙: {stats.IsUIThreadBusy}");
                #endif
                
                // 如果性能指标异常，记录警告
                if (stats.AverageFrameTime > 50) // 帧时间超过50ms
                {
                    LogUtil.Warning($"帧处理时间过长: {stats.AverageFrameTime:F1}ms");
                }
                
                if (stats.MemoryUsage > 1000) // 内存使用超过1GB
                {
                    LogUtil.Warning($"内存使用量过高: {stats.MemoryUsage}MB");
                }
                
                if (stats.IsUIThreadBusy)
                {
                    LogUtil.Warning("UI线程繁忙，可能影响用户体验");
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"性能统计处理失败: {ex.Message}");
            }
        }

        #endregion
    }

    #region 枚举和数据类

    /// <summary>
    /// 相机属性枚举
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
        /// 对焦
        /// </summary>
        Focus,

        /// <summary>
        /// 缩放
        /// </summary>
        Zoom
    }

    /// <summary>
    /// 图像格式枚举
    /// </summary>
    public enum ImageFormat
    {
        /// <summary>
        /// JPEG格式
        /// </summary>
        JPEG,

        /// <summary>
        /// PNG格式
        /// </summary>
        PNG,

        /// <summary>
        /// TIFF格式
        /// </summary>
        TIFF,

        /// <summary>
        /// BMP格式
        /// </summary>
        BMP
    }

    /// <summary>
    /// 图像增强设置
    /// </summary>
    public class ImageEnhancementSettings
    {
        /// <summary>
        /// 是否启用图像增强
        /// </summary>
        public bool IsEnabled { get; set; } = false;

        /// <summary>
        /// 亮度调整 (-100 到 100)
        /// </summary>
        public double Brightness { get; set; } = 0;

        /// <summary>
        /// 对比度调整 (0.1 到 3.0)
        /// </summary>
        public double Contrast { get; set; } = 1.0;

        /// <summary>
        /// 饱和度调整 (0.1 到 3.0)
        /// </summary>
        public double Saturation { get; set; } = 0.1;

        /// <summary>
        /// 伽马校正 (0.1 到 3.0)
        /// </summary>
        public double Gamma { get; set; } = 1.0;

        /// <summary>
        /// 锐化强度 (0 到 2.0)
        /// </summary>
        public double Sharpness { get; set; } = 0;

        /// <summary>
        /// 降噪强度 (0 到 30)
        /// </summary>
        public float NoiseReduction { get; set; } = 0;

        /// <summary>
        /// 是否启用降噪
        /// </summary>
        public bool IsNoiseReductionEnabled { get; set; } = false;

        /// <summary>
        /// 是否启用直方图均衡化
        /// </summary>
        public bool HistogramEqualization { get; set; } = false;

        /// <summary>
        /// 是否启用边缘增强
        /// </summary>
        public bool EdgeEnhancement { get; set; } = false;
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

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="bitmapSource">位图源</param>
        /// <param name="frame">Mat帧</param>
        public FrameCapturedEventArgs(BitmapSource bitmapSource, Mat frame)
        {
            BitmapSource = bitmapSource;
            Frame = frame;
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// 图像捕获事件参数
    /// </summary>
    public class ImageCapturedEventArgs : EventArgs
    {
        /// <summary>
        /// 保存路径
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="filePath">保存路径</param>
        /// <param name="success">是否成功</param>
        /// <param name="errorMessage">错误信息</param>
        public ImageCapturedEventArgs(string filePath, bool success, string errorMessage = null)
        {
            FilePath = filePath;
            Success = success;
            ErrorMessage = errorMessage;
        }
    }

    /// <summary>
    /// 录制停止事件参数
    /// </summary>
    public class RecordingStoppedEventArgs : EventArgs
    {
        /// <summary>
        /// 保存路径
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// 录制时长
        /// </summary>
        public TimeSpan Duration { get; }

        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="filePath">保存路径</param>
        /// <param name="duration">录制时长</param>
        /// <param name="success">是否成功</param>
        /// <param name="errorMessage">错误信息</param>
        public RecordingStoppedEventArgs(string filePath, TimeSpan duration, bool success, string errorMessage = null)
        {
            FilePath = filePath;
            Duration = duration;
            Success = success;
            ErrorMessage = errorMessage;
        }
    }

    /// <summary>
    /// 状态变更事件参数
    /// </summary>
    public class StatusChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 是否正在捕获
        /// </summary>
        public bool IsCapturing { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="isCapturing">是否正在捕获</param>
        public StatusChangedEventArgs(bool isCapturing)
        {
            IsCapturing = isCapturing;
        }
    }

    /// <summary>
    /// 录制状态变更事件参数
    /// </summary>
    public class RecordingStatusChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 是否正在录制
        /// </summary>
        public bool IsRecording { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="isRecording">是否正在录制</param>
        public RecordingStatusChangedEventArgs(bool isRecording)
        {
            IsRecording = isRecording;
        }
    }

    /// <summary>
    /// 错误发生事件参数
    /// </summary>
    public class ErrorOccurredEventArgs : EventArgs
    {
        /// <summary>
        /// 异常信息
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="exception">异常</param>
        public ErrorOccurredEventArgs(Exception exception)
        {
            Exception = exception;
        }
    }

    /// <summary>
    /// 连接状态事件参数
    /// </summary>
    public class ConnectionStatusEventArgs : EventArgs
    {
        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool IsConnected { get; }

        /// <summary>
        /// 设备索引
        /// </summary>
        public int DeviceIndex { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="isConnected">是否已连接</param>
        /// <param name="deviceIndex">设备索引</param>
        public ConnectionStatusEventArgs(bool isConnected, int deviceIndex)
        {
            IsConnected = isConnected;
            DeviceIndex = deviceIndex;
        }
    }

    /// <summary>
    /// 预览状态事件参数
    /// </summary>
    public class PreviewStatusEventArgs : EventArgs
    {
        /// <summary>
        /// 是否正在预览
        /// </summary>
        public bool IsPreviewing { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="isPreviewing">是否正在预览</param>
        public PreviewStatusEventArgs(bool isPreviewing)
        {
            IsPreviewing = isPreviewing;
        }
    }

    /// <summary>
    /// 录制状态事件参数
    /// </summary>
    public class RecordingStatusEventArgs : EventArgs
    {
        /// <summary>
        /// 是否正在录制
        /// </summary>
        public bool IsRecording { get; }

        /// <summary>
        /// 录制文件路径
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="isRecording">是否正在录制</param>
        /// <param name="filePath">录制文件路径</param>
        public RecordingStatusEventArgs(bool isRecording, string filePath = null)
        {
            IsRecording = isRecording;
            FilePath = filePath;
        }
    }

    /// <summary>
    /// 拍照完成事件参数
    /// </summary>
    public class PhotoCapturedEventArgs : EventArgs
    {
        /// <summary>
        /// 保存路径
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// 图像格式
        /// </summary>
        public ImageFormat Format { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="filePath">保存路径</param>
        /// <param name="success">是否成功</param>
        /// <param name="format">图像格式</param>
        public PhotoCapturedEventArgs(string filePath, bool success, ImageFormat format)
        {
            FilePath = filePath;
            Success = success;
            Format = format;
        }
    }

    /// <summary>
    /// 录像完成事件参数
    /// </summary>
    public class VideoRecordedEventArgs : EventArgs
    {
        /// <summary>
        /// 保存路径
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// 录制时长
        /// </summary>
        public TimeSpan Duration { get; }

        /// <summary>
        /// 帧数
        /// </summary>
        public int FrameCount { get; }

        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="filePath">保存路径</param>
        /// <param name="duration">录制时长</param>
        /// <param name="frameCount">帧数</param>
        /// <param name="success">是否成功</param>
        public VideoRecordedEventArgs(string filePath, TimeSpan duration, int frameCount, bool success)
        {
            FilePath = filePath;
            Duration = duration;
            FrameCount = frameCount;
            Success = success;
        }
    }

    /// <summary>
    /// 性能统计事件参数
    /// </summary>
    public class PerformanceStatsEventArgs : EventArgs
    {
        /// <summary>
        /// 当前帧率
        /// </summary>
        public double CurrentFps { get; }

        /// <summary>
        /// 平均帧率
        /// </summary>
        public double AverageFps { get; }

        /// <summary>
        /// 丢帧数
        /// </summary>
        public int DroppedFrames { get; }

        /// <summary>
        /// CPU使用率
        /// </summary>
        public double CpuUsage { get; }

        /// <summary>
        /// 内存使用量（MB）
        /// </summary>
        public double MemoryUsage { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="currentFps">当前帧率</param>
        /// <param name="averageFps">平均帧率</param>
        /// <param name="droppedFrames">丢帧数</param>
        /// <param name="cpuUsage">CPU使用率</param>
        /// <param name="memoryUsage">内存使用量</param>
        public PerformanceStatsEventArgs(double currentFps, double averageFps, int droppedFrames, double cpuUsage, double memoryUsage)
        {
            CurrentFps = currentFps;
            AverageFps = averageFps;
            DroppedFrames = droppedFrames;
            CpuUsage = cpuUsage;
            MemoryUsage = memoryUsage;
        }
    }

    /// <summary>
    /// 相机默认参数设置类
    /// 用于存储从设备获取的默认参数值
    /// </summary>
    public class CameraDefaultSettings
    {
        /// <summary>
        /// 曝光值
        /// </summary>
        public double ExposureValue { get; set; } = 0.5;

        /// <summary>
        /// 增益值
        /// </summary>
        public double GainValue { get; set; } = 0.5;

        /// <summary>
        /// 对比度值
        /// </summary>
        public double ContrastValue { get; set; } = 1.0;

        /// <summary>
        /// 饱和度值
        /// </summary>
        public double SaturationValue { get; set; } = 1.0;

        /// <summary>
        /// 亮度值
        /// </summary>
        public double BrightnessValue { get; set; } = 0.5;

        /// <summary>
        /// 伽马校正值
        /// </summary>
        public double GammaValue { get; set; } = 1.0;

        /// <summary>
        /// 锐化值
        /// </summary>
        public double SharpnessValue { get; set; } = 1.0;

        /// <summary>
        /// 是否启用自动白平衡
        /// </summary>
        public bool IsAutoWhiteBalance { get; set; } = true;

        /// <summary>
        /// 是否启用降噪
        /// </summary>
        public bool IsNoiseReductionEnabled { get; set; } = false;
    }

    /// <summary>
    /// 性能统计信息类
    /// </summary>
    public class PerformanceStats
    {
        /// <summary>
        /// 当前帧率
        /// </summary>
        public double CurrentFps { get; set; }

        /// <summary>
        /// 平均帧率
        /// </summary>
        public double AverageFps { get; set; }

        /// <summary>
        /// 丢帧数量
        /// </summary>
        public int DroppedFrames { get; set; }

        /// <summary>
        /// 目标帧率
        /// </summary>
        public double TargetFps { get; set; }

        /// <summary>
        /// 自适应延迟（毫秒）
        /// </summary>
        public double AdaptiveDelayMs { get; set; }

        /// <summary>
        /// 平均处理时间（毫秒）
        /// </summary>
        public double AverageProcessingTime { get; set; }

        /// <summary>
        /// 处理队列大小
        /// </summary>
        public int ProcessingQueueSize { get; set; }

        /// <summary>
        /// 录像队列大小
        /// </summary>
        public int RecordingQueueSize { get; set; }

        /// <summary>
        /// 性能效率百分比
        /// </summary>
        public double EfficiencyPercentage => TargetFps > 0 ? (CurrentFps / TargetFps) * 100 : 0;

        /// <summary>
        /// 转换为字符串表示
        /// </summary>
        /// <returns>性能统计的字符串表示</returns>
        public override string ToString()
        {
            return $"当前帧率: {CurrentFps:F1} FPS, 平均帧率: {AverageFps:F1} FPS, " +
                   $"目标帧率: {TargetFps:F1} FPS, 效率: {EfficiencyPercentage:F1}%, " +
                   $"丢帧: {DroppedFrames}, 延迟: {AdaptiveDelayMs:F1}ms, " +
                   $"处理队列: {ProcessingQueueSize}, 录像队列: {RecordingQueueSize}";
        }
    }

    #endregion
}