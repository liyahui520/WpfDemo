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
            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            
            // 立即创建并显示启动Loading窗口
            var splash = new Windows.LoadingWindow();
            // 窗口已在构造函数中显示并启动动画，这里立即更新状态
            splash.UpdateStatus("正在初始化系统配置...");
            
            // 将所有初始化操作移到后台线程，避免阻塞UI
            _ = Task.Run(async () =>
            {
                try
                {
                    // 在后台线程执行配置初始化
                    await Task.Run(() =>
                    {
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
                    });
                    
                    splash.UpdateStatus("系统配置初始化完成");
                    //await Task.Delay(200); // 短暂延迟让用户看到状态更新
                    
                    // 启动其他服务的初始化
                    await InitializeServicesAsync(splash);
                }
                catch (Exception ex)
                {
                    LogUtil.Error($"系统初始化失败: {ex.Message}");
                    splash.UpdateStatus("系统初始化失败");
                    
                    // 即使初始化失败，也要正确关闭Loading窗口
                    await Task.Delay(1000);
                    splash.SetCompleted("初始化失败，请重试");
                    await Task.Delay(500);
                    splash.SafeClose();
                    
                    // 显示主窗口
                    this.Dispatcher.Invoke(() =>
                    {
                        this.ShutdownMode = ShutdownMode.OnMainWindowClose;
                        this.MainWindow = new MainWindow();
                        this.MainWindow.Show();
                    });
                }
            });

        }

        /// <summary>
        /// 异步初始化各种服务
        /// </summary>
        /// <param name="splash">启动窗口实例</param>
        /// <returns>异步任务</returns>
        private async Task InitializeServicesAsync(Windows.LoadingWindow splash)
        {
            // 并行启动打印服务（不阻塞）
            _ = Task.Run(async () =>
            {
                try
                {
                    splash.UpdateStatus("正在初始化打印服务...");
                    await PrintNotesService.Instance.StartAsync();
                    splash.UpdateStatus("打印服务初始化完成");
                }
                catch (Exception ex)
                {
                    LogUtil.Error($"打印服务初始化失败: {ex.Message}");
                    splash.UpdateStatus("打印服务初始化失败");
                }
            });

            // 在后台异步完成相机控件预加载，完成后关闭Loading并显示主界面
            try
            {
                splash.UpdateStatus("正在初始化相机控件...");
                await IndustrialCameraPreloadService.Instance.StartPreloadAsync();
                splash.UpdateStatus("相机控件初始化完成");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"IndustrialCameraControl预加载服务启动失败: {ex.Message}");
                splash.UpdateStatus("相机控件初始化失败");
            }
            finally
            {
                // 设置加载完成状态
                splash.SetCompleted("初始化完成");
                
                // 延迟一点时间让用户看到完成状态
                await Task.Delay(500);
                
                // 安全关闭Loading窗口
                splash.SafeClose();

                // 打开主窗口
                this.Dispatcher.Invoke(() =>
                {
                    this.ShutdownMode = ShutdownMode.OnMainWindowClose;
                    this.MainWindow = new MainWindow();
                    this.MainWindow.Show();
                });
            }
        }

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
//        try
//        {
//            if (!SetupLogic.Update())
//                Current.Shutdown();
//        }
//        catch (Exception ex)
//        {
//            //BCLApplication.log.Error(ex);
//        }
//#endif

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
