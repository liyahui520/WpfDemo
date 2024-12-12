
using System.Windows;
using System.Windows.Input;
using HandyControl.Tools.Extension;
using WpfMain.Module;
using WpfMain.Module.PetModule;

namespace WpfMain
{
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            this.MaxHeight = SystemParameters.PrimaryScreenHeight;//防止最大化时系统任务栏被遮盖
        }
        /// <summary>
        /// 窗口移动
        /// </summary>
        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        /// <summary>
        /// 最小化按钮
        /// </summary>
        private void MinButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        /// <summary>
        /// 最大化按钮
        /// </summary>
        private void MaxButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = this.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        /// <summary>
        /// 关闭按钮
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void PetModule_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            FrmModule pet = new FrmModule(new FrmPet());
            pet.title.Text = "新检查";
            pet.Owner = this;
            pet.ShowDialog();
        }

        private void UIElement_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            FrmModule frm = new FrmModule(new FrmHistory());
            frm.title.Text = "历史记录";
            frm.Owner = this;
            frm.ShowDialog();
        }
    }
}
