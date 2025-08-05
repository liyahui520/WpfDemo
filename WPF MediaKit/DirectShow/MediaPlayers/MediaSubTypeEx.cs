using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WPFMediaKit.DirectShow.MediaPlayers
{
    /// <summary>
    /// 媒体子类型常量定义（补充缺少的 MP4 相关编码类型）
    /// </summary>
    public static class MediaSubTypeEx
    {
        // H.264 编码（MP4 中最常用的视频编码）
        public static readonly Guid H264 = new Guid("34363248-0000-0010-8000-00AA00389B71");

        // MPEG-4 Part 2 编码（早期 MP4 视频编码）
        public static readonly Guid MPEG4 = new Guid("3450454D-0000-0010-8000-00AA00389B71");
    }
}
