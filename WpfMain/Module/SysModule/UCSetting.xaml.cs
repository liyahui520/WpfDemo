using AForge.Video.DirectShow;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Entity.Entity;
using Tools.App;
using Tools.Extend;
using System.Linq;
using HandyControl.Tools.Extension;
using System.Windows.Controls.Primitives;
using DevExpress.Office.Utils;

namespace WpfMain.Module.SysModule
{
    /// <summary>
    /// UCSetting.xaml 的交互逻辑
    /// </summary>
    public partial class UCSetting
    {

        private string _title;
        public UCSetting()
        {
            InitializeComponent();
        }


        public UCSetting(string title)
        {
            InitializeComponent();
            _title = title;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            #region 视频设置初始化
            VideoPath.Text = AppStatic.VideoConfig.VideoPath;
            List<VideoType> videoTypes = new List<VideoType>() { new VideoType() { Name = "AVI" } };
            videoType.ItemsSource = videoTypes;//, new VideoType() { Name = "MP4" }, new VideoType() { Name = "WMV" } 
            videoType.SelectedIndex = 0;
            if (!string.IsNullOrEmpty(AppStatic.VideoConfig.VideoType))
            {
                VideoType vt = videoTypes.FirstOrDefault(o => o.Name == AppStatic.VideoConfig.VideoType.ToUpper());
                if (vt != null)
                    videoType.SelectedItem = vt;
            }


            #endregion

            #region 图片设置初始化
            ImagePath.Text = AppStatic.VideoConfig.ImagePath;
            List < VideoType > imagetypes= new List<VideoType>() { new VideoType() { Name = "JPG" }, new VideoType() { Name = "PNG" } };
            imageType.ItemsSource = imagetypes;
            imageType.SelectedIndex = 0;
            if (!string.IsNullOrEmpty(AppStatic.VideoConfig.ImageType))
            {
                VideoType vt = imagetypes.FirstOrDefault(o => o.Name == AppStatic.VideoConfig.ImageType.ToUpper());
                if (vt != null)
                    imageType.SelectedItem = vt;
            }

            #endregion

            #region 医院信息初始化

            hospitalName.Text = AppStatic.AppHospital.HospitalName;
            hospitalContacts.Text = AppStatic.AppHospital.HospitalContacts;
            hospitalPhone.Text = AppStatic.AppHospital.HospitalPhone;
            img.Source = AppStatic.AppHospital.HospitalLogo?.FromByteArray();
            UserName.Text = AppStatic.AppHospital.UserName;
            jianjie.Text = AppStatic.AppHospital.HospitalBiref;
            address.Text = AppStatic.AppHospital.HospitalAddress;
            #endregion

            if (!string.IsNullOrEmpty(_title))
            {
                switch (_title)
                {
                    case "医院设置":
                        Selector.SetIsSelected(TabItemHospital, true);
                        break;
                    case "设置":
                        Selector.SetIsSelected(TabItemVideo, true);
                        break;
                }
            }


            var deviceList = new List<VideoType>();
            // 设定初始视频设备
            FilterInfoCollection videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            for (int i = 0; i < videoDevices.Count; i++)
            {
                deviceList.Add(new VideoType() { Name = videoDevices[i].Name, VideoString = videoDevices[i].MonikerString });
            }
            device.ItemsSource = deviceList;
            if (deviceList != null && deviceList.Count != 0)
            {
                device.SelectedIndex = 0;
                if (!string.IsNullOrEmpty(AppStatic.VideoConfig?.VideoDecive))
                    device.SelectedValue = AppStatic.VideoConfig.VideoDecive;
            }
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



            //config.ImagePath = ImagePath.Text.Trim();
            config.VideoType = videoType.SelectedValue.ToString().ToLower();
            config.ImageType = imageType.SelectedValue.ToString().ToLower();
            config.VideoDecive = device.SelectedValue.ToString();
            config.Save();
            AppStatic.VideoConfig = config;

            AppHospital hospital = new AppHospital();
            hospital.HospitalName = hospitalName.Text.Trim();
            hospital.UserName = UserName.Text.Trim();
            hospital.HospitalContacts = hospitalContacts.Text.Trim();
            hospital.HospitalPhone = hospitalPhone.Text.Trim();
            hospital.HospitalLogo = ((BitmapImage)img.Source)?.ToByteArray();
            hospital.HospitalBiref = jianjie.Text.Trim();
            hospital.HospitalAddress = address.Text.Trim();
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
