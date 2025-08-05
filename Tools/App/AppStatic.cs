using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;
using Entity.Entity;

namespace Tools.App
{
    public static class AppStatic
    {
        public static AppVideoConfig VideoConfig { get; set; }

        public static AppHospital AppHospital { get; set; }

        public static PetInfo PetInfo { get; set; }
         
        public static Window MainWindow { get; set; }

        /// <summary>
        /// 分辨率列表
        /// </summary>
        public static Resolution Resolution { get; set; }
    }
}
