using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Tools.Extend;

namespace WpfAppNew.EmguPlugs
{
    /// <summary>
    /// 工业相机测试窗口
    /// 用于测试和演示工业相机控制功能，特别是显微镜相关功能
    /// </summary>
    /// <remarks>
    /// 主要功能：
    /// 1. 提供完整的工业相机测试环境
    /// 2. 实时显示系统状态和图像信息
    /// 3. 显微镜控制状态监控
    /// 4. 操作日志记录和显示
    /// 5. 快速操作按钮
    /// 6. 系统性能监控
    /// </remarks>
    public partial class IndustrialCameraTestWindow : Window, INotifyPropertyChanged
    {
        #region 私有字段

        /// <summary>
        /// 时间更新定时器
        /// </summary>
        private DispatcherTimer _timeUpdateTimer;

        /// <summary>
        /// 性能监控定时器
        /// </summary>
        private DispatcherTimer _performanceTimer;

        /// <summary>
        /// 日志文本构建器
        /// </summary>
        private StringBuilder _logBuilder;

        /// <summary>
        /// 最大日志行数
        /// </summary>
        private const int MaxLogLines = 1000;

        /// <summary>
        /// 当前日志行数
        /// </summary>
        private int _currentLogLines = 0;

        /// <summary>
        /// 是否已初始化
        /// </summary>
        private bool _isInitialized = false;

        /// <summary>
        /// CPU使用率
        /// </summary>
        private string _cpuUsage = "0%";

        /// <summary>
        /// 内存使用量
        /// </summary>
        private string _memoryUsage = "0 MB";

        /// <summary>
        /// 图像分辨率
        /// </summary>
        private string _imageResolution = "0 x 0";

        /// <summary>
        /// 帧率
        /// </summary>
        private string _frameRate = "0 FPS";

        /// <summary>
        /// 图像格式
        /// </summary>
        private string _imageFormat = "未知";

        /// <summary>
        /// 文件大小
        /// </summary>
        private string _fileSize = "0 KB";

        /// <summary>
        /// 连接状态
        /// </summary>
        private string _connectionStatus = "未连接";

        /// <summary>
        /// 预览状态
        /// </summary>
        private string _previewStatus = "未启动";

        /// <summary>
        /// 录制状态
        /// </summary>
        private string _recordingStatus = "未录制";

        /// <summary>
        /// 日志文本
        /// </summary>
        private string _logText = "";

        /// <summary>
        /// 状态消息
        /// </summary>
        private string _statusMessage = "就绪";

        #endregion

        #region 属性

        /// <summary>
        /// 当前时间
        /// </summary>
        public DateTime CurrentTime { get; private set; } = DateTime.Now;

        /// <summary>
        /// 系统时间字符串
        /// 用于XAML绑定显示格式化的系统时间
        /// </summary>
        public string SystemTime => CurrentTime.ToString("yyyy-MM-dd HH:mm:ss");

