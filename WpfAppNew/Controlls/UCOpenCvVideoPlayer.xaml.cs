using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using OpenCvSharp.WpfExtensions;
using Tools.Extend;

namespace WpfAppNew.Controlls
{
    /// <summary>
    /// 基于OpenCvSharp的高性能视频播放器
    /// 支持多种视频格式，具有优化的内存管理和渲染性能
    /// </summary>
    public partial class UCOpenCvVideoPlayer
    {
        #region 私有字段

        /// <summary>
        /// OpenCV视频捕获对象
        /// </summary>
        private VideoCapture _videoCapture;

        /// <summary>
        /// 播放控制相关
        /// </summary>
        private bool _isPlaying;
        private bool _isPaused;
        private bool _isDisposed;
        private bool _isDraggingProgress;
        private bool _wasPlayingBeforeDrag;

        /// <summary>
        /// 视频信息
        /// </summary>
        private string _currentFilePath;
        private double _totalFrames;
        private double _fps;
        private int _videoWidth;
        private int _videoHeight;
        private double _totalDurationMs;

        /// <summary>
        /// 线程和定时器
        /// </summary>
        private Thread _playbackThread;
        private DispatcherTimer _uiUpdateTimer;
        private CancellationTokenSource _cancellationTokenSource;

        /// <summary>
        /// 性能监控
        /// </summary>
        private DateTime _lastFrameTime;
        private int _frameCount;
        private double _currentFps;

        /// <summary>
        /// 线程同步锁
        /// </summary>
        private readonly object _lockObject = new object();

        /// <summary>
        /// 音频播放辅助类
        /// </summary>
        private AudioPlayerHelper _audioPlayer;

        #endregion

        #region 构造函数和初始化

        /// <summary>
        /// 初始化视频播放器
        /// </summary>
        /// <param name="filePath">视频文件路径（可选）</param>
        public UCOpenCvVideoPlayer(string filePath = null)
        {
            InitializeComponent();
            InitializePlayer();
            
            if (!string.IsNullOrEmpty(filePath))
            {
                // 延迟加载视频直到窗口完全加载
                this.Loaded += async (s, e) => 
                {
                    await Task.Delay(500); // 等待布局完成
                    await LoadVideo(filePath);
                };
            }
        }

        /// <summary>
        /// 初始化播放器组件
        /// </summary>
        private void InitializePlayer()
        {
            _isDisposed = false;
            _isPlaying = false;
            _isPaused = false;
            _isDraggingProgress = false;
            
            // 初始化UI更新定时器
            _uiUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100) // 10fps UI更新
            };
            _uiUpdateTimer.Tick += UiUpdateTimer_Tick;
            
            // 初始化音频播放器
            _audioPlayer = new AudioPlayerHelper();
            _audioPlayer.PlaybackCompleted += AudioPlayer_PlaybackCompleted;
            
            // 设置初始状态
            UpdatePlayButtonState(false);
            UpdateTimeDisplay(0, 0);
            
