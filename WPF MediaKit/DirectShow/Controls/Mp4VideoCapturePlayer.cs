using System;
using System.Runtime.InteropServices;
using DirectShowLib;
using WPFMediaKit.DirectShow.MediaPlayers;
using WPFMediaKit.DirectShow.Controls;
using System.Windows;
using System.Windows.Threading;

namespace WPFMediaKit.DirectShow.Controls
{
    public class Mp4VideoRecorder : VideoCapturePlayer
    {
        // 媒体类型和滤镜CLSID（与之前保持一致）
        private static readonly Guid MEDIASUBTYPE_H264 = new Guid("6B652FFF-11FE-4FCE-92AD-0266B5D7C78F");
        private static readonly Guid CLSID_MPEG4Mux = new Guid("0F6417D6-76AE-4322-8230-58D47E0E5F12");
        private static readonly Guid MEDIASUBTYPE_ASF = new Guid("3DB80F90-9412-11CF-9E6F-00AA00A3F1A6");
        private static readonly Guid CLSID_LAV_MP4_Mux = new Guid("171252A0-8820-4AFE-9DF8-5C92B2D66B04");

        // 压缩参数和录制状态（与之前保持一致）
        public int VideoBitRate { get; set; } = 2000000;
        public int FrameRate { get; set; } = 30;
        private ICaptureGraphBuilder2 _captureGraph;
        private IBaseFilter _muxFilter;
        private IFileSinkFilter _fileSink;
        private bool _isRecording;
        private bool _isInitialized;

