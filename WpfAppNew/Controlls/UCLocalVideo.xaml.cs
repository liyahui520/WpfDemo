using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using Tools.Extend;
using WpfAppNew.Controlls.VideoPlayer;

namespace WpfAppNew.Controlls
{
    /// <summary>
    /// 本地视频播放控件
    /// 支持多种播放器引擎：WPF MediaElement、WPF MediaKit等
    /// 提供现代化的用户界面和完整的播放控制功能
    /// </summary>
    public partial class UCLocalVideo  
    {
        #region 私有字段

        private IVideoPlayer _currentPlayer;
        private bool _isUserSeeking; // 用户是否正在拖拽进度条
        private string _currentFilePath;
        private bool _isInitialized;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化本地视频播放控件
        /// </summary>
        public UCLocalVideo()
        {
            InitializeComponent();
            InitializePlayer();
            LogUtil.Info("UCLocalVideo控件初始化完成");
        }

        /// <summary>
        /// 构造函数（兼容旧版本）
        /// </summary>
        /// <param name="videoPath">视频文件路径</param>
        public UCLocalVideo(string videoPath) : this()
        {
            if (!string.IsNullOrEmpty(videoPath))
            {
                LoadMediaFile(videoPath);
            }
        }

        #endregion

        #region 初始化方法

