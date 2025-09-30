using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using Tools.App;
using WpfAppNew.Controlls;
using WpfAppNew.Module;
using WpfAppNew.Module.PetModule;
using WpfAppNew.Module.SysModule;
using WpfAppNew.EmguPlugs;

namespace WpfAppNew
{
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            this.MaxHeight = SystemParameters.PrimaryScreenHeight;//防止最大化时系统任务栏被遮盖
            AppStatic.MainWindow = this;
            AppStatic.Resolution = new Resolution() { Text = "默认", IsDefault = true };
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



        private FrmModule pet;

        private void PetModule_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (pet == null || pet.isCloseed)
            {
                //if (AppStatic.uVCVideo != null)
                //{
                //    pet = new FrmModule(AppStatic.uVCVideo);
                //    pet.title.Text = "新检查";
                //    pet.Title = "新检查";
                //    //pet.Owner = this;
                //    pet.Show();
                //    return;
                //}
                var video = new FrmPetNew();
                pet = new FrmModule(video);
                pet.title.Text = "新检查";
                pet.Title = "新检查";
                //pet.Owner = this;
                pet.Show();
            }
            e.Handled = true;
            pet.WindowState = WindowState.Maximized;
            pet.Activate();
            //WindowState = WindowState.Minimized;
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

        /// <summary>
        /// 打开工业相机测试窗口
        /// </summary>
        private void IndustrialCamera_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                IndustrialCameraTestWindow cameraWindow = new IndustrialCameraTestWindow();
                cameraWindow.Owner = this;
                cameraWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开工业相机测试窗口失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 打开OpenCV摄像头预览窗口
        /// </summary>
        private void OpenOpenCvPreviewWindow()
        {
            //try
            //{
            //    Windows.OpenCvPreviewWindow previewWindow = new Windows.OpenCvPreviewWindow();
            //    previewWindow.Owner = this;
            //    previewWindow.Show();
            //}
            //catch (Exception ex)
            //{
            //    MessageBox.Show($"打开OpenCV预览窗口失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            //}
        }
    }
}
