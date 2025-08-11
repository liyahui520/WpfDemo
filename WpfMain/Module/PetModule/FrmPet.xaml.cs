using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CuPrint;
using DirectShowLib;
using Entity.Entity;
using HandyControl.Expression.Media;
using Microsoft.Win32;
using Tools.App;
using Tools.Extend;
using WpfMain.Controlls;
using WpfMain.Logic;
using WpfMain.Module.SysModule;
using WPFMediaKit.Manager;
using MessageBox = HandyControl.Controls.MessageBox;

namespace WpfMain.Module.PetModule
{
    /// <summary>
    /// FrmPet.xaml 的交互逻辑
    /// </summary>
    public partial class FrmPet : UserControl, ICustom
    {
        public UCMFVideo VideoMF { get; set; }

        /// <summary>
        /// 是否保存
        /// </summary>
        private static bool IsSave = false;


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
            IsSave = false;
            tInfo = new TestInfo();
            tInfo.Result = new TestResult();
            tInfo.Result.Images = new List<ImageItem>();
            DataContext = this;
            HandyControl.Controls.Screenshot.Snapped += Screenshot_Snapped;
            SetupTimer();
            var a = new List<Resolution>();
            a.AddRange(new List<Resolution>()
            {
                new Resolution() { Text = "默认",    IsDefault=true, Width = 0, Height = 0 },
                new Resolution() { Text = "800*600",IsDefault=false,  Width = 800, Height = 600 },
                new Resolution() { Text = "1024*768", IsDefault=false,  Width = 1024, Height = 768 },
                new Resolution() { Text = "1280*720", IsDefault=false,  Width = 1280, Height = 720 },
                new Resolution() { Text = "1920*1080",IsDefault=false,   Width = 1920, Height = 1080 }
            });
            //resolutionList.ItemsSource = a;
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
        }

        private bool isStart = false;

