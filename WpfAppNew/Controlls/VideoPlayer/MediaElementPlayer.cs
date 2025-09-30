using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Tools.Extend;

namespace WpfAppNew.Controlls.VideoPlayer
{
    /// <summary>
    /// 基于WPF原生MediaElement的视频播放器实现
    /// 支持常见的视频格式，轻量级，无需额外依赖
    /// </summary>
    public class MediaElementPlayer : IVideoPlayer, IDisposable
    {
        #region 私有字段

        private readonly MediaElement _mediaElement;
        private readonly DispatcherTimer _positionTimer;
        private PlaybackState _state = PlaybackState.None;
        private string _mediaPath;
        private bool _isDisposed;
        private bool _isUserSeeking; // 用户是否正在拖拽进度条

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化MediaElement播放器
        /// </summary>
        public MediaElementPlayer()
        {
            // 创建MediaElement控件
            _mediaElement = new MediaElement
            {
                LoadedBehavior = MediaState.Manual,
                UnloadedBehavior = MediaState.Manual,
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.Both
            };

            // 订阅MediaElement事件
            _mediaElement.MediaOpened += OnMediaOpened;
            _mediaElement.MediaEnded += OnMediaEnded;
            _mediaElement.MediaFailed += OnMediaFailed;
            _mediaElement.BufferingStarted += OnBufferingStarted;
            _mediaElement.BufferingEnded += OnBufferingEnded;

            // 创建位置更新定时器
            _positionTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100) // 每100ms更新一次位置
            };
            _positionTimer.Tick += OnPositionTimerTick;

