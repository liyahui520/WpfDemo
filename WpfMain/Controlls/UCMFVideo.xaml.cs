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
using System.Windows.Threading;
using System.Windows.Input;
using Newtonsoft.Json;
using System.Windows.Media.Media3D;
using System.Diagnostics;
using WPFMediaKit;

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

        private static string videoFileName = Path.Combine(AppVideoConfig.TempPath, "{0}." + AppStatic.VideoConfig.VideoType); // 视频文件名格式化字符串
        private double _width;
        private double _hight;
        private CameraRecorderManager Camra;
        private Process ffmpegProcess;
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
            try
            {

                SetCameraCaptureElementVisible(true);
                Camra = new CameraRecorderManager();
                cameraCaptureElement.VideoCaptureDevice = Camra.initCapture();
                if (cameraCaptureElement.VideoCaptureDevice == null)
                    return;

                cameraCaptureElement.LoadedBehavior = MediaState.Play;

                cameraCaptureElement.NewVideoSample += Camera_NewVideoSample;
                Camra.Camera = cameraCaptureElement;
                Camra.Camera.ManipulationCompleted += Camera_ManipulationCompleted;
                Camra.Camera.ManipulationDelta += Camera_ManipulationDelta;
                Camra.Camera.ManipulationInertiaStarting += Camera_ManipulationInertiaStarting;
                Camra.Camera.MediaFailed += Camera_MediaFailed;
                Camra.Camera.NewVideoSample += Camera_NewVideoSample;
                Camra.Camera.Play();
            }
            catch(WPFMediaKitException ex)
            {
                throw new Exception("摄像头初始化失败，请检查摄像头是否连接或驱动是否安装正确。", ex);
            }
        }

        private void Camera_NewVideoSample(object sender, VideoSampleArgs e)
        {
        }



        private void Camera_MediaFailed(object sender, MediaFailedEventArgs e)
        {
            LogUtil.Error($"Camera_MediaFailed--" + e.Message);
        }

        private void Camera_ManipulationInertiaStarting(object sender, ManipulationInertiaStartingEventArgs e)
        {
            LogUtil.Error($"Camera_ManipulationInertiaStarting--" + JsonConvert.SerializeObject(e.Device));
        }

        private void Camera_ManipulationDelta(object sender, ManipulationDeltaEventArgs e)
        {
            LogUtil.Error($"Camera_ManipulationDelta--" + JsonConvert.SerializeObject(e.Device));
        }

        private void Camera_ManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
        {
            LogUtil.Error($"Camera_ManipulationCompleted--" + JsonConvert.SerializeObject(e.Device));
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
            try
            {
                if (!Camra.Camera.HasVideo)
                {

                    HandyControl.Controls.MessageBox.Success($"摄像头未连接成功，无法拍照！", "系统提示");
                    return null;
                }
                LogGpuAccelerationStatus();
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
            catch (Exception ex)
            {
                LogUtil.Error(ex.Message);
                return null;
            }
            finally
            {
                cobVideoSource_SelectionChanged(null, null);
            }
        }

        private void LogGpuAccelerationStatus()
        {
            try
            {
                // 获取当前渲染模式
                var renderingTier = (RenderCapability.Tier >> 16) & 0xFF;
                string accelerationLevel;
                switch (renderingTier)
                {
                    case 0:
                        accelerationLevel = "软件渲染（无GPU加速）";
                        break;
                    case 1:
                        accelerationLevel = "部分GPU加速（基本图形加速）";
                        break;
                    case 2:
                        accelerationLevel = "完全GPU加速（推荐）";
                        break;
                    default:
                        accelerationLevel = "未知";
                        break;
                }

                LogUtil.Info($"当前GPU加速级别: {renderingTier} - {accelerationLevel}");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"无法检测GPU加速状态: {ex.Message}");
            }
        }

        public void Start()
        {
            //LogGpuAccelerationStatus();
            //var mediaType = new AMMediaType();
            cameraCaptureElement.StartRecording(
               string.Format(videoFileName, DateTime.Now.ToString("yyyyMMddHHmmss"))
                );
            ////StartFFmpegRecording();
            //if (Camra.Camera.HasVideo)
            //    await Camra.Start(string.Format(videoFileName, DateTime.Now.ToString("yyyyMMddHHmmss")));
            //else
            //{

            //    HandyControl.Controls.MessageBox.Success($"摄像头未连接成功，无法录像！", "系统提示");
            //}
        }

        public void AutoWavRecorder(bool isOpen)
        {
            Camra.AutoWavRecorder(isOpen);
            isStart = true;
        }

        public  string End()
        {
            // 停止录制
            var a =cameraCaptureElement.StopRecording();
            //var a = Camra.End();
            ////cameraCaptureElement.Close();
            //Camra.CamClose();
            ////Camra.Camera.VideoCaptureDevice?.Dispose(); 
            //Camra.Camera.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() =>
            //{
            //    cameraCaptureElement.Close();
            //}));
            //Camra.Pause();
            //cobVideoSource_SelectionChanged(null, null);
            Task.Delay(1000); // 等待1秒，确保文件写入完成
            var _cancellationTokenSource = new CancellationTokenSource();
            if (IsFileInUse(a))
            {
                // 异步等待文件释放
                bool fileReleased = WaitForFileReleaseAsync(a, _cancellationTokenSource.Token);
                if (fileReleased)
                    return a;
            } 
            return a;
        }

        /// <summary>
        /// 检查文件是否被占用
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>如果被占用返回true，否则返回false</returns>
        private bool IsFileInUse(string filePath)
        {
            try
            {
                using (var stream = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    return false;
                }
            }
            catch (IOException)
            {
                // 文件被占用时会抛出IOException
                return true;
            }
            catch (Exception)
            {
                // 其他错误也视为文件不可用
                return true;
            }
        }

        /// <summary>
        /// 异步等待文件释放
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>如果文件被释放返回true，否则返回false</returns>
        private bool WaitForFileReleaseAsync(string filePath, CancellationToken cancellationToken)
        {
            const int checkIntervalMs = 2000; // 检查间隔，2秒

            while (!cancellationToken.IsCancellationRequested)
            {
                if (!IsFileInUse(filePath))
                {
                    return true;
                }

                // 等待指定时间或直到取消请求
                try
                {
                     Task.Delay(checkIntervalMs, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    // 取消请求，退出循环
                    break;
                }
            }

            return false;
        }

        public void Stop()
        {
            Camra.Pause();
        }

        public void Close()
        {
            Camra.CamClose();
            cameraCaptureElement.Close();
            cobVideoSource_SelectionChanged(null, null);
        }
        #endregion 

        public void CamReLoad()
        {
            cameraCaptureElement.ReLoad();
        }

        private void FFmpegProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                // 记录FFmpeg输出（可用于调试）
                Debug.WriteLine($"FFmpeg: {e.Data}");
            }
        }

        private void StopRecording()
        {
            try
            {
                if (ffmpegProcess != null && !ffmpegProcess.HasExited)
                {
                    // 优雅地停止FFmpeg
                    ffmpegProcess.StandardInput.WriteLine("q"); // 发送'q'命令停止录制
                    ffmpegProcess.WaitForExit(2000); // 等待2秒

                    if (!ffmpegProcess.HasExited)
                    {
                        ffmpegProcess.Kill(); // 强制终止
                    }
                }

                MessageBox.Show("录制已停止");
                (FindName("StartButton") as System.Windows.Controls.Button).Content = "开始录制";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"停止录制时出错: {ex.Message}");
            }
        }
    }
}
