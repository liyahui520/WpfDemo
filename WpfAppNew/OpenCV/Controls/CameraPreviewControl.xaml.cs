using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using WpfAppNew.OpenCv.Core; 

namespace WpfAppNew.OpenCV.Controls
{
    /// <summary>
    /// 摄像头预览控件
    /// 提供完整的摄像头预览、录像、拍照功能界面
    /// 支持多设备切换、分辨率设置、性能监控等功能
    /// 集成了设备管理、视频录制、性能监控和错误处理功能
    /// </summary>
    /// <remarks>
    /// 主要功能：
    /// 1. 摄像头设备管理和切换
    /// 2. 实时视频预览显示
    /// 3. 视频录制和拍照功能
    /// 4. 性能监控和状态显示
    /// 5. 错误处理和异常恢复
    /// 6. 响应式UI布局和多主题支持
    /// </remarks>
    public partial class CameraPreviewControl : UserControl, INotifyPropertyChanged
    {
        #region 私有字段

        /// <summary>
        /// 摄像头管理器实例
        /// </summary>
        private CameraManager _cameraManager;

        /// <summary>
        /// 当前显示的帧数据
        /// </summary>
        private BitmapSource _currentFrame;

        /// <summary>
        /// 是否正在捕获视频
        /// </summary>
        private bool _isCapturing;

        /// <summary>
        /// 是否正在录制视频
        /// </summary>
        private bool _isRecording;

        /// <summary>
        /// 是否有视频信号
        /// </summary>
        private bool _hasSignal;

        /// <summary>
        /// 是否显示十字线
        /// </summary>
        private bool _showCrosshair;

        /// <summary>
        /// 状态文本
        /// </summary>
        private string _statusText = "未初始化";

        /// <summary>
        /// 当前帧率
        /// </summary>
        private double _currentFps;

        /// <summary>
        /// 当前选择的设备
        /// </summary>
        private CameraDevice _selectedDevice;

        #endregion

        #region 公共属性

        /// <summary>
        /// 可用设备列表
        /// </summary>
        public ObservableCollection<CameraDevice> AvailableDevices { get; } = new ObservableCollection<CameraDevice>();

