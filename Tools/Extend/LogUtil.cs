using System;
using System.IO;
using System.Text;

namespace Tools.Extend
{
    public static class LogUtil
    {
        private const int LOG_CONTENT_MAX_LENGTH = 800;

        static LogUtil()
        {
            string logDirPath = Path.Combine(Directory.GetCurrentDirectory(), "Log");
            if (!Directory.Exists(logDirPath))
            {
                Directory.CreateDirectory(logDirPath);
            }
        }

        private static string TestCurrentLogExists()
        {
            string logPath = Path.Combine(Directory.GetCurrentDirectory(), "Log", DateTime.Now.ToString("yyyy-MM-dd") + ".log");
            if (!File.Exists(logPath))
            {
                File.Create(logPath).Close();
            }
            return logPath;
        }

        public static void Debug(string text)
        {
            WriteLog("Debug", text);
        }
        public static void Error(string text)
        {
            WriteLog("Error", text);
        }

        public static void Info(string text)
        {
            WriteLog("Info", text);
        }
        private static void WriteLog(string label, string log_content)
        {
            if (log_content.Length > LOG_CONTENT_MAX_LENGTH)
            {
                log_content = log_content.Substring(0, LOG_CONTENT_MAX_LENGTH);
            }

            var logPath = TestCurrentLogExists();
            using (var fs = File.Open(logPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                fs.Position = fs.Length;
                byte[] bytes = Encoding.UTF8.GetBytes($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")} {label} {log_content}{Environment.NewLine}");
                fs.Write(bytes, 0, bytes.Length);
            }
        }
    }
}
