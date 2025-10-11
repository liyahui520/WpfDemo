using System;
using System.Threading.Tasks;
using System.Windows;
using HandyControl.Tools;
using System.Globalization;
using System.Reflection;
using System.IO;
using Entity.Entity;
using Tools.App;
using DevExpress.Utils.About;
using Newtonsoft.Json;
using Tools.Extend;
using WpfAppNew.Logic;
using System.Windows.Interop;
using System.Windows.Media;
using WpfAppNew.Controlls;
using WpfAppNew.Module.PetModule;
using WpfAppNew.Services;

namespace WpfAppNew
{
    public partial class App : Application
    {
        //internal void UpdateTheme(ApplicationTheme theme)
        //{
        //    if (ThemeManager.Current.ApplicationTheme != theme)
        //    {
        //        ThemeManager.Current.ApplicationTheme = theme;
        //    }
        //}

        //internal void UpdateAccent(Brush accent)
        //{
        //    if (ThemeManager.Current.AccentColor != accent)
        //    {
        //        ThemeManager.Current.AccentColor = accent;
        //    }
        //}

        public App()
        {
            // Dispatche UI 线程 未被处理的异常 最后一道关卡 
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            // 全局处理 全局捕获异常 但是不可以捕获Task的异常
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            // 处理Task没有捕获到全局异常
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            // 注释掉软件渲染模式，使用默认的硬件加速渲染，这对OpenCV图像显示更友好
            // RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly; // 默认情况下，WPF 会自动选择最适合的模式
            RenderOptions.ProcessRenderMode = RenderMode.Default; // 使用默认渲染模式，支持硬件加速



        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            // 在显示主窗体前，显式控制关闭行为，避免只有Splash时关闭导致应用退出
            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            ConfigHelper.Instance.SetLang("zh-cn");
            AppStatic.VideoConfig = AppVideoConfig.GetConfig();
            AppStatic.AppHospital = AppHospital.GetConfig();
            AppStatic.PetInfo = PetInfo.GetConfig();
            ConfigHelper.Instance.SetWindowDefaultStyle();
            ConfigHelper.Instance.SetNavigationWindowDefaultStyle();
            //初始化DLL配置
            //Global.InitDllPath();
            System.Threading.Thread.CurrentThread.CurrentUICulture = new CultureInfo("zh-Hans");
            System.Threading.Thread.CurrentThread.CurrentCulture = new CultureInfo("zh-Hans");
            LogUtil.Info("系统启动");
            
            // 显示启动Loading窗口，并在完成相机控件预加载后打开主窗口
            var splash = new Windows.LoadingWindow();
            splash.Show();

            // 并行启动打印服务（不阻塞）
            _ = Task.Run(async () =>
            {
                try
                {
                    await PrintNotesService.Instance.StartAsync();
                }
                catch (Exception ex)
                {
                    LogUtil.Error($"打印服务初始化失败: {ex.Message}");
                }
            });

            // 在后台异步完成相机控件预加载，完成后关闭Loading并显示主界面
            _ = Task.Run(async () =>
            {
                try
                {
                    await IndustrialCameraPreloadService.Instance.StartPreloadAsync();
                }
                catch (Exception ex)
                {
                    LogUtil.Error($"IndustrialCameraControl预加载服务启动失败: {ex.Message}");
                }
                finally
                {
                    // 关闭Loading窗口
                    splash.Dispatcher.Invoke(() =>
                    {
                        try { splash.Close(); } catch { }
                    });

                    // 打开主窗口
                    this.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            var main = new MainWindow();
                            this.MainWindow = main;
                            main.Show();
                            // 主窗体已显示，恢复默认关闭行为
                            this.ShutdownMode = ShutdownMode.OnMainWindowClose;
                        }
                        catch (Exception showEx)
                        {
                            LogUtil.Error($"显示主窗口失败: {showEx.Message}");
                        }
                    });
                }
            });

            //AppStatic.uCVideo = new UCLocalVideo("");
            //AppStatic.uVCVideo = new FrmPetNew();
            //string[] files = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.dll");
            //foreach (string file in files)
            //{
            //    using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(file))
            //    {
            //        if (stream != null)
            //        {
            //            byte[] assemblyData = new byte[stream.Length];
            //            stream.Read(assemblyData, 0, assemblyData.Length);
            //            Assembly.Load(assemblyData);
            //        }

            //    }
            //}

//#if !DEBUG
//            try
//            {
//                if (!SetupLogic.Update())
//                    Current.Shutdown();
//            }
//            catch (Exception ex)
//            {
//                //BCLApplication.log.Error(ex);
//            }
//#endif

        }

        // 在垃圾回收机制触发的时候，才能捕捉到Task异常
        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            LogUtil.Error(e.Exception.Message.ToString());
            // 
        }
        // 全局处理异常 不可以捕获Task
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // 可以记录下日志 
            LogUtil.Error(e.ExceptionObject.ToString());
        }
        // 处理UI异常
        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            //MessageBox.Show(e.Exception.Message, "系统提示", MessageBoxButton.OK, MessageBoxImage.Error);
            LogUtil.Error(e.Exception.Message.ToString());
            e.Handled = true;
        }



        public class SetupLogic
        {

            public static bool Update()
            {
                if (File.Exists("TempSetup.exe"))
                {
                    File.Copy("TempSetup.exe", "Setup.exe", true);
                    File.Delete("TempSetup.exe");
                    return true;
                }

                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Setup.exe");
                MethodInfo minfo = Assembly.LoadFile(path).GetType("Setup.Logic").GetMethod("Update");
                if (minfo == null)
                    return true;

                return (bool)minfo.Invoke(null, null);
            }

        }
    }
}
