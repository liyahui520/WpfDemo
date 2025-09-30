using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Tools.Extend;
using WpfAppNew.OpenCv.Core;
using WpfAppNew.OpenCV.Core;

namespace WpfAppNew.OpenCv.Core
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

        /// <summary>
        /// 硬件加速状态
        /// </summary>
        public AccelerationType AccelerationType { get; private set; } = AccelerationType.None;

        /// <summary>
        /// 硬件加速是否启用
        /// </summary>
        public bool IsHardwareAccelerationEnabled { get; private set; }

        /// <summary>
        /// OpenCL设备信息
        /// </summary>
        public string OpenCLDeviceInfo { get; private set; } = "未检测到";

        /// <summary>
        /// CUDA设备信息
        /// </summary>
        public string CudaDeviceInfo { get; private set; } = "未检测到";

        /// <summary>
        /// Intel TBB线程数
        /// </summary>
        public int TbbThreadCount { get; private set; }

        /// <summary>
        /// 硬件加速性能提升比例 (%)
        /// </summary>
        public double AccelerationPerformanceGain { get; private set; }

        /// <summary>
        /// 加速状态描述
        /// </summary>
        public string AccelerationStatusDescription { get; private set; } = "正在检测...";

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化性能监控器
        /// 包括硬件加速状态检测
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
                LogUtil.Info($"PerformanceMonitor: 性能计数器初始化失败: {ex.Message}");
            }

            // 初始化硬件加速状态监控
            InitializeAccelerationMonitoring();

            _monitorTimer = new Timer(MonitorCallback, null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// 初始化硬件加速状态监控
        /// </summary>
        private void InitializeAccelerationMonitoring()
        {
            try
            {
                // 获取加速管理器实例
                var accelerationManager = AccelerationManager.Instance;
                
                if (accelerationManager.AccelerationInfo != null)
                {
                    var info = accelerationManager.AccelerationInfo;
                    
                    // 更新加速状态
                    AccelerationType = info.EnabledAcceleration;
                    IsHardwareAccelerationEnabled = info.EnabledAcceleration != AccelerationType.None;
                    
                    // 更新设备信息
                    OpenCLDeviceInfo = info.IsOpenClSupported ? "支持OpenCL加速" : "不支持OpenCL";
                    CudaDeviceInfo = info.IsCudaSupported ? $"支持CUDA加速 (设备数: {info.CudaDeviceCount})" : "不支持CUDA";
                    TbbThreadCount = info.IsTbbSupported ? Environment.ProcessorCount : 0;
                    
                    // 设置状态描述
                    AccelerationStatusDescription = GetAccelerationStatusDescription(info.EnabledAcceleration);
                    
                    LogUtil.Info($"PerformanceMonitor: 硬件加速状态初始化完成 - {AccelerationStatusDescription}");
                }
                else
                {
                    AccelerationStatusDescription = "硬件加速检测失败";
                    LogUtil.Debug("PerformanceMonitor: 无法获取硬件加速信息");
                }
            }
            catch (Exception ex)
            {
                AccelerationStatusDescription = $"硬件加速检测异常: {ex.Message}";
                LogUtil.Error($"PerformanceMonitor: 硬件加速状态初始化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取加速状态描述
        /// </summary>
        /// <param name="accelerationType">加速类型</param>
        /// <returns>状态描述</returns>
        private string GetAccelerationStatusDescription(AccelerationType accelerationType)
        {
            switch (accelerationType)
            {
                case AccelerationType.OpenCL:
                    return "OpenCL硬件加速已启用";
                case AccelerationType.CUDA:
                    return "CUDA硬件加速已启用";
                case AccelerationType.TBB:
                    return "Intel TBB并行加速已启用";
                case AccelerationType.None:
                    return "使用CPU处理模式";
                default:
                    return "未知加速状态";
            }
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
        /// 获取硬件加速状态报告
        /// </summary>
        /// <returns>硬件加速状态报告</returns>
        public AccelerationStatusReport GetAccelerationStatusReport()
        {
            try
            {
                return new AccelerationStatusReport
                {
                    GeneratedAt = DateTime.Now,
                    AccelerationType = AccelerationType,
                    IsHardwareAccelerationEnabled = IsHardwareAccelerationEnabled,
                    OpenCLDeviceInfo = OpenCLDeviceInfo,
                    CudaDeviceInfo = CudaDeviceInfo,
                    TbbThreadCount = TbbThreadCount,
                    AccelerationPerformanceGain = AccelerationPerformanceGain,
                    AccelerationStatusDescription = AccelerationStatusDescription,
                    
                    // 详细状态信息
                    IsOpenCLAvailable = AccelerationManager.Instance.IsOpenCLAvailable,
                    IsCudaAvailable = AccelerationManager.Instance.IsCudaAvailable,
                    IsTbbAvailable = AccelerationManager.Instance.IsTbbAvailable,
                    
                    // 性能统计
                    CurrentFps = CurrentFps,
                    AverageFps = AverageFps,
                    FrameProcessingTime = FrameProcessingTime,
                    
                    // 建议
                    Recommendations = GenerateAccelerationRecommendations()
                };
            }
            catch (Exception ex)
            {
                LogUtil.Error($"PerformanceMonitor: 获取硬件加速状态报告时发生错误 - {ex.Message}");
                return new AccelerationStatusReport
                {
                    GeneratedAt = DateTime.Now,
                    AccelerationType = AccelerationType.None,
                    IsHardwareAccelerationEnabled = false,
                    AccelerationStatusDescription = $"获取状态失败: {ex.Message}",
                    Recommendations = new List<string> { "请检查硬件加速配置" }
                };
            }
        }

        /// <summary>
        /// 刷新硬件加速状态
        /// </summary>
        public void RefreshAccelerationStatus()
        {
            try
            {
                InitializeAccelerationMonitoring();
                LogUtil.Info("PerformanceMonitor: 硬件加速状态已刷新");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"PerformanceMonitor: 刷新硬件加速状态时发生错误 - {ex.Message}");
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
        /// 包括硬件加速性能监控
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

                // 更新硬件加速性能统计
                UpdateAccelerationPerformance(snapshot);

                // 触发性能数据更新事件
                PerformanceDataUpdated?.Invoke(this, new PerformanceDataEventArgs(snapshot));

                // 检查性能警告
                CheckPerformanceWarnings(snapshot);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"性能监控错误: {ex.Message}");
                LogUtil.Info($"PerformanceMonitor: 性能监控错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新硬件加速性能统计
        /// </summary>
        /// <param name="snapshot">当前性能快照</param>
        private void UpdateAccelerationPerformance(PerformanceSnapshot snapshot)
        {
            try
            {
                // 获取加速管理器实例
                var accelerationManager = AccelerationManager.Instance;
                
                if (accelerationManager.AccelerationInfo != null)
                {
                    var info = accelerationManager.AccelerationInfo;
                    
                    // 更新加速状态（可能在运行时发生变化）
                    if (AccelerationType != info.EnabledAcceleration)
                    {
                        AccelerationType = info.EnabledAcceleration;
                        IsHardwareAccelerationEnabled = info.EnabledAcceleration != AccelerationType.None;
                        AccelerationStatusDescription = GetAccelerationStatusDescription(info.EnabledAcceleration);
                        
                        LogUtil.Info($"PerformanceMonitor: 硬件加速状态变更为 {AccelerationStatusDescription}");
                    }
                    
                    // 计算性能提升比例
                    CalculateAccelerationPerformanceGain(snapshot);
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"PerformanceMonitor: 更新硬件加速性能统计时发生错误 - {ex.Message}");
            }
        }

        /// <summary>
        /// 计算硬件加速性能提升比例
        /// </summary>
        /// <param name="currentSnapshot">当前性能快照</param>
        private void CalculateAccelerationPerformanceGain(PerformanceSnapshot currentSnapshot)
        {
            try
            {
                if (!IsHardwareAccelerationEnabled || _snapshots.Count < 10)
                {
                    AccelerationPerformanceGain = 0;
                    return;
                }

                // 获取最近的性能数据
                var recentSnapshots = _snapshots.Skip(Math.Max(0, _snapshots.Count - 10)).ToList();
                var avgFps = recentSnapshots.Average(s => s.Fps);
                var avgProcessingTime = recentSnapshots.Average(s => s.FrameProcessingTime);

                // 估算CPU模式下的性能（基于经验值）
                var estimatedCpuFps = avgFps * 0.6; // 假设硬件加速能提升40%的性能
                var estimatedCpuProcessingTime = avgProcessingTime * 1.5; // 假设CPU处理时间增加50%

                // 计算性能提升比例
                var fpsGain = avgFps > estimatedCpuFps ? ((avgFps - estimatedCpuFps) / estimatedCpuFps) * 100 : 0;
                var timeGain = avgProcessingTime < estimatedCpuProcessingTime ? 
                    ((estimatedCpuProcessingTime - avgProcessingTime) / estimatedCpuProcessingTime) * 100 : 0;

                AccelerationPerformanceGain = Math.Max(fpsGain, timeGain);

                LogUtil.Debug($"PerformanceMonitor: 硬件加速性能提升 {AccelerationPerformanceGain:F1}%");
            }
            catch (Exception ex)
            {
                LogUtil.Debug($"PerformanceMonitor: 计算硬件加速性能提升失败: {ex.Message}");
                AccelerationPerformanceGain = 0;
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
                LogUtil.Info($"PerformanceMonitor: 获取性能数据失败: {ex.Message}");
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
                LogUtil.Info($"PerformanceMonitor: 记录性能快照错误: {ex.Message}");
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
        /// 生成硬件加速建议
        /// </summary>
        /// <returns>加速建议列表</returns>
        private List<string> GenerateAccelerationRecommendations()
        {
            var recommendations = new List<string>();

            try
            {
                // 检查硬件加速可用性
                bool isOpenCLAvailable = AccelerationManager.Instance.IsOpenCLAvailable;
                bool isCudaAvailable = AccelerationManager.Instance.IsCudaAvailable;
                bool isTbbAvailable = AccelerationManager.Instance.IsTbbAvailable;

                // 当前加速状态
                bool isAccelerationEnabled = IsHardwareAccelerationEnabled;
                var currentAcceleration = AccelerationType;

                // 性能分析
                bool hasPerformanceIssues = CurrentFps < 25 || FrameProcessingTime > 30;

                if (!isAccelerationEnabled && hasPerformanceIssues)
                {
                    recommendations.Add("检测到性能问题，建议启用硬件加速以提升处理速度");
                }

                // OpenCL建议
                if (isOpenCLAvailable && currentAcceleration != AccelerationType.OpenCL)
                {
                    if (hasPerformanceIssues)
                    {
                        recommendations.Add("OpenCL可用，建议启用OpenCL加速以提升图像处理性能");
                    }
                    else
                    {
                        recommendations.Add("OpenCL可用，可考虑启用以获得更好的性能表现");
                    }
                }

                // CUDA建议
                if (isCudaAvailable && currentAcceleration != AccelerationType.CUDA)
                {
                    if (hasPerformanceIssues)
                    {
                        recommendations.Add("CUDA可用，建议启用CUDA加速以获得最佳性能");
                    }
                    else
                    {
                        recommendations.Add("CUDA可用，可启用以获得更强的并行计算能力");
                    }
                }

                // TBB建议
                if (isTbbAvailable && currentAcceleration == AccelerationType.None)
                {
                    recommendations.Add("TBB可用，建议启用多线程加速以提升CPU处理效率");
                }

                // 性能优化建议
                if (isAccelerationEnabled)
                {
                    if (AccelerationPerformanceGain < 1.2)
                    {
                        recommendations.Add("当前硬件加速效果不明显，可尝试其他加速方式或优化算法");
                    }
                    else if (AccelerationPerformanceGain > 2.0)
                    {
                        recommendations.Add($"硬件加速效果良好（提升{AccelerationPerformanceGain:F1}倍），建议保持当前配置");
                    }
                }

                // 设备特定建议
                if (currentAcceleration == AccelerationType.OpenCL && !string.IsNullOrEmpty(OpenCLDeviceInfo))
                {
                    if (OpenCLDeviceInfo.Contains("Intel"))
                    {
                        recommendations.Add("使用Intel OpenCL，适合基础图像处理任务");
                    }
                    else if (OpenCLDeviceInfo.Contains("NVIDIA") || OpenCLDeviceInfo.Contains("AMD"))
                    {
                        recommendations.Add("使用独立显卡OpenCL，适合复杂图像处理和计算密集型任务");
                    }
                }

                // 无硬件加速可用的建议
                if (!isOpenCLAvailable && !isCudaAvailable && !isTbbAvailable)
                {
                    recommendations.Add("未检测到可用的硬件加速，建议更新显卡驱动或安装支持的运行时");
                }

                // 性能监控建议
                if (recommendations.Count == 0)
                {
                    recommendations.Add("硬件加速配置良好，建议定期监控性能表现");
                }
            }
            catch (Exception ex)
            {
                recommendations.Add($"生成加速建议时发生错误: {ex.Message}");
                LogUtil.Error($"PerformanceMonitor: 生成硬件加速建议时发生错误 - {ex.Message}");
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
    /// 硬件加速状态报告
    /// </summary>
    public class AccelerationStatusReport
    {
        /// <summary>
        /// 生成时间
        /// </summary>
        public DateTime GeneratedAt { get; set; }

        /// <summary>
        /// 加速类型
        /// </summary>
        public AccelerationType AccelerationType { get; set; }

        /// <summary>
        /// 是否启用硬件加速
        /// </summary>
        public bool IsHardwareAccelerationEnabled { get; set; }

        /// <summary>
        /// OpenCL设备信息
        /// </summary>
        public string OpenCLDeviceInfo { get; set; }

        /// <summary>
        /// CUDA设备信息
        /// </summary>
        public string CudaDeviceInfo { get; set; }

        /// <summary>
        /// TBB线程数
        /// </summary>
        public int TbbThreadCount { get; set; }

        /// <summary>
        /// 加速性能提升比例
        /// </summary>
        public double AccelerationPerformanceGain { get; set; }

        /// <summary>
        /// 加速状态描述
        /// </summary>
        public string AccelerationStatusDescription { get; set; }

        /// <summary>
        /// OpenCL是否可用
        /// </summary>
        public bool IsOpenCLAvailable { get; set; }

        /// <summary>
        /// CUDA是否可用
        /// </summary>
        public bool IsCudaAvailable { get; set; }

        /// <summary>
        /// TBB是否可用
        /// </summary>
        public bool IsTbbAvailable { get; set; }

        /// <summary>
        /// 当前帧率
        /// </summary>
        public double CurrentFps { get; set; }

        /// <summary>
        /// 平均帧率
        /// </summary>
        public double AverageFps { get; set; }

        /// <summary>
        /// 帧处理时间
        /// </summary>
        public double FrameProcessingTime { get; set; }

        /// <summary>
        /// 加速建议
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