using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Tools.Extend;

namespace WpfAppNew.Utils
{
    /// <summary>
    /// 性能监控器
    /// 用于监控UI性能、内存使用、帧率等关键指标
    /// </summary>
    public class PerformanceMonitor : IDisposable
    {
        #region 私有字段

        private readonly Timer _monitorTimer;
        private readonly PerformanceCounter _cpuCounter;
        private readonly PerformanceCounter _memoryCounter;
        private readonly Stopwatch _frameStopwatch;
        private readonly Queue<double> _frameTimeHistory;
        private readonly Queue<long> _memoryHistory;
        private readonly Queue<float> _cpuHistory;
        private readonly object _lockObject = new object();
        
        private long _frameCount;
        private long _lastFrameCount;
        private DateTime _lastUpdateTime;
        private bool _isDisposed;

        #endregion

        #region 公共属性

        /// <summary>
        /// 当前帧率
        /// </summary>
        public double CurrentFPS { get; private set; }

        /// <summary>
        /// 平均帧时间（毫秒）
        /// </summary>
        public double AverageFrameTime { get; private set; }

        /// <summary>
        /// 当前内存使用量（MB）
        /// </summary>
        public long CurrentMemoryUsage { get; private set; }

        /// <summary>
        /// 当前CPU使用率（%）
        /// </summary>
        public float CurrentCpuUsage { get; private set; }

        /// <summary>
        /// UI线程是否繁忙
        /// </summary>
        public bool IsUIThreadBusy { get; private set; }

        /// <summary>
        /// 性能统计更新事件
        /// </summary>
        public event EventHandler<PerformanceStatsEventArgs> StatsUpdated;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化性能监控器
        /// </summary>
        /// <param name="updateInterval">更新间隔（毫秒）</param>
        public PerformanceMonitor(int updateInterval = 1000)
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
                _frameStopwatch = new Stopwatch();
                _frameTimeHistory = new Queue<double>();
                _memoryHistory = new Queue<long>();
                _cpuHistory = new Queue<float>();
                _lastUpdateTime = DateTime.Now;

                // 启动监控定时器
                _monitorTimer = new Timer(UpdateStats, null, updateInterval, updateInterval);
                
                LogUtil.Info("PerformanceMonitor: 性能监控器初始化完成");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"PerformanceMonitor: 初始化失败 - {ex.Message}");
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 记录帧开始
        /// </summary>
        public void StartFrame()
        {
            if (_isDisposed) return;
            
            lock (_lockObject)
            {
                _frameStopwatch.Restart();
            }
        }

        /// <summary>
        /// 记录帧结束
        /// </summary>
        public void EndFrame()
        {
            if (_isDisposed) return;
            
            lock (_lockObject)
            {
                _frameStopwatch.Stop();
                var frameTime = _frameStopwatch.Elapsed.TotalMilliseconds;
                
                _frameTimeHistory.Enqueue(frameTime);
                if (_frameTimeHistory.Count > 60) // 保持最近60帧的历史
                {
                    _frameTimeHistory.Dequeue();
                }
                
                _frameCount++;
            }
        }

