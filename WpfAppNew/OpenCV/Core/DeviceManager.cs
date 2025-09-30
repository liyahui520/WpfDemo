using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DirectShowLib;
using OpenCvSharp; 
using Tools.Extend;

namespace WpfAppNew.OpenCv.Core
{
    /// <summary>
    /// 摄像头设备管理器
    /// 负责设备发现、管理和切换功能
    /// 提供设备信息查询和状态监控
    /// </summary>
    public class DeviceManager : INotifyPropertyChanged, IDisposable
    {
        #region 私有字段

        private List<CameraDevice> _availableDevices;
        private CameraDevice _currentDevice;
        private bool _disposed;
        private readonly object _lockObject = new object();

        #endregion

        #region 事件定义

        /// <summary>
        /// 设备列表变更事件
        /// </summary>
        public event EventHandler<DeviceListChangedEventArgs> DeviceListChanged;

        /// <summary>
        /// 当前设备变更事件
        /// </summary>
        public event EventHandler<CurrentDeviceChangedEventArgs> CurrentDeviceChanged;

        /// <summary>
        /// 设备状态变更事件
        /// </summary>
        public event EventHandler<DeviceStatusChangedEventArgs> DeviceStatusChanged;

        /// <summary>
        /// 设备错误事件
        /// </summary>
        public event EventHandler<DeviceErrorEventArgs> DeviceError;

        #endregion

        #region 公共属性

        /// <summary>
        /// 可用设备列表
        /// </summary>
        public List<CameraDevice> AvailableDevices
        {
            get => _availableDevices ?? new List<CameraDevice>();
            private set
            {
                if (_availableDevices != value)
                {
                    _availableDevices = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 当前选中的设备
        /// </summary>
        public CameraDevice CurrentDevice
        {
            get => _currentDevice;
            private set
            {
                if (_currentDevice != value)
                {
                    var oldDevice = _currentDevice;
                    _currentDevice = value;
                    OnPropertyChanged();
                    CurrentDeviceChanged?.Invoke(this, new CurrentDeviceChangedEventArgs(oldDevice, value));
                }
            }
        }

        /// <summary>
        /// 设备数量
        /// </summary>
        public int DeviceCount => AvailableDevices.Count;

        /// <summary>
        /// 是否有可用设备
        /// </summary>
        public bool HasDevices => DeviceCount > 0;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化设备管理器
        /// </summary>
        public DeviceManager()
        {
            _availableDevices = new List<CameraDevice>();
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 异步刷新设备列表
        /// </summary>
        /// <returns>刷新任务</returns>
        public async Task RefreshDevicesAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    lock (_lockObject)
                    {
                        var oldDevices = new List<CameraDevice>(AvailableDevices);
                        var newDevices = DiscoverDevices();
                        
                        AvailableDevices = newDevices;
                        
                        // 检查当前设备是否仍然可用
                        if (CurrentDevice != null)
                        {
                            var currentDeviceStillExists = newDevices.Any(d => d.Index == CurrentDevice.Index);
                            if (!currentDeviceStillExists)
                            {
                                CurrentDevice = null;
                            }
                        }
                        
                        // 触发设备列表变更事件
                        DeviceListChanged?.Invoke(this, new DeviceListChangedEventArgs(oldDevices, newDevices));
                    }
                });
            }
            catch (Exception ex)
            {
                DeviceError?.Invoke(this, new DeviceErrorEventArgs(ex, "刷新设备列表失败"));
            }
        }

        /// <summary>
        /// 同步刷新设备列表
        /// </summary>
        public void RefreshDevices()
        {
            try
            {
                lock (_lockObject)
                {
                    var oldDevices = new List<CameraDevice>(AvailableDevices);
                    var newDevices = DiscoverDevices();
                    
                    AvailableDevices = newDevices;
                    
                    // 检查当前设备是否仍然可用
                    if (CurrentDevice != null)
                    {
                        var currentDeviceStillExists = newDevices.Any(d => d.Index == CurrentDevice.Index);
                        if (!currentDeviceStillExists)
                        {
                            CurrentDevice = null;
                        }
                    }
                    
                    // 触发设备列表变更事件
                    DeviceListChanged?.Invoke(this, new DeviceListChangedEventArgs(oldDevices, newDevices));
                }
            }
            catch (Exception ex)
            {
                DeviceError?.Invoke(this, new DeviceErrorEventArgs(ex, "刷新设备列表失败"));
            }
        }

