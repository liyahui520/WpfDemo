using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Tools.Extend;

namespace WpfAppNew.Windows
{
    /// <summary>
    /// 启动加载窗口
    /// 用于显示应用程序启动时的加载进度
    /// </summary>
    public partial class LoadingWindow : Window
    {
        /// <summary>
        /// 进度条控件引用
        /// </summary>
        private ProgressBar _progressBar;

        /// <summary>
        /// 状态文本控件引用
        /// </summary>
        private TextBlock _statusText;

        /// <summary>
        /// 初始化LoadingWindow实例
        /// </summary>
        public LoadingWindow()
        {
            InitializeComponent();
            InitializeControls();
            
            // 立即启动进度条动画
            StartProgressBarAnimation();
            
            // 立即显示窗口并激活
            this.Show();
            this.Activate();
            this.Topmost = true;
            
            this.Loaded += LoadingWindow_Loaded;
        }

        /// <summary>
        /// 初始化控件引用
        /// </summary>
        private void InitializeControls()
        {
            // 获取进度条控件引用
            _progressBar = this.FindName("LoadingProgressBar") as ProgressBar;
            _statusText = this.FindName("StatusText") as TextBlock;
        }

        /// <summary>
        /// 立即启动进度条动画
        /// </summary>
        private void StartProgressBarAnimation()
        {
            if (_progressBar != null)
            {
                // 直接设置为不确定模式，避免频繁切换
                _progressBar.IsIndeterminate = true;
            }
        }

        /// <summary>
        /// 窗口加载完成事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void LoadingWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 确保进度条动画正常启动（双重保险）
            if (_progressBar != null && !_progressBar.IsIndeterminate)
            {
                _progressBar.IsIndeterminate = true;
            }
        }

        /// <summary>
        /// 更新加载状态文本
        /// </summary>
        /// <param name="status">状态文本</param>
        public void UpdateStatus(string status)
        {
            // 使用更高优先级避免UI阻塞
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                if (_statusText != null)
                {
                    _statusText.Text = status;
                }
            }), DispatcherPriority.Send);
        }

        /// <summary>
        /// 设置进度条为确定模式并更新进度
        /// </summary>
        /// <param name="progress">进度值 (0-100)</param>
        public void SetProgress(double progress)
        {
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                if (_progressBar != null)
                {
                    _progressBar.IsIndeterminate = false;
                    _progressBar.Value = progress;
                }
            }));
        }

        /// <summary>
        /// 设置进度条为不确定模式
        /// </summary>
        public void SetIndeterminate()
        {
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                if (_progressBar != null)
                {
                    _progressBar.IsIndeterminate = true;
                }
            }));
        }

        /// <summary>
        /// 停止进度条动画
        /// </summary>
        public void StopProgressBar()
        {
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                if (_progressBar != null)
                {
                    _progressBar.IsIndeterminate = false;
                    _progressBar.Value = 0;
                }
            }));
        }

        /// <summary>
        /// 安全关闭加载窗口
        /// 保持进度条loading动画直到窗口关闭
        /// </summary>
        public void SafeClose()
        {
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                try
                {
                    // 保持进度条loading动画，不停止
                    // 更新状态为完成
                    if (_statusText != null)
                    {
                        _statusText.Text = "初始化完成";
                    }
                    
                    // 短暂延迟后关闭窗口
                    var timer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(200)
                    };
                    timer.Tick += (s, e) =>
                    {
                        timer.Stop();
                        this.Close();
                    };
                    timer.Start();
                }
                catch (Exception ex)
                {
                    // 如果出现异常，直接关闭窗口
                    LogUtil.Error($"LoadingWindow关闭时发生异常: {ex.Message}");
                    this.Close();
                }
            }));
        }

        /// <summary>
        /// 设置加载完成状态
        /// 保持进度条loading动画，只更新状态文本
        /// </summary>
        /// <param name="message">完成消息</param>
        public void SetCompleted(string message = "加载完成")
        {
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                // 保持进度条的loading动画状态，不设置为100%
                // 只更新状态文本
                if (_statusText != null)
                {
                    _statusText.Text = message;
                }
            }));
        }
    }
}