            LogUtil.Info("OpenCV视频播放器初始化完成");
        }

        #endregion

        #region 视频加载和播放控制

        /// <summary>
        /// 加载视频文件
        /// </summary>
        /// <param name="filePath">视频文件路径</param>
        public async Task<bool> LoadVideo(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                LogUtil.Error($"视频文件不存在: {filePath}");
                return false;
            }

            try
            {
                // 显示加载指示器
                ShowLoadingIndicator(true, "正在加载视频...");
                
                // 停止当前播放
                await StopPlayback();
                
                // 在后台线程加载视频
                return await Task.Run(async () =>
                {
                    try
                    {
                        // 释放之前的资源
                        _videoCapture?.Release();
                        _videoCapture?.Dispose();
                        
                        // 创建新的视频捕获对象
                        _videoCapture = new VideoCapture(filePath);
                        
                        if (!_videoCapture.IsOpened())
                        {
                            throw new Exception("无法打开视频文件");
                        }
                        
                        // 获取视频信息
                        _totalFrames = _videoCapture.Get(VideoCaptureProperties.FrameCount);
                        _fps = _videoCapture.Get(VideoCaptureProperties.Fps);
                        _videoWidth = (int)_videoCapture.Get(VideoCaptureProperties.FrameWidth);
                        _videoHeight = (int)_videoCapture.Get(VideoCaptureProperties.FrameHeight);
                        _totalDurationMs = (_totalFrames / _fps) * 1000;
                        
                        _currentFilePath = filePath;
                        
                        // 尝试加载对应的音频文件
                        var audioPath = _audioPlayer.GetAudioPathFromVideo(filePath);
                        if (!string.IsNullOrEmpty(audioPath))
                        {
                            await _audioPlayer.LoadAudio(audioPath);
                            LogUtil.Info($"已加载对应音频文件: {Path.GetFileName(audioPath)}");
                        }
                        else
                        {
                            LogUtil.Info("未找到对应的音频文件，将静音播放");
                        }

                        // 更新UI
                        Dispatcher.Invoke(() =>
                        {
                            titleText.Text = $"OpenCV 视频播放器 - {Path.GetFileName(filePath)}";
                            resolutionText.Text = $"分辨率: {_videoWidth}x{_videoHeight}";
                            UpdateTimeDisplay(0, _totalDurationMs);
                            progressSlider.Maximum = _totalFrames;
                            progressSlider.Value = 0;
                            
                            ShowLoadingIndicator(false);
                            UpdatePlayButtonState(false);
                        });
                        
                        LogUtil.Info($"视频加载成功: {filePath}, 分辨率: {_videoWidth}x{_videoHeight}, FPS: {_fps:F2}, 总帧数: {_totalFrames}");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        LogUtil.Error($"加载视频失败: {ex.Message}");
                        
                        Dispatcher.Invoke(() =>
                        {
                            ShowLoadingIndicator(false);
                            MessageBox.Show($"加载视频失败: {ex.Message}", "错误", 
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                        
                        return false;
                    }
                });
            }
            catch (Exception ex)
            {
                LogUtil.Error($"LoadVideo异常: {ex.Message}");
                ShowLoadingIndicator(false);
                return false;
            }
        }

        /// <summary>
        /// 开始播放
        /// </summary>
        public async Task StartPlayback()
        {
            if (_videoCapture == null || !_videoCapture.IsOpened() || _isPlaying)
                return;

            try
            {
                _cancellationTokenSource = new CancellationTokenSource();
                
                lock (_lockObject)
                {
                    _isPlaying = true;
                    _isPaused = false;
                }
                
                // 启动播放线程
                _playbackThread = new Thread(() => PlaybackLoop(_cancellationTokenSource.Token))
                {
                    IsBackground = true,
                    Name = "VideoPlaybackThread"
                };
                _playbackThread.Start();
                
                // 启动UI更新定时器
                _uiUpdateTimer.Start();
                
                // 同步启动音频播放
                if (_audioPlayer != null)
                {
                    await _audioPlayer.PlayAsync();
                }
                
                UpdatePlayButtonState(true);
                LogUtil.Info("视频播放开始");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"开始播放失败: {ex.Message}");
                lock (_lockObject)
                {
                    _isPlaying = false;
                }
            }
        }

        /// <summary>
        /// 暂停播放
        /// </summary>
        public void PausePlayback()
        {
            if (!_isPlaying) return;
            
            lock (_lockObject)
            {
                _isPaused = !_isPaused;
            }
            
            // 同步音频暂停/继续
            if (_audioPlayer != null)
            {
                _audioPlayer.Pause();
            }
            
            UpdatePlayButtonState(_isPlaying && !_isPaused);
            ShowPlayStateOverlay(_isPaused ? "&#xE768;" : null); // 显示播放图标或隐藏
            
            LogUtil.Info($"视频播放{(_isPaused ? "暂停" : "继续")}");
        }

        /// <summary>
        /// 停止播放
        /// </summary>
        public async Task StopPlayback()
        {
            try
            {
                // 停止播放标志
                lock (_lockObject)
                {
                    _isPlaying = false;
                    _isPaused = false;
                }
                
                // 取消播放线程
                _cancellationTokenSource?.Cancel();
                
                // 等待播放线程结束
                if (_playbackThread != null && _playbackThread.IsAlive)
                {
                    if (!_playbackThread.Join(1000)) // 等待1秒
                    {
                        _playbackThread.Abort(); // 强制终止
                    }
                }
                
                // 停止UI更新定时器
                _uiUpdateTimer?.Stop();
                
                // 同步停止音频播放
                if (_audioPlayer != null)
                {
                    await _audioPlayer.StopAsync();
                }
                
                // 重置到开始位置
                _videoCapture?.Set(VideoCaptureProperties.PosFrames, 0);
                
                // 更新UI
                Dispatcher.Invoke(() =>
                {
                    progressSlider.Value = 0;
                    UpdateTimeDisplay(0, _totalDurationMs);
                    UpdatePlayButtonState(false);
                    ShowPlayStateOverlay(null);
                });
                
                LogUtil.Info("视频播放停止");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"停止播放失败: {ex.Message}");
            }
        }

        #endregion

        #region 播放循环和帧渲染

        /// <summary>
        /// 视频播放主循环
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        private void PlaybackLoop(CancellationToken cancellationToken)
        {
            var frameInterval = TimeSpan.FromMilliseconds(1000.0 / _fps);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                while (!cancellationToken.IsCancellationRequested && _isPlaying)
                {
                    lock (_lockObject)
                    {
                        if (_isPaused)
                        {
                            Thread.Sleep(50); // 暂停时减少CPU使用
                            continue;
                        }
                    }
                    
                    var frameStart = stopwatch.Elapsed;
                    
                    // 读取下一帧
                    using (var frame = new Mat())
                    {
                        bool readSuccess = _videoCapture.Read(frame);
                        if (!readSuccess || frame.Empty())
                        {
                            LogUtil.Info($"视频读取结束: readSuccess={readSuccess}, frameEmpty={frame.Empty()}");
                            // 视频播放结束
                            Dispatcher.BeginInvoke(new Action(async () => await OnPlaybackCompleted()));
                            break;
                        }
                        
                        // 渲染帧到UI
                        RenderFrame(frame);
                        
                        // 更新性能统计
                        UpdatePerformanceStats();
                    }
                    
                    // 帧率控制
                    var frameTime = stopwatch.Elapsed - frameStart;
                    var sleepTime = frameInterval - frameTime;
                    
                    if (sleepTime > TimeSpan.Zero)
                    {
                        Thread.Sleep(sleepTime);
                    }
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"播放循环异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 安全的Mat到BitmapSource转换方法
        /// 解决OpenCvSharp.WpfExtensions.ToBitmapSource()的堆损坏问题
        /// </summary>
        /// <param name="mat">要转换的Mat对象</param>
        /// <returns>安全转换的BitmapSource</returns>
        private BitmapSource SafeMatToBitmapSource(Mat mat)
        {
            if (mat == null || mat.Empty())
                return null;

            try
            {
                // 确保Mat格式正确
                Mat convertedMat = null;
                try
                {
                    // 根据通道数进行格式转换
                    if (mat.Channels() == 1)
                    {
                        // 灰度图转RGB
                        convertedMat = new Mat();
                        Cv2.CvtColor(mat, convertedMat, ColorConversionCodes.GRAY2RGB);
                    }
                    else if (mat.Channels() == 3)
                    {
                        // BGR转RGB
                        convertedMat = new Mat();
                        Cv2.CvtColor(mat, convertedMat, ColorConversionCodes.BGR2RGB);
                    }
                    else if (mat.Channels() == 4)
                    {
                        // BGRA转RGBA
                        convertedMat = new Mat();
                        Cv2.CvtColor(mat, convertedMat, ColorConversionCodes.BGRA2RGBA);
                    }
                    else
                    {
                        convertedMat = mat.Clone();
                    }

                    // 使用安全的方式创建BitmapSource
                    var width = convertedMat.Width;
                    var height = convertedMat.Height;
                    var stride = width * convertedMat.Channels();
                    
                    // 创建字节数组副本，避免直接使用Mat的内存指针
                    var imageData = new byte[height * stride];
                    System.Runtime.InteropServices.Marshal.Copy(convertedMat.Data, imageData, 0, imageData.Length);

                    // 确定像素格式
                    PixelFormat pixelFormat;
                    switch (convertedMat.Channels())
                    {
                        case 1:
                            pixelFormat = PixelFormats.Gray8;
                            break;
                        case 3:
                            pixelFormat = PixelFormats.Rgb24;
                            break;
                        //case 4:
                        //    pixelFormat = PixelFormats.Rgba32;
                        //    break;
                        default:
                            throw new NotSupportedException($"不支持的通道数: {convertedMat.Channels()}");
                    }

                    // 创建BitmapSource
                    var bitmapSource = BitmapSource.Create(
                        width, height,
                        96, 96, // DPI
                        pixelFormat,
                        null, // palette
                        imageData,
                        stride);

                    // 冻结以提高性能和线程安全
                    if (bitmapSource.CanFreeze)
                    {
                        bitmapSource.Freeze();
                    }

                    return bitmapSource;
                }
                finally
                {
                    // 释放临时Mat对象
                    if (convertedMat != null && convertedMat != mat)
                    {
                        convertedMat.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"SafeMatToBitmapSource转换失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 渲染视频帧到UI
        /// </summary>
        /// <param name="frame">OpenCV帧</param>
        private void RenderFrame(Mat frame)
        {
            try
            {
                if (frame.Empty() || _isDisposed)
                {
                    LogUtil.Info("帧为空或播放器已释放，跳过渲染");
                    return;
                }
                
                // 只在每30帧输出一次渲染信息，避免日志过多
                if (_frameCount % 30 == 0)
                {
                    LogUtil.Info($"开始渲染帧: {frame.Width}x{frame.Height}, Type: {frame.Type()}, Channels: {frame.Channels()}");
                }
                
                // 转换为WPF可显示的格式
                BitmapSource bitmap = null;
                try
                {
                    // 使用安全的转换方法
                    bitmap = SafeMatToBitmapSource(frame);
                    
                    // 只在每30帧输出一次BitmapSource信息
                    if (_frameCount % 30 == 0)
                    {
                        LogUtil.Info($"BitmapSource创建成功: {bitmap?.PixelWidth}x{bitmap?.PixelHeight}, Format: {bitmap?.Format}");
                    }
                }
                catch (Exception bitmapEx)
                {
                    LogUtil.Error($"ToBitmapSource转换失败: {bitmapEx.Message}");
                    
                    // 尝试备用转换方法
                    try
                    {
                        using (var bitmap2 = frame.ToBitmap())
                         {
                             var imageSource = bitmap2.BitmapToImageSource();
                             bitmap = imageSource as BitmapSource;
                             LogUtil.Info($"备用转换方法成功: {bitmap?.PixelWidth}x{bitmap?.PixelHeight}");
                         }
                    }
                    catch (Exception fallbackEx)
                    {
                        LogUtil.Error($"备用转换方法也失败: {fallbackEx.Message}");
                        return;
                    }
                }
                
                // 在UI线程更新图像
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!_isDisposed && bitmap != null)
                    {
                        videoImage.Source = bitmap;
                        
                        // 只在第一次设置时确保可见性
                        if (videoImage.Visibility != System.Windows.Visibility.Visible)
                        {
                            videoImage.Visibility = System.Windows.Visibility.Visible;
                            LogUtil.Info("设置videoImage为可见");
                        }
                        
                        if (videoContainer.Visibility != System.Windows.Visibility.Visible)
                        {
                            videoContainer.Visibility = System.Windows.Visibility.Visible;
                            LogUtil.Info("设置videoContainer为可见");
                        }
                        
                        // 移除频繁的UpdateLayout调用，让WPF自然处理布局更新
                        // 只在调试模式下每10帧输出一次详细信息
                        _frameCount++;
                        if (_frameCount % 10 == 0)
                        {
                            LogUtil.Info($"视频帧已更新 (第{_frameCount}帧): {videoImage.ActualWidth}x{videoImage.ActualHeight}, 容器: {videoContainer.ActualWidth}x{videoContainer.ActualHeight}");
                        }
                    }
                    else
                    {
                        LogUtil.Error($"无法设置图像: _isDisposed={_isDisposed}, bitmap={bitmap != null}");
                    }
                }), DispatcherPriority.Normal);
            }
            catch (Exception ex)
            {
                LogUtil.Error($"帧渲染失败: {ex.Message}");
                LogUtil.Error($"异常堆栈: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// 更新性能统计
        /// </summary>
        private void UpdatePerformanceStats()
        {
            _frameCount++;
            var now = DateTime.Now;
            
            if ((now - _lastFrameTime).TotalSeconds >= 1.0)
            {
                _currentFps = _frameCount / (now - _lastFrameTime).TotalSeconds;
                _frameCount = 0;
                _lastFrameTime = now;
                
                // 更新FPS显示
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!_isDisposed)
                    {
                        fpsText.Text = $"FPS: {_currentFps:F1}";
                    }
                }), DispatcherPriority.Background);
            }
        }

        #endregion

        #region UI事件处理

        /// <summary>
        /// 打开文件按钮点击事件
        /// </summary>
        private async void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "视频文件|*.mp4;*.avi;*.mkv;*.mov;*.flv;*.wmv;*.m4v;*.3gp;*.webm|所有文件|*.*",
                Title = "选择视频文件"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                await LoadVideo(openFileDialog.FileName);
            }
        }

        /// <summary>
        /// 播放/暂停按钮点击事件
        /// </summary>
        private async void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (_videoCapture == null || !_videoCapture.IsOpened())
            {
                MessageBox.Show("请先加载视频文件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_isPlaying)
            {
                PausePlayback();
            }
            else
            {
                await StartPlayback();
            }
        }

        /// <summary>
        /// 停止按钮点击事件
        /// </summary>
        private async void StopButton_Click(object sender, RoutedEventArgs e)
        {
            await StopPlayback();
        }

        /// <summary>
        /// 视频区域点击事件
        /// </summary>
        private async void VideoArea_MouseClick(object sender, MouseButtonEventArgs e)
        {
            if (_videoCapture == null || !_videoCapture.IsOpened())
                return;
                
            if (_isPlaying)
            {
                PausePlayback();
            }
            else
            {
                await StartPlayback();
            }
        }

        /// <summary>
        /// 音量滑块值改变事件
        /// </summary>
        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_audioPlayer != null)
            {
                _audioPlayer.Volume = (float)(e.NewValue / 100.0);
            }
            LogUtil.Info($"音量设置为: {e.NewValue}%");
        }

        #endregion

        #region 进度条控制

        /// <summary>
        /// 进度条鼠标按下事件
        /// </summary>
        private void ProgressSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_videoCapture == null || !_videoCapture.IsOpened())
                return;

            _wasPlayingBeforeDrag = _isPlaying && !_isPaused;
            if (_wasPlayingBeforeDrag)
            {
                PausePlayback();
            }
            
            _isDraggingProgress = true;
            UpdateProgressFromMousePosition(e);
        }

        /// <summary>
        /// 进度条鼠标移动事件
        /// </summary>
        private void ProgressSlider_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingProgress)
            {
                UpdateProgressFromMousePosition(e);
            }
        }

        /// <summary>
        /// 进度条鼠标释放事件
        /// </summary>
        private async void ProgressSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDraggingProgress)
                return;

            try
            {
                UpdateProgressFromMousePosition(e);
                
                // 设置视频位置
                var frameNumber = progressSlider.Value;
                _videoCapture?.Set(VideoCaptureProperties.PosFrames, frameNumber);
                
                // 同步音频位置
                if (_audioPlayer != null)
                {
                    var currentTimeMs = (frameNumber / _fps) * 1000;
                    _audioPlayer.SetPosition(currentTimeMs);
                }
                
                // 恢复播放状态
                if (_wasPlayingBeforeDrag)
                {
                    await StartPlayback();
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"进度调整失败: {ex.Message}");
            }
            finally
            {
                _isDraggingProgress = false;
            }
        }

        /// <summary>
        /// 根据鼠标位置更新进度
        /// </summary>
        private void UpdateProgressFromMousePosition(MouseEventArgs e)
        {
            var slider = progressSlider;
            var position = e.GetPosition(slider);
            var percentage = position.X / slider.ActualWidth;
            percentage = Math.Max(0, Math.Min(1, percentage));
            
            var frameNumber = percentage * _totalFrames;
            slider.Value = frameNumber;
            
            var currentTimeMs = (frameNumber / _fps) * 1000;
            UpdateTimeDisplay(currentTimeMs, _totalDurationMs);
        }

        #endregion

        #region UI更新方法

        /// <summary>
        /// UI更新定时器事件
        /// </summary>
        private void UiUpdateTimer_Tick(object sender, EventArgs e)
        {
            if (_videoCapture == null || !_videoCapture.IsOpened() || _isDraggingProgress)
                return;

            try
            {
                var currentFrame = _videoCapture.Get(VideoCaptureProperties.PosFrames);
                var currentTimeMs = (currentFrame / _fps) * 1000;
                
                progressSlider.Value = currentFrame;
                UpdateTimeDisplay(currentTimeMs, _totalDurationMs);
            }
            catch (Exception ex)
            {
                LogUtil.Error($"UI更新失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新播放按钮状态
        /// </summary>
        private void UpdatePlayButtonState(bool isPlaying)
        {
            playButtonIcon.Text = isPlaying ? "&#xE769;" : "&#xE768;"; // 暂停图标 : 播放图标
            playButton.ToolTip = isPlaying ? "暂停" : "播放";
        }

        /// <summary>
        /// 更新时间显示
        /// </summary>
        private void UpdateTimeDisplay(double currentMs, double totalMs)
        {
            var current = TimeSpan.FromMilliseconds(currentMs);
            var total = TimeSpan.FromMilliseconds(totalMs);
            
            currentTimeText.Text = current.ToString(@"mm\:ss");
            totalTimeText.Text = total.ToString(@"mm\:ss");
        }

        /// <summary>
        /// 显示/隐藏加载指示器
        /// </summary>
        private void ShowLoadingIndicator(bool show, string message = "")
        {
            loadingIndicator.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (show && !string.IsNullOrEmpty(message))
            {
                loadingText.Text = message;
            }
        }

        /// <summary>
        /// 显示播放状态覆盖层
        /// </summary>
        private void ShowPlayStateOverlay(string icon)
        {
            if (string.IsNullOrEmpty(icon))
            {
                playStateOverlay.Visibility = Visibility.Collapsed;
            }
            else
            {
                playStateIcon.Text = icon;
                playStateOverlay.Visibility = Visibility.Visible;
                
                // 2秒后自动隐藏
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                timer.Tick += (s, e) =>
                {
                    playStateOverlay.Visibility = Visibility.Collapsed;
                    timer.Stop();
                };
                timer.Start();
            }
        }

        #endregion

        #region 窗口控制事件

        /// <summary>
        /// 窗口拖动
        /// </summary>
        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        /// <summary>
        /// 最大化/还原按钮
        /// </summary>
        private void MaxButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        /// <summary>
        /// 关闭按钮
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        #endregion

        #region 音频事件处理

        /// <summary>
        /// 音频播放完成事件处理
        /// </summary>
        private void AudioPlayer_PlaybackCompleted(object sender, EventArgs e)
        {
            LogUtil.Info("音频播放完成");
            // 音频播放完成时，可以选择是否停止视频播放
            // 这里保持视频继续播放，因为有些视频可能没有音频
        }

        #endregion

        #region 播放完成和资源清理

        /// <summary>
        /// 播放完成事件处理
        /// </summary>
        private async Task OnPlaybackCompleted()
        {
            await StopPlayback();
            ShowPlayStateOverlay("&#xE768;"); // 显示播放图标
            LogUtil.Info("视频播放完成");
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            _isDisposed = true;
            
            try
            {
                // 停止播放
                StopPlayback().Wait(2000);
                
                // 释放OpenCV资源
                _videoCapture?.Release();
                _videoCapture?.Dispose();
                
                // 停止定时器
                _uiUpdateTimer?.Stop();
                _uiUpdateTimer = null;
                
                // 取消令牌
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                
                // 释放音频播放器
                _audioPlayer?.Dispose();
                _audioPlayer = null;
                
                LogUtil.Info("OpenCV视频播放器资源已释放");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"资源释放异常: {ex.Message}");
            }
            
            base.OnClosed(e);
        }

        #endregion
    }
}