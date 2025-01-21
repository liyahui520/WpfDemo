using HandyControl.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WpfMain.Module.SysModule
{
    /// <summary>
    /// UCSetting.xaml 的交互逻辑
    /// </summary>
    public partial class UCSetting
    {
        public UCSetting()
        {
            InitializeComponent();

           ImagePath.Text= AppStatic.VideoConfig.ImagePath;
           VideoPath.Text= AppStatic.VideoConfig.VideoPath;
        }
        /// <summary>
        /// 视频路径
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void VideoPath_OnClick(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog folderBrowser = new FolderBrowserDialog(); 
            if (folderBrowser.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                VideoPath.Text = folderBrowser.SelectedPath;
            }
        }

        /// <summary>
        /// 拍照路径
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ImagePath_OnClick(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog folderBrowser = new FolderBrowserDialog();
            if (folderBrowser.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ImagePath.Text = folderBrowser.SelectedPath;
            }
        }

        /// <summary>
        /// 保存设置
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void Save_OnClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(VideoPath.Text.Trim()))
            {
                HandyControl.Controls.MessageBox.Show("视频路径未设置！", "系统提示", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (string.IsNullOrWhiteSpace(ImagePath.Text.Trim()))
            {
                HandyControl.Controls.MessageBox.Show("拍照路径未设置！", "系统提示", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            } 
            AppVideoConfig config = new AppVideoConfig();
            config.VideoPath = VideoPath.Text.Trim();
            config.ImagePath = ImagePath.Text.Trim();
            config.Save();
            AppStatic.VideoConfig = config;
            this.Close();
            HandyControl.Controls.MessageBox.Show("保存成功！", "系统提示", MessageBoxButton.OK, MessageBoxImage.None);
        }
    }
}
