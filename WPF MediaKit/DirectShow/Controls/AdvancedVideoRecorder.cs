using DirectShowLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows;
using WPFMediaKit.DirectShow.MediaPlayers;
using Record.Interfaces;

namespace WPFMediaKit.DirectShow.Controls
{
    // 主录制播放器类
    public class AdvancedVideoRecorder : VideoCapturePlayer
    {
        #region 媒体类型和滤镜GUID定义
        // 标准H.264视频编码
        private static readonly Guid MEDIASUBTYPE_H264 = new Guid("34363248-0000-0010-8000-00AA00389B71");
        // LAV MP4复用器
        private static readonly Guid CLSID_LAV_MP4_Mux = new Guid("171252A0-8820-4AFE-9DF8-5C92B2D66B04");
        // 智能分流滤镜（用于同时预览和录制）
        //private static readonly Guid CLSID_SmartTee = new Guid("6B652FFF-11FE-4FCE-92AD-0266B5D7C78F");
        // 标准 Smart Tee 滤镜的官方 CLSID（系统自带，无需额外安装）
        private static readonly Guid CLSID_SmartTee = new Guid("36B73880-C2C8-11CF-8B46-00805F6CEF60");
        #endregion

        #region 录制和压缩参数
        // 压缩参数（可通过控件属性设置）
        public int VideoBitRate { get; set; } = 2500000; // 2.5Mbps
        public int FrameRate { get; set; } = 30;
        public int KeyFrameInterval { get; set; } = 60; // 每60帧一个关键帧
        public int VideoWidth { get; set; } = 1280;
        public int VideoHeight { get; set; } = 720;
        #endregion

        #region 内部成员
        private ICaptureGraphBuilder2 _captureGraph;
        private IBaseFilter _smartTeeFilter;
        private IBaseFilter _muxFilter;
        private IFileSinkFilter _fileSink;
        private bool _isRecording;
        private bool _isInitialized;
        private readonly DispatcherTimer _initChecker;
        #endregion

        public AdvancedVideoRecorder()
        {
            // 初始化检查定时器
            //_initChecker = new DispatcherTimer
            //{
            //    Interval = TimeSpan.FromMilliseconds(150)
            //};
            //_initChecker.Tick += InitChecker_Tick;
            //_initChecker.Start();

            // 使用Dispatcher定时器检查初始化状态
            var initTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            initTimer.Tick += (s, e) =>
            {
                // 检查基类是否已完成初始化（通过判断核心对象是否存在）
                if (IsBaseInitialized())
                {
                    initTimer.Stop();
                    Dispatcher.BeginInvoke(() =>
                    {
                        InitChecker_Tick(null,null);
                    });

                }
            };
            initTimer.Start();
        }

        #region 初始化逻辑
        private void InitChecker_Tick(object sender, EventArgs e)
        {
            // 检查基类是否已完成初始化
            if (IsBaseInitialized())
            { 
                Dispatcher.BeginInvoke(() =>
                {
                    InitializeRecordingComponents();
                });
            }
        }

        private bool IsBaseInitialized()
        {
            // 检查核心组件是否就绪
            return GetGraph() != null &&
                   GetCaptureDevice() != null &&
                   GetMediaControl() != null;
        }

        private void InitializeRecordingComponents()
        {
            try
            {
                // 创建捕获图形构建器
                _captureGraph = (ICaptureGraphBuilder2)new CaptureGraphBuilder2();
                var hr = _captureGraph.SetFiltergraph(GetGraph());
                DsError.ThrowExceptionForHR(hr);

                // 添加智能分流滤镜（用于同时预览和录制）
                SetupSmartTee();

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                InvokeMediaFailed(new MediaFailedEventArgs("录制组件初始化失败", ex));
            }
        }

        // 设置智能分流滤镜，分离预览和录制流
        private void SetupSmartTee()
        {
            var graph = GetGraph();
            var captureDevice = GetCaptureDevice();

            var mediaControl = (IMediaControl)m_graph;
            mediaControl.StopWhenReady();
            // 创建Smart Tee滤镜
            _smartTeeFilter = (IBaseFilter)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_LAV_MP4_Mux));
            var hr = graph.AddFilter(_smartTeeFilter, "Smart Tee");
            DsError.ThrowExceptionForHR(hr);

