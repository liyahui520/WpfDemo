using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Tools.App;
using Tools.Extend;

namespace WpfAppNew.Utils
{
    /// <summary>
    /// UI更新节流器
    /// 用于优化频繁的UI更新操作，减少UI线程压力
    /// 适用于宠物医疗设备的实时数据显示场景
    /// </summary>
    public class UIUpdateThrottler : IDisposable
    {
        #region 私有字段

        private readonly Dispatcher _dispatcher;
        private readonly int _throttleIntervalMs;
        private readonly Timer _timer;
        private Action _pendingAction;
        private readonly object _lockObject = new object();
        private bool _isDisposed;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化UI更新节流器
        /// </summary>
        /// <param name="dispatcher">UI调度器</param>
        /// <param name="throttleIntervalMs">节流间隔（毫秒），默认100ms</param>
        /// <exception cref="ArgumentNullException">调度器为空时抛出</exception>
        /// <exception cref="ArgumentOutOfRangeException">节流间隔小于等于0时抛出</exception>
        public UIUpdateThrottler(Dispatcher dispatcher, int throttleIntervalMs = 100)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            
            if (throttleIntervalMs <= 0)
                throw new ArgumentOutOfRangeException(nameof(throttleIntervalMs), "节流间隔必须大于0");

            _throttleIntervalMs = throttleIntervalMs;
            _timer = new Timer(OnTimerElapsed, null, Timeout.Infinite, Timeout.Infinite);
            
            LogUtil.Info($"UIUpdateThrottler初始化完成，节流间隔: {throttleIntervalMs}ms");
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 请求UI更新
        /// 如果在节流间隔内有多次请求，只会执行最后一次
        /// </summary>
        /// <param name="updateAction">更新操作</param>
        /// <exception cref="ArgumentNullException">更新操作为空时抛出</exception>
        /// <exception cref="ObjectDisposedException">对象已释放时抛出</exception>
        public void RequestUpdate(Action updateAction)
        {
            if (updateAction == null)
                throw new ArgumentNullException(nameof(updateAction));

            if (_isDisposed)
                throw new ObjectDisposedException(nameof(UIUpdateThrottler));

            lock (_lockObject)
            {
                _pendingAction = updateAction;
                
                // 重置定时器
                _timer.Change(_throttleIntervalMs, Timeout.Infinite);
            }
        }

        /// <summary>
        /// 立即执行待处理的更新操作
        /// </summary>
        /// <exception cref="ObjectDisposedException">对象已释放时抛出</exception>
        public void FlushPendingUpdates()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(UIUpdateThrottler));

            lock (_lockObject)
            {
                if (_pendingAction != null)
                {
                    var action = _pendingAction;
                    _pendingAction = null;
                    
                    // 停止定时器
                    _timer.Change(Timeout.Infinite, Timeout.Infinite);
                    
                    // 在UI线程执行更新
                    ExecuteOnUIThread(action);
                }
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 定时器回调方法
        /// </summary>
        /// <param name="state">状态对象</param>
        private void OnTimerElapsed(object state)
        {
            try
            {
                lock (_lockObject)
                {
                    if (_pendingAction != null && !_isDisposed)
                    {
                        var action = _pendingAction;
                        _pendingAction = null;
                        
                        // 在UI线程执行更新
                        ExecuteOnUIThread(action);
                    }
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"UIUpdateThrottler定时器回调异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 在UI线程执行操作
        /// </summary>
        /// <param name="action">要执行的操作</param>
        private void ExecuteOnUIThread(Action action)
        {
            try
            {
                if (_dispatcher.CheckAccess())
                {
                    // 已在UI线程，直接执行
                    action();
                }
                else
                {
                    // 使用低优先级调度到UI线程
                    _dispatcher.BeginInvoke(DispatcherPriority.Background, action);
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"UIUpdateThrottler执行UI更新异常: {ex.Message}");
            }
        }

        #endregion

        #region IDisposable实现

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源的具体实现
        /// </summary>
        /// <param name="disposing">是否正在释放托管资源</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed && disposing)
            {
                lock (_lockObject)
                {
                    _isDisposed = true;
                    _timer?.Dispose();
                    _pendingAction = null;
                }
                
                LogUtil.Info("UIUpdateThrottler已释放");
            }
        }

        #endregion
    }
}