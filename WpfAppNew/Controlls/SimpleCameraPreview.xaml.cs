using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WpfAppNew.Services;

namespace WpfAppNew.Controlls
{
    /// <summary>
    /// 分辨率信息类
    /// </summary>
    public class ResolutionInfo
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public string Description { get; set; }

        public ResolutionInfo(int width, int height, string description)
        {
            Width = width;
            Height = height;
            Description = description;
        }

        public override string ToString()
        {
            return Description;
        }
    }
    /// <summary>
    /// SimpleCameraPreview.xaml 的交互逻辑
    /// </summary>
    public partial class SimpleCameraPreview : UserControl
    {
        private readonly ICameraService _cameraService;
        private readonly IImageConverterService _imageConverterService;
        private readonly ILoggerService _loggerService;
        private CancellationTokenSource _cancellationTokenSource;
        private Task _previewTask;
        private int _currentCameraIndex = 0;
        private int _currentWidth = 640;
        private int _currentHeight = 480;

        public SimpleCameraPreview()
        {
            InitializeComponent();

            // 初始化服务
            _cameraService = new CameraService();
            _imageConverterService = new ImageConverterService();
            _loggerService = new LoggerService();

            UpdateStatus("应用程序已启动，正在加载摄像头列表...");
            _loggerService.Info("应用程序启动");
            LoadCameraList();
            LoadResolutionList();
        }

        /// <summary>
        /// 加载分辨率列表
        /// </summary>
        private void LoadResolutionList()
        {
            try
            {
                ResolutionComboBox.Items.Clear();

                // 添加常用分辨率
                var resolutions = new List<ResolutionInfo>
                {
                    new ResolutionInfo(320, 240, "320x240"),
                    new ResolutionInfo(640, 480, "640x480 (VGA)"),
                    new ResolutionInfo(800, 600, "800x600 (SVGA)"),
                    new ResolutionInfo(1024, 768, "1024x768 (XGA)"),
                    new ResolutionInfo(1280, 720, "1280x720 (HD)"),
                    new ResolutionInfo(1920, 1080, "1920x1080 (Full HD)")
                };

                foreach (var resolution in resolutions)
                {
                    ResolutionComboBox.Items.Add(resolution);
                }

                // 默认选择640x480
                ResolutionComboBox.SelectedIndex = 1;
            }
            catch (Exception ex)
            {
                UpdateStatus("加载分辨率列表失败: " + ex.Message);
                _loggerService.Error("加载分辨率列表失败", ex);
            }
        }

        /// <summary>
        /// 加载摄像头列表
        /// </summary>
        private async void LoadCameraList()
        {
            try
            {
                CameraListComboBox.Items.Clear();
                UpdateStatus("正在加载摄像头列表...");

                // 异步获取可用的摄像头
                var cameraList = await _cameraService.GetAvailableCamerasAsync();

                if (cameraList.Count == 0)
                {
                    cameraList.Add(new CameraInfo(0, "未检测到摄像头"));
                    CameraListComboBox.IsEnabled = false;
                }
                else
                {
                    CameraListComboBox.IsEnabled = true;
                }

                // 填充摄像头列表
                foreach (var camera in cameraList)
                {
                    CameraListComboBox.Items.Add(camera);
                }

                // 选择第一个摄像头
                if (CameraListComboBox.Items.Count > 0)
                {
                    CameraListComboBox.SelectedIndex = 0;
                }

                UpdateStatus("已加载 " + cameraList.Count + " 个摄像头");
                _loggerService.Info($"已加载 {cameraList.Count} 个摄像头");
            }
            catch (Exception ex)
            {
                UpdateStatus("加载摄像头列表失败: " + ex.Message);
                _loggerService.Error("加载摄像头列表失败", ex);
            }
        }

        /// <summary>
        /// 刷新摄像头列表
        /// </summary>
        private void RefreshCameraList_Click(object sender, RoutedEventArgs e)
        {
            UpdateStatus("正在刷新摄像头列表...");
            _loggerService.Info("刷新摄像头列表");
            LoadCameraList();
        }

        /// <summary>
        /// 分辨率选择变更
        /// </summary>
        private void ResolutionComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ResolutionComboBox.SelectedItem is ResolutionInfo selectedResolution)
            {
                _currentWidth = selectedResolution.Width;
                _currentHeight = selectedResolution.Height;
                UpdateStatus($"已选择分辨率: {selectedResolution.Description}");
                _loggerService.Info($"已选择分辨率: {selectedResolution.Description}");

                // 如果正在预览，重新启动预览以应用新的分辨率
                if (_cameraService.IsPreviewing)
                {
                    StopPreview_Click(sender, e);
                    StartPreview_Click(sender, e);
                }
            }
        }

        /// <summary>
        /// 摄像头选择变更
        /// </summary>
        private void CameraListComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (CameraListComboBox.SelectedItem is CameraInfo selectedCamera)
            {
                _currentCameraIndex = selectedCamera.Index;
                UpdateStatus("已选择摄像头: " + selectedCamera.Name);
                _loggerService.Info($"已选择摄像头: {selectedCamera.Name}");

                // 如果正在预览，切换到新摄像头
                if (_cameraService.IsPreviewing)
                {
                    StopPreview_Click(sender, e);
                    StartPreview_Click(sender, e);
                }
            }
        }

        private void StartPreview_Click(object sender, RoutedEventArgs e)
        {
            if (_cameraService.IsPreviewing)
            {
                UpdateStatus("预览已在运行");
                _loggerService.Warning("预览已在运行");
                return;
            }

            try
            {
                UpdateStatus($"正在初始化摄像头 {_currentCameraIndex}，分辨率 {_currentWidth}x{_currentHeight}...");
                _loggerService.Info($"正在初始化摄像头 {_currentCameraIndex}，分辨率 {_currentWidth}x{_currentHeight}");

                // 初始化摄像头，使用选定的分辨率
                bool initialized = _cameraService.InitializeCamera(_currentCameraIndex, _currentWidth, _currentHeight);

                if (!initialized)
                {
                    string errorMsg = "错误：无法打开摄像头，请检查设备连接";
                    UpdateStatus(errorMsg);
                    _loggerService.Error(errorMsg);
                    return;
                }

                UpdateStatus($"摄像头 {_currentCameraIndex} 初始化成功，开始预览 ({_currentWidth}x{_currentHeight})");
                _loggerService.Info($"摄像头 {_currentCameraIndex} 初始化成功，开始预览 ({_currentWidth}x{_currentHeight})");

                // 启动预览任务
                _cancellationTokenSource = new CancellationTokenSource();
                _previewTask = _cameraService.StartPreviewAsync(OnFrameReceived, _cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                string errorMsg = "启动预览失败: " + ex.Message;
                UpdateStatus(errorMsg);
                _loggerService.Error("启动预览失败", ex);
            }
        }

        /// <summary>
        /// 处理接收到的帧
        /// </summary>
        /// <param name="frame">接收到的帧</param>
        private void OnFrameReceived(Mat frame)
        {
            if (frame != null && !frame.Empty())
            {
                // 在UI线程上更新图像
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        var bitmap = _imageConverterService.MatToBitmapImage(frame);
                        PreviewImage.Source = bitmap;
                    }
                    catch (Exception ex)
                    {
                        string errorMsg = "更新预览失败: " + ex.Message;
                        UpdateStatus(errorMsg);
                        _loggerService.Error("更新预览失败", ex);
                    }
                    finally
                    {
                        // 使用完后释放帧
                        frame?.Dispose();
                    }
                }));
            }
            else
            {
                // 即使帧无效，也要确保释放它
                frame?.Dispose();
            }
        }

        private void StopPreview_Click(object sender, RoutedEventArgs e)
        {
            if (!_cameraService.IsPreviewing)
            {
                UpdateStatus("预览未运行");
                _loggerService.Warning("预览未运行");
                return;
            }

            try
            {
                UpdateStatus("正在停止预览...");
                _loggerService.Info("正在停止预览");

                // 停止预览
                _cameraService.StopPreview();
                _cancellationTokenSource?.Cancel();

                // 等待任务完成，但设置超时
                if (_previewTask != null)
                {
                    if (!_previewTask.Wait(3000)) // 等待最多3秒
                    {
                        UpdateStatus("警告：预览任务未及时停止");
                        _loggerService.Warning("预览任务未及时停止");
                    }
                }

                // 停止录制（如果正在录制）
                if (_cameraService.IsRecording)
                {
                    StopRecording();
                }

                UpdateStatus("预览已停止");
                _loggerService.Info("预览已停止");
                PreviewImage.Source = null;
            }
            catch (Exception ex)
            {
                UpdateStatus("停止预览失败: " + ex.Message);
                _loggerService.Error("停止预览失败", ex);
            }
        }

        private void StartRecording_Click(object sender, RoutedEventArgs e)
        {
            if (!_cameraService.IsPreviewing)
            {
                UpdateStatus("请先开始预览");
                _loggerService.Warning("请先开始预览");
                return;
            }

            if (_cameraService.IsRecording)
            {
                UpdateStatus("录制已在运行");
                _loggerService.Warning("录制已在运行");
                return;
            }

            try
            {
                // 获取选定的帧率
                int fps = 30;
                if (FpsComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem fpsItem &&
                    int.TryParse(fpsItem.Tag.ToString(), out int selectedFps))
                {
                    fps = selectedFps;
                }

                // 获取选定的质量
                string quality = "medium";
                if (QualityComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem qualityItem)
                {
                    quality = qualityItem.Tag.ToString();
                }

                // 创建视频文件名
                string fileName = "video_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".mp4";
                UpdateStatus($"正在创建视频文件: {fileName} (FPS: {fps}, 质量: {quality})");
                _loggerService.Info($"正在创建视频文件: {fileName} (FPS: {fps}, 质量: {quality})");

                bool started = _cameraService.StartRecording(fileName, fps, quality);

                if (!started)
                {
                    UpdateStatus("错误：无法创建视频文件，请检查编解码器");
                    _loggerService.Error("无法创建视频文件，请检查编解码器");
                    return;
                }

                UpdateStatus($"开始录制: {fileName} (FPS: {fps}, 质量: {quality})");
                _loggerService.Info($"开始录制: {fileName} (FPS: {fps}, 质量: {quality})");
            }
            catch (Exception ex)
            {
                UpdateStatus("开始录制失败: " + ex.Message);
                _loggerService.Error("开始录制失败", ex);
            }
        }

        private void StopRecording_Click(object sender, RoutedEventArgs e)
        {
            if (!_cameraService.IsRecording)
            {
                UpdateStatus("录制未运行");
                _loggerService.Warning("录制未运行");
                return;
            }

            StopRecording();
        }

        private void StopRecording()
        {
            try
            {
                _cameraService.StopRecording();
                UpdateStatus("录制已停止");
                _loggerService.Info("录制已停止");
            }
            catch (Exception ex)
            {
                UpdateStatus("停止录制失败: " + ex.Message);
                _loggerService.Error("停止录制失败", ex);
            }
        }

        private void UpdateStatus(string message)
        {
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                StatusText.Text = DateTime.Now.ToString("HH:mm:ss") + " - " + message;
            }));
        }
         
    }
}