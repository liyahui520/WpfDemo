using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using WpfMain.Extend;

namespace WpfMain
{
    /// <summary>
    /// 视频配置类
    /// </summary>
    public class AppVideoConfig
    {
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
            ConfigPath = AppDomain.CurrentDomain.BaseDirectory + "Config\\";
            if (!Directory.Exists(ConfigPath))
                Directory.CreateDirectory(ConfigPath);
            ConfigFullPath = Path.Combine(ConfigPath, "appVideoConfig.xml");
        }

        /// <summary>
        /// 视频保存路径
        /// </summary>
        public string VideoPath { get; set; }

        /// <summary>
        /// 拍照保存路径
        /// </summary>
        public string ImagePath { get; set; }

        public static AppVideoConfig GetConfig()
        {
            try
            {
                if (File.Exists(ConfigFullPath))
                    return ObjectExtension.GetObjectByXml<AppVideoConfig>(ConfigFullPath);
                else
                {
                    return new AppVideoConfig();
                }
            }
            catch (Exception e)
            {
                throw;
            }
        }

        public void Save()
        {
            this.ObjectSaveToXml(ConfigFullPath);
        }
    }
}
