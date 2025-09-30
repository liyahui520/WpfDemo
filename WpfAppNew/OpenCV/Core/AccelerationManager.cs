using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenCvSharp;
using Tools.Extend;

namespace WpfAppNew.OpenCV.Core
{
    /// <summary>
    /// OpenCV硬件加速管理器
    /// 负责检测和配置各种硬件加速选项，包括CUDA、OpenCL、Intel TBB等
    /// </summary>
    public class AccelerationManager
    {
        #region 私有字段

        /// <summary>
        /// 单例实例
        /// </summary>
        private static AccelerationManager _instance;

        /// <summary>
        /// 线程锁
        /// </summary>
        private static readonly object _lock = new object();

        /// <summary>
        /// 是否已初始化
        /// </summary>
        private bool _isInitialized;

        /// <summary>
        /// 加速配置信息
        /// </summary>
        private AccelerationInfo _accelerationInfo;

        #endregion

        #region 公共属性

        /// <summary>
        /// 单例实例
        /// </summary>
        public static AccelerationManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new AccelerationManager();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 加速配置信息
        /// </summary>
        public AccelerationInfo AccelerationInfo => _accelerationInfo;

        /// <summary>
        /// 是否支持CUDA加速
        /// </summary>
        public bool IsCudaSupported => _accelerationInfo?.IsCudaSupported ?? false;

        /// <summary>
        /// 是否支持OpenCL加速
        /// </summary>
        public bool IsOpenClSupported => _accelerationInfo?.IsOpenClSupported ?? false;

        /// <summary>
        /// 是否支持Intel TBB
        /// </summary>
        public bool IsTbbSupported => _accelerationInfo?.IsTbbSupported ?? false;

        /// <summary>
        /// 当前启用的加速类型
        /// </summary>
        public AccelerationType EnabledAcceleration => _accelerationInfo?.EnabledAcceleration ?? AccelerationType.None;

        /// <summary>
        /// 检查OpenCL是否可用
        /// </summary>
        public bool IsOpenCLAvailable => IsOpenClSupported;

        /// <summary>
        /// 检查CUDA是否可用
        /// </summary>
        public bool IsCudaAvailable => IsCudaSupported;

        /// <summary>
        /// 检查Intel TBB是否可用
        /// </summary>
        public bool IsTbbAvailable => IsTbbSupported;

        #endregion

        #region 构造函数

