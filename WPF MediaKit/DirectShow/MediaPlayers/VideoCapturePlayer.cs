//using System;
//using System.Collections.Generic;
//using System.Drawing;
//using System.Drawing.Imaging;
//using System.Runtime.InteropServices;
//using System.Text;
//using DirectShowLib;
//using static System.Windows.Forms.LinkLabel;

//namespace WPFMediaKit.DirectShow.MediaPlayers
//{
//    public class VideoSampleArgs : EventArgs
//    {
//        public Bitmap VideoFrame { get; internal set; }
//    }

//    /// <summary>
//    /// A Player that plays video from a video capture device.
//    /// </summary>
//    public class VideoCapturePlayer : MediaPlayerBase, ISampleGrabberCB
//    {
//        [DllImport("Kernel32.dll", EntryPoint = "RtlMoveMemory")]
//        private static extern void CopyMemory(IntPtr destination, IntPtr source, [MarshalAs(UnmanagedType.U4)] int length);

//        #region Locals
//        /// <summary>
//        /// The video capture pixel height
//        /// </summary>
//        private int m_desiredHeight = 240;

//        /// <summary>
//        /// The video capture pixel width
//        /// </summary>
//        private int m_desiredWidth = 320;

//        /// <summary>
//        /// The video capture's frames per second
//        /// </summary>
//        private int m_fps = 30;

//        /// <summary>
//        /// Our DirectShow filter graph
//        /// </summary>
//        public IGraphBuilder m_graph;

//        /// <summary>
//        /// The DirectShow video renderer
//        /// </summary>
//        public IBaseFilter m_renderer;

//        /// <summary>
//        /// The capture device filter
//        /// </summary>
//        public IBaseFilter m_captureDevice;

//        /// <summary>
//        /// The name of the video capture source device
//        /// </summary>
//        private string m_videoCaptureSource;

//        /// <summary>
//        /// Flag to detect if the capture source has changed
//        /// </summary>
//        private bool m_videoCaptureSourceChanged;

//        /// <summary>
//        /// The video capture device
//        /// </summary>
//        private DsDevice m_videoCaptureDevice;

//        /// <summary>
//        /// Flag to detect if the capture source device has changed
//        /// </summary>
//        private bool m_videoCaptureDeviceChanged;

//        /// <summary>
//        /// The sample grabber interface used for getting samples in a callback
//        /// </summary>
//        private ISampleGrabber m_sampleGrabber;

//        public string m_fileName;

//#if DEBUG
//        private DsROTEntry m_rotEntry;
//#endif
//        #endregion

//        /// <summary>
//        /// Gets or sets if the instance fires an event for each of the samples
//        /// </summary>
//        public bool EnableSampleGrabbing { get; set; }

//        /// <summary>
//        /// Fires when a new video sample is ready
//        /// </summary>
//        public event EventHandler<VideoSampleArgs> NewVideoSample;

//        private void InvokeNewVideoSample(VideoSampleArgs e)
//        {
//            EventHandler<VideoSampleArgs> sample = NewVideoSample;
//            if (sample != null) sample(this, e);
//        }

//        /// <summary>
//        /// The name of the video capture source to use
//        /// </summary>
//        public string VideoCaptureSource
//        {
//            get
//            {
//                VerifyAccess();
//                return m_videoCaptureSource;
//            }
//            set
//            {
//                VerifyAccess();
//                m_videoCaptureSource = value;
//                m_videoCaptureSourceChanged = true;

//                /* Free our unmanaged resources when
//                 * the source changes */
//                FreeResources();
//            }
//        }

//        public DsDevice VideoCaptureDevice
//        {
//            get
//            {
//                VerifyAccess();
//                return m_videoCaptureDevice;
//            }
//            set
//            {
//                VerifyAccess();
//                m_videoCaptureDevice = value;
//                m_videoCaptureDeviceChanged = true;

//                /* Free our unmanaged resources when
//                 * the source changes */
//                FreeResources();
//            }
//        }

//        /// <summary>
//        /// The frames per-second to play
//        /// the capture device back at
//        /// </summary>
//        public int FPS
//        {
//            get
//            {
//                VerifyAccess();
//                return m_fps;
//            }
//            set
//            {
//                VerifyAccess();

//                /* We support only a minimum of
//                 * one frame per second */
//                if (value < 1)
//                    value = 1;

//                m_fps = value;
//            }
//        }

//        /// <summary>
//        /// Gets or sets if Yuv is the prefered color space
//        /// </summary>
//        public bool UseYuv { get; set; }

//        /// <summary>
//        /// The desired pixel width of the video
//        /// </summary>
//        public int DesiredWidth
//        {
//            get
//            {
//                VerifyAccess();
//                return m_desiredWidth;
//            }
//            set
//            {
//                VerifyAccess();
//                m_desiredWidth = value;
//            }
//        }

//        /// <summary>
//        /// The desired pixel height of the video
//        /// </summary>
//        public int DesiredHeight
//        {
//            get
//            {
//                VerifyAccess();
//                return m_desiredHeight;
//            }
//            set
//            {
//                VerifyAccess();
//                m_desiredHeight = value;
//            }
//        }

//        public string FileName
//        {
//            get
//            {
//                //VerifyAccess();
//                return m_fileName;
//            }
//            set
//            {
//                //VerifyAccess();
//                m_fileName = value;
//            }
//        }

//        /// <summary>
//        /// Plays the video capture device
//        /// </summary>
//        public override void Play()
//        {
//            VerifyAccess();
//            if (m_graph == null)
//                SetupGraph();

//            base.Play();
//        }

//        /// <summary>
//        /// Pauses the video capture device
//        /// </summary>
//        public override void Pause()
//        {
//            VerifyAccess();

//            if (m_graph == null)
//                SetupGraph();

//            base.Pause();
//        }

//        public void ShowCapturePropertyPages(IntPtr hwndOwner)
//        {
//            VerifyAccess();

//            if (m_captureDevice == null)
//                return;

//            using (var dialog = new PropertyPageHelper(m_captureDevice))
//            {
//                dialog.Show(hwndOwner);
//            }
//        }

//        public void StartLoad()
//        {
//            /* Clean up any messes left behind */
//            //FreeResources();

//            try
//            {
//                /* Create a capture graph builder to help 
//                 * with rendering a capture graph */
//                var graphBuilder = (ICaptureGraphBuilder2)new CaptureGraphBuilder2();

//                var mediaControl = (IMediaControl)m_graph;
//                mediaControl.Pause();//?.StopWhenReady();
//                /* Set our filter graph to the capture graph */
//                int hr = graphBuilder.SetFiltergraph(m_graph);
//                DsError.ThrowExceptionForHR(hr);

//                // 强制设置MJPG格式确保兼容性
//                SetVideoCaptureParameters(graphBuilder, m_captureDevice, MediaSubType.MJPG);

//                hr = graphBuilder.RenderStream(PinCategory.Preview,
//                    MediaType.Video,
//                    m_captureDevice,
//                    null,
//                    m_renderer);
//                DsError.ThrowExceptionForHR(hr);