            // 连接捕获设备到Smart Tee
            var captureOutPin = DsFindPin.ByCategory(captureDevice, PinCategory.Capture, 0);
            var teeInPin = DsFindPin.ByDirection(_smartTeeFilter, PinDirection.Input, 0);

            hr = graph.Connect(captureOutPin, teeInPin);
            DsError.ThrowExceptionForHR(hr);

            // 重新连接预览流（从Smart Tee的Preview输出到渲染器）
            var teePreviewPin = DsFindPin.ByName(_smartTeeFilter, "Preview");
            var rendererPin = DsFindPin.ByDirection(GetRenderer(), PinDirection.Input, 0);

            hr = graph.Connect(teePreviewPin, rendererPin);
            DsError.ThrowExceptionForHR(hr);

            // 释放临时引脚引用
            Marshal.ReleaseComObject(captureOutPin);
            Marshal.ReleaseComObject(teeInPin);
            Marshal.ReleaseComObject(teePreviewPin);
            Marshal.ReleaseComObject(rendererPin);
        }
        #endregion

        #region 录制控制
        public void StartRecording(string outputPath)
        {
            if (!_isInitialized || _isRecording || string.IsNullOrEmpty(outputPath))
                return;

            try
            {
                var graph = GetGraph();

                // 配置视频压缩
                ConfigureVideoCompression();

                // 创建MP4复用器
                _muxFilter = (IBaseFilter)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_LAV_MP4_Mux));
                var hr = graph.AddFilter(_muxFilter, "LAV MP4 Muxer");
                DsError.ThrowExceptionForHR(hr);

                // 设置输出文件
                hr = _captureGraph.SetOutputFileName(
                    MEDIASUBTYPE_H264,
                    outputPath,
                    out _,
                    out _fileSink);
                DsError.ThrowExceptionForHR(hr);

                // 从Smart Tee的Capture输出连接到复用器
                var teeCapturePin = DsFindPin.ByName(_smartTeeFilter, "Capture");

                hr = _captureGraph.RenderStream(
                    PinCategory.Capture,
                    MediaType.Video,
                    teeCapturePin,
                    null,
                    _muxFilter);
                DsError.ThrowExceptionForHR(hr);

                Marshal.ReleaseComObject(teeCapturePin);

                // 启动录制
                GetMediaControl()?.Run();
                _isRecording = true;
            }
            catch (Exception ex)
            {
                StopRecording();
                InvokeMediaFailed(new MediaFailedEventArgs("启动录制失败", ex));
            }
        }

        public void StopRecording()
        {
            if (!_isRecording) return;

            // 停止媒体控制
            GetMediaControl()?.Stop();

            // 释放复用器和文件接收器
            if (_fileSink != null)
            {
                _fileSink.SetFileName(null, null);
                Marshal.ReleaseComObject(_fileSink);
                _fileSink = null;
            }

            if (_muxFilter != null && GetGraph() != null)
            {
                GetGraph().RemoveFilter(_muxFilter);
                Marshal.ReleaseComObject(_muxFilter);
                _muxFilter = null;
            }

            _isRecording = false;

            // 重启预览
            GetMediaControl()?.Run();
        }
        #endregion

        #region 视频压缩配置
        private void ConfigureVideoCompression()
        {
            var captureDevice = GetCaptureDevice();
            if (captureDevice == null) return;

            // 获取视频流配置接口
            var videoPin = DsFindPin.ByCategory(captureDevice, PinCategory.Capture, 0);
            var streamConfig = videoPin as IAMStreamConfig;
            if (streamConfig == null)
                throw new Exception("无法获取视频流配置接口");

            try
            {
                // 获取当前媒体类型
                streamConfig.GetFormat(out var mediaType);
                try
                {
                    // 修改媒体类型参数（压缩配置）
                    var videoInfo = (VideoInfoHeader)Marshal.PtrToStructure(
                        mediaType.formatPtr, typeof(VideoInfoHeader));

                    // 设置分辨率
                    videoInfo.BmiHeader.Width = VideoWidth;
                    videoInfo.BmiHeader.Height = VideoHeight;

                    // 设置帧率（100纳秒为单位）
                    videoInfo.AvgTimePerFrame = DSHOW_ONE_SECOND_UNIT / FrameRate;

                    // 设置H.264压缩格式
                    mediaType.subType = MEDIASUBTYPE_H264;

                    // 应用配置
                    Marshal.StructureToPtr(videoInfo, mediaType.formatPtr, false);
                    var hr = streamConfig.SetFormat(mediaType);
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
        #endregion

        #region 基类成员访问（通过反射）
        private IGraphBuilder GetGraph()
        {
            return m_graph;
        }

        private IBaseFilter GetCaptureDevice()
        {
            return m_captureDevice;
        }

        private IMediaControl GetMediaControl()
        {
            return m_mediaControl;
        }

        private IBaseFilter GetRenderer()
        {
            return m_renderer;
        }

        private T GetBaseFieldValue<T>(string fieldName)
        {
            var field = GetType().BaseType.GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            return field != null ? (T)field.GetValue(this) : default;
        }
        #endregion

        #region 资源释放
        protected override void FreeResources()
        {
            StopRecording();
            _initChecker?.Stop();

            if (_smartTeeFilter != null)
            {
                GetGraph()?.RemoveFilter(_smartTeeFilter);
                Marshal.ReleaseComObject(_smartTeeFilter);
                _smartTeeFilter = null;
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

    // WPF控件封装
    public class VideoRecorderElement : VideoCaptureElement
    {
        private AdvancedVideoRecorder _recorder;

        #region 依赖属性（可在XAML中设置）
        public static readonly DependencyProperty VideoBitRateProperty =
            DependencyProperty.Register("VideoBitRate", typeof(int), typeof(VideoRecorderElement),
                new FrameworkPropertyMetadata(2500000));

        public static readonly DependencyProperty FrameRateProperty =
            DependencyProperty.Register("FrameRate", typeof(int), typeof(VideoRecorderElement),
                new FrameworkPropertyMetadata(30));

        public static readonly DependencyProperty VideoWidthProperty =
            DependencyProperty.Register("VideoWidth", typeof(int), typeof(VideoRecorderElement),
                new FrameworkPropertyMetadata(1280));

        public static readonly DependencyProperty VideoHeightProperty =
            DependencyProperty.Register("VideoHeight", typeof(int), typeof(VideoRecorderElement),
                new FrameworkPropertyMetadata(720));

        public int VideoBitRate
        {
            get => (int)GetValue(VideoBitRateProperty);
            set => SetValue(VideoBitRateProperty, value);
        }

        public int FrameRate
        {
            get => (int)GetValue(FrameRateProperty);
            set => SetValue(FrameRateProperty, value);
        }

        public int VideoWidth
        {
            get => (int)GetValue(VideoWidthProperty);
            set => SetValue(VideoWidthProperty, value);
        }

        public int VideoHeight
        {
            get => (int)GetValue(VideoHeightProperty);
            set => SetValue(VideoHeightProperty, value);
        }
        #endregion

        protected override MediaPlayerBase OnRequestMediaPlayer()
        {
            _recorder = new AdvancedVideoRecorder
            {
                VideoBitRate = VideoBitRate,
                FrameRate = FrameRate,
                VideoWidth = VideoWidth,
                VideoHeight = VideoHeight
            };
            return _recorder;
        }

        #region 录制控制方法（供外部调用）
        public void StartRecording(string path)
        {
            if (_recorder != null && !string.IsNullOrEmpty(path))
            {
                VideoCapturePlayer.Dispatcher.BeginInvoke(() =>
                {
                    _recorder.StartRecording(path);
                });
            }
        }

        public void StopRecording()
        {
            VideoCapturePlayer.Dispatcher.BeginInvoke(() =>
            {
                _recorder?.StopRecording();
            });
        }
        #endregion
    }
}
