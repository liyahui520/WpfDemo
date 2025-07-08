using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using Tools.App;
using WpfMain.Controlls;
using WpfMain.Module;
using WpfMain.Module.PetModule;
using WpfMain.Module.SysModule;

namespace WpfMain
{
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            this.MaxHeight = SystemParameters.PrimaryScreenHeight;//防止最大化时系统任务栏被遮盖
            AppStatic.MainWindow = this;
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
            Application.Current.Shutdown();
        }

        private void PetModule_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var video = new FrmPet();
            FrmModule pet = new FrmModule(video);
            pet.title.Text = "新检查";
            pet.Title = "新检查";
            pet.Owner = this;
            pet.Show();

            //new UCMFVideo().Show();
        }

        private void UIElement_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            FrmModule frm = new FrmModule(new FrmHistory());
            frm.title.Text = "历史记录";
            frm.Title = "历史记录";
            frm.Owner = this;
            frm.ShowDialog();
        }

        private void UCCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            UCSetting setting = new UCSetting((sender as UCCard).Tag?.ToString());
            setting.Owner = this;
            setting.ShowDialog();
        }

        private void UIElement_Print_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DXNotes note = new DXNotes();
            note.Owner = this;
            note.ShowDialog();
            return;

            //FrmModule frm = new FrmModule(new FrmNotes());
            //frm.title.Text = "模板";
            //frm.Owner = this;
            //frm.ShowDialog();
        }

    }
}
