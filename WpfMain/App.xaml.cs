using System;
using System.Threading.Tasks;
using HandyControl.Themes;
using Record;
using System.Windows;
using System.Windows.Media;
using MessageBox = HandyControl.Controls.MessageBox;
using HandyControl.Properties.Langs;
using HandyControl.Tools;
using System.Globalization;
namespace WpfMain
{
    public partial class App : Application
    {
        internal void UpdateTheme(ApplicationTheme theme)
        {
            if (ThemeManager.Current.ApplicationTheme != theme)
            {
                ThemeManager.Current.ApplicationTheme = theme;
            }
        }

        internal void UpdateAccent(Brush accent)
        {
            if (ThemeManager.Current.AccentColor != accent)
            {
                ThemeManager.Current.AccentColor = accent;
            }
        }

        public App()
        {
            // Dispatche UI 线程 未被处理的异常 最后一道关卡 
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            // 全局处理 全局捕获异常 但是不可以捕获Task的异常
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            // 处理Task没有捕获到全局异常
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e); 
            ConfigHelper.Instance.SetLang("zh-cn"); 
            AppStatic.VideoConfig = AppVideoConfig.GetConfig();
            ConfigHelper.Instance.SetWindowDefaultStyle();
            ConfigHelper.Instance.SetNavigationWindowDefaultStyle();
            //初始化DLL配置
            Global.InitDllPath();
        }

        // 在垃圾回收机制触发的时候，才能捕捉到Task异常
        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            // 
        }
        // 全局处理异常 不可以捕获Task
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // 可以记录下日志
        }
        // 处理UI异常
        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(e.Exception.Message, "系统提示", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
