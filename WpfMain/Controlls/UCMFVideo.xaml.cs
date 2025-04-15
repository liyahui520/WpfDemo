using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
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

        public UCMFVideo(double width, double hight)
        {
            InitializeComponent();
            SetCameraCaptureElementVisible(false);
            Camra = new CameraRecorderManager(string.Format(videoFileName, DateTime.Now.ToString("yyyyMMddHHmmss")), string.Format(wavFileName, DateTime.Now.ToString("yyyyMMddHHmmss")));
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
            cameraCaptureElement.VideoCaptureDevice = Camra.initCapture();
            cameraCaptureElement.OutputFileName = string.Format(videoFileName, DateTime.Now.ToString("yyyyMMddHHmmss"));
            cameraCaptureElement.LoadedBehavior = MediaState.Play;
            cameraCaptureElement.NewVideoSample += CameraCaptureElement_NewVideoSample1;

            cameraCaptureElement.Play();
            Camra.Camera = cameraCaptureElement;



            //var _vh = cameraCaptureElement.NaturalVideoHeight;
            //var _vd = cameraCaptureElement.NaturalVideoWidth;
            //if (_vd > _width)
            //{
            //    cameraCaptureElement.Width = _vd;
            //    cameraCaptureElement.Height = _vh * ((_vd - _width) / _vd);
            //}
            //else
            //{
            //    cameraCaptureElement.Width = _vd * ((_vh - _hight) / _vd); ;
            //    cameraCaptureElement.Height = _vh;
            //}
        }

        private void CameraCaptureElement_NewVideoSample1(object sender, VideoSampleArgs e)
        {
            throw new NotImplementedException();
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
            System.Drawing.Image img = null;
            // 为避免抓不全的情况，需要在Render之前调用Measure、Arrange 
            cameraCaptureElement.Measure(cameraCaptureElement.RenderSize);
            cameraCaptureElement.Arrange(new Rect(cameraCaptureElement.RenderSize));
            // 创建一个RenderTargetBitmap对象，用于捕获当前VideoCaptureElement的画面 
            RenderTargetBitmap bmp = new RenderTargetBitmap((int)cameraCaptureElement.ActualWidth, (int)cameraCaptureElement.ActualHeight, 96, 96, PixelFormats.Default);
            bmp.Render(cameraCaptureElement);

            var _vh = cameraCaptureElement.NaturalVideoHeight;
            var _vd = cameraCaptureElement.NaturalVideoWidth;
            double _vhX = 0.00;
            double _vdX = 0.00;
            double cropWidth = 0.0;
            double cropX = 0.0;
            double cropHight = 0.0;
            double cropH = 0.0;
            double yB = 0.0;

            //if (_vd > _width)
            //{
            //    _vdX = ((_vd - _width) / _vd);
            //    //cropHight = bmp.Height * (_vhX / 2.0);
            //    //cropH = bmp.Height * (_vhX / 2.0);
            //    //cropRect = new Int32Rect((int)cropH, 0, (int)_vd, (int)cropHight); 
            //}
            //else if (_vd <= _width)
            //{
            //    _vdX = ((_width - _vd) / _width);
            //}

            //if (_vh > _hight)
            //{
            //    _vhX = (_vh - _hight) / _vh;
            //}
            //else if (_vh <= _hight)
            //{
            //    _vhX = (_hight - _vh) / _hight;
            //}
            var cropRect = new Int32Rect();
            if (_vh > _vd)
            {
                yB = _vd * 1.0 / _vh;
                // 自动计算有效区域（示例：中心区域90%）
                cropRect = new Int32Rect(
                    (int)(bmp.Width * (1- yB)/2.0),  // 左裁剪5%
                    (int)(0),// 上裁剪5%
                    (int)(bmp.Width * yB),   // 保留宽度90%
                    (int)(bmp.Height)  // 保留高度90%
                );
            }
            else
            {
                yB = _vh * 1.0 / _vd;
                var h = (cameraCaptureElement.ActualWidth * yB) > cameraCaptureElement.ActualHeight
                    ? ((cameraCaptureElement.ActualWidth * yB) - cameraCaptureElement.ActualHeight) / cameraCaptureElement.ActualHeight / 2.6
                    : ((cameraCaptureElement.ActualHeight - (cameraCaptureElement.ActualWidth * yB)) / (cameraCaptureElement.ActualWidth * yB) / 2.6);
                // 自动计算有效区域（示例：中心区域90%）
                cropRect = new Int32Rect(
                    (int)(0),  // 左裁剪5%
                    (int)((h) * cameraCaptureElement.ActualHeight),// 上裁剪5%
                    (int)(cameraCaptureElement.ActualWidth),   // 保留宽度90%
                    (int)(cameraCaptureElement.ActualWidth * yB)  // 保留高度90%
                );
            }

          


            // 应用裁剪 
            var croppedBmp = new CroppedBitmap(bmp, cropRect);
            // 创建一个JPEG编码器 
            BitmapEncoder encoder = new JpegBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(croppedBmp));

            // 使用内存流保存编码后的图像数据 
            using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
            {
                encoder.Save(ms);
                byte[] captureData = ms.ToArray();
                img = captureData.String2Image();
            }

            return img;
        }



        public void Start()
        {
            Camra.SetAviFilePath(string.Format(videoFileName, DateTime.Now.ToString("yyyyMMddHHmmss")));
            Camra.Start();
        }

        private void CameraCaptureElement_NewVideoSample(object sender, VideoSampleArgs e)
        {
        }


        public void AutoWavRecorder(bool isOpen)
        {
            Camra.AutoWavRecorder(string.Format(wavFileName, DateTime.Now.ToString("yyyyMMddHHmmss")), isOpen);
            isStart = true;
        }

        public string End()
        {
            return Camra.End();
        }

        public void Stop()
        {
            Camra.Pause();
        }

        #endregion

    }
}
