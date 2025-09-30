using System;
using System.Windows.Media.Imaging;
using OpenCvSharp;

namespace WpfAppNew.Services
{
    /// <summary>
    /// 图像转换服务接口
    /// </summary>
    public interface IImageConverterService
    {
        /// <summary>
        /// 将Mat转换为BitmapImage
        /// </summary>
        /// <param name="mat">输入的Mat对象</param>
        /// <returns>转换后的BitmapImage</returns>
        BitmapImage MatToBitmapImage(Mat mat);
    }
}