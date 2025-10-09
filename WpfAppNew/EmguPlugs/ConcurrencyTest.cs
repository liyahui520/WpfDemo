using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Tools.Extend;

namespace WpfAppNew.EmguPlugs
{
    /// <summary>
    /// 并发性能测试类
    /// 用于测试IndustrialCameraManager的读写锁性能改进效果
    /// </summary>
    public class ConcurrencyTest
    {
        private readonly IndustrialCameraManager _cameraManager;
        private readonly int _testDurationSeconds;
        private volatile bool _isTestRunning;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="cameraManager">相机管理器实例</param>
        /// <param name="testDurationSeconds">测试持续时间（秒）</param>
        public ConcurrencyTest(IndustrialCameraManager cameraManager, int testDurationSeconds = 30)
        {
            _cameraManager = cameraManager ?? throw new ArgumentNullException(nameof(cameraManager));
            _testDurationSeconds = testDurationSeconds;
        }

        /// <summary>
        /// 运行并发性能测试
        /// </summary>
        /// <returns>测试结果</returns>
        public async Task<ConcurrencyTestResult> RunConcurrencyTestAsync()
        {
            LogUtil.Info("ConcurrencyTest: 开始并发性能测试");
            
            var result = new ConcurrencyTestResult();
            var stopwatch = Stopwatch.StartNew();
            _isTestRunning = true;

            // 启动多个并发任务
            var tasks = new[]
            {
                // 模拟频繁的截图操作（读操作）
                Task.Run(() => SimulateSnapshotOperations(result)),
                Task.Run(() => SimulateSnapshotOperations(result)),
                Task.Run(() => SimulateSnapshotOperations(result)),
                
                // 模拟录像操作（读操作）
                Task.Run(() => SimulateRecordingOperations(result)),
                
                // 模拟帧更新操作（写操作）
                Task.Run(() => SimulateFrameUpdates(result))
            };

            // 等待测试时间结束
            await Task.Delay(_testDurationSeconds * 1000);
            _isTestRunning = false;

            // 等待所有任务完成
            await Task.WhenAll(tasks);
            
            stopwatch.Stop();
            result.TotalTestTime = stopwatch.Elapsed;
            
            LogUtil.Info($"ConcurrencyTest: 测试完成 - 总时间:{result.TotalTestTime.TotalSeconds:F2}秒");
            LogUtil.Info($"ConcurrencyTest: 截图操作 - 成功:{result.SnapshotSuccessCount}, 失败:{result.SnapshotFailureCount}");
            LogUtil.Info($"ConcurrencyTest: 录像操作 - 成功:{result.RecordingSuccessCount}, 失败:{result.RecordingFailureCount}");
            LogUtil.Info($"ConcurrencyTest: 帧更新操作 - 成功:{result.FrameUpdateSuccessCount}, 失败:{result.FrameUpdateFailureCount}");
            
            return result;
        }

        /// <summary>
        /// 模拟截图操作
        /// </summary>
        /// <param name="result">测试结果</param>
        private async Task SimulateSnapshotOperations(ConcurrencyTestResult result)
        {
            while (_isTestRunning)
            {
                try
                {
                    var success = await _cameraManager.CapturePhotoAsync($"test_snapshot_{DateTime.Now:yyyyMMdd_HHmmss_fff}.jpg");
                    if (success)
                    {
                        result.IncrementSnapshotSuccess();
                    }
                    else
                    {
                        result.IncrementSnapshotFailure();
                    }
                }
                catch (Exception ex)
                {
                    LogUtil.Debug($"ConcurrencyTest: 截图操作异常 - {ex.Message}");
                    result.IncrementSnapshotFailure();
                }
                
                // 短暂延迟，模拟实际使用场景
                await Task.Delay(100);
            }
        }

