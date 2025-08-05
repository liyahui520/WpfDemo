using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using HandyControl.Controls;
using Microsoft.Win32;

namespace WpfMain.Controlls
{
    /// <summary>
    /// UCLocalVideo.xaml 的交互逻辑
    /// </summary>
    public enum LoopMode { None, Single }
    public partial class UCLocalVideo
    {
        private bool isPlaying = false;
        private LoopMode loopMode = LoopMode.None;
        private double volume = 0.5;
        private TimeSpan totalTime = TimeSpan.Zero;
        private bool isDraggingProgress = false;
        private System.Windows.Threading.DispatcherTimer timer;


        public UCLocalVideo(string path)
        {
            InitializeComponent();
            InitializeTimer();
            mediaElement.Source = new Uri(path);
            mediaElement.Play();
            isPlaying = true;
            UpdatePlayPauseIcon();
            BtnCenterPlay.Visibility = Visibility.Collapsed;
            timer.Start();
        }
        private void InitializeTimer()
        {
            timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(500);
            timer.Tick += Timer_Tick;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (mediaElement.Source != null && !isDraggingProgress)
            {
                UpdateProgress();
            }
        }

        private void UpdateProgress()
        {
            var currentTime = mediaElement.NaturalDuration.TimeSpan;
            if (totalTime.TotalSeconds > 0)
            {
                sliderProgress.Value = currentTime.TotalSeconds / totalTime.TotalSeconds * 100;
            }
            TxtTime.Text = $"{FormatTime(currentTime)} / {FormatTime(totalTime)}";
        }

        private string FormatTime(TimeSpan time)
        {
            return time.Hours > 0 ? time.ToString("hh\\:mm\\:ss") : time.ToString("mm\\:ss");
        }

        // 窗口加载：初始化音量图标
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            mediaElement.Volume = volume;
            SliderVolume.Value = volume;
            UpdateMuteIcon(); // 初始化音量图标
        }

        // 打开视频
        private void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            var openDialog = new OpenFileDialog
            {
                Filter = "视频文件 (*.mp4;*.avi;*.wmv;*.mov;*.mkv)|*.mp4;*.avi;*.wmv;*.mov;*.mkv|所有文件 (*.*)|*.*"
            };
            if (openDialog.ShowDialog() == true)
            {
                try
                {
                    mediaElement.Source = new Uri(openDialog.FileName);
                    mediaElement.Play();
                    isPlaying = true;
                    UpdatePlayPauseIcon();
                    BtnCenterPlay.Visibility = Visibility.Collapsed;
                    timer.Start();
                }
                catch (Exception ex)
                { 
                }
            }
        }

        // 播放/暂停切换
        private void BtnPlayPause_Click(object sender, RoutedEventArgs e) => TogglePlayPause();
        private void BtnCenterPlay_Click(object sender, RoutedEventArgs e) => TogglePlayPause();

        private void TogglePlayPause()
        {
            if (mediaElement.Source == null) return;

            if (isPlaying)
            {
                mediaElement.Pause();
                BtnCenterPlay.Visibility = Visibility.Visible;
            }
            else
            {
                mediaElement.Play();
                BtnCenterPlay.Visibility = Visibility.Collapsed;
                timer.Start();
            }
            isPlaying = !isPlaying;
            UpdatePlayPauseIcon();
        }

        // 更新播放/暂停图标（关键：从资源字典获取Geometry）
        private void UpdatePlayPauseIcon()
        {
            var geometry = isPlaying
                ? (Geometry)FindResource("VideoStopGeometry")
                : (Geometry)FindResource("VideoStartGeometry");
            BtnPlayPause.SetValue(IconElement.GeometryProperty, geometry);
        }

        // 视频加载完成：获取总时长
        private void mediaElement_MediaOpened(object sender, RoutedEventArgs e)
        {
            if (mediaElement.NaturalDuration.HasTimeSpan)
            {
                totalTime = mediaElement.NaturalDuration.TimeSpan;
                UpdateProgress();
            }
        }

        // 视频结束：处理循环
        private void mediaElement_MediaEnded(object sender, RoutedEventArgs e)
        {
            if (loopMode == LoopMode.Single)
            {
                mediaElement.Position = TimeSpan.Zero;
                mediaElement.Play();
                isPlaying = true;
                UpdatePlayPauseIcon();
                BtnCenterPlay.Visibility = Visibility.Collapsed;
            }
            else
            {
                isPlaying = false;
                UpdatePlayPauseIcon();
                BtnCenterPlay.Visibility = Visibility.Visible;
                timer.Stop();
            }
        }

        // 循环模式切换
        private void BtnLoop_Click(object sender, RoutedEventArgs e)
        {
            loopMode = loopMode == LoopMode.None ? LoopMode.Single : LoopMode.None;
            UpdateLoopIcon();
        }

        // 更新循环图标（默认RepeatGeometry，单曲循环用自定义LoopSingleGeometry）
        private void UpdateLoopIcon()
        {
            var geometry = loopMode == LoopMode.Single
                ? (Geometry)FindResource("LoopSingleGeometry")
                : (Geometry)FindResource("RepeatGeometry");
            //BtnLoop.SetValue(IconElement.GeometryProperty, geometry);
        }

        // 倍速调节
        private void CmbSpeed_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (mediaElement.Source == null) return;

            if (CmbSpeed.SelectedIndex == 0) mediaElement.SpeedRatio = 1.0;
            else if (CmbSpeed.SelectedIndex == 1) mediaElement.SpeedRatio = 1.5;
            else if (CmbSpeed.SelectedIndex == 2) mediaElement.SpeedRatio = 2.0;
        }

        // 进度条拖动
        private void SliderProgress_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            isDraggingProgress = true;
            timer.Stop();
        }

        private void SliderProgress_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            if (mediaElement.Source == null || totalTime.TotalSeconds <= 0) return;

            isDraggingProgress = false;
            double positionSeconds = (sliderProgress.Value / 100) * totalTime.TotalSeconds;
            mediaElement.Position = TimeSpan.FromSeconds(positionSeconds);
            UpdateProgress();
            timer.Start();
        }

        // 音量调节（更新图标）
        private void SliderVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded) return;

            volume = SliderVolume.Value;
            mediaElement.Volume = volume;
            UpdateMuteIcon();
        }

        // 更新静音图标（VolumeGeometry / VolumeMuteGeometry）
        private void UpdateMuteIcon()
        {
            //var geometry = volume == 0
            //    ? (Geometry)FindResource("VolumeMuteGeometry")
            //    : (Geometry)FindResource("VolumeGeometry");
            //BtnMute.SetValue(IconElement.GeometryProperty, geometry);
        }

        // 静音切换
        private void BtnMute_Click(object sender, RoutedEventArgs e)
        {
            SliderVolume.Value = volume == 0 ? 0.5 : 0; // 0.5为默认音量，可优化为记录历史值
        }

        // 全屏切换
        private void BtnFullScreen_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Normal)
            {
                WindowState = WindowState.Maximized;
                ResizeMode = ResizeMode.NoResize;
            }
            else
            {
                WindowState = WindowState.Normal;
                ResizeMode = ResizeMode.CanResizeWithGrip;
            }
        }

        // 置顶切换
        private void BtnTopMost_Checked(object sender, RoutedEventArgs e) => Topmost = true;
        private void BtnTopMost_Unchecked(object sender, RoutedEventArgs e) => Topmost = false;
    }
}
