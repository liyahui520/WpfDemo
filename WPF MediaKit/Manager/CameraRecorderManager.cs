using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Record;
using Record.AviFile;
using AForge.Video.FFMPEG;
using AForge.Video.VFW;
using DirectShowLib;
using Record.Extension;
using Tools.App;
using Tools.Extend;
using WPFMediaKit.DirectShow.Controls;
using AForge.Video.DirectShow;
using WPFMediaKit.DirectShow.MediaPlayers;
using System.Threading;
using System.Windows.Media.Media3D;

namespace WPFMediaKit.Manager
{
    public class CameraRecorderManager
    {
        #region Fields
        private int DEFAULT_FRAME_RATE = 10;
        protected int ScreenWidth;
        protected int ScreenHight;
        private int BitRate;
        private int FrameRate;
        private Rectangle ScreenArea;
        protected VideoFileWriter VideoWriter;
        private FolderBrowserDialog FolderBrowser;
        private AVIWriter aviWriter;
        private VideoCodec VideoCodec;

        private bool isProcessingStream = false;

        private Process ffmpegProcess;

        private DsDevice device;

        /// <summary>
        /// 操作摄像头
        /// </summary> 
        public VideoCaptureElement Camera = null;

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
        /// 是否开启录音
        /// </summary>
        private bool IsOpen { get; set; } = false;


        /// <summary>
        /// 摄像头录制
        /// </summary>
        public CameraRecorderManager()
        {
        }

        public void AutoWavRecorder(bool open = true)
        {
            IsOpen = open;
        }

        public void CamClose()
        { 

            Camera.Close();
        }

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="monikerString"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public DsDevice initCapture()
        {
            DsDevice devs;
            try
            {
                device =
                    MultimediaUtil.VideoInputDevices.FirstOrDefault(s => s.DevicePath == AppStatic.VideoConfig.VideoDecive);
                if (device == null)
                {
                    HandyControl.Controls.MessageBox.Error("未获取到摄像头信息", "系统提示");
                    return null;
                }

                devs = device;
            }
            catch
            {
                HandyControl.Controls.MessageBox.Error("摄像头不存在!", "系统提示");
                return null;
            }
            return devs;
        }


        /// <summary>
        /// 开始
        /// </summary> 
        public async Task Start(string path)
        {
            this.RecorderStatus = RecorderStatus.Start;
            isProcessingStream = true;
            AviFilePath = path;
            Camera.Start(AviFilePath, IsOpen); 
        } 
         
        /// <summary>
        /// 结束
        /// </summary>
        public virtual string End()
        {
            try
            { 
                isProcessingStream = false;
                Camera.Stop(); 
                return AviFilePath; 
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
            this.RecorderStatus = RecorderStatus.Pause;
        }

    }
}
