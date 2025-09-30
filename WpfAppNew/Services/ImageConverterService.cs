using System;
using System.Windows.Media.Imaging;
using OpenCvSharp;

namespace WpfAppNew.Services
{
    /// <summary>
    /// 图像转换服务实现
    /// </summary>
    public class ImageConverterService : IImageConverterService
    {
        /// <summary>
        /// 将Mat转换为BitmapImage
        /// </summary>
        /// <param name="mat">输入的Mat对象</param>
        /// <returns>转换后的BitmapImage</returns>
        public BitmapImage MatToBitmapImage(Mat mat)
        {
            if (mat == null || mat.Empty())
                throw new ArgumentException("输入的Mat对象为空或无效");

            Mat bgrMat = null;
            try
            {
                // 转换为BGR格式
                if (mat.Channels() == 1)
                {
                    bgrMat = new Mat();
                    Cv2.CvtColor(mat, bgrMat, ColorConversionCodes.GRAY2BGR);
                }
                else
                {
                    // 对于多通道图像，克隆以避免在转换过程中出现问题
                    bgrMat = mat.Clone();
                }

                // 编码为JPEG
                var bytes = bgrMat.ToBytes(".jpg");

                // 创建BitmapImage
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = new System.IO.MemoryStream(bytes);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                return bitmap;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("图像转换失败", ex);
            }
            finally
            {
                // 清理资源
                bgrMat?.Dispose();
            }
        }
    }
}