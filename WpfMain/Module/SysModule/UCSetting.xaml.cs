using AForge.Video.DirectShow;
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
using WpfMain.Entity;
using WpfMain.Extend;

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

            #region 视频设置初始化
            VideoPath.Text = AppStatic.VideoConfig.VideoPath;
            videoType.ItemsSource = new List<VideoType>() { new VideoType() { Name = "AVI" } };//, new VideoType() { Name = "MP4" }, new VideoType() { Name = "WMV" } 
            videoType.SelectedValue = AppStatic.VideoConfig.VideoType.ToUpper();
            #endregion

            #region 图片设置初始化
            ImagePath.Text = AppStatic.VideoConfig.ImagePath;
            imageType.ItemsSource = new List<VideoType>() { new VideoType() { Name = "JPG" }, new VideoType() { Name = "PNG" } };
            imageType.SelectedValue = AppStatic.VideoConfig.ImageType.ToUpper();
            #endregion

            #region 医院信息初始化

            hospitalName.Text = AppStatic.AppHospital.HospitalName;
            hospitalContacts.Text = AppStatic.AppHospital.HospitalContacts;
            hospitalPhone.Text = AppStatic.AppHospital.HospitalPhone;
            img.Source = AppStatic.AppHospital.HospitalLogo?.FromByteArray();
            #endregion

            var deviceList = new List<VideoType>();
            // 设定初始视频设备
            FilterInfoCollection videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            for (int i = 0; i < videoDevices.Count; i++)
            {
                deviceList.Add(new VideoType() { Name = videoDevices[i].Name, VideoString = videoDevices[i].MonikerString });
            }
            device.ItemsSource = deviceList;
            device.SelectedValue = AppStatic.VideoConfig.VideoDecive;

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
            //if (string.IsNullOrWhiteSpace(VideoPath.Text.Trim()))
            //{
            //    HandyControl.Controls.MessageBox.Show("视频路径未设置！", "系统提示", MessageBoxButton.OK, MessageBoxImage.Error);
            //    return;
            //}
            //if (string.IsNullOrWhiteSpace(ImagePath.Text.Trim()))
            //{
            //    HandyControl.Controls.MessageBox.Show("拍照路径未设置！", "系统提示", MessageBoxButton.OK, MessageBoxImage.Error);
            //    return;
            //}
            AppVideoConfig config = new AppVideoConfig();
            config.VideoPath = VideoPath.Text.Trim();
            config.ImagePath = ImagePath.Text.Trim();
            config.VideoType = videoType.SelectedValue.ToString().ToLower();
            config.ImageType = imageType.SelectedValue.ToString().ToLower();
            config.VideoDecive = device.SelectedValue.ToString();
            config.Save();
            AppStatic.VideoConfig = config;

            AppHospital hospital = new AppHospital();
            hospital.HospitalName = hospitalName.Text.Trim();
            hospital.HospitalContacts = hospitalContacts.Text.Trim();
            hospital.HospitalPhone = hospitalPhone.Text.Trim();
            hospital.HospitalLogo = ((BitmapImage)img.Source)?.ToByteArray();
            AppStatic.AppHospital = hospital;
            hospital.Save();
            DialogResult = true;
            this.Close();
            HandyControl.Controls.MessageBox.Show("保存成功！", "系统提示", MessageBoxButton.OK, MessageBoxImage.None);
        }

        private void UIElement_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                RestoreDirectory = true,
                Filter = "(.jpg)|*.jpg|(.png)|*.png",
                DefaultExt = ".jpg"
            };
            var dia = dialog.ShowDialog();
            if (dia == System.Windows.Forms.DialogResult.OK)
            {
                img.Source = dialog.FileName.FromStream();
            }
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            this.Close();
        }
    }
}