//                mediaControl.Run();
//                /* Register the filter graph
//                 * with the base classes */
//                SetupFilterGraph(m_graph);

//                /* Sets the NaturalVideoWidth/Height */
//                SetNativePixelSizes(m_renderer);
//                Marshal.ReleaseComObject(graphBuilder);
//            }
//            catch (Exception ex)
//            {
//                /* Something got fuct up */
//                FreeResources();
//                InvokeMediaFailed(new MediaFailedEventArgs(ex.Message, ex));
//            }
//        }

//        // 添加关键方法：StartCapture和StopCapture
//        public void StartCapture(string filePath, bool isWav = false)
//        {
//            VerifyAccess();
//            try
//            {
//                //StartRecording(filePath);
//                //StartRecording(filePath);
//                // 重新创建过滤器图
//                //m_graph = (IGraphBuilder)new FilterGraphNoThread();
//#if DEBUG
//                m_rotEntry = new DsROTEntry(m_graph);
//#endif

//                var graphBuilder = (ICaptureGraphBuilder2)new CaptureGraphBuilder2();
//                int hr = graphBuilder.SetFiltergraph(m_graph);
//                DsError.ThrowExceptionForHR(hr);

//                // 强制重新初始化捕获设备
//                //if (m_videoCaptureDevice != null)
//                //{
//                //    m_captureDevice = AddFilterByDevicePath(m_graph,
//                //        FilterCategory.VideoInputDevice,
//                //        m_videoCaptureDevice.DevicePath);
//                //}
//                //else if (!string.IsNullOrEmpty(m_videoCaptureSource))
//                //{
//                //    m_captureDevice = AddFilterByName(m_graph,
//                //        FilterCategory.VideoInputDevice,
//                //        m_videoCaptureSource);
//                //}

//                //// 确保捕获设备已正确添加
//                //if (m_captureDevice == null)
//                //    throw new ApplicationException("视频捕获设备初始化失败"); 
//                //m_captureDevice = AddFilterByName(m_graph,
//                //    FilterCategory.VideoInputDevice,
//                //    VideoCaptureSource);

//                //m_videoCaptureSourceChanged = false;
//                // 停止媒体流
//                var mediaControl = (IMediaControl)m_graph;
//                mediaControl.StopWhenReady();
//                //if (UseYuv && !EnableSampleGrabbing)
//                //{
//                //    /* Configure the video output pin with our parameters and if it fails
//                //     * then just use the default media subtype*/
//                //    if (!SetVideoCaptureParameters(graphBuilder, m_captureDevice, MediaSubType.YUY2))
//                //        SetVideoCaptureParameters(graphBuilder, m_captureDevice, MediaSubType.MJPG);
//                //}
//                //else
//                //    /* Configure the video output pin with our parameters */
//                //    SetVideoCaptureParameters(graphBuilder, m_captureDevice, MediaSubType.MJPG);
//                //// 强制设置MJPG格式确保兼容性
//                ////SetVideoCaptureParameters(graphBuilder, m_captureDevice, MediaSubType.MJPG);
//                //var rendererType = VideoRendererType.VideoMixingRenderer9;

//                ///* Creates a video renderer and register the allocator with the base class */
//                //m_renderer = CreateVideoRenderer(rendererType, m_graph, 1);

//                //if (rendererType == VideoRendererType.VideoMixingRenderer9)
//                //{
//                //    var mixer = m_renderer as IVMRMixerControl9;

//                //    if (mixer != null && !EnableSampleGrabbing && UseYuv)
//                //    {
//                //        VMR9MixerPrefs dwPrefs;
//                //        mixer.GetMixingPrefs(out dwPrefs);
//                //        dwPrefs &= ~VMR9MixerPrefs.RenderTargetMask;
//                //        dwPrefs |= VMR9MixerPrefs.RenderTargetYUV;
//                //        /* Prefer YUV */
//                //        mixer.SetMixingPrefs(dwPrefs);
//                //    }
//                //}
//                IBaseFilter mux;
//                IFileSinkFilter sink;

//                // 创建AVI复用器和文件写入器
//                hr = graphBuilder.SetOutputFileName(MediaSubType.Avi, filePath, out mux, out sink);
//                DsError.ThrowExceptionForHR(hr);

//                if (isWav)
//                {
//                    var audioDevices = DsDevice.GetDevicesOfCat(FilterCategory.AudioInputDevice);

//                    if (audioDevices.Length > 0)
//                    {
//                        var audioDevice = AddFilterByDevicePath(m_graph,
//                            FilterCategory.AudioInputDevice,
//                            audioDevices[0].DevicePath);

//                        hr = graphBuilder.RenderStream(PinCategory.Capture, MediaType.Audio, audioDevice, null, mux);
//                        DsError.ThrowExceptionForHR(hr);
//                    }
//                }

//                // 查找MJPG编码器（可选，用于格式转换）
//                IBaseFilter encoder = FindEncoder(MediaSubType.Avi);
//                if (encoder != null)
//                {
//                    hr = m_graph.AddFilter(encoder, "Avi Encoder");
//                    DsError.ThrowExceptionForHR(hr);
//                }

//                // 渲染视频流：CaptureDevice -> Encoder -> Mux
//                hr = graphBuilder.RenderStream(
//                    PinCategory.Capture,
//                    MediaType.Video,
//                    m_captureDevice,
//                    encoder,
//                    mux);
//                // 手动连接引脚（如果自动失败）
//                if (hr < 0)
//                {
//                    ConnectPinsManually(m_captureDevice, "Capture", mux, "Input");
//                }
//                DsError.ThrowExceptionForHR(hr);
//                mediaControl.Run();
//                //Play();
//                // 清理COM对象
//                SafeRelease(mux);
//                SafeRelease(sink);
//                SafeRelease(graphBuilder);

//            }
//            catch (Exception ex)
//            {
//                //FreeResources();
//                //Play();
//                Console.WriteLine("录像启动失败" + ex.Message);
//            }
//        }
//        private FilterGraph graph;
//        private IBaseFilter videoSource;
//        private IBaseFilter h264Encoder;
//        private IBaseFilter mp4Mux;
//        private IFileSinkFilter fileWriter;

//        // Windows SDK 编码器的 CLSID
//        private static readonly Guid CLSID_H264Encoder = MediaSubType.H264;
//        private static readonly Guid CLSID_AACEncoder = new Guid("{C1F400A0-3F08-11D3-9F0B-006008039E37}");
//        private static readonly Guid CLSID_MPEG4SinkWriter = MediaSubType.Mpeg2Video;


//        public void StartRecording(string filePath)
//        {
//            var graphBuilder = (ICaptureGraphBuilder2)new CaptureGraphBuilder2();
//            graphBuilder.SetFiltergraph((IGraphBuilder)graph);

//            // 添加视频源
//            AddVideoSource();

//            // 添加 H.264 编码器（使用 Windows SDK）
//            h264Encoder = CreateSystemEncoder(CLSID_H264Encoder);
//            m_graph.AddFilter(h264Encoder, "H.264 Encoder");
//            ConfigureH264Encoder(h264Encoder);

