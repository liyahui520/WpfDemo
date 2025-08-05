using AForge.Video;
using AForge.Video.DirectShow;
using AForge.Video.FFMPEG;
using Record.AviFile;
using Record.Extension;
using Record.Interfaces;
using MiniScreenRecorder.AviFile;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static AForge.Robotics.Surveyor.SVS;
using AForge.Video.VFW;
using Tools.App;
namespace Record
{
    /// <summary>
    /// 录制摄像头
    /// </summary>
    public class CameraRecorder : IRecorder
    {
        #region Fields
        private int DEFAULT_FRAME_RATE = 10;
        protected int ScreenWidth;
        protected int ScreenHight; 
        private  int FrameRate=10;
        private Rectangle ScreenArea;
        protected VideoFileWriter VideoWriter;
        private ScreenCaptureStream VideoStreamer = null;
        private FolderBrowserDialog FolderBrowser; 
        private VideoCodec VideoCodec;
        /// <summary>
        /// 操作摄像头
        /// </summary>
        public VideoCaptureDevice Camera = null;

        /// <summary>
        /// 视频路径
        /// </summary>
        private string AviFilePath { get; set; }
        #endregion

        /// <summary>
        /// 录制声音
        /// </summary>
        private WavRecorder wavRecorder { get; set; }

        /// <summary>
        /// 总帧数
        /// </summary>
        private int TotalFrame { get; set; }

        /// <summary>
        /// 录制状态
        /// </summary>
        public RecorderStatus RecorderStatus { get; set; }


        /// <summary>
        /// 摄像头录制
        /// </summary>
        /// <param name="aviFilePath">视频路径</param>
        /// <param name="wavFilePath">录音路径</param>
        /// <param name="defaultFrameRate">默认帧数</param>
        /// <param name="isLoopingWav">是否录制声音(默认不录制)</param>
        /// <param name="videoCodec">视频格式</param>
        public CameraRecorder(string aviFilePath, string wavFilePath, int defaultFrameRate = 30, bool isLoopingWav = false, VideoCodec videoCodec = VideoCodec.Raw)
        {
            this.AviFilePath = aviFilePath;
            this.DEFAULT_FRAME_RATE = defaultFrameRate;
            this.ScreenWidth = SystemInformation.VirtualScreen.Width;
            this.ScreenHight = SystemInformation.VirtualScreen.Height;
            this.FrameRate = DEFAULT_FRAME_RATE;
            this.ScreenArea = Rectangle.Empty;
            this.VideoWriter = new VideoFileWriter();
            this.FolderBrowser = new FolderBrowserDialog();
            this.VideoCodec = videoCodec; 
            //是否需要录制声音
            if (isLoopingWav)
                wavRecorder = new WavRecorder(wavFilePath);
        }

        public void SetAviFilePath(string aviFilePath)
        {
            AviFilePath = aviFilePath;
        }

        public void AutoWavRecorder(string wavFilePath, bool open = true)
        {
            if (open)
            {
                wavRecorder = new WavRecorder(wavFilePath);
            }
            else
            {
                wavRecorder = null;
            }
        }

