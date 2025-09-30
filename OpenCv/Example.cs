using System;
using System.Threading.Tasks;
using OpenCv.Core;

namespace OpenCv.Examples
{
    /// <summary>
    /// OpenCV类库使用示例
    /// 演示如何使用摄像头管理器和相关功能
    /// </summary>
    public class Example
    {
        /// <summary>
        /// 基本摄像头操作示例
        /// </summary>
        /// <returns>异步任务</returns>
        public static async Task BasicCameraExample()
        {
            try
            {
                // 创建摄像头管理器实例
                var cameraManager = new CameraManager();
                
                Console.WriteLine("正在初始化摄像头管理器...");
                
                // 初始化摄像头捕获
                bool initialized = await cameraManager.InitializeCaptureAsync(640, 480);
                if (!initialized)
                {
                    Console.WriteLine("摄像头初始化失败");
                    return;
                }
                
                // 获取可用的摄像头设备
                var devices = cameraManager.AvailableDevices;
                
                Console.WriteLine($"发现 {devices.Count} 个摄像头设备:");
                foreach (var device in devices)
                {
                    Console.WriteLine($"- 设备 {device.Index}: {device.Name}");
                }
                
                if (devices.Count > 0)
                {
                    // 开始预览
                    cameraManager.StartPreview();
                    Console.WriteLine("预览已开始");
                    
                    // 等待一段时间
                    await Task.Delay(5000);
                    
                    // 停止预览
                    cameraManager.StopPreview();
                    Console.WriteLine("预览已停止");
                }
                else
                {
                    Console.WriteLine("未发现可用的摄像头设备");
                }
                
                // 释放资源
                cameraManager.Dispose();
                Console.WriteLine("摄像头管理器已释放");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发生错误: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 摄像头测试示例
        /// </summary>
        public static void CameraTestExample()
        {
            Console.WriteLine("开始摄像头访问测试...");
            
            // 运行摄像头测试
            var testResults = CameraTest.TestCameraAccess();
            
            // 输出测试结果
            foreach (var result in testResults)
            {
                Console.WriteLine(result);
            }
        }
        
        /// <summary>
        /// 设备管理示例
        /// </summary>
        public static void DeviceManagementExample()
        {
            try
            {
                var cameraManager = new CameraManager();
                
                Console.WriteLine("正在获取摄像头设备...");
                
                // 获取设备列表
                var devices = cameraManager.AvailableDevices;
                
                Console.WriteLine($"设备管理器发现 {devices.Count} 个设备:");
                foreach (var device in devices)
                {
                    Console.WriteLine($"- {device.Name} (索引: {device.Index})");
                    Console.WriteLine($"  状态: {(device.IsConnected ? "已连接" : "未连接")}");
                }
                
                cameraManager.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"设备管理错误: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 录像功能示例
        /// </summary>
        /// <returns>异步任务</returns>
        public static async Task RecordingExample()
        {
            try
            {
                var cameraManager = new CameraManager();
                
                Console.WriteLine("正在初始化摄像头...");
                
                // 初始化摄像头
                bool initialized = await cameraManager.InitializeCaptureAsync(640, 480);
                if (!initialized)
                {
                    Console.WriteLine("摄像头初始化失败");
                    return;
                }
                
                // 开始预览
                cameraManager.StartPreview();
                Console.WriteLine("预览已开始");
                
                // 开始录像
                string outputPath = $"recording_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
                bool recordingStarted = cameraManager.StartRecording(outputPath);
                
                if (recordingStarted)
                {
                    Console.WriteLine($"录像已开始，保存到: {outputPath}");
                    
                    // 录制5秒
                    await Task.Delay(5000);
                    
                    // 停止录像
                    cameraManager.StopRecording();
                    Console.WriteLine("录像已停止");
                }
                else
                {
                    Console.WriteLine("录像开始失败");
                }
                
                // 停止预览
                cameraManager.StopPreview();
                
                // 释放资源
                cameraManager.Dispose();
                Console.WriteLine("摄像头管理器已释放");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"录像示例错误: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 拍照功能示例
        /// </summary>
        /// <returns>异步任务</returns>
        public static async Task SnapshotExample()
        {
            try
            {
                var cameraManager = new CameraManager();
                
                Console.WriteLine("正在初始化摄像头...");
                
                // 初始化摄像头
                bool initialized = await cameraManager.InitializeCaptureAsync(640, 480);
                if (!initialized)
                {
                    Console.WriteLine("摄像头初始化失败");
                    return;
                }
                
                // 开始预览
                cameraManager.StartPreview();
                Console.WriteLine("预览已开始");
                
                // 等待摄像头稳定
                await Task.Delay(2000);
                
                // 拍照
                string snapshotPath = $"snapshot_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                bool snapshotTaken = cameraManager.TakeSnapshot(snapshotPath);
                
                if (snapshotTaken)
                {
                    Console.WriteLine($"拍照成功，保存到: {snapshotPath}");
                }
                else
                {
                    Console.WriteLine("拍照失败");
                }
                
                // 停止预览
                cameraManager.StopPreview();
                
                // 释放资源
                cameraManager.Dispose();
                Console.WriteLine("摄像头管理器已释放");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"拍照示例错误: {ex.Message}");
            }
        }
    }
}