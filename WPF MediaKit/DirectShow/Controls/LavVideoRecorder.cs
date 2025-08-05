using DirectShowLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows;
using WPFMediaKit.DirectShow.MediaPlayers;

namespace WPFMediaKit.DirectShow.Controls
{
    /// <summary>
    /// 使用LAV Splitter实现视频流分流（预览+录制）的录制器
    /// </summary>
    /// <summary>
    /// 基于LAV Splitter的视频录制器，实现预览和录制同时进行
    /// </summary>
    public class LavVideoRecorder : VideoCapturePlayer
    {
        #region 常量定义（GUID和CLSID）
        // LAV Splitter的CLSID（用于分流视频流）
        private static readonly Guid CLSID_LAV_Splitter = new Guid("171252A0-8820-4AFE-9DF8-5C92B2D66B04");

        // LAV MP4复用器的CLSID（用于生成MP4文件）
        private static readonly Guid CLSID_LAV_MP4_Mux = new Guid("0F6417D6-76AE-4322-8230-58D47E0E5F12");

        // H.264视频编码格式
        private static readonly Guid MEDIASUBTYPE_H264 = new Guid("34363248-0000-0010-8000-00AA00389B71");

        // 系统视频转换滤镜（用于格式兼容）
        private static readonly Guid CLSID_VideoConverter = new Guid("04FE9017-F873-11d0-A18C-00A0C9118956");
         
        #endregion

        #region 成员变量
        // 捕获图形构建器（用于构建录制管线）
        private ICaptureGraphBuilder2 _captureGraph;

        // LAV Splitter实例（核心分流组件）
        private IBaseFilter _lavSplitter;

        // MP4复用器
        private IBaseFilter _muxFilter;

        // 文件输出过滤器
        private IFileSinkFilter _fileSink;

        // 录制状态
        private bool _isRecording;

        // 初始化完成标志
        private bool _isInitialized;

        // 视频压缩参数
        public int VideoBitRate { get; set; } = 2500000; // 2.5Mbps
        public int FrameRate { get; set; } = 30;
        #endregion

        #region 构造函数与初始化
        public LavVideoRecorder()
        {
            // 使用定时器检查基类初始化状态（替代OnMediaPlayerInitialized）
            var initTimer = new DispatcherTimer(DispatcherPriority.Normal)
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };

            initTimer.Tick += (s, e) =>
            {
                if (IsBaseInitialized())
                {
                    initTimer.Stop();
                    Dispatcher.BeginInvoke(() =>
                    {
                        CompleteInitialization();
                    });
                }
            };

            initTimer.Start();
        }

        /// <summary>
        /// 检查基类是否初始化完成
        /// </summary>
        private bool IsBaseInitialized()
        {
            // 检查核心组件是否就绪
            return GetGraph() != null &&
                   GetCaptureDevice() != null &&
                   GetMediaControl() != null;
        }

        /// <summary>
        /// 完成自定义初始化
        /// </summary>
        private void CompleteInitialization()
        {
            try
            {
                // 初始化捕获图形构建器
                _captureGraph = (ICaptureGraphBuilder2)new CaptureGraphBuilder2();
                int hr = _captureGraph.SetFiltergraph(GetGraph());
                DsError.ThrowExceptionForHR(hr);

                // 配置捕获设备输出格式
                ConfigureCaptureFormat();

                // 初始化LAV Splitter并连接
                InitializeLavSplitter();

                _isInitialized = true;
                InvokeMediaOpened(); // 通知媒体已打开
            }
            catch (Exception ex)
            {
                InvokeMediaFailed(new MediaFailedEventArgs("初始化失败", ex));
            }
        }
        #endregion