            LogUtil.Info("MediaElement播放器初始化完成");
        }

        #endregion

        #region IVideoPlayer 属性实现

        /// <summary>
        /// 当前播放状态
        /// </summary>
        public PlaybackState State
        {
            get => _state;
            private set
            {
                if (_state != value)
                {
                    var oldState = _state;
                    _state = value;
                    StateChanged?.Invoke(this, new PlaybackStateChangedEventArgs(oldState, value));
                    LogUtil.Info($"播放状态改变: {oldState} -> {value}");
                }
            }
        }

        /// <summary>
        /// 当前播放位置（秒）
        /// </summary>
        public double Position
        {
            get
            {
                if (_mediaElement.NaturalDuration.HasTimeSpan)
                {
                    return _mediaElement.Position.TotalSeconds;
                }
                return 0;
            }
            set
            {
                if (_mediaElement.NaturalDuration.HasTimeSpan && !_isDisposed)
                {
                    _isUserSeeking = true;
                    _mediaElement.Position = TimeSpan.FromSeconds(value);
                    _isUserSeeking = false;
                }
            }
        }

        /// <summary>
        /// 视频总时长（秒）
        /// </summary>
        public double Duration
        {
            get
            {
                if (_mediaElement.NaturalDuration.HasTimeSpan)
                {
                    return _mediaElement.NaturalDuration.TimeSpan.TotalSeconds;
                }
                return 0;
            }
        }

        /// <summary>
        /// 音量（0-100）
        /// </summary>
        public int Volume
        {
            get => (int)(_mediaElement.Volume * 100);
            set
            {
                if (value >= 0 && value <= 100)
                {
                    _mediaElement.Volume = value / 100.0;
                }
            }
        }

        /// <summary>
        /// 是否静音
        /// </summary>
        public bool IsMuted
        {
            get => _mediaElement.IsMuted;
            set => _mediaElement.IsMuted = value;
        }

        /// <summary>
        /// 播放速度（1.0为正常速度）
        /// </summary>
        public double PlaybackRate
        {
            get => _mediaElement.SpeedRatio;
            set
            {
                if (value > 0)
                {
                    _mediaElement.SpeedRatio = value;
                }
            }
        }

        /// <summary>
        /// 当前加载的媒体文件路径
        /// </summary>
        public string MediaPath => _mediaPath;

        /// <summary>
        /// 播放器控件（用于在UI中显示）
        /// </summary>
        public FrameworkElement PlayerControl => _mediaElement;

        #endregion

        #region IVideoPlayer 事件实现

        /// <summary>
        /// 播放状态改变事件
        /// </summary>
        public event EventHandler<PlaybackStateChangedEventArgs> StateChanged;

        /// <summary>
        /// 播放位置改变事件
        /// </summary>
        public event EventHandler<PositionChangedEventArgs> PositionChanged;

        /// <summary>
        /// 媒体加载完成事件
        /// </summary>
        public event EventHandler<MediaLoadedEventArgs> MediaLoaded;

        /// <summary>
        /// 播放结束事件
        /// </summary>
        public event EventHandler MediaEnded;

        /// <summary>
        /// 错误发生事件
        /// </summary>
        public event EventHandler<ErrorEventArgs> ErrorOccurred;

        #endregion

        #region IVideoPlayer 方法实现

        /// <summary>
        /// 加载媒体文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>是否加载成功</returns>
        public bool LoadMedia(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    LogUtil.Error($"文件不存在: {filePath}");
                    return false;
                }

                State = PlaybackState.Loading;
                _mediaPath = filePath;

                // 设置媒体源
                _mediaElement.Source = new Uri(filePath, UriKind.Absolute);

                LogUtil.Info($"开始加载媒体文件: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                LogUtil.Error($"加载媒体文件失败: {ex.Message}");
                State = PlaybackState.Error;
                ErrorOccurred?.Invoke(this, new ErrorEventArgs($"加载媒体文件失败: {ex.Message}", ex));
                return false;
            }
        }

        /// <summary>
        /// 开始播放
        /// </summary>
        public void Play()
        {
            try
            {
                if (_isDisposed) return;

                _mediaElement.Play();
                _positionTimer.Start();
                State = PlaybackState.Playing;
                LogUtil.Info("开始播放");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"播放失败: {ex.Message}");
                ErrorOccurred?.Invoke(this, new ErrorEventArgs($"播放失败: {ex.Message}", ex));
            }
        }

        /// <summary>
        /// 暂停播放
        /// </summary>
        public void Pause()
        {
            try
            {
                if (_isDisposed) return;

                _mediaElement.Pause();
                _positionTimer.Stop();
                State = PlaybackState.Paused;
                LogUtil.Info("暂停播放");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"暂停失败: {ex.Message}");
                ErrorOccurred?.Invoke(this, new ErrorEventArgs($"暂停失败: {ex.Message}", ex));
            }
        }

        /// <summary>
        /// 停止播放
        /// </summary>
        public void Stop()
        {
            try
            {
                if (_isDisposed) return;

                _mediaElement.Stop();
                _positionTimer.Stop();
                State = PlaybackState.Stopped;
                LogUtil.Info("停止播放");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"停止失败: {ex.Message}");
                ErrorOccurred?.Invoke(this, new ErrorEventArgs($"停止失败: {ex.Message}", ex));
            }
        }

        /// <summary>
        /// 跳转到指定位置
        /// </summary>
        /// <param name="position">位置（秒）</param>
        public void Seek(double position)
        {
            try
            {
                if (_isDisposed || !_mediaElement.NaturalDuration.HasTimeSpan) return;

                var duration = _mediaElement.NaturalDuration.TimeSpan.TotalSeconds;
                if (position >= 0 && position <= duration)
                {
                    Position = position;
                    LogUtil.Info($"跳转到位置: {position:F2}秒");
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"跳转失败: {ex.Message}");
                ErrorOccurred?.Invoke(this, new ErrorEventArgs($"跳转失败: {ex.Message}", ex));
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;
            _positionTimer?.Stop();
            _mediaElement?.Close();

            LogUtil.Info("MediaElement播放器资源已释放");
        }

        #endregion

        #region 私有事件处理方法

        /// <summary>
        /// 媒体打开事件处理
        /// </summary>
        private void OnMediaOpened(object sender, RoutedEventArgs e)
        {
            try
            {
                State = PlaybackState.Stopped;
                
                var duration = Duration;
                var width = _mediaElement.NaturalVideoWidth;
                var height = _mediaElement.NaturalVideoHeight;

                LogUtil.Info($"媒体加载完成: 时长={duration:F2}秒, 分辨率={width}x{height}");
                
                MediaLoaded?.Invoke(this, new MediaLoadedEventArgs(_mediaPath, duration, width, height));
            }
            catch (Exception ex)
            {
                LogUtil.Error($"媒体打开事件处理失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 媒体结束事件处理
        /// </summary>
        private void OnMediaEnded(object sender, RoutedEventArgs e)
        {
            _positionTimer.Stop();
            State = PlaybackState.Ended;
            LogUtil.Info("媒体播放结束");
            MediaEnded?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 媒体失败事件处理
        /// </summary>
        private void OnMediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            _positionTimer.Stop();
            State = PlaybackState.Error;
            var errorMsg = $"媒体播放失败: {e.ErrorException?.Message}";
            LogUtil.Error(errorMsg);
            ErrorOccurred?.Invoke(this, new ErrorEventArgs(errorMsg, e.ErrorException));
        }

        /// <summary>
        /// 缓冲开始事件处理
        /// </summary>
        private void OnBufferingStarted(object sender, RoutedEventArgs e)
        {
            LogUtil.Info("开始缓冲");
        }

        /// <summary>
        /// 缓冲结束事件处理
        /// </summary>
        private void OnBufferingEnded(object sender, RoutedEventArgs e)
        {
            LogUtil.Info("缓冲结束");
        }

        /// <summary>
        /// 位置定时器事件处理
        /// </summary>
        private void OnPositionTimerTick(object sender, EventArgs e)
        {
            if (!_isUserSeeking && _mediaElement.NaturalDuration.HasTimeSpan)
            {
                var position = Position;
                var duration = Duration;
                PositionChanged?.Invoke(this, new PositionChangedEventArgs(position, duration));
            }
        }

        #endregion
    }
}