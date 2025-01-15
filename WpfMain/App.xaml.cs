using HandyControl.Themes;
using Record;
using System.Windows;
using System.Windows.Media;
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

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            //初始化DLL配置
            Global.InitDllPath();
        }
}
}
