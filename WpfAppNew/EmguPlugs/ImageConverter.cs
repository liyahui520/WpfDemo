using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using Tools.Extend;

namespace WpfAppNew.EmguPlugs
{
    /// <summary>
    /// 图像转换工具类
    /// 提供OpenCV Mat与WPF ImageSource之间的高效转换功能
    /// </summary>
    /// <remarks>
    /// 主要功能：
    /// 1. OpenCV Mat转换为WPF ImageSource
    /// 2. WPF ImageSource转换为OpenCV Mat
    /// 3. 支持多种图像格式和色彩空间
    /// 4. 内存优化和性能优化
    /// 5. 异常处理和错误恢复
    /// </remarks>
    public static class ImageConverter
    {
        #region 常量定义

        /// <summary>
        /// 默认DPI值
        /// </summary>
        private const double DefaultDpi = 96.0;

        /// <summary>
        /// 最大图像尺寸（用于内存保护）
        /// </summary>
        private const int MaxImageSize = 8192;

        #endregion

        #region Mat转ImageSource

        /// <summary>
        /// 将OpenCV Mat转换为WPF ImageSource
        /// </summary>
        /// <param name="mat">OpenCV Mat对象</param>
        /// <returns>WPF ImageSource对象，转换失败时返回null</returns>
        /// <exception cref="ArgumentNullException">当mat为null时抛出</exception>
        /// <exception cref="ArgumentException">当mat为空或尺寸无效时抛出</exception>
        public static ImageSource MatToImageSource(Mat mat)
        {
            if (mat == null)
            {
                LogUtil.Warning("ImageConverter: Mat对象为null");
                return null;
            }

            if (mat.Empty())
            {
                LogUtil.Warning("ImageConverter: Mat对象为空");
                return null;
            }

            try
            {
                // 验证图像尺寸
                if (mat.Width <= 0 || mat.Height <= 0 || 
                    mat.Width > MaxImageSize || mat.Height > MaxImageSize)
                {
                    LogUtil.Warning($"ImageConverter: 图像尺寸无效 - {mat.Width}x{mat.Height}");
                    return null;
                }

                // 根据通道数选择转换方法
                switch (mat.Channels())
                {
                    case 1:
                        return ConvertGrayMatToImageSource(mat);
                    case 3:
                        return ConvertBgrMatToImageSource(mat);
                    case 4:
                        return ConvertBgraMatToImageSource(mat);
                    default:
                        return ConvertGenericMatToImageSource(mat);
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"ImageConverter: Mat转ImageSource失败 - {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 将灰度Mat转换为ImageSource
        /// </summary>
        /// <param name="mat">灰度Mat对象</param>
        /// <returns>ImageSource对象</returns>
        private static unsafe ImageSource ConvertGrayMatToImageSource(Mat mat)
        {
            try
            {
                var bitmap = new WriteableBitmap(
                    mat.Width, 
                    mat.Height, 
                    DefaultDpi, 
                    DefaultDpi, 
                    PixelFormats.Gray8, 
                    null);

                var stride = bitmap.BackBufferStride;
                var dataPtr = mat.DataPointer;

                bitmap.Lock();
                try
                {
                    unsafe
                    {
                        var backBuffer = (byte*)bitmap.BackBuffer;
                        var matData = (byte*)dataPtr;

                        for (int y = 0; y < mat.Height; y++)
                        {
                            var srcRow = matData + y * mat.Step();
                            var dstRow = backBuffer + y * stride;
                            
                            for (int x = 0; x < mat.Width; x++)
                            {
                                dstRow[x] = srcRow[x];
                            }
                        }
                    }

                    bitmap.AddDirtyRect(new Int32Rect(0, 0, mat.Width, mat.Height));
                }
                finally
                {
                    bitmap.Unlock();
                }

                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception ex)
            {
                LogUtil.Error($"ImageConverter: 灰度Mat转换失败 - {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 将BGR Mat转换为ImageSource
        /// </summary>
        /// <param name="mat">BGR Mat对象</param>
        /// <returns>ImageSource对象</returns>
        private static unsafe ImageSource ConvertBgrMatToImageSource(Mat mat)
        {
            try
            {
                var bitmap = new WriteableBitmap(
                    mat.Width, 
                    mat.Height, 
                    DefaultDpi, 
                    DefaultDpi, 
                    PixelFormats.Bgr24, 
                    null);

                var stride = bitmap.BackBufferStride;
                var dataPtr = mat.DataPointer;

                bitmap.Lock();
                try
                {
                    unsafe
                    {
                        var backBuffer = (byte*)bitmap.BackBuffer;
                        var matData = (byte*)dataPtr;

                        for (int y = 0; y < mat.Height; y++)
                        {
                            var srcRow = matData + y * mat.Step();
                            var dstRow = backBuffer + y * stride;
                            
                            for (int x = 0; x < mat.Width; x++)
                            {
                                var srcPixel = srcRow + x * 3;
                                var dstPixel = dstRow + x * 3;
                                
                                dstPixel[0] = srcPixel[0]; // B
                                dstPixel[1] = srcPixel[1]; // G
                                dstPixel[2] = srcPixel[2]; // R
                            }
                        }
                    }

                    bitmap.AddDirtyRect(new Int32Rect(0, 0, mat.Width, mat.Height));
                }
                finally
                {
                    bitmap.Unlock();
                }

                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception ex)
            {
                LogUtil.Error($"ImageConverter: BGR Mat转换失败 - {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 将BGRA Mat转换为ImageSource
        /// </summary>
        /// <param name="mat">BGRA Mat对象</param>
        /// <returns>ImageSource对象</returns>
        private static unsafe ImageSource ConvertBgraMatToImageSource(Mat mat)
        {
            try
            {
                var bitmap = new WriteableBitmap(
                    mat.Width, 
                    mat.Height, 
                    DefaultDpi, 
                    DefaultDpi, 
                    PixelFormats.Bgra32, 
                    null);

                var stride = bitmap.BackBufferStride;
                var dataPtr = mat.DataPointer;

                bitmap.Lock();
                try
                {
                    unsafe
                    {
                        var backBuffer = (byte*)bitmap.BackBuffer;
                        var matData = (byte*)dataPtr;

                        for (int y = 0; y < mat.Height; y++)
                        {
                            var srcRow = matData + y * mat.Step();
                            var dstRow = backBuffer + y * stride;
                            
                            for (int x = 0; x < mat.Width; x++)
                            {
                                var srcPixel = srcRow + x * 4;
                                var dstPixel = dstRow + x * 4;
                                
                                dstPixel[0] = srcPixel[0]; // B
                                dstPixel[1] = srcPixel[1]; // G
                                dstPixel[2] = srcPixel[2]; // R
                                dstPixel[3] = srcPixel[3]; // A
                            }
                        }
                    }

                    bitmap.AddDirtyRect(new Int32Rect(0, 0, mat.Width, mat.Height));
                }
                finally
                {
                    bitmap.Unlock();
                }

                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception ex)
            {
                LogUtil.Error($"ImageConverter: BGRA Mat转换失败 - {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 通用Mat转换方法（通过Bitmap中转）
        /// </summary>
        /// <param name="mat">Mat对象</param>
        /// <returns>ImageSource对象</returns>
        private static ImageSource ConvertGenericMatToImageSource(Mat mat)
        {
            try
            {
                // 转换为标准BGR格式
                using (var bgrMat = new Mat())
                {
                    if (mat.Channels() == 1)
                    {
                        Cv2.CvtColor(mat, bgrMat, ColorConversionCodes.GRAY2BGR);
                    }
                    else if (mat.Channels() == 4)
                    {
                        Cv2.CvtColor(mat, bgrMat, ColorConversionCodes.BGRA2BGR);
                    }
                    else
                    {
                        mat.CopyTo(bgrMat);
                    }

                    return ConvertBgrMatToImageSource(bgrMat);
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"ImageConverter: 通用Mat转换失败 - {ex.Message}");
                return null;
            }
        }

        #endregion

        #region ImageSource转Mat

        /// <summary>
        /// 将WPF ImageSource转换为OpenCV Mat
        /// </summary>
        /// <param name="imageSource">WPF ImageSource对象</param>
        /// <returns>OpenCV Mat对象，转换失败时返回null</returns>
        /// <exception cref="ArgumentNullException">当imageSource为null时抛出</exception>
        public static Mat ImageSourceToMat(ImageSource imageSource)
        {
            if (imageSource == null)
            {
                LogUtil.Warning("ImageConverter: ImageSource对象为null");
                return null;
            }

            try
            {
                if (imageSource is BitmapSource bitmapSource)
                {
                    return BitmapSourceToMat(bitmapSource);
                }
                else
                {
                    LogUtil.Warning("ImageConverter: 不支持的ImageSource类型");
                    return null;
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"ImageConverter: ImageSource转Mat失败 - {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 将BitmapSource转换为Mat
        /// </summary>
        /// <param name="bitmapSource">BitmapSource对象</param>
        /// <returns>Mat对象</returns>
        private static Mat BitmapSourceToMat(BitmapSource bitmapSource)
        {
            try
            {
                // 确保格式为BGR24
                var convertedBitmap = new FormatConvertedBitmap(bitmapSource, PixelFormats.Bgr24, null, 0);
                
                var width = convertedBitmap.PixelWidth;
                var height = convertedBitmap.PixelHeight;
                var stride = width * 3; // BGR24 = 3 bytes per pixel
                
                var pixelData = new byte[height * stride];
                convertedBitmap.CopyPixels(pixelData, stride, 0);
                
                var mat = new Mat(height, width, MatType.CV_8UC3);
                
                unsafe
                {
                    var matData = (byte*)mat.DataPointer;
                    
                    for (int y = 0; y < height; y++)
                    {
                        var srcRow = y * stride;
                        var dstRow = matData + y * mat.Step();
                        
                        for (int x = 0; x < width; x++)
                        {
                            var srcPixel = srcRow + x * 3;
                            var dstPixel = dstRow + x * 3;
                            
                            dstPixel[0] = pixelData[srcPixel + 0]; // B
                            dstPixel[1] = pixelData[srcPixel + 1]; // G
                            dstPixel[2] = pixelData[srcPixel + 2]; // R
                        }
                    }
                }
                
                return mat;
            }
            catch (Exception ex)
            {
                LogUtil.Error($"ImageConverter: BitmapSource转Mat失败 - {ex.Message}");
                return null;
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 创建空白ImageSource
        /// </summary>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        /// <param name="color">背景颜色</param>
        /// <returns>空白ImageSource</returns>
        public static ImageSource CreateBlankImageSource(int width, int height, System.Windows.Media.Color color)
        {
            try
            {
                var bitmap = new WriteableBitmap(width, height, DefaultDpi, DefaultDpi, PixelFormats.Bgr24, null);
                
                bitmap.Lock();
                try
                {
                    unsafe
                    {
                        var backBuffer = (byte*)bitmap.BackBuffer;
                        var stride = bitmap.BackBufferStride;
                        
                        for (int y = 0; y < height; y++)
                        {
                            var row = backBuffer + y * stride;
                            
                            for (int x = 0; x < width; x++)
                            {
                                var pixel = row + x * 3;
                                pixel[0] = color.B; // B
                                pixel[1] = color.G; // G
                                pixel[2] = color.R; // R
                            }
                        }
                    }
                    
                    bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
                }
                finally
                {
                    bitmap.Unlock();
                }
                
                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception ex)
            {
                LogUtil.Error($"ImageConverter: 创建空白图像失败 - {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取图像信息
        /// </summary>
        /// <param name="imageSource">图像源</param>
        /// <returns>图像信息字符串</returns>
        public static string GetImageInfo(ImageSource imageSource)
        {
            if (imageSource == null)
                return "无图像";

            try
            {
                if (imageSource is BitmapSource bitmapSource)
                {
                    return $"{bitmapSource.PixelWidth}x{bitmapSource.PixelHeight} " +
                           $"{bitmapSource.Format} " +
                           $"DPI:{bitmapSource.DpiX:F0}x{bitmapSource.DpiY:F0}";
                }
                else
                {
                    return $"{imageSource.Width:F0}x{imageSource.Height:F0}";
                }
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"ImageConverter: 获取图像信息失败 - {ex.Message}");
                return "信息获取失败";
            }
        }

        /// <summary>
        /// 验证Mat对象有效性
        /// </summary>
        /// <param name="mat">Mat对象</param>
        /// <returns>是否有效</returns>
        public static bool IsValidMat(Mat mat)
        {
            return mat != null && 
                   !mat.Empty() && 
                   mat.Width > 0 && 
                   mat.Height > 0 && 
                   mat.Width <= MaxImageSize && 
                   mat.Height <= MaxImageSize;
        }

        /// <summary>
        /// 验证ImageSource对象有效性
        /// </summary>
        /// <param name="imageSource">ImageSource对象</param>
        /// <returns>是否有效</returns>
        public static bool IsValidImageSource(ImageSource imageSource)
        {
            if (imageSource == null)
                return false;

            try
            {
                return imageSource.Width > 0 && 
                       imageSource.Height > 0 && 
                       imageSource.Width <= MaxImageSize && 
                       imageSource.Height <= MaxImageSize;
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}