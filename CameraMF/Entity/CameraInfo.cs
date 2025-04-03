using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CameraMF.Entity
{
    public class CameraInfo
    {
        /// <summary>
        /// 设备友好名称（如"USB Camera"）
        /// </summary>
        public string FriendlyName { get; set; }

        /// <summary>
        /// 设备符号链接（用于后续初始化）
        /// </summary>
        public string SymbolicLink { get; set; }

        /// <summary>
        /// 设备支持的视频格式列表 
        /// </summary>
        public List<string> MediaTypes { get; set; } = new List<string>();
    }
}
