using System;
using System.IO;

namespace WpfAppNew.Services
{
    /// <summary>
    /// 日志服务实现
    /// </summary>
    public class LoggerService : ILoggerService
    {
        private readonly string _logFilePath;
        private readonly object _lockObject = new object();

        public LoggerService()
        {
            try
            {
                // 创建日志文件路径
                string logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }
                
                _logFilePath = Path.Combine(logDirectory, $"app_{DateTime.Now:yyyyMMdd}.log");
            }
            catch
            {
                // 如果无法创建日志目录，使用临时文件
                _logFilePath = Path.GetTempFileName();
            }
        }

        /// <summary>
        /// 记录调试信息
        /// </summary>
        /// <param name="message">日志消息</param>
        public void Debug(string message)
        {
            WriteLog("DEBUG", message);
        }

        /// <summary>
        /// 记录一般信息
        /// </summary>
        /// <param name="message">日志消息</param>
        public void Info(string message)
        {
            WriteLog("INFO", message);
        }

        /// <summary>
        /// 记录警告信息
        /// </summary>
        /// <param name="message">日志消息</param>
        public void Warning(string message)
        {
            WriteLog("WARNING", message);
        }

        /// <summary>
        /// 记录错误信息
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="exception">异常对象</param>
        public void Error(string message, Exception exception = null)
        {
            string fullMessage = exception != null ? $"{message} - {exception}" : message;
            WriteLog("ERROR", fullMessage);
        }

        /// <summary>
        /// 写入日志文件
        /// </summary>
        /// <param name="level">日志级别</param>
        /// <param name="message">日志消息</param>
        private void WriteLog(string level, string message)
        {
            lock (_lockObject)
            {
                try
                {
                    string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";
                    File.AppendAllText(_logFilePath, logEntry);
                }
                catch
                {
                    // 忽略日志写入错误，避免影响主程序流程
                }
            }
        }
    }
}