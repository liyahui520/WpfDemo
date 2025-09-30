using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using WpfAppNew.Controlls;
using Tools.Extend;

namespace WpfAppNew.Windows
{
    /// <summary>
    /// 视频播放器测试窗口
    /// 用于对比VLC播放器和OpenCV播放器的性能和功能
    /// </summary>
    public partial class TestVideoPlayerWindow : Window
    {
        #region 私有字段

        /// <summary>
        /// VLC播放器实例
        /// </summary>
        private UCLocalVideo _vlcPlayer;

        /// <summary>
        /// OpenCV播放器实例
        /// </summary>
        private UCOpenCvVideoPlayer _openCvPlayer;

        /// <summary>
        /// 当前视频文件路径
        /// </summary>
        private string _currentVideoPath;

        /// <summary>
        /// 性能监控
        /// </summary>
        private DispatcherTimer _performanceTimer;
        private Process _currentProcess;
        private long _initialMemory;
        private DateTime _testStartTime;

        #endregion

        #region 构造函数和初始化

        /// <summary>
        /// 初始化测试窗口
        /// </summary>
        public TestVideoPlayerWindow()
        {
            InitializeComponent();
            InitializeTestEnvironment();
        }

        /// <summary>
        /// 初始化测试环境
        /// </summary>
        private void InitializeTestEnvironment()
        {
            try
            {
                // 获取当前进程用于性能监控
                _currentProcess = Process.GetCurrentProcess();
                _initialMemory = _currentProcess.WorkingSet64;

                // 初始化性能监控定时器
                _performanceTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1)
                };
                _performanceTimer.Tick += PerformanceTimer_Tick;

                // 创建VLC播放器
                try
                {
                    _vlcPlayer = new UCLocalVideo();
                    _vlcPlayer.Width = vlcPlayerContainer.Width;
                    _vlcPlayer.Height = vlcPlayerContainer.Height;
                    vlcPlayerContainer.Child = _vlcPlayer;
                    vlcInfoText.Text = "VLC: 已初始化";
                }
                catch (Exception ex)
                {
                    vlcInfoText.Text = $"VLC: 初始化失败 - {ex.Message}";
                    LogUtil.Error($"VLC播放器初始化失败: {ex.Message}");
                }

                // 创建OpenCV播放器
                try
                {
                    _openCvPlayer = new UCOpenCvVideoPlayer();
                    _openCvPlayer.Width = openCvPlayerContainer.Width;
                    _openCvPlayer.Height = openCvPlayerContainer.Height;
                    openCvPlayerContainer.Child = _openCvPlayer;
                    openCvInfoText.Text = "OpenCV: 已初始化";
                }
                catch (Exception ex)
                {
                    openCvInfoText.Text = $"OpenCV: 初始化失败 - {ex.Message}";
                    LogUtil.Error($"OpenCV播放器初始化失败: {ex.Message}");
                }