        private  void StartCamp_OnClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(AppStatic.VideoConfig.ImagePath))
            {
                if (HandyControl.Controls.MessageBox.Show("视频路径未设置！请先配置", "系统提示", MessageBoxButton.OKCancel,
                        MessageBoxImage.Warning) == MessageBoxResult.OK)
                {

                    UCSetting setting = new UCSetting();
                    setting.Owner = AppStatic.MainWindow;
                    setting.ShowDialog();
                    return;
                }
            }
            if (isStart)
            {
                try
                {
                    isStart = false;
                    StartCamp.IsEnabled = false;
                    StartCamp.Content = "正在停止..";
                    timer.Stop();
                    timeT.Visibility = Visibility.Hidden;
                    System.Drawing.Image img = VideoMF?.Capture();
                    string videoPath = VideoMF?.End();
                    if (tInfo?.Result?.Vedios == null)
                    {
                        if(tInfo == null)
                        {
                            tInfo.Result = new TestResult();
                        }
                        tInfo.Result.Vedios = new List<MediaItem>();
                    } 
                    var path = Path.Combine(AppVideoConfig.TempThumbnailPath, DateTime.Now.ToString("yyyyMMddHHmmss") + ".png");
                    img.Save(path);
                    var old = tInfo.Result;
                    tInfo.Result = new TestResult();
                    old.Vedios.Add(new MediaItem() { Source = videoPath, Name = Path.GetFileName(videoPath), Type = MediaSourceType.LocalPath, ThumbnailSource = path });
                    tInfo.Result = old;
                    VideoModel.ExposureModel = new Exposure() { IsAuto = VideoModel.ExposureModel.IsAuto, IsEnable = true };
                    StartCamp.Content = "开始录像";
                    StartCamp.IsEnabled = true;
                    img.Dispose();
                }
                catch (Exception)
                { 
                    StartCamp.IsEnabled = true;
                    
                }
                finally
                {
                    StartCamp.IsEnabled = true;
                }
            }
            else
            {
                isStart = true;
                //Video?.SetAviFilePath(); 
                VideoMF?.Start();
                startTime = DateTime.Now;
                timer.Start();
                timeT.Visibility = Visibility.Visible;
                VideoModel.ExposureModel = new Exposure() { IsAuto = VideoModel.ExposureModel.IsAuto, IsEnable = false };
                StartCamp.Content = "停止录像";
            }
        }

        private System.Timers.Timer timer;
        private DateTime startTime;
        private void SetupTimer()
        {
            timer = new System.Timers.Timer(1000); // 设置间隔为1000毫秒（1秒）
            timer.Elapsed += OnTimerElapsed; // 注册事件处理程序
            timer.AutoReset = true; // 设置是否重复计时
            timer.Enabled = false; // 启动计时器
        }

        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            UpdateTimerText(); // 更新UI组件，需要使用Dispatcher来确保线程安全
        }


        private void UpdateTimerText()
        {
            TimeSpan elapsedTime = DateTime.Now - startTime; // 计算经过的时间
            string timeText = elapsedTime.ToString(@"hh\:mm\:ss"); // 格式化时间字符串
            this.Dispatcher.Invoke(() => LXDate1.Text = timeText);
            //LXDate1.Text = timeText; // 直接更新UI组件，无需使用Dispatcher。
        }


        private void StopCamp_OnClick(object sender, RoutedEventArgs e)
        {
            VideoMF?.Stop();
        }

        //拍照
        private  void EndCamp_OnClick(object sender, RoutedEventArgs e)
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
            System.Drawing.Image img = VideoMF?.Capture();
            if (img != null)
            {
                //string fullName = DateTime.Now.ToString("yyyyMMddHHmmss") + "-camp." + AppStatic.VideoConfig.ImageType;
                string fullName = $"0{tInfo.Result?.Images?.Count + 1}.{AppStatic.VideoConfig.ImageType}";
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
            VideoMF?.AutoWavRecorder(true);
        }

        /// <summary>
        /// 取消录音
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ToggleButton_OnUnchecked(object sender, RoutedEventArgs e)
        {
            VideoMF?.AutoWavRecorder(false);
        }

        public void Closed()
        {
            //关闭摄像头
            VideoMF?.Close();
            //处理图像资源
            if (tInfo?.Result?.Images != null)
            {
                foreach (var item in tInfo.Result.Images)
                {
                    if (item.ImageSource is BitmapSource bitmapSource)
                    {
                        // 释放BitmapSource资源
                        bitmapSource.Freeze();
                    }
                }
            }
            //删除指定目录下所有文件 
            ObjectExtension.DeleteDirectoryContents(AppVideoConfig.TempPath);
            tInfo = null;
            // 清理大对象堆（仅在必要时使用）
            if (GC.CollectionCount(2) < 1) // 检查大对象堆回收次数
            {
                GC.Collect(2, GCCollectionMode.Forced); // 强制回收大对象堆
                GC.WaitForPendingFinalizers();
            }
            //Video?.Close();
            //VideoMF.Stop();
        }

        public void Refresh()
        {
            //Video?.InitVideo();
            //VideoMF.VideoInit();
        }

        /// <summary>
        /// 保存检查
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_SaveTest(object sender, RoutedEventArgs e)
        {
            //if (tInfo?.Result?.Images?.Count == 0)
            //    if (HandyControl.Controls.MessageBox.Ask($"摄像头未获取到", "系统提示") == MessageBoxResult.OK)
            //        return;

            if (VideoMF.isStart)
            {
                HandyControl.Controls.MessageBox.Warning($"正在录像中，请先停止！", "系统提示");
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
            IsSave = true;
            HandyControl.Controls.MessageBox.Success($"保存成功！", "系统提示");
        }

        /// <summary>
        /// 图片点击事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void UCFiles_OnImagesClick(object sender, TestInfo e)
        {
            VideoMF?.Stop();
            Files.SelectedImageItem = (sender as UCFiles).SelectedImageItem;
            FrmBackModule pet = new FrmBackModule(new FrmPetImage(e, (sender as UCFiles).SelectedImageItem));
            pet.title.Text = "查看";
            pet.Owner = AppStatic.MainWindow;
            pet.ShowDialog();
            tInfo = new TestInfo();
            tInfo = e;
            VideoMF?.Start();

        }

        private void UCFiles_OnVideoClick(object sender, MediaItem e)
        {
            //UCLocalVideo video = new UCLocalVideo(e.Source);
            //video.ShowDialog();

            (AppStatic.uCVideo as UCLocalVideo)?.InitVodio(e.Source);
            (AppStatic.uCVideo as UCLocalVideo).ShowDialog();
        }

        private void Videop_OnVideoClick(object sender, RoutedEventArgs e)
        {
            var openDialog = new OpenFileDialog
            {
                Filter = "视频文件 (*.mp4;*.avi;*.wmv;*.mov;*.mkv)|*.mp4;*.avi;*.wmv;*.mov;*.mkv|所有文件 (*.*)|*.*"
            };
            if (openDialog.ShowDialog() == true)
            {
                try
                {
                    //UCLocalVideo pet = new UCLocalVideo(openDialog.FileName);
                    //pet.ShowDialog();
                    (AppStatic.uCVideo as UCLocalVideo)?.InitVodio(openDialog.FileName);
                    (AppStatic.uCVideo as UCLocalVideo).ShowDialog();
                }
                catch (Exception)
                {
                }
            }
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
                HandyControl.Controls.MessageBox.Error($"打印模板文件不存在！", "系统提示");
                return;
            }

            FrmModule f = new FrmModule(new UCPrintNotes(tInfo));
            f.Title = "打印报告";
            f.ShowDialog();
            return;
            //FrmModule frm = new FrmModule(new UCPrint(tInfo));
            //frm.ShowDialog();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            //Video = new UCVideo(BorderVideo.ActualWidth, BorderVideo.ActualHeight);
            //this.BorderVideo.Child = Video;

            VideoMF = new UCMFVideo(BorderVideo.ActualWidth, BorderVideo.ActualHeight);
            this.BorderVideo.Child = VideoMF;

            //VideoMF.VideoInit();
            //if (this.ActualHeight < 780)
            //    Ctl.MaxHeight = 600;
        }

        public void fenb_OnSelected(object sender, RoutedEventArgs e)
        {
            //if (resolutionList.SelectedItem == null) return;
            //AppStatic.Resolution = ((Resolution)resolutionList.SelectedItem);
            //VideoMF?.CamReLoad();
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