//            // 添加 MP4 Mux（使用 Windows SDK）
//            mp4Mux = CreateSystemFilter(CLSID_MPEG4SinkWriter);
//            m_graph.AddFilter(mp4Mux, "MP4 Mux");

//            // 添加文件写入器
//            fileWriter = CreateFileWriter(filePath);
//            m_graph.AddFilter((IBaseFilter)fileWriter, "File Writer");

//            // 连接过滤器
//            graphBuilder.RenderStream(null, null, videoSource, h264Encoder, mp4Mux);
//            graphBuilder.RenderStream(null, null, mp4Mux, null, (IBaseFilter)fileWriter);

//            // 开始录制
//            var mediaControl = (IMediaControl)graph;
//            mediaControl.Run();
//        }

//        private IBaseFilter CreateSystemEncoder(Guid clsid)
//        {
//            try
//            {
//                return (IBaseFilter)Activator.CreateInstance(Type.GetTypeFromCLSID(clsid));
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"创建编码器失败: {ex.Message}");
//                throw;
//            }
//        }

//        private IBaseFilter CreateSystemFilter(Guid clsid)
//        {
//            try
//            {
//                return (IBaseFilter)Activator.CreateInstance(Type.GetTypeFromCLSID(clsid));
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"创建过滤器失败: {ex.Message}");
//                throw;
//            }
//        }

//        private IFileSinkFilter CreateFileWriter(string filePath)
//        {
//            var fileSink = (IBaseFilter)new FileWriter();
//            var fileSinkFilter = (IFileSinkFilter)fileSink;
//            fileSinkFilter.SetFileName(filePath, null);
//            return fileSinkFilter;
//        }

//        private void AddVideoSource()
//        {
//            // 实现视频源添加逻辑
//            // 例如: videoSource = CreateVideoSourceDevice();
//        }
//        //private IBaseFilter CreateMp4MuxFilter()
//        //{
//        //    try
//        //    {
//        //        // 尝试使用首选的 MP4 Mux
//        //        Guid clsidMp4Mux = MediaSubType.Mpeg2Video;
//        //        return (IBaseFilter)Activator.CreateInstance(Type.GetTypeFromCLSID(clsidMp4Mux));
//        //    }
//        //    catch (COMException)
//        //    {
//        //        // 尝试替代的 MP4 Mux
//        //        try
//        //        {
//        //            Guid clsidAlternativeMp4Mux = MediaSubType.Mpeg2Video;
//        //            return (IBaseFilter)Activator.CreateInstance(Type.GetTypeFromCLSID(clsidAlternativeMp4Mux));
//        //        }
//        //        catch (COMException ex)
//        //        {
//        //            Console.WriteLine("无法创建任何 MP4 Mux 过滤器: " + ex.Message);
//        //            throw;
//        //        }
//        //    }
//        //}

//        private void ConfigureH264Encoder(IBaseFilter encoder)
//        {
//            // 配置H.264编码器参数
//            try
//            {
//                // 获取编码器属性页接口
//                ISpecifyPropertyPages propertyPages = (ISpecifyPropertyPages)encoder;

//                // 设置编码器参数（示例：中等质量）
//                // 实际应用中可能需要更复杂的参数配置
//                // 这里简化处理，使用默认参数
//            }
//            catch { /* 忽略配置错误 */ }
//        }

//        private IBaseFilter CreateMp4MuxFilter()
//        {
//            try
//            {
//                // 尝试使用 Windows SDK 中的 MPEG-4 Sink Writer
//                Guid clsidMpeg4SinkWriter = new Guid("{72EF14B1-F8E5-44BC-BC7F-5A39E2BE7F56}");
//                return CreateFilterByClsid(clsidMpeg4SinkWriter, "MPEG-4 Sink Writer");
//            }
//            catch (COMException)
//            {
//                // 备选方案：使用 LAV Filters 的 MP4 Mux
//                Guid clsidLavMp4Mux = new Guid("{B98D13E7-55DB-4385-A86C-DEB4B6A97429}");
//                return CreateFilterByClsid(clsidLavMp4Mux, "LAV MP4 Mux");
//            }
//        }

//        private IBaseFilter CreateH264Encoder()
//        {
//            try
//            {
//                // 使用 Windows SDK 中的 H.264 编码器
//                Guid clsidH264Encoder = new Guid("{66B56515-5476-4B6F-B75E-3D9B51045A82}");
//                return CreateFilterByClsid(clsidH264Encoder, "H.264 Encoder");
//            }
//            catch (COMException)
//            {
//                // 备选方案：使用 x264vfw
//                Guid clsidX264Encoder = new Guid("{XXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX}"); // 替换为实际的 x264vfw CLSID
//                return CreateFilterByClsid(clsidX264Encoder, "x264vfw Encoder");
//            }
//        }

//        private IBaseFilter CreateAacEncoder()
//        {
//            try
//            {
//                // 使用 Windows Media Audio 编码器 (AAC)
//                Guid clsidWmaEncoder = new Guid("{C1F400A0-3F08-11D3-9F0B-006008039E37}");
//                return CreateFilterByClsid(clsidWmaEncoder, "Windows Media Audio Encoder");
//            }
//            catch (COMException)
//            {
//                // 备选方案：使用 FDK AAC 编码器 (需单独安装)
//                Guid clsidFdkAacEncoder = new Guid("{XXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX}");
//                return CreateFilterByClsid(clsidFdkAacEncoder, "FDK AAC Encoder");
//            }
//        }

//        private IBaseFilter CreateFilterByClsid(Guid clsid, string filterName)
//        {
//            try
//            {
//                Console.WriteLine($"尝试创建过滤器: {filterName} (CLSID: {clsid})");
//                var filter = (IBaseFilter)Activator.CreateInstance(Type.GetTypeFromCLSID(clsid));
//                Console.WriteLine($"成功创建过滤器: {filterName}");
//                return filter;
//            }
//            catch (COMException ex)
//            {
//                Console.WriteLine($"创建过滤器失败: {filterName} - {ex.Message} (HRESULT: {ex.ErrorCode:X})");
//                throw;
//            }
//        }

//        private IFileSinkFilter CreateFileSink(string filePath)
//        {
//            // 创建文件写入器
//            IBaseFilter fileSinkFilter = (IBaseFilter)new FileWriter();
//            IFileSinkFilter fileSink = (IFileSinkFilter)fileSinkFilter;
//            fileSink.SetFileName(filePath, null);
//            return fileSink;
//        }

//        private bool ConnectFilters(IGraphBuilder graph, IBaseFilter src, IBaseFilter dest)
//        {
//            // 连接两个过滤器
//            IPin outPin = GetUnconnectedPin(src, PinDirection.Output);
//            IPin inPin = GetUnconnectedPin(dest, PinDirection.Input);

//            if (outPin == null || inPin == null)
//                return false;

//            int hr = graph.Connect(outPin, inPin);
//            SafeRelease(outPin);
//            SafeRelease(inPin);
//            return hr >= 0;
//        }

//        private IPin GetUnconnectedPin(IBaseFilter filter, PinDirection pinDir)
//        {
//            // 获取过滤器上未连接的引脚
//            IEnumPins pinEnum;
//            filter.EnumPins(out pinEnum);