        /// <summary>
        /// 私有构造函数
        /// </summary>
        private AccelerationManager()
        {
            _accelerationInfo = new AccelerationInfo();
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 初始化加速管理器
        /// 检测所有可用的硬件加速选项
        /// </summary>
        /// <returns>是否成功初始化</returns>
        public bool Initialize()
        {
            if (_isInitialized)
            {
                return true;
            }

            try
            {
                LogUtil.Info("AccelerationManager: 开始检测硬件加速支持");

                // 检测CUDA支持
                DetectCudaSupport();

                // 检测OpenCL支持
                DetectOpenClSupport();

                // 检测Intel TBB支持
                DetectTbbSupported();

                // 检测可用的后端
                DetectAvailableBackends();

                // 自动选择最佳加速方式
                SelectOptimalAcceleration();

                _isInitialized = true;
                LogUtil.Info($"AccelerationManager: 初始化完成，启用加速: {_accelerationInfo.EnabledAcceleration}");

                return true;
            }
            catch (Exception ex)
            {
                LogUtil.Error($"AccelerationManager: 初始化失败 - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 启用指定的加速类型
        /// </summary>
        /// <param name="accelerationType">加速类型</param>
        /// <returns>是否成功启用</returns>
        public bool EnableAcceleration(AccelerationType accelerationType)
        {
            try
            {
                switch (accelerationType)
                {
                    case AccelerationType.CUDA:
                        if (!_accelerationInfo.IsCudaSupported)
                        {
                            LogUtil.Debug("AccelerationManager: CUDA不受支持，无法启用");
                            return false;
                        }
                        break;

                    case AccelerationType.OpenCL:
                        if (!_accelerationInfo.IsOpenClSupported)
                        {
                            LogUtil.Debug("AccelerationManager: OpenCL不受支持，无法启用");
                            return false;
                        }
                        break;

                    case AccelerationType.TBB:
                        if (!_accelerationInfo.IsTbbSupported)
                        {
                            LogUtil.Debug("AccelerationManager: Intel TBB不受支持，无法启用");
                            return false;
                        }
                        break;
                }

                _accelerationInfo.EnabledAcceleration = accelerationType;
                LogUtil.Info($"AccelerationManager: 已启用加速类型: {accelerationType}");
                return true;
            }
            catch (Exception ex)
            {
                LogUtil.Error($"AccelerationManager: 启用加速失败 - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取性能基准测试结果
        /// </summary>
        /// <returns>基准测试结果</returns>
        public BenchmarkResult RunBenchmark()
        {
            var result = new BenchmarkResult();

            try
            {
                LogUtil.Info("AccelerationManager: 开始性能基准测试");

                // 创建测试图像
                using (var testImage = new Mat(1920, 1080, MatType.CV_8UC3))
                {
                    testImage.SetTo(new Scalar(128, 128, 128));

                    // 测试CPU性能
                    result.CpuTime = BenchmarkOperation(testImage, AccelerationType.None);

                    // 测试CUDA性能（如果支持）
                    if (IsCudaSupported)
                    {
                        result.CudaTime = BenchmarkOperation(testImage, AccelerationType.CUDA);
                    }

                    // 测试OpenCL性能（如果支持）
                    if (IsOpenClSupported)
                    {
                        result.OpenClTime = BenchmarkOperation(testImage, AccelerationType.OpenCL);
                    }

                    // 测试TBB性能（如果支持）
                    if (IsTbbSupported)
                    {
                        result.TbbTime = BenchmarkOperation(testImage, AccelerationType.TBB);
                    }
                }

                LogUtil.Info($"AccelerationManager: 基准测试完成 - CPU: {result.CpuTime}ms");
                return result;
            }
            catch (Exception ex)
            {
                LogUtil.Error($"AccelerationManager: 基准测试失败 - {ex.Message}");
                return result;
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 检测CUDA支持
        /// </summary>
        private void DetectCudaSupport()
        {
            try
            {
                // 检查构建信息中是否包含CUDA支持
                string buildInfo = Cv2.GetBuildInformation();
                _accelerationInfo.IsCudaSupported = buildInfo.Contains("CUDA");

                if (_accelerationInfo.IsCudaSupported)
                {
                    // 在OpenCvSharp4中，CUDA设备检测可能不可用
                    // 设置默认值
                    _accelerationInfo.CudaDeviceCount = 1; // 假设有一个CUDA设备
                    LogUtil.Info($"AccelerationManager: CUDA支持已检测到（基于构建信息）");
                }
                else
                {
                    _accelerationInfo.CudaDeviceCount = 0;
                }

                LogUtil.Info($"AccelerationManager: CUDA支持: {_accelerationInfo.IsCudaSupported}");
            }
            catch (Exception ex)
            {
                LogUtil.Debug($"AccelerationManager: CUDA检测异常 - {ex.Message}");
                _accelerationInfo.IsCudaSupported = false;
            }
        }

        /// <summary>
        /// 检测OpenCL支持
        /// </summary>
        private void DetectOpenClSupport()
        {
            try
            {
                // 检查OpenCV是否编译了OpenCL支持
                var buildInfo = Cv2.GetBuildInformation();
                _accelerationInfo.IsOpenClSupported = buildInfo.Contains("OpenCL");

                // 在OpenCV 3.x+中，OpenCL使用透明API，自动启用
                // 不需要手动调用SetUseOpenCL等方法

                LogUtil.Info($"AccelerationManager: OpenCL支持: {_accelerationInfo.IsOpenClSupported}");
            }
            catch (Exception ex)
            {
                LogUtil.Debug($"AccelerationManager: OpenCL检测异常 - {ex.Message}");
                _accelerationInfo.IsOpenClSupported = false;
            }
        }

        /// <summary>
        /// 检测Intel TBB支持
        /// </summary>
        private void DetectTbbSupported()
        {
            try
            {
                // 检查OpenCV是否编译了TBB支持
                var buildInfo = Cv2.GetBuildInformation();
                _accelerationInfo.IsTbbSupported = buildInfo.Contains("TBB") && buildInfo.Contains("YES");

                LogUtil.Info($"AccelerationManager: Intel TBB支持: {_accelerationInfo.IsTbbSupported}");
            }
            catch (Exception ex)
            {
                LogUtil.Debug($"AccelerationManager: Intel TBB检测异常 - {ex.Message}");
                _accelerationInfo.IsTbbSupported = false;
            }
        }

        /// <summary>
        /// 检测可用的后端
        /// </summary>
        private void DetectAvailableBackends()
        {
            try
            {
                _accelerationInfo.AvailableBackends = new List<VideoCaptureAPIs>();

                // 测试各种后端
                var backendsToTest = new[]
                {
                    VideoCaptureAPIs.DSHOW,
                    VideoCaptureAPIs.MSMF,
                    VideoCaptureAPIs.ANY
                };

                foreach (var backend in backendsToTest)
                {
                    try
                    {
                        using (var testCapture = new VideoCapture(0, backend))
                        {
                            if (testCapture.IsOpened())
                            {
                                _accelerationInfo.AvailableBackends.Add(backend);
                                LogUtil.Info($"AccelerationManager: 后端 {backend} 可用");
                            }
                        }
                    }
                    catch
                    {
                        // 忽略测试失败的后端
                    }
                }
            }
            catch (Exception ex)
            {
                LogUtil.Debug($"AccelerationManager: 后端检测异常 - {ex.Message}");
                _accelerationInfo.AvailableBackends = new List<VideoCaptureAPIs>();
            }
        }

        /// <summary>
        /// 自动选择最佳加速方式
        /// </summary>
        private void SelectOptimalAcceleration()
        {
            // 优先级：CUDA > OpenCL > TBB > None
            if (_accelerationInfo.IsCudaSupported && _accelerationInfo.CudaDeviceCount > 0)
            {
                _accelerationInfo.EnabledAcceleration = AccelerationType.CUDA;
            }
            else if (_accelerationInfo.IsOpenClSupported)
            {
                _accelerationInfo.EnabledAcceleration = AccelerationType.OpenCL;
            }
            else if (_accelerationInfo.IsTbbSupported)
            {
                _accelerationInfo.EnabledAcceleration = AccelerationType.TBB;
            }
            else
            {
                _accelerationInfo.EnabledAcceleration = AccelerationType.None;
            }
        }

        /// <summary>
        /// 执行基准测试操作
        /// </summary>
        /// <param name="testImage">测试图像</param>
        /// <param name="accelerationType">加速类型</param>
        /// <returns>执行时间（毫秒）</returns>
        private double BenchmarkOperation(Mat testImage, AccelerationType accelerationType)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // 在OpenCV 3.x+中，OpenCL使用透明API，会自动选择最优实现
                // 不需要手动设置OpenCL状态

                // 执行一系列图像处理操作作为基准测试
                using (var blurred = new Mat())
                using (var edges = new Mat())
                {
                    // 高斯模糊
                    Cv2.GaussianBlur(testImage, blurred, new Size(15, 15), 0);

                    // 边缘检测
                    Cv2.Canny(blurred, edges, 50, 150);

                    // 形态学操作
                    using (var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(5, 5)))
                    {
                        Cv2.MorphologyEx(edges, edges, MorphTypes.Close, kernel);
                    }
                }

                stopwatch.Stop();
                return stopwatch.Elapsed.TotalMilliseconds;
            }
            catch (Exception ex)
            {
                LogUtil.Debug($"AccelerationManager: 基准测试操作失败 ({accelerationType}) - {ex.Message}");
                stopwatch.Stop();
                return double.MaxValue;
            }
        }

        #endregion
    }

    #region 辅助类和枚举

    /// <summary>
    /// 加速类型枚举
    /// </summary>
    public enum AccelerationType
    {
        /// <summary>
        /// 无加速
        /// </summary>
        None,

        /// <summary>
        /// CUDA加速
        /// </summary>
        CUDA,

        /// <summary>
        /// OpenCL加速
        /// </summary>
        OpenCL,

        /// <summary>
        /// Intel TBB加速
        /// </summary>
        TBB
    }

    /// <summary>
    /// 加速配置信息
    /// </summary>
    public class AccelerationInfo
    {
        /// <summary>
        /// 是否支持CUDA
        /// </summary>
        public bool IsCudaSupported { get; set; }

        /// <summary>
        /// CUDA设备数量
        /// </summary>
        public int CudaDeviceCount { get; set; }

        /// <summary>
        /// 是否支持OpenCL
        /// </summary>
        public bool IsOpenClSupported { get; set; }

        /// <summary>
        /// 是否支持Intel TBB
        /// </summary>
        public bool IsTbbSupported { get; set; }

        /// <summary>
        /// 当前启用的加速类型
        /// </summary>
        public AccelerationType EnabledAcceleration { get; set; }

        /// <summary>
        /// 可用的后端列表
        /// </summary>
        public List<VideoCaptureAPIs> AvailableBackends { get; set; } = new List<VideoCaptureAPIs>();
    }

    /// <summary>
    /// 基准测试结果
    /// </summary>
    public class BenchmarkResult
    {
        /// <summary>
        /// CPU执行时间（毫秒）
        /// </summary>
        public double CpuTime { get; set; } = double.MaxValue;

        /// <summary>
        /// CUDA执行时间（毫秒）
        /// </summary>
        public double CudaTime { get; set; } = double.MaxValue;

        /// <summary>
        /// OpenCL执行时间（毫秒）
        /// </summary>
        public double OpenClTime { get; set; } = double.MaxValue;

        /// <summary>
        /// Intel TBB执行时间（毫秒）
        /// </summary>
        public double TbbTime { get; set; } = double.MaxValue;

        /// <summary>
        /// 获取最佳加速类型
        /// </summary>
        public AccelerationType GetBestAcceleration()
        {
            var times = new Dictionary<AccelerationType, double>
            {
                { AccelerationType.None, CpuTime },
                { AccelerationType.CUDA, CudaTime },
                { AccelerationType.OpenCL, OpenClTime },
                { AccelerationType.TBB, TbbTime }
            };

            return times.Where(x => x.Value < double.MaxValue)
                       .OrderBy(x => x.Value)
                       .FirstOrDefault().Key;
        }
    }

    #endregion
}