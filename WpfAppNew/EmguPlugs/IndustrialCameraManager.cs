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
using Tools.Extend;

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
        /// 视频录制器
        /// </summary>
        private VideoWriter _videoWriter;

        /// <summary>
        /// 当前帧
        /// </summary>
        private Mat _currentFrame;

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
        /// 同步锁
        /// </summary>
        private readonly ReaderWriterLockSlim _lockObject = new ReaderWriterLockSlim();

        /// <summary>
        /// 预览帧率
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
            
            LogUtil.Info("IndustrialCameraManager: 工业相机管理器初始化完成");
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
                
                // 使用读锁获取当前帧，避免死锁，设置超时时间
                bool lockTaken = false;
                try
                {
                    lockTaken = _lockObject.TryEnterReadLock(500);
                    if (lockTaken)
                    {
                        if (_currentFrame != null && !_currentFrame.Empty())
                        {
                            captureFrame = _currentFrame.Clone();
                        }
                    }
                }
                finally
                {
                    if (lockTaken)
                    {
                        _lockObject.ExitReadLock();
                    }
                }

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
                        // 在后台线程中应用图像增强，避免阻塞UI
                        enhancedFrame = ApplyImageEnhancement(captureFrame);

                        // 保存图像
                        switch (format)
                        {
                            case ImageFormat.JPEG:
                                return enhancedFrame.SaveImage(filePath, new int[] { (int)ImwriteFlags.JpegQuality, quality });
                            case ImageFormat.PNG:
                                return enhancedFrame.SaveImage(filePath, new int[] { (int)ImwriteFlags.PngCompression, 9 });
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

                // 线程安全地检查当前帧，增加重试机制
                Mat currentFrameCopy = null;
                bool lockTaken = false;
                int retryCount = 0;
                const int maxRetries = 3;
                
                while (retryCount < maxRetries && currentFrameCopy == null)
                {
                    try
                    {
                        // 使用读锁获取当前帧，增加超时时间到500ms
                        lockTaken = _lockObject.TryEnterReadLock(500);
                        if (!lockTaken)
                        {
                            retryCount++;
                            LogUtil.Warning($"IndustrialCameraManager: 第{retryCount}次尝试获取锁失败，等待后重试");
                            
                            if (retryCount < maxRetries)
                            {
                                Thread.Sleep(100); // 等待100ms后重试
                                continue;
                            }
                            else
                            {
                                LogUtil.Error("IndustrialCameraManager: 多次尝试后仍无法获取锁，开始录像失败");
                                return false;
                            }
                        }

                        if (_currentFrame == null || _currentFrame.Empty())
                        {
                            LogUtil.Error("IndustrialCameraManager: 当前无有效帧，无法开始录像");
                            return false;
                        }

                        // 创建当前帧的副本，避免在锁外访问时被释放
                        currentFrameCopy = _currentFrame.Clone();
                        break; // 成功获取帧，退出重试循环
                    }
                    finally
                    {
                        if (lockTaken)
                        {
                            _lockObject.ExitReadLock();
                            lockTaken = false;
                        }
                    }
                }

                if (currentFrameCopy == null || currentFrameCopy.Empty())
                {
                    LogUtil.Error("IndustrialCameraManager: 无法获取有效帧副本，开始录像失败");
                    currentFrameCopy?.Dispose();
                    return false;
                }

                _recordingFilePath = filePath;
                var actualCodec = codec ?? FourCC.MJPG;
                
                // 使用当前帧的尺寸创建视频写入器
                var frameSize = new OpenCvSharp.Size(currentFrameCopy.Width, currentFrameCopy.Height);
                _videoWriter = new VideoWriter(filePath, actualCodec, fps, frameSize);

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
        public string StopRecording()
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

                // 等待一小段时间，确保正在进行的写入操作完成
                Thread.Sleep(50);

                // 安全地释放视频写入器，使用超时机制避免死锁
                bool lockTaken = _lockObject.TryEnterWriteLock(2000); // 2秒超时
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
                        _lockObject.ExitWriteLock();
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

                // 线程安全地检查当前帧，增加重试机制
                Mat currentFrameCopy = null;
                bool lockTaken = false;
                int retryCount = 0;
                const int maxRetries = 3;
                
                while (retryCount < maxRetries && currentFrameCopy == null)
                {
                    try
                    {
                        // 使用读锁获取当前帧，增加超时时间到500ms
                        lockTaken = _lockObject.TryEnterReadLock(500);
                        if (!lockTaken)
                        {
                            retryCount++;
                            LogUtil.Warning($"IndustrialCameraManager: 第{retryCount}次尝试获取锁失败，等待后重试");
                            
                            if (retryCount < maxRetries)
                            {
                                await Task.Delay(100); // 等待100ms后重试
                                continue;
                            }
                            else
                            {
                                LogUtil.Error("IndustrialCameraManager: 无法获取锁，开始录制失败");
                                return false;
                            }
                        }

                        // 检查是否有有效帧
                        if (_currentFrame != null && !_currentFrame.Empty())
                        {
                            currentFrameCopy = _currentFrame.Clone();
                        }
                    }
                    finally
                    {
                        if (lockTaken)
                        {
                            _lockObject.ExitReadLock();
                            lockTaken = false;
                        }
                    }
                    
                    if (currentFrameCopy == null)
                    {
                        retryCount++;
                        if (retryCount < maxRetries)
                        {
                            LogUtil.Warning($"IndustrialCameraManager: 第{retryCount}次获取帧失败，等待后重试");
                            await Task.Delay(100);
                        }
                    }
                }

                if (currentFrameCopy == null)
                {
                    LogUtil.Error("IndustrialCameraManager: 无法获取有效帧，开始录制失败");
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
                var width = currentFrameCopy.Width;
                var height = currentFrameCopy.Height;

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
        public async Task<bool> StopRecordingAsync()
        {
            try
            {
                if (!_isRecording)
                {
                    LogUtil.Info("IndustrialCameraManager: 录制未在进行中");
                    return true;
                }

                LogUtil.Info("IndustrialCameraManager: 停止录制视频");

                // 先设置录制状态为false，防止新的写入操作（直接使用属性，确保事件触发）
                IsRecording = false;

                // 等待一小段时间，确保正在进行的写入操作完成
                await Task.Delay(50);

                // 释放视频写入器，使用超时机制避免死锁
                await Task.Run(() =>
                {
                    bool lockTaken = _lockObject.TryEnterWriteLock(2000); // 2秒超时
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
                            _lockObject.ExitWriteLock();
                        }
                    }
                });

                var recordDuration = DateTime.Now - _recordStartTime;

                LogUtil.Info($"IndustrialCameraManager: 录制已停止 - 时长{recordDuration.TotalSeconds:F2}秒，帧数{_recordedFrameCount}");
                _recordingFilePath = null;
                return true;
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraManager: 停止录制失败 - {ex.Message}");
                ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(ex));
                return false;
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

            StopRecording();
            StopCaptureAsync().Wait();

            _previewTimer?.Stop();
            _previewTimer?.Dispose();

            _camera?.Release();
            _camera?.Dispose();
            _currentFrame?.Dispose();
            _processedFrame?.Dispose();

            _disposed = true;
            LogUtil.Info("IndustrialCameraManager: 资源已释放");
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
        /// 预览定时器事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">事件参数</param>
        private void OnPreviewTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (!_isPreviewing || _camera == null || !_camera.IsOpened())
            {
                return;
            }

            Mat frame = null;
            Mat processedFrame = null;
            BitmapSource bitmapSource = null;
            string currentStep = "初始化";
            
            try
            {
                // 第一步：获取原始帧（需要写锁保护相机访问）
                currentStep = "获取相机锁";
                bool lockTaken = _lockObject.TryEnterWriteLock(50); // 50ms超时
                if (!lockTaken)
                {
                    // 如果无法获取锁，跳过这一帧，避免阻塞
                    LogUtil.Debug("IndustrialCameraManager: 无法获取相机锁，跳过当前帧");
                    return;
                }
                
                try
                {
                    currentStep = "读取相机帧";
                    frame = new Mat();
                    if (!_camera.Read(frame) || frame.Empty())
                    {
                        frame?.Dispose();
                        LogUtil.Debug("IndustrialCameraManager: 相机读取失败或帧为空");
                        return;
                    }
                    
                    // 验证帧的基本属性
                    if (frame.Width <= 0 || frame.Height <= 0 || frame.Channels() <= 0)
                    {
                        LogUtil.Warning($"IndustrialCameraManager: 读取到无效帧 - 尺寸:{frame.Width}x{frame.Height}, 通道:{frame.Channels()}");
                        frame?.Dispose();
                        return;
                    }
                }
                finally
                {
                    _lockObject.ExitWriteLock();
                }

                // 第二步：处理帧（在锁外执行，提高并发性能）
                if (frame != null && !frame.Empty())
                {
                    // 添加调试信息
                    if (_frameCounter % 30 == 0) // 每30帧输出一次调试信息
                    {
                        LogUtil.Debug($"IndustrialCameraManager: 获取帧成功 - 尺寸:{frame.Width}x{frame.Height}, 通道:{frame.Channels()}, 类型:{frame.Type()}");
                        
                        // 检查图像是否全黑
                        try
                        {
                            var scalar = Cv2.Mean(frame);
                            LogUtil.Debug($"IndustrialCameraManager: 图像平均亮度 - R:{scalar.Val0:F2}, G:{scalar.Val1:F2}, B:{scalar.Val2:F2}");
                        }
                        catch (Exception meanEx)
                        {
                            LogUtil.Warning($"IndustrialCameraManager: 计算图像平均亮度失败 - {meanEx.Message}");
                        }
                    }

                    // 应用图像处理（在锁外执行）
                    currentStep = "处理帧";
                    try
                    {
                        processedFrame = ProcessFrame(frame);
                        if (processedFrame == null || processedFrame.Empty())
                        {
                            LogUtil.Warning("IndustrialCameraManager: ProcessFrame返回空帧");
                            return;
                        }
                    }
                    catch (Exception processEx)
                    {
                        LogUtil.Error($"IndustrialCameraManager: ProcessFrame失败 - {processEx.Message}");
                        throw new Exception($"帧处理失败: {processEx.Message}", processEx);
                    }
                    
                    // 创建位图源（在锁外执行）
                    currentStep = "转换位图源";
                    try
                    {
                        bitmapSource = SafeMatToBitmapSource(processedFrame);
                        if (bitmapSource == null)
                        {
                            LogUtil.Warning("IndustrialCameraManager: SafeMatToBitmapSource返回空值");
                            return;
                        }
                    }
                    catch (Exception bitmapEx)
                    {
                        LogUtil.Error($"IndustrialCameraManager: SafeMatToBitmapSource失败 - {bitmapEx.Message}");
                        throw new Exception($"位图转换失败: {bitmapEx.Message}", bitmapEx);
                    }

                    // 第三步：更新共享资源（需要写锁保护）
                    currentStep = "更新共享资源";
                    lockTaken = _lockObject.TryEnterWriteLock(20); // 减少到20ms超时，因为操作更快
                    if (lockTaken)
                    {
                        try
                        {
                            // 更新当前帧
                            _processedFrame?.Dispose();
                            _processedFrame = processedFrame.Clone();
                            
                            _currentFrame?.Dispose();
                            _currentFrame = processedFrame.Clone();

                            // 注意：录像逻辑已移至异步处理，避免在主帧处理中阻塞

                            // 更新性能统计
                            UpdatePerformanceStats();
                        }
                        finally
                        {
                            _lockObject.ExitWriteLock();
                        }
                    }
                    else
                    {
                        LogUtil.Debug("IndustrialCameraManager: 无法获取更新锁，跳过资源更新");
                    }

                    // 第四步：触发事件（在锁外执行，避免事件处理阻塞）
                    currentStep = "触发事件";
                    if (bitmapSource != null)
                    {
                        try
                        {
                            FrameCaptured?.Invoke(this, new FrameCapturedEventArgs(bitmapSource, processedFrame.Clone()));
                        }
                        catch (Exception eventEx)
                        {
                            LogUtil.Error($"IndustrialCameraManager: FrameCaptured事件处理失败 - {eventEx.Message}");
                            // 事件处理失败不应该影响主流程，所以不重新抛出异常
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 记录详细的错误信息，包括当前执行步骤
                var errorMessage = $"预览帧处理失败 - 步骤:{currentStep}, 类型:{ex.GetType().Name}, 消息:{ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $", 内部异常:{ex.InnerException.Message}";
                }
                
                LogUtil.Error($"IndustrialCameraManager: {errorMessage}");
                
                // 如果是特定的异常类型，提供更多上下文信息
                if (ex.Message.Contains("videoSample") || ex.Message.Contains("内存") || ex.Message.Contains("指针"))
                {
                    LogUtil.Error($"IndustrialCameraManager: 疑似内存访问问题 - 帧信息: frame={frame?.Width}x{frame?.Height}x{frame?.Channels()}, processedFrame={processedFrame?.Width}x{processedFrame?.Height}x{processedFrame?.Channels()}");
                }
                
                ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(ex));
            }
            finally
            {
                // 清理资源
                frame?.Dispose();
                processedFrame?.Dispose();
            }
        }

        /// <summary>
        /// 处理帧
        /// </summary>
        /// <param name="frame">输入帧</param>
        /// <returns>处理后的帧</returns>
        private Mat ProcessFrame(Mat frame)
        {
            var processed = frame.Clone();

            try
            {
                // 记录原始图像信息（用于调试）
                var meanBrightness = Cv2.Mean(processed);
                var avgBrightness = (meanBrightness.Val0 + meanBrightness.Val1 + meanBrightness.Val2) / 3.0;
                
                // 每30帧输出一次调试信息
                if (_frameCount % 30 == 0)
                {
                    LogUtil.Debug($"IndustrialCameraManager: 原始图像 - 尺寸:{processed.Width}x{processed.Height}, 通道:{processed.Channels()}, 类型:{processed.Type()}, 平均亮度:R{meanBrightness.Val2:F1}/G{meanBrightness.Val1:F1}/B{meanBrightness.Val0:F1}");
                }

                // 仅在用户明确启用图像增强时才应用处理
                if (_enhancementSettings != null && _enhancementSettings.IsEnabled)
                {
                    processed = ApplyImageEnhancement(processed);
                    LogUtil.Debug($"IndustrialCameraManager: 应用用户自定义图像增强");
                }

                return processed;
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"IndustrialCameraManager: 帧处理失败 - {ex.Message}");
                processed.Dispose();
                return frame.Clone();
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
                        // 在锁外应用图像增强，减少锁持有时间
                        var enhancedFrame = ApplyImageEnhancement(frame);

                        // 使用写锁更新当前帧，减少锁竞争
                        Mat previousFrame = null;
                        bool frameLockTaken = false;
                        try
                        {
                            frameLockTaken = _lockObject.TryEnterWriteLock(5); // 减少帧更新的锁等待时间到5ms
                            if (frameLockTaken)
                            {
                                previousFrame = _currentFrame;
                                _currentFrame = enhancedFrame.Clone();
                            }
                            // 如果无法获取锁，跳过帧更新，避免阻塞
                        }
                        finally
                        {
                            if (frameLockTaken)
                            {
                                _lockObject.ExitWriteLock();
                            }
                        }
                        
                        // 在锁外释放之前的帧，避免在锁内执行耗时操作
                        previousFrame?.Dispose();

                        // 录像处理 - 使用异步方式，避免阻塞主循环
                        if (_isRecording)
                        {
                            var frameToRecord = enhancedFrame.Clone();
                            // 使用Task.Run异步处理录像，完全避免阻塞主循环
                            _ = Task.Run(() =>
                            {
                                try
                                {
                                    bool videoLockTaken = false;
                                    try
                                    {
                                        // 录像使用写锁，但设置较短的超时时间避免阻塞
                                        videoLockTaken = _lockObject.TryEnterWriteLock(30); // 30ms超时，快速失败
                                        if (videoLockTaken && _videoWriter != null && _videoWriter.IsOpened())
                                        {
                                            _videoWriter.Write(frameToRecord);
                                            Interlocked.Increment(ref _recordedFrameCount);
                                        }
                                        else if (!videoLockTaken)
                                        {
                                            // 降低日志级别，避免过多输出
                                            if (_recordedFrameCount % 30 == 0) // 每30帧输出一次
                                            {
                                                LogUtil.Debug("IndustrialCameraManager: 异步录像无法获取锁，跳过当前帧");
                                            }
                                        }
                                    }
                                    finally
                                    {
                                        if (videoLockTaken)
                                        {
                                            _lockObject.ExitWriteLock();
                                        }
                                    }
                                }
                                catch (Exception videoEx)
                                {
                                    LogUtil.Error($"IndustrialCameraManager: 异步录像写入失败 - {videoEx.Message}");
                                }
                                finally
                                {
                                    frameToRecord?.Dispose();
                                }
                            });
                        }

                        // 触发帧捕获事件 - 使用安全的颜色转换
                        var bitmapSource = SafeMatToBitmapSource(enhancedFrame);
                        FrameCaptured?.Invoke(this, new FrameCapturedEventArgs(bitmapSource, enhancedFrame.Clone()));

                        enhancedFrame.Dispose();

                        // 更新帧率
                        UpdateFrameRate();
                    }
                    else
                    {
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

    #endregion
}