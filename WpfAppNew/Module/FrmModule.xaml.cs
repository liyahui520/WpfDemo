using System;
using System.Windows;
using WinFormsUserControl = System.Windows.Controls.UserControl;
using System.Windows.Forms;
using DevExpress.Xpf.Core;
using Tools.App;
using WpfAppNew.Module.SysModule;
using WpfAppNew.Utils;

namespace WpfAppNew.Module
{
    /// <summary>
    /// FrmModule.xaml 的交互逻辑
    /// </summary>
    public partial class FrmModule: Window
    {
        public bool isCloseed { get; set; }
        public FrmModule(WinFormsUserControl control)
        {
            InitializeComponent();
            
            // 设置窗口全屏但显示任务栏，支持多显示屏
            SetFullScreenWithTaskbar();
            
            this.Control.Content = control;
            
            // 监听窗口位置变化以支持多显示器切换
            this.LocationChanged += OnLocationChanged;
        }
        
        /// <summary>
        /// 窗口位置变化事件处理
        /// </summary>
        private void OnLocationChanged(object sender, EventArgs e)
        {
            // 当窗口被拖拽到其他显示器时，重新调整全屏设置
            WindowUtils.SetFullScreenWithTaskbar(this);
        }
        
        /// <summary>
        /// 设置全屏显示但保留任务栏，支持多显示屏
        /// </summary>
        private void SetFullScreenWithTaskbar()
        {
            WindowUtils.SetFullScreenWithTaskbar(this);
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
            isCloseed = true;
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

        private void MinButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }
         


    }
}