        /// <summary>
        /// 选择设备
        /// </summary>
        /// <param name="deviceIndex">设备索引</param>
        /// <returns>是否选择成功</returns>
        public bool SelectDevice(int deviceIndex)
        {
            try
            {
                lock (_lockObject)
                {
                    var device = AvailableDevices.FirstOrDefault(d => d.Index == deviceIndex);
                    if (device == null)
                    {
                        DeviceError?.Invoke(this, new DeviceErrorEventArgs(
                            new ArgumentException($"设备索引 {deviceIndex} 不存在"), 
                            "选择设备失败"));
                        return false;
                    }

                    // 测试设备是否可用
                    if (!TestDevice(device))
                    {
                        device.Status = DeviceStatus.Error;
                        DeviceStatusChanged?.Invoke(this, new DeviceStatusChangedEventArgs(device, DeviceStatus.Available, DeviceStatus.Error));
                        return false;
                    }

                    device.Status = DeviceStatus.Selected;
                    CurrentDevice = device;
                    
                    // 更新其他设备状态
                    foreach (var otherDevice in AvailableDevices.Where(d => d.Index != deviceIndex))
                    {
                        if (otherDevice.Status == DeviceStatus.Selected)
                        {
                            otherDevice.Status = DeviceStatus.Available;
                        }
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                DeviceError?.Invoke(this, new DeviceErrorEventArgs(ex, "选择设备失败"));
                return false;
            }
        }

        /// <summary>
        /// 选择设备
        /// </summary>
        /// <param name="device">设备对象</param>
        /// <returns>是否选择成功</returns>
        public bool SelectDevice(CameraDevice device)
        {
            return device != null && SelectDevice(device.Index);
        }

        /// <summary>
        /// 获取设备信息
        /// </summary>
        /// <param name="deviceIndex">设备索引</param>
        /// <returns>设备信息</returns>
        public CameraDevice GetDeviceInfo(int deviceIndex)
        {
            lock (_lockObject)
            {
                return AvailableDevices.FirstOrDefault(d => d.Index == deviceIndex);
            }
        }

        /// <summary>
        /// 测试设备是否可用
        /// </summary>
        /// <param name="deviceIndex">设备索引</param>
        /// <returns>是否可用</returns>
        public bool IsDeviceAvailable(int deviceIndex)
        {
            var device = GetDeviceInfo(deviceIndex);
            return device != null && TestDevice(device);
        }

        /// <summary>
        /// 获取设备支持的分辨率
        /// </summary>
        /// <param name="deviceIndex">设备索引</param>
        /// <returns>支持的分辨率列表</returns>
        public List<OpenCvSharp.Size> GetSupportedResolutions(int deviceIndex)
        {
            var device = GetDeviceInfo(deviceIndex);
            return device?.SupportedResolutions ?? new List<OpenCvSharp.Size>();
        }

        /// <summary>
        /// 获取设备支持的帧率
        /// </summary>
        /// <param name="deviceIndex">设备索引</param>
        /// <returns>支持的帧率列表</returns>
        public List<double> GetSupportedFrameRates(int deviceIndex)
        {
            var device = GetDeviceInfo(deviceIndex);
            return device?.SupportedFrameRates ?? new List<double>();
        }

        /// <summary>
        /// 切换到下一个设备
        /// </summary>
        /// <returns>是否切换成功</returns>
        public bool SwitchToNextDevice()
        {
            lock (_lockObject)
            {
                if (!HasDevices) return false;

                var currentIndex = CurrentDevice?.Index ?? -1;
                var nextDevice = AvailableDevices
                    .Where(d => d.Index > currentIndex)
                    .OrderBy(d => d.Index)
                    .FirstOrDefault() ?? AvailableDevices.OrderBy(d => d.Index).FirstOrDefault();

                return nextDevice != null && SelectDevice(nextDevice.Index);
            }
        }

        /// <summary>
         /// 切换到上一个设备
         /// </summary>
         /// <returns>是否切换成功</returns>
         public bool SwitchToPreviousDevice()
         {
             lock (_lockObject)
             {
                 if (!HasDevices) return false;

                 var currentIndex = CurrentDevice?.Index ?? int.MaxValue;
                 var previousDevice = AvailableDevices
                     .Where(d => d.Index < currentIndex)
                     .OrderByDescending(d => d.Index)
                     .FirstOrDefault() ?? AvailableDevices.OrderByDescending(d => d.Index).FirstOrDefault();

                 return previousDevice != null && SelectDevice(previousDevice.Index);
             }
         }

         /// <summary>
         /// 根据设备路径选择设备
         /// </summary>
         /// <param name="devicePath">设备路径</param>
         /// <returns>是否选择成功</returns>
         public bool SelectDeviceByPath(string devicePath)
         {
             try
             {
                 if (string.IsNullOrWhiteSpace(devicePath))
                 {
                     DeviceError?.Invoke(this, new DeviceErrorEventArgs(
                         new ArgumentException("设备路径不能为空"), 
                         "选择设备失败"));
                     return false;
                 }

                 lock (_lockObject)
                 {
                     var device = AvailableDevices.FirstOrDefault(d => 
                         string.Equals(d.DevicePath, devicePath, StringComparison.OrdinalIgnoreCase));
                     
                     if (device == null)
                     {
                         DeviceError?.Invoke(this, new DeviceErrorEventArgs(
                             new ArgumentException($"设备路径 {devicePath} 不存在"), 
                             "选择设备失败"));
                         return false;
                     }

                     // 测试设备是否可用
                     if (!TestDevice(device))
                     {
                         device.Status = DeviceStatus.Error;
                         DeviceStatusChanged?.Invoke(this, new DeviceStatusChangedEventArgs(device, DeviceStatus.Available, DeviceStatus.Error));
                         return false;
                     }

                     device.Status = DeviceStatus.Selected;
                     CurrentDevice = device;
                     
                     // 更新其他设备状态
                     foreach (var otherDevice in AvailableDevices.Where(d => 
                         !string.Equals(d.DevicePath, devicePath, StringComparison.OrdinalIgnoreCase)))
                     {
                         if (otherDevice.Status == DeviceStatus.Selected)
                         {
                             otherDevice.Status = DeviceStatus.Available;
                         }
                     }

                     return true;
                 }
             }
             catch (Exception ex)
             {
                 DeviceError?.Invoke(this, new DeviceErrorEventArgs(ex, "根据路径选择设备失败"));
                 return false;
             }
         }

         /// <summary>
         /// 根据设备路径获取设备信息
         /// </summary>
         /// <param name="devicePath">设备路径</param>
         /// <returns>设备信息，如果不存在则返回null</returns>
         public CameraDevice GetDeviceByPath(string devicePath)
         {
             if (string.IsNullOrWhiteSpace(devicePath))
                 return null;

             lock (_lockObject)
             {
                 return AvailableDevices.FirstOrDefault(d => 
                     string.Equals(d.DevicePath, devicePath, StringComparison.OrdinalIgnoreCase));
             }
         }

         /// <summary>
         /// 根据设备路径测试设备是否可用
         /// </summary>
         /// <param name="devicePath">设备路径</param>
         /// <returns>是否可用</returns>
         public bool IsDeviceAvailableByPath(string devicePath)
         {
             var device = GetDeviceByPath(devicePath);
             return device != null && TestDevice(device);
         }

         /// <summary>
         /// 根据设备路径获取设备支持的分辨率
         /// </summary>
         /// <param name="devicePath">设备路径</param>
         /// <returns>支持的分辨率列表</returns>
         public List<OpenCvSharp.Size> GetSupportedResolutionsByPath(string devicePath)
         {
             var device = GetDeviceByPath(devicePath);
             return device?.SupportedResolutions ?? new List<OpenCvSharp.Size>();
         }

         /// <summary>
         /// 根据设备路径获取设备支持的帧率
         /// </summary>
         /// <param name="devicePath">设备路径</param>
         /// <returns>支持的帧率列表</returns>
         public List<double> GetSupportedFrameRatesByPath(string devicePath)
         {
             var device = GetDeviceByPath(devicePath);
             return device?.SupportedFrameRates ?? new List<double>();
         }

         /// <summary>
         /// 验证设备路径格式是否有效
         /// </summary>
         /// <param name="devicePath">设备路径</param>
         /// <returns>是否有效</returns>
         public bool IsValidDevicePath(string devicePath)
         {
             if (string.IsNullOrWhiteSpace(devicePath))
                 return false;

             // DirectShow设备路径通常以特定格式开始
             return devicePath.StartsWith(@"\\?\") || 
                    devicePath.StartsWith(@"@device:") ||
                    devicePath.Contains("vid_") ||
                    devicePath.Contains("pid_");
         }

         /// <summary>
         /// 根据友好名称查找设备
         /// </summary>
         /// <param name="friendlyName">友好名称</param>
         /// <returns>匹配的设备列表</returns>
         public List<CameraDevice> FindDevicesByFriendlyName(string friendlyName)
         {
             if (string.IsNullOrWhiteSpace(friendlyName))
                 return new List<CameraDevice>();

             lock (_lockObject)
             {
                 return AvailableDevices.Where(d => 
                     !string.IsNullOrEmpty(d.FriendlyName) &&
                     d.FriendlyName.IndexOf(friendlyName, StringComparison.OrdinalIgnoreCase) >= 0)
                     .ToList();
             }
         }

         #endregion

        #region 私有方法

        /// <summary>
        /// 发现可用设备
        /// </summary>
        /// <returns>设备列表</returns>
        private List<CameraDevice> DiscoverDevices()
        {
            var devices = new List<CameraDevice>();
            
            System.Diagnostics.Debug.WriteLine("开始设备发现过程...");
            LogUtil.Info("DeviceManager: 开始设备发现过程...");

            // 尝试检测最多10个设备
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"尝试打开设备索引 {i}...");
                    LogUtil.Info($"DeviceManager: 尝试打开设备索引 {i}...");
                    using (var capture = new VideoCapture(i))
                    {
                        if (capture.IsOpened())
                        {
                            System.Diagnostics.Debug.WriteLine($"设备 {i} 成功打开");
                            LogUtil.Info($"DeviceManager: 设备 {i} 成功打开");
                            var device = CreateDeviceInfo(i, capture);
                            devices.Add(device);
                            System.Diagnostics.Debug.WriteLine($"设备 {i} 信息创建完成: {device.Name}");
                            LogUtil.Info($"DeviceManager: 设备 {i} 信息创建完成: {device.Name}");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"设备 {i} 无法打开");
                            LogUtil.Info($"DeviceManager: 设备 {i} 无法打开");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"设备 {i} 检测异常: {ex.Message}");
                    LogUtil.Info($"DeviceManager: 设备 {i} 检测异常: {ex.Message}");
                    // 忽略无法打开的设备
                    continue;
                }
            }

            System.Diagnostics.Debug.WriteLine($"设备发现完成，共找到 {devices.Count} 个设备");
            LogUtil.Info($"DeviceManager: 设备发现完成，共找到 {devices.Count} 个设备");
            return devices;
        }

