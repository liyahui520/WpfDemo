using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Tools.Extend;

namespace WpfAppNew.EmguPlugs
{
    /// <summary>
    /// 读写锁性能测试类
    /// 用于验证读写锁相比传统锁的性能改进
    /// </summary>
    public class LockPerformanceTest
    {
        /// <summary>
        /// 运行性能测试
        /// </summary>
        /// <param name="testDurationSeconds">测试持续时间（秒）</param>
        /// <returns>测试结果</returns>
        public static async Task<LockTestResult> RunPerformanceTest(int testDurationSeconds = 10)
        {
            LogUtil.Info($"LockPerformanceTest: 开始性能测试，持续时间 {testDurationSeconds} 秒");

            var result = new LockTestResult();
            var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(testDurationSeconds));
            var cancellationToken = cancellationTokenSource.Token;

            // 创建读写锁和传统锁
            var readerWriterLock = new ReaderWriterLockSlim();
            var traditionalLock = new object();

            // 共享数据
            int sharedData = 0;

            // 启动读写锁测试
            var rwLockTasks = new[]
            {
                // 多个读操作任务
                Task.Run(() => ReadOperationWithRWLock(readerWriterLock, ref sharedData, result, cancellationToken)),
                Task.Run(() => ReadOperationWithRWLock(readerWriterLock, ref sharedData, result, cancellationToken)),
                Task.Run(() => ReadOperationWithRWLock(readerWriterLock, ref sharedData, result, cancellationToken)),
                Task.Run(() => ReadOperationWithRWLock(readerWriterLock, ref sharedData, result, cancellationToken)),
                
                // 少量写操作任务
                Task.Run(() => WriteOperationWithRWLock(readerWriterLock, ref sharedData, result, cancellationToken))
            };

            // 启动传统锁测试
            var traditionalLockTasks = new[]
            {
                // 多个读操作任务
                Task.Run(() => ReadOperationWithTraditionalLock(traditionalLock, ref sharedData, result, cancellationToken)),
                Task.Run(() => ReadOperationWithTraditionalLock(traditionalLock, ref sharedData, result, cancellationToken)),
                Task.Run(() => ReadOperationWithTraditionalLock(traditionalLock, ref sharedData, result, cancellationToken)),
                Task.Run(() => ReadOperationWithTraditionalLock(traditionalLock, ref sharedData, result, cancellationToken)),
                
                // 少量写操作任务
                Task.Run(() => WriteOperationWithTraditionalLock(traditionalLock, ref sharedData, result, cancellationToken))
            };

            // 等待所有任务完成
            await Task.WhenAll(rwLockTasks);
            await Task.WhenAll(traditionalLockTasks);

            // 计算性能改进
            result.CalculatePerformanceImprovement();

            LogUtil.Info($"LockPerformanceTest: 测试完成");
            LogUtil.Info($"读写锁读操作: {result.RWLockReadOperations} 次");
            LogUtil.Info($"读写锁写操作: {result.RWLockWriteOperations} 次");
            LogUtil.Info($"传统锁读操作: {result.TraditionalLockReadOperations} 次");
            LogUtil.Info($"传统锁写操作: {result.TraditionalLockWriteOperations} 次");
            LogUtil.Info($"读操作性能改进: {result.ReadPerformanceImprovement:P2}");
            LogUtil.Info($"写操作性能改进: {result.WritePerformanceImprovement:P2}");

            return result;
        }

