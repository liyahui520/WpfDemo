using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using System.Drawing;
using Entity.Entity;
using Tools.App;
using Tools.Extend;

namespace WpfAppNew.Logic
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

        private static void DeleteFiles(string targetDirectory)
        {
            if (!Directory.Exists(targetDirectory))
            {
                Console.WriteLine("目标文件夹不存在！");
                return;
            }

            try
            {
                // 获取所有文件（包括子文件夹）
                // SearchOption.AllDirectories 表示递归搜索子目录
                string[] files = Directory.GetFiles(targetDirectory, "*", SearchOption.AllDirectories);

                foreach (string file in files)
                {
                    File.Delete(file);
                    Console.WriteLine($"已删除文件：{file}");
                }

                Console.WriteLine("所有文件（包括子文件夹内）删除完成！");
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"权限不足：{ex.Message}");
            }
            catch (PathTooLongException ex)
            {
                Console.WriteLine($"路径过长：{ex.Message}");
            }
            catch (IOException ex)
            {
                Console.WriteLine($"文件操作错误：{ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发生错误：{ex.Message}");
            }
        }

        /// <summary>
        /// 安全的文件复制方法，带重试机制
        /// </summary>
        /// <param name="sourceFile">源文件路径</param>
        /// <param name="destFile">目标文件路径</param>
        /// <param name="overwrite">是否覆盖现有文件</param>
        /// <param name="maxRetries">最大重试次数</param>
        /// <param name="retryDelayMs">重试间隔毫秒数</param>
        /// <returns>复制是否成功</returns>
        private static bool SafeFileCopy(string sourceFile, string destFile, bool overwrite = true, int maxRetries = 3, int retryDelayMs = 500)
        {
            if (string.IsNullOrEmpty(sourceFile) || string.IsNullOrEmpty(destFile))
            {
                LogUtil.Error($"TestLogic: SafeFileCopy - 源文件或目标文件路径为空");
                return false;
            }

            if (!File.Exists(sourceFile))
            {
                LogUtil.Error($"TestLogic: SafeFileCopy - 源文件不存在: {sourceFile}");
                return false;
            }

            // 确保目标目录存在
            var destDir = Path.GetDirectoryName(destFile);
            if (!Directory.Exists(destDir))
            {
                try
                {
                    Directory.CreateDirectory(destDir);
                }
                catch (Exception ex)
                {
                    LogUtil.Error($"TestLogic: SafeFileCopy - 创建目标目录失败: {destDir}, 错误: {ex.Message}");
                    return false;
                }
            }

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    // 检查源文件是否被占用
                    if (IsFileInUse(sourceFile))
                    {
                        LogUtil.Warning($"TestLogic: SafeFileCopy - 源文件被占用，尝试等待释放: {sourceFile} (尝试 {attempt}/{maxRetries})");
                        if (attempt < maxRetries)
                        {
                            System.Threading.Thread.Sleep(retryDelayMs);
                            continue;
                        }
                        else
                        {
                            LogUtil.Error($"TestLogic: SafeFileCopy - 源文件持续被占用，复制失败: {sourceFile}");
                            return false;
                        }
                    }

                    // 如果目标文件存在且被占用，尝试等待释放
                    if (File.Exists(destFile) && IsFileInUse(destFile))
                    {
                        LogUtil.Warning($"TestLogic: SafeFileCopy - 目标文件被占用，尝试等待释放: {destFile} (尝试 {attempt}/{maxRetries})");
                        if (attempt < maxRetries)
                        {
                            System.Threading.Thread.Sleep(retryDelayMs);
                            continue;
                        }
                        else
                        {
                            LogUtil.Error($"TestLogic: SafeFileCopy - 目标文件持续被占用，复制失败: {destFile}");
                            return false;
                        }
                    }

                    // 执行文件复制
                    File.Copy(sourceFile, destFile, overwrite);
                    LogUtil.Info($"TestLogic: SafeFileCopy - 文件复制成功: {sourceFile} -> {destFile}");
                    return true;
                }
                catch (IOException ioEx) when (ioEx.Message.Contains("正由另一进程使用") || ioEx.Message.Contains("being used by another process"))
                {
                    LogUtil.Warning($"TestLogic: SafeFileCopy - 文件被占用，第 {attempt} 次重试: {ioEx.Message}");
                    if (attempt < maxRetries)
                    {
                        System.Threading.Thread.Sleep(retryDelayMs);
                    }
                    else
                    {
                        LogUtil.Error($"TestLogic: SafeFileCopy - 文件复制失败，已达到最大重试次数: {sourceFile} -> {destFile}, 错误: {ioEx.Message}");
                        return false;
                    }
                }
                catch (UnauthorizedAccessException uaEx)
                {
                    LogUtil.Error($"TestLogic: SafeFileCopy - 访问权限不足: {sourceFile} -> {destFile}, 错误: {uaEx.Message}");
                    return false;
                }
                catch (Exception ex)
                {
                    LogUtil.Error($"TestLogic: SafeFileCopy - 文件复制异常: {sourceFile} -> {destFile}, 错误: {ex.Message}");
                    if (attempt < maxRetries)
                    {
                        System.Threading.Thread.Sleep(retryDelayMs);
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 检查文件是否被其他进程占用
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>是否被占用</returns>
        private static bool IsFileInUse(string filePath)
        {
            if (!File.Exists(filePath))
                return false;

            try
            {
                using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    // 如果能够打开文件，说明没有被占用
                    return false;
                }
            }
            catch (IOException)
            {
                // 如果抛出IOException，通常表示文件被占用
                return true;
            }
            catch (Exception)
            {
                // 其他异常也认为文件可能被占用
                return true;
            }
        }

        public static bool Save(TestInfo tInfo)
        {
            if (infos != null && infos.Any(s => s.Id == tInfo.Id && s.TestDate != tInfo.TestDate))
            {
                var r = infos.FirstOrDefault(s => s.Id == tInfo.Id);
                string delName = $"{r.TestDate:yyyyMMddHHmmss}_{tInfo.Id}";
                string deljsonfileName = Path.Combine(JsonDataPath, $"{delName}.json");
                DeleteFiles(deljsonfileName);
                infos.RemoveAll(s => s.Id == tInfo.Id);
            }
            string name = $"{tInfo.TestDate:yyyyMMddHHmmss}_{tInfo.Id}";
            string jsonfileName = Path.Combine(JsonDataPath, $"{name}.json");

            //将检查数据由内存或临时目录保存到结果目录 
            if (!Directory.Exists(AppStatic.VideoConfig.VideoPath))
                Directory.CreateDirectory(AppStatic.VideoConfig.VideoPath);
            if (!Directory.Exists(Path.Combine(AppStatic.VideoConfig.VideoPath, name, "Thumbnail\\")))
                Directory.CreateDirectory(Path.Combine(AppStatic.VideoConfig.VideoPath, name, "Thumbnail\\"));
            string dname = Path.Combine(AppStatic.VideoConfig.VideoPath, name);
            string tname = Path.Combine(AppStatic.VideoConfig.VideoPath, name, "Thumbnail\\");
            if (!Directory.Exists(dname))
                Directory.CreateDirectory(dname);

            tInfo.Result?.Images?.ForEach(x =>
            {
                new Bitmap(x.BitBuffer.Byte2Bitmap()).Save(Path.Combine(dname, x.Name));
                x.Source = Path.Combine(dname, x.Name);
                x.Type = MediaSourceType.LocalPath;
                x.Name = x.Name;
            });
            tInfo.Result?.Vedios?.ForEach(x =>
            {
                // 使用安全的文件复制方法
                if (SafeFileCopy(Path.Combine(AppVideoConfig.TempPath, x.Name), Path.Combine(dname, x.Name), true))
                {
                    x.Source = Path.Combine(dname, x.Name);
                }
                else
                {
                    LogUtil.Error($"TestLogic: Save - 视频文件复制失败: {x.Name}");
                }

                // 使用安全的文件复制方法复制缩略图
                if (!string.IsNullOrEmpty(x.ThumbnailSource) && File.Exists(x.ThumbnailSource))
                {
                    var thumbnailDestPath = Path.Combine(tname, Path.GetFileName(x.ThumbnailSource));
                    if (SafeFileCopy(x.ThumbnailSource, thumbnailDestPath, true))
                    {
                        x.ThumbnailSource = thumbnailDestPath;
                    }
                    else
                    {
                        LogUtil.Error($"TestLogic: Save - 缩略图文件复制失败: {x.ThumbnailSource}");
                    }
                }
            });

            File.WriteAllText(jsonfileName, JsonConvert.SerializeObject(tInfo));
            //添加缓存
            if (infos.All(o => o.Id != tInfo.Id))
                infos.Add(tInfo);
            return true;
        }

        public static List<TestInfo> Load(DateTime startTime, DateTime endTime)
        {
            if (infos.Count > 0)
                return infos;
            FillTest(startTime);
            return infos.Where(o => o.TestDate >= startTime && o.TestDate <= endTime).OrderByDescending(s => s.TestDate).ToList();
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

        private static List<AppTemp> _appTemps = null;

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
            var path = AppDomain.CurrentDomain.BaseDirectory + "PrintTemp\\";
            string[] files = Directory.GetFiles(path, "*.docx");
            foreach (string file in files)
            {
                string[] names = file.Replace(path, "").Split('.');
                tenmps.Add(new AppTemp() { Name = names[0], Path = file.ToString() });
            }

            return tenmps;
        }
    }
}