        /// <summary>
        /// 模拟录像操作
        /// </summary>
        /// <param name="result">测试结果</param>
        private async Task SimulateRecordingOperations(ConcurrencyTestResult result)
        {
            while (_isTestRunning)
            {
                try
                {
                    // 模拟短时间录像
                    if (!_cameraManager.IsRecording)
                    {
                        var success = await _cameraManager.StartRecordingAsync("test_recording.mp4");
                        if (success)
                        {
                            result.IncrementRecordingSuccess();
                            
                            // 录像2秒后停止
                            await Task.Delay(2000);
                            await _cameraManager.StopRecordingAsync();
                        }
                        else
                        {
                            result.IncrementRecordingFailure();
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogUtil.Debug($"ConcurrencyTest: 录像操作异常 - {ex.Message}");
                    result.IncrementRecordingFailure();
                }
                
                // 等待一段时间再进行下一次录像
                await Task.Delay(3000);
            }
        }

        /// <summary>
        /// 模拟帧更新操作
        /// </summary>
        /// <param name="result">测试结果</param>
        private async Task SimulateFrameUpdates(ConcurrencyTestResult result)
        {
            while (_isTestRunning)
            {
                try
                {
                    // 模拟帧处理操作
                    // 这里我们只是检查相机管理器的状态
                    if (_cameraManager.IsCapturing)
                    {
                        result.IncrementFrameUpdateSuccess();
                    }
                    else
                    {
                        result.IncrementFrameUpdateFailure();
                    }
                }
                catch (Exception ex)
                {
                    LogUtil.Debug($"ConcurrencyTest: 帧更新操作异常 - {ex.Message}");
                    result.IncrementFrameUpdateFailure();
                }
                
                // 高频率更新，模拟实际的帧处理
                await Task.Delay(33); // 约30fps
            }
        }
    }

    /// <summary>
    /// 并发测试结果类
    /// </summary>
    public class ConcurrencyTestResult
    {
        // 使用字段来支持Interlocked操作
        private int _snapshotSuccessCount;
        private int _snapshotFailureCount;
        private int _recordingSuccessCount;
        private int _recordingFailureCount;
        private int _frameUpdateSuccessCount;
        private int _frameUpdateFailureCount;
        
        /// <summary>
        /// 总测试时间
        /// </summary>
        public TimeSpan TotalTestTime { get; set; }
        
        /// <summary>
        /// 截图成功次数
        /// </summary>
        public int SnapshotSuccessCount 
        { 
            get => _snapshotSuccessCount; 
            set => _snapshotSuccessCount = value; 
        }
        
        /// <summary>
        /// 截图失败次数
        /// </summary>
        public int SnapshotFailureCount 
        { 
            get => _snapshotFailureCount; 
            set => _snapshotFailureCount = value; 
        }
        
        /// <summary>
        /// 录像成功次数
        /// </summary>
        public int RecordingSuccessCount 
        { 
            get => _recordingSuccessCount; 
            set => _recordingSuccessCount = value; 
        }
        
        /// <summary>
        /// 录像失败次数
        /// </summary>
        public int RecordingFailureCount 
        { 
            get => _recordingFailureCount; 
            set => _recordingFailureCount = value; 
        }
        
        /// <summary>
        /// 帧更新成功次数
        /// </summary>
        public int FrameUpdateSuccessCount 
        { 
            get => _frameUpdateSuccessCount; 
            set => _frameUpdateSuccessCount = value; 
        }
        
        /// <summary>
        /// 帧更新失败次数
        /// </summary>
        public int FrameUpdateFailureCount 
        { 
            get => _frameUpdateFailureCount; 
            set => _frameUpdateFailureCount = value; 
        }
        
        /// <summary>
        /// 增加截图成功次数（线程安全）
        /// </summary>
        public void IncrementSnapshotSuccess() => Interlocked.Increment(ref _snapshotSuccessCount);
        
        /// <summary>
        /// 增加截图失败次数（线程安全）
        /// </summary>
        public void IncrementSnapshotFailure() => Interlocked.Increment(ref _snapshotFailureCount);
        
        /// <summary>
        /// 增加录像成功次数（线程安全）
        /// </summary>
        public void IncrementRecordingSuccess() => Interlocked.Increment(ref _recordingSuccessCount);
        
        /// <summary>
        /// 增加录像失败次数（线程安全）
        /// </summary>
        public void IncrementRecordingFailure() => Interlocked.Increment(ref _recordingFailureCount);
        
        /// <summary>
        /// 增加帧更新成功次数（线程安全）
        /// </summary>
        public void IncrementFrameUpdateSuccess() => Interlocked.Increment(ref _frameUpdateSuccessCount);
        
        /// <summary>
        /// 增加帧更新失败次数（线程安全）
        /// </summary>
        public void IncrementFrameUpdateFailure() => Interlocked.Increment(ref _frameUpdateFailureCount);
        
        /// <summary>
        /// 计算总成功率
        /// </summary>
        /// <returns>成功率百分比</returns>
        public double GetOverallSuccessRate()
        {
            var totalOperations = SnapshotSuccessCount + SnapshotFailureCount + 
                                RecordingSuccessCount + RecordingFailureCount + 
                                FrameUpdateSuccessCount + FrameUpdateFailureCount;
            
            if (totalOperations == 0) return 0;
            
            var totalSuccesses = SnapshotSuccessCount + RecordingSuccessCount + FrameUpdateSuccessCount;
            return (double)totalSuccesses / totalOperations * 100;
        }
        
        /// <summary>
        /// 计算操作吞吐量（每秒操作数）
        /// </summary>
        /// <returns>每秒操作数</returns>
        public double GetOperationThroughput()
        {
            var totalOperations = SnapshotSuccessCount + SnapshotFailureCount + 
                                RecordingSuccessCount + RecordingFailureCount + 
                                FrameUpdateSuccessCount + FrameUpdateFailureCount;
            
            if (TotalTestTime.TotalSeconds == 0) return 0;
            
            return totalOperations / TotalTestTime.TotalSeconds;
        }
    }
}