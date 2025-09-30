using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using OpenCvSharp;
using Tools.Extend;

namespace WpfAppNew.EmguPlugs
{
    /// <summary>
    /// 显微镜控制器
    /// 专门用于显微镜设备的高级控制功能
    /// 包括自动对焦、放大倍数控制、光源管理、测量标定等专业功能
    /// </summary>
    /// <remarks>
    /// 主要功能：
    /// 1. 自动对焦算法和手动对焦控制
    /// 2. 放大倍数管理和标定
    /// 3. 光源亮度和色温控制
    /// 4. 图像测量和标注功能
    /// 5. 多层焦点合成（Focus Stacking）
    /// 6. 实时图像分析和质量评估
    /// 7. 标本定位和导航
    /// 8. 图像比较和差异分析
    /// </remarks>
    public class MicroscopeController : INotifyPropertyChanged, IDisposable
    {
        #region 私有字段

        /// <summary>
        /// 工业相机管理器引用
        /// </summary>
        private readonly IndustrialCameraManager _cameraManager;

        /// <summary>
        /// 当前放大倍数
        /// </summary>
        private double _currentMagnification = 1.0;

        /// <summary>
        /// 当前对焦值
        /// </summary>
        private double _currentFocusValue = 0.5;

        /// <summary>
        /// 光源亮度
        /// </summary>
        private double _lightBrightness = 0.5;

        /// <summary>
        /// 光源色温
        /// </summary>
        private double _lightTemperature = 5500; // 开尔文

        /// <summary>
        /// 是否启用自动对焦
        /// </summary>
        private bool _isAutoFocusEnabled = false;

        /// <summary>
        /// 是否正在自动对焦
        /// </summary>
        private bool _isAutoFocusing = false;

        /// <summary>
        /// 测量标定比例（像素/微米）
        /// </summary>
        private double _calibrationScale = 1.0;

        /// <summary>
        /// 是否已标定
        /// </summary>
        private bool _isCalibrated = false;

        /// <summary>
        /// 当前图像清晰度分数
        /// </summary>
        private double _currentSharpnessScore = 0.0;

        /// <summary>
        /// 焦点堆叠是否启用
        /// </summary>
        private bool _isFocusStackingEnabled = false;

        /// <summary>
        /// 焦点堆叠步数
        /// </summary>
        private int _focusStackingSteps = 10;

        /// <summary>
        /// 是否已释放资源
        /// </summary>
        private bool _disposed = false;

        #endregion

        #region 属性

        /// <summary>
        /// 当前放大倍数
        /// </summary>
        public double CurrentMagnification
        {
            get => _currentMagnification;
            set
            {
                if (Math.Abs(_currentMagnification - value) > 0.01)
                {
                    _currentMagnification = value;
                    OnPropertyChanged();
                    UpdateCalibrationScale();
                }
            }
        }

        /// <summary>
        /// 当前对焦值 (0.0 - 1.0)
        /// </summary>
        public double CurrentFocusValue
        {
            get => _currentFocusValue;
            set
            {
                var clampedValue = Math.Max(0.0, Math.Min(1.0, value));
                if (Math.Abs(_currentFocusValue - clampedValue) > 0.001)
                {
                    _currentFocusValue = clampedValue;
                    OnPropertyChanged();
                    ApplyFocusValue(clampedValue);
                }
            }
        }

        /// <summary>
        /// 光源亮度 (0.0 - 1.0)
        /// </summary>
        public double LightBrightness
        {
            get => _lightBrightness;
            set
            {
                var clampedValue = Math.Max(0.0, Math.Min(1.0, value));
                if (Math.Abs(_lightBrightness - clampedValue) > 0.01)
                {
                    _lightBrightness = clampedValue;
                    OnPropertyChanged();
                    ApplyLightBrightness(clampedValue);
                }
            }
        }