        // 构造函数中启动初始化检查
        public Mp4VideoRecorder()
        {
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
                    Dispatcher.BeginInvoke((Action)(() =>
                    {
                        CompleteInitialization();
                    }));
                    
                }
            };
            initTimer.Start();
        }

        // 判断基类是否已初始化
        private bool IsBaseInitialized()
        {
            // 根据基类实际情况调整判断条件，确保核心组件已创建
            return GetGraph() != null && GetCaptureDevice() != null;
        }

        // 完成自定义初始化
        private void CompleteInitialization()
        {
            try
            {
                InitializeCaptureGraph();
                ConfigureCompressionSettings();
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                InvokeMediaFailed(new MediaFailedEventArgs("初始化录制组件失败", ex));
            }
        }

        // 封装基类核心对象的访问（避免直接访问私有成员）
        private IGraphBuilder GetGraph()
        {
            // 如果基类有公开Graph的方法，使用该方法；否则通过反射（不推荐但可行）
            // 示例：假设基类有保护成员m_graph，通过反射获取
            return m_graph;
        }

        private IBaseFilter GetCaptureDevice()
        {
            // 同理获取捕获设备
            return m_captureDevice;
        }

        private IMediaControl GetMediaControl()
        {
            // 获取媒体控制接口
            return m_mediaControl;
        }

        // 反射辅助方法（获取基类私有/保护成员）
        private T GetBaseFieldValue<T>(string fieldName)
        {
            var field = GetType().BaseType.GetField(fieldName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            return field != null ? (T)field.GetValue(this) : default;
        }

        // 初始化捕获图形构建器（与之前保持一致）
        private void InitializeCaptureGraph()
        {
            var graph = GetGraph();
            if (graph == null) return;

            _captureGraph = (ICaptureGraphBuilder2)new CaptureGraphBuilder2();
            int hr = _captureGraph.SetFiltergraph(graph);
            DsError.ThrowExceptionForHR(hr);
        }

        // 配置压缩参数（与之前保持一致）
        private void ConfigureCompressionSettings()
        {
            var captureDevice = GetCaptureDevice();
            if (captureDevice == null) return;

            try
            {
                var videoPin = DsFindPin.ByCategory(captureDevice, PinCategory.Capture, 0);
                var streamConfig = videoPin as IAMStreamConfig;
                if (streamConfig == null)
                    throw new Exception("无法获取视频流配置接口");

                AMMediaType mediaType;
                streamConfig.GetFormat(out mediaType);

                try
                {
                    var videoInfo = (VideoInfoHeader)Marshal.PtrToStructure(
                        mediaType.formatPtr, typeof(VideoInfoHeader));

                    // 设置压缩参数
                    videoInfo.BmiHeader.Width = DesiredWidth;
                    videoInfo.BmiHeader.Height = DesiredHeight;
                    videoInfo.AvgTimePerFrame = DSHOW_ONE_SECOND_UNIT / FrameRate;
                    mediaType.subType = MEDIASUBTYPE_H264;

                    Marshal.StructureToPtr(videoInfo, mediaType.formatPtr, false);
                    streamConfig.SetFormat(mediaType);
                }
                finally
                {
                    DsUtils.FreeAMMediaType(mediaType);
                    Marshal.ReleaseComObject(videoPin);
                }
            }
            catch (Exception ex)
            {
                InvokeMediaFailed(new MediaFailedEventArgs("压缩参数配置失败", ex));
            }
        }

        // 开始录制（与之前保持一致，增加初始化检查）
        public void StartRecording(string outputPath)
        {
            if (!_isInitialized || _isRecording || string.IsNullOrEmpty(outputPath))
                return;

            try
            {
                VerifyAccess();
                var graph = GetGraph();
                if (graph == null) return;
                var graphBuilder = (ICaptureGraphBuilder2)new CaptureGraphBuilder2();
                int hr = graphBuilder.SetFiltergraph(graph);
                DsError.ThrowExceptionForHR(hr);


                var mediaControl = (IMediaControl)m_graph;
                mediaControl.StopWhenReady();
                IBaseFilter _muxFilter;
                IFileSinkFilter _fileSink;
                // 创建MP4复用器
                _muxFilter = (IBaseFilter)Activator.CreateInstance(
                    Type.GetTypeFromCLSID(CLSID_LAV_MP4_Mux));
                //_muxFilter = FindEncoder(MediaSubType.Avi);
                  hr = graph.AddFilter(_muxFilter, "MP4 Muxer");
                DsError.ThrowExceptionForHR(hr);

                // 设置输出文件
                hr = graphBuilder.SetOutputFileName(
                    MediaSubType.YUY2, outputPath, out _, out _fileSink);
                DsError.ThrowExceptionForHR(hr); 

                // 连接视频流
                hr = graphBuilder.RenderStream(
                    PinCategory.Capture, MediaType.Video, m_captureDevice, null, _muxFilter);
                  

                    DsError.ThrowExceptionForHR(hr);

                // 启动录制
                mediaControl?.Run();
                _isRecording = true;
            }
            catch (Exception ex)
            {
                StopRecording();
                InvokeMediaFailed(new MediaFailedEventArgs("录制启动失败", ex));
                var mediaControl = (IMediaControl)m_graph;
                mediaControl.Run();
            }
        }

        // 停止录制和资源释放（与之前保持一致）
        public void StopRecording()
        {
            if (!_isRecording) return;

            GetMediaControl()?.Stop();

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
            GetMediaControl()?.Run(); // 重启预览
        }

        protected override void FreeResources()
        {
            StopRecording();

            if (_captureGraph != null)
            {
                Marshal.ReleaseComObject(_captureGraph);
                _captureGraph = null;
            }

            base.FreeResources();
        }
    }

    // 自定义控件（与之前保持一致）
    public class Mp4RecorderElement : VideoCaptureElement
    {
        private Mp4VideoRecorder _mp4Recorder;

        public static readonly DependencyProperty VideoBitRateProperty =
            DependencyProperty.Register("VideoBitRate", typeof(int), typeof(Mp4RecorderElement),
                new FrameworkPropertyMetadata(2000000));

        public int VideoBitRate
        {
            get => (int)GetValue(VideoBitRateProperty);
            set => SetValue(VideoBitRateProperty, value);
        }

        protected override MediaPlayerBase OnRequestMediaPlayer()
        {
            _mp4Recorder = new Mp4VideoRecorder
            {
                VideoBitRate = VideoBitRate,
                FrameRate = 30
            };
            return _mp4Recorder;
        }
         
    }
}