        #region LAV Splitter初始化与连接
        /// <summary>
        /// 初始化LAV Splitter并连接到捕获设备
        /// </summary>
        private void InitializeLavSplitter()
        {
            var graph = GetGraph();
            var captureDevice = GetCaptureDevice();

            if (graph == null || captureDevice == null)
                throw new InvalidOperationException("无法获取图形构建器或捕获设备");

            // 创建LAV Splitter实例
            _lavSplitter = (IBaseFilter)Activator.CreateInstance(
                Type.GetTypeFromCLSID(CLSID_LAV_Splitter));

            // 添加到滤镜图
            int hr = graph.AddFilter(_lavSplitter, "LAV Splitter");
            DsError.ThrowExceptionForHR(hr);

            // 连接捕获设备到LAV Splitter（带格式转换）
            if (!ConnectCaptureToSplitter(captureDevice, _lavSplitter))
            {
                throw new Exception("无法连接捕获设备到LAV Splitter，请检查LAV Filters是否正确安装");
            }

            // 连接预览流（LAV Splitter输出到渲染器）
            ConnectPreviewStream();
        }

        /// <summary>
        /// 连接捕获设备到LAV Splitter（处理格式兼容问题）
        /// </summary>
        private bool ConnectCaptureToSplitter(IBaseFilter captureDevice, IBaseFilter splitter)
        {
            // 获取捕获设备的输出引脚（视频）
            IPin captureOutPin = GetVideoOutputPin(captureDevice);
            if (captureOutPin == null)
                return false;

            // 获取LAV Splitter的输入引脚
            IPin splitterInPin = DsFindPin.ByDirection(splitter, PinDirection.Input, 0);
            if (splitterInPin == null)
            {
                Marshal.ReleaseComObject(captureOutPin);
                return false;
            }

            try
            {
                // 策略1：尝试直接连接
                int hr = GetGraph().Connect(captureOutPin, splitterInPin);
                if (hr == 0)
                    return true;

                // 策略2：添加格式转换滤镜后连接
                if (TryConnectWithConverter(captureOutPin, splitterInPin))
                    return true;

                // 策略3：强制指定支持的媒体类型
                return TryConnectWithSpecificMediaType(captureOutPin, splitterInPin);
            }
            finally
            {
                Marshal.ReleaseComObject(captureOutPin);
                Marshal.ReleaseComObject(splitterInPin);
            }
        }

        /// <summary>
        /// 正确获取滤镜的所有输出引脚
        /// </summary>
        private IPin[] GetAllOutputPins(IBaseFilter filter)
        {
            if (filter == null)
                throw new ArgumentNullException(nameof(filter));

            var pins = new List<IPin>();
            int index = 0;

            // 循环获取所有输出引脚（直到获取失败）
            while (true)
            {
                IPin pin = DsFindPin.ByDirection(filter, PinDirection.Output, index);
                if (pin == null)
                    break; // 没有更多引脚时退出循环

                pins.Add(pin);
                index++;
            }

            return pins.ToArray();
        }

        /// <summary>
        /// 获取视频输出引脚（调用修复后的方法）
        /// </summary>
        private IPin GetVideoOutputPin(IBaseFilter filter)
        {
            IPin[] pins = GetAllOutputPins(filter); // 使用修复后的方法
            foreach (var pin in pins)
            {
                if (IsVideoPin(pin))
                {
                    // 释放其他引脚
                    foreach (var p in pins)
                    {
                        if (!p.Equals(pin))
                            Marshal.ReleaseComObject(p);
                    }
                    return pin;
                }
                Marshal.ReleaseComObject(pin);
            }
            return null;
        }

        /// <summary>
        /// 判断引脚是否为视频引脚
        /// </summary>
        private bool IsVideoPin(IPin pin)
        {
            var mediaType = new AMMediaType();
            try
            {
                pin.ConnectionMediaType(mediaType);
                return mediaType.majorType == MediaType.Video;
            }
            catch
            {
                return false;
            }
            finally
            {
                DsUtils.FreeAMMediaType(mediaType);
            }
        }

