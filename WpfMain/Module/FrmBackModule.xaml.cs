using System;
using System.Windows;
using System.Windows.Controls;
using Tools.App;
using WpfMain.Module.SysModule;

namespace WpfMain.Module
{
    /// <summary>
    /// FrmBackModule.xaml 的交互逻辑
    /// </summary>
    public partial class FrmBackModule 
    {
        public FrmBackModule(UserControl control)
        {
            InitializeComponent();
            this.Control.Content = control;
        }
        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
            if (this.Control.Content is ICustom)
                ((ICustom)this.Control.Content)?.Closed();
        }

        /// <summary>
        /// 设置页面
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void ButtonSetting_OnClick(object sender, RoutedEventArgs e)
        {
            UCSetting setting = new UCSetting();
            setting.Owner = AppStatic.MainWindow;
            if (setting.ShowDialog() == true)
                if (this.Control.Content is ICustom)
                    ((ICustom)this.Control.Content)?.Refresh();
        }
    }
}