//            IPin[] pins = new IPin[1];
//            IntPtr fetched = IntPtr.Zero;

//            while (pinEnum.Next(1, pins, fetched) == 0)
//            {
//                IPin pin = pins[0];
//                PinDirection direction;
//                pin.QueryDirection(out direction);

//                if (direction == pinDir)
//                {
//                    IPin connected;
//                    if (pin.ConnectedTo(out connected) < 0)
//                    {
//                        SafeRelease(connected);
//                        return pin;
//                    }
//                    SafeRelease(connected);
//                }
//                SafeRelease(pin);
//            }

//            SafeRelease(pinEnum);
//            return null;
//        }

//        private IEnumerable<IBaseFilter> GetFilters(IGraphBuilder graph)
//        {
//            IEnumFilters enumFilters;
//            graph.EnumFilters(out enumFilters);

//            IBaseFilter[] filters = new IBaseFilter[1];
//            IntPtr fetched = IntPtr.Zero;

//            while (enumFilters.Next(1, filters, fetched) == 0)
//            {
//                yield return filters[0];
//            }
//            Marshal.ReleaseComObject(enumFilters);
//        }
//        public void StopCapture()
//        {
//            VerifyAccess();
//            try
//            {
//                if (m_graph == null) return;

//                var mediaControl = (IMediaControl)m_graph;
//                mediaControl.Pause();

//                // 清理所有文件写入相关过滤器
//                //var filtersToRemove = new List<IBaseFilter>();
//                //foreach (var filter in GetFilters(m_graph))
//                //{
//                //    if (filter is IFileSinkFilter ||
//                //        filter is IBaseFilter && filter.ToString().Contains("MJPG Encoder"))
//                //    {
//                //        filtersToRemove.Add(filter);
//                //    }
//                //}

//                //foreach (var filter in filtersToRemove)
//                //{
//                //    m_graph.RemoveFilter(filter);
//                //    SafeRelease(filter);
//                //}

//                // 显式释放关键COM对象 

//                // 重启预览流
//                mediaControl.Run();
//            }
//            catch (Exception)
//            {
//                //FreeResources();
//                //throw new ApplicationException("停止录像失败", ex);
//            }
//        }

//        // 辅助方法：查找编码器
//        public IBaseFilter FindEncoder(Guid mediaSubType)
//        {
//            foreach (DsDevice device in DsDevice.GetDevicesOfCat(FilterCategory.VideoCompressorCategory))
//            {
//                if (device.Name.Contains("MJPG"))
//                {
//                    return AddFilterByDevicePath(m_graph, FilterCategory.VideoCompressorCategory, device.DevicePath);
//                }
//            }
//            return null;
//        }


//        // 辅助方法：安全释放COM对象
//        private void SafeRelease(object obj)
//        {
//            if (obj != null && Marshal.IsComObject(obj))
//                Marshal.ReleaseComObject(obj);
//        }



//        // 查找或创建编码器（示例查找MJPG编码器）
//        private IBaseFilter FindOrCreateEncoder(Guid mediaSubType)
//        {
//            var encoders = DsDevice.GetDevicesOfCat(FilterCategory.VideoCompressorCategory);
//            foreach (DsDevice encoderDev in encoders)
//            {
//                if (encoderDev.Name.Contains("MJPG") || encoderDev.Name.Contains("MJPEG"))
//                {
//                    return AddFilterByDevicePath(m_graph, FilterCategory.VideoCompressorCategory, encoderDev.DevicePath);
//                }
//            }
//            return null; // 未找到编码器
//        }

//        // 连接引脚（扩展ConnectPinsManually方法）
//        public void ConnectPinsManually(IBaseFilter sourceFilter, string sourcePinName, IBaseFilter destFilter, string destPinName)
//        {
//            IPin sourcePin = DsFindPin.ByName(sourceFilter, sourcePinName);
//            IPin destPin = DsFindPin.ByName(destFilter, destPinName);
//            if (sourcePin != null && destPin != null)
//            {
//                int hr = m_graph.Connect(sourcePin, destPin);
//                DsError.ThrowExceptionForHR(hr);
//            }
//            SafeReleaseComObject(sourcePin);
//            SafeReleaseComObject(destPin);
//        }

//        // 辅助方法：安全释放COM对象
//        private void SafeReleaseComObject(object obj)
//        {
//            if (obj != null && Marshal.IsComObject(obj))
//            {
//                Marshal.ReleaseComObject(obj);
//            }
//        }
//        /// <summary>
//        /// Configures the DirectShow graph to play the selected video capture
//        /// device with the selected parameters
//        /// </summary>
//        private void SetupGraph()
//        {
//            /* Clean up any messes left behind */
//            FreeResources();

//            try
//            {
//                /* Create a new graph */
//                m_graph = (IGraphBuilder)new FilterGraphNoThread();

//#if DEBUG
//                m_rotEntry = new DsROTEntry(m_graph);
//#endif

//                /* Create a capture graph builder to help 
//                 * with rendering a capture graph */
//                var graphBuilder = (ICaptureGraphBuilder2)new CaptureGraphBuilder2();

//                /* Set our filter graph to the capture graph */
//                int hr = graphBuilder.SetFiltergraph(m_graph);
//                DsError.ThrowExceptionForHR(hr);

//                /* Add our capture device source to the graph */
//                if (m_videoCaptureSourceChanged)
//                {
//                    m_captureDevice = AddFilterByName(m_graph,
//                                                      FilterCategory.VideoInputDevice,
//                                                      VideoCaptureSource);

//                    m_videoCaptureSourceChanged = false;
//                }
//                else if (m_videoCaptureDeviceChanged)
//                {
//                    m_captureDevice = AddFilterByDevicePath(m_graph,
//                                                            FilterCategory.VideoInputDevice,
//                                                            VideoCaptureDevice.DevicePath);

//                    m_videoCaptureDeviceChanged = false;
//                }

//                /* If we have a null capture device, we have an issue */
//                if (m_captureDevice == null)
//                    throw new WPFMediaKitException(string.Format("Capture device {0} not found or could not be created", VideoCaptureSource));

//                if (UseYuv && !EnableSampleGrabbing)
//                {
//                    /* Configure the video output pin with our parameters and if it fails
//                     * then just use the default media subtype*/
//                    if (!SetVideoCaptureParameters(graphBuilder, m_captureDevice, MediaSubType.YUY2))
//                        SetVideoCaptureParameters(graphBuilder, m_captureDevice, MediaSubType.MJPG);
//                }
//                else
//                    /* Configure the video output pin with our parameters */
//                    SetVideoCaptureParameters(graphBuilder, m_captureDevice, MediaSubType.MJPG);

//                var rendererType = VideoRendererType.VideoMixingRenderer9;

//                /* Creates a video renderer and register the allocator with the base class */
//                m_renderer = CreateVideoRenderer(rendererType, m_graph, 1);

//                if (rendererType == VideoRendererType.VideoMixingRenderer9)
//                {
//                    var mixer = m_renderer as IVMRMixerControl9;