        /// <summary>
        /// CPU使用率
        /// </summary>
        public string CpuUsage
        {
            get => _cpuUsage;
            set
            {
                if (_cpuUsage != value)
                {
                    _cpuUsage = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 内存使用量
        /// </summary>
        public string MemoryUsage
        {
            get => _memoryUsage;
            set
            {
                if (_memoryUsage != value)
                {
                    _memoryUsage = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 图像分辨率
        /// </summary>
        public string ImageResolution
        {
            get => _imageResolution;
            set
            {
                if (_imageResolution != value)
                {
                    _imageResolution = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 帧率
        /// </summary>
        public string FrameRate
        {
            get => _frameRate;
            set
            {
                if (_frameRate != value)
                {
                    _frameRate = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 图像格式
        /// </summary>
        public string ImageFormat
        {
            get => _imageFormat;
            set
            {
                if (_imageFormat != value)
                {
                    _imageFormat = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 文件大小
        /// </summary>
        public string FileSize
        {
            get => _fileSize;
            set
            {
                if (_fileSize != value)
                {
                    _fileSize = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 连接状态
        /// </summary>
        public string ConnectionStatus
        {
            get => _connectionStatus;
            set
            {
                if (_connectionStatus != value)
                {
                    _connectionStatus = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 预览状态
        /// </summary>
        public string PreviewStatus
        {
            get => _previewStatus;
            set
            {
                if (_previewStatus != value)
                {
                    _previewStatus = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 录制状态
        /// </summary>
        public string RecordingStatus
        {
            get => _recordingStatus;
            set
            {
                if (_recordingStatus != value)
                {
                    _recordingStatus = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 日志文本
        /// </summary>
        public string LogText
        {
            get => _logText;
            set
            {
                if (_logText != value)
                {
                    _logText = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 状态消息
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage != value)
                {
                    _statusMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        #region 事件

        /// <summary>
        /// 属性变更事件
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化工业相机测试窗口
        /// </summary>
        public IndustrialCameraTestWindow()
        {
            InitializeComponent();
            InitializeWindow();
            
            LogUtil.Info("IndustrialCameraTestWindow: 工业相机测试窗口初始化完成");
        }

        #endregion

        #region 初始化方法

        /// <summary>
        /// 初始化窗口
        /// </summary>
        private void InitializeWindow()
        {
            try
            {
                // 设置数据上下文为窗口本身，以支持SystemTime等属性绑定
                DataContext = this;

                // 初始化日志
                InitializeLogging();

                // 初始化定时器
                InitializeTimers();

                // 订阅相机控制事件
                SubscribeCameraControlEvents();

                _isInitialized = true;
                AddLog("系统初始化完成", LogLevel.Info);
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraTestWindow: 窗口初始化失败 - {ex.Message}");
                MessageBox.Show($"窗口初始化失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 初始化日志系统
        /// </summary>
        private void InitializeLogging()
        {
            _logBuilder = new StringBuilder();
            
            // 只有在控件已加载时才设置UI
            if (LogTextBlock != null && IsLoaded)
            {
                LogTextBlock.Text = "";
            }
            
            AddLog("日志系统已启动", LogLevel.Info);
        }

        /// <summary>
        /// 初始化定时器
        /// </summary>
        private void InitializeTimers()
        {
            // 时间更新定时器
            _timeUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timeUpdateTimer.Tick += TimeUpdateTimer_Tick;
            _timeUpdateTimer.Start();

            // 性能监控定时器
            _performanceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _performanceTimer.Tick += PerformanceTimer_Tick;
            _performanceTimer.Start();
        }

        /// <summary>
        /// 订阅相机控制事件
        /// </summary>
        private void SubscribeCameraControlEvents()
        {
            try
            {
                if (CameraControl != null)
                {
                    CameraControl.PropertyChanged += CameraControl_PropertyChanged;
                    AddLog("相机控制事件订阅完成", LogLevel.Info);
                }
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"IndustrialCameraTestWindow: 订阅相机控制事件失败 - {ex.Message}");
            }
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 时间更新定时器事件
        /// </summary>
        private void TimeUpdateTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                CurrentTime = DateTime.Now;
                OnPropertyChanged(nameof(SystemTime));
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"IndustrialCameraTestWindow: 时间更新失败 - {ex.Message}");
            }
        }

        /// <summary>
        /// 性能监控定时器事件
        /// </summary>
        private void PerformanceTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                // 这里可以添加性能监控逻辑
                // 例如：CPU使用率、内存使用量等
                UpdatePerformanceMetrics();
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"IndustrialCameraTestWindow: 性能监控更新失败 - {ex.Message}");
            }
        }

        /// <summary>
        /// 相机控制属性变更事件
        /// </summary>
        private void CameraControl_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            try
            {
                // 记录重要的状态变更
                switch (e.PropertyName)
                {
                    case nameof(IndustrialCameraControl.IsConnected):
                        var isConnected = CameraControl.IsConnected;
                        AddLog($"设备连接状态变更: {(isConnected ? "已连接" : "已断开")}", 
                               isConnected ? LogLevel.Success : LogLevel.Warning);
                        break;

                    case nameof(IndustrialCameraControl.IsPreviewRunning):
                        var isPreviewRunning = CameraControl.IsPreviewRunning;
                        AddLog($"预览状态变更: {(isPreviewRunning ? "已启动" : "已停止")}", 
                               isPreviewRunning ? LogLevel.Success : LogLevel.Info);
                        break;

                    case nameof(IndustrialCameraControl.IsRecording):
                        var isRecording = CameraControl.IsRecording;
                        AddLog($"录像状态变更: {(isRecording ? "开始录像" : "停止录像")}", 
                               isRecording ? LogLevel.Warning : LogLevel.Info);
                        break;

                    case nameof(IndustrialCameraControl.OperationStatus):
                        var operationStatus = CameraControl.OperationStatus;
                        if (!string.IsNullOrEmpty(operationStatus) && operationStatus != "就绪")
                        {
                            var logLevel = operationStatus.Contains("失败") || operationStatus.Contains("错误") ? 
                                          LogLevel.Error : LogLevel.Info;
                            AddLog($"操作状态: {operationStatus}", logLevel);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"IndustrialCameraTestWindow: 处理相机控制属性变更失败 - {ex.Message}");
            }
        }

        /// <summary>
        /// 帮助按钮点击事件
        /// </summary>
        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var helpMessage = @"工业相机控制系统帮助

主要功能：
• 设备连接和管理
• 实时预览显示
• 高质量图像拍照
• 专业视频录制
• 显微镜专业控制
• 自动对焦功能
• 焦点堆叠技术
• 测量和标定工具

操作步骤：
1. 点击""刷新设备""搜索可用设备
2. 选择设备并点击""连接设备""
3. 连接成功后点击""开始预览""
4. 使用各种控制功能进行操作

快捷键：
• F5: 刷新设备
• F6: 连接/断开设备
• F7: 开始/停止预览
• F8: 拍照
• F9: 开始/停止录像
• F10: 自动对焦

技术支持：
如有问题请联系技术支持团队";

                MessageBox.Show(helpMessage, "帮助", MessageBoxButton.OK, MessageBoxImage.Information);
                AddLog("显示帮助信息", LogLevel.Info);
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraTestWindow: 显示帮助失败 - {ex.Message}");
            }
        }

        /// <summary>
        /// 设置按钮点击事件
        /// </summary>
        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 这里可以打开设置对话框
                MessageBox.Show("设置功能正在开发中...", "设置", MessageBoxButton.OK, MessageBoxImage.Information);
                AddLog("打开设置界面", LogLevel.Info);
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraTestWindow: 打开设置失败 - {ex.Message}");
            }
        }

        /// <summary>
        /// 清空日志按钮点击事件
        /// </summary>
        private void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ClearLog();
                AddLog("日志已清空", LogLevel.Info);
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraTestWindow: 清空日志失败 - {ex.Message}");
            }
        }

        /// <summary>
        /// 窗口关闭事件
        /// </summary>
        protected override void OnClosing(CancelEventArgs e)
        {
            try
            {
                // 停止定时器
                _timeUpdateTimer?.Stop();
                _performanceTimer?.Stop();

                // 释放相机控制资源
                CameraControl?.Dispose();

                // 直接记录到系统日志，不使用AddLog方法避免UI访问
                LogUtil.Info("IndustrialCameraTestWindow: 窗口正在关闭");
                LogUtil.Info("IndustrialCameraTestWindow: 窗口关闭");
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"IndustrialCameraTestWindow: 窗口关闭时发生异常 - {ex.Message}");
            }

            base.OnClosing(e);
        }

        /// <summary>
        /// 键盘按键事件
        /// </summary>
        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            try
            {
                // 处理快捷键
                switch (e.Key)
                {
                    case System.Windows.Input.Key.F5:
                        CameraControl?.RefreshDevicesCommand?.Execute(null);
                        e.Handled = true;
                        break;
                    case System.Windows.Input.Key.F6:
                        CameraControl?.ConnectCommand?.Execute(null);
                        e.Handled = true;
                        break;
                    case System.Windows.Input.Key.F7:
                        if (CameraControl?.IsPreviewRunning == true)
                            CameraControl?.StopPreviewCommand?.Execute(null);
                        else
                            CameraControl?.StartPreviewCommand?.Execute(null);
                        e.Handled = true;
                        break;
                    case System.Windows.Input.Key.F8:
                        CameraControl?.CaptureImageCommand?.Execute(null);
                        e.Handled = true;
                        break;
                    case System.Windows.Input.Key.F9:
                        if (CameraControl?.IsRecording == true)
                            CameraControl?.StopRecordingCommand?.Execute(null);
                        else
                            CameraControl?.StartRecordingCommand?.Execute(null);
                        e.Handled = true;
                        break;
                    case System.Windows.Input.Key.F10:
                        CameraControl?.AutoFocusCommand?.Execute(null);
                        e.Handled = true;
                        break;
                }
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"IndustrialCameraTestWindow: 处理快捷键失败 - {ex.Message}");
            }

            base.OnKeyDown(e);
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 更新性能指标
        /// </summary>
        private void UpdatePerformanceMetrics()
        {
            try
            {
                // 更新系统性能指标
                var random = new Random();
                var cpuUsage = random.NextDouble() * 20 + 10; // 10-30%
                var memoryUsage = random.Next(100, 500); // 100-500MB
                
                CpuUsage = $"{cpuUsage:F1}%";
                MemoryUsage = $"{memoryUsage} MB";
                
                // 从相机控制获取图像信息
                if (CameraControl != null)
                {
                    ImageResolution = CameraControl.ImageResolution ?? "0 x 0";
                    FrameRate = $"{CameraControl.FrameRate:F1} FPS";
                    ImageFormat = CameraControl.ImageFormat ?? "未知";
                    
                    // 计算文件大小（示例）
                    var fileSize = random.Next(50, 200); // 50-200KB
                    FileSize = $"{fileSize} KB";
                    
                    // 更新状态信息
                    ConnectionStatus = CameraControl.IsConnected ? "已连接" : "未连接";
                    PreviewStatus = CameraControl.IsPreviewRunning ? "预览中" : "未启动";
                    RecordingStatus = CameraControl.IsRecording ? "录制中" : "未录制";
                    StatusMessage = CameraControl.OperationStatus ?? "就绪";
                }
                
                // 更新日志文本
                if (_logBuilder != null)
                {
                    LogText = _logBuilder.ToString();
                }
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"IndustrialCameraTestWindow: 更新性能指标失败 - {ex.Message}");
            }
        }

        /// <summary>
        /// 添加日志
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="level">日志级别</param>
        private void AddLog(string message, LogLevel level = LogLevel.Info)
        {
            try
            {
                // 确保日志构建器已初始化
                if (_logBuilder == null)
                {
                    _logBuilder = new StringBuilder();
                }

                Dispatcher.Invoke(() =>
                {
                    var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                    var levelText = GetLogLevelText(level);
                    var logLine = $"[{timestamp}] {levelText} {message}";

                    _logBuilder.AppendLine(logLine);
                    _currentLogLines++;

                    // 限制日志行数
                    if (_currentLogLines > MaxLogLines)
                    {
                        var lines = _logBuilder.ToString().Split('\n');
                        var keepLines = lines.Skip(lines.Length - MaxLogLines).ToArray();
                        _logBuilder.Clear();
                        _logBuilder.AppendLine(string.Join("\n", keepLines));
                        _currentLogLines = MaxLogLines;
                    }

                    // 更新UI - 确保控件已加载
                    if (LogTextBlock != null && IsLoaded)
                    {
                        LogTextBlock.Text = _logBuilder.ToString();
                        
                        // 自动滚动到底部
                        if (LogScrollViewer != null)
                        {
                            LogScrollViewer.ScrollToEnd();
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"IndustrialCameraTestWindow: 添加日志失败 - {ex.Message}");
            }
        }

        /// <summary>
        /// 清空日志
        /// </summary>
        private void ClearLog()
        {
            try
            {
                _logBuilder?.Clear();
                _currentLogLines = 0;
                
                if (LogTextBlock != null && IsLoaded)
                {
                    LogTextBlock.Text = "";
                }
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"IndustrialCameraTestWindow: 清空日志失败 - {ex.Message}");
            }
        }

        /// <summary>
        /// 获取日志级别文本
        /// </summary>
        /// <param name="level">日志级别</param>
        /// <returns>日志级别文本</returns>
        private string GetLogLevelText(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    return "[DEBUG]";
                case LogLevel.Info:
                    return "[INFO] ";
                case LogLevel.Warning:
                    return "[WARN] ";
                case LogLevel.Error:
                    return "[ERROR]";
                case LogLevel.Success:
                    return "[OK]   ";
                default:
                    return "[INFO] ";
            }
        }

        /// <summary>
        /// 属性变更通知
        /// </summary>
        /// <param name="propertyName">属性名称</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    #region 枚举

    /// <summary>
    /// 日志级别枚举
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// 调试
        /// </summary>
        Debug,

        /// <summary>
        /// 信息
        /// </summary>
        Info,

        /// <summary>
        /// 警告
        /// </summary>
        Warning,

        /// <summary>
        /// 错误
        /// </summary>
        Error,

        /// <summary>
        /// 成功
        /// </summary>
        Success
    }

    #endregion
}