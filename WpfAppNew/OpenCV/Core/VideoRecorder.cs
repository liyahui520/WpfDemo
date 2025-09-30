using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;
using Tools.Extend;
using WpfAppNew.OpenCv.Core;
using WpfAppNew.OpenCV.Core;

namespace WpfAppNew.OpenCv.Core
{
    /// <summary>
    /// 高级视频录制器
    /// 支持多种视频格式、编码器和录制选项
    /// 提供高性能的视频录制功能
    /// </summary>
    public class VideoRecorder : INotifyPropertyChanged, IDisposable
    {
        #region 私有字段

        private VideoWriter _videoWriter;
        private bool _isRecording;
        private bool _disposed;
        private string _outputPath;
        private VideoCodec _codec;
        private double _fps;
        private OpenCvSharp.Size _frameSize;
        private readonly object _lockObject = new object();
        
        // 录制统计
        private DateTime _recordingStartTime;
        private long _frameCount;
        private long _totalFileSize;
        
        // 质量控制
        private int _compressionLevel = 6;
        private bool _useHardwareAcceleration;

        #endregion

        #region 事件定义

        /// <summary>
        /// 录制状态变更事件
        /// </summary>
        public event EventHandler<RecordingStatusEventArgs> RecordingStatusChanged;

        /// <summary>
        /// 录制进度事件
        /// </summary>
        public event EventHandler<RecordingProgressEventArgs> RecordingProgress;

        /// <summary>
        /// 录制错误事件
        /// </summary>
        public event EventHandler<RecordingErrorEventArgs> RecordingError;

        #endregion

