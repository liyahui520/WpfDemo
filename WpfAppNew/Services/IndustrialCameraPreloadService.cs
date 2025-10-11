using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using WpfAppNew.EmguPlugs;
using WpfAppNew.OpenCv.Core;
using Tools.Extend;

namespace WpfAppNew.Services
{
    /// <summary>
    /// IndustrialCameraControl预加载完成事件参数
    /// </summary>
    public class IndustrialCameraPreloadCompletedEventArgs : EventArgs
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// 预加载的控件实例
        /// </summary>
        public IndustrialCameraControl PreloadedControl { get; set; }

        /// <summary>
        /// 可用设备列表
        /// </summary>
        public List<CameraDevice> AvailableDevices { get; set; }

        /// <summary>
        /// 消息
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 异常信息
        /// </summary>
        public Exception Exception { get; set; }
    }

    /// <summary>
    /// IndustrialCameraControl预加载服务
    /// 负责在程序启动时预加载IndustrialCameraControl实例，提升用户体验
    /// </summary>
    public class IndustrialCameraPreloadService
    {
        #region 私有字段

        /// <summary>
        /// 单例实例
        /// </summary>
        private static IndustrialCameraPreloadService _instance;

        /// <summary>
        /// 线程锁对象
        /// </summary>
        private static readonly object _lock = new object();

        /// <summary>
        /// 是否正在预加载
        /// </summary>
        private bool _isPreloading = false;

        /// <summary>
        /// 是否已完成预加载
        /// </summary>
        private bool _isPreloaded = false;

        /// <summary>
        /// 预加载的控件实例
        /// </summary>
        private IndustrialCameraControl _preloadedControl;

        /// <summary>
        /// 可用设备列表
        /// </summary>
        private List<CameraDevice> _availableDevices;

        /// <summary>
        /// 预加载完成事件
        /// </summary>
        public event EventHandler<IndustrialCameraPreloadCompletedEventArgs> PreloadCompleted;

        #endregion

        #region 构造函数

        /// <summary>
        /// 私有构造函数，防止外部实例化
        /// </summary>
        private IndustrialCameraPreloadService()
        {
        }

        #endregion

        #region 公共属性

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static IndustrialCameraPreloadService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new IndustrialCameraPreloadService();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 是否正在预加载
        /// </summary>
        public bool IsPreloading => _isPreloading;

        /// <summary>
        /// 是否已完成预加载
        /// </summary>
        public bool IsPreloaded => _isPreloaded;

        /// <summary>
        /// 获取预加载的控件实例
        /// </summary>
        public IndustrialCameraControl PreloadedControl => _preloadedControl;

        /// <summary>
        /// 获取可用设备列表
        /// </summary>
        public List<CameraDevice> AvailableDevices => _availableDevices ?? new List<CameraDevice>();

        #endregion

        #region 公共方法

        /// <summary>
        /// 开始异步预加载IndustrialCameraControl
        /// </summary>
        /// <returns>预加载任务</returns>
        public async Task StartPreloadAsync()
        {
            if (_isPreloading || _isPreloaded)
            {
                LogUtil.Info("IndustrialCameraControl已经预加载或正在预加载中");
                return;
            }

            _isPreloading = true;
            LogUtil.Info("开始预加载IndustrialCameraControl...");

            try
            {
                // 在UI线程创建控件实例
                await Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    try
                    {
                        // 创建IndustrialCameraControl实例
                        _preloadedControl = new IndustrialCameraControl();
                        
                        LogUtil.Info("IndustrialCameraControl实例创建成功");

                        // 等待控件初始化完成
                        await Task.Delay(1000);

                        // 获取可用设备列表
                        if (_preloadedControl.AvailableDevices != null)
                        {
                            _availableDevices = new List<CameraDevice>(_preloadedControl.AvailableDevices);
                            LogUtil.Info($"预加载完成，找到 {_availableDevices.Count} 个相机设备");
                        }
                        else
                        {
                            _availableDevices = new List<CameraDevice>();
                            LogUtil.Warning("预加载完成，但未找到可用设备");
                        }

                        _isPreloaded = true;

                        // 触发预加载完成事件
                        PreloadCompleted?.Invoke(this, new IndustrialCameraPreloadCompletedEventArgs
                        {
                            IsSuccess = true,
                            PreloadedControl = _preloadedControl,
                            AvailableDevices = _availableDevices,
                            Message = $"IndustrialCameraControl预加载成功，找到 {_availableDevices.Count} 个设备"
                        });

                        LogUtil.Info("IndustrialCameraControl预加载完成");
                    }
                    catch (Exception ex)
                    {
                        LogUtil.Error($"IndustrialCameraControl预加载异常: {ex.Message}");
                        
                        _availableDevices = new List<CameraDevice>();
                        _isPreloaded = true;

                        // 触发预加载完成事件
                        PreloadCompleted?.Invoke(this, new IndustrialCameraPreloadCompletedEventArgs
                        {
                            IsSuccess = false,
                            PreloadedControl = null,
                            AvailableDevices = _availableDevices,
                            Message = $"IndustrialCameraControl预加载失败: {ex.Message}",
                            Exception = ex
                        });
                    }
                }, DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraControl预加载服务异常: {ex.Message}");
                
                _availableDevices = new List<CameraDevice>();
                _isPreloaded = true;

                // 触发预加载完成事件
                PreloadCompleted?.Invoke(this, new IndustrialCameraPreloadCompletedEventArgs
                {
                    IsSuccess = false,
                    PreloadedControl = null,
                    AvailableDevices = _availableDevices,
                    Message = $"IndustrialCameraControl预加载服务异常: {ex.Message}",
                    Exception = ex
                });
            }
            finally
            {
                _isPreloading = false;
            }
        }

        /// <summary>
        /// 获取或创建IndustrialCameraControl实例
        /// </summary>
        /// <returns>控件实例</returns>
        public async Task<IndustrialCameraControl> GetOrCreateControlAsync()
        {
            if (!_isPreloaded && !_isPreloading)
            {
                await StartPreloadAsync();
            }
            else if (_isPreloading)
            {
                // 等待预加载完成
                while (_isPreloading)
                {
                    await Task.Delay(100);
                }
            }

            // 如果预加载成功，返回预加载的实例
            if (_preloadedControl != null)
            {
                var control = _preloadedControl;
                _preloadedControl = null; // 清空引用，避免重复使用
                LogUtil.Info("返回预加载的IndustrialCameraControl实例");
                return control;
            }

            // 如果预加载失败，创建新实例
            LogUtil.Warning("预加载失败，创建新的IndustrialCameraControl实例");
            return new IndustrialCameraControl();
        }

        /// <summary>
        /// 获取可用设备列表（如果未预加载则等待预加载完成）
        /// </summary>
        /// <returns>设备列表</returns>
        public async Task<List<CameraDevice>> GetAvailableDevicesAsync()
        {
            if (!_isPreloaded && !_isPreloading)
            {
                await StartPreloadAsync();
            }
            else if (_isPreloading)
            {
                // 等待预加载完成
                while (_isPreloading)
                {
                    await Task.Delay(100);
                }
            }

            return _availableDevices ?? new List<CameraDevice>();
        }

        /// <summary>
        /// 重置预加载状态（用于刷新设备列表）
        /// </summary>
        public void Reset()
        {
            LogUtil.Info("重置IndustrialCameraControl预加载状态");
            
            _isPreloading = false;
            _isPreloaded = false;
            _preloadedControl = null;
            _availableDevices = null;
        }

        #endregion
    }
}