        /// <summary>
        /// 使用读写锁的读操作
        /// </summary>
        private static void ReadOperationWithRWLock(ReaderWriterLockSlim rwLock, ref int sharedData, LockTestResult result, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    rwLock.EnterReadLock();
                    try
                    {
                        // 模拟读操作
                        var value = sharedData;
                        Thread.SpinWait(100); // 模拟一些计算
                        result.IncrementRWLockReadOperations();
                    }
                    finally
                    {
                        rwLock.ExitReadLock();
                    }
                }
                catch (Exception ex)
                {
                    LogUtil.Error($"LockPerformanceTest: 读写锁读操作异常 - {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 使用读写锁的写操作
        /// </summary>
        private static void WriteOperationWithRWLock(ReaderWriterLockSlim rwLock, ref int sharedData, LockTestResult result, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    rwLock.EnterWriteLock();
                    try
                    {
                        // 模拟写操作
                        sharedData++;
                        Thread.SpinWait(500); // 模拟一些计算
                        result.IncrementRWLockWriteOperations();
                    }
                    finally
                    {
                        rwLock.ExitWriteLock();
                    }

                    // 写操作频率较低
                    Thread.Sleep(10);
                }
                catch (Exception ex)
                {
                    LogUtil.Error($"LockPerformanceTest: 读写锁写操作异常 - {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 使用传统锁的读操作
        /// </summary>
        private static void ReadOperationWithTraditionalLock(object lockObject, ref int sharedData, LockTestResult result, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    lock (lockObject)
                    {
                        // 模拟读操作
                        var value = sharedData;
                        Thread.SpinWait(100); // 模拟一些计算
                        result.IncrementTraditionalLockReadOperations();
                    }
                }
                catch (Exception ex)
                {
                    LogUtil.Error($"LockPerformanceTest: 传统锁读操作异常 - {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 使用传统锁的写操作
        /// </summary>
        private static void WriteOperationWithTraditionalLock(object lockObject, ref int sharedData, LockTestResult result, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    lock (lockObject)
                    {
                        // 模拟写操作
                        sharedData++;
                        Thread.SpinWait(500); // 模拟一些计算
                        result.IncrementTraditionalLockWriteOperations();
                    }

                    // 写操作频率较低
                    Thread.Sleep(10);
                }
                catch (Exception ex)
                {
                    LogUtil.Error($"LockPerformanceTest: 传统锁写操作异常 - {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// 锁性能测试结果
    /// </summary>
    public class LockTestResult
    {
        private long _rwLockReadOperations = 0;
        private long _rwLockWriteOperations = 0;
        private long _traditionalLockReadOperations = 0;
        private long _traditionalLockWriteOperations = 0;

        /// <summary>
        /// 读写锁读操作次数
        /// </summary>
        public long RWLockReadOperations => _rwLockReadOperations;

        /// <summary>
        /// 读写锁写操作次数
        /// </summary>
        public long RWLockWriteOperations => _rwLockWriteOperations;

        /// <summary>
        /// 传统锁读操作次数
        /// </summary>
        public long TraditionalLockReadOperations => _traditionalLockReadOperations;

        /// <summary>
        /// 传统锁写操作次数
        /// </summary>
        public long TraditionalLockWriteOperations => _traditionalLockWriteOperations;

        /// <summary>
        /// 读操作性能改进百分比
        /// </summary>
        public double ReadPerformanceImprovement { get; private set; }

        /// <summary>
        /// 写操作性能改进百分比
        /// </summary>
        public double WritePerformanceImprovement { get; private set; }

        /// <summary>
        /// 增加读写锁读操作计数
        /// </summary>
        public void IncrementRWLockReadOperations()
        {
            Interlocked.Increment(ref _rwLockReadOperations);
        }

        /// <summary>
        /// 增加读写锁写操作计数
        /// </summary>
        public void IncrementRWLockWriteOperations()
        {
            Interlocked.Increment(ref _rwLockWriteOperations);
        }

        /// <summary>
        /// 增加传统锁读操作计数
        /// </summary>
        public void IncrementTraditionalLockReadOperations()
        {
            Interlocked.Increment(ref _traditionalLockReadOperations);
        }

        /// <summary>
        /// 增加传统锁写操作计数
        /// </summary>
        public void IncrementTraditionalLockWriteOperations()
        {
            Interlocked.Increment(ref _traditionalLockWriteOperations);
        }

        /// <summary>
        /// 计算性能改进
        /// </summary>
        public void CalculatePerformanceImprovement()
        {
            // 计算读操作性能改进
            if (_traditionalLockReadOperations > 0)
            {
                ReadPerformanceImprovement = (double)(_rwLockReadOperations - _traditionalLockReadOperations) / _traditionalLockReadOperations;
            }

            // 计算写操作性能改进
            if (_traditionalLockWriteOperations > 0)
            {
                WritePerformanceImprovement = (double)(_rwLockWriteOperations - _traditionalLockWriteOperations) / _traditionalLockWriteOperations;
            }
        }
    }
}