using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OpenCv.Core
{
    /// <summary>
    /// 全局错误处理和异常恢复管理器
    /// 提供统一的错误处理、日志记录、异常恢复和系统监控功能
    /// 支持自动重试、故障转移和系统健康检查
    /// </summary>
    public class ErrorHandler : INotifyPropertyChanged, IDisposable
    {
        #region 私有字段

        private readonly object _lockObject = new object();
        private readonly List<ErrorRecord> _errorHistory = new List<ErrorRecord>();
        private readonly Dictionary<Type, int> _errorCounts = new Dictionary<Type, int>();
        private readonly Timer _healthCheckTimer;
        private readonly string _logFilePath;
        private bool _disposed;
        private int _totalErrors;
        private int _criticalErrors;
        private DateTime _lastErrorTime;
        private bool _isSystemHealthy = true;

        #endregion

        #region 事件定义

        /// <summary>
        /// 错误发生事件
        /// </summary>
        public event EventHandler<ErrorOccurredEventArgs> ErrorOccurred;

        /// <summary>
        /// 系统健康状态变更事件
        /// </summary>
        public event EventHandler<SystemHealthChangedEventArgs> SystemHealthChanged;

        /// <summary>
        /// 错误恢复事件
        /// </summary>
        public event EventHandler<ErrorRecoveryEventArgs> ErrorRecovery;

        /// <summary>
        /// 属性变更事件
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region 公共属性

        /// <summary>
        /// 总错误数
        /// </summary>
        public int TotalErrors
        {
            get => _totalErrors;
            private set
            {
                _totalErrors = value;
                OnPropertyChanged(nameof(TotalErrors));
            }
        }

        /// <summary>
        /// 严重错误数
        /// </summary>
        public int CriticalErrors
        {
            get => _criticalErrors;
            private set
            {
                _criticalErrors = value;
                OnPropertyChanged(nameof(CriticalErrors));
            }
        }

        /// <summary>
        /// 最后错误时间
        /// </summary>
        public DateTime LastErrorTime
        {
            get => _lastErrorTime;
            private set
            {
                _lastErrorTime = value;
                OnPropertyChanged(nameof(LastErrorTime));
            }
        }

        /// <summary>
        /// 系统是否健康
        /// </summary>
        public bool IsSystemHealthy
        {
            get => _isSystemHealthy;
            private set
            {
                if (_isSystemHealthy != value)
                {
                    _isSystemHealthy = value;
                    OnPropertyChanged(nameof(IsSystemHealthy));
                    SystemHealthChanged?.Invoke(this, new SystemHealthChangedEventArgs(value));
                }
            }
        }

        /// <summary>
        /// 错误历史记录
        /// </summary>
        public IReadOnlyList<ErrorRecord> ErrorHistory
        {
            get
            {
                lock (_lockObject)
                {
                    return _errorHistory.ToList();
                }
            }
        }

        /// <summary>
        /// 错误统计
        /// </summary>
        public IReadOnlyDictionary<Type, int> ErrorCounts
        {
            get
            {
                lock (_lockObject)
                {
                    return new Dictionary<Type, int>(_errorCounts);
                }
            }
        }

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化错误处理器
        /// </summary>
        public ErrorHandler()
        {
            // 创建日志文件路径
            var logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "OpenCV_Logs");
            Directory.CreateDirectory(logDirectory);
            _logFilePath = Path.Combine(logDirectory, $"ErrorLog_{DateTime.Now:yyyyMMdd}.txt");

            // 启动健康检查定时器
            _healthCheckTimer = new Timer(PerformHealthCheck, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

            WriteLog("错误处理器已初始化");
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 处理异常
        /// </summary>
        /// <param name="exception">异常对象</param>
        /// <param name="context">错误上下文</param>
        /// <param name="severity">错误严重程度</param>
        /// <param name="autoRecover">是否自动恢复</param>
        /// <returns>是否成功处理</returns>
        public bool HandleError(Exception exception, string context = "", ErrorSeverity severity = ErrorSeverity.Medium, bool autoRecover = true)
        {
            if (exception == null) return false;

            try
            {
                lock (_lockObject)
                {
                    // 创建错误记录
                    var errorRecord = new ErrorRecord
                    {
                        Exception = exception,
                        Context = context,
                        Severity = severity,
                        Timestamp = DateTime.Now,
                        ThreadId = Thread.CurrentThread.ManagedThreadId,
                        ProcessId = Process.GetCurrentProcess().Id
                    };

                    // 添加到历史记录
                    _errorHistory.Add(errorRecord);
                    
                    // 限制历史记录数量
                    if (_errorHistory.Count > 1000)
                    {
                        _errorHistory.RemoveAt(0);
                    }

                    // 更新统计
                    var exceptionType = exception.GetType();
                    if (_errorCounts.ContainsKey(exceptionType))
                    {
                        _errorCounts[exceptionType]++;
                    }
                    else
                    {
                        _errorCounts[exceptionType] = 1;
                    }

                    TotalErrors++;
                    LastErrorTime = DateTime.Now;

                    if (severity == ErrorSeverity.Critical)
                    {
                        CriticalErrors++;
                    }

                    // 写入日志
                    WriteErrorLog(errorRecord);

                    // 触发错误事件
                    ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(exception, context));

                    // 自动恢复
                    if (autoRecover)
                    {
                        return AttemptRecovery(errorRecord);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                // 防止递归错误
                WriteLog($"错误处理器自身发生异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 尝试恢复操作
        /// </summary>
        /// <param name="operation">操作委托</param>
        /// <param name="maxRetries">最大重试次数</param>
        /// <param name="retryDelay">重试延迟</param>
        /// <param name="context">操作上下文</param>
        /// <returns>是否成功</returns>
        public async Task<bool> TryRecoverAsync(Func<Task<bool>> operation, int maxRetries = 3, TimeSpan? retryDelay = null, string context = "")
        {
            var delay = retryDelay ?? TimeSpan.FromSeconds(1);
            
            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var result = await operation();
                    if (result)
                    {
                        if (attempt > 0)
                        {
                            WriteLog($"操作恢复成功 - 上下文: {context}, 尝试次数: {attempt + 1}");
                            ErrorRecovery?.Invoke(this, new ErrorRecoveryEventArgs(context, attempt + 1, true));
                        }
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    HandleError(ex, $"恢复操作失败 - {context} (尝试 {attempt + 1}/{maxRetries + 1})", ErrorSeverity.Medium, false);
                    
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(delay);
                        delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 1.5); // 指数退避
                    }
                }
            }

            WriteLog($"操作恢复失败 - 上下文: {context}, 已达到最大重试次数");
            ErrorRecovery?.Invoke(this, new ErrorRecoveryEventArgs(context, maxRetries + 1, false));
            return false;
        }

        /// <summary>
        /// 清理错误历史
        /// </summary>
        /// <param name="olderThan">清理早于指定时间的记录</param>
        public void ClearErrorHistory(DateTime? olderThan = null)
        {
            lock (_lockObject)
            {
                var cutoffTime = olderThan ?? DateTime.Now.AddDays(-7);
                var removedCount = _errorHistory.RemoveAll(e => e.Timestamp < cutoffTime);
                
                WriteLog($"清理了 {removedCount} 条错误历史记录");
            }
        }

        /// <summary>
        /// 获取错误统计报告
        /// </summary>
        /// <returns>错误统计报告</returns>
        public ErrorStatistics GetErrorStatistics()
        {
            lock (_lockObject)
            {
                var now = DateTime.Now;
                var last24Hours = _errorHistory.Where(e => e.Timestamp > now.AddDays(-1)).ToList();
                var lastHour = _errorHistory.Where(e => e.Timestamp > now.AddHours(-1)).ToList();

                return new ErrorStatistics
                {
                    TotalErrors = TotalErrors,
                    CriticalErrors = CriticalErrors,
                    ErrorsLast24Hours = last24Hours.Count,
                    ErrorsLastHour = lastHour.Count,
                    MostCommonError = _errorCounts.OrderByDescending(kv => kv.Value).FirstOrDefault().Key?.Name ?? "无",
                    AverageErrorsPerHour = last24Hours.Count / 24.0,
                    LastErrorTime = LastErrorTime,
                    IsSystemHealthy = IsSystemHealthy
                };
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 尝试自动恢复
        /// </summary>
        /// <param name="errorRecord">错误记录</param>
        /// <returns>是否恢复成功</returns>
        private bool AttemptRecovery(ErrorRecord errorRecord)
        {
            try
            {
                var recoveryStrategy = GetRecoveryStrategy(errorRecord.Exception);
                if (recoveryStrategy != null)
                {
                    var success = recoveryStrategy.Invoke();
                    if (success)
                    {
                        WriteLog($"自动恢复成功 - 错误类型: {errorRecord.Exception.GetType().Name}");
                        ErrorRecovery?.Invoke(this, new ErrorRecoveryEventArgs(errorRecord.Context, 1, true));
                        return true;
                    }
                }

                WriteLog($"自动恢复失败 - 错误类型: {errorRecord.Exception.GetType().Name}");
                return false;
            }
            catch (Exception ex)
            {
                WriteLog($"恢复策略执行失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取恢复策略
        /// </summary>
        /// <param name="exception">异常对象</param>
        /// <returns>恢复策略</returns>
        private Func<bool> GetRecoveryStrategy(Exception exception)
        {
            switch (exception)
            {
                case UnauthorizedAccessException _:
                    return () => {
                        // 尝试重新获取权限或使用备用路径
                        WriteLog("尝试权限恢复策略");
                        return false; // 需要具体实现
                    };

                case FileNotFoundException _:
                case DirectoryNotFoundException _:
                    return () => {
                        // 尝试创建缺失的文件或目录
                        WriteLog("尝试文件系统恢复策略");
                        return false; // 需要具体实现
                    };

                case OutOfMemoryException _:
                    return () => {
                        // 强制垃圾回收
                        WriteLog("尝试内存恢复策略");
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        GC.Collect();
                        return true;
                    };

                case InvalidOperationException invalidOpEx:
                    if (invalidOpEx.Message.Contains("摄像头"))
                    {
                        return () => {
                            // 摄像头相关错误的恢复策略
                            WriteLog("尝试摄像头恢复策略");
                            return false; // 需要具体实现
                        };
                    }
                    return null;

                default:
                    return null;
            }
        }

        /// <summary>
        /// 执行健康检查
        /// </summary>
        /// <param name="state">状态对象</param>
        private void PerformHealthCheck(object state)
        {
            try
            {
                var now = DateTime.Now;
                var recentErrors = _errorHistory.Where(e => e.Timestamp > now.AddMinutes(-5)).ToList();
                var criticalRecentErrors = recentErrors.Where(e => e.Severity == ErrorSeverity.Critical).ToList();

                // 健康状态评估
                var isHealthy = criticalRecentErrors.Count == 0 && recentErrors.Count < 10;
                
                if (IsSystemHealthy != isHealthy)
                {
                    IsSystemHealthy = isHealthy;
                    WriteLog($"系统健康状态变更: {(isHealthy ? "健康" : "不健康")}");
                }

                // 清理旧的错误记录
                if (now.Hour == 0 && now.Minute < 1) // 每天午夜清理
                {
                    ClearErrorHistory(now.AddDays(-7));
                }
            }
            catch (Exception ex)
            {
                WriteLog($"健康检查失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 写入错误日志
        /// </summary>
        /// <param name="errorRecord">错误记录</param>
        private void WriteErrorLog(ErrorRecord errorRecord)
        {
            try
            {
                var logEntry = $"[{errorRecord.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] " +
                              $"[{errorRecord.Severity}] " +
                              $"[Thread:{errorRecord.ThreadId}] " +
                              $"[Process:{errorRecord.ProcessId}] " +
                              $"[Context:{errorRecord.Context}] " +
                              $"{errorRecord.Exception.GetType().Name}: {errorRecord.Exception.Message}" +
                              Environment.NewLine +
                              $"StackTrace: {errorRecord.Exception.StackTrace}" +
                              Environment.NewLine + Environment.NewLine;

                File.AppendAllText(_logFilePath, logEntry);
            }
            catch
            {
                // 忽略日志写入错误
            }
        }

        /// <summary>
        /// 写入普通日志
        /// </summary>
        /// <param name="message">日志消息</param>
        private void WriteLog(string message)
        {
            try
            {
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [INFO] {message}" + Environment.NewLine;
                File.AppendAllText(_logFilePath, logEntry);
            }
            catch
            {
                // 忽略日志写入错误
            }
        }

        /// <summary>
        /// 触发属性变更事件
        /// </summary>
        /// <param name="propertyName">属性名称</param>
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否正在释放</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _healthCheckTimer?.Dispose();
                WriteLog("错误处理器已释放");
                _disposed = true;
            }
        }

        #endregion
    }

    #region 数据类和枚举

    /// <summary>
    /// 错误记录
    /// </summary>
    public class ErrorRecord
    {
        /// <summary>
        /// 异常对象
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// 错误上下文
        /// </summary>
        public string Context { get; set; }

        /// <summary>
        /// 错误严重程度
        /// </summary>
        public ErrorSeverity Severity { get; set; }

        /// <summary>
        /// 发生时间
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 线程ID
        /// </summary>
        public int ThreadId { get; set; }

        /// <summary>
        /// 进程ID
        /// </summary>
        public int ProcessId { get; set; }
    }

    /// <summary>
    /// 错误严重程度
    /// </summary>
    public enum ErrorSeverity
    {
        /// <summary>
        /// 低级错误
        /// </summary>
        Low,

        /// <summary>
        /// 中级错误
        /// </summary>
        Medium,

        /// <summary>
        /// 高级错误
        /// </summary>
        High,

        /// <summary>
        /// 严重错误
        /// </summary>
        Critical
    }

    /// <summary>
    /// 错误统计信息
    /// </summary>
    public class ErrorStatistics
    {
        /// <summary>
        /// 总错误数
        /// </summary>
        public int TotalErrors { get; set; }

        /// <summary>
        /// 严重错误数
        /// </summary>
        public int CriticalErrors { get; set; }

        /// <summary>
        /// 最近24小时错误数
        /// </summary>
        public int ErrorsLast24Hours { get; set; }

        /// <summary>
        /// 最近1小时错误数
        /// </summary>
        public int ErrorsLastHour { get; set; }

        /// <summary>
        /// 最常见错误类型
        /// </summary>
        public string MostCommonError { get; set; }

        /// <summary>
        /// 平均每小时错误数
        /// </summary>
        public double AverageErrorsPerHour { get; set; }

        /// <summary>
        /// 最后错误时间
        /// </summary>
        public DateTime LastErrorTime { get; set; }

        /// <summary>
        /// 系统是否健康
        /// </summary>
        public bool IsSystemHealthy { get; set; }
    }

    /// <summary>
    /// 系统健康状态变更事件参数
    /// </summary>
    public class SystemHealthChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 是否健康
        /// </summary>
        public bool IsHealthy { get; }

        /// <summary>
        /// 初始化事件参数
        /// </summary>
        /// <param name="isHealthy">是否健康</param>
        public SystemHealthChangedEventArgs(bool isHealthy)
        {
            IsHealthy = isHealthy;
        }
    }

    /// <summary>
    /// 错误恢复事件参数
    /// </summary>
    public class ErrorRecoveryEventArgs : EventArgs
    {
        /// <summary>
        /// 恢复上下文
        /// </summary>
        public string Context { get; }

        /// <summary>
        /// 尝试次数
        /// </summary>
        public int AttemptCount { get; }

        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccessful { get; }

        /// <summary>
        /// 初始化事件参数
        /// </summary>
        /// <param name="context">恢复上下文</param>
        /// <param name="attemptCount">尝试次数</param>
        /// <param name="isSuccessful">是否成功</param>
        public ErrorRecoveryEventArgs(string context, int attemptCount, bool isSuccessful)
        {
            Context = context;
            AttemptCount = attemptCount;
            IsSuccessful = isSuccessful;
        }
    }

    #endregion
}