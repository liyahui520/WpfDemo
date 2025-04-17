using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DirectShowLib;
using Tools.App;
using Tools.Extend;
using WPFMediaKit.DirectShow.Controls;
using WPFMediaKit.DirectShow.MediaPlayers;
using WPFMediaKit.Manager;
using MediaState = WPFMediaKit.DirectShow.MediaPlayers.MediaState;

namespace WpfMain.Controlls
{
    /// <summary>
    /// UCMFVideo.xaml 的交互逻辑
    /// </summary>
    public partial class UCMFVideo : UserControl
    {
        private bool sliderDrag;
        private bool sliderMediaChange;
        public bool isStart = false;

        private static string videoFileName = Path.Combine(AppVideoConfig.TempPath, "{0}." + AppStatic.VideoConfig.VideoType);
        private static string wavFileName = Path.Combine(AppVideoConfig.TempPath, "{0}.wav");
        private double _width;
        private double _hight;
        private CameraRecorderManager Camra;
        //private VideoCaptureElement cameraCaptureElement;

        public UCMFVideo(double width, double hight)
        {
            InitializeComponent();
            SetCameraCaptureElementVisible(false);
            Camra = new CameraRecorderManager();
            _width = width;
            _hight = hight;
        }

        private void SetCameraCaptureElementVisible(bool visible)
        {
            btnStop_Click(null, null);
        }

        private void SetPlayButtons(bool playing)
        {

        }

        private void btnOpen_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog();
            var result = dlg.ShowDialog();
            if (result != true)
                return;
            SetCameraCaptureElementVisible(false);
            SetPlayButtons(true);
        }

        private void MediaUriElement_MediaFailed(object sender, WPFMediaKit.DirectShow.MediaPlayers.MediaFailedEventArgs e)
        {

        }


        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            SetPlayButtons(false);
        }

        private void btnPause_Click(object sender, RoutedEventArgs e)
        {
        }

        private void MediaUriPlayer_MediaPositionChanged(object sender, EventArgs e)
        {
            if (sliderDrag)
                return;
            this.Dispatcher.BeginInvoke(new Action(ChangeSlideValue), null);
        }

        private void ChangeSlideValue()
        {
            if (sliderDrag)
                return;

            sliderMediaChange = true;

            sliderMediaChange = false;
        }

        private void ChangeMediaPosition()
        {
            if (sliderMediaChange)
                return;

            sliderDrag = true;
            sliderDrag = false;
        }

        private void slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sliderMediaChange)
                return;

            this.Dispatcher.BeginInvoke(new Action(ChangeMediaPosition), null);
        }

        private void cobVideoSource_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            SetCameraCaptureElementVisible(true);
            Camra = new CameraRecorderManager();
            cameraCaptureElement.VideoCaptureDevice = Camra.initCapture();
            cameraCaptureElement.LoadedBehavior = MediaState.Play;
            cameraCaptureElement.Play();
            Camra.Camera = cameraCaptureElement;
        }


        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            cobVideoSource_SelectionChanged(null, null);
        }

        #region 录像，拍照功能

        /// <summary>
        /// 拍照
        /// </summary>
        /// <returns></returns>
        public System.Drawing.Image Capture()
        {
            if (!Camra.Camera.HasVideo)
            {

                HandyControl.Controls.MessageBox.Success($"摄像头未连接成功，无法拍照！", "系统提示");
                return null;
            }

            Size size = new Size(cameraCaptureElement.NaturalVideoWidth, cameraCaptureElement.NaturalVideoHeight);

            // 创建一个RenderTargetBitmap对象，用于捕获当前VideoCaptureElement的画面 
            RenderTargetBitmap bmp = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Default);

            // 为避免抓不全的情况，需要在Render之前调用Measure、Arrange 
            camp.Measure(size);
            camp.Arrange(new Rect(size));
            bmp.Render(camp);
            // 创建一个png编码器 
            BitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bmp));

            // 使用内存流保存编码后的图像数据 
            using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
            {
                encoder.Save(ms);
                byte[] captureData = ms.ToArray();
                return captureData.String2Image();
            }
        }



        public async Task Start()
        {
            if (Camra.Camera.HasVideo)
                await Camra.Start(string.Format(videoFileName, DateTime.Now.ToString("yyyyMMddHHmmss")));
            else
            {

                HandyControl.Controls.MessageBox.Success($"摄像头未连接成功，无法录像！", "系统提示");
            }
        }

        public void AutoWavRecorder(bool isOpen)
        {
            Camra.AutoWavRecorder(isOpen);
            isStart = true;
        }

        public string End()
        {
            var a = Camra.End();
            //cameraCaptureElement.Close();
            Camra.CamClose();
            cobVideoSource_SelectionChanged(null, null);
            return a;
        }

        public void Stop()
        {
            Camra.Pause();
        }

        #endregion

    }
}
