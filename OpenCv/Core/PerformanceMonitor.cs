using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace OpenCv.Core
{
    /// <summary>
    /// 性能监控器
    /// 监控摄像头系统的各项性能指标
    /// 提供性能统计、分析和优化建议
    /// </summary>
    public class PerformanceMonitor : INotifyPropertyChanged, IDisposable
    {
        #region 私有字段

        private readonly Timer _monitorTimer;
        private readonly object _lockObject = new object();
        private bool _disposed;
        private bool _isMonitoring;

        // 性能计数器
        private readonly PerformanceCounter _cpuCounter;
        private readonly PerformanceCounter _memoryCounter;
        private readonly Process _currentProcess;

        // 统计数据
        private readonly Queue<PerformanceSnapshot> _snapshots;
        private readonly int _maxSnapshots = 100;

        // 当前性能指标
        private double _currentFps;
        private double _averageFps;
        private double _cpuUsage;
        private double _memoryUsage;
        private long _processMemory;
        private double _frameProcessingTime;
        private int _droppedFrames;
        private int _totalFrames;

        #endregion

        #region 事件定义

        /// <summary>
        /// 性能数据更新事件
        /// </summary>
        public event EventHandler<PerformanceDataEventArgs> PerformanceDataUpdated;

        /// <summary>
        /// 性能警告事件
        /// </summary>
        public event EventHandler<PerformanceWarningEventArgs> PerformanceWarning;

        /// <summary>
        /// 性能报告事件
        /// </summary>
        public event EventHandler<PerformanceReportEventArgs> PerformanceReport;

        #endregion

        #region 公共属性

        /// <summary>
        /// 是否正在监控
        /// </summary>
        public bool IsMonitoring
        {
            get => _isMonitoring;
            private set
            {
                if (_isMonitoring != value)
                {
                    _isMonitoring = value;
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
            private set
            {
                if (Math.Abs(_currentFps - value) > 0.1)
                {
                    _currentFps = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 平均帧率
        /// </summary>
        public double AverageFps
        {
            get => _averageFps;
            private set
            {
                if (Math.Abs(_averageFps - value) > 0.1)
                {
                    _averageFps = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// CPU使用率 (%)
        /// </summary>
        public double CpuUsage
        {
            get => _cpuUsage;
            private set
            {
                if (Math.Abs(_cpuUsage - value) > 0.1)
                {
                    _cpuUsage = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 系统内存使用率 (%)
        /// </summary>
        public double MemoryUsage
        {
            get => _memoryUsage;
            private set
            {
                if (Math.Abs(_memoryUsage - value) > 0.1)
                {
                    _memoryUsage = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 进程内存使用量 (MB)
        /// </summary>
        public long ProcessMemory
        {
            get => _processMemory;
            private set
            {
                if (_processMemory != value)
                {
                    _processMemory = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 帧处理时间 (ms)
        /// </summary>
        public double FrameProcessingTime
        {
            get => _frameProcessingTime;
            private set
            {
                if (Math.Abs(_frameProcessingTime - value) > 0.1)
                {
                    _frameProcessingTime = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 丢帧数量
        /// </summary>
        public int DroppedFrames
        {
            get => _droppedFrames;
            private set
            {
                if (_droppedFrames != value)
                {
                    _droppedFrames = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 总帧数
        /// </summary>
        public int TotalFrames
        {
            get => _totalFrames;
            private set
            {
                if (_totalFrames != value)
                {
                    _totalFrames = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 丢帧率 (%)
        /// </summary>
        public double DropFrameRate => TotalFrames > 0 ? (double)DroppedFrames / TotalFrames * 100 : 0;

        /// <summary>
        /// 监控间隔 (ms)
        /// </summary>
        public int MonitorInterval { get; set; } = 1000;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化性能监控器
        /// </summary>
        public PerformanceMonitor()
        {
            _snapshots = new Queue<PerformanceSnapshot>();
            _currentProcess = Process.GetCurrentProcess();

            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"性能计数器初始化失败: {ex.Message}");
            }

            _monitorTimer = new Timer(MonitorCallback, null, Timeout.Infinite, Timeout.Infinite);
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 开始监控
        /// </summary>
        public void StartMonitoring()
        {
            if (IsMonitoring) return;

            lock (_lockObject)
            {
                IsMonitoring = true;
                _monitorTimer.Change(0, MonitorInterval);
            }
        }

        /// <summary>
        /// 停止监控
        /// </summary>
        public void StopMonitoring()
        {
            if (!IsMonitoring) return;

            lock (_lockObject)
            {
                IsMonitoring = false;
                _monitorTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }

        /// <summary>
        /// 记录帧处理时间
        /// </summary>
        /// <param name="processingTime">处理时间 (ms)</param>
        public void RecordFrameProcessingTime(double processingTime)
        {
            FrameProcessingTime = processingTime;
            TotalFrames++;

            // 检查是否丢帧（处理时间过长）
            var targetFrameTime = 1000.0 / 30.0; // 假设目标30fps
            if (processingTime > targetFrameTime * 1.5)
            {
                DroppedFrames++;
            }
        }

        /// <summary>
        /// 更新帧率
        /// </summary>
        /// <param name="fps">当前帧率</param>
        public void UpdateFps(double fps)
        {
            CurrentFps = fps;
            
            // 计算平均帧率
            lock (_lockObject)
            {
                if (_snapshots.Count > 0)
                {
                    var snapshotList = _snapshots.ToList();
                    var startIndex = Math.Max(0, snapshotList.Count - 10);
                    var recentSnapshots = snapshotList.Skip(startIndex).ToList();
                    AverageFps = recentSnapshots.Average(s => s.Fps);
                }
                else
                {
                    AverageFps = fps;
                }
            }
        }

        /// <summary>
        /// 更新性能统计信息
        /// </summary>
        /// <param name="stats">性能统计事件参数</param>
        public void UpdateStats(PerformanceStatsEventArgs stats)
        {
            if (stats == null) return;

            // 更新帧率
            UpdateFps(stats.Fps);
            
            // 更新帧计数
            TotalFrames += stats.FrameCount;
            
            // 记录性能快照
            RecordSnapshot();
            
            // 检查性能警告 - 使用最新的快照
            lock (_lockObject)
            {
                if (_snapshots.Count > 0)
                {
                    var latestSnapshot = _snapshots.Last();
                    CheckPerformanceWarnings(latestSnapshot);
                }
            }
        }

        /// <summary>
        /// 获取性能报告
        /// </summary>
        /// <returns>性能报告</returns>
        public PerformanceReport GetPerformanceReport()
        {
            lock (_lockObject)
            {
                var snapshots = _snapshots.ToList();
                
                return new PerformanceReport
                {
                    GeneratedAt = DateTime.Now,
                    MonitoringDuration = snapshots.Count > 0 ? 
                        snapshots.Last().Timestamp - snapshots.First().Timestamp : 
                        TimeSpan.Zero,
                    
                    // FPS统计
                    AverageFps = snapshots.Count > 0 ? snapshots.Average(s => s.Fps) : 0,
                    MinFps = snapshots.Count > 0 ? snapshots.Min(s => s.Fps) : 0,
                    MaxFps = snapshots.Count > 0 ? snapshots.Max(s => s.Fps) : 0,
                    
                    // CPU统计
                    AverageCpuUsage = snapshots.Count > 0 ? snapshots.Average(s => s.CpuUsage) : 0,
                    MaxCpuUsage = snapshots.Count > 0 ? snapshots.Max(s => s.CpuUsage) : 0,
                    
                    // 内存统计
                    AverageMemoryUsage = snapshots.Count > 0 ? snapshots.Average(s => s.MemoryUsage) : 0,
                    MaxMemoryUsage = snapshots.Count > 0 ? snapshots.Max(s => s.MemoryUsage) : 0,
                    AverageProcessMemory = snapshots.Count > 0 ? snapshots.Average(s => s.ProcessMemory) : 0,
                    MaxProcessMemory = snapshots.Count > 0 ? snapshots.Max(s => s.ProcessMemory) : 0,
                    
                    // 帧处理统计
                    AverageFrameProcessingTime = snapshots.Count > 0 ? snapshots.Average(s => s.FrameProcessingTime) : 0,
                    MaxFrameProcessingTime = snapshots.Count > 0 ? snapshots.Max(s => s.FrameProcessingTime) : 0,
                    
                    // 丢帧统计
                    TotalFrames = TotalFrames,
                    DroppedFrames = DroppedFrames,
                    DropFrameRate = DropFrameRate,
                    
                    // 性能建议
                    Recommendations = GenerateRecommendations(snapshots)
                };
            }
        }

        /// <summary>
        /// 重置统计数据
        /// </summary>
        public void ResetStatistics()
        {
            lock (_lockObject)
            {
                _snapshots.Clear();
                TotalFrames = 0;
                DroppedFrames = 0;
                CurrentFps = 0;
                AverageFps = 0;
                FrameProcessingTime = 0;
            }
        }

        /// <summary>
        /// 获取历史快照
        /// </summary>
        /// <param name="count">获取数量</param>
        /// <returns>快照列表</returns>
        public List<PerformanceSnapshot> GetHistorySnapshots(int count = 50)
        {
            lock (_lockObject)
            {
                var snapshotList = _snapshots.ToList();
                var startIndex = Math.Max(0, snapshotList.Count - count);
                return snapshotList.Skip(startIndex).ToList();
            }
        }

        /// <summary>
        /// 检查性能状态
        /// </summary>
        /// <returns>性能状态</returns>
        public PerformanceStatus CheckPerformanceStatus()
        {
            var status = PerformanceStatus.Good;

            // 检查帧率
            if (CurrentFps < 15)
                status = PerformanceStatus.Poor;
            else if (CurrentFps < 25)
                status = PerformanceStatus.Fair;

            // 检查CPU使用率
            if (CpuUsage > 80)
                status = PerformanceStatus.Poor;
            else if (CpuUsage > 60)
                status = (PerformanceStatus)Math.Max((int)status, (int)PerformanceStatus.Fair);

            // 检查内存使用率
            if (MemoryUsage > 90)
                status = PerformanceStatus.Poor;
            else if (MemoryUsage > 75 && (int)status < (int)PerformanceStatus.Fair)
                status = PerformanceStatus.Fair;

            // 检查丢帧率
            if (DropFrameRate > 10)
                status = PerformanceStatus.Poor;
            else if (DropFrameRate > 5 && (int)status < (int)PerformanceStatus.Fair)
                status = PerformanceStatus.Fair;

            return status;
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 监控回调
        /// </summary>
        /// <param name="state">状态对象</param>
        private void MonitorCallback(object state)
        {
            if (!IsMonitoring) return;

            try
            {
                var snapshot = CreateSnapshot();
                
                lock (_lockObject)
                {
                    _snapshots.Enqueue(snapshot);
                    
                    // 保持快照数量在限制内
                    while (_snapshots.Count > _maxSnapshots)
                    {
                        _snapshots.Dequeue();
                    }
                }

                // 更新当前性能指标
                UpdateCurrentMetrics(snapshot);

                // 触发性能数据更新事件
                PerformanceDataUpdated?.Invoke(this, new PerformanceDataEventArgs(snapshot));

                // 检查性能警告
                CheckPerformanceWarnings(snapshot);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"性能监控错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建性能快照
        /// </summary>
        /// <returns>性能快照</returns>
        private PerformanceSnapshot CreateSnapshot()
        {
            var snapshot = new PerformanceSnapshot
            {
                Timestamp = DateTime.Now,
                Fps = CurrentFps,
                FrameProcessingTime = FrameProcessingTime
            };

            try
            {
                // 获取CPU使用率
                if (_cpuCounter != null)
                {
                    snapshot.CpuUsage = _cpuCounter.NextValue();
                }

                // 获取内存使用率
                if (_memoryCounter != null)
                {
                    var availableMemory = _memoryCounter.NextValue();
                    var totalMemory = GetTotalPhysicalMemory();
                    snapshot.MemoryUsage = totalMemory > 0 ? 
                        (totalMemory - availableMemory) / totalMemory * 100 : 0;
                }

                // 获取进程内存使用量
                _currentProcess.Refresh();
                snapshot.ProcessMemory = _currentProcess.WorkingSet64 / 1024 / 1024; // MB
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取性能数据失败: {ex.Message}");
            }

            return snapshot;
        }

        /// <summary>
        /// 更新当前性能指标
        /// </summary>
        /// <param name="snapshot">性能快照</param>
        private void UpdateCurrentMetrics(PerformanceSnapshot snapshot)
        {
            CpuUsage = snapshot.CpuUsage;
            MemoryUsage = snapshot.MemoryUsage;
            ProcessMemory = snapshot.ProcessMemory;
        }

        /// <summary>
        /// 记录性能快照
        /// </summary>
        private void RecordSnapshot()
        {
            try
            {
                var snapshot = CreateSnapshot();
                
                lock (_lockObject)
                {
                    _snapshots.Enqueue(snapshot);
                    
                    // 保持快照数量在限制内
                    while (_snapshots.Count > _maxSnapshots)
                    {
                        _snapshots.Dequeue();
                    }
                }

                // 更新当前性能指标
                UpdateCurrentMetrics(snapshot);

                // 触发性能数据更新事件
                PerformanceDataUpdated?.Invoke(this, new PerformanceDataEventArgs(snapshot));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"记录性能快照错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查性能警告
        /// </summary>
        /// <param name="snapshot">性能快照</param>
        private void CheckPerformanceWarnings(PerformanceSnapshot snapshot)
        {
            var warnings = new List<string>();

            // 检查帧率警告
            if (snapshot.Fps < 15)
            {
                warnings.Add($"帧率过低: {snapshot.Fps:F1} FPS");
            }

            // 检查CPU使用率警告
            if (snapshot.CpuUsage > 80)
            {
                warnings.Add($"CPU使用率过高: {snapshot.CpuUsage:F1}%");
            }

            // 检查内存使用率警告
            if (snapshot.MemoryUsage > 90)
            {
                warnings.Add($"内存使用率过高: {snapshot.MemoryUsage:F1}%");
            }

            // 检查进程内存警告
            if (snapshot.ProcessMemory > 1000) // 1GB
            {
                warnings.Add($"进程内存使用过多: {snapshot.ProcessMemory} MB");
            }

            // 检查帧处理时间警告
            if (snapshot.FrameProcessingTime > 50) // 50ms
            {
                warnings.Add($"帧处理时间过长: {snapshot.FrameProcessingTime:F1} ms");
            }

            // 触发警告事件
            if (warnings.Count > 0)
            {
                PerformanceWarning?.Invoke(this, new PerformanceWarningEventArgs(warnings, snapshot));
            }
        }

        /// <summary>
        /// 生成性能建议
        /// </summary>
        /// <param name="snapshots">快照列表</param>
        /// <returns>建议列表</returns>
        private List<string> GenerateRecommendations(List<PerformanceSnapshot> snapshots)
        {
            var recommendations = new List<string>();

            if (snapshots.Count == 0) return recommendations;

            var avgFps = snapshots.Average(s => s.Fps);
            var avgCpu = snapshots.Average(s => s.CpuUsage);
            var avgMemory = snapshots.Average(s => s.MemoryUsage);
            var avgProcessingTime = snapshots.Average(s => s.FrameProcessingTime);

            // FPS建议
            if (avgFps < 20)
            {
                recommendations.Add("建议降低视频分辨率或帧率以提高性能");
            }

            // CPU建议
            if (avgCpu > 70)
            {
                recommendations.Add("CPU使用率较高，建议关闭其他应用程序或降低视频质量");
            }

            // 内存建议
            if (avgMemory > 80)
            {
                recommendations.Add("内存使用率较高，建议释放内存或增加物理内存");
            }

            // 处理时间建议
            if (avgProcessingTime > 30)
            {
                recommendations.Add("帧处理时间较长，建议优化图像处理算法或使用硬件加速");
            }

            // 丢帧建议
            if (DropFrameRate > 5)
            {
                recommendations.Add("丢帧率较高，建议检查系统性能或调整视频参数");
            }

            return recommendations;
        }

        /// <summary>
        /// 获取总物理内存 (MB)
        /// </summary>
        /// <returns>总内存大小</returns>
        private double GetTotalPhysicalMemory()
        {
            try
            {
                var totalMemory = GC.GetTotalMemory(false);
                return totalMemory / 1024.0 / 1024.0; // 转换为MB
            }
            catch
            {
                return 8192; // 默认8GB
            }
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
                    StopMonitoring();
                    _monitorTimer?.Dispose();
                    _cpuCounter?.Dispose();
                    _memoryCounter?.Dispose();
                    _currentProcess?.Dispose();
                }
                _disposed = true;
            }
        }

        ~PerformanceMonitor()
        {
            Dispose(false);
        }

        #endregion
    }

    #region 数据类和枚举

    /// <summary>
    /// 性能快照
    /// </summary>
    public class PerformanceSnapshot
    {
        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 帧率
        /// </summary>
        public double Fps { get; set; }

        /// <summary>
        /// CPU使用率
        /// </summary>
        public double CpuUsage { get; set; }

        /// <summary>
        /// 内存使用率
        /// </summary>
        public double MemoryUsage { get; set; }

        /// <summary>
        /// 进程内存使用量 (MB)
        /// </summary>
        public long ProcessMemory { get; set; }

        /// <summary>
        /// 帧处理时间 (ms)
        /// </summary>
        public double FrameProcessingTime { get; set; }
    }

    /// <summary>
    /// 性能报告
    /// </summary>
    public class PerformanceReport
    {
        /// <summary>
        /// 生成时间
        /// </summary>
        public DateTime GeneratedAt { get; set; }

        /// <summary>
        /// 监控时长
        /// </summary>
        public TimeSpan MonitoringDuration { get; set; }

        /// <summary>
        /// 平均帧率
        /// </summary>
        public double AverageFps { get; set; }

        /// <summary>
        /// 最小帧率
        /// </summary>
        public double MinFps { get; set; }

        /// <summary>
        /// 最大帧率
        /// </summary>
        public double MaxFps { get; set; }

        /// <summary>
        /// 平均CPU使用率
        /// </summary>
        public double AverageCpuUsage { get; set; }

        /// <summary>
        /// 最大CPU使用率
        /// </summary>
        public double MaxCpuUsage { get; set; }

        /// <summary>
        /// 平均内存使用率
        /// </summary>
        public double AverageMemoryUsage { get; set; }

        /// <summary>
        /// 最大内存使用率
        /// </summary>
        public double MaxMemoryUsage { get; set; }

        /// <summary>
        /// 平均进程内存使用量
        /// </summary>
        public double AverageProcessMemory { get; set; }

        /// <summary>
        /// 最大进程内存使用量
        /// </summary>
        public double MaxProcessMemory { get; set; }

        /// <summary>
        /// 平均帧处理时间
        /// </summary>
        public double AverageFrameProcessingTime { get; set; }

        /// <summary>
        /// 最大帧处理时间
        /// </summary>
        public double MaxFrameProcessingTime { get; set; }

        /// <summary>
        /// 总帧数
        /// </summary>
        public int TotalFrames { get; set; }

        /// <summary>
        /// 丢帧数
        /// </summary>
        public int DroppedFrames { get; set; }

        /// <summary>
        /// 丢帧率
        /// </summary>
        public double DropFrameRate { get; set; }

        /// <summary>
        /// 性能建议
        /// </summary>
        public List<string> Recommendations { get; set; } = new List<string>();
    }

    /// <summary>
    /// 性能状态枚举
    /// </summary>
    public enum PerformanceStatus
    {
        /// <summary>
        /// 良好
        /// </summary>
        Good,

        /// <summary>
        /// 一般
        /// </summary>
        Fair,

        /// <summary>
        /// 较差
        /// </summary>
        Poor
    }

    #endregion

    #region 事件参数类

    /// <summary>
    /// 性能数据事件参数
    /// </summary>
    public class PerformanceDataEventArgs : EventArgs
    {
        /// <summary>
        /// 性能快照
        /// </summary>
        public PerformanceSnapshot Snapshot { get; }

        public PerformanceDataEventArgs(PerformanceSnapshot snapshot)
        {
            Snapshot = snapshot;
        }
    }

    /// <summary>
    /// 性能警告事件参数
    /// </summary>
    public class PerformanceWarningEventArgs : EventArgs
    {
        /// <summary>
        /// 警告消息列表
        /// </summary>
        public List<string> Warnings { get; }

        /// <summary>
        /// 相关的性能快照
        /// </summary>
        public PerformanceSnapshot Snapshot { get; }

        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; }

        public PerformanceWarningEventArgs(List<string> warnings, PerformanceSnapshot snapshot)
        {
            Warnings = warnings ?? new List<string>();
            Snapshot = snapshot;
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// 性能报告事件参数
    /// </summary>
    public class PerformanceReportEventArgs : EventArgs
    {
        /// <summary>
        /// 性能报告
        /// </summary>
        public PerformanceReport Report { get; }

        public PerformanceReportEventArgs(PerformanceReport report)
        {
            Report = report;
        }
    }

    #endregion
}