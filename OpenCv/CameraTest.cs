using System;
using System.Collections.Generic;
using OpenCvSharp;

namespace OpenCv
{
    /// <summary>
    /// 摄像头测试工具类
    /// 用于验证OpenCV摄像头访问权限和设备枚举功能
    /// </summary>
    public static class CameraTest
    {
        /// <summary>
        /// 测试摄像头设备访问权限
        /// </summary>
        /// <returns>测试结果信息</returns>
        public static List<string> TestCameraAccess()
        {
            var results = new List<string>();
            results.Add("=== OpenCV 摄像头访问测试 ===");
            
            try
            {
                // 测试OpenCV版本
                results.Add($"OpenCV 版本: {Cv2.GetVersionString()}");
                
                // 测试设备枚举
                results.Add("开始枚举摄像头设备...");
                
                int deviceCount = 0;
                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        results.Add($"测试设备索引 {i}...");
                        
                        using (var capture = new VideoCapture(i))
                        {
                            if (capture.IsOpened())
                            {
                                deviceCount++;
                                
                                // 获取设备信息
                                var width = capture.Get(VideoCaptureProperties.FrameWidth);
                                var height = capture.Get(VideoCaptureProperties.FrameHeight);
                                var fps = capture.Get(VideoCaptureProperties.Fps);
                                var backend = capture.GetBackendName();
                                
                                results.Add($"✓ 设备 {i}: 成功打开");
                                results.Add($"  - 分辨率: {width}x{height}");
                                results.Add($"  - 帧率: {fps}");
                                results.Add($"  - 后端: {backend}");
                                
                                // 尝试读取一帧
                                using (var frame = new Mat())
                                {
                                    if (capture.Read(frame) && !frame.Empty())
                                    {
                                        results.Add($"  - 帧读取: 成功 ({frame.Width}x{frame.Height})");
                                    }
                                    else
                                    {
                                        results.Add($"  - 帧读取: 失败");
                                    }
                                }
                            }
                            else
                            {
                                results.Add($"✗ 设备 {i}: 无法打开");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        results.Add($"✗ 设备 {i}: 异常 - {ex.Message}");
                    }
                }
                
                results.Add($"设备枚举完成，共发现 {deviceCount} 个可用设备");
                
                // 测试系统权限
                results.Add("检查系统权限...");
                try
                {
                    // 尝试创建一个临时的VideoCapture来测试权限
                    using (var testCapture = new VideoCapture(0))
                    {
                        if (testCapture.IsOpened())
                        {
                            results.Add("✓ 系统摄像头权限: 正常");
                        }
                        else
                        {
                            results.Add("✗ 系统摄像头权限: 可能被拒绝");
                        }
                    }
                }
                catch (Exception ex)
                {
                    results.Add($"✗ 系统摄像头权限测试异常: {ex.Message}");
                }
                
            }
            catch (Exception ex)
            {
                results.Add($"测试过程发生异常: {ex.Message}");
                results.Add($"异常详情: {ex.StackTrace}");
            }
            
            results.Add("=== 测试完成 ===");
            return results;
        }
        
        /// <summary>
        /// 获取可用的摄像头后端列表
        /// </summary>
        /// <returns>后端信息列表</returns>
        private static List<string> GetAvailableBackends()
        {
            var backends = new List<string>
            {
                "支持的后端类型:",
                "- DirectShow (Windows)",
                "- MediaFoundation (Windows)",
                "- V4L2 (Linux)",
                "- AVFoundation (macOS)"
            };
            
            return backends;
        }
    }
}