//                    if (mixer != null && !EnableSampleGrabbing && UseYuv)
//                    {
//                        VMR9MixerPrefs dwPrefs;
//                        mixer.GetMixingPrefs(out dwPrefs);
//                        dwPrefs &= ~VMR9MixerPrefs.RenderTargetMask;
//                        dwPrefs |= VMR9MixerPrefs.RenderTargetYUV;
//                        /* Prefer YUV */
//                        mixer.SetMixingPrefs(dwPrefs);
//                    }
//                }

//                if (EnableSampleGrabbing)
//                {
//                    m_sampleGrabber = (ISampleGrabber)new SampleGrabber();
//                    SetupSampleGrabber(m_sampleGrabber);
//                    hr = m_graph.AddFilter(m_sampleGrabber as IBaseFilter, "SampleGrabber");
//                    DsError.ThrowExceptionForHR(hr);
//                }

//                IBaseFilter mux = null;
//                IFileSinkFilter sink = null;
//                //if (!string.IsNullOrEmpty(this.m_fileName))
//                //{
//                //    hr = graphBuilder.SetOutputFileName(MediaSubType.Avi, this.m_fileName, out mux, out sink);
//                //    DsError.ThrowExceptionForHR(hr);

//                //    hr = graphBuilder.RenderStream(PinCategory.Capture, MediaType.Video, m_captureDevice, null, mux);
//                //    DsError.ThrowExceptionForHR(hr);

//                //    // use the first audio device
//                //    var audioDevices = DsDevice.GetDevicesOfCat(FilterCategory.AudioInputDevice);

//                //    if (audioDevices.Length > 0)
//                //    {
//                //        var audioDevice = AddFilterByDevicePath(m_graph,
//                //                                            FilterCategory.AudioInputDevice,
//                //                                            audioDevices[0].DevicePath);

//                //        hr = graphBuilder.RenderStream(PinCategory.Capture, MediaType.Audio, audioDevice, null, mux);
//                //        DsError.ThrowExceptionForHR(hr);
//                //    }
//                //}

//                hr = graphBuilder.RenderStream(PinCategory.Preview,
//                                               MediaType.Video,
//                                               m_captureDevice,
//                                               null,
//                                               m_renderer);
//                DsError.ThrowExceptionForHR(hr);

//                /* Register the filter graph 
//                 * with the base classes */
//                SetupFilterGraph(m_graph);

//                /* Sets the NaturalVideoWidth/Height */
//                SetNativePixelSizes(m_renderer);

//                HasVideo = true;

//                /* Make sure we Release() this COM reference */
//                if (mux != null)
//                {
//                    Marshal.ReleaseComObject(mux);
//                }
//                if (sink != null)
//                {
//                    Marshal.ReleaseComObject(sink);
//                }

//                Marshal.ReleaseComObject(graphBuilder);
//            }
//            catch (Exception ex)
//            {
//                /* Something got fuct up */
//                FreeResources();
//                InvokeMediaFailed(new MediaFailedEventArgs(ex.Message, ex));
//            }

//            /* Success */
//            InvokeMediaOpened();
//        }

//        /// <summary>
//        /// Sets the capture parameters for the video capture device
//        /// </summary>
//        private bool SetVideoCaptureParameters(ICaptureGraphBuilder2 capGraph, IBaseFilter captureFilter, Guid mediaSubType)
//        {
//            /* The stream config interface */
//            object streamConfig;

//            /* Get the stream's configuration interface */
//            int hr = capGraph.FindInterface(PinCategory.Capture,
//                                            MediaType.Video,
//                                            captureFilter,
//                                            typeof(IAMStreamConfig).GUID,
//                                            out streamConfig);

//            DsError.ThrowExceptionForHR(hr);

//            var videoStreamConfig = streamConfig as IAMStreamConfig;

//            /* If QueryInterface fails... */
//            if (videoStreamConfig == null)
//            {
//                throw new WPFMediaKitException("Failed to get IAMStreamConfig");
//            }

//            /* The media type of the video */
//            AMMediaType media;

//            /* Get the AMMediaType for the video out pin */
//            hr = videoStreamConfig.GetFormat(out media);
//            DsError.ThrowExceptionForHR(hr);

//            /* Make the VIDEOINFOHEADER 'readable' */
//            var videoInfo = new VideoInfoHeader();
//            Marshal.PtrToStructure(media.formatPtr, videoInfo);

//            /* Setup the VIDEOINFOHEADER with the parameters we want */
//            videoInfo.AvgTimePerFrame = DSHOW_ONE_SECOND_UNIT / FPS;
//            videoInfo.BmiHeader.Width = DesiredWidth;
//            videoInfo.BmiHeader.Height =  DesiredHeight;
//            // 压缩配置：使用H.264编码（需系统安装对应编码器）
//            if (mediaSubType == Guid.Empty && !string.IsNullOrEmpty(m_fileName))
//            {
//                // 尝试设置H.264编码（替换默认格式）
//                mediaSubType = new Guid("34363248-0000-0010-8000-00AA00389B71"); // MEDIASUBTYPE_H264
//            }

//            // 调整分辨率（缩小尺寸降低文件大小）
//            videoInfo.BmiHeader.Width = (int)(DesiredWidth * 0.75); // 75%原始分辨率
//            videoInfo.BmiHeader.Height = (int)(DesiredHeight * 0.75);
//            videoInfo.AvgTimePerFrame = DSHOW_ONE_SECOND_UNIT / (FPS / 2); // 降低帧率

//            if (mediaSubType != Guid.Empty)
//            {
//                int fourCC = 0;
//                byte[] b = mediaSubType.ToByteArray();
//                fourCC = b[0];
//                fourCC |= b[1] << 8;
//                fourCC |= b[2] << 16;
//                fourCC |= b[3] << 24;

//                videoInfo.BmiHeader.Compression = fourCC;
//                media.subType = mediaSubType;
//            }

//            /* Copy the data back to unmanaged memory */
//            Marshal.StructureToPtr(videoInfo, media.formatPtr, false);

//            /* Set the format */
//            hr = videoStreamConfig.SetFormat(media);

//            /* We don't want any memory leaks, do we? */
//            DsUtils.FreeAMMediaType(media);

//            if (hr < 0)
//                return false;

//            return true;
//        }

//        private Bitmap m_videoFrame;

//        private void InitializeBitmapFrame(int width, int height)
//        {
//            if (m_videoFrame != null)
//            {
//                m_videoFrame.Dispose();
//            }

//            m_videoFrame = new Bitmap(width, height, PixelFormat.Format24bppRgb);
//        }

//        #region ISampleGrabberCB Members

//        int ISampleGrabberCB.SampleCB(double sampleTime, IMediaSample pSample)
//        {
//            var mediaType = new AMMediaType();

//            /* We query for the media type the sample grabber is using */
//            int hr = m_sampleGrabber.GetConnectedMediaType(mediaType);

//            var videoInfo = new VideoInfoHeader();

//            /* 'Cast' the pointer to our managed struct */
//            Marshal.PtrToStructure(mediaType.formatPtr, videoInfo);