                LogUtil.Info("视频播放器测试环境初始化完成");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"测试环境初始化失败: {ex.Message}");
                MessageBox.Show($"测试环境初始化失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 选择文件按钮点击事件
        /// </summary>
        private async void SelectFileButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "视频文件|*.mp4;*.avi;*.mkv;*.mov;*.flv;*.wmv;*.m4v;*.3gp;*.webm|所有文件|*.*",
                Title = "选择测试视频文件"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _currentVideoPath = openFileDialog.FileName;
                var fileName = Path.GetFileName(_currentVideoPath);

                try
                {
                    // 重置性能监控
                    _testStartTime = DateTime.Now;
                    _initialMemory = _currentProcess.WorkingSet64;

                    // 加载到VLC播放器
                    if (_vlcPlayer != null)
                    {
                        vlcInfoText.Text = "VLC: 加载中...";
                        _vlcPlayer.InitVodio(_currentVideoPath);
                        vlcInfoText.Text = $"VLC: 已加载 {fileName}";
                    }

                    // 加载到OpenCV播放器
                    if (_openCvPlayer != null)
                    {
                        openCvInfoText.Text = "OpenCV: 加载中...";
                        var success = await _openCvPlayer.LoadVideo(_currentVideoPath);
                        openCvInfoText.Text = success ? 
                            $"OpenCV: 已加载 {fileName}" : 
                            "OpenCV: 加载失败";
                    }

                    // 启动性能监控
                    _performanceTimer.Start();

                    LogUtil.Info($"测试视频文件已加载: {fileName}");
                }
                catch (Exception ex)
                {
                    LogUtil.Error($"加载视频文件失败: {ex.Message}");
                    MessageBox.Show($"加载视频文件失败: {ex.Message}", "错误", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 同时播放按钮点击事件
        /// </summary>
        private async void PlayBothButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentVideoPath))
            {
                MessageBox.Show("请先选择视频文件", "提示", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                // 同时开始播放
                var vlcTask = Task.Run(() =>
                {
                    if (_vlcPlayer != null)
                    {
                        Dispatcher.Invoke(() => _vlcPlayer.StartPlayback());
                    }
                });

                var openCvTask = _openCvPlayer?.StartPlayback();

                await Task.WhenAll(vlcTask, openCvTask ?? Task.CompletedTask);

                LogUtil.Info("两个播放器同时开始播放");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"同时播放失败: {ex.Message}");
                MessageBox.Show($"同时播放失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 同时暂停按钮点击事件
        /// </summary>
        private void PauseBothButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 同时暂停
                _vlcPlayer?.btnPlay_Click(null, null);
                _openCvPlayer?.PausePlayback();

                LogUtil.Info("两个播放器同时暂停");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"同时暂停失败: {ex.Message}");
                MessageBox.Show($"同时暂停失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 同时停止按钮点击事件
        /// </summary>
        private async void StopBothButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 同时停止
                var vlcTask = Task.Run(() =>
                {
                    if (_vlcPlayer != null)
                    {
                        Dispatcher.Invoke(async () => await _vlcPlayer.StopPlayback());
                    }
                });

                var openCvTask = _openCvPlayer?.StopPlayback();

                await Task.WhenAll(vlcTask, openCvTask ?? Task.CompletedTask);

                LogUtil.Info("两个播放器同时停止");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"同时停止失败: {ex.Message}");
                MessageBox.Show($"同时停止失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 性能监控定时器事件
        /// </summary>
        private void PerformanceTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                _currentProcess.Refresh();
                var currentMemory = _currentProcess.WorkingSet64;
                var memoryDiff = (currentMemory - _initialMemory) / 1024 / 1024; // MB
                var runTime = (DateTime.Now - _testStartTime).TotalSeconds;

                performanceText.Text = $"运行时间: {runTime:F1}s | 内存增长: {memoryDiff:F1}MB | CPU: {GetCpuUsage():F1}%";
            }
            catch (Exception ex)
            {
                LogUtil.Error($"性能监控更新失败: {ex.Message}");
            }
        }

        #endregion

        #region 性能监控方法

        /// <summary>
        /// 获取CPU使用率
        /// </summary>
        /// <returns>CPU使用率百分比</returns>
        private double GetCpuUsage()
        {
            try
            {
                return _currentProcess.TotalProcessorTime.TotalMilliseconds / 
                       Environment.ProcessorCount / 
                       Environment.TickCount * 100;
            }
            catch
            {
                return 0;
            }
        }

        #endregion

        #region 窗口生命周期

        /// <summary>
        /// 窗口关闭事件
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            try
            {
                // 停止性能监控
                _performanceTimer?.Stop();
                _performanceTimer = null;

                // 释放播放器资源
                _vlcPlayer?.Close();
                _openCvPlayer?.Close();

                LogUtil.Info("视频播放器测试窗口已关闭");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"关闭测试窗口时发生异常: {ex.Message}");
            }

            base.OnClosed(e);
        }

        #endregion

        #region 测试报告生成

        /// <summary>
        /// 生成测试报告
        /// </summary>
        /// <returns>测试报告内容</returns>
        public string GenerateTestReport()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== 视频播放器对比测试报告 ===");
            report.AppendLine($"测试时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine($"测试文件: {Path.GetFileName(_currentVideoPath)}");
            report.AppendLine();

            // VLC播放器信息
            report.AppendLine("VLC播放器:");
            report.AppendLine($"  状态: {vlcInfoText.Text}");
            report.AppendLine($"  优点: 成熟稳定，支持格式广泛，音视频同步好");
            report.AppendLine($"  缺点: 依赖外部库，集成复杂，资源占用较高");
            report.AppendLine();

            // OpenCV播放器信息
            report.AppendLine("OpenCV播放器:");
            report.AppendLine($"  状态: {openCvInfoText.Text}");
            report.AppendLine($"  优点: 轻量级，易于集成，性能优化好，内存管理佳");
            report.AppendLine($"  缺点: 音频支持需要额外集成，支持格式相对有限");
            report.AppendLine();

            // 性能对比
            report.AppendLine("性能对比:");
            report.AppendLine($"  {performanceText.Text}");
            report.AppendLine();

            // 建议
            report.AppendLine("使用建议:");
            report.AppendLine("  - 对于简单的视频播放需求，推荐使用OpenCV播放器");
            report.AppendLine("  - 对于复杂的多媒体应用，可考虑VLC播放器");
            report.AppendLine("  - OpenCV播放器在宠物行业应用中具有更好的性能表现");

            return report.ToString();
        }

        #endregion
    }
}