using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Documents;
using Tools.Extend;

namespace Tools.App
{
    /// <summary>
    /// 视频配置类
    /// </summary>
    public class AppVideoConfig
    {

        /// <summary>
        /// 系统临时文件路径
        /// </summary>
        public static string TempPath { get; set; } = AppDomain.CurrentDomain.BaseDirectory + "Temp\\";

        /// <summary>
        /// 设备信息
        /// </summary>
        public string VideoDecive { get; set; }

        /// <summary>
        /// 配置路径
        /// </summary>
        private static readonly string ConfigPath;
        /// <summary>
        /// 配置路径全路径
        /// </summary>
        private static readonly string ConfigFullPath;

        static AppVideoConfig()
        {
            if (!Directory.Exists(TempPath))
                Directory.CreateDirectory(TempPath);

            ConfigPath = AppDomain.CurrentDomain.BaseDirectory + "Config\\";
            if (!Directory.Exists(ConfigPath))
                Directory.CreateDirectory(ConfigPath);
            ConfigFullPath = Path.Combine(ConfigPath, "appVideoConfig.xml");
        }

        /// <summary>
        /// 视频保存路径
        /// </summary>
        public string VideoPath { get; set; } = AppDomain.CurrentDomain.BaseDirectory + "VideoFile\\";

        /// <summary>
        /// 视频类型
        /// </summary>
        public string VideoType { get; set; } = "avi";

        /// <summary>
        /// 拍照保存路径
        /// </summary>
        public string ImagePath { get; set; } = AppDomain.CurrentDomain.BaseDirectory + "VideoImgFile\\";

        /// <summary>
        /// 图片类型
        /// </summary>
        public string ImageType { get; set; } = "png";
         
        public static AppVideoConfig GetConfig()
        {
            if (File.Exists(ConfigFullPath))
                return ObjectExtension.GetObjectByXml<AppVideoConfig>(ConfigFullPath);
            return new AppVideoConfig();
        }

        public void Save()
        {
            this.ObjectSaveToXml(ConfigFullPath);
        }



    }

    public class Resolution
    {
        /// <summary>
        /// 分辨率名称
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// 默认分辨率
        /// </summary>
        public bool IsDefault { get; set; }

        /// <summary>
        /// 分辨率宽度
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// 分辨率高度
        /// </summary>
        public int Height { get; set; }
    }
}
