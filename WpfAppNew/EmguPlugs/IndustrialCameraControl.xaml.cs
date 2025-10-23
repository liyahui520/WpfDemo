using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using Tools.App;
using Tools.Extend;
using WpfAppNew.OpenCv.Core;
using WpfAppNew.Services;

namespace WpfAppNew.EmguPlugs
{
    /// <summary>
    /// 工业相机控制界面
    /// 提供完整的工业相机操作界面，包括预览、拍照、录像、参数调节等功能
    /// </summary>
    /// <remarks>
    /// 主要功能：
    /// 1. 设备连接和管理
    /// 2. 实时预览显示
    /// 3. 图像拍照和录像
    /// 4. 显微镜专业控制
    /// 5. 相机参数调节
    /// 6. 图像处理和增强
    /// 7. 测量和标定工具
    /// 8. 焦点堆叠功能
    /// </remarks>
    public partial class IndustrialCameraControl : UserControl, INotifyPropertyChanged
    {
        #region 私有字段

        /// <summary>
        /// 工业相机管理器
        /// </summary>
        private IndustrialCameraManager _cameraManager;

        /// <summary>
        /// 显微镜控制器
        /// </summary>
        private MicroscopeController _microscopeController;

        /// <summary>
        /// 是否已初始化
        /// </summary>
        private bool _isInitialized = false;

        /// <summary>
        /// 当前预览图像源
        /// </summary>
        private ImageSource _previewImageSource;

        /// <summary>
        /// 是否已连接设备
        /// </summary>
        private bool _isConnected = false;

        /// <summary>
        /// 是否正在预览
        /// </summary>
        private bool _isPreviewRunning = false;

        /// <summary>
        /// 是否正在录像
        /// </summary>
        private bool _isRecording = false;

        /// <summary>
        /// 连接状态文本
        /// </summary>
        private string _connectionStatus = "未连接";

        /// <summary>
        /// 操作状态文本
        /// </summary>
        private string _operationStatus = "就绪";

        /// <summary>
        /// 进度值
        /// </summary>
        private double _progressValue = 0;

        /// <summary>
        /// 是否有操作正在进行
        /// </summary>
        private bool _isOperationInProgress = false;

        /// <summary>
        /// 运行时间
        /// </summary>
        private TimeSpan _runningTime = TimeSpan.Zero;

        /// <summary>
        /// 是否正在刷新设备列表
        /// </summary>
        private bool _isRefreshingDevices = false;

        /// <summary>
        /// 缩放级别
        /// </summary>
        private double _zoomLevel = 1.0;

        /// <summary>
        /// 图像分辨率
        /// </summary>
        private string _imageResolution = "0 x 0";

        /// <summary>
        /// 图像格式
        /// </summary>
        private string _imageFormat = "未知";

        /// <summary>
        /// 帧率
        /// </summary>
        private double _frameRate = 0.0;

        /// <summary>
        /// CPU使用率
        /// </summary>
        private double _cpuUsage = 0.0;

        /// <summary>
        /// 内存使用量
        /// </summary>
        private string _memoryUsage = "0 MB";

        /// <summary>
        /// 录像持续时间
        /// </summary>
        private TimeSpan _recordingDuration = TimeSpan.Zero;

        /// <summary>
        /// 录像时间更新定时器
        /// </summary>
        private DispatcherTimer _recordingDurationTimer;

        /// <summary>
        /// 图像增强设置
        /// </summary>
        private ImageEnhancementSettings _imageEnhancementSettings;

        /// <summary>
        /// 可用设备列表
        /// </summary>
        private ObservableCollection<CameraDevice> _availableDevices;

        /// <summary>
        /// 当前选择的设备
        /// </summary>
        private CameraDevice _selectedDevice;

        #endregion

        #region 属性

        /// <summary>
        /// 预览图像源
        /// </summary>
        public ImageSource PreviewImageSource
        {
            get => _previewImageSource;
            set
            {
                if (_previewImageSource != value)
                {
                    _previewImageSource = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 是否已连接设备
        /// </summary>
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (_isConnected != value)
                {
                    _isConnected = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ConnectButtonText));
                    OnPropertyChanged(nameof(CanStartRecording));
                }
            }
        }