        /// <summary>
        /// 创建设备信息
        /// </summary>
        /// <param name="index">设备索引</param>
        /// <param name="capture">视频捕获对象</param>
        /// <returns>设备信息</returns>
        private CameraDevice CreateDeviceInfo(int index, VideoCapture capture)
        {
            var device = new CameraDevice
            {
                Index = index,
                Name = $"摄像头 {index}",
                Status = DeviceStatus.Available,
                IsConnected = true
            };

            try
            {
                // 获取设备属性
                device.DefaultWidth = (int)capture.Get(VideoCaptureProperties.FrameWidth);
                device.DefaultHeight = (int)capture.Get(VideoCaptureProperties.FrameHeight);
                device.Width = device.DefaultWidth;
                device.Height = device.DefaultHeight;
                device.DefaultFps = capture.Get(VideoCaptureProperties.Fps);

                // 检测支持的分辨率
                device.SupportedResolutions = DetectSupportedResolutions(capture);
                
                // 检测支持的帧率
                device.SupportedFrameRates = DetectSupportedFrameRates(capture);

                // 获取设备后端信息
                device.Backend = capture.GetBackendName();

                // 尝试获取设备名称（某些系统可能支持）
                try
                {
                    var deviceName = GetDeviceName(index);
                    if (!string.IsNullOrEmpty(deviceName))
                    {
                        device.Name = deviceName;
                    }
                }
                catch
                {
                    // 使用默认名称
                }
            }
            catch (Exception ex)
            {
                device.Status = DeviceStatus.Error;
                device.ErrorMessage = ex.Message;
            }

            return device;
        }

