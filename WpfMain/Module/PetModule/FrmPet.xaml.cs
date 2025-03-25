using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AForge.Video.DirectShow;
using CuPrint;
using Entity.Entity;
using Tools.App;
using Tools.Extend;
using WpfMain.Controlls;
using WpfMain.Logic;
using WpfMain.Module.SysModule;
using MessageBox = HandyControl.Controls.MessageBox;

namespace WpfMain.Module.PetModule
{
    /// <summary>
    /// FrmPet.xaml 的交互逻辑
    /// </summary>
    public partial class FrmPet : UserControl, ICustom
    {
        //public List<object> ResolutionDataList = new List<object>();

        //public static readonly DependencyProperty DemoModelProperty = DependencyProperty.Register(
        //    nameof(DemoModel), typeof(PropertyGridDemoModel), typeof(FrmPet), new PropertyMetadata(default(PropertyGridDemoModel)));

        //public PropertyGridDemoModel DemoModel
        //{
        //    get => (PropertyGridDemoModel)GetValue(DemoModelProperty);
        //    set => SetValue(DemoModelProperty, value);
        //}

        /// <summary>
        /// 当前检查信息
        /// </summary>
        //public TestInfo tInfo = new TestInfo();

        public UCVideo Video { get; set; }

        public static readonly DependencyProperty VideoEntityProperty = DependencyProperty.Register(
            nameof(VideoModel), typeof(PropertyVideoModel), typeof(FrmPet), new PropertyMetadata(default(PropertyVideoModel)));

        public PropertyVideoModel VideoModel
        {
            get => (PropertyVideoModel)GetValue(VideoEntityProperty);
            set => SetValue(VideoEntityProperty, value);
        }

        public static readonly DependencyProperty TestInfoProperty = DependencyProperty.Register(
            nameof(tInfo), typeof(TestInfo), typeof(FrmPet), new PropertyMetadata(default(TestInfo)));

        public TestInfo tInfo
        {
            get => (TestInfo)GetValue(TestInfoProperty);
            set => SetValue(TestInfoProperty, value);
        }

        public FrmPet()
        {
            InitializeComponent();
            VideoModel = new PropertyVideoModel();
            VideoModel.ExposureModel = new Exposure();
            VideoModel.Images = new List<VideoImage>();// { new VideoImage() { Path = "https://tpc.googlesyndication.com/simgad/2324724962607117599", Name = "1" }, new VideoImage() { Path = "https://tpc.googlesyndication.com/simgad/2324724962607117599", Name = "1" } };

            tInfo = new TestInfo();
            tInfo.Result = new TestResult();
            tInfo.Result.Images = new List<ImageItem>();
            DataContext = this;
            HandyControl.Controls.Screenshot.Snapped += Screenshot_Snapped;
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {

        }

        /// <summary>
        /// 设置曝光
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CameraUC_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Video?.OnVideoSetCamera(VideoProcAmpProperty.Brightness, int.Parse(e.NewValue.ToString()), VideoProcAmpFlags.Manual);
        }