        /// <summary>
        /// 检查UI线程状态
        /// </summary>
        /// <returns>UI线程是否繁忙</returns>
        public bool CheckUIThreadStatus()
        {
            if (_isDisposed) return false;
            
            try
            {
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher == null) return false;

                bool isBusy = false;
                var checkCompleted = false;
                
                // 使用低优先级操作检查UI线程状态
                dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    isBusy = dispatcher.HasShutdownStarted || !dispatcher.CheckAccess();
                    checkCompleted = true;
                }));

                // 等待检查完成（最多100ms）
                var timeout = DateTime.Now.AddMilliseconds(100);
                while (!checkCompleted && DateTime.Now < timeout)
                {
                    Thread.Sleep(1);
                }

                IsUIThreadBusy = isBusy;
                return isBusy;
            }
            catch (Exception ex)
            {
                LogUtil.Error($"PerformanceMonitor: UI线程状态检查失败 - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取性能统计信息
        /// </summary>
        /// <returns>性能统计信息</returns>
        public PerformanceStats GetStats()
        {
            if (_isDisposed) return new PerformanceStats();
            
            lock (_lockObject)
            {
                return new PerformanceStats
                {
                    FPS = CurrentFPS,
                    AverageFrameTime = AverageFrameTime,
                    MemoryUsage = CurrentMemoryUsage,
                    CpuUsage = CurrentCpuUsage,
                    IsUIThreadBusy = IsUIThreadBusy,
                    FrameCount = _frameCount,
                    Timestamp = DateTime.Now
                };
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 更新性能统计
        /// </summary>
        /// <param name="state">状态对象</param>
        private void UpdateStats(object state)
        {
            if (_isDisposed) return;
            
            try
            {
                lock (_lockObject)
                {
                    var now = DateTime.Now;
                    var elapsed = (now - _lastUpdateTime).TotalSeconds;
                    
                    // 计算FPS
                    var framesDelta = _frameCount - _lastFrameCount;
                    CurrentFPS = elapsed > 0 ? framesDelta / elapsed : 0;
                    
                    // 计算平均帧时间
                    if (_frameTimeHistory.Count > 0)
                    {
                        AverageFrameTime = _frameTimeHistory.Average();
                    }
                    
                    // 获取内存使用量
                    var process = Process.GetCurrentProcess();
                    CurrentMemoryUsage = process.WorkingSet64 / (1024 * 1024); // 转换为MB
                    
                    // 获取CPU使用率
                    try
                    {
                        CurrentCpuUsage = _cpuCounter.NextValue();
                        _cpuHistory.Enqueue(CurrentCpuUsage);
                        if (_cpuHistory.Count > 10) // 保持最近10次的历史
                        {
                            _cpuHistory.Dequeue();
                        }
                    }
                    catch (Exception ex)
                    {
                        LogUtil.Warning($"PerformanceMonitor: CPU使用率获取失败 - {ex.Message}");
                    }
                    
                    // 更新内存历史
                    _memoryHistory.Enqueue(CurrentMemoryUsage);
                    if (_memoryHistory.Count > 60) // 保持最近60秒的历史
                    {
                        _memoryHistory.Dequeue();
                    }
                    
                    // 检查UI线程状态
                    CheckUIThreadStatus();
                    
                    _lastFrameCount = _frameCount;
                    _lastUpdateTime = now;
                    
                    // 触发统计更新事件
                    StatsUpdated?.Invoke(this, new PerformanceStatsEventArgs(GetStats()));
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"PerformanceMonitor: 性能统计更新失败 - {ex.Message}");
            }
        }

        #endregion

        #region IDisposable实现

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            
            _isDisposed = true;
            
            try
            {
                _monitorTimer?.Dispose();
                _cpuCounter?.Dispose();
                _memoryCounter?.Dispose();
                _frameStopwatch?.Stop();
                
                LogUtil.Info("PerformanceMonitor: 性能监控器已释放");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"PerformanceMonitor: 释放资源失败 - {ex.Message}");
            }
        }

        #endregion
    }

    /// <summary>
    /// 性能统计信息
    /// </summary>
    public class PerformanceStats
    {
        /// <summary>
        /// 帧率
        /// </summary>
        public double FPS { get; set; }

        /// <summary>
        /// 平均帧时间（毫秒）
        /// </summary>
        public double AverageFrameTime { get; set; }

        /// <summary>
        /// 内存使用量（MB）
        /// </summary>
        public long MemoryUsage { get; set; }

        /// <summary>
        /// CPU使用率（%）
        /// </summary>
        public float CpuUsage { get; set; }

        /// <summary>
        /// UI线程是否繁忙
        /// </summary>
        public bool IsUIThreadBusy { get; set; }

        /// <summary>
        /// 帧计数
        /// </summary>
        public long FrameCount { get; set; }

        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// 性能统计事件参数
    /// </summary>
    public class PerformanceStatsEventArgs : EventArgs
    {
        /// <summary>
        /// 性能统计信息
        /// </summary>
        public PerformanceStats Stats { get; }

        /// <summary>
        /// 初始化性能统计事件参数
        /// </summary>
        /// <param name="stats">性能统计信息</param>
        public PerformanceStatsEventArgs(PerformanceStats stats)
        {
            Stats = stats;
        }
    }
}