        /// <summary>
        /// 使用格式转换滤镜连接
        /// </summary>
        private bool TryConnectWithConverter(IPin capturePin, IPin splitterPin)
        {
            try
            {
                // 创建系统视频转换滤镜
                var converter = (IBaseFilter)Activator.CreateInstance(
                    Type.GetTypeFromCLSID(CLSID_VideoConverter));

                GetGraph().AddFilter(converter, "Video Converter");

                // 连接捕获设备到转换滤镜
                int hr = GetGraph().Connect(capturePin,
                    DsFindPin.ByDirection(converter, PinDirection.Input, 0));
                if (hr != 0)
                {
                    CleanupFilter(converter);
                    return false;
                }

                // 连接转换滤镜到LAV Splitter
                hr = GetGraph().Connect(
                    DsFindPin.ByDirection(converter, PinDirection.Output, 0),
                    splitterPin);

                if (hr != 0)
                {
                    CleanupFilter(converter);
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 尝试使用指定的媒体类型连接
        /// </summary>
        private bool TryConnectWithSpecificMediaType(IPin capturePin, IPin splitterPin)
        {
            // LAV Splitter支持的常见视频格式
            var supportedSubTypes = new[] {
                MEDIASUBTYPE_H264,
                new Guid("32595559-0000-0010-8000-00AA00389B71"), // YUY2
                new Guid("E436EB7E-524F-11CE-9F53-0020AF0BA770")  // RGB24
            };

            foreach (var subType in supportedSubTypes)
            {
                var mediaType = new AMMediaType
                {
                    majorType = MediaType.Video,
                    subType = subType,
                    formatType = FormatType.VideoInfo,
                    fixedSizeSamples = true,
                    sampleSize = 1
                };

                try
                {
                    int hr = GetGraph().ConnectDirect(capturePin, splitterPin, mediaType);
                    if (hr == 0) return true;
                }
                finally
                {
                    DsUtils.FreeAMMediaType(mediaType);
                }
            }

            return false;
        }

        /// <summary>
        /// 连接预览流（LAV Splitter到渲染器）
        /// </summary>
        private void ConnectPreviewStream()
        {
            // 获取LAV Splitter的第一个输出引脚作为预览流
            IPin previewPin = GetSplitterOutputPin(0);
            if (previewPin == null)
                throw new Exception("无法获取LAV Splitter的预览输出引脚");

            try
            {
                // 渲染预览流到默认渲染器
                int hr = _captureGraph.RenderStream(
                    PinCategory.Preview,
                    MediaType.Video,
                    previewPin,
                    null,
                    GetPreviewRenderer()); // 基类的预览渲染器

                DsError.ThrowExceptionForHR(hr);
            }
            finally
            {
                Marshal.ReleaseComObject(previewPin);
            }
        }

        /// <summary>
        /// 获取LAV Splitter的输出引脚（按索引）
        /// </summary>
        private IPin GetSplitterOutputPin(int index)
        {
            try
            {
                // 先尝试按名称获取（LAV Splitter通常命名为"Output 0", "Output 1"）
                var pin = DsFindPin.ByName(_lavSplitter, $"Output {index}");
                if (pin != null)
                    return pin;

                // 按索引获取输出引脚
                return DsFindPin.ByDirection(_lavSplitter, PinDirection.Output, index);
            }
            catch
            {
                return null;
            }
        }
        #endregion

        #region 录制控制
        /// <summary>
        /// 开始录制
        /// </summary>
        public void StartRecording(string outputPath)
        {
            if (!_isInitialized || _isRecording || string.IsNullOrEmpty(outputPath))
                throw new InvalidOperationException("无法开始录制：组件未初始化或已在录制中");

            try
            {
                var graph = GetGraph();
                if (graph == null)
                    throw new Exception("图形构建器未初始化");

                // 创建MP4复用器
                _muxFilter = (IBaseFilter)Activator.CreateInstance(
                    Type.GetTypeFromCLSID(CLSID_LAV_MP4_Mux));
                int hr = graph.AddFilter(_muxFilter, "LAV MP4 Muxer");
                DsError.ThrowExceptionForHR(hr);

                // 设置输出文件
                hr = _captureGraph.SetOutputFileName(
                    MEDIASUBTYPE_H264,
                    outputPath,
                    out _,
                    out _fileSink);
                DsError.ThrowExceptionForHR(hr);

                // 获取LAV Splitter的第二个输出引脚作为录制流
                IPin recordPin = GetSplitterOutputPin(1);
                if (recordPin == null)
                    throw new Exception("无法获取LAV Splitter的录制输出引脚");

                try
                {
                    // 连接录制流到复用器（带压缩）
                    hr = _captureGraph.RenderStream(
                        PinCategory.Capture,
                        MediaType.Video,
                        recordPin,
                        GetVideoCompressor(), // 压缩滤镜
                        _muxFilter);
                    DsError.ThrowExceptionForHR(hr);
                }
                finally
                {
                    Marshal.ReleaseComObject(recordPin);
                }

                // 启动媒体控制
                GetMediaControl()?.Run();
                _isRecording = true;
            }
            catch (Exception ex)
            {
                StopRecording();
                throw new Exception($"录制启动失败：{ex.Message}", ex);
            }
        }

        /// <summary>
        /// 停止录制
        /// </summary>
        public void StopRecording()
        {
            if (!_isRecording) return;

            try
            {
                // 停止媒体控制
                GetMediaControl()?.Stop();

                // 释放文件输出
                if (_fileSink != null)
                {
                    _fileSink.SetFileName(null, null);
                    Marshal.ReleaseComObject(_fileSink);
                    _fileSink = null;
                }

                // 移除并释放复用器
                if (_muxFilter != null && GetGraph() != null)
                {
                    GetGraph().RemoveFilter(_muxFilter);
                    Marshal.ReleaseComObject(_muxFilter);
                    _muxFilter = null;
                }
            }
            finally
            {
                _isRecording = false;
                // 重启预览
                GetMediaControl()?.Run();
            }
        }
        #endregion

        #region 格式配置与压缩
        /// <summary>
        /// 配置捕获设备的输出格式
        /// </summary>
        private void ConfigureCaptureFormat()
        {
            var captureDevice = GetCaptureDevice();
            if (captureDevice == null) return;

            // 获取视频流配置接口
            var videoPin = DsFindPin.ByCategory(captureDevice, PinCategory.Capture, 0);
            var streamConfig = videoPin as IAMStreamConfig;
            if (streamConfig == null)
            {
                Marshal.ReleaseComObject(videoPin);
                throw new Exception("无法获取视频流配置接口");
            }

            try
            {
                // 获取当前格式
                AMMediaType mediaType;
                streamConfig.GetFormat(out mediaType);

                try
                {
                    // 转换为视频信息格式
                    var videoInfo = (VideoInfoHeader)Marshal.PtrToStructure(
                        mediaType.formatPtr, typeof(VideoInfoHeader));

                    // 设置分辨率
                    videoInfo.BmiHeader.Width = 1920;
                    videoInfo.BmiHeader.Height = 1080;

                    // 设置帧率
                    videoInfo.AvgTimePerFrame = DSHOW_ONE_SECOND_UNIT / FrameRate;
                   var m_sampleGrabber = (ISampleGrabber)new SampleGrabber();
                    // 应用设置
                    Marshal.StructureToPtr(videoInfo, mediaType.formatPtr, false);
                    int hr = streamConfig.SetFormat(mediaType);
                    DsError.ThrowExceptionForHR(hr);
                }
                finally
                {
                    DsUtils.FreeAMMediaType(mediaType);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(videoPin);
            }
        }

        /// <summary>
        /// 获取视频压缩滤镜（LAV Video Encoder）
        /// </summary>
        private IBaseFilter GetVideoCompressor()
        {
            // LAV Video Encoder的CLSID
            var encoderClsid = new Guid("6B652FFF-11FE-4FCE-92AD-0266B5D7C78F");

            // 创建编码器实例
            var encoder = (IBaseFilter)Activator.CreateInstance(
                Type.GetTypeFromCLSID(encoderClsid));

            // 添加到滤镜图
            int hr = GetGraph().AddFilter(encoder, "LAV Video Encoder");
            DsError.ThrowExceptionForHR(hr);

            // 配置压缩参数（比特率等）
            ConfigureEncoderSettings(encoder);

            return encoder;
        }

        /// <summary>
        /// 配置编码器参数（H.264压缩设置）
        /// </summary>
        private void ConfigureEncoderSettings(IBaseFilter encoder)
        {
            // 获取编码器的配置接口（LAV特定接口）
            var encoderConfig = encoder as ILAVVideoEncoder;
            if (encoderConfig != null)
            {
                // 设置编码格式为H.264
                encoderConfig.SetCodec(MEDIASUBTYPE_H264);

                // 设置比特率（单位：kbps）
                encoderConfig.SetBitrate(VideoBitRate / 1000);

                // 设置关键帧间隔
                encoderConfig.SetKeyframeInterval(FrameRate * 2); // 2秒一个关键帧
            }
        }
        #endregion

        #region 辅助方法
        /// <summary>
        /// 清理滤镜资源
        /// </summary>
        private void CleanupFilter(IBaseFilter filter)
        {
            if (filter == null) return;

            try
            {
                GetGraph()?.RemoveFilter(filter);
            }
            finally
            {
                Marshal.ReleaseComObject(filter);
            }
        }

        /// <summary>
        /// 获取基类的IGraphBuilder（通过反射）
        /// </summary>
        private IGraphBuilder GetGraph()
        {
            return m_graph;
        }

        /// <summary>
        /// 获取基类的捕获设备
        /// </summary>
        private IBaseFilter GetCaptureDevice()
        {
            return m_captureDevice;
        }

        /// <summary>
        /// 获取基类的媒体控制接口
        /// </summary>
        private IMediaControl GetMediaControl()
        {
            return m_mediaControl;
        }

        /// <summary>
        /// 获取基类的预览渲染器
        /// </summary>
        private IBaseFilter GetPreviewRenderer()
        {
            return m_renderer;
        }

        /// <summary>
        /// 反射获取基类的字段值
        /// </summary>
        private T GetBaseField<T>(string fieldName)
        {
            var field = GetType().BaseType.GetField(fieldName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);

            return field != null ? (T)field.GetValue(this) : default;
        }
        #endregion

        #region 资源释放
        /// <summary>
        /// 释放资源
        /// </summary>
        protected override void FreeResources()
        {
            StopRecording();

            if (_lavSplitter != null)
            {
                GetGraph()?.RemoveFilter(_lavSplitter);
                Marshal.ReleaseComObject(_lavSplitter);
                _lavSplitter = null;
            }

            if (_captureGraph != null)
            {
                Marshal.ReleaseComObject(_captureGraph);
                _captureGraph = null;
            }

            base.FreeResources();
        }
        #endregion
    }

    #region LAV编码器配置接口（ILAVVideoEncoder）
    /// <summary>
    /// LAV Video Encoder的配置接口（简化版）
    /// </summary>
    [ComImport, Guid("6B652FFF-11FE-4FCE-92AD-0266B5D7C78F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ILAVVideoEncoder
    {
        /// <summary>
        /// 设置编码格式
        /// </summary>
        [PreserveSig]
        int SetCodec(Guid codec);

        /// <summary>
        /// 设置比特率（kbps）
        /// </summary>
        [PreserveSig]
        int SetBitrate(int bitrateKbps);

        /// <summary>
        /// 设置关键帧间隔（帧数）
        /// </summary>
        [PreserveSig]
        int SetKeyframeInterval(int interval);
    }
    #endregion

    /// <summary>
    /// WPF控件封装
    /// </summary>
    public class LavRecorderElement : VideoCaptureElement
    {
        private LavVideoRecorder _recorder;

        // 依赖属性：视频比特率（压缩质量控制）
        public static readonly DependencyProperty VideoBitRateProperty =
            DependencyProperty.Register("VideoBitRate", typeof(int), typeof(LavRecorderElement),
                new FrameworkPropertyMetadata(25000, // 默认2.5Mbps2500000
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        // 视频比特率（越高质量越好，文件越大）
        public int VideoBitRate
        {
            get => (int)GetValue(VideoBitRateProperty);
            set => SetValue(VideoBitRateProperty, value);
        }

        // 分辨率宽度
        public int VideoWidth { get; set; } = 1280;

        // 分辨率高度
        public int VideoHeight { get; set; } = 720;

        // 帧率
        public int FrameRate { get; set; } = 30;

        /// <summary>
        /// 请求创建媒体播放器
        /// </summary>
        protected override MediaPlayerBase OnRequestMediaPlayer()
        { 
                _recorder = new LavVideoRecorder
                {
                    VideoBitRate = VideoBitRate, 
                    FrameRate = FrameRate
                }; 
            
            return _recorder;
        } 
    }
}
