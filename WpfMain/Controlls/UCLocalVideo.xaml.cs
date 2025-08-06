using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Vlc.DotNet.Core;

namespace WpfMain.Controlls
{
    /// <summary>
    /// UCLocalVideo.xaml 的交互逻辑
    /// </summary> 
    public partial class UCLocalVideo
    {
        // VLC 库路径 (请根据你的实际路径修改)
        private string _vlcLibDirectory;

        // 当前播放的文件路径
        private string _currentFilePath;

        // 进度条拖动状态
        private bool _isDraggingProgress;
        private bool _isDisposed; // 用于标记是否已释放资源
        private bool _isMediaEnded; // 标记媒体是否已播放结束
        private bool _wasPlayingBeforeDrag; // 记录拖拽前的播放状态
        private readonly object _lockObj = new object(); // 用于线程同步
        private long _totalMediaDuration; // 视频总时长（毫秒）
        private string path = string.Empty;

        public UCLocalVideo(string path)
        {
            InitializeComponent();
            this.path = path;
            Thread thread = new Thread(new ThreadStart(InitializeVlc));
            thread.IsBackground = true;
            thread.Start();
            _isDisposed = false;
            _isMediaEnded = false;
        }
        /// <summary>
        /// 初始化 VLC 控件
        /// </summary>
        private async void InitializeVlc()
        {
            try
            {
                // 设置 VLC 库目录 

                // 更新加载状态
                UpdateInitStatus("正在检测系统环境...");

                // 异步确定VLC库路径
                string _vlcLibDirectory = await Task.Run(() =>
                {
                    return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "libvlc");
                });
                UpdateInitStatus("正在加载VLC组件...");
                UpdateInitProgress("验证库文件完整性...");
                // 检查 VLC 库是否存在
                if (!Directory.Exists(_vlcLibDirectory))
                {
                    throw new DirectoryNotFoundException($"VLC 库目录不存在: {_vlcLibDirectory}\n请确保已将 VLC 库文件放置在正确位置。");
                }
                UpdateInitProgress("初始化播放器核心...");
                // 初始化 VLC 控件
                // 关键：在UI线程执行CreatePlayer（VLC要求）
                //await Dispatcher.InvokeAsync(() =>
                //{
                vlcControl.SourceProvider.CreatePlayer(new DirectoryInfo(_vlcLibDirectory));
                //}, DispatcherPriority.Normal);
                UpdateInitProgress("配置播放器参数...");
                // 获取媒体播放器实例
                var mediaPlayer = vlcControl.SourceProvider.MediaPlayer;

                // 新版本中音量控制在Audio属性下
                if (mediaPlayer.Audio != null)
                {
                    mediaPlayer.Audio.Volume = 100; // 设置初始音量
                }
                // 注册事件
                mediaPlayer.EndReached += MediaPlayer_EndReached;
                mediaPlayer.PositionChanged += MediaPlayer_PositionChanged;
                mediaPlayer.LengthChanged += MediaPlayer_LengthChanged;
                mediaPlayer.Playing += MediaPlayer_Playing;
                mediaPlayer.Paused += MediaPlayer_Paused;
                mediaPlayer.Stopped += MediaPlayer_Stopped;

                UpdateInitStatus("初始化完成");
                UpdateInitProgress("正在进入播放器...");
                // 延迟一小段时间，让用户看到完成状态
                await Task.Delay(500);
                // 切换到主界面
                Dispatcher.Invoke(() =>
                {
                    loadingScreen.Visibility = Visibility.Collapsed;
                    mainContent.Visibility = Visibility.Visible;
                    UpdateButtonStates(false);
                });
                InitVodio(path);
            }
            catch (Exception ex)
            {// 显示错误信息，允许用户重试
                Dispatcher.Invoke(() =>
                {
                    txtInitStatus.Text = "初始化失败";
                    txtInitProgress.Text = ex.Message;

                    // 添加重试按钮
                    var retryButton = new Button
                    {
                        Content = "重试",
                        Style = (Style)FindResource("ControlButtonStyle"),
                        Margin = new Thickness(0, 20, 0, 0)
                    };
                    retryButton.Click += (s, e) => InitializeVlc();

                    ((StackPanel)loadingScreen.Children[0]).Children.Add(retryButton);
                });
            }
        }
        /// <summary>
        /// 更新初始化状态文本
        /// </summary>
        private void UpdateInitStatus(string message)
        {
            Dispatcher.Invoke(() => txtInitStatus.Text = message);
        }

