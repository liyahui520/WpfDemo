using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using WpfAppNew.OpenCv.Core;

namespace WpfAppNew.Services
{
    /// <summary>
    /// 相机初始化服务
    /// 负责在程序启动时异步初始化相机设备，提升用户体验
    /// </summary>
    public class CameraInitializationService
    {
        #region 私有字段

        /// <summary>
        /// 单例实例
        /// </summary>
        private static CameraInitializationService _instance;

        /// <summary>
        /// 线程锁对象
        /// </summary>
        private static readonly object _lock = new object();

        /// <summary>
        /// 是否正在初始化
        /// </summary>
        private bool _isInitializing = false;

        /// <summary>
        /// 是否已完成初始化
        /// </summary>
        private bool _isInitialized = false;

        /// <summary>
        /// 缓存的设备列表
        /// </summary>
        private List<CameraDevice> _cachedDevices;

        /// <summary>
        /// 初始化完成事件
        /// </summary>
        public event EventHandler<CameraInitializationCompletedEventArgs> InitializationCompleted;

        #endregion

        #region 构造函数

        /// <summary>
        /// 私有构造函数，防止外部实例化
        /// </summary>
        private CameraInitializationService()
        {
        }

        #endregion

        #region 公共属性

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static CameraInitializationService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new CameraInitializationService();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 是否正在初始化
        /// </summary>
        public bool IsInitializing => _isInitializing;

        /// <summary>
        /// 是否已完成初始化
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// 获取缓存的设备列表
        /// </summary>
        public List<CameraDevice> CachedDevices => _cachedDevices ?? new List<CameraDevice>();

        #endregion

        #region 公共方法

        /// <summary>
        /// 开始异步初始化相机设备
        /// </summary>
        /// <returns>初始化任务</returns>
        public async Task StartInitializationAsync()
        {
            if (_isInitializing || _isInitialized)
            {
                return;
            }

            _isInitializing = true;

            try
            {
                // 在后台线程执行设备扫描
                await Task.Run(() =>
                {
                    try
                    {
                        Console.WriteLine("CameraInitializationService: 开始异步扫描相机设备...");
                        
                        // 获取可用设备列表
                        _cachedDevices = CameraManager.GetAvailableDevices();
                        
                        Console.WriteLine($"CameraInitializationService: 扫描完成，找到 {_cachedDevices?.Count ?? 0} 个设备");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"CameraInitializationService: 设备扫描异常: {ex.Message}");
                        _cachedDevices = new List<CameraDevice>();
                    }
                });

                _isInitialized = true;

                // 在UI线程触发完成事件
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    InitializationCompleted?.Invoke(this, new CameraInitializationCompletedEventArgs
                    {
                        IsSuccess = true,
                        Devices = _cachedDevices,
                        Message = $"成功扫描到 {_cachedDevices?.Count ?? 0} 个相机设备"
                    });
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CameraInitializationService: 初始化异常: {ex.Message}");
                
                _cachedDevices = new List<CameraDevice>();
                _isInitialized = true;

                // 在UI线程触发完成事件
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    InitializationCompleted?.Invoke(this, new CameraInitializationCompletedEventArgs
                    {
                        IsSuccess = false,
                        Devices = _cachedDevices,
                        Message = $"相机设备初始化失败: {ex.Message}",
                        Exception = ex
                    });
                });
            }
            finally
            {
                _isInitializing = false;
            }
        }

        /// <summary>
        /// 获取设备列表（如果未初始化则等待初始化完成）
        /// </summary>
        /// <returns>设备列表</returns>
        public async Task<List<CameraDevice>> GetDevicesAsync()
        {
            if (!_isInitialized && !_isInitializing)
            {
                await StartInitializationAsync();
            }
            else if (_isInitializing)
            {
                // 等待初始化完成
                while (_isInitializing)
                {
                    await Task.Delay(100);
                }
            }

            return _cachedDevices ?? new List<CameraDevice>();
        }

        /// <summary>
        /// 刷新设备列表
        /// </summary>
        /// <returns>刷新任务</returns>
        public async Task RefreshDevicesAsync()
        {
            _isInitialized = false;
            await StartInitializationAsync();
        }

        #endregion
    }

    /// <summary>
    /// 相机初始化完成事件参数
    /// </summary>
    public class CameraInitializationCompletedEventArgs : EventArgs
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// 设备列表
        /// </summary>
        public List<CameraDevice> Devices { get; set; }

        /// <summary>
        /// 消息
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 异常信息
        /// </summary>
        public Exception Exception { get; set; }
    }
}