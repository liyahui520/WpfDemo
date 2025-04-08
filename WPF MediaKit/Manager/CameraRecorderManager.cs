using Record.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
using WPFMediaKit.DirectShow.MediaPlayers;
using System.Windows.Controls;

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
        /// 摄像头录制
        /// </summary>
        /// <param name="aviFilePath">视频路径</param>
        /// <param name="wavFilePath">录音路径</param>
        /// <param name="defaultFrameRate">默认帧数</param>
        /// <param name="isLoopingWav">是否录制声音(默认不录制)</param>
        /// <param name="videoCodec">视频格式</param>
        public CameraRecorderManager(string aviFilePath, string wavFilePath, int defaultFrameRate = 30, bool isLoopingWav = false, VideoCodec videoCodec = VideoCodec.MSMPEG4v3)
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
            DsDevice devs ;
            try
            {
                device =
                    MultimediaUtil.VideoInputDevices.FirstOrDefault(s => s.DevicePath == AppStatic.VideoConfig.VideoDecive);
                if (device == null)
                {
                    HandyControl.Controls.MessageBox.Error("未获取到摄像头信息", "系统提示");
                    return null;
                }

                devs= device;
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
        public virtual async Task Start()
        { 
            this.RecorderStatus = RecorderStatus.Start;
            isProcessingStream = true;
            ////设置回调,aforge会不断从这个回调推出图像数据
            //Camera.NewVideoSample += Camera_NewVideoSample;
            //Camera.LoadedBehavior = MediaState.Play;
            ////是否需要录制声音
            //if (wavRecorder != null)
            //    wavRecorder.Start();
            //Camera_NewVideoSample();
            await Task.Run(ProcessVideoStream);
        }

        // 视频流处理线程 
        private void ProcessVideoStream()
        {
            try
            {
                while (isProcessingStream)
                {
                    
                    Dispatcher.CurrentDispatcher.Invoke(() =>
                    {
                        var renderTarget = new RenderTargetBitmap(
                            (int)Camera.ActualWidth,
                            (int)Camera.ActualHeight,
                            96, 96, PixelFormats.Pbgra32);
                        renderTarget.Render(Camera);
                        if (renderTarget != null)
                        {
                            // 获取当前视频帧 
                            var frame = renderTarget;
                            try
                            {
                                var img = ((Bitmap)frame.Clone().ImageSourceToBitmap());
                                if (!File.Exists(AviFilePath))
                                {
                                    Console.WriteLine("地址：" + AviFilePath);
                                    VideoWriter.Open(AviFilePath, img.Width, img.Height, DEFAULT_FRAME_RATE, VideoCodec);
                                }

                                this.VideoWriter?.WriteVideoFrame(img);
                                if ((TotalFrame++) % 100 == 0)
                                {
                                    WindowApi.ClearMemory();
                                }
                                img.Dispose();
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e);
                            }
                            finally
                            {
                            }
                        }
                       
                    });

                    // 控制处理频率（约30fps）
                    //System.Threading.Thread.Sleep(30);
                }
            }
            catch (Exception ex)
            {
            }
        }

        // 获取当前视频帧数据 
        private BitmapSource GetCurrentVideoFrame()
        {
            try
            {
              
                Dispatcher.CurrentDispatcher.Invoke(() =>
                {
                    var renderTarget = new RenderTargetBitmap(
                        (int)Camera.ActualWidth,
                        (int)Camera.ActualHeight,
                        96, 96, PixelFormats.Pbgra32);
                    renderTarget.Render(Camera);
                    return renderTarget;
                });
            }
            catch (Exception e)
            {
                Console.WriteLine(e);

            }
            finally
            {
                
            }

            return null;
        }

        // 停止视频流处理 
        public void StopStream()
        {
            isProcessingStream = false; 
        }

        public void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // 确保停止所有处理 
            isProcessingStream = false;
            Camera.Close(); 
        }

        private void Camera_NewVideoSample()
        {
            try
            {
                string ffmpegPath = "ffmpeg.exe";  // 确保ffmpeg在程序目录下 
                string arguments = $"-f dshow -i video=\"{device.Name}\" -r 25 -vcodec libx264 -preset:v ultrafast -tune:v zerolatency \"test.mp4\"";

                ffmpegProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = arguments,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardInput = true
                    }
                };

                ffmpegProcess.Start();
                //if (this.RecorderStatus != RecorderStatus.Start) return;
                //var img = ((Bitmap)e.VideoFrame.Clone()); 

                //if (!File.Exists(AviFilePath))
                //{ 
                //    VideoWriter.Open(AviFilePath, img.Width, img.Height, DEFAULT_FRAME_RATE, VideoCodec);
                //}
                //this.VideoWriter?.WriteVideoFrame(img);
                ////aviWriter.AddFrame(img);
                ////每100帧回收一次虚拟内存
                //if ((TotalFrame++) % 100 == 0)
                //{
                //    WindowApi.ClearMemory();
                //}
            }
            catch (Exception ex)
            {
                // ignored
                Console.WriteLine(ex.Message);
            }
        } 
        /// <summary>
        /// 结束
        /// </summary>
        public virtual string End()
        {
            try
            {

                isProcessingStream = false;
                VideoWriter.Close();
                return AviFilePath;
                //this.RecorderStatus = RecorderStatus.End; 
                //VideoWriter.Close();
                ////是否需要录制声音
                //if (wavRecorder != null)
                //{
                //    wavRecorder.End();
                //    //获取和保存音频流到文件(桌面录制)
                //    AviManager aviManager = new AviManager(AviFilePath, true);
                //    aviManager.AddAudioStream(wavRecorder.WavFilePath, 0);
                //    aviManager.Close();
                //    //删除临时音频文件
                //    try
                //    {
                //        File.Delete(wavRecorder.WavFilePath);
                //    }
                //    catch
                //    {
                //    }
                //}
                ffmpegProcess?.StandardInput.WriteLine("q");
                ffmpegProcess?.WaitForExit();
                ffmpegProcess?.Close();
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
            this.RecorderStatus = RecorderStatus.Pause;
        }

    }
}
