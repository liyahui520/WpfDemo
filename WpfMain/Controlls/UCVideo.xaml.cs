using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using AForge.Video;
using AForge.Video.DirectShow;
using Record;
using System.IO;
using Image = System.Drawing.Image;
using AForge.Video.FFMPEG;
using Tools.App;
using Size = System.Windows.Size;
using System.Windows.Media;
using System.Windows.Forms;

namespace WpfMain.Controlls
{
    /// <summary>
    /// UCVideo.xaml 的交互逻辑
    /// </summary>
    public partial class UCVideo : System.Windows.Controls.UserControl
    {
        VideoCaptureDevice CaptureDevice;
        private string videoFileName = string.Empty; //视频文件名
        private string wavFileName = string.Empty; //音频文件名
        public bool isStart = false;
        private double _vw;
        private double _vh;
        #region 自定义事件
        public CameraRecorder recorder { get; set; }

        /// <summary>
        /// 设置属性
        /// </summary>
        /// <param name="value"></param>
        public void OnVideoSetCamera(VideoProcAmpProperty videoProcAmp, int value, VideoProcAmpFlags flag = VideoProcAmpFlags.Auto)
        {
            if (CaptureDevice != null && CaptureDevice.IsRunning)
            {
                CaptureDevice.SetCameraProcAmp(videoProcAmp, value, flag);
                CaptureDevice.Start();
            }
            else
            {
                System.Windows.MessageBox.Show("摄像头未获取到");
            }
        }

        #endregion



        static UCVideo()
        {
            if (Directory.Exists(AppVideoConfig.TempPath))
                Directory.CreateDirectory(AppVideoConfig.TempPath);
        }
        public UCVideo()
        {
            InitializeComponent();
        }
        public UCVideo(double width, double height)
        {
            InitializeComponent();
            _vw = width;
            _vh = height;
        }
        private void UCVideo_OnLoaded(object sender, RoutedEventArgs e)
        {

            videoFileName = Path.Combine(AppVideoConfig.TempPath, DateTime.Now.ToString("yyyyMMddHHmmss") + "." + AppStatic.VideoConfig.VideoType);
            wavFileName = Path.Combine(AppVideoConfig.TempPath, DateTime.Now.ToString("yyyyMMddHHmmss") + ".wav");
            recorder = new CameraRecorder(videoFileName, wavFileName, 30, true, VideoCodec.MSMPEG4v3);
            InitVideo();

        }

        public void InitVideo()
        {
            if (CaptureDevice != null)
            {
                if (CaptureDevice.IsRunning)
                {
                    CaptureDevice.SignalToStop(); // 请求停止摄像头数据接收
                    CaptureDevice.WaitForStop();  // 等待摄像头停止
                }

                CaptureDevice = null; // 重置videoSource对象
            }
            recorder.CamClose();
            if (!string.IsNullOrWhiteSpace(AppStatic.VideoConfig.VideoDecive))
            {
                CaptureDevice = recorder.initCapture(AppStatic.VideoConfig.VideoDecive);
                sourcePlayer.Width = CaptureDevice.VideoResolution.FrameSize.Width;
                sourcePlayer.Height = CaptureDevice.VideoResolution.FrameSize.Height;
                sourcePlayer.VideoSource = CaptureDevice;
                //CaptureDevice.NewFrame += new NewFrameEventHandler(video_NewFrame);
                button_Play_Click(this, null);
            }
            else
            {
                // 设定初始视频设备
                FilterInfoCollection videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                if (videoDevices.Count > 0)
                {   // 默认设备
                    //CaptureDevice = new VideoCaptureDevice(videoDevices[0].MonikerString);
                    CaptureDevice = recorder.initCapture(videoDevices[0].MonikerString);
                    sourcePlayer.Width = CaptureDevice.VideoResolution.FrameSize.Width;
                    sourcePlayer.Height = CaptureDevice.VideoResolution.FrameSize.Height;
                    sourcePlayer.VideoSource = CaptureDevice;
                    //CaptureDevice.NewFrame += new NewFrameEventHandler(video_NewFrame);
                    button_Play_Click(this, null);
                }
            }

            view.Width = _vw;
            view.Height = _vh;
            if (_vw / CaptureDevice.VideoResolution.FrameSize.Width < _vw / CaptureDevice.VideoResolution.FrameSize.Height)
                view.Height = (double)CaptureDevice.VideoResolution.FrameSize.Height / (double)CaptureDevice.VideoResolution.FrameSize.Width * _vw;
            else
                view.Width = (double)CaptureDevice.VideoResolution.FrameSize.Width / (double)CaptureDevice.VideoResolution.FrameSize.Height * _vh;






            // // 假设已经有VideoSourcePlayer实例 sourcePlayer 
            // // 获取显示控件的宽高  
            //var p= new Size(SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
            // this.Measure(p);
            // double viewWidth = this.Width;
            // double viewHeight = this.Height;

            // // 假设已经获取到视频的宽高 mWidth 和 mHeight 
            // double ratioView = viewWidth / viewHeight;
            // double ratio = Width / Height;

            // if (ratioView - ratio < 0)
            // {
            //     // 屏幕宽高比相比视频的宽高比更小，表示视频是过于宽了 
            //     // 那就以宽为基准，宽最大化 
            //     sourcePlayer.Width = int.Parse(viewWidth.ToString());
            //     // 高进行等比例缩放 
            //     sourcePlayer.Height = int.Parse((viewWidth / ratio).ToString());
            // }
            // else
            // {
            //     // 以高为基准，高铺满全屏 
            //     sourcePlayer.Height = int.Parse(viewHeight.ToString());
            //     // 等比例计算出横向区域，多出父控件的范围将被裁剪 
            //     sourcePlayer.Width = int.Parse((viewHeight * ratio).ToString());
            // }
            //VideoCapabilities[] capabilities = CaptureDevice.VideoCapabilities;
            //if (capabilities.Length > 0)
            //{
            //    // 3. 设置分辨率（示例选择第一个支持的分辨率）
            //    videoDevice.VideoResolution = capabilities[0];

            //    // 4. 启动摄像头并获取当前分辨率 
            //    videoDevice.Start();
            //    Console.WriteLine($"当前分辨率: {CaptureDevice.VideoResolution.FrameSize.Width}x{CaptureDevice.VideoResolution.FrameSize.Height}");
            //}
        }