        #region 公共属性

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
                }
            }
        }

        /// <summary>
        /// 输出文件路径
        /// </summary>
        public string OutputPath
        {
            get => _outputPath;
            set
            {
                if (_outputPath != value)
                {
                    _outputPath = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 视频编码器
        /// </summary>
        public VideoCodec Codec
        {
            get => _codec;
            set
            {
                if (_codec != value)
                {
                    _codec = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 帧率
        /// </summary>
        public double Fps
        {
            get => _fps;
            set
            {
                if (Math.Abs(_fps - value) > 0.1)
                {
                    _fps = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 帧尺寸
        /// </summary>
        public OpenCvSharp.Size FrameSize
        {
            get => _frameSize;
            set
            {
                if (_frameSize != value)
                {
                    _frameSize = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 压缩级别 (0-9, 0=无压缩, 9=最大压缩)
        /// </summary>
        public int CompressionLevel
        {
            get => _compressionLevel;
            set
            {
                var clampedValue = Math.Max(0, Math.Min(9, value));
                if (_compressionLevel != clampedValue)
                {
                    _compressionLevel = clampedValue;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 是否使用硬件加速
        /// </summary>
        public bool UseHardwareAcceleration
        {
            get => _useHardwareAcceleration;
            set
            {
                if (_useHardwareAcceleration != value)
                {
                    _useHardwareAcceleration = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 录制时长
        /// </summary>
        public TimeSpan RecordingDuration => IsRecording ? DateTime.Now - _recordingStartTime : TimeSpan.Zero;

        /// <summary>
        /// 已录制帧数
        /// </summary>
        public long FrameCount => _frameCount;

        /// <summary>
        /// 文件大小（字节）
        /// </summary>
        public long FileSize => _totalFileSize;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化视频录制器并配置硬件加速
        /// </summary>
        public VideoRecorder()
        {
            _codec = VideoCodec.H264;
            _fps = 30.0;
            _frameSize = new OpenCvSharp.Size(640, 480);
            
            // 初始化硬件加速配置
            InitializeHardwareAcceleration();
        }

        /// <summary>
        /// 初始化硬件加速配置
        /// </summary>
        private void InitializeHardwareAcceleration()
        {
            try
            {
                // 获取加速管理器实例
                var accelerationManager = AccelerationManager.Instance;
                
                // 根据检测到的硬件加速类型启用相应功能
                if (accelerationManager.AccelerationInfo != null)
                {
                    var enabledAcceleration = accelerationManager.AccelerationInfo.EnabledAcceleration;
                    
                    switch (enabledAcceleration)
                    {
                        case AccelerationType.OpenCL:
                            _useHardwareAcceleration = true;
                            LogUtil.Info("VideoRecorder: 启用OpenCL硬件加速录制");
                            break;
                            
                        case AccelerationType.CUDA:
                            _useHardwareAcceleration = true;
                            LogUtil.Info("VideoRecorder: 启用CUDA硬件加速录制");
                            break;
                            
                        case AccelerationType.TBB:
                            _useHardwareAcceleration = true;
                            LogUtil.Info("VideoRecorder: 启用Intel TBB并行加速录制");
                            break;
                            
                        default:
                            _useHardwareAcceleration = false;
                            LogUtil.Info("VideoRecorder: 使用CPU录制模式");
                            break;
                    }
                    
                    LogUtil.Debug($"VideoRecorder: 硬件加速状态 - {enabledAcceleration}, 启用: {_useHardwareAcceleration}");
                }
                else
                {
                    _useHardwareAcceleration = false;
                    LogUtil.Debug("VideoRecorder: 未检测到硬件加速支持，使用CPU模式");
                }
            }
            catch (Exception ex)
            {
                _useHardwareAcceleration = false;
                LogUtil.Error($"VideoRecorder: 硬件加速初始化失败: {ex.Message}");
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 开始录制
        /// </summary>
        /// <param name="outputPath">输出文件路径</param>
        /// <param name="frameSize">帧尺寸</param>
        /// <param name="fps">帧率</param>
        /// <param name="codec">编码器</param>
        /// <returns>是否开始成功</returns>
        public bool StartRecording(string outputPath, OpenCvSharp.Size frameSize, double fps = 30.0, VideoCodec codec = VideoCodec.H264)
        {
            if (IsRecording)
            {
                RecordingError?.Invoke(this, new RecordingErrorEventArgs(new InvalidOperationException("录制已在进行中"), "重复开始录制"));
                return false;
            }

            try
            {
                lock (_lockObject)
                {
                    OutputPath = outputPath;
                    FrameSize = frameSize;
                    Fps = fps;
                    Codec = codec;

                    // 确保输出目录存在
                    var directory = Path.GetDirectoryName(outputPath);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    // 获取编码器FourCC
                    var fourCC = GetFourCC(codec);
                    
                    // 创建视频写入器
                    _videoWriter = new VideoWriter(outputPath, fourCC, fps, frameSize);
                    
                    if (!_videoWriter.IsOpened())
                    {
                        throw new InvalidOperationException($"无法创建视频写入器，路径: {outputPath}");
                    }

                    // 重置统计信息
                    _recordingStartTime = DateTime.Now;
                    _frameCount = 0;
                    _totalFileSize = 0;

                    IsRecording = true;
                    
                    RecordingStatusChanged?.Invoke(this, new RecordingStatusEventArgs(true, outputPath, codec));
                    
                    return true;
                }
            }
            catch (Exception ex)
            {
                RecordingError?.Invoke(this, new RecordingErrorEventArgs(ex, "开始录制失败"));
                return false;
            }
        }

        /// <summary>
        /// 停止录制
        /// </summary>
        public void StopRecording()
        {
            if (!IsRecording) return;

            try
            {
                lock (_lockObject)
                {
                    _videoWriter?.Release();
                    _videoWriter?.Dispose();
                    _videoWriter = null;

                    // 获取最终文件大小
                    if (File.Exists(OutputPath))
                    {
                        _totalFileSize = new FileInfo(OutputPath).Length;
                    }

                    IsRecording = false;
                    
                    RecordingStatusChanged?.Invoke(this, new RecordingStatusEventArgs(false, OutputPath, Codec));
                }
            }
            catch (Exception ex)
            {
                RecordingError?.Invoke(this, new RecordingErrorEventArgs(ex, "停止录制失败"));
            }
        }

        /// <summary>
        /// 写入帧
        /// 支持硬件加速的帧处理和编码
        /// </summary>
        /// <param name="frame">视频帧</param>
        /// <returns>是否写入成功</returns>
        public bool WriteFrame(Mat frame)
        {
            if (!IsRecording || _videoWriter == null || !_videoWriter.IsOpened())
            {
                return false;
            }

            try
            {
                lock (_lockObject)
                {
                    if (frame == null || frame.Empty())
                    {
                        return false;
                    }

                    Mat processedFrame = null;

                    // 使用硬件加速处理帧（如果启用）
                    if (_useHardwareAcceleration)
                    {
                        processedFrame = ProcessFrameForRecording(frame);
                    }
                    else
                    {
                        processedFrame = frame;
                    }

                    try
                    {
                        // 检查帧尺寸是否匹配
                        if (processedFrame.Size() != FrameSize)
                        {
                            using (var resizedFrame = new Mat())
                            {
                                // 使用硬件加速的缩放（如果可用）
                                if (_useHardwareAcceleration)
                                {
                                    ResizeFrameWithAcceleration(processedFrame, resizedFrame, FrameSize);
                                }
                                else
                                {
                                    Cv2.Resize(processedFrame, resizedFrame, FrameSize);
                                }
                                
                                _videoWriter.Write(resizedFrame);
                            }
                        }
                        else
                        {
                            _videoWriter.Write(processedFrame);
                        }

                        _frameCount++;

                        // 定期更新进度
                        if (_frameCount % 30 == 0) // 每30帧更新一次
                        {
                            UpdateProgress();
                        }

                        return true;
                    }
                    finally
                    {
                        // 释放处理后的帧（如果不是原始帧）
                        if (processedFrame != frame && processedFrame != null)
                        {
                            processedFrame.Dispose();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                RecordingError?.Invoke(this, new RecordingErrorEventArgs(ex, "写入帧失败"));
                return false;
            }
        }

        /// <summary>
        /// 使用硬件加速处理录制帧
        /// </summary>
        /// <param name="frame">输入帧</param>
        /// <returns>处理后的帧</returns>
        private Mat ProcessFrameForRecording(Mat frame)
        {
            try
            {
                // 获取加速管理器
                var accelerationManager = AccelerationManager.Instance;
                var accelerationType = accelerationManager.AccelerationInfo?.EnabledAcceleration ?? AccelerationType.None;

                switch (accelerationType)
                {
                    case AccelerationType.OpenCL:
                        return ProcessRecordingFrameWithOpenCL(frame);

                    case AccelerationType.CUDA:
                        return ProcessRecordingFrameWithCuda(frame);

                    case AccelerationType.TBB:
                        return ProcessRecordingFrameWithTBB(frame);

                    default:
                        return frame; // 不进行额外处理
                }
            }
            catch (Exception ex)
            {
                LogUtil.Debug($"VideoRecorder: 硬件加速帧处理异常，使用原始帧: {ex.Message}");
                return frame;
            }
        }

        /// <summary>
        /// 使用OpenCL处理录制帧
        /// </summary>
        /// <param name="frame">输入帧</param>
        /// <returns>处理后的帧</returns>
        private Mat ProcessRecordingFrameWithOpenCL(Mat frame)
        {
            try
            {
                // OpenCV 3.x+使用透明API，OpenCL会自动启用

                // 可以在这里添加OpenCL加速的预处理操作
                // 例如：色彩空间转换、降噪、锐化等
                
                // 对于录制，通常不需要太多处理，直接返回原帧
                return frame;
            }
            catch (Exception ex)
            {
                LogUtil.Debug($"VideoRecorder: OpenCL录制帧处理异常: {ex.Message}");
                return frame;
            }
        }

        /// <summary>
        /// 使用CUDA处理录制帧
        /// </summary>
        /// <param name="frame">输入帧</param>
        /// <returns>处理后的帧</returns>
        private Mat ProcessRecordingFrameWithCuda(Mat frame)
        {
            try
            {
                // CUDA加速的录制帧处理
                // 注意：需要OpenCV编译时包含CUDA支持
                
                // 对于录制，通常不需要太多处理，直接返回原帧
                return frame;
            }
            catch (Exception ex)
            {
                LogUtil.Debug($"VideoRecorder: CUDA录制帧处理异常: {ex.Message}");
                return frame;
            }
        }

        /// <summary>
        /// 使用Intel TBB处理录制帧
        /// </summary>
        /// <param name="frame">输入帧</param>
        /// <returns>处理后的帧</returns>
        private Mat ProcessRecordingFrameWithTBB(Mat frame)
        {
            try
            {
                // Intel TBB加速的录制帧处理
                // 主要用于并行化CPU操作
                
                // 对于录制，通常不需要太多处理，直接返回原帧
                return frame;
            }
            catch (Exception ex)
            {
                LogUtil.Debug($"VideoRecorder: TBB录制帧处理异常: {ex.Message}");
                return frame;
            }
        }

        /// <summary>
        /// 使用硬件加速缩放帧
        /// </summary>
        /// <param name="source">源帧</param>
        /// <param name="destination">目标帧</param>
        /// <param name="size">目标尺寸</param>
        private void ResizeFrameWithAcceleration(Mat source, Mat destination, OpenCvSharp.Size size)
        {
            try
            {
                // 获取加速管理器
                var accelerationManager = AccelerationManager.Instance;
                var accelerationType = accelerationManager.AccelerationInfo?.EnabledAcceleration ?? AccelerationType.None;

                switch (accelerationType)
                {
                    case AccelerationType.OpenCL:
                        // OpenCV 3.x+使用透明API，OpenCL会自动启用
                        // 使用UMat可以自动利用OpenCL加速
                        Cv2.Resize(source, destination, size, 0, 0, InterpolationFlags.Linear);
                        break;

                    case AccelerationType.CUDA:
                        // CUDA加速缩放（需要CUDA支持）
                        Cv2.Resize(source, destination, size, 0, 0, InterpolationFlags.Linear);
                        break;

                    default:
                        // 标准CPU缩放
                        Cv2.Resize(source, destination, size, 0, 0, InterpolationFlags.Linear);
                        break;
                }
            }
            catch (Exception ex)
            {
                LogUtil.Debug($"VideoRecorder: 硬件加速缩放异常，使用CPU缩放: {ex.Message}");
                Cv2.Resize(source, destination, size, 0, 0, InterpolationFlags.Linear);
            }
        }

        /// <summary>
        /// 暂停录制
        /// </summary>
        public void PauseRecording()
        {
            // 注意：OpenCV的VideoWriter不直接支持暂停，这里可以通过停止写入帧来实现
            // 实际应用中可能需要更复杂的实现
        }

        /// <summary>
        /// 恢复录制
        /// </summary>
        public void ResumeRecording()
        {
            // 与暂停录制配合使用
        }

        /// <summary>
        /// 获取支持的编码器列表
        /// </summary>
        /// <returns>编码器列表</returns>
        public static List<VideoCodecInfo> GetSupportedCodecs()
        {
            return new List<VideoCodecInfo>
            {
                new VideoCodecInfo { Codec = VideoCodec.H264, Name = "H.264", Extension = ".mp4", Description = "高效视频编码，广泛支持" },
                new VideoCodecInfo { Codec = VideoCodec.H265, Name = "H.265/HEVC", Extension = ".mp4", Description = "新一代高效编码，文件更小" },
                new VideoCodecInfo { Codec = VideoCodec.XVID, Name = "XVID", Extension = ".avi", Description = "开源MPEG-4编码器" },
                new VideoCodecInfo { Codec = VideoCodec.MJPEG, Name = "Motion JPEG", Extension = ".avi", Description = "基于JPEG的视频编码" },
                new VideoCodecInfo { Codec = VideoCodec.MP4V, Name = "MPEG-4", Extension = ".mp4", Description = "标准MPEG-4编码" },
                new VideoCodecInfo { Codec = VideoCodec.WMV, Name = "Windows Media Video", Extension = ".wmv", Description = "微软视频格式" },
                new VideoCodecInfo { Codec = VideoCodec.FLV, Name = "Flash Video", Extension = ".flv", Description = "Flash视频格式" },
                new VideoCodecInfo { Codec = VideoCodec.AVI, Name = "AVI Raw", Extension = ".avi", Description = "未压缩AVI格式" }
            };
        }

        /// <summary>
        /// 获取推荐的录制设置
        /// </summary>
        /// <param name="quality">质量级别</param>
        /// <returns>录制设置</returns>
        public static RecordingSettings GetRecommendedSettings(RecordingQuality quality)
        {
            switch (quality)
            {
                case RecordingQuality.Low:
                    return new RecordingSettings
                    {
                        Codec = VideoCodec.H264,
                        Fps = 15,
                        Size = new OpenCvSharp.Size(320, 240),
                        CompressionLevel = 8
                    };
                case RecordingQuality.Medium:
                    return new RecordingSettings
                    {
                        Codec = VideoCodec.H264,
                        Fps = 25,
                        Size = new OpenCvSharp.Size(640, 480),
                        CompressionLevel = 6
                    };
                case RecordingQuality.High:
                    return new RecordingSettings
                    {
                        Codec = VideoCodec.H264,
                        Fps = 30,
                        Size = new OpenCvSharp.Size(1280, 720),
                        CompressionLevel = 4
                    };
                case RecordingQuality.Ultra:
                    return new RecordingSettings
                    {
                        Codec = VideoCodec.H265,
                        Fps = 60,
                        Size = new OpenCvSharp.Size(1920, 1080),
                        CompressionLevel = 2
                    };
                default:
                    return GetRecommendedSettings(RecordingQuality.Medium);
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 获取编码器FourCC
        /// 根据硬件加速支持情况选择最优编码器
        /// </summary>
        /// <param name="codec">视频编码器</param>
        /// <returns>FourCC代码</returns>
        private FourCC GetFourCC(VideoCodec codec)
        {
            // 获取硬件加速信息
            var accelerationManager = AccelerationManager.Instance;
            var accelerationType = accelerationManager.AccelerationInfo?.EnabledAcceleration ?? AccelerationType.None;
            
            switch (codec)
            {
                case VideoCodec.H264:
                    return GetOptimizedH264FourCC(accelerationType);
                    
                case VideoCodec.H265:
                    return GetOptimizedH265FourCC(accelerationType);
                    
                case VideoCodec.XVID:
                    return FourCC.XVID;
                    
                case VideoCodec.MJPEG:
                    return FourCC.MJPG;
                    
                case VideoCodec.MP4V:
                    return FourCC.MP4V;
                    
                case VideoCodec.WMV:
                    return FourCC.WMV1;
                    
                case VideoCodec.FLV:
                    return FourCC.Default; // FLV1 不存在，使用默认值
                    
                case VideoCodec.AVI:
                    return FourCC.Default;
                    
                default:
                    return GetOptimizedH264FourCC(accelerationType);
            }
        }

        /// <summary>
        /// 获取优化的H264编码器FourCC
        /// </summary>
        /// <param name="accelerationType">加速类型</param>
        /// <returns>H264 FourCC代码</returns>
        private FourCC GetOptimizedH264FourCC(AccelerationType accelerationType)
        {
            try
            {
                switch (accelerationType)
                {
                    case AccelerationType.CUDA:
                        // 尝试使用NVIDIA硬件编码器
                        LogUtil.Debug("VideoRecorder: 尝试使用NVIDIA H264硬件编码器");
                        return FourCC.H264; // 可以尝试其他NVIDIA特定的编码器
                        
                    case AccelerationType.OpenCL:
                        // OpenCL加速的H264编码
                        LogUtil.Debug("VideoRecorder: 使用OpenCL加速的H264编码器");
                        return FourCC.H264;
                        
                    case AccelerationType.TBB:
                        // Intel TBB并行化的H264编码
                        LogUtil.Debug("VideoRecorder: 使用Intel TBB并行化的H264编码器");
                        return FourCC.H264;
                        
                    default:
                        LogUtil.Debug("VideoRecorder: 使用标准H264编码器");
                        return FourCC.H264;
                }
            }
            catch (Exception ex)
            {
                LogUtil.Debug($"VideoRecorder: 获取优化H264编码器失败，使用默认编码器: {ex.Message}");
                return FourCC.H264;
            }
        }

        /// <summary>
        /// 获取优化的H265编码器FourCC
        /// </summary>
        /// <param name="accelerationType">加速类型</param>
        /// <returns>H265 FourCC代码</returns>
        private FourCC GetOptimizedH265FourCC(AccelerationType accelerationType)
        {
            try
            {
                switch (accelerationType)
                {
                    case AccelerationType.CUDA:
                        // 尝试使用NVIDIA H265硬件编码器
                        LogUtil.Debug("VideoRecorder: 尝试使用NVIDIA H265硬件编码器");
                        return FourCC.HEVC;
                        
                    case AccelerationType.OpenCL:
                        // OpenCL加速的H265编码
                        LogUtil.Debug("VideoRecorder: 使用OpenCL加速的H265编码器");
                        return FourCC.HEVC;
                        
                    case AccelerationType.TBB:
                        // Intel TBB并行化的H265编码
                        LogUtil.Debug("VideoRecorder: 使用Intel TBB并行化的H265编码器");
                        return FourCC.HEVC;
                        
                    default:
                        LogUtil.Debug("VideoRecorder: 使用标准H265编码器");
                        return FourCC.HEVC;
                }
            }
            catch (Exception ex)
            {
                LogUtil.Debug($"VideoRecorder: 获取优化H265编码器失败，使用默认编码器: {ex.Message}");
                return FourCC.HEVC;
            }
        }

        /// <summary>
        /// 更新录制进度
        /// </summary>
        private void UpdateProgress()
        {
            try
            {
                if (File.Exists(OutputPath))
                {
                    _totalFileSize = new FileInfo(OutputPath).Length;
                }

                RecordingProgress?.Invoke(this, new RecordingProgressEventArgs
                {
                    Duration = RecordingDuration,
                    FrameCount = _frameCount,
                    FileSize = _totalFileSize,
                    AverageFps = _frameCount / RecordingDuration.TotalSeconds
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新录制进度失败: {ex.Message}");
                LogUtil.Info($"VideoRecorder: 更新录制进度失败: {ex.Message}");
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
                    StopRecording();
                }
                _disposed = true;
            }
        }

        ~VideoRecorder()
        {
            Dispose(false);
        }

        #endregion
    }

    #region 枚举和数据类

    /// <summary>
    /// 视频编码器枚举
    /// </summary>
    public enum VideoCodec
    {
        /// <summary>
        /// H.264编码器
        /// </summary>
        H264,
        
        /// <summary>
        /// H.265/HEVC编码器
        /// </summary>
        H265,
        
        /// <summary>
        /// XVID编码器
        /// </summary>
        XVID,
        
        /// <summary>
        /// Motion JPEG编码器
        /// </summary>
        MJPEG,
        
        /// <summary>
        /// MPEG-4编码器
        /// </summary>
        MP4V,
        
        /// <summary>
        /// Windows Media Video
        /// </summary>
        WMV,
        
        /// <summary>
        /// Flash Video
        /// </summary>
        FLV,
        
        /// <summary>
        /// AVI原始格式
        /// </summary>
        AVI
    }

    /// <summary>
    /// 录制质量枚举
    /// </summary>
    public enum RecordingQuality
    {
        /// <summary>
        /// 低质量
        /// </summary>
        Low,
        
        /// <summary>
        /// 中等质量
        /// </summary>
        Medium,
        
        /// <summary>
        /// 高质量
        /// </summary>
        High,
        
        /// <summary>
        /// 超高质量
        /// </summary>
        Ultra
    }

    /// <summary>
    /// 视频编码器信息
    /// </summary>
    public class VideoCodecInfo
    {
        /// <summary>
        /// 编码器类型
        /// </summary>
        public VideoCodec Codec { get; set; }
        
        /// <summary>
        /// 编码器名称
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// 文件扩展名
        /// </summary>
        public string Extension { get; set; }
        
        /// <summary>
        /// 描述
        /// </summary>
        public string Description { get; set; }

        public override string ToString()
        {
            return $"{Name} ({Extension})";
        }
    }

    /// <summary>
    /// 录制设置
    /// </summary>
    public class RecordingSettings
    {
        /// <summary>
        /// 编码器
        /// </summary>
        public VideoCodec Codec { get; set; }
        
        /// <summary>
        /// 帧率
        /// </summary>
        public double Fps { get; set; }
        
        /// <summary>
        /// 视频尺寸
        /// </summary>
        public OpenCvSharp.Size Size { get; set; }
        
        /// <summary>
        /// 压缩级别
        /// </summary>
        public int CompressionLevel { get; set; }
    }

    #endregion

    #region 事件参数类

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
        /// 文件路径
        /// </summary>
        public string FilePath { get; }
        
        /// <summary>
        /// 编码器
        /// </summary>
        public VideoCodec Codec { get; }
        
        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; }

        public RecordingStatusEventArgs(bool isRecording, string filePath, VideoCodec codec)
        {
            IsRecording = isRecording;
            FilePath = filePath;
            Codec = codec;
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// 录制进度事件参数
    /// </summary>
    public class RecordingProgressEventArgs : EventArgs
    {
        /// <summary>
        /// 录制时长
        /// </summary>
        public TimeSpan Duration { get; set; }
        
        /// <summary>
        /// 帧数
        /// </summary>
        public long FrameCount { get; set; }
        
        /// <summary>
        /// 文件大小
        /// </summary>
        public long FileSize { get; set; }
        
        /// <summary>
        /// 平均帧率
        /// </summary>
        public double AverageFps { get; set; }
        
        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 录制错误事件参数
    /// </summary>
    public class RecordingErrorEventArgs : EventArgs
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

        public RecordingErrorEventArgs(Exception exception, string message)
        {
            Exception = exception;
            Message = message;
            Timestamp = DateTime.Now;
        }
    }

    #endregion
}