        /// <summary>
        /// 初始化播放器
        /// </summary>
        private void InitializePlayer()
        {
            try
            {
                // 默认使用MediaElement播放器
                SwitchPlayer("MediaElement");
                _isInitialized = true;
                
                // 显示无文件提示
                ShowNoFilePanel();
                
                LogUtil.Info("播放器初始化成功");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"播放器初始化失败: {ex.Message}");
                ShowError($"播放器初始化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 切换播放器引擎
        /// </summary>
        /// <param name="playerType">播放器类型</param>
        private void SwitchPlayer(string playerType)
        {
            try
            {
                // 释放当前播放器
                if (_currentPlayer != null)
                {
                    _currentPlayer.StateChanged -= OnPlayerStateChanged;
                    _currentPlayer.PositionChanged -= OnPlayerPositionChanged;
                    _currentPlayer.MediaLoaded -= OnPlayerMediaLoaded;
                    _currentPlayer.MediaEnded -= OnPlayerMediaEnded;
                    _currentPlayer.ErrorOccurred -= OnPlayerErrorOccurred;
                    _currentPlayer.Dispose();
                    _currentPlayer = null;
                }

                // 清空播放器容器
                PlayerContainer.Child = null;

                // 创建新播放器
                switch (playerType)
                {
                    case "MediaElement":
                        _currentPlayer = new MediaElementPlayer();
                        break;
                    case "MediaKit":
                        // TODO: 实现WPF MediaKit播放器
                        LogUtil.Info("WPF MediaKit播放器尚未实现，使用MediaElement替代");
                        _currentPlayer = new MediaElementPlayer();
                        break;
                    case "Custom":
                        // TODO: 实现自定义播放器
                        LogUtil.Info("自定义播放器尚未实现，使用MediaElement替代");
                        _currentPlayer = new MediaElementPlayer();
                        break;
                    default:
                        _currentPlayer = new MediaElementPlayer();
                        break;
                }

                // 订阅播放器事件
                _currentPlayer.StateChanged += OnPlayerStateChanged;
                _currentPlayer.PositionChanged += OnPlayerPositionChanged;
                _currentPlayer.MediaLoaded += OnPlayerMediaLoaded;
                _currentPlayer.MediaEnded += OnPlayerMediaEnded;
                _currentPlayer.ErrorOccurred += OnPlayerErrorOccurred;

                // 将播放器控件添加到容器
                PlayerContainer.Child = _currentPlayer.PlayerControl;

                // 设置初始音量
                _currentPlayer.Volume = (int)VolumeSlider.Value;

                LogUtil.Info($"切换到播放器: {playerType}");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"切换播放器失败: {ex.Message}");
                ShowError($"切换播放器失败: {ex.Message}");
            }
        }

        #endregion

        #region 播放器事件处理

        /// <summary>
        /// 播放状态改变事件处理
        /// </summary>
        private void OnPlayerStateChanged(object sender, PlaybackStateChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    switch (e.NewState)
                    {
                        case PlaybackState.Loading:
                            ShowLoadingPanel();
                            break;
                        case PlaybackState.Playing:
                            HideAllPanels();
                            UpdatePlayPauseButtonIcon(true);
                            break;
                        case PlaybackState.Paused:
                            UpdatePlayPauseButtonIcon(false);
                            break;
                        case PlaybackState.Stopped:
                            UpdatePlayPauseButtonIcon(false);
                            PositionSlider.Value = 0;
                            CurrentTimeText.Text = "00:00";
                            break;
                        case PlaybackState.Ended:
                            UpdatePlayPauseButtonIcon(false);
                            break;
                        case PlaybackState.Error:
                            ShowError("播放出错");
                            break;
                    }

                    LogUtil.Info($"播放状态: {e.OldState} -> {e.NewState}");
                }
                catch (Exception ex)
                {
                    LogUtil.Error($"处理播放状态改变事件失败: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// 播放位置改变事件处理
        /// </summary>
        private void OnPlayerPositionChanged(object sender, PositionChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    if (!_isUserSeeking && e.Duration > 0)
                    {
                        PositionSlider.Value = (e.Position / e.Duration) * 100;
                        CurrentTimeText.Text = FormatTime(e.Position);
                    }
                }
                catch (Exception ex)
                {
                    LogUtil.Error($"处理播放位置改变事件失败: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// 媒体加载完成事件处理
        /// </summary>
        private void OnPlayerMediaLoaded(object sender, MediaLoadedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    DurationText.Text = FormatTime(e.Duration);
                    FileNameText.Text = Path.GetFileName(e.FilePath);
                    HideAllPanels();
                    
                    LogUtil.Info($"媒体加载完成: {e.FilePath}, 时长: {e.Duration:F2}秒, 分辨率: {e.Width}x{e.Height}");
                }
                catch (Exception ex)
                {
                    LogUtil.Error($"处理媒体加载完成事件失败: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// 媒体播放结束事件处理
        /// </summary>
        private void OnPlayerMediaEnded(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    UpdatePlayPauseButtonIcon(false);
                    LogUtil.Info("媒体播放结束");
                }
                catch (Exception ex)
                {
                    LogUtil.Error($"处理媒体播放结束事件失败: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// 播放器错误事件处理
        /// </summary>
        private void OnPlayerErrorOccurred(object sender, VideoPlayer.ErrorEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    ShowError(e.Message);
                    LogUtil.Error($"播放器错误: {e.Message}");
                }
                catch (Exception ex)
                {
                    LogUtil.Error($"处理播放器错误事件失败: {ex.Message}");
                }
            });
        }

        #endregion

        #region UI事件处理

        /// <summary>
        /// 播放器选择改变事件
        /// </summary>
        private void PlayerSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (!_isInitialized) return;

                var selectedItem = PlayerSelector.SelectedItem as ComboBoxItem;
                if (selectedItem?.Tag != null)
                {
                    var playerType = selectedItem.Tag.ToString();
                    SwitchPlayer(playerType);
                    
                    // 如果有当前文件，重新加载
                    if (!string.IsNullOrEmpty(_currentFilePath))
                    {
                        LoadMediaFile(_currentFilePath);
                    }
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"播放器选择改变失败: {ex.Message}");
                ShowError($"播放器选择改变失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 打开文件按钮点击事件
        /// </summary>
        private void OpenFileButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Title = "选择视频文件",
                    Filter = "视频文件|*.mp4;*.avi;*.wmv;*.mov;*.mkv;*.flv;*.3gp;*.webm|" +
                            "MP4文件|*.mp4|" +
                            "AVI文件|*.avi|" +
                            "WMV文件|*.wmv|" +
                            "MOV文件|*.mov|" +
                            "所有文件|*.*",
                    FilterIndex = 1
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    LoadMediaFile(openFileDialog.FileName);
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"打开文件失败: {ex.Message}");
                ShowError($"打开文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 设置按钮点击事件
        /// </summary>
        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // TODO: 实现设置界面
                MessageBox.Show("设置功能正在开发中...", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogUtil.Error($"打开设置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 播放/暂停按钮点击事件
        /// </summary>
        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentPlayer == null) return;

                switch (_currentPlayer.State)
                {
                    case PlaybackState.Playing:
                        _currentPlayer.Pause();
                        break;
                    case PlaybackState.Paused:
                    case PlaybackState.Stopped:
                        _currentPlayer.Play();
                        break;
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"播放/暂停操作失败: {ex.Message}");
                ShowError($"播放/暂停操作失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 停止按钮点击事件
        /// </summary>
        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _currentPlayer?.Stop();
            }
            catch (Exception ex)
            {
                LogUtil.Error($"停止操作失败: {ex.Message}");
                ShowError($"停止操作失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 上一个按钮点击事件
        /// </summary>
        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // TODO: 实现播放列表功能
                MessageBox.Show("播放列表功能正在开发中...", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogUtil.Error($"上一个操作失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 下一个按钮点击事件
        /// </summary>
        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // TODO: 实现播放列表功能
                MessageBox.Show("播放列表功能正在开发中...", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogUtil.Error($"下一个操作失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 进度条值改变事件
        /// </summary>
        private void PositionSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                if (_isUserSeeking && _currentPlayer != null && _currentPlayer.Duration > 0)
                {
                    var position = (e.NewValue / 100) * _currentPlayer.Duration;
                    _currentPlayer.Seek(position);
                    CurrentTimeText.Text = FormatTime(position);
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"进度条操作失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 进度条鼠标按下事件
        /// </summary>
        private void PositionSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _isUserSeeking = true;
        }

        /// <summary>
        /// 进度条鼠标释放事件
        /// </summary>
        private void PositionSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            _isUserSeeking = false;
        }

        /// <summary>
        /// 播放速度选择改变事件
        /// </summary>
        private void SpeedSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (_currentPlayer == null) return;

                var selectedItem = SpeedSelector.SelectedItem as ComboBoxItem;
                if (selectedItem?.Tag != null && double.TryParse(selectedItem.Tag.ToString(), out double speed))
                {
                    _currentPlayer.PlaybackRate = speed;
                    LogUtil.Info($"播放速度设置为: {speed}x");
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"设置播放速度失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 静音按钮点击事件
        /// </summary>
        private void MuteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentPlayer == null) return;

                _currentPlayer.IsMuted = !_currentPlayer.IsMuted;
                UpdateMuteButtonIcon(_currentPlayer.IsMuted);
                LogUtil.Info($"静音状态: {_currentPlayer.IsMuted}");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"静音操作失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 音量滑块值改变事件
        /// </summary>
        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                if (_currentPlayer != null)
                {
                    _currentPlayer.Volume = (int)e.NewValue;
                    VolumeText.Text = $"{(int)e.NewValue}%";
                    
                    // 更新静音按钮状态
                    if (e.NewValue == 0)
                    {
                        UpdateMuteButtonIcon(true);
                    }
                    else if (!_currentPlayer.IsMuted)
                    {
                        UpdateMuteButtonIcon(false);
                    }
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"设置音量失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 重试按钮点击事件
        /// </summary>
        private void RetryButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(_currentFilePath))
                {
                    LoadMediaFile(_currentFilePath);
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"重试失败: {ex.Message}");
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 加载媒体文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        public void LoadMediaFile(string filePath)
        {
            try
            {
                if (_currentPlayer == null)
                {
                    ShowError("播放器未初始化");
                    return;
                }

                _currentFilePath = filePath;
                
                if (_currentPlayer.LoadMedia(filePath))
                {
                    LogUtil.Info($"开始加载媒体文件: {filePath}");
                }
                else
                {
                    ShowError("加载媒体文件失败");
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"加载媒体文件失败: {ex.Message}");
                ShowError($"加载媒体文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示加载面板
        /// </summary>
        private void ShowLoadingPanel()
        {
            LoadingPanel.Visibility = Visibility.Visible;
            ErrorPanel.Visibility = Visibility.Collapsed;
            NoFilePanel.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// 显示错误面板
        /// </summary>
        /// <param name="message">错误消息</param>
        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorPanel.Visibility = Visibility.Visible;
            LoadingPanel.Visibility = Visibility.Collapsed;
            NoFilePanel.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// 显示无文件面板
        /// </summary>
        private void ShowNoFilePanel()
        {
            NoFilePanel.Visibility = Visibility.Visible;
            LoadingPanel.Visibility = Visibility.Collapsed;
            ErrorPanel.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// 隐藏所有面板
        /// </summary>
        private void HideAllPanels()
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            ErrorPanel.Visibility = Visibility.Collapsed;
            NoFilePanel.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// 格式化时间显示
        /// </summary>
        /// <param name="seconds">秒数</param>
        /// <returns>格式化的时间字符串</returns>
        private string FormatTime(double seconds)
        {
            var timeSpan = TimeSpan.FromSeconds(seconds);
            if (timeSpan.TotalHours >= 1)
            {
                return timeSpan.ToString(@"h\:mm\:ss");
            }
            else
            {
                return timeSpan.ToString(@"m\:ss");
            }
        }

        /// <summary>
        /// 更新播放/暂停按钮图标
        /// </summary>
        /// <param name="isPlaying">是否正在播放</param>
        private void UpdatePlayPauseButtonIcon(bool isPlaying)
        {
            try
            {
                if (PlayPauseButton.Content is TextBlock textBlock)
                {
                    textBlock.Text = isPlaying ? "&#xE769;" : "&#xE768;"; // 暂停图标 : 播放图标
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"更新播放按钮图标失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新静音按钮图标
        /// </summary>
        /// <param name="isMuted">是否静音</param>
        private void UpdateMuteButtonIcon(bool isMuted)
        {
            try
            {
                if (MuteButton.Content is TextBlock textBlock)
                {
                    textBlock.Text = isMuted ? "&#xE74F;" : "&#xE767;"; // 静音图标 : 音量图标
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"更新静音按钮图标失败: {ex.Message}");
            }
        }

        #endregion

        #region 资源清理

        /// <summary>
        /// 控件卸载时清理资源
        /// </summary>
        private void UCLocalVideo_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _currentPlayer?.Dispose();
                LogUtil.Info("UCLocalVideo控件资源已清理");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"清理UCLocalVideo控件资源失败: {ex.Message}");
            }
        }

        #endregion
    }
}
