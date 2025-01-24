using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WpfMain.Entity;
using System.IO;
using Newtonsoft.Json;

namespace WpfMain.Logic
{

    /// <summary>
    /// 检查结果操作
    /// </summary>
    public static class TestLogic
    {
        private static string TempPath = "Temp";
        private static string DataPath = "data";
        private static List<TestInfo> infos;
        private static DateTime? stime;

        static TestLogic()
        {
            TempPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, TempPath);
            DataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DataPath);
            infos = new List<TestInfo>();

            if (!Directory.Exists(TempPath))
                Directory.CreateDirectory(TempPath);
            if (!Directory.Exists(DataPath))
                Directory.CreateDirectory(TempPath);
        }

        public static bool Save(TestInfo tInfo)
        {
            string fileName = Path.Combine(DataPath, $"{tInfo.TestDate:yyyyMMddHHmmss}|{tInfo.Id}.json");
            File.WriteAllText(fileName, JsonConvert.SerializeObject(tInfo));

            //将检查数据由内存或临时目录保存到结果目录
            { 
            
            }

            //添加缓存
            if (!infos.Any(o => o.Id == tInfo.Id))
                infos.Add(tInfo);
            return true;
        }

        public static List<TestInfo> Load(DateTime startTime, DateTime endTime)
        {
            FillTest(startTime);
            return infos.Where(o=>o.TestDate>=startTime && o.TestDate<=endTime).ToList();
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

            string[] files = Directory.GetFiles(DataPath, "*.json");
            foreach (string file in files)
            {
                string[] names = file.Split('|');
                if (DateTime.ParseExact(names[0], "yyyyMMddHHmmss", null) < startTime)
                    continue;
                if (infos.Any(o => o.Id == names[1]))
                    continue;
                infos.Add(JsonConvert.DeserializeObject<TestInfo>(File.ReadAllText(file)));
            }

        }
    }
}
