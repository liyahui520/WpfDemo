using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using OpenCv.Core;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace OpenCv
{
    /// <summary>
    /// 分辨率选项类
    /// 用于ComboBox显示和数据绑定
    /// </summary>
    public class ResolutionItem
    {
        /// <summary>
        /// 分辨率宽度
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// 分辨率高度
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// 显示名称
        /// </summary>
        public string DisplayName => $"{Width}x{Height}";

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        public ResolutionItem(int width, int height)
        {
            Width = width;
            Height = height;
        }

        /// <summary>
        /// 重写ToString方法
        /// </summary>
        /// <returns>显示名称</returns>
        public override string ToString()
        {
            return DisplayName;
        }
    }

    /// <summary>
    /// OpenCV摄像头测试系统主窗口
    /// 提供摄像头预览、录像、拍照等功能的完整测试界面
    /// </summary>
    public partial class MainWindow : System.Windows.Window
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
        private DispatcherTimer _uiUpdateTimer;
        private DispatcherTimer _timeUpdateTimer;

        /// <summary>
        /// 录像开始时间
        /// </summary>
        private DateTime _recordingStartTime;

        /// <summary>
        /// 是否正在录像
        /// </summary>
        private bool _isRecording;

        /// <summary>
        /// 输出目录
        /// </summary>
        private string _outputDirectory;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化MainWindow
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
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
                // 创建输出目录
                _outputDirectory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "OpenCV_Captures");
                Directory.CreateDirectory(_outputDirectory);

                // 订阅CameraPreviewControl的事件
                CameraPreview.StatusChanged += OnStatusChanged;
                CameraPreview.RecordingStatusChanged += OnRecordingStatusChanged;
                CameraPreview.ErrorOccurred += OnErrorOccurred;
                CameraPreview.PerformanceStats += OnPerformanceStats;

                // 初始化性能计数器
                InitializePerformanceCounters();

                // 初始化定时器
                InitializeTimers();

                // 设置CameraPreviewControl的输出目录
                CameraPreview.SetOutputDirectory(_outputDirectory);

                // 加载设备列表
                LoadDeviceList();

                // 初始化UI
                UpdateUI();

                AddLog("系统初始化完成");
            }
            catch (Exception ex)
            {
                AddLog($"系统初始化失败: {ex.Message}");
                MessageBox.Show($"系统初始化失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
            _uiUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _uiUpdateTimer.Tick += UiUpdateTimer_Tick;
            _uiUpdateTimer.Start();

            // 时间更新定时器
            _timeUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timeUpdateTimer.Tick += TimeUpdateTimer_Tick;
            _timeUpdateTimer.Start();
        }

        /// <summary>
        /// 加载设备列表
        /// </summary>
        private void LoadDeviceList()
        {
            try
            {
                AddLog("开始刷新设备列表...");
                DeviceComboBox.Items.Clear();
                
                // 通过CameraPreviewControl刷新设备列表
                var devices = CameraPreview.AvailableDevices;
                AddLog($"设备管理器返回 {devices.Count()} 个设备");
                
                foreach (var device in devices)
                {
                    var deviceInfo = $"设备 {device.Index}: {device.Name} ({device.Status})";
                    DeviceComboBox.Items.Add(deviceInfo);
                    AddLog($"添加设备: {deviceInfo}");
                }

                if (DeviceComboBox.Items.Count > 0)
                {
                    DeviceComboBox.SelectedIndex = 0;
                    AddLog($"默认选择第一个设备");
                    
                    // 加载分辨率选项
                    LoadResolutionOptions();
                }
                else
                {
                    AddLog("警告: 未发现任何可用的摄像头设备");
                    // 清空分辨率选项
                    ResolutionComboBox.Items.Clear();
                    ResolutionComboBox.IsEnabled = false;
                    ApplyResolutionButton.IsEnabled = false;
                }

                AddLog($"设备列表加载完成，共发现 {devices.Count()} 个摄像头设备");
            }
            catch (Exception ex)
            {
                AddLog($"加载设备列表失败: {ex.Message}");
                AddLog($"异常详情: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// 加载分辨率选项
        /// </summary>
        private void LoadResolutionOptions()
        {
            try
            {
                AddLog("开始加载分辨率选项...");
                ResolutionComboBox.Items.Clear();

                // 获取当前选中的设备
                var selectedDeviceIndex = DeviceComboBox.SelectedIndex;
                if (selectedDeviceIndex < 0 || selectedDeviceIndex >= CameraPreview.AvailableDevices.Count())
                {
                    AddLog("警告: 没有选中有效的设备");
                    ResolutionComboBox.IsEnabled = false;
                    return;
                }

                var selectedDevice = CameraPreview.AvailableDevices.ElementAt(selectedDeviceIndex);
                
                // 添加设备支持的分辨率
                if (selectedDevice.SupportedResolutions != null && selectedDevice.SupportedResolutions.Any())
                {
                    foreach (var resolution in selectedDevice.SupportedResolutions)
                    {
                        var resolutionItem = new ResolutionItem(resolution.Width, resolution.Height);
                        ResolutionComboBox.Items.Add(resolutionItem);
                        AddLog($"添加支持的分辨率: {resolutionItem.DisplayName}");
                    }
                }
                else
                {
                    // 如果设备没有提供支持的分辨率列表，添加常见分辨率
                    var commonResolutions = new[]
                    {
                        new ResolutionItem(320, 240),   // QVGA
                        new ResolutionItem(640, 480),   // VGA
                        new ResolutionItem(800, 600),   // SVGA
                        new ResolutionItem(1024, 768),  // XGA
                        new ResolutionItem(1280, 720),  // HD 720p
                        new ResolutionItem(1280, 960),  // SXGA
                        new ResolutionItem(1920, 1080), // Full HD 1080p
                        new ResolutionItem(2560, 1440), // QHD
                        new ResolutionItem(3840, 2160)  // 4K UHD
                    };

                    foreach (var resolution in commonResolutions)
                    {
                        ResolutionComboBox.Items.Add(resolution);
                        AddLog($"添加常见分辨率: {resolution.DisplayName}");
                    }
                }

                // 设置默认选择
                if (ResolutionComboBox.Items.Count > 0)
                {
                    // 尝试选择当前设备的默认分辨率
                    var defaultResolution = new ResolutionItem(selectedDevice.DefaultWidth, selectedDevice.DefaultHeight);
                    var matchingItem = ResolutionComboBox.Items.Cast<ResolutionItem>()
                        .FirstOrDefault(r => r.Width == defaultResolution.Width && r.Height == defaultResolution.Height);

                    if (matchingItem != null)
                    {
                        ResolutionComboBox.SelectedItem = matchingItem;
                        AddLog($"选择默认分辨率: {matchingItem.DisplayName}");
                    }
                    else
                    {
                        // 如果没有匹配的，选择第一个
                        ResolutionComboBox.SelectedIndex = 0;
                        AddLog($"选择第一个分辨率: {ResolutionComboBox.Items[0]}");
                    }

                    ResolutionComboBox.IsEnabled = true;
                    
                    // 更新当前分辨率显示
                    ResolutionText.Text = $"{selectedDevice.Width}x{selectedDevice.Height}";
                }
                else
                {
                    ResolutionComboBox.IsEnabled = false;
                    AddLog("警告: 没有可用的分辨率选项");
                }

                AddLog($"分辨率选项加载完成，共 {ResolutionComboBox.Items.Count} 个选项");
            }
            catch (Exception ex)
            {
                AddLog($"加载分辨率选项失败: {ex.Message}");
                ResolutionComboBox.IsEnabled = false;
            }
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 开始/停止按钮点击事件
        /// </summary>
        private async void StartStopButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CameraPreview.IsCapturing)
                {
                    CameraPreview.StopPreview();
                    StartStopButton.Content = "开始预览";
                    AddLog("停止预览");
                }
                else
                {
                    var deviceIndex = DeviceComboBox.SelectedIndex;
                    if (deviceIndex >= 0)
                    {
                        var success = await CameraPreview.StartPreviewAsync(deviceIndex);
                        if (success)
                        {
                            StartStopButton.Content = "停止预览";
                            AddLog($"开始预览设备 {deviceIndex}");
                        }
                        else
                        {
                            AddLog("启动预览失败");
                        }
                    }
                    else
                    {
                        MessageBox.Show("请选择一个摄像头设备", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                AddLog($"预览操作失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 录像按钮点击事件
        /// </summary>
        private void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!CameraPreview.IsCapturing)
                {
                    MessageBox.Show("请先开始预览", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (CameraPreview.IsRecording)
                {
                    CameraPreview.StopRecording();
                    RecordButton.Content = "开始录像";
                    _isRecording = false;
                    AddLog("停止录像");
                }
                else
                {
                    var fileName = $"Recording_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
                    var filePath = System.IO.Path.Combine(_outputDirectory, fileName);
                    
                    var success = CameraPreview.StartRecording(filePath);
                    if (success)
                    {
                        RecordButton.Content = "停止录像";
                        _isRecording = true;
                        _recordingStartTime = DateTime.Now;
                        AddLog($"开始录像: {fileName}");
                    }
                    else
                    {
                        AddLog("启动录像失败");
                    }
                }
            }
            catch (Exception ex)
            {
                AddLog($"录像操作失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 拍照按钮点击事件
        /// </summary>
        private void SnapshotButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!CameraPreview.IsCapturing)
                {
                    MessageBox.Show("请先开始预览", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var fileName = $"Snapshot_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                var filePath = System.IO.Path.Combine(_outputDirectory, fileName);
                
                var success = CameraPreview.TakeSnapshot(filePath);
                if (success)
                {
                    AddLog($"拍照成功: {fileName}");
                    MessageBox.Show($"拍照成功!\n保存位置: {filePath}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    AddLog("拍照失败");
                }
            }
            catch (Exception ex)
            {
                AddLog($"拍照失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 设置按钮点击事件
        /// </summary>
        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 打开输出目录
                Process.Start("explorer.exe", _outputDirectory);
                AddLog($"打开输出目录: {_outputDirectory}");
            }
            catch (Exception ex)
            {
                AddLog($"打开设置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 测试摄像头按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">路由事件参数</param>
        private void TestCameraButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddLog("开始测试摄像头访问权限和设备枚举功能...");
                
                // 在后台线程中运行测试，避免阻塞UI
                Task.Run(() =>
                {
                    try
                    {
                        var testResults = CameraTest.TestCameraAccess();
                        
                        // 在UI线程中更新日志
                        Dispatcher.Invoke(() =>
                        {
                            foreach (var result in testResults)
                            {
                                AddLog(result);
                            }
                            AddLog("摄像头测试完成");
                        });
                    }
                    catch (Exception testEx)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            AddLog($"摄像头测试失败: {testEx.Message}");
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                AddLog($"启动摄像头测试失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 设备选择变更事件
        /// </summary>
        private async void DeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                // 重新加载分辨率选项
                LoadResolutionOptions();

                if (CameraPreview?.IsCapturing == true)
                {
                    // 如果正在预览，重新启动
                    CameraPreview.StopPreview();
                    var deviceIndex = DeviceComboBox.SelectedIndex;
                    if (deviceIndex >= 0)
                    {
                        await CameraPreview.StartPreviewAsync(deviceIndex);
                        AddLog($"切换到设备 {deviceIndex}");
                    }
                }
            }
            catch (Exception ex)
            {
                AddLog($"设备选择变更失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 刷新设备按钮点击事件
        /// </summary>
        private async void RefreshDevicesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddLog("手动刷新设备列表...");
                await CameraPreview.RefreshDevicesAsync();
                LoadDeviceList();
                AddLog("设备列表刷新完成");
            }
            catch (Exception ex)
            {
                AddLog($"刷新设备列表失败: {ex.Message}");
                MessageBox.Show($"刷新设备列表失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 分辨率选择变更事件
        /// </summary>
        private void ResolutionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (ResolutionComboBox.SelectedItem != null)
                {
                    ApplyResolutionButton.IsEnabled = true;
                    AddLog($"选择分辨率: {ResolutionComboBox.SelectedItem}");
                }
                else
                {
                    ApplyResolutionButton.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                AddLog($"分辨率选择变更失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 应用分辨率按钮点击事件
        /// </summary>
        /// <summary>
        /// 应用分辨率按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private async void ApplyResolutionButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 验证分辨率选择
                if (ResolutionComboBox.SelectedItem == null)
                {
                    MessageBox.Show("请先选择分辨率", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    AddLog("错误: 未选择分辨率");
                    return;
                }

                var selectedResolution = ResolutionComboBox.SelectedItem as ResolutionItem;
                if (selectedResolution == null)
                {
                    AddLog("错误: 无效的分辨率选择");
                    MessageBox.Show("无效的分辨率选择", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 验证摄像头预览控件
                if (CameraPreview == null)
                {
                    AddLog("错误: 摄像头预览控件未初始化");
                    MessageBox.Show("摄像头预览控件未初始化", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 验证设备选择
                int currentDeviceIndex = DeviceComboBox.SelectedIndex;
                if (currentDeviceIndex < 0)
                {
                    AddLog("错误: 未选择摄像头设备");
                    MessageBox.Show("请先选择摄像头设备", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 验证分辨率参数
                if (selectedResolution.Width <= 0 || selectedResolution.Height <= 0)
                {
                    AddLog($"错误: 无效的分辨率参数 {selectedResolution.Width}x{selectedResolution.Height}");
                    MessageBox.Show($"无效的分辨率参数: {selectedResolution.Width}x{selectedResolution.Height}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 检查分辨率是否过大（可能导致性能问题）
                if (selectedResolution.Width > 3840 || selectedResolution.Height > 2160)
                {
                    var result = MessageBox.Show(
                        $"选择的分辨率 {selectedResolution.Width}x{selectedResolution.Height} 较高，可能影响性能。是否继续？",
                        "性能警告",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning
                    );
                    
                    if (result == MessageBoxResult.No)
                    {
                        AddLog("用户取消了高分辨率设置");
                        return;
                    }
                }

                AddLog($"开始应用分辨率: {selectedResolution.Width}x{selectedResolution.Height}");
                
                // 禁用按钮防止重复点击并显示进度指示器
                ApplyResolutionButton.IsEnabled = false;
                ApplyResolutionButton.Content = "设置中...";
                ResolutionProgressPanel.Visibility = Visibility.Visible;
                ResolutionProgressText.Text = $"正在设置分辨率为 {selectedResolution.Width}x{selectedResolution.Height}...";
                
                try
                {
                    // 记录当前状态
                    bool wasCapturing = CameraPreview.IsCapturing;
                    bool wasRecording = CameraPreview.IsRecording;
                    
                    // 如果正在录像，先停止录像
                    if (wasRecording)
                    {
                        AddLog("检测到正在录像，先停止录像");
                        ResolutionProgressText.Text = "正在停止录像...";
                        CameraPreview.StopRecording();
                        await Task.Delay(500); // 等待录像停止
                    }

                    // 如果正在预览，先停止预览
                    if (wasCapturing)
                    {
                        AddLog("停止当前预览以应用新分辨率");
                        ResolutionProgressText.Text = "正在停止预览...";
                        CameraPreview.StopPreview();
                        await Task.Delay(200); // 等待停止完成
                    }

                    // 设置新分辨率
                    AddLog($"正在设置分辨率为 {selectedResolution.Width}x{selectedResolution.Height}");
                    ResolutionProgressText.Text = $"正在配置 {selectedResolution.Width}x{selectedResolution.Height} 分辨率...";
                    var success = await CameraPreview.SetResolutionAsync(selectedResolution.Width, selectedResolution.Height);

                    if (success)
                    {
                        // 更新当前分辨率显示
                        ResolutionText.Text = $"{selectedResolution.Width}x{selectedResolution.Height}";
                        
                        // 如果之前在预览，重新开始预览
                        if (wasCapturing)
                        {
                            AddLog("重新启动预览");
                            ResolutionProgressText.Text = "正在重新启动预览...";
                            var previewSuccess = await CameraPreview.StartPreviewAsync(currentDeviceIndex);
                            if (!previewSuccess)
                            {
                                AddLog("警告: 重新启动预览失败");
                                MessageBox.Show("分辨率设置成功，但重新启动预览失败", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        }
                        
                        AddLog($"分辨率已成功应用: {selectedResolution.Width}x{selectedResolution.Height}");
                        MessageBox.Show($"分辨率已成功设置为 {selectedResolution.Width}x{selectedResolution.Height}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        AddLog($"分辨率设置失败: {selectedResolution.Width}x{selectedResolution.Height}");
                        MessageBox.Show($"无法设置分辨率为 {selectedResolution.Width}x{selectedResolution.Height}，请尝试其他分辨率", "设置失败", MessageBoxButton.OK, MessageBoxImage.Error);
                        
                        // 尝试恢复之前的状态
                        if (wasCapturing)
                        {
                            AddLog("尝试恢复之前的预览状态");
                            await CameraPreview.StartPreviewAsync(currentDeviceIndex);
                        }
                    }
                }
                finally
                {
                    // 恢复按钮状态并隐藏进度指示器
                    ApplyResolutionButton.IsEnabled = true;
                    ApplyResolutionButton.Content = "✓";
                    ResolutionProgressPanel.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                AddLog($"应用分辨率异常: {ex.Message}");
                MessageBox.Show($"应用分辨率时发生异常: {ex.Message}", "异常错误", MessageBoxButton.OK, MessageBoxImage.Error);
                
                // 恢复按钮状态并隐藏进度指示器
                ApplyResolutionButton.IsEnabled = true;
                ApplyResolutionButton.Content = "✓";
                ResolutionProgressPanel.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// UI更新定时器事件
        /// </summary>
        private void UiUpdateTimer_Tick(object sender, EventArgs e)
        {
            UpdatePerformanceUI();
            UpdateRecordingTime();
        }

        /// <summary>
        /// 时间更新定时器事件
        /// </summary>
        private void TimeUpdateTimer_Tick(object sender, EventArgs e)
        {
            TimeText.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        #endregion

        #region 摄像头事件处理



        /// <summary>
        /// 状态变更事件处理
        /// </summary>
        private void OnStatusChanged(object sender, StatusChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                DeviceStatusText.Text = e.IsCapturing ? "已连接" : "未连接";
                DeviceStatusText.Foreground = e.IsCapturing ? 
                    new SolidColorBrush(Color.FromRgb(78, 205, 196)) : 
                    new SolidColorBrush(Color.FromRgb(255, 107, 107));
                
                StatusText.Text = e.IsCapturing ? $"正在预览设备 {e.DeviceIndex}" : "就绪";
                
                // 更新按钮状态
                UpdateUI();
            }));
        }

        /// <summary>
        /// 录像状态变更事件处理
        /// </summary>
        private void OnRecordingStatusChanged(object sender, RecordingStatusChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                RecordingStatusText.Text = e.IsRecording ? "正在录像" : "未录像";
                RecordingStatusText.Foreground = e.IsRecording ? 
                    new SolidColorBrush(Color.FromRgb(255, 107, 107)) : 
                    new SolidColorBrush(Color.FromRgb(204, 204, 204));
                
                // 更新按钮状态
                UpdateUI();
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
            }));
        }

        /// <summary>
        /// 性能统计事件处理
        /// </summary>
        private void OnPerformanceStats(object sender, PerformanceStatsEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                FpsText.Text = $"{e.Fps:F1}";
                ResolutionText.Text = CameraPreview.ResolutionText;
            }));
        }

        /// <summary>
        /// 系统健康状态变更事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">事件参数</param>
        private void OnSystemHealthChanged(object sender, SystemHealthChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (e.IsHealthy)
                {
                    AddLog($"系统健康状态: 正常");
                }
                else
                {
                    AddLog($"系统健康状态: 异常");
                }
            }));
        }

        /// <summary>
        /// 错误恢复事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">事件参数</param>
        private void OnErrorRecoveryOccurred(object sender, ErrorRecoveryEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (e.IsSuccessful)
                {
                    AddLog($"错误恢复成功: {e.Context} (尝试次数: {e.AttemptCount})");
                }
                else
                {
                    AddLog($"错误恢复失败: {e.Context} (尝试次数: {e.AttemptCount})");
                }
            }));
        }

        #endregion

        #region UI更新方法

        /// <summary>
        /// 更新UI状态
        /// </summary>
        private void UpdateUI()
        {
            var isCapturing = CameraPreview?.IsCapturing ?? false;
            var isRecording = CameraPreview?.IsRecording ?? false;

            StartStopButton.Content = isCapturing ? "停止预览" : "开始预览";
            RecordButton.Content = isRecording ? "停止录像" : "开始录像";
            RecordButton.IsEnabled = isCapturing;
            SnapshotButton.IsEnabled = isCapturing;
        }

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
            }
        }

        /// <summary>
        /// 更新错误统计显示
        /// </summary>
        private void UpdateErrorStatistics()
        {
            try
            {
                // 简化错误统计显示，后续可以通过CameraPreviewControl获取
                TotalErrorsText.Text = "0";
                CriticalErrorsText.Text = "0";
                
                // 根据CameraPreviewControl状态更新系统状态文本
                if (CameraPreview?.IsCapturing == true)
                {
                    SystemStatusText.Text = "正常运行";
                    SystemStatusText.Foreground = new SolidColorBrush(Colors.Green);
                }
                else
                {
                    SystemStatusText.Text = "未运行";
                    SystemStatusText.Foreground = new SolidColorBrush(Colors.Orange);
                }
            }
            catch (Exception ex)
            {
                AddLog($"更新错误统计失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新录像时间
        /// </summary>
        private void UpdateRecordingTime()
        {
            if (_isRecording)
            {
                var elapsed = DateTime.Now - _recordingStartTime;
                RecordingTimeText.Text = elapsed.ToString(@"hh\:mm\:ss");
            }
            else
            {
                RecordingTimeText.Text = "00:00:00";
            }
        }

        /// <summary>
        /// 添加日志
        /// </summary>
        /// <param name="message">日志消息</param>
        private void AddLog(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logEntry = $"[{timestamp}] {message}\r\n";
            
            LogTextBox.AppendText(logEntry);
            LogTextBox.ScrollToEnd();
        }

        #endregion

        #region 窗口事件

        /// <summary>
        /// 窗口关闭事件
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            try
            {
                // 停止定时器
                _uiUpdateTimer?.Stop();
                _timeUpdateTimer?.Stop();

                // CameraPreviewControl会自动释放摄像头资源

                // 释放性能计数器
                _cpuCounter?.Dispose();
                _memoryCounter?.Dispose();

                AddLog("系统已关闭");
            }
            catch (Exception ex)
            {
                // 忽略关闭时的错误
            }

            base.OnClosed(e);
        }

        #endregion
    }
}
