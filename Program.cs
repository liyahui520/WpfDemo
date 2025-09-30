using System;
using OpenCvSharp;
using System.Threading;

namespace CameraColorTest
{
    /// <summary>
    /// 相机颜色测试程序
    /// 用于检测相机输出的图像格式和颜色信息
    /// </summary>
    class Program
    {
        /// <summary>
        /// 程序入口点
        /// </summary>
        /// <param name="args">命令行参数</param>
        static void Main(string[] args)
        {
            Console.WriteLine("=== 相机颜色测试程序 ===");
            Console.WriteLine("正在初始化相机...");

            VideoCapture camera = null;
            try
            {
                // 初始化相机
                camera = new VideoCapture(0);
                if (!camera.IsOpened())
                {
                    Console.WriteLine("错误：无法打开相机设备0");
                    return;
                }

                // 设置相机参数
                camera.Set(VideoCaptureProperties.FrameWidth, 640);
                camera.Set(VideoCaptureProperties.FrameHeight, 480);
                camera.Set(VideoCaptureProperties.Saturation, 1.0); // 最大饱和度
                camera.Set(VideoCaptureProperties.Contrast, 0.6);   // 提高对比度
                camera.Set(VideoCaptureProperties.Brightness, 0.5); // 默认亮度

                Console.WriteLine("相机初始化成功");
                Console.WriteLine("开始捕获图像...");

                Mat frame = new Mat();
                for (int i = 0; i < 10; i++)
                {
                    // 捕获帧
                    if (camera.Read(frame) && !frame.Empty())
                    {
                        // 输出图像信息
                        Console.WriteLine($"帧 {i + 1}:");
                        Console.WriteLine($"  尺寸: {frame.Width} x {frame.Height}");
                        Console.WriteLine($"  通道数: {frame.Channels()}");
                        Console.WriteLine($"  类型: {frame.Type()}");
                        Console.WriteLine($"  深度: {frame.Depth()}");

                        // 分析颜色信息
                        AnalyzeColorInfo(frame, i + 1);

                        // 保存测试图像
                        string filename = $"test_frame_{i + 1}.jpg";
                        Cv2.ImWrite(filename, frame);
                        Console.WriteLine($"  已保存: {filename}");
                    }
                    else
                    {
                        Console.WriteLine($"帧 {i + 1}: 捕获失败");
                    }

                    Thread.Sleep(500); // 等待500ms
                }

                Console.WriteLine("测试完成！");
                Console.WriteLine("请检查保存的图像文件以验证颜色显示。");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: {ex.Message}");
            }
            finally
            {
                camera?.Release();
                camera?.Dispose();
            }

            Console.WriteLine("按任意键退出...");
            Console.ReadKey();
        }

        /// <summary>
        /// 分析图像的颜色信息
        /// </summary>
        /// <param name="frame">输入图像</param>
        /// <param name="frameNumber">帧编号</param>
        private static void AnalyzeColorInfo(Mat frame, int frameNumber)
        {
            try
            {
                if (frame.Channels() == 1)
                {
                    // 灰度图像
                    Scalar meanValue = Cv2.Mean(frame);
                    Console.WriteLine($"  图像类型: 灰度图像");
                    Console.WriteLine($"  平均亮度: {meanValue.Val0:F2}");
                }
                else if (frame.Channels() == 3)
                {
                    // 彩色图像 (BGR)
                    Scalar meanValue = Cv2.Mean(frame);
                    Console.WriteLine($"  图像类型: 彩色图像 (BGR)");
                    Console.WriteLine($"  平均B值: {meanValue.Val0:F2}");
                    Console.WriteLine($"  平均G值: {meanValue.Val1:F2}");
                    Console.WriteLine($"  平均R值: {meanValue.Val2:F2}");

                    // 检查是否实际为灰度图像（RGB值相近）
                    double colorDiff = Math.Max(Math.Abs(meanValue.Val0 - meanValue.Val1), 
                                               Math.Abs(meanValue.Val1 - meanValue.Val2));
                    if (colorDiff < 5.0)
                    {
                        Console.WriteLine($"  警告: 虽然是3通道图像，但RGB值相近，可能是灰度图像");
                    }
                    else
                    {
                        Console.WriteLine($"  确认: 图像包含真实的颜色信息");
                    }
                }
                else
                {
                    Console.WriteLine($"  图像类型: {frame.Channels()}通道图像");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  分析颜色信息失败: {ex.Message}");
            }
        }
    }
}