using System;
using System.IO;
using Tools.Extend;

namespace Tools.App
{
    public class AppHospital
    {
        /// <summary>
        /// 医院名称
        /// </summary>
        public string HospitalName { get; set; }

        /// <summary>
        /// 医院名称
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// 默认医生名称
        /// </summary>
        public string DcName { get; set; } = "化验室";

        /// <summary>
        /// 联系人
        /// </summary>
        public string HospitalContacts { get; set; }


        /// <summary>
        /// 电话
        /// </summary>
        public string HospitalPhone { get; set; }

        /// <summary>
        /// LOGO
        /// </summary>
        public byte[] HospitalLogo { get; set; }

        /// <summary>
        /// 配置路径
        /// </summary>
        private static readonly string ConfigPath;
        /// <summary>
        /// 配置路径全路径
        /// </summary>
        private static readonly string ConfigFullPath;




        static AppHospital()
        {
            ConfigPath = AppDomain.CurrentDomain.BaseDirectory + "Config\\";
            if (!Directory.Exists(ConfigPath))
                Directory.CreateDirectory(ConfigPath);
            ConfigFullPath = Path.Combine(ConfigPath, "appHospitalConfig.xml");
        }
         

        public static AppHospital GetConfig()
        {
            try
            {
                if (File.Exists(ConfigFullPath))
                    return ObjectExtension.GetObjectByXml<AppHospital>(ConfigFullPath);
                else
                {
                    return new AppHospital();
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
