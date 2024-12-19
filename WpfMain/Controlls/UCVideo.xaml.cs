using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using AForge.Video;
using AForge.Video.DirectShow;

namespace WpfMain.Controlls
{
    /// <summary>
    /// UCVideo.xaml 的交互逻辑
    /// </summary>
    public partial class UCVideo : UserControl
    {
        BitmapSource ImagePlay;
        BitmapSource ImageStop;
        VideoCaptureDevice CaptureDevice;

        #region 自定义事件


        /// <summary>
        /// 设置属性
        /// </summary>
        /// <param name="value"></param>
        public void OnVideoSetCamera(VideoProcAmpProperty videoProcAmp, int value, VideoProcAmpFlags flag=VideoProcAmpFlags.Auto)
        {
            if (CaptureDevice != null && CaptureDevice.IsRunning)
            {
                CaptureDevice.SetCameraProcAmp(videoProcAmp, value, flag);
                CaptureDevice.Start();
            }
            else
            {
                MessageBox.Show("摄像头未获取到");
            }
        }

        #endregion

        public UCVideo()
        {
            InitializeComponent();
        }

        private void UCVideo_OnLoaded(object sender, RoutedEventArgs e)
        {
            // 设定初始视频设备
            FilterInfoCollection videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            if (videoDevices.Count > 0)
            {   // 默认设备
                CaptureDevice = new VideoCaptureDevice(videoDevices[0].MonikerString);

                sourcePlayer.VideoSource = CaptureDevice;
                CaptureDevice.NewFrame += new NewFrameEventHandler(video_NewFrame);
                button_Play_Click(this, null);
            }
            else
            {
            }

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
            sourcePlayer.Start();
            //if (image_Play.Source == ImagePlay)
            //{   // 开启视频
            //    sourcePlayer.Start();
            //    if (sourcePlayer.IsRunning)
            //    {
            //        // 改变按钮为“停止”状态
            //        image_Play.Source = ImageStop;
            //        label_Play.Content = "停止";

            //        // 允许拍照
            //        button_Capture.IsEnabled = true;
            //    }
            //}
            //else
            //{
            //    if (sourcePlayer.IsRunning)
            //    {   // 停止视频
            //        sourcePlayer.SignalToStop();
            //        sourcePlayer.WaitForStop();

            //        // 改变按钮为“开始”状态
            //        image_Play.Source = ImagePlay;
            //        label_Play.Content = "开启摄像头"; ;

            //        // 关闭拍照
            //        button_Capture.IsEnabled = false;
            //    }
            //}
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