        /// <summary>
        /// 检测支持的分辨率
        /// </summary>
        /// <param name="capture">视频捕获对象</param>
        /// <returns>分辨率列表</returns>
        private List<OpenCvSharp.Size> DetectSupportedResolutions(VideoCapture capture)
        {
            var resolutions = new List<OpenCvSharp.Size>();
            var commonResolutions = new[]
            {
                new OpenCvSharp.Size(160, 120),   // QQVGA
                new OpenCvSharp.Size(320, 240),   // QVGA
                new OpenCvSharp.Size(640, 480),   // VGA
                new OpenCvSharp.Size(800, 600),   // SVGA
                new OpenCvSharp.Size(1024, 768),  // XGA
                new OpenCvSharp.Size(1280, 720),  // HD
                new OpenCvSharp.Size(1280, 960),  // SXGA
                new OpenCvSharp.Size(1600, 1200), // UXGA
                new OpenCvSharp.Size(1920, 1080), // Full HD
                new OpenCvSharp.Size(2560, 1440), // QHD
                new OpenCvSharp.Size(3840, 2160)  // 4K
            };

            foreach (var resolution in commonResolutions)
            {
                try
                {
                    capture.Set(VideoCaptureProperties.FrameWidth, resolution.Width);
                    capture.Set(VideoCaptureProperties.FrameHeight, resolution.Height);

                    var actualWidth = (int)capture.Get(VideoCaptureProperties.FrameWidth);
                    var actualHeight = (int)capture.Get(VideoCaptureProperties.FrameHeight);

                    if (actualWidth == resolution.Width && actualHeight == resolution.Height)
                    {
                        resolutions.Add(resolution);
                    }
                }
                catch
                {
                    // 忽略不支持的分辨率
                }
            }

            return resolutions.Distinct().ToList();
        }

        /// <summary>
        /// 检测支持的帧率
        /// </summary>
        /// <param name="capture">视频捕获对象</param>
        /// <returns>帧率列表</returns>
        private List<double> DetectSupportedFrameRates(VideoCapture capture)
        {
            var frameRates = new List<double> { 15, 24, 25, 30, 50, 60 };
            var supportedRates = new List<double>();

            foreach (var rate in frameRates)
            {
                try
                {
                    capture.Set(VideoCaptureProperties.Fps, rate);
                    var actualRate = capture.Get(VideoCaptureProperties.Fps);
                    
                    if (Math.Abs(actualRate - rate) < 1.0)
                    {
                        supportedRates.Add(rate);
                    }
                }
                catch
                {
                    // 忽略不支持的帧率
                }
            }

            return supportedRates.Distinct().ToList();
        }

        /// <summary>
        /// 测试设备是否可用
        /// </summary>
        /// <param name="device">设备信息</param>
        /// <returns>是否可用</returns>
        private bool TestDevice(CameraDevice device)
        {
            try
            {
                using (var capture = new VideoCapture(device.Index))
                {
                    if (!capture.IsOpened())
                    {
                        return false;
                    }

                    using (var frame = new Mat())
                    {
                        return capture.Read(frame) && !frame.Empty();
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取设备名称（Windows特定实现）
        /// </summary>
        /// <param name="deviceIndex">设备索引</param>
        /// <returns>设备名称</returns>
        private string GetDeviceName(int deviceIndex)
        {
            // 这里可以实现特定平台的设备名称获取逻辑
            // 例如在Windows上使用DirectShow API
            return $"摄像头 {deviceIndex}";
        }

        #endregion

        #region INotifyPropertyChanged 实现

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region IDisposable 实现

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 清理托管资源
                    _availableDevices?.Clear();
                    _currentDevice = null;
                }
                _disposed = true;
            }
        }

        ~DeviceManager()
        {
            Dispose(false);
        }

        #endregion
    }

    #region 数据类和枚举

    /// <summary>
    /// 摄像头设备信息
    /// 用于管理宠物监控系统中的摄像头设备
    /// </summary>
    public class CameraDevice : INotifyPropertyChanged
    {
        private DeviceStatus _status;
        private bool _isConnected;
        private string _errorMessage;