        public void CamClose()
        {
            if (Camera != null)
            {
                if (Camera.IsRunning)
                {
                    Camera.SignalToStop(); // 请求停止摄像头数据接收
                    Camera.WaitForStop();  // 等待摄像头停止
                }

                Camera = null; // 重置videoSource对象
            }
        }

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="monikerString"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public virtual VideoCaptureDevice initCapture(string monikerString)
        {
            try
            {
                //获取摄像头列表
                var devs = new FilterInfoCollection(FilterCategory.VideoInputDevice);

                if (devs.Count != 0)
                {
                    string cname = devs[0].MonikerString;

                    if (!string.IsNullOrEmpty(AppStatic.VideoConfig?.VideoDecive))
                    {
                        foreach (FilterInfo dev in devs)
                            if (dev.MonikerString == AppStatic.VideoConfig.VideoDecive)
                                cname = dev.MonikerString;
                    }
                    Camera = new VideoCaptureDevice(cname);
                    //配置录像参数(宽,高,帧率,比特率等参数)VideoCapabilities这个属性会返回摄像头支持哪些配置,从这里面选一个赋值接即可,我选了第1个
                    Camera.VideoResolution = Camera.VideoCapabilities[0];

                    //打开摄像头
                    Camera.Start();
                    return Camera;
                }
                else
                {
                    HandyControl.Controls.MessageBox.Error("摄像头不存在!","系统提示");
                    return null;
                }
            }
            catch
            {
                HandyControl.Controls.MessageBox.Error("摄像头不存在!", "系统提示");
                return null;
            }
        }
        /// <summary>
        /// 开始
        /// </summary>
        /// <param name="frameEventHandler">每帧回调（默认不需要填）</param>
        public virtual void Start(NewFrameEventHandler frameEventHandler = null)
        {
            //继续播放
            //if (this.RecorderStatus == RecorderStatus.Pause)
            //{
            //    //this.VideoStreamer.Start();
            //    if (wavRecorder != null)
            //        this.wavRecorder.Start();

            //    this.RecorderStatus = RecorderStatus.Start;
            //    return;
            //}
            //if (wavRecorder != null)
            //    this.wavRecorder.Start();
            this.RecorderStatus = RecorderStatus.Start;
            //// 初始化AVIWriter并设置压缩编码器（例如Motion JPEG）
            //aviWriter = new AVIWriter("MJPG"); // 使用Motion JPEG编码器
            //aviWriter.FrameRate = 30; // 设置帧率
            //aviWriter.Quality = 80;   // 设置压缩质量（0-100）
            //// 确保分辨率与摄像头实际输出一致
            //var caps = Camera.VideoCapabilities;
            //var selectedCap = caps.FirstOrDefault(c => c.FrameSize.Width == 640 && c.FrameSize.Height == 480);

            //if (selectedCap != null)
            //{
            //    aviWriter.Open(AviFilePath, selectedCap.FrameSize.Width, selectedCap.FrameSize.Height);
            //    aviWriter.FrameRate = selectedCap.AverageFrameRate;
            //}
            //else
            //{
            //    // 默认参数
            //    aviWriter.Open(AviFilePath, 640, 480);
            //    aviWriter.FrameRate = 30;
            //}
            //if (!File.Exists(AviFilePath))
            //    File.Create(AviFilePath);
            //aviWriter.Open(AviFilePath, 640, 480);
            //设置回调,aforge会不断从这个回调推出图像数据
            Camera.NewFrame += Camera_NewFrame;
            //是否需要录制声音
            if (wavRecorder != null)
                wavRecorder.Start();
        }

        /// <summary>
        /// 摄像头回调
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="eventArgs"></param>
        private async void Camera_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            try
            {
                if (this.RecorderStatus != RecorderStatus.Start) return;
                var img = ((Bitmap)eventArgs.Frame.Clone());
                //using (Graphics g = Graphics.FromImage(img))
                //{
                //    using (SolidBrush drawBrush = new SolidBrush(System.Drawing.Color.Yellow))
                //    {
                //        using (Font drawFont = new Font("Arial", 12, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel))
                //        {
                //            int xPos = 15;
                //            int yPos = 10;
                //            string drawDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                //            g.DrawString(drawDate, drawFont, drawBrush, xPos, yPos);
                //        }
                //    }
                //}

                if (!File.Exists(AviFilePath))
                {
                    var caps = Camera.VideoCapabilities;
                    var selectedCap = caps.FirstOrDefault(c => c.FrameSize.Width == 640 && c.FrameSize.Height == 480);
                    //    aviWriter.Open(AviFilePath, img.Width, img.Height);
                    if (selectedCap != null)
                        VideoWriter.Open(AviFilePath, img.Width, img.Height, DEFAULT_FRAME_RATE, VideoCodec);
                    else
                    {
                        VideoWriter.Open(AviFilePath, img.Width, img.Height, DEFAULT_FRAME_RATE, VideoCodec);
                    }
                }
                this.VideoWriter?.WriteVideoFrame(img);
                //aviWriter.AddFrame(img);
                //每100帧回收一次虚拟内存
                if ((TotalFrame++) % 100 == 0)
                {
                    await WindowApi.ClearMemory();
                }
            }
            catch
            {

            }

        }

        /// <summary>
        /// 结束
        /// </summary>
        public virtual string End()
        {
            try
            {
                this.RecorderStatus = RecorderStatus.End;
                //设置回调,aforge会不断从这个回调推出图像数据
                //Camera.NewFrame -= Camera_NewFrame;
                //// 释放资源
                //aviWriter.Close();
                //aviWriter.Dispose();
                VideoStreamer?.Stop();
                VideoWriter.Close();
                //是否需要录制声音
                if (wavRecorder != null)
                {
                    wavRecorder.End();
                    //获取和保存音频流到文件(桌面录制)
                    AviManager aviManager = new AviManager(AviFilePath, true);
                    aviManager.AddAudioStream(wavRecorder.WavFilePath, 0);
                    aviManager.Close();
                    //删除临时音频文件
                    try
                    {
                        File.Delete(wavRecorder.WavFilePath);
                    }
                    catch
                    {
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
            }

            return AviFilePath;
        }

        /// <summary>
        /// 暂停
        /// </summary>
        public void Pause()
        {
            this.VideoStreamer?.Stop();
            this.RecorderStatus = RecorderStatus.Pause;
        }

    }
}