//            /* The stride is "How many bytes across for each pixel line (0 to width)" */
//            int stride = Math.Abs(videoInfo.BmiHeader.Width * (videoInfo.BmiHeader.BitCount / 8 /* eight bits per byte */));
//            int width = videoInfo.BmiHeader.Width;
//            int height = videoInfo.BmiHeader.Height;

//            if (m_videoFrame == null)
//                InitializeBitmapFrame(width, height);

//            if (m_videoFrame == null)
//                return 0;

//            BitmapData bmpData = m_videoFrame.LockBits(new Rectangle(0, 0, width, height),
//                                                       ImageLockMode.ReadWrite,
//                                                       PixelFormat.Format24bppRgb);

//            /* Get the pointer to the pixels */
//            IntPtr pBmp = bmpData.Scan0;

//            IntPtr samplePtr;

//            /* Get the native pointer to the sample */
//            pSample.GetPointer(out samplePtr);

//            int pSize = stride * height;

//            /* Copy the memory from the sample pointer to our bitmap pixel pointer */
//            CopyMemory(pBmp, samplePtr, pSize);

//            m_videoFrame.UnlockBits(bmpData);

//            InvokeNewVideoSample(new VideoSampleArgs { VideoFrame = m_videoFrame });

//            DsUtils.FreeAMMediaType(mediaType);

//            /* Dereference the sample COM object */
//            Marshal.ReleaseComObject(pSample);
//            return 0;
//        }

//        int ISampleGrabberCB.BufferCB(double sampleTime, IntPtr pBuffer, int bufferLen)
//        {
//            return 0;
//        }

//        #endregion

//        private void SetupSampleGrabber(ISampleGrabber sampleGrabber)
//        {
//            var mediaType = new DirectShowLib.AMMediaType
//            {
//                majorType = MediaType.Video,
//                subType = MediaSubType.RGB24,
//                formatType = FormatType.VideoInfo
//            };

//            int hr = sampleGrabber.SetMediaType(mediaType);

//            DsUtils.FreeAMMediaType(mediaType);
//            DsError.ThrowExceptionForHR(hr);

//            hr = sampleGrabber.SetCallback(this, 0);
//            DsError.ThrowExceptionForHR(hr);
//        }

//        protected override void FreeResources()
//        {
//            /* We run the StopInternal() to avoid any 
//             * Dispatcher VeryifyAccess() issues */
//            StopInternal();

//            /* Let's clean up the base 
//             * class's stuff first */
//            base.FreeResources();

//#if DEBUG
//            if (m_rotEntry != null)
//                m_rotEntry.Dispose();

//            m_rotEntry = null;
//#endif
//            if (m_videoFrame != null)
//            {
//                m_videoFrame.Dispose();
//                m_videoFrame = null;
//            }
//            if (m_renderer != null)
//            {
//                Marshal.FinalReleaseComObject(m_renderer);
//                m_renderer = null;
//            }
//            if (m_captureDevice != null)
//            {
//                Marshal.FinalReleaseComObject(m_captureDevice);
//                m_captureDevice = null;
//            }
//            if (m_sampleGrabber != null)
//            {
//                Marshal.FinalReleaseComObject(m_sampleGrabber);
//                m_sampleGrabber = null;
//            }
//            if (m_graph != null)
//            {
//                Marshal.FinalReleaseComObject(m_graph);
//                m_graph = null;

//                InvokeMediaClosed(new EventArgs());
//            }

//            m_videoCaptureDeviceChanged = true;
//        }
//    }
//}

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using DirectShowLib;

namespace WPFMediaKit.DirectShow.MediaPlayers
{
    public class VideoSampleArgs : EventArgs
    {
        public Bitmap VideoFrame { get; internal set; }
    }

    /// <summary>
    /// A Player that plays video from a video capture device.
    /// </summary>
    public class VideoCapturePlayer : MediaPlayerBase, ISampleGrabberCB
    {
        [DllImport("Kernel32.dll", EntryPoint = "RtlMoveMemory")]
        private static extern void CopyMemory(IntPtr destination, IntPtr source, [MarshalAs(UnmanagedType.U4)] int length);

        #region Locals
        /// <summary>
        /// The video capture pixel height
        /// </summary>
        private int m_desiredHeight = 240;

        /// <summary>
        /// The video capture pixel width
        /// </summary>
        private int m_desiredWidth = 320;

        /// <summary>
        /// The video capture's frames per second
        /// </summary>
        private int m_fps = 30;

        /// <summary>
        /// Our DirectShow filter graph
        /// </summary>
        public  IGraphBuilder m_graph;

        /// <summary>
        /// The DirectShow video renderer
        /// </summary>
        public IBaseFilter m_renderer;

        /// <summary>
        /// The capture device filter
        /// </summary>
        public IBaseFilter m_captureDevice;

        /// <summary>
        /// The name of the video capture source device
        /// </summary>
        private string m_videoCaptureSource;

        /// <summary>
        /// Flag to detect if the capture source has changed
        /// </summary>
        private bool m_videoCaptureSourceChanged;

        /// <summary>
        /// The video capture device
        /// </summary>
        private DsDevice m_videoCaptureDevice;

        /// <summary>
        /// Flag to detect if the capture source device has changed
        /// </summary>
        public bool m_videoCaptureDeviceChanged;

        /// <summary>
        /// The sample grabber interface used for getting samples in a callback
        /// </summary>
        public ISampleGrabber m_sampleGrabber;

        private string m_fileName;

#if DEBUG
        private DsROTEntry m_rotEntry;
#endif
        #endregion

        /// <summary>
        /// Gets or sets if the instance fires an event for each of the samples
        /// </summary>
        public bool EnableSampleGrabbing { get; set; }

        /// <summary>
        /// Fires when a new video sample is ready
        /// </summary>
        public event EventHandler<VideoSampleArgs> NewVideoSample;

        private void InvokeNewVideoSample(VideoSampleArgs e)
        {
            EventHandler<VideoSampleArgs> sample = NewVideoSample;
            if (sample != null) sample(this, e);
        }

        /// <summary>
        /// The name of the video capture source to use
        /// </summary>
        public string VideoCaptureSource
        {
            get
            {
                VerifyAccess();
                return m_videoCaptureSource;
            }
            set
            {
                VerifyAccess();
                m_videoCaptureSource = value;
                m_videoCaptureSourceChanged = true;

                /* Free our unmanaged resources when
                 * the source changes */
                FreeResources();
            }
        }

        public DsDevice VideoCaptureDevice
        {
            get
            {
                VerifyAccess();
                return m_videoCaptureDevice;
            }
            set
            {
                VerifyAccess();
                m_videoCaptureDevice = value;
                m_videoCaptureDeviceChanged = true;

                /* Free our unmanaged resources when
                 * the source changes */
                FreeResources();
            }
        }

        /// <summary>
        /// The frames per-second to play
        /// the capture device back at
        /// </summary>
        public int FPS
        {
            get
            {
                VerifyAccess();
                return m_fps;
            }
            set
            {
                VerifyAccess();

                /* We support only a minimum of
                 * one frame per second */
                if (value < 1)
                    value = 1;

                m_fps = value;
            }
        }