        /// <summary>
        /// 更新初始化进度文本
        /// </summary>
        private void UpdateInitProgress(string message)
        {
            Dispatcher.Invoke(() => txtInitProgress.Text = message);
        }
        #region 进度条事件处理（修复无法触发问题）

        /// <summary>
        /// 鼠标按下（开始拖拽）
        /// </summary>
        private void sldProgress_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentFilePath) || _totalMediaDuration <= 0)
                return;

            // 记录拖拽前的播放状态
            var mediaPlayer = vlcControl.SourceProvider.MediaPlayer;
            if (mediaPlayer != null)
            {
                _wasPlayingBeforeDrag = mediaPlayer.IsPlaying();
                if (_wasPlayingBeforeDrag)
                {
                    mediaPlayer.Pause(); // 拖拽时暂停播放
                }
            }

            _isDraggingProgress = true;
            // 计算初始拖拽位置
            UpdateProgressFromMousePosition(e);
        }

        /// <summary>
        /// 鼠标移动（拖拽过程中）
        /// </summary>
        private void sldProgress_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingProgress && _totalMediaDuration > 0)
            {
                UpdateProgressFromMousePosition(e);
            }
        }

        /// <summary>
        /// 鼠标释放（结束拖拽）
        /// </summary>
        private void sldProgress_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDraggingProgress || string.IsNullOrEmpty(_currentFilePath) || _totalMediaDuration <= 0)
            {
                _isDraggingProgress = false;
                return;
            }

            try
            {
                // 计算最终位置并更新视频
                UpdateProgressFromMousePosition(e);

                var mediaPlayer = vlcControl.SourceProvider.MediaPlayer;
                if (mediaPlayer != null)
                {
                    // 设置视频位置（0-1之间的比例）
                    var position = (float)(sldProgress.Value / 100);
                    position = Math.Max(0, Math.Min(1, position)); // 限制在有效范围内
                    mediaPlayer.Position = position;

                    // 恢复拖拽前的播放状态
                    if (_wasPlayingBeforeDrag && !mediaPlayer.IsPlaying())
                    {
                        mediaPlayer.Play();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"进度调整错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isDraggingProgress = false;
            }
        }

        public async void InitVodio(string path)
        {
            try
            {
                this.path = path;
                // 先停止当前播放
                await SafeStopAsync();

                _currentFilePath = path;
                lock (_lockObj)
                {
                    _isMediaEnded = false;
                }

                var mediaPlayer = vlcControl.SourceProvider.MediaPlayer;
                if (mediaPlayer != null)
                {
                    mediaPlayer.SetMedia(new FileInfo(_currentFilePath));
                    mediaPlayer.Play();
                    UpdateButtonStates(true);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开文件失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 根据鼠标位置更新进度条和时间显示
        /// </summary>
        private void UpdateProgressFromMousePosition(MouseEventArgs e)
        {
            var slider = sldProgress;
            // 获取进度条在屏幕上的位置和宽度
            var sliderRect = slider.TransformToAncestor(this).TransformBounds(new Rect(0, 0, slider.ActualWidth, slider.ActualHeight));
            var mouseX = e.GetPosition(this).X;

            // 计算鼠标在进度条上的相对位置（0-100）
            var value = (mouseX - sliderRect.Left) / sliderRect.Width * 100;
            value = Math.Max(0, Math.Min(100, value)); // 限制在0-100之间

            // 更新进度条和时间显示
            slider.Value = value;
            var currentMs = (long)(value / 100 * _totalMediaDuration);
            txtCurrentTime.Text = TimeSpan.FromMilliseconds(currentMs).ToString(@"mm\:ss");
        }

        #endregion

        #region 关键修复：安全的Stop()方法实现

        /// <summary>
        /// 安全停止播放（带超时控制）
        /// </summary>
        private async Task SafeStopAsync()
        {
            if (_isDisposed) return;

            var mediaPlayer = vlcControl.SourceProvider.MediaPlayer;
            if (mediaPlayer == null) return;

            try
            {
                // 使用锁确保线程安全
                lock (_lockObj)
                {
                    _isMediaEnded = false;
                }

                // 使用Task.Run避免UI线程阻塞
                await Task.Run(() =>
                {
                    try
                    {
                        // 创建超时控制
                        var cancellationTokenSource = new CancellationTokenSource(2000); // 2秒超时
                        var stopTask = Task.Factory.StartNew(() =>
                        {
                            if (!_isDisposed && mediaPlayer.IsPlaying())
                            {
                                mediaPlayer.Stop();
                            }
                        }, cancellationTokenSource.Token);

                        // 等待任务完成或超时
                        if (!stopTask.Wait(2000))
                        {
                            throw new TimeoutException("停止操作超时");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // 超时异常处理
                        Console.WriteLine("停止操作已超时");
                    }
                });

                // 更新UI
                Dispatcher.Invoke(() =>
                {
                    UpdateButtonStates(false);
                    sldProgress.Value = 0;
                    txtCurrentTime.Text = "00:00";
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"停止播放时出错：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        #endregion

        #region 事件处理 
        private void MediaPlayer_EndReached(object sender, EventArgs e)
        {
            if (_isDisposed) return;

            Dispatcher.BeginInvoke(new Action(async () =>
            {
                try
                {
                    lock (_lockObj)
                    {
                        _isMediaEnded = true;
                    }
                    await SafeStopAsync(); // 使用安全停止方法
                    btnPlay.Content = new TextBlock { FontFamily = new FontFamily("Segoe MDL2 Assets"), Text = "" };

                    InitVodio(path);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"播放结束处理错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }));
        }

        private void MediaPlayer_PositionChanged(object sender, VlcMediaPlayerPositionChangedEventArgs e)
        {
            if (_isDisposed || _isDraggingProgress || _isMediaEnded) return;

            try
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_isDisposed) return;

                    sldProgress.Value = e.NewPosition * 100;

                    var mediaPlayer = vlcControl.SourceProvider.MediaPlayer;
                    if (mediaPlayer != null)
                    {
                        var currentTime = TimeSpan.FromMilliseconds(mediaPlayer.Time);
                        txtCurrentTime.Text = currentTime.ToString(@"mm\:ss");
                    }
                }), System.Windows.Threading.DispatcherPriority.Render);
            }
            catch { }
        }

        private void MediaPlayer_LengthChanged(object sender, VlcMediaPlayerLengthChangedEventArgs e)
        {
            if (_isDisposed) return;
            // 保存视频总时长（毫秒）
            _totalMediaDuration = e.NewLength;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var totalTime = TimeSpan.FromMilliseconds(e.NewLength);
                txtTotalTime.Text = totalTime.ToString(@"mm\:ss");
            }));
        }

        private void BtnPlay_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentFilePath)) return;

            var mediaPlayer = vlcControl.SourceProvider.MediaPlayer;
            if (mediaPlayer == null) return;

            try
            {
                if (mediaPlayer.IsPlaying())
                {
                    // 暂停操作
                    mediaPlayer.Pause();
                    btnPlay.Content = new TextBlock { FontFamily = new FontFamily("Segoe MDL2 Assets"), Text = "" };
                }
                else
                {
                    // 播放操作
                    if (_isMediaEnded)
                    {
                        mediaPlayer.Position = 0;
                        lock (_lockObj)
                        {
                            _isMediaEnded = false;
                        }
                    }
                    mediaPlayer.Play();
                    btnPlay.Content = new TextBlock { FontFamily = new FontFamily("Segoe MDL2 Assets"), Text = "" };
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"播放控制错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            // 调用安全停止方法
            await SafeStopAsync();
        }

        private async void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "视频文件|*.mp4;*.avi;*.mkv;*.mov;*.flv;*.wmv|所有文件|*.*",
                Title = "选择视频文件"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    // 先停止当前播放
                    await SafeStopAsync();

                    _currentFilePath = openFileDialog.FileName;
                    lock (_lockObj)
                    {
                        _isMediaEnded = false;
                    }

                    var mediaPlayer = vlcControl.SourceProvider.MediaPlayer;
                    if (mediaPlayer != null)
                    {
                        mediaPlayer.SetMedia(new FileInfo(_currentFilePath));
                        mediaPlayer.Play();
                        UpdateButtonStates(true);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"打开文件失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void VideoArea_MouseClick(object sender, MouseButtonEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentFilePath)) return;
            BtnPlay_Click(sender, e);
        }

        private void sldVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isDisposed) return;

            var mediaPlayer = vlcControl.SourceProvider.MediaPlayer;
            if (mediaPlayer?.Audio != null)
            {
                mediaPlayer.Audio.Volume = (int)e.NewValue;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _isDisposed = true;
            _isMediaEnded = true;

            try
            {
                var mediaPlayer = vlcControl.SourceProvider.MediaPlayer;
                if (mediaPlayer != null)
                {
                    // 注销所有事件
                    mediaPlayer.EndReached -= MediaPlayer_EndReached;
                    mediaPlayer.PositionChanged -= MediaPlayer_PositionChanged;
                    mediaPlayer.LengthChanged -= MediaPlayer_LengthChanged;
                    mediaPlayer.Playing -= MediaPlayer_Playing;
                    mediaPlayer.Paused -= MediaPlayer_Paused;
                    mediaPlayer.Stopped -= MediaPlayer_Stopped;

                    // 安全停止
                    if (mediaPlayer.IsPlaying())
                    {
                        var stopTask = Task.Run(() => mediaPlayer.Stop());
                        Task.WaitAny(stopTask, Task.Delay(1000)); // 等待1秒超时
                    }
                    mediaPlayer.Dispose();
                }
            }
            catch { }
        }

        /// <summary>
        /// 窗口移动
        /// </summary>
        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        /// <summary>
        /// 最小化按钮
        /// </summary>
        private void MinButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        /// <summary>
        /// 最大化按钮
        /// </summary>
        private void MaxButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = this.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        /// <summary>
        /// 关闭按钮
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }


        #endregion

        #region 辅助方法

        private void MediaPlayer_Playing(object sender, EventArgs e)
        {
            if (_isDisposed) return;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                btnPlay.Content = new TextBlock { FontFamily = new FontFamily("Segoe MDL2 Assets"), Text = "" };
            }));
        }

        private void MediaPlayer_Paused(object sender, EventArgs e)
        {
            if (_isDisposed) return;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                btnPlay.Content = new TextBlock { FontFamily = new FontFamily("Segoe MDL2 Assets"), Text = "" };
            }));
        }

        private void MediaPlayer_Stopped(object sender, EventArgs e)
        {
            if (_isDisposed) return;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                btnPlay.Content = new TextBlock { FontFamily = new FontFamily("Segoe MDL2 Assets"), Text = "" };
            }));
        }

        private void UpdateButtonStates(bool isPlaying)
        {
            btnPlay.IsEnabled = true;
            btnStop.IsEnabled = isPlaying;
        }

        #endregion
    }
}