        private void StartCamp_OnClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(AppStatic.VideoConfig.ImagePath))
            {
                if (HandyControl.Controls.MessageBox.Show("视频路径未设置！请先配置", "系统提示", MessageBoxButton.OKCancel,
                        MessageBoxImage.Warning) == MessageBoxResult.OK)
                {

                    UCSetting setting = new UCSetting();
                    setting.Owner = AppStatic.MainWindow;
                    setting.ShowDialog();
                    Video.SetAviFilePath();
                    return;
                }
            }
            if (Video.isStart)
            {
                string videoPath = Video?.End();
                if (tInfo.Result.Vedios == null)
                    tInfo.Result.Vedios = new List<MediaItem>();
                var old = tInfo.Result;
                tInfo.Result = new TestResult();
                var sp = videoPath.Split('\\');
                var fileName = sp[sp.Length - 1];
                old.Vedios.Add(new MediaItem() { Source = videoPath, Name = fileName, Type = MediaSourceType.LocalPath });
                tInfo.Result = old;
                VideoModel.ExposureModel = new Exposure() { IsAuto = VideoModel.ExposureModel.IsAuto, IsEnable = true };
                StartCamp.Content = "开始录像";
            }
            else
            {

                Video?.SetAviFilePath();
                Video?.Start();
                VideoModel.ExposureModel = new Exposure() { IsAuto = VideoModel.ExposureModel.IsAuto, IsEnable = false };
                StartCamp.Content = "停止录像";
            }
        }

        private void StopCamp_OnClick(object sender, RoutedEventArgs e)
        {
            Video?.Stop();
        }

        //拍照
        private void EndCamp_OnClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(AppStatic.VideoConfig.ImagePath))
            {
                if (HandyControl.Controls.MessageBox.Show("拍照路径未设置！请先配置", "系统提示", MessageBoxButton.OKCancel,
                        MessageBoxImage.Warning) == MessageBoxResult.OK)
                {

                    UCSetting setting = new UCSetting();
                    setting.Owner = AppStatic.MainWindow;
                    setting.ShowDialog();
                    return;
                }
            }
            EndCamp.IsEnabled = false;
            System.Drawing.Image img = Video?.Capture();
            if (img != null)
            {
                //string fullName = DateTime.Now.ToString("yyyyMMddHHmmss") + "-camp." + AppStatic.VideoConfig.ImageType;
                string fullName = $"0{tInfo.Result.Images.Count + 1}.{AppStatic.VideoConfig.ImageType}";
                try
                {
                    if (tInfo.Result.Images == null)
                        tInfo.Result.Images = new List<ImageItem>();
                    var old = tInfo.Result;
                    tInfo.Result = new TestResult();
                    old.Images.Add(new ImageItem() { ImageSource = (new Bitmap(img)).BitmapToImageSource(), Name = fullName });
                    tInfo.Result = old;
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                }
                finally
                {
                    img.Dispose();
                    EndCamp.IsEnabled = true;
                }

            }
        }

        /// <summary>
        /// 选中录音
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ToggleButton_OnChecked(object sender, RoutedEventArgs e)
        {
            VideoModel.ExposureModel = new Exposure() { IsAuto = true, IsEnable = VideoModel.ExposureModel.IsEnable };
            Video?.AutoWavRecorder(true);
        }

        /// <summary>
        /// 取消录音
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ToggleButton_OnUnchecked(object sender, RoutedEventArgs e)
        {
            VideoModel.ExposureModel = new Exposure() { IsAuto = false, IsEnable = VideoModel.ExposureModel.IsEnable };
            Video?.AutoWavRecorder(false);
        }

        public void Closed()
        {
            Video?.Close();
        }

        public void Refresh()
        {
            Video?.InitVideo();
        }

        /// <summary>
        /// 保存检查
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_SaveTest(object sender, RoutedEventArgs e)
        {
            if (tInfo?.Result?.Images?.Count == 0)
                if (MessageBox.Show("尚未添加任何图片,是否确定导出", "系统提示", MessageBoxButton.OKCancel, MessageBoxImage.Warning)== MessageBoxResult.OK)
                    return;

            if (Video.isStart)
            {
                MessageBox.Show(AppStatic.MainWindow, "正在录像中，请先停止！", "系统提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            TestLogic.Save(tInfo);
            if (AppStatic.PetInfo.PetTypes.Any(s => s.Name == tInfo.Type))
            {
                if (AppStatic.PetInfo.PetTypes.First(s => s.Name == tInfo.Type).PetVarietys.All(s => s.Name != tInfo.Variety))
                {
                    AppStatic.PetInfo.PetTypes.First(s => s.Name == tInfo.Type).PetVarietys.Add(new PetVariety() { Name = tInfo.Variety });
                }
            }
            else
            {
                AppStatic.PetInfo.PetTypes.Add(new PetType() { Name = tInfo.Type, PetVarietys = new List<PetVariety>() { new PetVariety() { Name = tInfo.Variety } } });
            }
            AppStatic.PetInfo.Save();
            MessageBox.Show(AppStatic.MainWindow, "保存成功！", "系统提示", MessageBoxButton.OK, MessageBoxImage.None);
        }

        /// <summary>
        /// 图片点击事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void UCFiles_OnImagesClick(object sender, TestInfo e)
        {
            Files.SelectedImageItem = (sender as UCFiles).SelectedImageItem;
            FrmBackModule pet = new FrmBackModule(new FrmPetImage(e, (sender as UCFiles).SelectedImageItem));
            pet.title.Text = "查看";
            pet.Owner = AppStatic.MainWindow;
            pet.ShowDialog();
            tInfo = new TestInfo();
            tInfo = e;

        }

        private void UCFiles_OnVideoClick(object sender, MediaItem e)
        {
            UCLocalVideo pet = new UCLocalVideo(e.Source);
            pet.ShowDialog();
        }

        /// <summary>
        /// 遮罩
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void Mask_OnClick(object sender, RoutedEventArgs e)
        {

        }

        /// <summary>
        /// 保存自带的截图功能的图片
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Screenshot_Snapped(object sender, HandyControl.Data.FunctionEventArgs<ImageSource> e)
        {
            var old = tInfo.Result;
            tInfo.Result = new TestResult();
            old.Images.Add(new ImageItem { Name = $"截图{DateTime.Now:yyyyMMddHHmmss}", ImageSource = e.Info });
            tInfo.Result = old;
        }

        private void Button_Save_Print_Test(object sender, RoutedEventArgs e)
        {
            if (!System.IO.File.Exists(tInfo.TestPath))
            {
                MessageBox.Show(AppStatic.MainWindow, "打印模板文件不存在！", "系统提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            FrmModule f = new FrmModule(new UCPrintNotes(tInfo));
            f.ShowDialog();
            return;
            //FrmModule frm = new FrmModule(new UCPrint(tInfo));
            //frm.ShowDialog();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            Video= new UCVideo(BorderVideo.ActualWidth, BorderVideo.ActualHeight);
            this.BorderVideo.Child = Video;


            if (this.ActualHeight < 780)
                Ctl.MaxHeight = 600;
        }
    }
}
public class PropertyGridDemoModel
{
    [Category("Category1")]
    public string String { get; set; }

    [Category("Category2")]
    public int Integer { get; set; }

    [Category("Category2")]
    public bool Boolean { get; set; }

    [Category("Category1")]
    public Gender Enum { get; set; }

    public HorizontalAlignment HorizontalAlignment { get; set; }

    public VerticalAlignment VerticalAlignment { get; set; }

    public ImageSource ImageSource { get; set; }
}

public enum Gender
{
    Male,
    Female
}