        /// <summary>
        /// Gets or sets if Yuv is the prefered color space
        /// </summary>
        public bool UseYuv { get; set; }

        /// <summary>
        /// The desired pixel width of the video
        /// </summary>
        public int DesiredWidth
        {
            get
            {
                VerifyAccess();
                return m_desiredWidth;
            }
            set
            {
                VerifyAccess();
                m_desiredWidth = value;
            }
        }

        /// <summary>
        /// The desired pixel height of the video
        /// </summary>
        public int DesiredHeight
        {
            get
            {
                VerifyAccess();
                return m_desiredHeight;
            }
            set
            {
                VerifyAccess();
                m_desiredHeight = value;
            }
        }

        public string FileName
        {
            get
            {
                //VerifyAccess();
                return m_fileName;
            }
            set
            {
                //VerifyAccess();
                m_fileName = value;
            }
        }

        /// <summary>
        /// Plays the video capture device
        /// </summary>
        public override void Play()
        {
            VerifyAccess();

            if (m_graph == null)
                SetupGraph();

            base.Play();
        }

        /// <summary>
        /// Pauses the video capture device
        /// </summary>
        public override void Pause()
        {
            VerifyAccess();

            if (m_graph == null)
                SetupGraph();

            base.Pause();
        }

        public void ShowCapturePropertyPages(IntPtr hwndOwner)
        {
            VerifyAccess();

            if (m_captureDevice == null)
                return;

            using (var dialog = new PropertyPageHelper(m_captureDevice))
            {
                dialog.Show(hwndOwner);
            }
        }

        /// <summary>
        /// Configures the DirectShow graph to play the selected video capture
        /// device with the selected parameters
        /// </summary>
        public void SetupGraph()
        {
            /* Clean up any messes left behind */
            FreeResources();

            try
            {
                /* Create a new graph */
                m_graph = (IGraphBuilder)new FilterGraphNoThread();

#if DEBUG
                m_rotEntry = new DsROTEntry(m_graph);
#endif

                /* Create a capture graph builder to help 
                 * with rendering a capture graph */
                var graphBuilder = (ICaptureGraphBuilder2)new CaptureGraphBuilder2();

                /* Set our filter graph to the capture graph */
                int hr = graphBuilder.SetFiltergraph(m_graph);
                DsError.ThrowExceptionForHR(hr);

                /* Add our capture device source to the graph */
                if (m_videoCaptureSourceChanged)
                {
                    m_captureDevice = AddFilterByName(m_graph,
                                                      FilterCategory.VideoInputDevice,
                                                      VideoCaptureSource);

                    m_videoCaptureSourceChanged = false;
                }
                else if (m_videoCaptureDeviceChanged)
                {
                    m_captureDevice = AddFilterByDevicePath(m_graph,
                                                            FilterCategory.VideoInputDevice,
                                                            VideoCaptureDevice.DevicePath);

                    m_videoCaptureDeviceChanged = false;
                }

                /* If we have a null capture device, we have an issue */
                if (m_captureDevice == null)
                    throw new WPFMediaKitException(string.Format("Capture device {0} not found or could not be created", VideoCaptureSource));

                if (UseYuv && !EnableSampleGrabbing)
                {
                    /* Configure the video output pin with our parameters and if it fails
                     * then just use the default media subtype*/
                    if (!SetVideoCaptureParameters(graphBuilder, m_captureDevice, MediaSubType.YUY2))
                        SetVideoCaptureParameters(graphBuilder, m_captureDevice, Guid.Empty);
                }
                else
                    /* Configure the video output pin with our parameters */
                    SetVideoCaptureParameters(graphBuilder, m_captureDevice, Guid.Empty);

                var rendererType = VideoRendererType.VideoMixingRenderer9;

                /* Creates a video renderer and register the allocator with the base class */
                m_renderer = CreateVideoRenderer(rendererType, m_graph, 1);

                if (rendererType == VideoRendererType.VideoMixingRenderer9)
                {
                    var mixer = m_renderer as IVMRMixerControl9;

                    if (mixer != null && !EnableSampleGrabbing && UseYuv)
                    {
                        VMR9MixerPrefs dwPrefs;
                        mixer.GetMixingPrefs(out dwPrefs);
                        dwPrefs &= ~VMR9MixerPrefs.RenderTargetMask;
                        dwPrefs |= VMR9MixerPrefs.RenderTargetYUV;
                        /* Prefer YUV */
                        mixer.SetMixingPrefs(dwPrefs);
                    }
                }

                if (EnableSampleGrabbing)
                {
                    m_sampleGrabber = (ISampleGrabber)new SampleGrabber();
                    SetupSampleGrabber(m_sampleGrabber);
                    hr = m_graph.AddFilter(m_sampleGrabber as IBaseFilter, "SampleGrabber");
                    DsError.ThrowExceptionForHR(hr);
                }

                IBaseFilter mux = null;
                IFileSinkFilter sink = null;
                if (!string.IsNullOrEmpty(this.m_fileName))
                {
                    var mediaSubType = new Guid("34363248-0000-0010-8000-00AA00389B71"); // MEDIASUBTYPE_H264
                    hr = graphBuilder.SetOutputFileName(MediaSubType.Asf, this.m_fileName, out mux, out sink);
                    //hr = graphBuilder.SetOutputFileName(MediaSubType.Asf, this.m_fileName, out mux, out sink);
                    DsError.ThrowExceptionForHR(hr);

                    hr = graphBuilder.RenderStream(PinCategory.Capture, MediaType.Video, m_captureDevice, null, mux);
                    DsError.ThrowExceptionForHR(hr);

                    // use the first audio device
                    var audioDevices = DsDevice.GetDevicesOfCat(FilterCategory.AudioInputDevice);

                    if (audioDevices.Length > 0)
                    {
                        var audioDevice = AddFilterByDevicePath(m_graph,
                                                            FilterCategory.AudioInputDevice,
                                                            audioDevices[0].DevicePath);

                        hr = graphBuilder.RenderStream(PinCategory.Capture, MediaType.Audio, audioDevice, null, mux);
                        DsError.ThrowExceptionForHR(hr);
                    }
                }

                hr = graphBuilder.RenderStream(PinCategory.Preview,
                                               MediaType.Video,
                                               m_captureDevice,
                                               null,
                                               m_renderer);

                DsError.ThrowExceptionForHR(hr);

                /* Register the filter graph 
                 * with the base classes */
                SetupFilterGraph(m_graph);

                /* Sets the NaturalVideoWidth/Height */
                SetNativePixelSizes(m_renderer);

                HasVideo = true;

                /* Make sure we Release() this COM reference */
                if (mux != null)
                {
                    Marshal.ReleaseComObject(mux);
                }
                if (sink != null)
                {
                    Marshal.ReleaseComObject(sink);
                }

                Marshal.ReleaseComObject(graphBuilder);
            }
            catch (Exception ex)
            {
                /* Something got fuct up */
                FreeResources();
                InvokeMediaFailed(new MediaFailedEventArgs(ex.Message, ex));
            }

            /* Success */
            InvokeMediaOpened();
        }

