using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tools.App;
using Tools.Extend;

namespace Entity.Entity
{
    public class PetInfo
    {
        public List<PetType> PetTypes { get; set; } = new List<PetType>();

        /// <summary>
        /// 配置路径
        /// </summary>
        private static readonly string ConfigPath;
        /// <summary>
        /// 配置路径全路径
        /// </summary>
        private static readonly string ConfigFullPath;
        static PetInfo()
        {
            ConfigPath = AppDomain.CurrentDomain.BaseDirectory + "Config\\";
            if (!Directory.Exists(ConfigPath))
                Directory.CreateDirectory(ConfigPath);
            ConfigFullPath = Path.Combine(ConfigPath, "PetConfig.xml");
        }


        public static PetInfo GetConfig()
        {
            try
            {
                if (File.Exists(ConfigFullPath))
                    return ObjectExtension.GetObjectByXml<PetInfo>(ConfigFullPath);
                else
                { 
                    return new PetInfo();
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        public void Save()
        {
            this.ObjectSaveToXml(ConfigFullPath);
        }
    }

    public class PetType
    {
        public string Name { get; set; }

        public List<PetVariety> PetVarietys { get; set; } = new List<PetVariety>();

    }

    public class PetVariety
    {
        public string Name { get; set; }
    }
}
