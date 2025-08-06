using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using System.Drawing;
using Entity.Entity;
using Tools.App;
using Tools.Extend;

namespace WpfMain.Logic
{

    /// <summary>
    /// 检查结果操作
    /// </summary>
    public static class TestLogic
    {
        //public static string TempPath = "Temp";
        //public static string DataPath = "data";
        public static string JsonDataPath = "jsondata";
        private static List<TestInfo> infos;
        private static DateTime? stime;

        static TestLogic()
        {
            //如果全局已配置data路径 改为配置路径
            //DataPath = AppStatic.VideoConfig.VideoPath;

            //TempPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, TempPath);
            //DataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DataPath);
            var tmp = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, JsonDataPath);
            infos = new List<TestInfo>();

            if (!Directory.Exists(tmp))
                Directory.CreateDirectory(tmp);
            //if (!Directory.Exists(DataPath))
            //    Directory.CreateDirectory(TempPath);
        }

        public static bool Save(TestInfo tInfo)
        {
            string name = $"{tInfo.TestDate:yyyyMMddHHmmss}_{tInfo.Id}";
            string jsonfileName = Path.Combine(JsonDataPath, $"{name}.json");

            //将检查数据由内存或临时目录保存到结果目录
            {
                if (!Directory.Exists(AppStatic.VideoConfig.VideoPath))
                    Directory.CreateDirectory(AppStatic.VideoConfig.VideoPath);
                string dname = Path.Combine(AppStatic.VideoConfig.VideoPath, name);
                if (!Directory.Exists(dname))
                    Directory.CreateDirectory(dname);

                tInfo.Result?.Images?.ForEach(x =>
                {
                    new Bitmap(x.BitBuffer.Byte2Bitmap()).Save(Path.Combine(dname, x.Name));
                    x.Source = Path.Combine(dname, x.Name);
                    x.Type = MediaSourceType.LocalPath;
                    x.Name = x.Name;
                });
                tInfo.Result?.Vedios?.ForEach(x => File.Copy(Path.Combine(AppVideoConfig.TempPath, x.Name), Path.Combine(dname, x.Name)));
            }

            File.WriteAllText(jsonfileName, JsonConvert.SerializeObject(tInfo));
            //添加缓存
            if (!infos.Any(o => o.Id == tInfo.Id))
                infos.Add(tInfo);
            return true;
        }

        public static List<TestInfo> Load(DateTime startTime, DateTime endTime)
        {
            if (infos.Count > 0)
                return infos;
            FillTest(startTime);
            return infos.Where(o => o.TestDate >= startTime && o.TestDate <= endTime).ToList();
        }

        public static void Delete(TestInfo info)
        {
            infos.Remove(info);
        }


        /// <summary>
        /// 读取数据并缓存，所有json数据只读取一次
        /// </summary>
        /// <param name="startTime"></param>
        private static void FillTest(DateTime startTime)
        {
            if (stime != null && stime < startTime)
                return;
            stime = startTime;

            string[] files = Directory.GetFiles(JsonDataPath, "*.json");
            foreach (string file in files)
            {
                string[] names = file.Replace(JsonDataPath + "\\", "").Split('_');
                if (DateTime.ParseExact(names[0], "yyyyMMddHHmmss", null) < startTime)
                    continue;
                if (infos.Any(o => o.Id == names[1]))
                    continue;
                if (!string.IsNullOrWhiteSpace(File.ReadAllText(file)))
                    infos.Add(JsonConvert.DeserializeObject<TestInfo>(File.ReadAllText(file)));
            }

        }

        private static List<AppTemp> _appTemps=null;

        public static List<AppTemp> AppTemps
        {
            get
            {
                if (_appTemps == null)
                {
                   return GetTemps();
                }

                return _appTemps;
            }
        }
        private static List<AppTemp> GetTemps()
        {
            List<AppTemp> tenmps = new List<AppTemp>();
            var path= AppDomain.CurrentDomain.BaseDirectory + "PrintTemp\\";
            string[] files = Directory.GetFiles(path, "*.docx");
            foreach (string file in files)
            {
                string[] names = file.Replace(path, "").Split('.');
                tenmps.Add(new AppTemp(){Name = names[0],Path = file.ToString()});
            }

            return tenmps;
        }
    }
}