        //重新设置视频保存路径
        public void SetAviFilePath()
        {
            videoFileName = DateTime.Now.ToString("yyyyMMddHHmmss") + "." + AppStatic.VideoConfig.VideoType;
            recorder.SetAviFilePath(AppVideoConfig.TempPath + videoFileName);
        }

        public void AutoWavRecorder(bool isOpen)
        {
            recorder?.AutoWavRecorder(wavFileName, isOpen);
        }

        public void Start()
        {
            recorder.Start();
            isStart = true;
        }

        public void Stop()
        {
            recorder.Pause();
            isStart = false;
        }

        public string End()
        {

            isStart = false;
            return recorder.End();
        }

        public void Close()
        {
            if (CaptureDevice != null)
            {
                if (CaptureDevice.IsRunning)
                {
                    CaptureDevice.SignalToStop(); // 请求停止摄像头数据接收
                    CaptureDevice.WaitForStop();  // 等待摄像头停止
                }

                CaptureDevice = null; // 重置videoSource对象
            }
            recorder.CamClose();
        }


        /// <summary>
        /// 拍照
        /// </summary>
        public Image Capture()
        {
            if (sourcePlayer.VideoSource == null)
            {
                throw new Exception("请检查摄像头是否连接正常");
            }
            else
            {
                return sourcePlayer.GetCurrentVideoFrame();
            }
        }

        /// <summary>
        /// 录像事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="image"></param>
        private void videoSourcePlayer1_NewFrame(object sender, ref Bitmap image)
        {
            ////录像
            //using (Graphics g = Graphics.FromImage(image))
            //{
            //    using (SolidBrush drawBrush = new SolidBrush(System.Drawing.Color.Yellow))
            //    {
            //        using (Font drawFont = new Font("Arial", 18, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel))
            //        {
            //            int xPos = 15;
            //            int yPos = 10;
            //            string drawDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            //            g.DrawString(drawDate, drawFont, drawBrush, xPos, yPos);

            //        }
            //        if (isStart)
            //        {
            //            using (SolidBrush drawBrush1 = new SolidBrush(System.Drawing.Color.Crimson))
            //            {
            //                using (Font drawFont = new Font("Arial", 18, System.Drawing.FontStyle.Regular, GraphicsUnit.Pixel))
            //                {
            //                    int xPos = 15;
            //                    int yPos = 35;
            //                    g.DrawString("正在录像中", drawFont, drawBrush1, xPos, yPos);

            //                }
            //            }

            //        }
            //    }
            //} 

        }

        // 新帧事件处理
        private void video_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            // 处理新帧
            Bitmap bitmap = (Bitmap)eventArgs.Frame.Clone();
            // ... 显示或处理bitmap
        }



        private void button_Play_Click(object sender, RoutedEventArgs e)
        {
            SetAviFilePath();
            sourcePlayer.Start();
        }

        private void button_Capture_Click(object sender, RoutedEventArgs e)
        {
            // 判断视频设备是否开启
            if (sourcePlayer.IsRunning)
            {   // 进行拍照
                for (Int32 i = 1; i <= 4; i++)
                {
                    object box = this.FindName("fingerPictureBox" + i);
                    //if (box is FingerPictureBox)
                    //{
                    //    if ((box as FingerPictureBox).ActiveImage == (box as FingerPictureBox).InitialImage)
                    //    {   // 更新图像
                    //        (box as FingerPictureBox).ActiveImage = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    //            sourcePlayer.GetCurrentVideoFrame().GetHbitmap(),
                    //            IntPtr.Zero,
                    //            Int32Rect.Empty,
                    //            BitmapSizeOptions.FromEmptyOptions());
                    //        break;
                    //    }
                    //}
                }
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (sourcePlayer.IsRunning)
            {   // 停止视频
                sourcePlayer.SignalToStop();
                sourcePlayer.WaitForStop();
            }
        }
    }
}