        /// <summary>
        /// 当前显示的帧数据
        /// </summary>
        public BitmapSource CurrentFrame
        {
            get => _currentFrame;
            set
            {
                if (_currentFrame != value)
                {
                    _currentFrame = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 是否正在捕获视频
        /// </summary>
        public bool IsCapturing
        {
            get => _isCapturing;
            set
            {
                if (_isCapturing != value)
                {
                    _isCapturing = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 是否正在录制视频
        /// </summary>
        public bool IsRecording
        {
            get => _isRecording;
            set
            {
                if (_isRecording != value)
                {
                    _isRecording = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 是否有视频信号
        /// </summary>
        public bool HasSignal
        {
            get => _hasSignal;
            set
            {
                if (_hasSignal != value)
                {
                    _hasSignal = value;
                    OnPropertyChanged();
                    System.Diagnostics.Debug.WriteLine($"CameraPreviewControl: HasSignal 属性已更新为 {value}");
                }
            }
        }

        /// <summary>
        /// 是否显示十字线
        /// </summary>
        public bool ShowCrosshair
        {
            get => _showCrosshair;
            set
            {
                if (_showCrosshair != value)
                {
                    _showCrosshair = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 状态文本
        /// </summary>
        public string StatusText
        {
            get => _statusText;
            set
            {
                if (_statusText != value)
                {
                    _statusText = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 当前帧率
        /// </summary>
        public double CurrentFps
        {
            get => _currentFps;
            set
            {
                if (Math.Abs(_currentFps - value) > 0.01)
                {
                    _currentFps = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 当前选择的设备
        /// </summary>
        public CameraDevice SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                if (_selectedDevice != value)
                {
                    _selectedDevice = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ResolutionText));
                }
            }
        }

        /// <summary>
        /// 分辨率文本显示
        /// </summary>
        public string ResolutionText
        {
            get
            {
                if (_cameraManager?.CurrentResolution != null)
                {
                    var res = _cameraManager.CurrentResolution;
                    return $"{res.Width}x{res.Height}";
                }
                return "未知";
            }
        }

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化摄像头预览控件
        /// </summary>
        public CameraPreviewControl()
        {
            InitializeComponent();
            DataContext = this;
            
            // 绑定加载和卸载事件
            //Loaded += CameraPreviewControl_Loaded;
            //Unloaded += CameraPreviewControl_Unloaded;
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 显示拍照闪光效果
        /// </summary>
        private void ShowFlashEffect()
        {
            try
            {
                if (FlashEffect != null)
                {
                    FlashEffect.Visibility = Visibility.Visible;
                    FlashEffect.Opacity = 0.8;

                    // 创建淡出动画
                    var fadeOut = new DoubleAnimation
                    {
                        From = 0.8,
                        To = 0,
                        Duration = TimeSpan.FromMilliseconds(300),
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    };

                    fadeOut.Completed += (s, e) =>
                    {
                        FlashEffect.Visibility = Visibility.Collapsed;
                    };

                    FlashEffect.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"闪光效果显示失败: {ex.Message}");
            }
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 控件加载事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private async void CameraPreviewControl_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                //System.Diagnostics.Debug.WriteLine("CameraPreviewControl: 开始初始化");
                
                //// 初始化摄像头管理器
                //_cameraManager = new CameraManager();
                //System.Diagnostics.Debug.WriteLine("CameraPreviewControl: CameraManager 创建成功");
                
                //// 订阅事件
                //_cameraManager.FrameCaptured += OnFrameCaptured;
                //_cameraManager.StatusChanged += OnStatusChanged;
                //_cameraManager.RecordingStatusChanged += OnRecordingStatusChanged;
                //_cameraManager.ErrorOccurred += OnErrorOccurred;
                //_cameraManager.PerformanceStats += OnPerformanceStats;

                //// 订阅事件转发
                //_cameraManager.StatusChanged += ForwardStatusChanged;
                //_cameraManager.RecordingStatusChanged += ForwardRecordingStatusChanged;
                //_cameraManager.ErrorOccurred += ForwardErrorOccurred;
                //_cameraManager.PerformanceStats += ForwardPerformanceStats;
                
                //System.Diagnostics.Debug.WriteLine("CameraPreviewControl: 事件订阅完成");

                //// 加载可用设备
                //await LoadAvailableDevicesAsync();
                //System.Diagnostics.Debug.WriteLine($"CameraPreviewControl: 设备加载完成，找到 {AvailableDevices.Count} 个设备");
                
                //StatusText = "就绪";
                //System.Diagnostics.Debug.WriteLine("CameraPreviewControl: 初始化完成");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CameraPreviewControl: 初始化失败 - {ex.Message}");
                MessageBox.Show($"初始化摄像头控件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 控件卸载事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void CameraPreviewControl_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _cameraManager?.Dispose();
            }
            catch (Exception ex)
            {
                // 记录日志但不显示错误
                System.Diagnostics.Debug.WriteLine($"摄像头控件卸载异常: {ex.Message}");
            }
        }

        #endregion

        #region 摄像头事件处理

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
                System.Diagnostics.Debug.WriteLine($"SafeMatToBitmapSource转换失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 帧捕获事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">帧捕获事件参数</param>
        private void OnFrameCaptured(object sender, FrameCapturedEventArgs e)
        {
            try
            {
                // 如果BitmapSource为null，从Mat对象转换
                BitmapSource bitmapSource = e.BitmapSource;
                if (bitmapSource == null && e.Frame != null && !e.Frame.Empty())
                {
                    // 在UI线程中进行转换
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            bitmapSource = SafeMatToBitmapSource(e.Frame);
                            CurrentFrame = bitmapSource;
                            HasSignal = true;
                            System.Diagnostics.Debug.WriteLine($"CameraPreviewControl: 从Mat转换帧数据，尺寸: {e.Frame.Width}x{e.Frame.Height}, HasSignal设置为true");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"CameraPreviewControl: Mat转换失败 - {ex.Message}");
                            HasSignal = false;
                        }
                    }));
                }
                else if (bitmapSource != null)
                {
                    System.Diagnostics.Debug.WriteLine($"CameraPreviewControl: 收到BitmapSource帧数据，尺寸: {bitmapSource.PixelWidth}x{bitmapSource.PixelHeight}");
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        CurrentFrame = bitmapSource;
                        HasSignal = true;
                        System.Diagnostics.Debug.WriteLine($"CameraPreviewControl: 帧数据已更新到UI, HasSignal设置为true, CurrentFrame是否为null: {CurrentFrame == null}");
                    }));
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("CameraPreviewControl: 收到空帧数据，HasSignal保持当前状态");
                    // 不要立即设置HasSignal为false，可能只是偶尔的空帧
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CameraPreviewControl: OnFrameCaptured异常 - {ex.Message}");
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    HasSignal = false;
                }));
            }
        }

        /// <summary>
        /// 状态变更事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">状态变更事件参数</param>
        private void OnStatusChanged(object sender, StatusChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                IsCapturing = e.Status == CameraStatus.Running;
                switch (e.Status)
                {
                    case CameraStatus.Running:
                        StatusText = "运行中";
                        break;
                    case CameraStatus.Stopped:
                        StatusText = "已停止";
                        break;
                    case CameraStatus.Error:
                        StatusText = "错误";
                        break;
                    default:
                        StatusText = "未知";
                        break;
                }

                if (e.Status == CameraStatus.Stopped)
                {
                    HasSignal = false;
                    CurrentFrame = null;
                }
            }));
        }

        /// <summary>
        /// 录像状态变更事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">录像状态变更事件参数</param>
        private void OnRecordingStatusChanged(object sender, RecordingStatusChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                IsRecording = e.IsRecording;
                
                if (e.IsRecording)
                {
                    StatusText = "录像中";
                }
                else if (IsCapturing)
                {
                    StatusText = "运行中";
                }
            }));
        }

