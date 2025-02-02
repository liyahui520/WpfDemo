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
        private int BitRate;
        private int FrameRate;
        private Rectangle ScreenArea;
        protected VideoFileWriter VideoWriter;
        private ScreenCaptureStream VideoStreamer;
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
        /// <param name="defaultFrameRate">默认帧数</param>
        /// <param name="isLoopingWav">是否录制声音(默认不录制)</param>
        /// <param name="videoCodec">视频格式</param>
        public CameraRecorder(string aviFilePath, int defaultFrameRate = 10, bool isLoopingWav = false, VideoCodec videoCodec = VideoCodec.Raw)
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
            this.BitRate = 3000000;

            //是否需要录制声音
            if (isLoopingWav)
                wavRecorder = new WavRecorder(AppDomain.CurrentDomain.BaseDirectory + Guid.NewGuid().ToString().Replace("-", "") + ".wav");
        }

        public void SetAviFilePath(string aviFilePath) => AviFilePath = aviFilePath;

        public void AutoWavRecorder(bool open = true)
        {
            if (open)
            {
                wavRecorder = new WavRecorder(AppDomain.CurrentDomain.BaseDirectory + Guid.NewGuid().ToString().Replace("-", "") + ".wav");
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
                    Camera = new VideoCaptureDevice(devs[0].MonikerString);
                    //配置录像参数(宽,高,帧率,比特率等参数)VideoCapabilities这个属性会返回摄像头支持哪些配置,从这里面选一个赋值接即可,我选了第1个
                    Camera.VideoResolution = Camera.VideoCapabilities[0];

                    //打开摄像头
                    Camera.Start();
                    return Camera;
                }
                else
                {
                    MessageBox.Show("摄像头不存在!");
                    return null;
                }
            }
            catch
            {
                MessageBox.Show("摄像头不存在!");
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
            if (wavRecorder != null)
                this.wavRecorder.Start();
            this.RecorderStatus = RecorderStatus.Start;
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
        private void Camera_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            try
            {
                if (this.RecorderStatus != RecorderStatus.Start) return;
                var img = ((Bitmap)eventArgs.Frame.Clone());
                using (Graphics g = Graphics.FromImage(img))
                {
                    using (SolidBrush drawBrush = new SolidBrush(System.Drawing.Color.Yellow))
                    {
                        using (Font drawFont = new Font("Arial", 12, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel))
                        {
                            int xPos = 15;
                            int yPos = 10;
                            string drawDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                            g.DrawString(drawDate, drawFont, drawBrush, xPos, yPos);
                        }
                    }
                }

                if (!File.Exists(AviFilePath))
                    VideoWriter.Open(AviFilePath, img.Width, img.Height, DEFAULT_FRAME_RATE, VideoCodec);
                this.VideoWriter?.WriteVideoFrame(img);

                //每100帧回收一次虚拟内存
                if ((TotalFrame++) % 100 == 0)
                {
                    WindowApi.ClearMemory();
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
                Camera.NewFrame -= Camera_NewFrame;
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

                return AviFilePath;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return AviFilePath;
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
