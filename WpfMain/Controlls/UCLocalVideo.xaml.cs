using AForge.Video;
using AForge.Video.DirectShow;
using AForge.Video.FFMPEG;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace WpfMain.Controlls
{
    /// <summary>
    /// UCLocalVideo.xaml 的交互逻辑
    /// </summary>
    public partial class UCLocalVideo
    {
        private VideoFileSource videoSource;
        private VideoFileReader videoReader;
        private DispatcherTimer progressTimer;
        private bool isDraggingProgress;

        public UCLocalVideo(string fileName)
        {
            InitializeComponent();
            InitializePlayer();
            LoadVideo(fileName);
        }
        private void InitializePlayer()
        {
            progressTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            progressTimer.Tick += UpdateProgress;
        }

        private void LoadVideo(string path)
        {
            loadingOverlay.Visibility = Visibility.Visible;

            //videoSource?.SignalToStop();
            videoSource = new VideoFileSource(path);
            videoReader = new VideoFileReader();
            videoReader.Open(path);

            videoSource.NewFrame += (s, e) =>
                Dispatcher.Invoke(() => videoPlayer.VideoSource = videoSource);

            videoSource.PlayingFinished += (s, e) =>
                Dispatcher.Invoke(ResetPlayer);

            videoSource.Start();
            progressTimer.Start();
            if (videoReader.IsOpen)
                totalTimeText.Text = TimeSpan.FromSeconds(videoReader.FrameCount / videoReader.FrameRate)
                    .ToString(@"mm\:ss");

            loadingOverlay.Visibility = Visibility.Collapsed;
        }

        private void BtnPlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (videoSource == null) return;

            if (videoSource.IsRunning)
            {
                videoSource.WaitForStop();//.Pause();
                playPauseIcon.Data = (Geometry)FindResource("PlayIcon");
            }
            else
            {
                videoSource.WaitForStop();
                playPauseIcon.Data = (Geometry)FindResource("PauseIcon");
            }
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e) => ResetPlayer();

        private void UpdateProgress(object sender, EventArgs e)
        {
            if (videoSource == null || isDraggingProgress) return;

            progressSlider.Maximum = videoReader.FrameCount;
            progressSlider.Value = videoReader.FrameRate;
            currentTimeText.Text = TimeSpan.FromSeconds(videoReader.FrameRate / videoReader.FrameRate)
                .ToString(@"mm\:ss");
        }

        private void ResetPlayer()
        {
            videoSource?.SignalToStop();
            progressTimer.Stop();
            progressSlider.Value = 0;
            currentTimeText.Text = "00:00";
            playPauseIcon.Data = (Geometry)FindResource("PlayIcon");
        }

        private void ProgressSlider_DragStarted(object sender, EventArgs e) =>
            isDraggingProgress = true;

        private void ProgressSlider_DragCompleted(object sender, EventArgs e)
        {
            if (videoSource != null)
            {
                //videoSource.Seek((int)progressSlider.Value);
                isDraggingProgress = false;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            videoSource?.SignalToStop();
            base.OnClosed(e);
        }
        //public void PlayLocalVideo(string fileName)
        //{
        //    // 初始化视频源
        //    videoSource = new VideoFileSource(fileName);
        //    sourcePlayer.VideoSource = videoSource;
        //    videoSource.Start();
        //}
    }
}
