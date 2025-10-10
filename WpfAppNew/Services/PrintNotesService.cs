using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Windows;
using Entity.Entity;
using WpfAppNew.Controlls;
using Tools.Extend;

namespace WpfAppNew.Services
{
    /// <summary>
    /// 打印报告服务类
    /// 负责预加载和管理UCPrintNotes实例，提升打印报告的加载性能
    /// </summary>
    public class PrintNotesService
    {
        #region 单例模式

        private static readonly Lazy<PrintNotesService> _instance = new Lazy<PrintNotesService>(() => new PrintNotesService());
        
        /// <summary>
        /// 获取PrintNotesService的单例实例
        /// </summary>
        public static PrintNotesService Instance => _instance.Value;

        private PrintNotesService() 
        {
            _preloadedControls = new ConcurrentDictionary<string, UCPrintNotes>();
        }

        #endregion

        #region 私有字段

        /// <summary>
        /// 预加载的UCPrintNotes控件缓存
        /// Key: TestInfo的唯一标识
        /// Value: 预加载的UCPrintNotes实例
        /// </summary>
        private readonly ConcurrentDictionary<string, UCPrintNotes> _preloadedControls;

        /// <summary>
        /// 是否正在初始化
        /// </summary>
        private bool _isInitializing = false;

        #endregion

        #region 公共事件

        /// <summary>
        /// 预加载完成事件
        /// </summary>
        public event EventHandler<string> PreloadCompleted;

        /// <summary>
        /// 预加载失败事件
        /// </summary>
        public event EventHandler<Exception> PreloadFailed;

        #endregion

        #region 公共方法

        /// <summary>
        /// 异步启动服务初始化
        /// 在程序启动时调用，预加载常用的打印模板
        /// </summary>
        /// <returns>初始化任务</returns>
        public async Task StartAsync()
        {
            if (_isInitializing) return;
            
            _isInitializing = true;
            
            try
            {
                await Task.Run(() =>
                {
                    LogUtil.Info("PrintNotesService: 开始异步初始化打印服务");
                    
                    // 预加载DevExpress RichEdit控件相关资源
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            // 创建一个临时的RichEditControl来预热DevExpress组件
                            var tempControl = new DevExpress.Xpf.RichEdit.RichEditControl();
                            tempControl = null; // 释放临时控件
                            
                            LogUtil.Info("PrintNotesService: DevExpress RichEdit控件预热完成");
                        }
                        catch (Exception ex)
                        {
                            LogUtil.Error($"PrintNotesService: DevExpress控件预热失败: {ex.Message}");
                        }
                    });
                });
                
                LogUtil.Info("PrintNotesService: 打印服务初始化完成");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"PrintNotesService: 初始化失败: {ex.Message}");
                PreloadFailed?.Invoke(this, ex);
            }
            finally
            {
                _isInitializing = false;
            }
        }

        /// <summary>
        /// 预加载指定TestInfo的UCPrintNotes控件
        /// </summary>
        /// <param name="testInfo">测试信息</param>
        /// <returns>预加载任务</returns>
        public async Task PreloadPrintNotesAsync(TestInfo testInfo)
        {
            if (testInfo == null) return;
            
            string key = GetCacheKey(testInfo);
            
            // 如果已经缓存，直接返回
            if (_preloadedControls.ContainsKey(key)) return;
            
            try
            {
                await Task.Run(() =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            LogUtil.Info($"PrintNotesService: 开始预加载打印控件 - {testInfo.TestName}");
                            
                            var printNotes = new UCPrintNotes(testInfo);
                            _preloadedControls.TryAdd(key, printNotes);
                            
                            LogUtil.Info($"PrintNotesService: 预加载完成 - {testInfo.TestName}");
                            PreloadCompleted?.Invoke(this, key);
                        }
                        catch (Exception ex)
                        {
                            LogUtil.Error($"PrintNotesService: 预加载失败 - {testInfo.TestName}: {ex.Message}");
                            PreloadFailed?.Invoke(this, ex);
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                LogUtil.Error($"PrintNotesService: 预加载异步任务失败: {ex.Message}");
                PreloadFailed?.Invoke(this, ex);
            }
        }

        /// <summary>
        /// 获取或创建UCPrintNotes控件
        /// 优先返回预加载的控件，如果没有则创建新的
        /// </summary>
        /// <param name="testInfo">测试信息</param>
        /// <returns>UCPrintNotes控件实例</returns>
        public UCPrintNotes GetOrCreatePrintNotes(TestInfo testInfo)
        {
            if (testInfo == null) return null;
            
            string key = GetCacheKey(testInfo);
            
            // 尝试从缓存获取
            if (_preloadedControls.TryRemove(key, out UCPrintNotes cachedControl))
            {
                LogUtil.Info($"PrintNotesService: 使用预加载的打印控件 - {testInfo.TestName}");
                return cachedControl;
            }
            
            // 缓存中没有，创建新的
            LogUtil.Info($"PrintNotesService: 创建新的打印控件 - {testInfo.TestName}");
            return new UCPrintNotes(testInfo);
        }

        /// <summary>
        /// 清理指定的缓存控件
        /// </summary>
        /// <param name="testInfo">测试信息</param>
        public void ClearCache(TestInfo testInfo)
        {
            if (testInfo == null) return;
            
            string key = GetCacheKey(testInfo);
            if (_preloadedControls.TryRemove(key, out UCPrintNotes control))
            {
                try
                {
                    // 释放控件资源
                    // UserControl没有Dispose方法，使用其他方式清理资源
                    if (control != null)
                    {
                        // 清理事件处理程序
                        control.Loaded -= null;
                        control.Unloaded -= null;
                    }
                    LogUtil.Info($"PrintNotesService: 清理缓存控件 - {testInfo.TestName}");
                }
                catch (Exception ex)
                {
                    LogUtil.Error($"PrintNotesService: 清理缓存失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 清理所有缓存控件
        /// </summary>
        public void ClearAllCache()
        {
            try
            {
                foreach (var kvp in _preloadedControls)
                {
                    try
                    {
                        // UserControl没有Dispose方法，使用其他方式清理资源
                        if (kvp.Value != null)
                        {
                            // 清理事件处理程序
                            kvp.Value.Loaded -= null;
                            kvp.Value.Unloaded -= null;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogUtil.Error($"PrintNotesService: 清理控件失败: {ex.Message}");
                    }
                }
                
                _preloadedControls.Clear();
                LogUtil.Info("PrintNotesService: 清理所有缓存控件完成");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"PrintNotesService: 清理所有缓存失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取当前缓存的控件数量
        /// </summary>
        /// <returns>缓存控件数量</returns>
        public int GetCacheCount()
        {
            return _preloadedControls.Count;
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 生成缓存键
        /// </summary>
        /// <param name="testInfo">测试信息</param>
        /// <returns>缓存键</returns>
        private string GetCacheKey(TestInfo testInfo)
        {
            // 使用TestInfo的关键属性生成唯一键
            return $"{testInfo.TestName}_{testInfo.TestPath}_{testInfo.GetHashCode()}";
        }

        #endregion
    }
}