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
        public virtual void Start()
        {
            this.RecorderStatus = RecorderStatus.Start;
            isProcessingStream = true;
            //Camera.Start(AviFilePath);
            //Camera.LoadedBehavior = MediaState.Play;
            ////是否需要录制声音
            //if (wavRecorder != null)
            //    wavRecorder.Start();
            //Camera_NewVideoSample();
            //var bitmap = new Bitmap((int)Camera.Width, (int)Camera.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            //var bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.ImageLockMode.WriteOnly, bitmap.PixelFormat);
            //try
            //{
            //    Camera.VideoCaptureDevice.GetCurrentVideoFrame(out IntPtr frame);
            //    System.Runtime.InteropServices.Marshal.Copy(frame, 0, bitmapData.Scan0, (int)(Camera.Width * Camera.Height * 3));
            //}
            //finally
            //{
            //    bitmap.UnlockBits(bitmapData);
            //}
            //Dispatcher.CurrentDispatcher.Invoke(() => { Camera.Play(); });
            //var captureDevice = new VideoCaptureDevice(Camera.VideoCaptureDevice.DevicePath);
            //captureDevice.NewFrame += (sender, e) =>
            //{
            //    // 应用灰度滤镜 
            //    var grayFrame = Grayscale.CommonAlgorithms.BT709.Apply((Bitmap)e.Frame.Clone());
            //    Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
            //    {
            //        while (isProcessingStream)
            //        {
            //            // 获取当前视频帧 
            //            var frame = grayFrame;
            //            try
            //            {
            //                var img = ((Bitmap)frame.Clone());
            //                if (!File.Exists(AviFilePath))
            //                {
            //                    Console.WriteLine("地址：" + AviFilePath);
            //                    VideoWriter.Open(AviFilePath, img.Width, img.Height, DEFAULT_FRAME_RATE, VideoCodec);
            //                }

            //                this.VideoWriter?.WriteVideoFrame(img);
            //                if ((TotalFrame++) % 100 == 0)
            //                {
            //                    WindowApi.ClearMemory();
            //                }
            //                img.Dispose();
            //            }
            //            catch (Exception e)
            //            {
            //                Console.WriteLine(e);
            //            }
            //            finally
            //            {
            //            } 
            //            // 控制处理频率（约30fps）
            //            //System.Threading.Thread.Sleep(30);
            //        }
            //    }));
            //};
            //captureDevice.Start();

            //ProcessVideoStream();
            //await Task.Run(ProcessVideoStream);
        }

        private void Camera_NewVideoSample2(object sender, VideoSampleArgs e)
        {
            throw new NotImplementedException();
        }

        private void Camera_Initialized(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        private void Camera_NewVideoSample1(object sender, DirectShow.MediaPlayers.VideoSampleArgs e)
        {
            throw new NotImplementedException();
        }

        // 视频流处理线程 
        private void ProcessVideoStream()
        {
            try
            {
                //var renderTarget = new RenderTargetBitmap(
                //    (int)Camera.ActualWidth,
                //    (int)Camera.ActualHeight,
                //    96, 96, PixelFormats.Pbgra32);
                while (isProcessingStream)
                {
                    RenderTargetBitmap renderTarget = new RenderTargetBitmap((int)Camera.NaturalVideoWidth, (int)Camera.NaturalVideoHeight, 96, 96, PixelFormats.Rgb128Float);
                    // 为避免抓不全的情况，需要在Render之前调用Measure、Arrange 
                    //Camera.Measure(Camera.RenderSize);
                    //Camera.Arrange(new Rect(Camera.RenderSize));
                    renderTarget.Render(Camera);
                    //renderTarget.Render(Camera);
                    if (renderTarget != null)
                    {
                        // 获取当前视频帧 
                        var frame = renderTarget;
                        try
                        {
                            var img = ((Bitmap)frame.ImageSourceToBitmap());
                            if (!File.Exists(AviFilePath))
                            {
                                Console.WriteLine("地址：" + AviFilePath);
                                VideoWriter?.Open(AviFilePath, img.Width, img.Height, DEFAULT_FRAME_RATE, VideoCodec);
                            }

                            this.VideoWriter?.WriteVideoFrame(img, DateTime.Now.TimeOfDay);
                            if ((TotalFrame++) % 100 == 0)
                            {
                                WindowApi.ClearMemory();
                            }
                            img.Dispose();
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("录像异常");
                            Console.WriteLine(e.Message);
                        }
                        finally
                        {
                        }
                    }
                    System.Threading.Thread.Sleep(30);
                    // 控制处理频率（约30fps）
                    //System.Threading.Thread.Sleep(30);
                }
            }
            catch (Exception)
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
                string arguments = $"-f dshow -i video=\"{device.Name}\" -r 25 -vcodec libx264 -preset:v ultrafast -tune:v zerolatency \"{AviFilePath}\"";

                ffmpegProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = arguments,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardInput = true
                        //RedirectStandardOutput = true,
                        //RedirectStandardError = true
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
                foreach (Process process in Process.GetProcessesByName("ffmpeg"))
                {
                    process.Kill();
                }
                //Camera.Stop();
               // return AviFilePath;
                //isProcessingStream = false;
                //VideoWriter.Close();
                //return AviFilePath;
                ////this.RecorderStatus = RecorderStatus.End; 
                ////VideoWriter.Close();
                //////是否需要录制声音
                ////if (wavRecorder != null)
                ////{
                ////    wavRecorder.End();
                ////    //获取和保存音频流到文件(桌面录制)
                ////    AviManager aviManager = new AviManager(AviFilePath, true);
                ////    aviManager.AddAudioStream(wavRecorder.WavFilePath, 0);
                ////    aviManager.Close();
                ////    //删除临时音频文件
                ////    try
                ////    {
                ////        File.Delete(wavRecorder.WavFilePath);
                ////    }
                ////    catch
                ////    {
                ////    }
                ////}
                //ffmpegProcess?.StandardInput.WriteLine("q");
                //ffmpegProcess?.WaitForExit();
                //ffmpegProcess?.Close();
                //return AviFilePath;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                //return AviFilePath;
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
