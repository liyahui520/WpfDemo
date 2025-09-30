using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Tools.Extend;
using WpfAppNew.OpenCv.Core;
using WpfAppNew.OpenCV.Controls; 

namespace WpfAppNew.Windows
{
    /// <summary>
    /// 性能监控窗口
    /// </summary>
    public partial class PerformanceMonitorWindow
    {
        #region 私有字段

        /// <summary>
        /// 性能计数器
        /// </summary>
        private PerformanceCounter _cpuCounter;
        private PerformanceCounter _memoryCounter;

        /// <summary>
        /// 定时器
        /// </summary>
        private DispatcherTimer _updateTimer;

        /// <summary>
        /// 摄像头预览控件引用
        /// </summary>
        private CameraPreviewControl _cameraPreview;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化PerformanceMonitorWindow
        /// </summary>
        public PerformanceMonitorWindow(CameraPreviewControl cameraPreview)
        {
            InitializeComponent();
            _cameraPreview = cameraPreview;
            
            // 订阅窗口关闭事件
            this.Closing += PerformanceMonitorWindow_Closing;
            
            InitializeSystem();
        }

        #endregion

        #region 初始化方法

        /// <summary>
        /// 初始化系统
        /// </summary>
        private void InitializeSystem()
        {
            try
            {
                // 初始化性能计数器
                InitializePerformanceCounters();

                // 初始化定时器
                InitializeTimers();

                // 订阅摄像头事件
                if (_cameraPreview != null)
                {
                    _cameraPreview.PerformanceStats += OnPerformanceStats;
                    _cameraPreview.ErrorOccurred += OnErrorOccurred;
                }

                AddLog("性能监控系统初始化完成");
            }
            catch (Exception ex)
            {
                AddLog($"性能监控系统初始化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 初始化性能计数器
        /// </summary>
        private void InitializePerformanceCounters()
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");

                // 预热计数器
                _cpuCounter.NextValue();
            }
            catch (Exception ex)
            {
                AddLog($"性能计数器初始化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 初始化定时器
        /// </summary>
        private void InitializeTimers()
        {
            // UI更新定时器
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _updateTimer.Tick += UpdateTimer_Tick;
            _updateTimer.Start();
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 更新定时器事件
        /// </summary>
        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            UpdatePerformanceUI();
        }

        /// <summary>
        /// 性能统计事件处理
        /// </summary>
        private void OnPerformanceStats(object sender, PerformanceStatsEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                FpsText.Text = $"{e.Fps:F1}";
            }));
        }

        /// <summary>
        /// 错误事件处理
        /// </summary>
        private void OnErrorOccurred(object sender, ErrorOccurredEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                AddLog($"错误: {e.Message} - {e.Exception?.Message}");
                // 更新错误统计
                UpdateErrorStatistics();
            }));
        }

        /// <summary>
        /// 清空日志按钮点击事件
        /// </summary>
        private void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            LogTextBox.Clear();
            AddLog("日志已清空");
        }

        /// <summary>
        /// 保存日志按钮点击事件
        /// </summary>
        private void SaveLogButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Title = "保存日志文件",
                    Filter = "文本文件|*.txt|所有文件|*.*",
                    DefaultExt = "txt",
                    FileName = $"性能监控日志_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    File.WriteAllText(saveDialog.FileName, LogTextBox.Text);
                    AddLog($"日志已保存到: {saveDialog.FileName}");
                }
            }
            catch (Exception ex)
            {
                AddLog($"保存日志失败: {ex.Message}");
            }
        }

        #endregion

        #region UI更新方法

        /// <summary>
        /// 更新性能UI
        /// </summary>
        private void UpdatePerformanceUI()
        {
            try
            {
                if (_cpuCounter != null)
                {
                    var cpuUsage = _cpuCounter.NextValue();
                    CpuProgressBar.Value = cpuUsage;
                    CpuText.Text = $"{cpuUsage:F1}%";
                }

                if (_memoryCounter != null)
                {
                    var availableMemory = _memoryCounter.NextValue();
                    var totalMemory = 8192; // 假设8GB内存
                    var usedMemory = totalMemory - availableMemory;
                    var memoryUsage = (usedMemory / totalMemory) * 100;

                    MemoryProgressBar.Value = memoryUsage;
                    MemoryText.Text = $"{usedMemory:F0} MB";
                }

                // 更新错误统计
                UpdateErrorStatistics();
            }
            catch (Exception ex)
            {
                // 忽略性能计数器错误
                LogUtil.Error($"更新性能UI失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新错误统计显示
        /// </summary>
        private void UpdateErrorStatistics()
        {
            try
            {
                // 简化错误统计显示
                TotalErrorsText.Text = "0";
                CriticalErrorsText.Text = "0";

                // 根据摄像头状态更新系统状态文本
                if (_cameraPreview?.IsCapturing == true)
                {
                    SystemStatusText.Text = "正常运行";
                    SystemStatusText.Foreground = new SolidColorBrush(Colors.Green);
                    SystemHealthIndicator.Fill = new SolidColorBrush(Colors.Green);
                }
                else
                {
                    SystemStatusText.Text = "未运行";
                    SystemStatusText.Foreground = new SolidColorBrush(Colors.Orange);
                    SystemHealthIndicator.Fill = new SolidColorBrush(Colors.Orange);
                }
            }
            catch (Exception ex)
            {
                AddLog($"更新错误统计失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 添加日志
        /// </summary>
        /// <param name="message">日志消息</param>
        public void AddLog(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var logEntry = $"[{timestamp}] {message}\r\n";

            LogTextBox.AppendText(logEntry);
            LogTextBox.ScrollToEnd();
        }

        #endregion

        #region 窗口事件

        /// <summary>
        /// 窗口关闭事件处理
        /// </summary>
        private void PerformanceMonitorWindow_Closing(object sender, CancelEventArgs e)
        {
            try
            {
                // 停止定时器
                _updateTimer?.Stop();

                // 取消订阅事件
                if (_cameraPreview != null)
                {
                    _cameraPreview.PerformanceStats -= OnPerformanceStats;
                    _cameraPreview.ErrorOccurred -= OnErrorOccurred;
                }

                // 释放性能计数器
                _cpuCounter?.Dispose();
                _memoryCounter?.Dispose();

                AddLog("性能监控窗口已关闭");
            }
            catch (Exception ex)
            {
                // 忽略关闭时的错误
                LogUtil.Error($"更新性能UI失败: {ex.Message}");
            }
        }

        #endregion
    }
}