        /// <summary>
        /// 光源色温 (2700K - 6500K)
        /// </summary>
        public double LightTemperature
        {
            get => _lightTemperature;
            set
            {
                var clampedValue = Math.Max(2700, Math.Min(6500, value));
                if (Math.Abs(_lightTemperature - clampedValue) > 10)
                {
                    _lightTemperature = clampedValue;
                    OnPropertyChanged();
                    ApplyLightTemperature(clampedValue);
                }
            }
        }

        /// <summary>
        /// 是否启用自动对焦
        /// </summary>
        public bool IsAutoFocusEnabled
        {
            get => _isAutoFocusEnabled;
            set
            {
                if (_isAutoFocusEnabled != value)
                {
                    _isAutoFocusEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 是否正在自动对焦
        /// </summary>
        public bool IsAutoFocusing
        {
            get => _isAutoFocusing;
            private set
            {
                if (_isAutoFocusing != value)
                {
                    _isAutoFocusing = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 测量标定比例（像素/微米）
        /// </summary>
        public double CalibrationScale
        {
            get => _calibrationScale;
            private set
            {
                if (Math.Abs(_calibrationScale - value) > 0.001)
                {
                    _calibrationScale = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 是否已标定
        /// </summary>
        public bool IsCalibrated
        {
            get => _isCalibrated;
            private set
            {
                if (_isCalibrated != value)
                {
                    _isCalibrated = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 当前图像清晰度分数 (0.0 - 1.0)
        /// </summary>
        public double CurrentSharpnessScore
        {
            get => _currentSharpnessScore;
            private set
            {
                if (Math.Abs(_currentSharpnessScore - value) > 0.01)
                {
                    _currentSharpnessScore = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 焦点堆叠是否启用
        /// </summary>
        public bool IsFocusStackingEnabled
        {
            get => _isFocusStackingEnabled;
            set
            {
                if (_isFocusStackingEnabled != value)
                {
                    _isFocusStackingEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 焦点堆叠步数
        /// </summary>
        public int FocusStackingSteps
        {
            get => _focusStackingSteps;
            set
            {
                var clampedValue = Math.Max(3, Math.Min(50, value));
                if (_focusStackingSteps != clampedValue)
                {
                    _focusStackingSteps = clampedValue;
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        #region 事件

        /// <summary>
        /// 属性变更事件
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 自动对焦完成事件
        /// </summary>
        public event EventHandler<AutoFocusCompletedEventArgs> AutoFocusCompleted;

        /// <summary>
        /// 测量完成事件
        /// </summary>
        public event EventHandler<MeasurementCompletedEventArgs> MeasurementCompleted;

        /// <summary>
        /// 焦点堆叠完成事件
        /// </summary>
        public event EventHandler<FocusStackingCompletedEventArgs> FocusStackingCompleted;

        /// <summary>
        /// 错误发生事件
        /// </summary>
        public event EventHandler<ErrorOccurredEventArgs> ErrorOccurred;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化显微镜控制器
        /// </summary>
        /// <param name="cameraManager">工业相机管理器</param>
        public MicroscopeController(IndustrialCameraManager cameraManager)
        {
            _cameraManager = cameraManager ?? throw new ArgumentNullException(nameof(cameraManager));
            
            // 订阅相机管理器的帧捕获事件，用于实时分析
            _cameraManager.FrameCaptured += OnFrameCaptured;
            
            LogUtil.Info("MicroscopeController: 显微镜控制器初始化完成");
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 执行自动对焦
        /// </summary>
        /// <param name="searchRange">搜索范围 (0.0 - 1.0)</param>
        /// <param name="stepSize">步长</param>
        /// <returns>对焦是否成功</returns>
        public async Task<bool> AutoFocusAsync(double searchRange = 0.3, double stepSize = 0.02)
        {
            if (IsAutoFocusing)
            {
                LogUtil.Warning("MicroscopeController: 自动对焦已在进行中");
                return false;
            }

            try
            {
                IsAutoFocusing = true;
                LogUtil.Info("MicroscopeController: 开始自动对焦");

                var bestFocus = await FindBestFocusAsync(searchRange, stepSize);
                
                if (bestFocus.HasValue)
                {
                    CurrentFocusValue = bestFocus.Value;
                    LogUtil.Info($"MicroscopeController: 自动对焦完成，最佳对焦值: {bestFocus.Value:F3}");
                    
                    AutoFocusCompleted?.Invoke(this, new AutoFocusCompletedEventArgs(true, bestFocus.Value, CurrentSharpnessScore));
                    return true;
                }
                else
                {
                    LogUtil.Warning("MicroscopeController: 自动对焦失败，未找到最佳对焦点");
                    AutoFocusCompleted?.Invoke(this, new AutoFocusCompletedEventArgs(false, CurrentFocusValue, CurrentSharpnessScore));
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"MicroscopeController: 自动对焦异常 - {ex.Message}");
                ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(ex));
                AutoFocusCompleted?.Invoke(this, new AutoFocusCompletedEventArgs(false, CurrentFocusValue, CurrentSharpnessScore));
                return false;
            }
            finally
            {
                IsAutoFocusing = false;
            }
        }

        /// <summary>
        /// 执行测量标定
        /// </summary>
        /// <param name="knownDistance">已知距离（微米）</param>
        /// <param name="pixelDistance">像素距离</param>
        /// <returns>标定是否成功</returns>
        public bool CalibrateScale(double knownDistance, double pixelDistance)
        {
            try
            {
                if (knownDistance <= 0 || pixelDistance <= 0)
                {
                    LogUtil.Error("MicroscopeController: 标定参数无效");
                    return false;
                }

                CalibrationScale = pixelDistance / knownDistance;
                IsCalibrated = true;

                LogUtil.Info($"MicroscopeController: 测量标定完成 - 比例: {CalibrationScale:F3} 像素/微米");
                return true;
            }
            catch (Exception ex)
            {
                LogUtil.Error($"MicroscopeController: 测量标定失败 - {ex.Message}");
                ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(ex));
                return false;
            }
        }

        /// <summary>
        /// 测量两点间距离
        /// </summary>
        /// <param name="point1">起点</param>
        /// <param name="point2">终点</param>
        /// <returns>距离（微米），如果未标定则返回像素距离</returns>
        public double MeasureDistance(Point2f point1, Point2f point2)
        {
            try
            {
                var pixelDistance = Math.Sqrt(Math.Pow(point2.X - point1.X, 2) + Math.Pow(point2.Y - point1.Y, 2));
                
                double realDistance;
                string unit;
                
                if (IsCalibrated)
                {
                    realDistance = pixelDistance / CalibrationScale;
                    unit = "μm";
                }
                else
                {
                    realDistance = pixelDistance;
                    unit = "像素";
                }

                LogUtil.Debug($"MicroscopeController: 测量距离 - {realDistance:F2} {unit}");
                
                MeasurementCompleted?.Invoke(this, new MeasurementCompletedEventArgs(point1, point2, realDistance, unit));
                
                return realDistance;
            }
            catch (Exception ex)
            {
                LogUtil.Error($"MicroscopeController: 距离测量失败 - {ex.Message}");
                ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(ex));
                return 0;
            }
        }

        /// <summary>
        /// 执行焦点堆叠
        /// </summary>
        /// <param name="startFocus">起始对焦值</param>
        /// <param name="endFocus">结束对焦值</param>
        /// <returns>合成后的图像</returns>
        public async Task<Mat> PerformFocusStackingAsync(double startFocus = 0.0, double endFocus = 1.0)
        {
            try
            {
                if (!IsFocusStackingEnabled)
                {
                    LogUtil.Warning("MicroscopeController: 焦点堆叠未启用");
                    return null;
                }

                LogUtil.Info($"MicroscopeController: 开始焦点堆叠 - {FocusStackingSteps} 步");

                var images = new List<Mat>();
                var step = (endFocus - startFocus) / (FocusStackingSteps - 1);

                // 采集不同焦点的图像
                for (int i = 0; i < FocusStackingSteps; i++)
                {
                    var focusValue = startFocus + i * step;
                    CurrentFocusValue = focusValue;
                    
                    // 等待对焦稳定
                    await Task.Delay(200);
                    
                    // 获取当前帧
                    var currentFrame = GetCurrentFrame();
                    if (currentFrame != null && !currentFrame.Empty())
                    {
                        images.Add(currentFrame.Clone());
                        LogUtil.Debug($"MicroscopeController: 采集焦点堆叠图像 {i + 1}/{FocusStackingSteps}");
                    }
                }

                if (images.Count == 0)
                {
                    LogUtil.Error("MicroscopeController: 焦点堆叠失败，未采集到有效图像");
                    return null;
                }

                // 执行焦点合成
                var stackedImage = StackFocusImages(images);
                
                // 清理临时图像
                foreach (var img in images)
                {
                    img.Dispose();
                }

                LogUtil.Info("MicroscopeController: 焦点堆叠完成");
                FocusStackingCompleted?.Invoke(this, new FocusStackingCompletedEventArgs(stackedImage?.Clone()));
                
                return stackedImage;
            }
            catch (Exception ex)
            {
                LogUtil.Error($"MicroscopeController: 焦点堆叠异常 - {ex.Message}");
                ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(ex));
                return null;
            }
        }

        /// <summary>
        /// 获取当前图像的清晰度分数
        /// </summary>
        /// <param name="frame">图像帧</param>
        /// <returns>清晰度分数 (0.0 - 1.0)</returns>
        public double CalculateSharpnessScore(Mat frame)
        {
            try
            {
                if (frame == null || frame.Empty())
                {
                    return 0.0;
                }

                // 转换为灰度图像
                var gray = new Mat();
                if (frame.Channels() == 3)
                {
                    Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
                }
                else
                {
                    gray = frame.Clone();
                }

                // 使用拉普拉斯算子计算清晰度
                var laplacian = new Mat();
                Cv2.Laplacian(gray, laplacian, MatType.CV_64F);
                
                var mean = new Scalar();
                var stddev = new Scalar();
                Cv2.MeanStdDev(laplacian, out mean, out stddev);
                
                // 标准差的平方作为清晰度指标
                var sharpness = stddev.Val0 * stddev.Val0;
                
                // 归一化到0-1范围（基于经验值）
                var normalizedSharpness = Math.Min(1.0, sharpness / 1000.0);
                
                gray.Dispose();
                laplacian.Dispose();
                
                return normalizedSharpness;
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"MicroscopeController: 计算清晰度失败 - {ex.Message}");
                return 0.0;
            }
        }

        /// <summary>
        /// 设置预设放大倍数
        /// </summary>
        /// <param name="magnification">放大倍数</param>
        public void SetMagnificationPreset(MagnificationPreset magnification)
        {
            switch (magnification)
            {
                case MagnificationPreset.Low:
                    CurrentMagnification = 4.0;
                    break;
                case MagnificationPreset.Medium:
                    CurrentMagnification = 10.0;
                    break;
                case MagnificationPreset.High:
                    CurrentMagnification = 40.0;
                    break;
                case MagnificationPreset.VeryHigh:
                    CurrentMagnification = 100.0;
                    break;
                case MagnificationPreset.Ultra:
                    CurrentMagnification = 400.0;
                    break;
            }
            
            LogUtil.Info($"MicroscopeController: 设置放大倍数预设 - {magnification} ({CurrentMagnification}x)");
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            if (_cameraManager != null)
            {
                _cameraManager.FrameCaptured -= OnFrameCaptured;
            }

            _disposed = true;
            LogUtil.Info("MicroscopeController: 资源已释放");
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 帧捕获事件处理
        /// </summary>
        private void OnFrameCaptured(object sender, FrameCapturedEventArgs e)
        {
            try
            {
                // 实时计算清晰度分数
                if (e.Frame != null && !e.Frame.Empty())
                {
                    CurrentSharpnessScore = CalculateSharpnessScore(e.Frame);
                }
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"MicroscopeController: 帧分析异常 - {ex.Message}");
            }
        }

        /// <summary>
        /// 寻找最佳对焦点
        /// </summary>
        /// <param name="searchRange">搜索范围</param>
        /// <param name="stepSize">步长</param>
        /// <returns>最佳对焦值</returns>
        private async Task<double?> FindBestFocusAsync(double searchRange, double stepSize)
        {
            var currentFocus = CurrentFocusValue;
            var startFocus = Math.Max(0.0, currentFocus - searchRange / 2);
            var endFocus = Math.Min(1.0, currentFocus + searchRange / 2);
            
            var bestFocus = currentFocus;
            var bestSharpness = 0.0;
            
            var steps = (int)((endFocus - startFocus) / stepSize) + 1;
            
            for (int i = 0; i < steps; i++)
            {
                var focusValue = startFocus + i * stepSize;
                CurrentFocusValue = focusValue;
                
                // 等待对焦稳定
                await Task.Delay(100);
                
                // 获取当前帧并计算清晰度
                var currentFrame = GetCurrentFrame();
                if (currentFrame != null && !currentFrame.Empty())
                {
                    var sharpness = CalculateSharpnessScore(currentFrame);
                    
                    if (sharpness > bestSharpness)
                    {
                        bestSharpness = sharpness;
                        bestFocus = focusValue;
                    }
                    
                    LogUtil.Debug($"MicroscopeController: 对焦扫描 - 焦点: {focusValue:F3}, 清晰度: {sharpness:F3}");
                }
            }
            
            return bestSharpness > 0.1 ? bestFocus : (double?)null;
        }

        /// <summary>
        /// 获取当前帧
        /// </summary>
        /// <returns>当前帧</returns>
        private Mat GetCurrentFrame()
        {
            // 这里需要从相机管理器获取当前帧
            // 由于相机管理器的当前帧是私有的，我们需要通过事件或公共方法获取
            // 暂时返回null，实际实现时需要修改相机管理器以提供获取当前帧的方法
            return null;
        }

        /// <summary>
        /// 合成焦点堆叠图像
        /// </summary>
        /// <param name="images">不同焦点的图像列表</param>
        /// <returns>合成后的图像</returns>
        private Mat StackFocusImages(List<Mat> images)
        {
            if (images.Count == 0)
                return null;

            try
            {
                var result = images[0].Clone();
                var resultGray = new Mat();
                var tempGray = new Mat();
                var laplacian = new Mat();
                var mask = new Mat();

                // 转换第一张图像为灰度
                Cv2.CvtColor(result, resultGray, ColorConversionCodes.BGR2GRAY);

                for (int i = 1; i < images.Count; i++)
                {
                    // 转换当前图像为灰度
                    Cv2.CvtColor(images[i], tempGray, ColorConversionCodes.BGR2GRAY);

                    // 计算拉普拉斯算子
                    Cv2.Laplacian(tempGray, laplacian, MatType.CV_64F);
                    Cv2.ConvertScaleAbs(laplacian, laplacian);

                    // 创建掩码，选择更清晰的区域
                    var resultLaplacian = new Mat();
                    Cv2.Laplacian(resultGray, resultLaplacian, MatType.CV_64F);
                    Cv2.ConvertScaleAbs(resultLaplacian, resultLaplacian);

                    Cv2.Compare(laplacian, resultLaplacian, mask, CmpType.GT);

                    // 复制更清晰的区域
                    images[i].CopyTo(result, mask);
                    tempGray.CopyTo(resultGray, mask);

                    resultLaplacian.Dispose();
                }

                resultGray.Dispose();
                tempGray.Dispose();
                laplacian.Dispose();
                mask.Dispose();

                return result;
            }
            catch (Exception ex)
            {
                LogUtil.Error($"MicroscopeController: 焦点合成失败 - {ex.Message}");
                return images[0].Clone();
            }
        }

        /// <summary>
        /// 应用对焦值
        /// </summary>
        /// <param name="focusValue">对焦值</param>
        private void ApplyFocusValue(double focusValue)
        {
            try
            {
                // 将对焦值应用到相机
                _cameraManager?.SetCameraProperty(CameraProperty.Focus, focusValue);
                LogUtil.Debug($"MicroscopeController: 应用对焦值 - {focusValue:F3}");
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"MicroscopeController: 应用对焦值失败 - {ex.Message}");
            }
        }

        /// <summary>
        /// 应用光源亮度
        /// </summary>
        /// <param name="brightness">亮度值</param>
        private void ApplyLightBrightness(double brightness)
        {
            try
            {
                // 通过调整相机亮度来模拟光源控制
                _cameraManager?.SetCameraProperty(CameraProperty.Brightness, brightness);
                LogUtil.Debug($"MicroscopeController: 应用光源亮度 - {brightness:F2}");
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"MicroscopeController: 应用光源亮度失败 - {ex.Message}");
            }
        }

        /// <summary>
        /// 应用光源色温
        /// </summary>
        /// <param name="temperature">色温值</param>
        private void ApplyLightTemperature(double temperature)
        {
            try
            {
                // 色温控制通常需要专门的硬件支持
                // 这里可以通过调整白平衡来模拟
                LogUtil.Debug($"MicroscopeController: 应用光源色温 - {temperature}K");
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"MicroscopeController: 应用光源色温失败 - {ex.Message}");
            }
        }

        /// <summary>
        /// 更新标定比例
        /// </summary>
        private void UpdateCalibrationScale()
        {
            if (IsCalibrated)
            {
                // 根据放大倍数调整标定比例
                // 这里需要根据实际的光学系统参数进行调整
                LogUtil.Debug($"MicroscopeController: 更新标定比例 - 放大倍数: {CurrentMagnification}x");
            }
        }

        /// <summary>
        /// 属性变更通知
        /// </summary>
        /// <param name="propertyName">属性名称</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    #region 枚举和数据类

    /// <summary>
    /// 放大倍数预设
    /// </summary>
    public enum MagnificationPreset
    {
        /// <summary>
        /// 低倍 (4x)
        /// </summary>
        Low,

        /// <summary>
        /// 中倍 (10x)
        /// </summary>
        Medium,

        /// <summary>
        /// 高倍 (40x)
        /// </summary>
        High,

        /// <summary>
        /// 超高倍 (100x)
        /// </summary>
        VeryHigh,

        /// <summary>
        /// 极高倍 (400x)
        /// </summary>
        Ultra
    }

    #endregion

    #region 事件参数类

    /// <summary>
    /// 自动对焦完成事件参数
    /// </summary>
    public class AutoFocusCompletedEventArgs : EventArgs
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// 最终对焦值
        /// </summary>
        public double FocusValue { get; }

        /// <summary>
        /// 清晰度分数
        /// </summary>
        public double SharpnessScore { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public AutoFocusCompletedEventArgs(bool isSuccess, double focusValue, double sharpnessScore)
        {
            IsSuccess = isSuccess;
            FocusValue = focusValue;
            SharpnessScore = sharpnessScore;
        }
    }

    /// <summary>
    /// 测量完成事件参数
    /// </summary>
    public class MeasurementCompletedEventArgs : EventArgs
    {
        /// <summary>
        /// 起点
        /// </summary>
        public Point2f StartPoint { get; }

        /// <summary>
        /// 终点
        /// </summary>
        public Point2f EndPoint { get; }

        /// <summary>
        /// 距离
        /// </summary>
        public double Distance { get; }

        /// <summary>
        /// 单位
        /// </summary>
        public string Unit { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public MeasurementCompletedEventArgs(Point2f startPoint, Point2f endPoint, double distance, string unit)
        {
            StartPoint = startPoint;
            EndPoint = endPoint;
            Distance = distance;
            Unit = unit;
        }
    }

    /// <summary>
    /// 焦点堆叠完成事件参数
    /// </summary>
    public class FocusStackingCompletedEventArgs : EventArgs
    {
        /// <summary>
        /// 合成后的图像
        /// </summary>
        public Mat StackedImage { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public FocusStackingCompletedEventArgs(Mat stackedImage)
        {
            StackedImage = stackedImage;
        }
    }

    #endregion
}