        /// <summary>
        /// 设备索引
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// 设备名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 设备状态
        /// </summary>
        public DeviceStatus Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 是否已连接
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
                }
            }
        }

        /// <summary>
        /// 默认宽度
        /// </summary>
        public int DefaultWidth { get; set; }

        /// <summary>
        /// 默认高度
        /// </summary>
        public int DefaultHeight { get; set; }

        /// <summary>
        /// 当前宽度
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// 当前高度
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// 默认帧率
        /// </summary>
        public double DefaultFps { get; set; }

        /// <summary>
        /// 支持的分辨率
        /// </summary>
        public List<OpenCvSharp.Size> SupportedResolutions { get; set; } = new List<OpenCvSharp.Size>();

        /// <summary>
        /// 支持的帧率
        /// </summary>
        public List<double> SupportedFrameRates { get; set; } = new List<double>();

        /// <summary>
        /// 后端名称
        /// </summary>
        public string Backend { get; set; }

        /// <summary>
        /// DirectShow设备信息
        /// </summary>
        public DsDevice DsDevice { get; set; }

        /// <summary>
        /// 设备友好名称（从DirectShow获取）
        /// </summary>
        public string FriendlyName { get; set; }

        /// <summary>
        /// 设备路径（从DirectShow获取）
        /// 用于唯一标识设备，不依赖于设备索引
        /// </summary>
        public string DevicePath { get; set; }

        /// <summary>
        /// 错误消息
        /// </summary>
        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                if (_errorMessage != value)
                {
                    _errorMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 默认分辨率
        /// </summary>
        public OpenCvSharp.Size DefaultSize => new OpenCvSharp.Size(DefaultWidth, DefaultHeight);

        /// <summary>
        /// 当前分辨率
        /// </summary>
        public OpenCvSharp.Size CurrentSize => new OpenCvSharp.Size(Width, Height);

        /// <summary>
        /// 设备显示名称（优先使用友好名称，否则使用设备名称）
        /// </summary>
        public string DisplayName => !string.IsNullOrEmpty(FriendlyName) ? FriendlyName : Name;

        /// <summary>
        /// 设备唯一标识符（优先使用设备路径，否则使用索引）
        /// </summary>
        public string UniqueId => !string.IsNullOrEmpty(DevicePath) ? DevicePath : $"Index_{Index}";

        /// <summary>
        /// 是否有有效的设备路径
        /// </summary>
        public bool HasValidDevicePath => !string.IsNullOrEmpty(DevicePath) && IsValidDevicePath(DevicePath);

        /// <summary>
        /// 设备是否可用（已连接且状态正常）
        /// </summary>
        public bool IsAvailable => IsConnected && (Status == DeviceStatus.Available || Status == DeviceStatus.Selected);

        /// <summary>
        /// 设备详细信息字符串
        /// </summary>
        public string DetailedInfo => $"{DisplayName} - {CurrentSize.Width}x{CurrentSize.Height}@{DefaultFps:F1}fps - {Backend}";

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 验证设备路径格式是否有效
        /// </summary>
        /// <param name="devicePath">设备路径</param>
        /// <returns>是否有效</returns>
        public static bool IsValidDevicePath(string devicePath)
        {
            if (string.IsNullOrWhiteSpace(devicePath))
                return false;

            // DirectShow设备路径通常以特定格式开始
            return devicePath.StartsWith(@"\\?\") || 
                   devicePath.StartsWith(@"@device:") ||
                   devicePath.Contains("vid_") ||
                   devicePath.Contains("pid_") ||
                   devicePath.Contains("usb#");
        }

        /// <summary>
        /// 比较两个设备是否为同一设备（基于设备路径）
        /// </summary>
        /// <param name="other">另一个设备</param>
        /// <returns>是否为同一设备</returns>
        public bool IsSameDevice(CameraDevice other)
        {
            if (other == null) return false;

            // 优先使用设备路径比较
            if (HasValidDevicePath && other.HasValidDevicePath)
            {
                return string.Equals(DevicePath, other.DevicePath, StringComparison.OrdinalIgnoreCase);
            }

            // 如果没有有效的设备路径，使用索引比较
            return Index == other.Index;
        }

        /// <summary>
        /// 获取设备路径的简短显示形式
        /// </summary>
        /// <returns>简短的设备路径</returns>
        public string GetShortDevicePath()
        {
            if (string.IsNullOrEmpty(DevicePath))
                return $"Index_{Index}";

            // 提取设备路径中的关键信息
            if (DevicePath.Contains("vid_") && DevicePath.Contains("pid_"))
            {
                var vidIndex = DevicePath.IndexOf("vid_", StringComparison.OrdinalIgnoreCase);
                var pidIndex = DevicePath.IndexOf("pid_", StringComparison.OrdinalIgnoreCase);
                
                if (vidIndex >= 0 && pidIndex >= 0)
                {
                    var vid = DevicePath.Substring(vidIndex, 8); // vid_xxxx
                    var pid = DevicePath.Substring(pidIndex, 8); // pid_xxxx
                    return $"{vid}_{pid}";
                }
            }

            // 如果路径太长，只显示最后部分
            if (DevicePath.Length > 30)
            {
                return "..." + DevicePath.Substring(DevicePath.Length - 27);
            }

            return DevicePath;
        }

        /// <summary>
        /// 克隆设备信息
        /// </summary>
        /// <returns>设备信息副本</returns>
        public CameraDevice Clone()
        {
            return new CameraDevice
            {
                Index = this.Index,
                Name = this.Name,
                Status = this.Status,
                IsConnected = this.IsConnected,
                DefaultWidth = this.DefaultWidth,
                DefaultHeight = this.DefaultHeight,
                Width = this.Width,
                Height = this.Height,
                DefaultFps = this.DefaultFps,
                SupportedResolutions = new List<OpenCvSharp.Size>(this.SupportedResolutions),
                SupportedFrameRates = new List<double>(this.SupportedFrameRates),
                Backend = this.Backend,
                DsDevice = this.DsDevice,
                FriendlyName = this.FriendlyName,
                DevicePath = this.DevicePath,
                ErrorMessage = this.ErrorMessage
            };
        }

        public override string ToString()
        {
            return $"{DisplayName} ({DefaultWidth}x{DefaultHeight}) - {GetShortDevicePath()}";
        }

        public override bool Equals(object obj)
        {
            return obj is CameraDevice other && IsSameDevice(other);
        }

        public override int GetHashCode()
        {
            return HasValidDevicePath ? DevicePath.GetHashCode() : Index.GetHashCode();
        }
    }

    /// <summary>
    /// 设备状态枚举
    /// </summary>
    public enum DeviceStatus
    {
        /// <summary>
        /// 可用
        /// </summary>
        Available,

        /// <summary>
        /// 已选中
        /// </summary>
        Selected,

        /// <summary>
        /// 使用中
        /// </summary>
        InUse,

        /// <summary>
        /// 错误
        /// </summary>
        Error,

        /// <summary>
        /// 断开连接
        /// </summary>
        Disconnected
    }

    #endregion

    #region 事件参数类

    /// <summary>
    /// 设备列表变更事件参数
    /// 用于宠物监控系统中设备列表变化的通知
    /// </summary>
    public class DeviceListChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 旧设备列表
        /// </summary>
        public List<CameraDevice> OldDevices { get; }

        /// <summary>
        /// 新设备列表
        /// </summary>
        public List<CameraDevice> NewDevices { get; }

        /// <summary>
        /// 添加的设备
        /// </summary>
        public List<CameraDevice> AddedDevices { get; }

        /// <summary>
        /// 移除的设备
        /// </summary>
        public List<CameraDevice> RemovedDevices { get; }

        /// <summary>
        /// 添加的设备路径列表
        /// </summary>
        public List<string> AddedDevicePaths { get; }

        /// <summary>
        /// 移除的设备路径列表
        /// </summary>
        public List<string> RemovedDevicePaths { get; }

        /// <summary>
        /// 是否有设备添加
        /// </summary>
        public bool HasDevicesAdded => AddedDevices.Count > 0;

        /// <summary>
        /// 是否有设备移除
        /// </summary>
        public bool HasDevicesRemoved => RemovedDevices.Count > 0;

        /// <summary>
        /// 设备变更摘要
        /// </summary>
        public string ChangesSummary => $"添加: {AddedDevices.Count}, 移除: {RemovedDevices.Count}";

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="oldDevices">旧设备列表</param>
        /// <param name="newDevices">新设备列表</param>
        public DeviceListChangedEventArgs(List<CameraDevice> oldDevices, List<CameraDevice> newDevices)
        {
            OldDevices = oldDevices ?? new List<CameraDevice>();
            NewDevices = newDevices ?? new List<CameraDevice>();

            // 基于设备路径进行比较，如果没有路径则使用索引
            AddedDevices = NewDevices.Where(n => !OldDevices.Any(o => IsSameDevice(o, n))).ToList();
            RemovedDevices = OldDevices.Where(o => !NewDevices.Any(n => IsSameDevice(o, n))).ToList();

            AddedDevicePaths = AddedDevices
                .Where(d => !string.IsNullOrEmpty(d.DevicePath))
                .Select(d => d.DevicePath)
                .ToList();

            RemovedDevicePaths = RemovedDevices
                .Where(d => !string.IsNullOrEmpty(d.DevicePath))
                .Select(d => d.DevicePath)
                .ToList();
        }

        /// <summary>
        /// 判断两个设备是否为同一设备
        /// </summary>
        /// <param name="device1">设备1</param>
        /// <param name="device2">设备2</param>
        /// <returns>是否为同一设备</returns>
        private bool IsSameDevice(CameraDevice device1, CameraDevice device2)
        {
            if (device1 == null || device2 == null) return false;

            // 优先使用设备路径比较
            if (!string.IsNullOrEmpty(device1.DevicePath) && !string.IsNullOrEmpty(device2.DevicePath))
            {
                return string.Equals(device1.DevicePath, device2.DevicePath, StringComparison.OrdinalIgnoreCase);
            }

            // 如果没有设备路径，使用索引比较
            return device1.Index == device2.Index;
        }

        /// <summary>
        /// 根据设备路径查找添加的设备
        /// </summary>
        /// <param name="devicePath">设备路径</param>
        /// <returns>设备信息，如果不存在则返回null</returns>
        public CameraDevice FindAddedDeviceByPath(string devicePath)
        {
            if (string.IsNullOrEmpty(devicePath)) return null;
            return AddedDevices.FirstOrDefault(d => 
                string.Equals(d.DevicePath, devicePath, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 根据设备路径查找移除的设备
        /// </summary>
        /// <param name="devicePath">设备路径</param>
        /// <returns>设备信息，如果不存在则返回null</returns>
        public CameraDevice FindRemovedDeviceByPath(string devicePath)
        {
            if (string.IsNullOrEmpty(devicePath)) return null;
            return RemovedDevices.FirstOrDefault(d => 
                string.Equals(d.DevicePath, devicePath, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// 当前设备变更事件参数
    /// 用于宠物监控系统中当前设备切换的通知
    /// </summary>
    public class CurrentDeviceChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 旧设备
        /// </summary>
        public CameraDevice OldDevice { get; }

        /// <summary>
        /// 新设备
        /// </summary>
        public CameraDevice NewDevice { get; }

        /// <summary>
        /// 旧设备路径
        /// </summary>
        public string OldDevicePath => OldDevice?.DevicePath;

        /// <summary>
        /// 新设备路径
        /// </summary>
        public string NewDevicePath => NewDevice?.DevicePath;

        /// <summary>
        /// 旧设备显示名称
        /// </summary>
        public string OldDeviceDisplayName => OldDevice?.DisplayName ?? "无设备";

        /// <summary>
        /// 新设备显示名称
        /// </summary>
        public string NewDeviceDisplayName => NewDevice?.DisplayName ?? "无设备";

        /// <summary>
        /// 是否从无设备切换到有设备
        /// </summary>
        public bool IsDeviceConnected => OldDevice == null && NewDevice != null;

        /// <summary>
        /// 是否从有设备切换到无设备
        /// </summary>
        public bool IsDeviceDisconnected => OldDevice != null && NewDevice == null;

        /// <summary>
        /// 是否为设备切换（从一个设备切换到另一个设备）
        /// </summary>
        public bool IsDeviceSwitch => OldDevice != null && NewDevice != null && !OldDevice.IsSameDevice(NewDevice);

        /// <summary>
        /// 设备变更摘要
        /// </summary>
        public string ChangesSummary
        {
            get
            {
                if (IsDeviceConnected) return $"设备连接: {NewDeviceDisplayName}";
                if (IsDeviceDisconnected) return $"设备断开: {OldDeviceDisplayName}";
                if (IsDeviceSwitch) return $"设备切换: {OldDeviceDisplayName} → {NewDeviceDisplayName}";
                return "无变更";
            }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="oldDevice">旧设备</param>
        /// <param name="newDevice">新设备</param>
        public CurrentDeviceChangedEventArgs(CameraDevice oldDevice, CameraDevice newDevice)
        {
            OldDevice = oldDevice;
            NewDevice = newDevice;
        }

        /// <summary>
        /// 检查是否涉及指定的设备路径
        /// </summary>
        /// <param name="devicePath">设备路径</param>
        /// <returns>是否涉及该设备路径</returns>
        public bool InvolvesDevicePath(string devicePath)
        {
            if (string.IsNullOrEmpty(devicePath)) return false;

            return string.Equals(OldDevicePath, devicePath, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(NewDevicePath, devicePath, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// 设备状态变更事件参数
    /// 用于宠物监控系统中设备状态变化的通知
    /// </summary>
    public class DeviceStatusChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 设备
        /// </summary>
        public CameraDevice Device { get; }

        /// <summary>
        /// 旧状态
        /// </summary>
        public DeviceStatus OldStatus { get; }

        /// <summary>
        /// 新状态
        /// </summary>
        public DeviceStatus NewStatus { get; }

        /// <summary>
        /// 设备路径
        /// </summary>
        public string DevicePath => Device?.DevicePath;

        /// <summary>
        /// 设备显示名称
        /// </summary>
        public string DeviceDisplayName => Device?.DisplayName ?? "未知设备";

        /// <summary>
        /// 设备唯一标识符
        /// </summary>
        public string DeviceUniqueId => Device?.UniqueId ?? "未知";

        /// <summary>
        /// 是否为错误状态变更
        /// </summary>
        public bool IsErrorStatusChange => NewStatus == DeviceStatus.Error;

        /// <summary>
        /// 是否为可用状态变更
        /// </summary>
        public bool IsAvailableStatusChange => NewStatus == DeviceStatus.Available;

        /// <summary>
        /// 是否为选中状态变更
        /// </summary>
        public bool IsSelectedStatusChange => NewStatus == DeviceStatus.Selected;

        /// <summary>
        /// 是否为使用中状态变更
        /// </summary>
        public bool IsInUseStatusChange => NewStatus == DeviceStatus.InUse;

        /// <summary>
        /// 是否为断开连接状态变更
        /// </summary>
        public bool IsDisconnectedStatusChange => NewStatus == DeviceStatus.Disconnected;

        /// <summary>
        /// 状态变更摘要
        /// </summary>
        public string StatusChangeSummary => $"{DeviceDisplayName}: {GetStatusDisplayName(OldStatus)} → {GetStatusDisplayName(NewStatus)}";

        /// <summary>
        /// 变更时间戳
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="device">设备</param>
        /// <param name="oldStatus">旧状态</param>
        /// <param name="newStatus">新状态</param>
        public DeviceStatusChangedEventArgs(CameraDevice device, DeviceStatus oldStatus, DeviceStatus newStatus)
        {
            Device = device;
            OldStatus = oldStatus;
            NewStatus = newStatus;
            Timestamp = DateTime.Now;
        }

        /// <summary>
        /// 检查是否涉及指定的设备路径
        /// </summary>
        /// <param name="devicePath">设备路径</param>
        /// <returns>是否涉及该设备路径</returns>
        public bool InvolvesDevicePath(string devicePath)
        {
            if (string.IsNullOrEmpty(devicePath) || Device == null) return false;
            return string.Equals(Device.DevicePath, devicePath, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 获取状态的显示名称
        /// </summary>
        /// <param name="status">设备状态</param>
        /// <returns>状态显示名称</returns>
        private string GetStatusDisplayName(DeviceStatus status)
        {
            switch (status)
            {
                case DeviceStatus.Available:
                    return "可用";
                case DeviceStatus.Selected:
                    return "已选中";
                case DeviceStatus.InUse:
                    return "使用中";
                case DeviceStatus.Error:
                    return "错误";
                case DeviceStatus.Disconnected:
                    return "已断开";
                default:
                    return "未知";
            }
        }
    }

    /// <summary>
    /// 设备错误事件参数
    /// 用于宠物监控系统中设备错误的通知和处理
    /// </summary>
    public class DeviceErrorEventArgs : EventArgs
    {
        /// <summary>
        /// 异常对象
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// 错误消息
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// 相关设备（可选）
        /// </summary>
        public CameraDevice RelatedDevice { get; }

        /// <summary>
        /// 相关设备路径
        /// </summary>
        public string RelatedDevicePath => RelatedDevice?.DevicePath;

        /// <summary>
        /// 相关设备显示名称
        /// </summary>
        public string RelatedDeviceDisplayName => RelatedDevice?.DisplayName;

        /// <summary>
        /// 错误级别
        /// </summary>
        public ErrorLevel Level { get; }

        /// <summary>
        /// 错误类型
        /// </summary>
        public ErrorType Type { get; }

        /// <summary>
        /// 是否为设备相关错误
        /// </summary>
        public bool IsDeviceRelated => RelatedDevice != null;

        /// <summary>
        /// 完整错误信息
        /// </summary>
        public string FullErrorMessage
        {
            get
            {
                var message = Message;
                if (IsDeviceRelated)
                {
                    message = $"[{RelatedDeviceDisplayName}] {message}";
                }
                if (Exception != null)
                {
                    message += $" - {Exception.Message}";
                }
                return message;
            }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="exception">异常对象</param>
        /// <param name="message">错误消息</param>
        /// <param name="relatedDevice">相关设备</param>
        /// <param name="level">错误级别</param>
        /// <param name="type">错误类型</param>
        public DeviceErrorEventArgs(Exception exception, string message, CameraDevice relatedDevice = null, 
            ErrorLevel level = ErrorLevel.Error, ErrorType type = ErrorType.General)
        {
            Exception = exception;
            Message = message;
            RelatedDevice = relatedDevice;
            Level = level;
            Type = type;
            Timestamp = DateTime.Now;
        }

        /// <summary>
        /// 检查是否涉及指定的设备路径
        /// </summary>
        /// <param name="devicePath">设备路径</param>
        /// <returns>是否涉及该设备路径</returns>
        public bool InvolvesDevicePath(string devicePath)
        {
            if (string.IsNullOrEmpty(devicePath) || RelatedDevice == null) return false;
            return string.Equals(RelatedDevice.DevicePath, devicePath, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 获取错误级别的显示名称
        /// </summary>
        /// <returns>错误级别显示名称</returns>
        public string GetLevelDisplayName()
        {
            switch (Level)
            {
                case ErrorLevel.Info:
                    return "信息";
                case ErrorLevel.Warning:
                    return "警告";
                case ErrorLevel.Error:
                    return "错误";
                case ErrorLevel.Critical:
                    return "严重错误";
                default:
                    return "未知";
            }
        }

        /// <summary>
        /// 获取错误类型的显示名称
        /// </summary>
        /// <returns>错误类型显示名称</returns>
        public string GetTypeDisplayName()
        {
            switch (Type)
            {
                case ErrorType.General:
                    return "一般错误";
                case ErrorType.DeviceNotFound:
                    return "设备未找到";
                case ErrorType.DeviceAccessDenied:
                    return "设备访问被拒绝";
                case ErrorType.DeviceInUse:
                    return "设备正在使用";
                case ErrorType.DeviceDisconnected:
                    return "设备已断开";
                case ErrorType.InvalidDevicePath:
                    return "无效设备路径";
                case ErrorType.UnsupportedOperation:
                    return "不支持的操作";
                case ErrorType.ConfigurationError:
                    return "配置错误";
                default:
                    return "未知错误";
            }
        }
    }

    /// <summary>
    /// 错误级别枚举
    /// </summary>
    public enum ErrorLevel
    {
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
        /// 严重错误
        /// </summary>
        Critical
    }

    /// <summary>
    /// 错误类型枚举
    /// </summary>
    public enum ErrorType
    {
        /// <summary>
        /// 一般错误
        /// </summary>
        General,

        /// <summary>
        /// 设备未找到
        /// </summary>
        DeviceNotFound,

        /// <summary>
        /// 设备访问被拒绝
        /// </summary>
        DeviceAccessDenied,

        /// <summary>
        /// 设备正在使用
        /// </summary>
        DeviceInUse,

        /// <summary>
        /// 设备已断开
        /// </summary>
        DeviceDisconnected,

        /// <summary>
        /// 无效设备路径
        /// </summary>
        InvalidDevicePath,

        /// <summary>
        /// 不支持的操作
        /// </summary>
        UnsupportedOperation,

        /// <summary>
        /// 配置错误
        /// </summary>
        ConfigurationError
    }

    #endregion
}