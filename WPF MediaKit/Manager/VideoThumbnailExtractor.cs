using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Size = System.Drawing.Size;

namespace WPFMediaKit.Manager
{
    /// <summary>
    /// 基于Windows API的视频缩略图提取器
    /// 使用Shell32和WIC接口获取视频文件缩略图
    /// </summary>
    public static class VideoThumbnailExtractor
    {
        #region Windows API 常量
        private const int S_OK = 0;
        private const uint SIIGBF_RESIZETOFIT = 0x00000001;
        private const uint SIIGBF_BIGGERSIZEOK = 0x00000002;
        private const uint SIIGBF_MEMORYONLY = 0x00000004;
        #endregion

        #region Windows API 结构体
        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE
        {
            public int cx;
            public int cy;

            public SIZE(int cx, int cy)
            {
                this.cx = cx;
                this.cy = cy;
            }
        }
        #endregion

        #region Windows API 接口定义
        [ComImport]
        [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            // 仅定义我们需要的方法
            [PreserveSig]
            int BindToHandler(IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid bhid,
                [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);
        }

        [ComImport]
        [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItemImageFactory
        {
            [PreserveSig]
            int GetImage(SIZE size, uint flags, out IntPtr phbm);
        }
        #endregion

        #region Windows API 函数导入
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            IntPtr pbc,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            out IShellItem ppv);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);
        #endregion

        /// <summary>
        /// 从视频文件获取缩略图
        /// </summary>
        /// <param name="videoPath">视频文件路径</param>
        /// <param name="width">缩略图宽度</param>
        /// <param name="height">缩略图高度</param>
        /// <param name="allowLarger">是否允许缩略图大于指定尺寸</param>
        /// <returns>缩略图图像源</returns>
        public static ImageSource GetVideoThumbnail(string videoPath, int width, int height, bool allowLarger = false)
        {
            if (string.IsNullOrEmpty(videoPath))
                throw new ArgumentNullException(nameof(videoPath));

            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException("宽度和高度必须为正数");

            IShellItem shellItem = null;
            IntPtr hBitmap = IntPtr.Zero;
            ImageSource imageSource = null;

            try
            {
                // 创建ShellItem
                Guid shellItemGuid = typeof(IShellItem).GUID;
                int hr = SHCreateItemFromParsingName(videoPath, IntPtr.Zero, shellItemGuid, out shellItem);
                if (hr != S_OK)
                    throw new System.ComponentModel.Win32Exception(hr);

                // 获取IShellItemImageFactory接口
                Guid imageFactoryGuid = typeof(IShellItemImageFactory).GUID;
                IntPtr imageFactoryPtr;
                hr = shellItem.BindToHandler(IntPtr.Zero, new Guid("3981e224-f559-11d3-8e3a-00c04f6837d5"),
                    imageFactoryGuid, out imageFactoryPtr);
                if (hr != S_OK)
                    throw new System.ComponentModel.Win32Exception(hr);

                IShellItemImageFactory imageFactory = (IShellItemImageFactory)Marshal.GetObjectForIUnknown(imageFactoryPtr);

                // 设置获取标志
                uint flags = SIIGBF_MEMORYONLY;
                if (!allowLarger)
                    flags |= SIIGBF_RESIZETOFIT;
                else
                    flags |= SIIGBF_BIGGERSIZEOK;

                // 获取缩略图
                SIZE size = new SIZE(width, height);
                hr = imageFactory.GetImage(size, flags, out hBitmap);
                Marshal.ReleaseComObject(imageFactory);
                if (hr != S_OK)
                    throw new System.ComponentModel.Win32Exception(hr);

                // 将HBITMAP转换为WPF可用的ImageSource
                imageSource = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

                // 冻结图像以提高性能
                if (imageSource.CanFreeze)
                    imageSource.Freeze();
            }
            finally
            {
                // 释放资源
                if (shellItem != null)
                    Marshal.ReleaseComObject(shellItem);

                if (hBitmap != IntPtr.Zero)
                    DeleteObject(hBitmap); // 必须释放GDI资源
            }

            return imageSource;
        }

        // 声明 IExtractImage 接口（兼容 XP）
        [ComImport]
        [Guid("BB2E617C-0920-11d1-9A0B-00C04FC2D6C1")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IExtractImage
        {
            [PreserveSig]
            int GetLocation(
                [Out, MarshalAs(UnmanagedType.LPWStr)] string pszPathBuffer,
                int cchMaxBuffer,
                out int pdwPriority,
                ref Size prgSize,
                int dwRecClrDepth,
                out int pdwFlags
            );

            [PreserveSig]
            int Extract(out IntPtr phBmpImage);
        }

        // 提取缩略图的方法（兼容 XP）
        public static Image ExtractThumbnailLegacy(string filePath, int width, int height)
        {
            if (!System.IO.File.Exists(filePath))
                throw new System.IO.FileNotFoundException("文件不存在", filePath);

            // 创建 Shell 项
            Guid iidIExtractImage = typeof(IExtractImage).GUID;
            SHCreateItemFromParsingName(filePath, IntPtr.Zero, typeof(IExtractImage).GUID, out object extractImageObj);
         

            IExtractImage extractImage = (IExtractImage)extractImageObj;
            Size size = new Size(width, height);
            int flags = 0;
            int priority = 0;
            int colorDepth = 32;

            // 获取图像位置信息
           int hr = extractImage.GetLocation(null, 0, out priority, ref size, colorDepth, out flags);
            if (hr != 0 && hr != 0x80070002) // 忽略“文件未找到”的临时错误
                Marshal.ThrowExceptionForHR(hr);

            // 提取图像
            hr = extractImage.Extract(out IntPtr hBitmap);
            if (hr != 0)
                Marshal.ThrowExceptionForHR(hr);

            // 将句柄转换为 Bitmap
            Image image = Image.FromHbitmap(hBitmap);
            DeleteObject(hBitmap); // 释放非托管资源
            return image;
        }

        // 导入 Shell32 函数
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string path,
            IntPtr pbc,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out object ppv
        ); 
    }


}
