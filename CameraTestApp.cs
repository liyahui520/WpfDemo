using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WpfDemo.OpenCv.Controls;

namespace WpfDemo
{
    /// <summary>
    /// 摄像头测试应用程序
    /// 用于验证CameraPreviewControl的修复效果
    /// </summary>
    public partial class CameraTestWindow : Window
    {
        /// <summary>
        /// 摄像头预览控件
        /// </summary>
        private CameraPreviewControl _cameraPreview;

        /// <summary>
        /// 构造函数
        /// </summary>
        public CameraTestWindow()
        {
            InitializeComponent();
            InitializeCameraPreview();
        }

        /// <summary>
        /// 初始化摄像头预览控件
        /// </summary>
        private void InitializeCameraPreview()
        {
            try
            {
                _cameraPreview = new CameraPreviewControl();
                
                // 将控件添加到窗口
                var grid = new Grid();
                grid.Children.Add(_cameraPreview);
                this.Content = grid;

                // 订阅事件
                _cameraPreview.DeviceChanged += OnDeviceChanged;
                _cameraPreview.PreviewStarted += OnPreviewStarted;
                _cameraPreview.PreviewStopped += OnPreviewStopped;

                Console.WriteLine("CameraTestApp: 摄像头预览控件初始化完成");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CameraTestApp: 初始化失败 - {ex.Message}");
                MessageBox.Show($"摄像头初始化失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 设备变更事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void OnDeviceChanged(object sender, EventArgs e)
        {
            Console.WriteLine("CameraTestApp: 设备已变更");
        }

        /// <summary>
        /// 预览开始事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void OnPreviewStarted(object sender, EventArgs e)
        {
            Console.WriteLine("CameraTestApp: 预览已开始");
            this.Title = "摄像头测试 - 预览中";
        }

        /// <summary>
        /// 预览停止事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void OnPreviewStopped(object sender, EventArgs e)
        {
            Console.WriteLine("CameraTestApp: 预览已停止");
            this.Title = "摄像头测试 - 已停止";
        }

        /// <summary>
        /// 测试使用特定设备路径启动预览
        /// </summary>
        /// <param name="devicePath">设备路径</param>
        /// <returns>启动结果</returns>
        public async Task<bool> TestStartPreviewWithDevicePath(string devicePath)
        {
            try
            {
                Console.WriteLine($"CameraTestApp: 测试启动预览，设备路径: {devicePath}");
                
                var result = await _cameraPreview.StartPreviewAsync(devicePath);
                
                Console.WriteLine($"CameraTestApp: 预览启动结果: {result}");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CameraTestApp: 预览启动异常 - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 测试使用设备名称启动预览
        /// </summary>
        /// <param name="deviceName">设备名称</param>
        /// <returns>启动结果</returns>
        public async Task<bool> TestStartPreviewWithDeviceName(string deviceName)
        {
            try
            {
                Console.WriteLine($"CameraTestApp: 测试启动预览，设备名称: {deviceName}");
                
                var result = await _cameraPreview.StartPreviewAsync(deviceName);
                
                Console.WriteLine($"CameraTestApp: 预览启动结果: {result}");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CameraTestApp: 预览启动异常 - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 窗口关闭时清理资源
        /// </summary>
        /// <param name="e">事件参数</param>
        protected override void OnClosed(EventArgs e)
        {
            try
            {
                _cameraPreview?.StopPreview();
                _cameraPreview?.Dispose();
                Console.WriteLine("CameraTestApp: 资源清理完成");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CameraTestApp: 资源清理异常 - {ex.Message}");
            }
            
            base.OnClosed(e);
        }
    }
}