        /// <summary>
        /// Sets the capture parameters for the video capture device
        /// </summary>
        private bool SetVideoCaptureParameters(ICaptureGraphBuilder2 capGraph, IBaseFilter captureFilter, Guid mediaSubType)
        {
            /* The stream config interface */
            object streamConfig;

            /* Get the stream's configuration interface */
            int hr = capGraph.FindInterface(PinCategory.Capture,
                                            MediaType.Video,
                                            captureFilter,
                                            typeof(IAMStreamConfig).GUID,
                                            out streamConfig);

            DsError.ThrowExceptionForHR(hr);

            var videoStreamConfig = streamConfig as IAMStreamConfig;

            /* If QueryInterface fails... */
            if (videoStreamConfig == null)
            {
                throw new WPFMediaKitException("Failed to get IAMStreamConfig");
            }

            /* The media type of the video */
            AMMediaType media;

            /* Get the AMMediaType for the video out pin */
            hr = videoStreamConfig.GetFormat(out media);
            DsError.ThrowExceptionForHR(hr);

            /* Make the VIDEOINFOHEADER 'readable' */
            var videoInfo = new VideoInfoHeader();
            Marshal.PtrToStructure(media.formatPtr, videoInfo);

            /* Setup the VIDEOINFOHEADER with the parameters we want */
            videoInfo.AvgTimePerFrame = DSHOW_ONE_SECOND_UNIT / FPS;
            videoInfo.BmiHeader.Width = DesiredWidth;
            videoInfo.BmiHeader.Height = DesiredHeight;
            // 压缩配置：使用H.264编码（需系统安装对应编码器）
            if (mediaSubType == Guid.Empty && !string.IsNullOrEmpty(m_fileName))
            {
                // 尝试设置H.264编码（替换默认格式）
                mediaSubType = new Guid("34363248-0000-0010-8000-00AA00389B71"); // MEDIASUBTYPE_H264
            }

            // 调整分辨率（缩小尺寸降低文件大小）
            videoInfo.BmiHeader.Width = (int)(DesiredWidth * 0.75); // 75%原始分辨率
            videoInfo.BmiHeader.Height = (int)(DesiredHeight * 0.75);
            videoInfo.AvgTimePerFrame = DSHOW_ONE_SECOND_UNIT / (FPS / 2); // 降低帧率
            if (mediaSubType != Guid.Empty)
            {
                int fourCC = 0;
                byte[] b = mediaSubType.ToByteArray();
                fourCC = b[0];
                fourCC |= b[1] << 8;
                fourCC |= b[2] << 16;
                fourCC |= b[3] << 24;

                videoInfo.BmiHeader.Compression = fourCC;
                media.subType = mediaSubType;
            }

            /* Copy the data back to unmanaged memory */
            Marshal.StructureToPtr(videoInfo, media.formatPtr, false);

            /* Set the format */
            hr = videoStreamConfig.SetFormat(media);

            /* We don't want any memory leaks, do we? */
            DsUtils.FreeAMMediaType(media);

            if (hr < 0)
                return false;

            return true;
        }

        private Bitmap m_videoFrame;

        private void InitializeBitmapFrame(int width, int height)
        {
            if (m_videoFrame != null)
            {
                m_videoFrame.Dispose();
            }

            m_videoFrame = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        }

        #region ISampleGrabberCB Members

        int ISampleGrabberCB.SampleCB(double sampleTime, IMediaSample pSample)
        {
            var mediaType = new AMMediaType();

            /* We query for the media type the sample grabber is using */
            int hr = m_sampleGrabber.GetConnectedMediaType(mediaType);

            var videoInfo = new VideoInfoHeader();

            /* 'Cast' the pointer to our managed struct */
            Marshal.PtrToStructure(mediaType.formatPtr, videoInfo);

            /* The stride is "How many bytes across for each pixel line (0 to width)" */
            int stride = Math.Abs(videoInfo.BmiHeader.Width * (videoInfo.BmiHeader.BitCount / 8 /* eight bits per byte */));
            int width = videoInfo.BmiHeader.Width;
            int height = videoInfo.BmiHeader.Height;

            if (m_videoFrame == null)
                InitializeBitmapFrame(width, height);

            if (m_videoFrame == null)
                return 0;

            BitmapData bmpData = m_videoFrame.LockBits(new Rectangle(0, 0, width, height),
                                                       ImageLockMode.ReadWrite,
                                                       PixelFormat.Format24bppRgb);

            /* Get the pointer to the pixels */
            IntPtr pBmp = bmpData.Scan0;

            IntPtr samplePtr;

            /* Get the native pointer to the sample */
            pSample.GetPointer(out samplePtr);

            int pSize = stride * height;

            /* Copy the memory from the sample pointer to our bitmap pixel pointer */
            CopyMemory(pBmp, samplePtr, pSize);

            m_videoFrame.UnlockBits(bmpData);

            InvokeNewVideoSample(new VideoSampleArgs { VideoFrame = m_videoFrame });

            DsUtils.FreeAMMediaType(mediaType);

            /* Dereference the sample COM object */
            Marshal.ReleaseComObject(pSample);
            return 0;
        }

        int ISampleGrabberCB.BufferCB(double sampleTime, IntPtr pBuffer, int bufferLen)
        {
            throw new NotImplementedException();
        }

        #endregion

        private void SetupSampleGrabber(ISampleGrabber sampleGrabber)
        {
            var mediaType = new DirectShowLib.AMMediaType
            {
                majorType = MediaType.Video,
                subType = MediaSubType.RGB24,
                formatType = FormatType.VideoInfo
            };

            int hr = sampleGrabber.SetMediaType(mediaType);

            DsUtils.FreeAMMediaType(mediaType);
            DsError.ThrowExceptionForHR(hr);

            hr = sampleGrabber.SetCallback(this, 0);
            DsError.ThrowExceptionForHR(hr);
        }
        /// <summary>
        /// 公共方法，供外部调用以释放资源
        /// </summary>
        public void ReleaseResources()
        {
            FreeResources(); // 调用受保护的FreeResources()
        }
        protected override void FreeResources()
        {
            /* We run the StopInternal() to avoid any 
             * Dispatcher VeryifyAccess() issues */
            StopInternal();

            /* Let's clean up the base 
             * class's stuff first */
            base.FreeResources();

#if DEBUG
            if (m_rotEntry != null)
                m_rotEntry.Dispose();

            m_rotEntry = null;
#endif
            if (m_videoFrame != null)
            {
                m_videoFrame.Dispose();
                m_videoFrame = null;
            }
            if (m_renderer != null)
            {
                Marshal.FinalReleaseComObject(m_renderer);
                m_renderer = null;
            }
            if (m_captureDevice != null)
            {
                Marshal.FinalReleaseComObject(m_captureDevice);
                m_captureDevice = null;
            }
            if (m_sampleGrabber != null)
            {
                Marshal.FinalReleaseComObject(m_sampleGrabber);
                m_sampleGrabber = null;
            }
            if (m_graph != null)
            {
                Marshal.FinalReleaseComObject(m_graph);
                m_graph = null;

                InvokeMediaClosed(new EventArgs());
            }
        }
    }
}