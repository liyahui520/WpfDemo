using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Record;
using Record.AviFile;
using DirectShowLib;
using Tools.App;
using WPFMediaKit.DirectShow.Controls;
using System.Windows;

namespace WPFMediaKit.Manager
{
    public class CameraRecorderManager
    {
        #region Fields 
        protected int ScreenWidth;
        protected int ScreenHight;  

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


                device = MultimediaUtil.VideoInputDevices.First();
                if(!string.IsNullOrEmpty(AppStatic.VideoConfig.VideoDecive))
                    device = MultimediaUtil.VideoInputDevices.FirstOrDefault(s => s.DevicePath == AppStatic.VideoConfig.VideoDecive);
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
        public void Start(string path)
        {
            this.RecorderStatus = RecorderStatus.Start; 
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