        /// <summary>
        /// 是否正在预览
        /// </summary>
        public bool IsPreviewRunning
        {
            get => _isPreviewRunning;
            set
            {
                if (_isPreviewRunning != value)
                {
                    _isPreviewRunning = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanStartRecording));
                }
            }
        }

        /// <summary>
        /// 是否正在录像
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
                    OnPropertyChanged(nameof(CanStartRecording));
                }
            }
        }

        /// <summary>
        /// 连接按钮文本
        /// </summary>
        public string ConnectButtonText => IsConnected ? "断开连接" : "连接设备";

        /// <summary>
        /// 是否可以开始录像
        /// </summary>
        public bool CanStartRecording => IsConnected && IsPreviewRunning && !IsRecording;

        /// <summary>
        /// 是否可以拍照
        /// </summary>
        public bool CanCapture => IsConnected && IsPreviewRunning && !_isCapturing;

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
        /// 操作状态
        /// </summary>
        public string OperationStatus
        {
            get => _operationStatus;
            set
            {
                if (_operationStatus != value)
                {
                    _operationStatus = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 进度值
        /// </summary>
        public double ProgressValue
        {
            get => _progressValue;
            set
            {
                if (Math.Abs(_progressValue - value) > 0.1)
                {
                    _progressValue = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 是否有操作正在进行
        /// </summary>
        public bool IsOperationInProgress
        {
            get => _isOperationInProgress;
            set
            {
                if (_isOperationInProgress != value)
                {
                    _isOperationInProgress = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 运行时间
        /// </summary>
        public TimeSpan RunningTime
        {
            get => _runningTime;
            set
            {
                if (_runningTime != value)
                {
                    _runningTime = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 缩放级别
        /// </summary>
        public double ZoomLevel
        {
            get => _zoomLevel;
            set
            {
                if (Math.Abs(_zoomLevel - value) > 0.01)
                {
                    _zoomLevel = value;
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
        /// 帧率
        /// </summary>
        public double FrameRate
        {
            get => _frameRate;
            set
            {
                if (Math.Abs(_frameRate - value) > 0.1)
                {
                    _frameRate = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// CPU使用率
        /// </summary>
        public double CpuUsage
        {
            get => _cpuUsage;
            set
            {
                if (Math.Abs(_cpuUsage - value) > 0.1)
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
        /// 录像持续时间
        /// </summary>
        public TimeSpan RecordingDuration
        {
            get => _recordingDuration;
            set
            {
                if (_recordingDuration != value)
                {
                    _recordingDuration = value;
                    OnPropertyChanged();
                }
            }
        }

        // 显微镜控制属性代理
        /// <summary>
        /// 放大倍数值
        /// </summary>
        public double MagnificationValue
        {
            get => _microscopeController?.CurrentMagnification ?? 1.0;
            set
            {
                if (_microscopeController != null)
                {
                    _microscopeController.CurrentMagnification = value;
                }
            }
        }

        /// <summary>
        /// 对焦值
        /// </summary>
        public double FocusValue
        {
            get => _microscopeController?.CurrentFocusValue ?? 0.5;
            set
            {
                if (_microscopeController != null)
                {
                    _microscopeController.CurrentFocusValue = value;
                }
            }
        }

        /// <summary>
        /// 是否启用自动对焦
        /// </summary>
        public bool IsAutoFocusEnabled
        {
            get => _microscopeController?.IsAutoFocusEnabled ?? false;
            set
            {
                if (_microscopeController != null)
                {
                    _microscopeController.IsAutoFocusEnabled = value;
                }
            }
        }

        /// <summary>
        /// 是否可以自动对焦
        /// </summary>
        public bool CanAutoFocus => IsConnected && IsPreviewRunning && !(_microscopeController?.IsAutoFocusing ?? false);

        /// <summary>
        /// 清晰度分数
        /// </summary>
        public double SharpnessScore => _microscopeController?.CurrentSharpnessScore ?? 0.0;

        /// <summary>
        /// 光源亮度
        /// </summary>
        public double LightBrightness
        {
            get => _microscopeController?.LightBrightness ?? 0.5;
            set
            {
                if (_microscopeController != null)
                {
                    _microscopeController.LightBrightness = value;
                }
            }
        }

        /// <summary>
        /// 光源色温
        /// </summary>
        public double LightTemperature
        {
            get => _microscopeController?.LightTemperature ?? 5500;
            set
            {
                if (_microscopeController != null)
                {
                    _microscopeController.LightTemperature = value;
                }
            }
        }

        /// <summary>
        /// 是否启用焦点堆叠
        /// </summary>
        public bool IsFocusStackingEnabled
        {
            get => _microscopeController?.IsFocusStackingEnabled ?? false;
            set
            {
                if (_microscopeController != null)
                {
                    _microscopeController.IsFocusStackingEnabled = value;
                }
            }
        }

        /// <summary>
        /// 焦点堆叠步数
        /// </summary>
        public int FocusStackingSteps
        {
            get => _microscopeController?.FocusStackingSteps ?? 10;
            set
            {
                if (_microscopeController != null)
                {
                    _microscopeController.FocusStackingSteps = value;
                }
            }
        }

        /// <summary>
        /// 是否可以执行焦点堆叠
        /// </summary>
        public bool CanPerformFocusStacking => IsConnected && IsPreviewRunning && IsFocusStackingEnabled;

        /// <summary>
        /// 标定状态
        /// </summary>
        public string CalibrationStatus => _microscopeController?.IsCalibrated == true ? "已标定" : "未标定";

        /// <summary>
        /// 是否可以测量
        /// </summary>
        public bool CanMeasure => IsConnected && IsPreviewRunning;

        // 相机参数属性（示例）
        /// <summary>
        /// 曝光值
        /// </summary>
        public double ExposureValue { get; set; } = 0.5;

        /// <summary>
        /// 增益值
        /// </summary>
        public double GainValue { get; set; } = 0.5;

        /// <summary>
        /// 是否自动白平衡
        /// </summary>
        public bool IsAutoWhiteBalance { get; set; } = true;

        /// <summary>
        /// 对比度值
        /// </summary>
        public double ContrastValue { get; set; } = 1.0;

        /// <summary>
        /// 饱和度值
        /// </summary>
        public double SaturationValue { get; set; } = 1.0;

        /// <summary>
        /// 锐化值
        /// </summary>
        public double SharpnessValue { get; set; } = 1.0;

        /// <summary>
        /// 伽马校正值
        /// </summary>
        public double GammaValue { get; set; } = 1.0;

        /// <summary>
        /// 是否启用降噪
        /// </summary>
        public bool IsNoiseReductionEnabled { get; set; } = false;

        /// <summary>
        /// 可用设备列表
        /// </summary>
        public ObservableCollection<CameraDevice> AvailableDevices
        {
            get => _availableDevices;
            set
            {
                if (_availableDevices != value)
                {
                    _availableDevices = value;
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
                }
            }
        }

        #endregion

        #region 命令

        /// <summary>
        /// 刷新设备命令
        /// </summary>
        public ICommand RefreshDevicesCommand { get; private set; }

        /// <summary>
        /// 连接命令
        /// </summary>
        public ICommand ConnectCommand { get; private set; }

        /// <summary>
        /// 开始预览命令
        /// </summary>
        public ICommand StartPreviewCommand { get; private set; }

        /// <summary>
        /// 停止预览命令
        /// </summary>
        public ICommand StopPreviewCommand { get; private set; }

        /// <summary>
        /// 拍照命令
        /// </summary>
        public ICommand CaptureImageCommand { get; private set; }

        /// <summary>
        /// 开始录像命令
        /// </summary>
        public ICommand StartRecordingCommand { get; private set; }

        /// <summary>
        /// 停止录像命令
        /// </summary>
        public ICommand StopRecordingCommand { get; private set; }

        /// <summary>
        /// 适应窗口命令
        /// </summary>
        public ICommand FitToWindowCommand { get; private set; }

        /// <summary>
        /// 实际大小命令
        /// </summary>
        public ICommand ActualSizeCommand { get; private set; }

        /// <summary>
        /// 设置放大倍数命令
        /// </summary>
        public ICommand SetMagnificationCommand { get; private set; }

        /// <summary>
        /// 自动对焦命令
        /// </summary>
        public ICommand AutoFocusCommand { get; private set; }

        /// <summary>
        /// 白平衡命令
        /// </summary>
        public ICommand WhiteBalanceCommand { get; private set; }

        /// <summary>
        /// 重置参数命令
        /// </summary>
        public ICommand ResetParametersCommand { get; private set; }

        /// <summary>
        /// 执行焦点堆叠命令
        /// </summary>
        public ICommand PerformFocusStackingCommand { get; private set; }

        /// <summary>
        /// 标定命令
        /// </summary>
        public ICommand CalibrateCommand { get; private set; }

        /// <summary>
        /// 距离测量命令
        /// </summary>
        public ICommand MeasureDistanceCommand { get; private set; }

        /// <summary>
        /// 角度测量命令
        /// </summary>
        public ICommand MeasureAngleCommand { get; private set; }

        /// <summary>
        /// 面积测量命令
        /// </summary>
        public ICommand MeasureAreaCommand { get; private set; }

        #endregion

        #region 事件

        /// <summary>
        /// 属性变更事件
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化工业相机控制界面
        /// </summary>
        public IndustrialCameraControl()
        {
            InitializeComponent();
            InitializeCommands();
            InitializeManagers();
            InitializeRecordingDurationTimer();
            
            DataContext = this;

            // 延迟异步刷新设备列表，避免阻塞UI线程
            //_ = Task.Run(async () =>
            //{
            //    await Task.Delay(100); // 让UI先完成渲染
            //    await RefreshDevicesAsync();
            //});

            LogUtil.Info("IndustrialCameraControl: 工业相机控制界面初始化完成（设备检测异步进行中）");
        }

        #endregion

        #region 初始化方法

        /// <summary>
        /// 初始化命令
        /// </summary>
        private void InitializeCommands()
        {
            RefreshDevicesCommand = new RelayCommand(async () => await RefreshDevicesAsync(), () => !_isRefreshingDevices);
            ConnectCommand = new RelayCommand(ToggleConnection);
            StartPreviewCommand = new RelayCommand(StartPreview, () => IsConnected && !IsPreviewRunning);
            StopPreviewCommand = new RelayCommand(StopPreview, () => IsPreviewRunning);
            //CaptureImageCommand = new RelayCommand(CaptureImage, () => CanCapture);
            StartRecordingCommand = new RelayCommand(StartRecording, () => CanStartRecording);
            //StopRecordingCommand = new RelayCommand(StopRecording, () => IsRecording);
            FitToWindowCommand = new RelayCommand(FitToWindow);
            ActualSizeCommand = new RelayCommand(ActualSize);
            SetMagnificationCommand = new RelayCommand<object>(SetMagnification);
            AutoFocusCommand = new RelayCommand(AutoFocus, () => CanAutoFocus);
            WhiteBalanceCommand = new RelayCommand(WhiteBalance);
            ResetParametersCommand = new RelayCommand(ResetParameters);
            PerformFocusStackingCommand = new RelayCommand(PerformFocusStacking, () => CanPerformFocusStacking);
            CalibrateCommand = new RelayCommand(Calibrate);
            MeasureDistanceCommand = new RelayCommand(MeasureDistance, () => CanMeasure);
            MeasureAngleCommand = new RelayCommand(MeasureAngle, () => CanMeasure);
            MeasureAreaCommand = new RelayCommand(MeasureArea, () => CanMeasure);
        }

        /// <summary>
        /// 初始化管理器
        /// </summary>
        private void InitializeManagers()
        {
            try
            {
                _cameraManager = new IndustrialCameraManager();
                _microscopeController = new MicroscopeController(_cameraManager);

                // 订阅事件
                _cameraManager.ConnectionStatusChanged += OnConnectionStatusChanged;
                _cameraManager.PreviewStatusChanged += OnPreviewStatusChanged;
                _cameraManager.FrameCaptured += OnFrameCaptured;
                _cameraManager.PhotoCaptured += OnPhotoCaptured;
                _cameraManager.RecordingStatusChanged += OnRecordingStatusChanged;
                _cameraManager.ErrorOccurred += OnErrorOccurred;

                _microscopeController.PropertyChanged += OnMicroscopePropertyChanged;
                _microscopeController.AutoFocusCompleted += OnAutoFocusCompleted;
                _microscopeController.MeasurementCompleted += OnMeasurementCompleted;
                _microscopeController.FocusStackingCompleted += OnFocusStackingCompleted;

                // 初始化图像增强设置
                _imageEnhancementSettings = new ImageEnhancementSettings();

                // 初始化设备列表
                _availableDevices = new ObservableCollection<CameraDevice>();

                // 初始化参数默认值（使用备用默认值，连接设备后会更新）
                InitializeParameterDefaults();

                _isInitialized = true;
                LogUtil.Info("IndustrialCameraControl: 管理器初始化完成");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraControl: 管理器初始化失败 - {ex.Message}");
                MessageBox.Show($"初始化失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 初始化录像时间更新定时器
        /// </summary>
        private void InitializeRecordingDurationTimer()
        {
            _recordingDurationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1) // 每秒更新一次
            };
            _recordingDurationTimer.Tick += RecordingDurationTimer_Tick;
        }

        /// <summary>
        /// 录像时间定时器事件处理
        /// </summary>
        private void RecordingDurationTimer_Tick(object sender, EventArgs e)
        {
            if (IsRecording && _cameraManager != null)
            {
                // 从相机管理器获取录像持续时间
                var duration = _cameraManager.GetRecordingDuration();
                RecordingDuration = duration;
            }
        }

        /// <summary>
        /// 初始化参数默认值
        /// 优先从设备获取，如果设备未连接则使用备用默认值
        /// </summary>
        private void InitializeParameterDefaults()
        {
            try
            {
                // 尝试从设备获取默认参数
                var defaultSettings = _cameraManager?.GetDeviceDefaultSettings();
                
                if (defaultSettings != null)
                {
                    // 使用从设备获取的默认值
                     ExposureValue = defaultSettings.ExposureValue;
                     GainValue = defaultSettings.GainValue;
                     ContrastValue = defaultSettings.ContrastValue;
                     SaturationValue = defaultSettings.SaturationValue;
                     // 注意：亮度使用LightBrightness属性，通过MicroscopeController设置
                     if (_microscopeController != null)
                     {
                         _microscopeController.LightBrightness = defaultSettings.BrightnessValue;
                     }
                     GammaValue = defaultSettings.GammaValue;
                     SharpnessValue = defaultSettings.SharpnessValue;
                     IsAutoWhiteBalance = defaultSettings.IsAutoWhiteBalance;
                     IsNoiseReductionEnabled = defaultSettings.IsNoiseReductionEnabled;
                    
                    LogUtil.Info("IndustrialCameraControl: 已使用设备默认参数初始化");
                }
                else
                {
                    // 使用备用默认值
                    ExposureValue = 0.5;
                    GainValue = 0.5;
                    ContrastValue = 1.0;
                    SaturationValue = 1.0;
                    // 亮度通过MicroscopeController设置
                    if (_microscopeController != null)
                    {
                        _microscopeController.LightBrightness = 0.5;
                    }
                    GammaValue = 1.0;
                    SharpnessValue = 1.0;
                    IsAutoWhiteBalance = true;
                    IsNoiseReductionEnabled = false;
                    
                    LogUtil.Warning("IndustrialCameraControl: 使用备用默认参数初始化");
                }
                
                // 触发属性变更通知
                OnPropertyChanged(nameof(ExposureValue));
                OnPropertyChanged(nameof(GainValue));
                OnPropertyChanged(nameof(ContrastValue));
                OnPropertyChanged(nameof(SaturationValue));
                OnPropertyChanged(nameof(LightBrightness)); // 使用正确的亮度属性名称
                OnPropertyChanged(nameof(GammaValue));
                OnPropertyChanged(nameof(SharpnessValue));
                OnPropertyChanged(nameof(IsAutoWhiteBalance));
                OnPropertyChanged(nameof(IsNoiseReductionEnabled));
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraControl: 初始化参数默认值失败 - {ex.Message}");
                // 发生异常时使用备用默认值
                ExposureValue = 0.5;
                GainValue = 0.5;
                ContrastValue = 1.0;
                SaturationValue = 1.0;
                // 亮度通过MicroscopeController设置
                if (_microscopeController != null)
                {
                    _microscopeController.LightBrightness = 0.5;
                }
                GammaValue = 1.0;
                SharpnessValue = 1.0;
                IsAutoWhiteBalance = true;
                IsNoiseReductionEnabled = false;
            }
        }

        /// <summary>
        /// 更新设备连接后的参数默认值
        /// 在设备连接成功后调用，从实际设备获取当前参数值
        /// </summary>
        private void UpdateParametersFromDevice()
        {
            try
            {
                if (_cameraManager != null && IsConnected)
                {
                    var defaultSettings = _cameraManager.GetDeviceDefaultSettings();
                    if (defaultSettings != null)
                    {
                        // 更新参数值为设备当前值
                        ExposureValue = defaultSettings.ExposureValue;
                        GainValue = defaultSettings.GainValue;
                        ContrastValue = defaultSettings.ContrastValue;
                        SaturationValue = defaultSettings.SaturationValue;
                        // 注意：亮度使用LightBrightness属性，通过MicroscopeController设置
                        if (_microscopeController != null)
                        {
                            _microscopeController.LightBrightness = defaultSettings.BrightnessValue;
                        }
                        GammaValue = defaultSettings.GammaValue;
                        SharpnessValue = defaultSettings.SharpnessValue;
                        IsAutoWhiteBalance = defaultSettings.IsAutoWhiteBalance;
                        IsNoiseReductionEnabled = defaultSettings.IsNoiseReductionEnabled;
                        
                        // 触发属性变更通知
                        OnPropertyChanged(nameof(ExposureValue));
                        OnPropertyChanged(nameof(GainValue));
                        OnPropertyChanged(nameof(ContrastValue));
                        OnPropertyChanged(nameof(SaturationValue));
                        OnPropertyChanged(nameof(LightBrightness)); // 使用正确的亮度属性名称
                        OnPropertyChanged(nameof(GammaValue));
                        OnPropertyChanged(nameof(SharpnessValue));
                        OnPropertyChanged(nameof(IsAutoWhiteBalance));
                        OnPropertyChanged(nameof(IsNoiseReductionEnabled));
                        
                        LogUtil.Info("IndustrialCameraControl: 已从设备更新参数默认值");
                    }
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraControl: 从设备更新参数失败 - {ex.Message}");
            }
        }

        #endregion

        #region 命令实现

        /// <summary>
        /// 刷新设备列表（同步版本，保持向后兼容）
        /// </summary>
        private void RefreshDevices()
        {
            _ = RefreshDevicesAsync();
        }

        /// <summary>
        /// 异步刷新设备列表
        /// </summary>
        private async Task RefreshDevicesAsync()
        {
            if (_isRefreshingDevices)
            {
                LogUtil.Info("IndustrialCameraControl: 设备刷新已在进行中，跳过重复请求");
                return;
            }

            try
            {
                _isRefreshingDevices = true;
                
                // 在UI线程上更新状态
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    OperationStatus = "正在刷新设备...";
                    // 清空现有设备列表
                    AvailableDevices.Clear();
                    // 更新命令状态
                    CommandManager.InvalidateRequerySuggested();
                });
                
                // 优先使用缓存的设备列表，避免重复扫描
                var devices = await CameraInitializationService.Instance.GetDevicesAsync();
                
                // 回到UI线程更新设备列表
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (devices != null)
                    {
                        foreach (var device in devices)
                        {
                            AvailableDevices.Add(device);
                        }
                    }
                    
                    // 如果没有设备，添加一个默认设备用于测试
                    if (AvailableDevices.Count == 0)
                    {
                        AvailableDevices.Add(new CameraDevice 
                        { 
                            Index = 0, 
                            Name = "默认摄像头",
                            Status = DeviceStatus.Available,
                            IsConnected = true
                        });
                    }
                    
                    // 自动选择第一个可用设备
                    if (AvailableDevices.Count > 0 && SelectedDevice == null)
                    {
                        SelectedDevice = AvailableDevices[0];
                        LogUtil.Info($"IndustrialCameraControl: 自动选择设备 {SelectedDevice.Index}: {SelectedDevice.Name}");
                    }
                    
                    OperationStatus = $"设备刷新完成，找到 {AvailableDevices.Count} 个设备";
                });
                
                LogUtil.Info($"IndustrialCameraControl: 设备列表已刷新，找到 {devices?.Count ?? 0} 个设备");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraControl: 刷新设备失败 - {ex.Message}");
                
                // 在UI线程上更新错误状态
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    OperationStatus = "设备刷新失败";
                });
            }
            finally
            {
                _isRefreshingDevices = false;
                
                // 更新命令状态
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    CommandManager.InvalidateRequerySuggested();
                });
            }
        }

        /// <summary>
        /// 切换连接状态
        /// </summary>
        private async void ToggleConnection()
        {
            try
            {
                if (IsConnected)
                {
                    // 停止预览和释放资源
                    await _cameraManager?.StopPreviewAsync();
                    _cameraManager?.Dispose();
                    IsConnected = false;
                    OperationStatus = "设备已断开";
                }
                else
                {
                    // 检查是否选择了设备
                    if (SelectedDevice == null)
                    {
                        OperationStatus = "请先选择一个设备";
                        LogUtil.Warning("IndustrialCameraControl: 未选择设备，无法连接");
                        return;
                    }

                    OperationStatus = $"正在连接设备 {SelectedDevice.Index}: {SelectedDevice.Name}...";
                    LogUtil.Info($"IndustrialCameraControl: 尝试连接设备 {SelectedDevice.Index}: {SelectedDevice.Name}");
                    
                    // 使用当前选择的设备索引进行连接
                    var success = await _cameraManager?.InitializeCameraAsync(SelectedDevice.Index);
                    if (success == true)
                    {
                        IsConnected = true;
                        OperationStatus = $"设备 {SelectedDevice.Index}: {SelectedDevice.Name} 连接成功";
                        LogUtil.Info($"IndustrialCameraControl: 设备 {SelectedDevice.Index}: {SelectedDevice.Name} 连接成功");
                    }
                    else
                    {
                        OperationStatus = $"设备 {SelectedDevice.Index}: {SelectedDevice.Name} 连接失败";
                        LogUtil.Error($"IndustrialCameraControl: 设备 {SelectedDevice.Index}: {SelectedDevice.Name} 连接失败");
                    }
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraControl: 连接操作失败 - {ex.Message}");
                OperationStatus = "连接失败";
            }
        }

        /// <summary>
        /// 开始预览
        /// </summary>
        private async void StartPreview()
        {
            try
            {
                OperationStatus = "正在启动预览...";
                var success = await _cameraManager?.StartPreviewAsync();
                if (success == true)
                {
                    IsPreviewRunning = true;
                    OperationStatus = "预览已启动";
                    FitToWindow();
                }
                else
                {
                    OperationStatus = "预览启动失败";
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraControl: 启动预览失败 - {ex.Message}");
                OperationStatus = "预览启动失败";
            }
        }

        /// <summary>
        /// 停止预览
        /// </summary>
        private async void StopPreview()
        {
            try
            {
                await _cameraManager?.StopPreviewAsync();
                IsPreviewRunning = false;
                OperationStatus = "预览已停止";
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraControl: 停止预览失败 - {ex.Message}");
            }
        }

        /// <summary>
        /// 拍照状态标志，防止重复点击
        /// </summary>
        private bool _isCapturing = false;

        /// <summary>
        /// 拍照（使用默认路径）
        /// </summary>
        //private async void CaptureImage()
        //{
        //    // 使用默认路径进行拍照
        //    await CaptureImage(null);
        //}

        /// <summary>
        /// 拍照（支持自定义文件路径）
        /// </summary>
        /// <param name="customFilePath">自定义文件路径，如果为null则使用默认路径</param>
        /// <returns>拍照是否成功</returns>
        public async Task<bool> CaptureImage(string customFilePath)
        {
            // 防止重复点击
            if (_isCapturing)
            {
                LogUtil.Warning("IndustrialCameraControl: 拍照正在进行中，请稍候");
                return false;
            }

            try
            {
                _isCapturing = true;
                OnPropertyChanged(nameof(CanCapture));
                OperationStatus = "正在拍照...";
                
                string fullPath;
                string fileName;
                
                // 如果提供了自定义路径，使用自定义路径
                if (!string.IsNullOrWhiteSpace(customFilePath))
                {
                    fullPath = customFilePath;
                    fileName = Path.GetFileName(fullPath);
                    
                    // 确保目录存在
                    var directory = Path.GetDirectoryName(fullPath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                }
                else
                {
                    // 使用默认路径
                    // 检查拍照路径是否配置
                    if (string.IsNullOrWhiteSpace(AppVideoConfig.TempPath))
                    {
                        OperationStatus = "拍照路径未配置";
                        LogUtil.Warning("IndustrialCameraControl: 拍照路径未配置");
                        return false;
                    }

                    // 确保目录存在
                    if (!Directory.Exists(AppVideoConfig.TempPath))
                    {
                        Directory.CreateDirectory(AppVideoConfig.TempPath);
                    }

                    fileName = $"Capture_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                    fullPath = Path.Combine(AppVideoConfig.TempPath, fileName);
                }
                
                LogUtil.Info($"IndustrialCameraControl: 开始拍照 - {fullPath}");
                
                // 使用ConfigureAwait(false)避免死锁
                var success = false;
                if (_cameraManager != null)
                {
                    success = await _cameraManager.CapturePhotoAsync(fullPath).ConfigureAwait(false);
                }
                
                // 回到UI线程更新状态
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (success == true)
                    {
                        OperationStatus = $"拍照成功: {fileName}";
                        LogUtil.Info($"IndustrialCameraControl: 拍照成功 - {fullPath}");
                    }
                    else
                    {
                        OperationStatus = "拍照失败";
                        LogUtil.Warning("IndustrialCameraControl: 拍照失败");
                    }
                });
                
                return success;
            }
            catch (OperationCanceledException)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    OperationStatus = "拍照超时";
                    LogUtil.Warning("IndustrialCameraControl: 拍照操作超时");
                });
                return false;
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraControl: 拍照失败 - {ex.Message}");
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    OperationStatus = "拍照失败";
                });
                return false;
            }
            finally
            {
                _isCapturing = false;
                OnPropertyChanged(nameof(CanCapture));
            }
        }

        /// <summary>
        /// 开始录像
        /// </summary>
        public async void StartRecording(string path)
        {
            try
            {
                OperationStatus = "正在开始录像...";
                var fileName = path;// $"Video_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
               await _cameraManager?.StartRecordingAsync(fileName);
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraControl: 开始录像失败 - {ex.Message}");
                OperationStatus = "录像启动失败";
            }
        }

        /// <summary>
        /// 停止录像
        /// </summary>
        public async Task<string> StopRecording()
        {
            try
            {
                OperationStatus = "录像已停止";
               return await _cameraManager?.StopRecordingAsync();
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraControl: 停止录像失败 - {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 适应窗口
        /// 优化：适配新的等比例显示布局，重置图像变换为默认状态
        /// </summary>
        private void FitToWindow()
        {
            try
            {
                if (PreviewContainer != null && PreviewImage != null)
                {
                    // 恢复等比例显示模式，图像会自动适应容器大小并居中
                    RestoreUniformStretch();
                }
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"IndustrialCameraControl: 适应窗口失败 - {ex.Message}");
            }
        }

        /// <summary>
        /// 实际大小
        /// 优化：设置图像为实际像素大小显示
        /// </summary>
        private void ActualSize()
        {
            try
            {
                if (PreviewContainer != null && PreviewImage != null)
                {
                    // 设置图像为实际像素大小，覆盖Stretch="Uniform"的自动缩放
                    PreviewImage.Stretch = Stretch.None;
                    PreviewImage.RenderTransform = Transform.Identity;
                    ZoomLevel = 1.0;
                    
                    LogUtil.Info("IndustrialCameraControl: 图像已设置为实际大小");
                }
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"IndustrialCameraControl: 实际大小失败 - {ex.Message}");
            }
        }

        /// <summary>
        /// 应用缩放
        /// 优化：支持在等比例显示模式下进行缩放
        /// </summary>
        /// <param name="scale">缩放比例</param>
        private void ApplyZoom(double scale)
        {
            try
            {
                if (PreviewImage != null)
                {
                    // 如果是缩放操作，切换到None模式以支持精确缩放
                    if (Math.Abs(scale - 1.0) > 0.01)
                    {
                        PreviewImage.Stretch = Stretch.None;
                    }
                    
                    var transform = new ScaleTransform(scale, scale);
                    PreviewImage.RenderTransform = transform;
                    
                    LogUtil.Debug($"IndustrialCameraControl: 应用缩放 - {scale:F2}x");
                }
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"IndustrialCameraControl: 应用缩放失败 - {ex.Message}");
            }
        }

        /// <summary>
        /// 恢复等比例显示模式
        /// </summary>
        private void RestoreUniformStretch()
        {
            try
            {
                if (PreviewImage != null)
                {
                    PreviewImage.Stretch = Stretch.Uniform;
                    PreviewImage.RenderTransform = Transform.Identity;
                    ZoomLevel = 1.0;
                    
                    LogUtil.Info("IndustrialCameraControl: 已恢复等比例显示模式");
                }
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"IndustrialCameraControl: 恢复等比例显示失败 - {ex.Message}");
            }
        }

        /// <summary>
        /// 设置放大倍数
        /// </summary>
        /// <param name="parameter">放大倍数参数</param>
        private void SetMagnification(object parameter)
        {
            try
            {
                if (parameter != null && double.TryParse(parameter.ToString(), out double magnification))
                {
                    MagnificationValue = magnification;
                    LogUtil.Info($"IndustrialCameraControl: 设置放大倍数 - {magnification}x");
                }
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"IndustrialCameraControl: 设置放大倍数失败 - {ex.Message}");
            }
        }

        /// <summary>
        /// 自动对焦
        /// </summary>
        private async void AutoFocus()
        {
            try
            {
                OperationStatus = "正在自动对焦...";
                IsOperationInProgress = true;
                
                var success = await _microscopeController?.AutoFocusAsync();
                
                OperationStatus = success == true ? "自动对焦完成" : "自动对焦失败";
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraControl: 自动对焦失败 - {ex.Message}");
                OperationStatus = "自动对焦失败";
            }
            finally
            {
                IsOperationInProgress = false;
            }
        }

        /// <summary>
        /// 白平衡
        /// </summary>
        private void WhiteBalance()
        {
            try
            {
                OperationStatus = "正在执行白平衡...";
                // 实现白平衡逻辑
                OperationStatus = "白平衡完成";
                LogUtil.Info("IndustrialCameraControl: 白平衡执行完成");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraControl: 白平衡失败 - {ex.Message}");
                OperationStatus = "白平衡失败";
            }
        }

        /// <summary>
        /// 重置参数
        /// 优先使用设备默认值，如果设备未连接则使用备用默认值
        /// </summary>
        private void ResetParameters()
        {
            try
            {
                // 尝试从设备获取默认参数
                var defaultSettings = _cameraManager?.GetDeviceDefaultSettings();
                
                if (defaultSettings != null && IsConnected)
                {
                    // 使用从设备获取的默认值
                    ExposureValue = defaultSettings.ExposureValue;
                    GainValue = defaultSettings.GainValue;
                    ContrastValue = defaultSettings.ContrastValue;
                    SaturationValue = defaultSettings.SaturationValue;
                    // 亮度通过MicroscopeController设置
                    if (_microscopeController != null)
                    {
                        _microscopeController.LightBrightness = defaultSettings.BrightnessValue;
                    }
                    GammaValue = defaultSettings.GammaValue;
                    SharpnessValue = defaultSettings.SharpnessValue;
                    IsAutoWhiteBalance = defaultSettings.IsAutoWhiteBalance;
                    IsNoiseReductionEnabled = defaultSettings.IsNoiseReductionEnabled;
                    
                    OperationStatus = "参数已重置为设备默认值";
                    LogUtil.Info("IndustrialCameraControl: 参数已重置为设备默认值");
                }
                else
                {
                    // 使用备用默认值
                    ExposureValue = 0.5;
                    GainValue = 0.5;
                    ContrastValue = 1.0;
                    SaturationValue = 1.0;
                    // 亮度通过MicroscopeController设置
                    if (_microscopeController != null)
                    {
                        _microscopeController.LightBrightness = 0.5;
                    }
                    GammaValue = 1.0;
                    SharpnessValue = 1.0;
                    IsAutoWhiteBalance = true;
                    IsNoiseReductionEnabled = false;
                    
                    OperationStatus = "参数已重置为备用默认值";
                    LogUtil.Info("IndustrialCameraControl: 参数已重置为备用默认值（设备未连接）");
                }
                
                // 通知所有属性变更
                OnPropertyChanged(nameof(ExposureValue));
                OnPropertyChanged(nameof(GainValue));
                OnPropertyChanged(nameof(ContrastValue));
                OnPropertyChanged(nameof(SaturationValue));
                OnPropertyChanged(nameof(LightBrightness)); // 使用正确的亮度属性名称
                OnPropertyChanged(nameof(GammaValue));
                OnPropertyChanged(nameof(SharpnessValue));
                OnPropertyChanged(nameof(IsAutoWhiteBalance));
                OnPropertyChanged(nameof(IsNoiseReductionEnabled));
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraControl: 重置参数失败 - {ex.Message}");
                OperationStatus = "参数重置失败";
            }
        }

        /// <summary>
        /// 执行焦点堆叠
        /// </summary>
        private async void PerformFocusStacking()
        {
            try
            {
                OperationStatus = "正在执行焦点堆叠...";
                IsOperationInProgress = true;
                
                var result = await _microscopeController?.PerformFocusStackingAsync();
                
                OperationStatus = result != null ? "焦点堆叠完成" : "焦点堆叠失败";
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraControl: 焦点堆叠失败 - {ex.Message}");
                OperationStatus = "焦点堆叠失败";
            }
            finally
            {
                IsOperationInProgress = false;
            }
        }

        /// <summary>
        /// 标定
        /// </summary>
        private void Calibrate()
        {
            try
            {
                OperationStatus = "正在执行标定...";
                // 这里应该打开标定对话框或引导用户进行标定
                // 暂时使用示例数据
                var success = _microscopeController?.CalibrateScale(100, 200); // 100微米对应200像素
                
                OperationStatus = success == true ? "标定完成" : "标定失败";
                OnPropertyChanged(nameof(CalibrationStatus));
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraControl: 标定失败 - {ex.Message}");
                OperationStatus = "标定失败";
            }
        }

        /// <summary>
        /// 距离测量
        /// </summary>
        private void MeasureDistance()
        {
            try
            {
                OperationStatus = "请在图像上选择两点进行距离测量";
                // 这里应该启用图像上的点选择模式
                LogUtil.Info("IndustrialCameraControl: 距离测量模式已启用");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraControl: 启用距离测量失败 - {ex.Message}");
            }
        }

        /// <summary>
        /// 角度测量
        /// </summary>
        private void MeasureAngle()
        {
            try
            {
                OperationStatus = "请在图像上选择三点进行角度测量";
                // 这里应该启用图像上的角度测量模式
                LogUtil.Info("IndustrialCameraControl: 角度测量模式已启用");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraControl: 启用角度测量失败 - {ex.Message}");
            }
        }

        /// <summary>
        /// 面积测量
        /// </summary>
        private void MeasureArea()
        {
            try
            {
                OperationStatus = "请在图像上绘制区域进行面积测量";
                // 这里应该启用图像上的区域绘制模式
                LogUtil.Info("IndustrialCameraControl: 面积测量模式已启用");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraControl: 启用面积测量失败 - {ex.Message}");
            }
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 连接状态变更事件处理
        /// </summary>
        private void OnConnectionStatusChanged(object sender, ConnectionStatusEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                IsConnected = e.IsConnected;
                ConnectionStatus = e.IsConnected ? "已连接" : "未连接";
                OperationStatus = e.IsConnected ? "设备连接成功" : "设备已断开";
                
                if (e.IsConnected)
                {
                    // 设备连接成功，但不自动更新参数值
                    // 保持用户当前设置的参数值不变
                    LogUtil.Info("IndustrialCameraControl: 设备连接成功，保持当前参数设置");
                }
                else
                {
                    IsPreviewRunning = false;
                    IsRecording = false;
                    PreviewImageSource = null;
                }
                
                LogUtil.Info($"IndustrialCameraControl: 连接状态变更 - {(e.IsConnected ? "已连接" : "已断开")}");
            });
        }

        /// <summary>
        /// 预览状态变更事件处理
        /// </summary>
        private void OnPreviewStatusChanged(object sender, PreviewStatusEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                IsPreviewRunning = e.IsPreviewing;
                OperationStatus = e.IsPreviewing ? "预览已启动" : "预览已停止";
                
                if (!e.IsPreviewing)
                {
                    IsRecording = false;
                    PreviewImageSource = null;
                }
                
                LogUtil.Info($"IndustrialCameraControl: 预览状态变更 - {(e.IsPreviewing ? "已启动" : "已停止")}");
            });
        }

        /// <summary>
        /// 帧捕获事件处理
        /// </summary>
        private void OnFrameCaptured(object sender, FrameCapturedEventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    // 更新预览图像
                    if (e.Frame != null && !e.Frame.Empty())
                    {
                        // 将OpenCV Mat转换为WPF ImageSource
                        PreviewImageSource = ConvertMatToImageSource(e.Frame);
                        
                        ImageResolution = $"{e.Frame.Width} x {e.Frame.Height}";
                        ImageFormat = e.Frame.Type().ToString();
                        // FrameRate通过其他方式计算，不从事件参数获取
                        
                        // 更新缩放级别
                        // ZoomLevel已经在其他地方设置，这里不需要从ScrollViewer获取
                    }
                });
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"IndustrialCameraControl: 帧处理异常 - {ex.Message}");
            }
        }

        /// <summary>
        /// 拍照完成事件处理
        /// </summary>
        private void OnPhotoCaptured(object sender, PhotoCapturedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                OperationStatus = $"图像已保存: {e.FilePath}";
                LogUtil.Info($"IndustrialCameraControl: 图像已保存 - {e.FilePath}");
            });
        }

        /// <summary>
        /// 录制状态变更事件处理
        /// </summary>
        private void OnRecordingStatusChanged(object sender, RecordingStatusEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                IsRecording = e.IsRecording;
                OperationStatus = e.IsRecording ? "录像已开始" : "录像已停止";
                
                if (e.IsRecording)
                {
                    // 开始录像时启动定时器
                    _recordingDurationTimer?.Start();
                }
                else
                {
                    // 停止录像时停止定时器并重置时间
                    _recordingDurationTimer?.Stop();
                    RecordingDuration = TimeSpan.Zero;
                }
                
                LogUtil.Info($"IndustrialCameraControl: 录制状态变更 - {(e.IsRecording ? "已开始" : "已停止")}");
            });
        }

        /// <summary>
        /// 错误发生事件处理
        /// </summary>
        private void OnErrorOccurred(object sender, ErrorOccurredEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                OperationStatus = $"错误: {e.Exception.Message}";
                LogUtil.Error($"IndustrialCameraControl: 发生错误 - {e.Exception.Message}");
                
                //// 显示错误消息
                //MessageBox.Show(e.Exception.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }

        /// <summary>
        /// 显微镜属性变更事件处理
        /// </summary>
        private void OnMicroscopePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                // 转发显微镜控制器的属性变更
                switch (e.PropertyName)
                {
                    case nameof(MicroscopeController.CurrentMagnification):
                        OnPropertyChanged(nameof(MagnificationValue));
                        break;
                    case nameof(MicroscopeController.CurrentFocusValue):
                        OnPropertyChanged(nameof(FocusValue));
                        break;
                    case nameof(MicroscopeController.IsAutoFocusEnabled):
                        OnPropertyChanged(nameof(IsAutoFocusEnabled));
                        OnPropertyChanged(nameof(CanAutoFocus));
                        break;
                    case nameof(MicroscopeController.CurrentSharpnessScore):
                        OnPropertyChanged(nameof(SharpnessScore));
                        break;
                    case nameof(MicroscopeController.LightBrightness):
                        OnPropertyChanged(nameof(LightBrightness));
                        break;
                    case nameof(MicroscopeController.LightTemperature):
                        OnPropertyChanged(nameof(LightTemperature));
                        break;
                    case nameof(MicroscopeController.IsFocusStackingEnabled):
                        OnPropertyChanged(nameof(IsFocusStackingEnabled));
                        OnPropertyChanged(nameof(CanPerformFocusStacking));
                        break;
                    case nameof(MicroscopeController.FocusStackingSteps):
                        OnPropertyChanged(nameof(FocusStackingSteps));
                        break;
                    case nameof(MicroscopeController.IsCalibrated):
                        OnPropertyChanged(nameof(CalibrationStatus));
                        break;
                }
            });
        }

        /// <summary>
        /// 自动对焦完成事件处理
        /// </summary>
        private void OnAutoFocusCompleted(object sender, AutoFocusCompletedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                OperationStatus = e.IsSuccess ? 
                    $"自动对焦完成 - 对焦值: {e.FocusValue:F3}, 清晰度: {e.SharpnessScore:F3}" : 
                    "自动对焦失败";
                
                OnPropertyChanged(nameof(CanAutoFocus));
            });
        }

        /// <summary>
        /// 测量完成事件处理
        /// </summary>
        private void OnMeasurementCompleted(object sender, MeasurementCompletedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                OperationStatus = $"测量完成 - 距离: {e.Distance:F2} {e.Unit}";
                LogUtil.Info($"IndustrialCameraControl: 测量完成 - {e.Distance:F2} {e.Unit}");
            });
        }

        /// <summary>
        /// 焦点堆叠完成事件处理
        /// </summary>
        private void OnFocusStackingCompleted(object sender, FocusStackingCompletedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                OperationStatus = "焦点堆叠完成";
                LogUtil.Info("IndustrialCameraControl: 焦点堆叠完成");
                
                // 这里可以显示合成后的图像
                if (e.StackedImage != null)
                {
                    // 将合成图像显示在预览区域或新窗口中
                }
            });
        }

        #endregion

        #region 图像增强事件处理

        /// <summary>
        /// 对比度变更事件处理
        /// </summary>
        private void OnContrastChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_cameraManager != null && _isInitialized && _imageEnhancementSettings != null)
            {
                _imageEnhancementSettings.Contrast = e.NewValue;
                _cameraManager.SetImageEnhancementSettings(_imageEnhancementSettings);
                ContrastValue = e.NewValue;
                OnPropertyChanged(nameof(ContrastValue));
                LogUtil.Info($"对比度已调整为: {e.NewValue:F2}");
            }
        }

        /// <summary>
        /// 饱和度变更事件处理
        /// </summary>
        private void OnSaturationChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_cameraManager != null && _isInitialized && _imageEnhancementSettings != null)
            {
                _imageEnhancementSettings.Saturation = e.NewValue;
                _cameraManager.SetImageEnhancementSettings(_imageEnhancementSettings);
                SaturationValue = e.NewValue;
                OnPropertyChanged(nameof(SaturationValue));
                LogUtil.Info($"饱和度已调整为: {e.NewValue:F2}");
            }
        }

        /// <summary>
        /// 亮度变更事件处理
        /// </summary>
        private void OnBrightnessChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_cameraManager != null && _imageEnhancementSettings != null)
            {
                _imageEnhancementSettings.Brightness = e.NewValue;
                _cameraManager.SetImageEnhancementSettings(_imageEnhancementSettings);
                LogUtil.Info($"亮度已调整为: {e.NewValue:F2}");
            }
        }

        /// <summary>
        /// 锐化变更事件处理
        /// </summary>
        private void OnSharpnessChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_cameraManager != null && _imageEnhancementSettings != null)
            {
                _imageEnhancementSettings.Sharpness = e.NewValue;
                _cameraManager.SetImageEnhancementSettings(_imageEnhancementSettings);
                SharpnessValue = e.NewValue;
                OnPropertyChanged(nameof(SharpnessValue));
                LogUtil.Info($"锐化已调整为: {e.NewValue:F2}");
            }
        }

        /// <summary>
        /// 降噪变更事件处理
        /// </summary>
        private void OnNoiseReductionChanged(object sender, RoutedEventArgs e)
        {
            if (_cameraManager != null && _imageEnhancementSettings != null)
            {
                var checkBox = sender as CheckBox;
                if (checkBox != null)
                {
                    _imageEnhancementSettings.IsNoiseReductionEnabled = checkBox.IsChecked ?? false;
                    _cameraManager.SetImageEnhancementSettings(_imageEnhancementSettings);
                    IsNoiseReductionEnabled = checkBox.IsChecked ?? false;
                    OnPropertyChanged(nameof(IsNoiseReductionEnabled));
                    LogUtil.Info($"降噪已{(checkBox.IsChecked == true ? "启用" : "禁用")}");
                }
            }
        }

        /// <summary>
        /// 伽马校正变更事件处理
        /// </summary>
        private void OnGammaChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_cameraManager != null && _imageEnhancementSettings != null)
            {
                _imageEnhancementSettings.Gamma = e.NewValue;
                GammaValue = e.NewValue;
                OnPropertyChanged(nameof(GammaValue));
                _cameraManager.SetImageEnhancementSettings(_imageEnhancementSettings);
                LogUtil.Info($"伽马校正已调整为: {e.NewValue:F2}");
            }
        }

        #endregion

        #region 相机参数事件处理

        /// <summary>
        /// 曝光值变更事件处理
        /// 当用户手动调整曝光滑块时，将新值应用到相机硬件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数，包含新的曝光值</param>
        private void OnExposureChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_cameraManager != null && _isInitialized)
            {
                try
                {
                    // 将曝光值应用到相机硬件
                    _cameraManager.SetCameraProperty(CameraProperty.Exposure, e.NewValue);
                    ExposureValue = e.NewValue;
                    OnPropertyChanged(nameof(ExposureValue));
                    LogUtil.Info($"曝光值已调整为: {e.NewValue:F2}");
                }
                catch (Exception ex)
                {
                    LogUtil.Error($"设置曝光值失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 增益值变更事件处理
        /// 当用户手动调整增益滑块时，将新值应用到相机硬件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数，包含新的增益值</param>
        private void OnGainChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_cameraManager != null && _isInitialized)
            {
                try
                {
                    // 将增益值应用到相机硬件
                    _cameraManager.SetCameraProperty(CameraProperty.Gain, e.NewValue);
                    GainValue = e.NewValue;
                    OnPropertyChanged(nameof(GainValue));
                    LogUtil.Info($"增益值已调整为: {e.NewValue:F2}");
                }
                catch (Exception ex)
                {
                    LogUtil.Error($"设置增益值失败: {ex.Message}");
                }
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 属性变更通知
        /// </summary>
        /// <param name="propertyName">属性名称</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 将OpenCV Mat转换为WPF ImageSource
        /// </summary>
        /// <param name="mat">OpenCV Mat对象</param>
        /// <returns>WPF ImageSource对象</returns>
        private ImageSource ConvertMatToImageSource(Mat mat)
        {
            if (mat == null || mat.Empty())
                return null;

            try
            {
                // 添加调试信息
                 int debugCounter = 0;
                debugCounter++;
                if (debugCounter % 30 == 1) // 每30帧输出一次调试信息
                {
                   // LogUtil.Debug($"ConvertMatToImageSource: 输入图像 - 尺寸:{mat.Width}x{mat.Height}, 通道:{mat.Channels()}, 类型:{mat.Type()}, 深度:{mat.Depth()}");
                }

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
                        if (debugCounter % 30 == 1)
                        {
                           // LogUtil.Debug("ConvertMatToImageSource: 检测到灰度图像，转换为RGB");
                        }
                    }
                    else if (mat.Channels() == 3)
                    {
                        // BGR转RGB
                        convertedMat = new Mat();
                        Cv2.CvtColor(mat, convertedMat, ColorConversionCodes.BGR2RGB);
                        if (debugCounter % 30 == 1)
                        {
                           // LogUtil.Debug("ConvertMatToImageSource: 检测到BGR图像，转换为RGB");
                        }
                    }
                    else if (mat.Channels() == 4)
                    {
                        // BGRA转RGBA
                        convertedMat = new Mat();
                        Cv2.CvtColor(mat, convertedMat, ColorConversionCodes.BGRA2RGBA);
                        if (debugCounter % 30 == 1)
                        {
                           // LogUtil.Debug("ConvertMatToImageSource: 检测到BGRA图像，转换为RGBA");
                        }
                    }
                    else
                    {
                        convertedMat = mat.Clone();
                        LogUtil.Warning($"不支持的图像通道数: {mat.Channels()}"); 
                    }

                    // 使用安全的方式创建BitmapSource
                    var width = convertedMat.Width;
                    var height = convertedMat.Height;
                    var stride = width * convertedMat.Channels();
                    
                    // 创建字节数组副本，避免直接使用Mat的内存指针
                    var imageData = new byte[height * stride];
                    System.Runtime.InteropServices.Marshal.Copy(convertedMat.Data, imageData, 0, imageData.Length);

                    // 确定像素格式
                    System.Windows.Media.PixelFormat pixelFormat;
                    switch (convertedMat.Channels())
                    {
                        case 1:
                            pixelFormat = System.Windows.Media.PixelFormats.Gray8;
                            break;
                        case 3:
                            pixelFormat = System.Windows.Media.PixelFormats.Rgb24;
                            break; 
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
                    
                    // 冻结BitmapSource以提高性能和线程安全
                    if (bitmapSource.CanFreeze)
                    {
                        bitmapSource.Freeze();
                    }

                    return bitmapSource;
                }
                finally
                {
                    // 确保释放临时Mat对象
                    convertedMat?.Dispose();
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"ConvertMatToImageSource失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public async void Dispose()
        {
            try
            {
                await Task.Run(() =>
                {
                    // 停止录像时间定时器
                    _recordingDurationTimer?.Stop();

                    _microscopeController?.Dispose();
                    _cameraManager?.Dispose();
                });
                
                
                LogUtil.Info("IndustrialCameraControl: 资源已释放");
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"IndustrialCameraControl: 释放资源时发生异常 - {ex.Message}");
            }
        }

        #endregion
    }

    #region 辅助类

    /// <summary>
    /// 简单的命令实现
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;
        private Action<string> startRecording;
        private Func<bool> value;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public RelayCommand(Action<string> startRecording, Func<bool> value)
        {
            this.startRecording = startRecording;
            this.value = value;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute?.Invoke() ?? true;
        }

        public void Execute(object parameter)
        {
            _execute();
        }
    }

    /// <summary>
    /// 带参数的命令实现
    /// </summary>
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Func<T, bool> _canExecute;

        public RelayCommand(Action<T> execute, Func<T, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute?.Invoke((T)parameter) ?? true;
        }

        public void Execute(object parameter)
        {
            _execute((T)parameter);
        }
    }

    #endregion
}