        /// <summary>
        /// 错误发生事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">错误发生事件参数</param>
        private void OnErrorOccurred(object sender, ErrorOccurredEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                StatusText = "错误";
                HasSignal = false;
                
                // 可以选择是否显示错误消息
                if (!(e.Exception is OperationCanceledException))
                {
                    System.Diagnostics.Debug.WriteLine($"摄像头错误: {e.Message} - {e.Exception?.Message}");
                }
            }));
        }

        /// <summary>
        /// 性能统计事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">性能统计事件参数</param>
        private void OnPerformanceStats(object sender, PerformanceStatsEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                CurrentFps = e.Fps;
            }));
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 开始摄像头预览
        /// </summary>
        /// <param name="deviceIndex">设备索引，如果为null则使用当前选中的设备</param>
        /// <returns>是否成功开始预览</returns>
        public async Task<bool> StartPreviewAsync(int? deviceIndex = null)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("CameraPreviewControl: 开始启动预览");
                
                if (_cameraManager == null)
                {
                    System.Diagnostics.Debug.WriteLine("CameraPreviewControl: 摄像头管理器未初始化");
                    StatusText = "摄像头管理器未初始化";
                    return false;
                }

                // 如果指定了设备索引，则使用指定的设备
                if (deviceIndex.HasValue)
                {
                    _cameraManager.CurrentDeviceIndex = deviceIndex.Value;
                    System.Diagnostics.Debug.WriteLine($"CameraPreviewControl: 使用指定设备索引: {deviceIndex.Value}");
                }
                else if (SelectedDevice != null)
                {
                    _cameraManager.CurrentDeviceIndex = SelectedDevice.Index;
                    System.Diagnostics.Debug.WriteLine($"CameraPreviewControl: 使用选择的设备: {SelectedDevice.Name}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("CameraPreviewControl: 未选择摄像头设备");
                    StatusText = "请先选择摄像头设备";
                    return false;
                }

                StatusText = "初始化中...";
                
                var success = await _cameraManager.InitializeCaptureAsync();
                System.Diagnostics.Debug.WriteLine($"CameraPreviewControl: 初始化结果: {success}");
                
                if (success)
                {
                    _cameraManager.StartPreview();
                    StatusText = "预览中";
                    System.Diagnostics.Debug.WriteLine("CameraPreviewControl: 预览已启动");
                    return true;
                }
                else
                {
                    StatusText = "初始化失败";
                    System.Diagnostics.Debug.WriteLine("CameraPreviewControl: 初始化失败");
                    return false;
                }
            }
            catch (Exception ex)
            {
                StatusText = "启动失败";
                System.Diagnostics.Debug.WriteLine($"CameraPreviewControl: 开始预览失败 - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 根据设备名称开始摄像头预览
        /// </summary>
        /// <param name="deviceName">设备名称</param>
        /// <returns>是否成功开始预览</returns>
        public async Task<bool> StartPreviewAsync(string deviceName)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"CameraPreviewControl: 开始根据设备名称启动预览: {deviceName}");
                
                if (_cameraManager == null)
                {
                    System.Diagnostics.Debug.WriteLine("CameraPreviewControl: 摄像头管理器未初始化");
                    StatusText = "摄像头管理器未初始化";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(deviceName))
                {
                    System.Diagnostics.Debug.WriteLine("CameraPreviewControl: 设备名称为空");
                    StatusText = "设备名称不能为空";
                    return false;
                }
                await LoadAvailableDevicesAsync();
                
                // 改进的设备匹配逻辑
                CameraDevice targetDevice = null;
                
                // 1. 首先尝试通过设备路径精确匹配
                targetDevice = AvailableDevices.FirstOrDefault(d => 
                    string.Equals(d.DevicePath, deviceName, StringComparison.OrdinalIgnoreCase));
                
                // 2. 如果路径匹配失败，尝试通过设备名称匹配
                if (targetDevice == null)
                {
                    targetDevice = AvailableDevices.FirstOrDefault(d => 
                        string.Equals(d.Name, deviceName, StringComparison.OrdinalIgnoreCase) ||
                        d.Name.Contains("Integrated Camera"));
                }
                
                // 3. 如果设备名称匹配失败，尝试通过设备路径包含匹配
                if (targetDevice == null)
                {
                    targetDevice = AvailableDevices.FirstOrDefault(d => 
                        d.DevicePath?.Contains(deviceName) == true);
                }
                
                // 4. 如果所有匹配都失败，使用第一个可用设备作为回退
                if (targetDevice == null && AvailableDevices.Count > 0)
                {
                    targetDevice = AvailableDevices.First();
                    System.Diagnostics.Debug.WriteLine($"CameraPreviewControl: 未找到指定设备 '{deviceName}'，使用第一个可用设备: {targetDevice.Name}");
                }

                if (targetDevice == null)
                {
                    System.Diagnostics.Debug.WriteLine($"CameraPreviewControl: 没有可用的摄像头设备");
                    StatusText = "没有可用的摄像头设备";
                    return false;
                }

                System.Diagnostics.Debug.WriteLine($"CameraPreviewControl: 找到设备 '{targetDevice.Name}' (索引: {targetDevice.Index})");
                
                // 设置当前设备
                SelectedDevice = targetDevice;
                _cameraManager.CurrentDeviceIndex = targetDevice.Index;

                StatusText = "初始化中...";
                
                var success = await _cameraManager.InitializeCaptureAsync();
                System.Diagnostics.Debug.WriteLine($"CameraPreviewControl: 初始化结果: {success}");
                
                if (success)
                {
                    _cameraManager.StartPreview();
                    StatusText = "预览中";
                    System.Diagnostics.Debug.WriteLine($"CameraPreviewControl: 设备 '{targetDevice.Name}' 预览已启动");
                    return true;
                }
                else
                {
                    StatusText = "初始化失败";
                    System.Diagnostics.Debug.WriteLine("CameraPreviewControl: 初始化失败");
                    return false;
                }
            }
            catch (Exception ex)
            {
                StatusText = "启动失败";
                System.Diagnostics.Debug.WriteLine($"CameraPreviewControl: 根据设备名称开始预览失败 - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 停止摄像头预览
        /// </summary>
        public void StopPreview()
        {
            try
            {
                if (_cameraManager != null)
                {
                    _cameraManager.StopPreview();
                    StatusText = "已停止";
                    HasSignal = false;
                    CurrentFrame = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"停止预览失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 拍照
        /// </summary>
        /// <param name="savePath">保存路径，如果为null则弹出保存对话框</param>
        /// <returns>是否成功拍照</returns>
        public bool TakeSnapshot(string savePath = null)
        {
            try
            {
                if (_cameraManager == null || !IsCapturing)
                {
                    MessageBox.Show("请先开始预览", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return false;
                }

                string filePath = savePath;
                if (string.IsNullOrEmpty(filePath))
                {
                    var saveDialog = new SaveFileDialog
                    {
                        Title = "选择照片保存位置",
                        Filter = "JPEG图片|*.jpg|PNG图片|*.png|BMP图片|*.bmp|所有文件|*.*",
                        DefaultExt = "jpg",
                        FileName = $"照片_{DateTime.Now:yyyyMMdd_HHmmss}.jpg"
                    };

                    if (saveDialog.ShowDialog() != true)
                    {
                        return false;
                    }
                    filePath = saveDialog.FileName;
                }

                var success = _cameraManager.TakeSnapshot(filePath);
                if (success)
                {
                    // 显示拍照闪光效果
                    ShowFlashEffect();
                    //MessageBox.Show("照片保存成功", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("拍照失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                return success;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"拍照失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// 开始录像
        /// </summary>
        /// <param name="savePath">保存路径，如果为null则弹出保存对话框</param>
        /// <returns>是否成功开始录像</returns>
        public bool StartRecording(string savePath = null)
        {
            try
            {
                if (_cameraManager == null || !IsCapturing)
                {
                    MessageBox.Show("请先开始预览", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return false;
                }

                if (IsRecording)
                {
                    MessageBox.Show("已在录像中", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return false;
                }

                string filePath = savePath;
                if (string.IsNullOrEmpty(filePath))
                {
                    var saveDialog = new SaveFileDialog
                    {
                        Title = "选择录像保存位置",
                        Filter = "MP4视频文件|*.mp4|AVI视频文件|*.avi|所有文件|*.*",
                        DefaultExt = "mp4",
                        FileName = $"录像_{DateTime.Now:yyyyMMdd_HHmmss}.mp4"
                    };

                    if (saveDialog.ShowDialog() != true)
                    {
                        return false;
                    }
                    filePath = saveDialog.FileName;
                }

                var success = _cameraManager.StartRecording(filePath);
                if (!success)
                {
                    MessageBox.Show("开始录像失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                return success;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"录像操作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// 停止录像
        /// </summary>
        /// <returns>录像文件路径</returns>
        public string StopRecording()
        {
            try
            {
                if (_cameraManager != null && IsRecording)
                {
                  return  _cameraManager.StopRecording();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"停止录像失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return "";
        }

        /// <summary>
        /// 切换录像状态
        /// </summary>
        /// <param name="savePath">保存路径，如果为null则弹出保存对话框</param>
        /// <returns>是否成功切换状态</returns>
        public bool ToggleRecording(string savePath = null)
        {
            if (IsRecording)
            {
                StopRecording();
                return true;
            }
            else
            {
                return StartRecording(savePath);
            }
        }

        /// <summary>
        /// 刷新设备列表
        /// </summary>
        /// <returns>异步任务</returns>
        public async Task RefreshDevicesAsync()
        {
            await LoadAvailableDevicesAsync();
        }

        /// <summary>
        /// 设置输出目录
        /// </summary>
        /// <param name="outputDirectory">输出目录路径</param>
        public void SetOutputDirectory(string outputDirectory)
        {
            if (!string.IsNullOrEmpty(outputDirectory) && Directory.Exists(outputDirectory))
            {
                // 可以在这里保存输出目录设置
                System.Diagnostics.Debug.WriteLine($"设置输出目录: {outputDirectory}");
            }
        }

        /// <summary>
        /// 设置摄像头分辨率
        /// </summary>
        /// <param name="width">分辨率宽度</param>
        /// <param name="height">分辨率高度</param>
        /// <returns>是否设置成功</returns>
        public async Task<bool> SetResolutionAsync(int width, int height)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"CameraPreviewControl: 设置分辨率为 {width}x{height}");
                
                if (_cameraManager == null)
                {
                    System.Diagnostics.Debug.WriteLine("CameraPreviewControl: 摄像头管理器未初始化");
                    StatusText = "摄像头管理器未初始化";
                    return false;
                }

                if (width <= 0 || height <= 0)
                {
                    System.Diagnostics.Debug.WriteLine($"CameraPreviewControl: 无效的分辨率参数: {width}x{height}");
                    StatusText = "无效的分辨率参数";
                    return false;
                }

                StatusText = "设置分辨率中...";
                
                var success = await _cameraManager.SetResolutionAsync(width, height);
                
                if (success)
                {
                    StatusText = $"分辨率已设置为 {width}x{height}";
                    System.Diagnostics.Debug.WriteLine($"CameraPreviewControl: 分辨率设置成功 {width}x{height}");
                    
                    // 等待一小段时间让摄像头稳定
                    await Task.Delay(200);
                    
                    // 强制清除当前帧，这样下一帧将是新分辨率的
                    CurrentFrame = null;
                    
                    // 触发属性变更通知，更新UI显示
                    OnPropertyChanged(nameof(CurrentFrame));
                    
                    System.Diagnostics.Debug.WriteLine($"CameraPreviewControl: 已清除旧帧数据，等待新分辨率帧数据");
                    
                    return true;
                }
                else
                {
                    StatusText = $"设置分辨率失败: 不支持 {width}x{height}";
                    System.Diagnostics.Debug.WriteLine($"CameraPreviewControl: 分辨率设置失败 {width}x{height}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                StatusText = $"设置分辨率失败: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"CameraPreviewControl: 设置分辨率异常 - {ex.Message}");
                return false;
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 加载可用设备列表
        /// </summary>
        /// <returns>异步任务</returns>
        private async Task LoadAvailableDevicesAsync()
        {
            try
            {
                Console.WriteLine("CameraPreviewControl: 开始加载可用设备...");
                var devices = await Task.Run(() => CameraManager.GetAvailableDevices());
                Console.WriteLine($"CameraPreviewControl: 获取到 {devices.Count} 个设备");
                
                AvailableDevices.Clear();
                foreach (var device in devices)
                {
                    AvailableDevices.Add(device);
                    Console.WriteLine($"CameraPreviewControl: 添加设备 {device.Index}: {device.Name}");
                }

                if (AvailableDevices.Count > 0)
                {
                    SelectedDevice = AvailableDevices[0];
                    Console.WriteLine($"CameraPreviewControl: 选择默认设备: {SelectedDevice.Name}");
                }
                else
                {
                    Console.WriteLine("CameraPreviewControl: 没有找到可用设备");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载设备列表失败: {ex.Message}");
            }
        }

        #endregion

        #region 事件转发

        /// <summary>
        /// 状态变更事件
        /// </summary>
        public event EventHandler<StatusChangedEventArgs> StatusChanged;

        /// <summary>
        /// 录像状态变更事件
        /// </summary>
        public event EventHandler<RecordingStatusChangedEventArgs> RecordingStatusChanged;

        /// <summary>
        /// 错误发生事件
        /// </summary>
        public event EventHandler<ErrorOccurredEventArgs> ErrorOccurred;

        /// <summary>
        /// 性能统计事件
        /// </summary>
        public event EventHandler<PerformanceStatsEventArgs> PerformanceStats;

        /// <summary>
        /// 转发CameraManager的状态变更事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">状态变更事件参数</param>
        private void ForwardStatusChanged(object sender, StatusChangedEventArgs e)
        {
            StatusChanged?.Invoke(this, e);
        }

        /// <summary>
        /// 转发CameraManager的录像状态变更事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">录像状态变更事件参数</param>
        private void ForwardRecordingStatusChanged(object sender, RecordingStatusChangedEventArgs e)
        {
            RecordingStatusChanged?.Invoke(this, e);
        }

        /// <summary>
        /// 转发CameraManager的错误事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">错误发生事件参数</param>
        private void ForwardErrorOccurred(object sender, ErrorOccurredEventArgs e)
        {
            ErrorOccurred?.Invoke(this, e);
        }

        /// <summary>
        /// 转发CameraManager的性能统计事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">性能统计事件参数</param>
        private void ForwardPerformanceStats(object sender, PerformanceStatsEventArgs e)
        {
            PerformanceStats?.Invoke(this, e);
        }

        #endregion

        #region INotifyPropertyChanged 实现

        /// <summary>
        /// 属性变更事件
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 触发属性变更通知
        /// </summary>
        /// <param name="propertyName">属性名称</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}