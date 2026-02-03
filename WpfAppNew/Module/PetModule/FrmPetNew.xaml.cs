using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Entity.Entity;
using HandyControl.Controls;
using HandyControl.Data;
using HandyControl.Tools.Extension;
using Tools.App;
using Tools.Extend;
using WpfAppNew.Controlls;
using WpfAppNew.Logic;
using WpfAppNew.Module.SysModule;
using WpfAppNew.OpenCv.Core;
using WpfAppNew.Windows; // 添加性能监控窗口引用
using WpfAppNew.EmguPlugs; // 添加IndustrialCameraControl引用
using WpfAppNew.Services;

namespace WpfAppNew.Module.PetModule
{
    /// <summary>
    /// 分辨率选项类
    /// 用于ComboBox显示和数据绑定
    /// </summary>
    public class ResolutionItem
    {
        /// <summary>
        /// 分辨率宽度
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// 分辨率高度
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// 显示名称
        /// </summary>
        public string DisplayName => $"{Width}x{Height}";

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        public ResolutionItem(int width, int height)
        {
            Width = width;
            Height = height;
        }

        /// <summary>
        /// 重写ToString方法
        /// </summary>
        /// <returns>显示名称</returns>
        public override string ToString()
        {
            return DisplayName;
        }
    }

    /// <summary>
    /// 宠物检查模块新界面
    /// </summary>
    public partial class FrmPetNew : UserControl, ICustom
    {
        #region 私有字段

        /// <summary>
        /// 视频文件名格式化字符串
        /// </summary>
        private static string videoFileName = Path.Combine(AppVideoConfig.TempPath, "{0}.pm4");

        /// <summary>
        /// 定时器
        /// </summary>
        private DispatcherTimer _recordingTimer;




        /// <summary>
        /// 点击计数器（用于检测五次点击）
        /// </summary>
        private int _headerClickCount = 0;

        /// <summary>
        /// 点击计时器
        /// </summary>
        private DispatcherTimer _clickTimer;

        /// <summary>
        /// 工业相机控件实例
        /// </summary>
        private IndustrialCameraControl _industrialCameraControl;

        #endregion

        #region 公共属性

        /// <summary>
        /// 获取工业相机控件实例（保持向后兼容）
        /// </summary>
        public IndustrialCameraControl IndustrialCameraControl => _industrialCameraControl;

        #endregion

        #region 依赖属性

        /// <summary>
        /// 视频模型依赖属性
        /// </summary>
        public static readonly DependencyProperty VideoEntityProperty = DependencyProperty.Register(
            nameof(VideoModel), typeof(PropertyVideoModel), typeof(FrmPetNew), new PropertyMetadata(default(PropertyVideoModel)));

        /// <summary>
        /// 视频模型
        /// </summary>
        public PropertyVideoModel VideoModel
        {
            get => (PropertyVideoModel)GetValue(VideoEntityProperty);
            set => SetValue(VideoEntityProperty, value);
        }

        /// <summary>
        /// 测试信息依赖属性
        /// </summary>
        public static readonly DependencyProperty TestInfoProperty = DependencyProperty.Register(
            nameof(tInfo), typeof(TestInfo), typeof(FrmPetNew), new PropertyMetadata(default(TestInfo)));

        /// <summary>
        /// 测试信息
        /// </summary>
        public TestInfo tInfo
        {
            get => (TestInfo)GetValue(TestInfoProperty);
            set => SetValue(TestInfoProperty, value);
        }

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化宠物检查模块新界面
        /// </summary>
        public FrmPetNew()
        {
            InitializeComponent();
            InitializeData();
            DataContext = this;
            Screenshot.Snapped += Screenshot_Snapped;
            InitializeRecordingTimer();
            InitializeClickTimer();
            //InitializeSystem();
        }

        #endregion

        #region 初始化方法

        /// <summary>
        /// 初始化数据
        /// </summary>
        private void InitializeData()
        {
            VideoModel = new PropertyVideoModel();
            VideoModel.ExposureModel = new Exposure();
            VideoModel.Images = new List<VideoImage>();
            tInfo = new TestInfo();
            tInfo.Result = new TestResult();
            tInfo.Result.Images = new List<ImageItem>();
        }

        /// <summary>
        /// 初始化录像定时器
        /// </summary>
        private void InitializeRecordingTimer()
        {
            _recordingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _recordingTimer.Tick += RecordingTimer_Tick;
        }

        /// <summary>
        /// 初始化点击计时器
        /// </summary>
        private void InitializeClickTimer()
        {
            _clickTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2) // 2秒内点击五次
            };
            _clickTimer.Tick += ClickTimer_Tick;
        }

        /// <summary>
        /// 加载设备列表（功能已迁移到IndustrialCameraControl）
        /// </summary>
        private async void LoadDeviceList()
        {
            // 设备管理功能已迁移到IndustrialCameraControl控件
            LogUtil.Info("设备管理功能已迁移到IndustrialCameraControl控件");

            // 加载分辨率选项
            LoadResolutionOptions();
        }

        /// <summary>
        /// 加载分辨率选项（功能已迁移到IndustrialCameraControl）
        /// </summary>
        private void LoadResolutionOptions()
        {
            // 分辨率选择功能已迁移到IndustrialCameraControl控件
            LogUtil.Info("分辨率选择功能已迁移到IndustrialCameraControl控件");
        }

        #endregion

        #region 定时器事件处理

        /// <summary>
        /// 录像定时器事件
        /// </summary>
        private void RecordingTimer_Tick(object sender, EventArgs e)
        {
            UpdateRecordingTime();
        }

        /// <summary>
        /// 点击计时器事件
        /// </summary>
        private void ClickTimer_Tick(object sender, EventArgs e)
        {
            _headerClickCount = 0;
            _clickTimer.Stop();
        }

        /// <summary>
        /// 更新录像时间
        /// </summary>
        private void UpdateRecordingTime()
        {
            if (IndustrialCameraControl != null && IndustrialCameraControl.IsRecording)
            {
                // 从IndustrialCameraControl获取录像持续时间
                var duration = IndustrialCameraControl.RecordingDuration;
                RecordingTimeText.Text = duration.ToString(@"hh\:mm\:ss");
            }
            else
            {
                RecordingTimeText.Text = "00:00:00";
            }
        }

        #endregion

        #region 摄像头事件处理

        /// <summary>
        /// 摄像头状态变更事件处理
        /// </summary>
        private void OnCameraStatusChanged(object sender, WpfAppNew.OpenCv.Core.StatusChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (e.Status == CameraStatus.Running)
                    IndustrialCameraControl.FitToWindowCommand.Execute(null); // 可选：自动调整预览窗口大小 
                //StartPreviewButton.Content = e.Status == CameraStatus.Running ? "停止预览" : "开始预览";
                //StartPreviewButton.Foreground = e.Status == CameraStatus.Running ?
                //    new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 107, 107)) :
                //    new SolidColorBrush(System.Windows.Media.Color.FromRgb(204, 204, 204));
            }));
        }

        /// <summary>
        /// 摄像头录像状态变更事件处理
        /// </summary>
        private void OnCameraRecordingStatusChanged(object sender, WpfAppNew.OpenCv.Core.RecordingStatusChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                StartRecordButton.Content = e.IsRecording ? "停止录像" : "开始录像";
                //StartRecordButton.Foreground = e.IsRecording ?
                //    new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 107, 107)) :
                //    new SolidColorBrush(System.Windows.Media.Color.FromRgb(204, 204, 204));
            }));
        }

        /// <summary>
        /// 摄像头错误事件处理
        /// </summary>
        private void OnCameraErrorOccurred(object sender, WpfAppNew.OpenCv.Core.ErrorOccurredEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                //Growl.Error($"摄像头错误: {e.Message}");
                LogUtil.Error($"摄像头错误: {e.Message} - {e.Exception?.Message}");

                // 显示详细错误信息
                if (e.Exception != null)
                {
                    LogUtil.Error($"摄像头错误详细信息: {e.Exception}");
                }
            }));
        }

        /// <summary>
        /// 摄像头性能统计事件处理
        /// </summary>
        private void OnCameraPerformanceStats(object sender, WpfAppNew.OpenCv.Core.PerformanceStatsEventArgs e)
        {
            // 可以在这里处理性能统计信息
        }

        #endregion

        #region 控件事件处理

        /// <summary>
        /// 设备选择变更事件
        /// 将选择的设备同步到IndustrialCameraControl
        /// </summary>
        private async void DeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (DeviceComboBox.SelectedItem is CameraDevice selectedDevice && IndustrialCameraControl != null)
                {
                    // 同步设备选择到IndustrialCameraControl
                    IndustrialCameraControl.SelectedDevice = selectedDevice;
                    IndustrialCameraControl.FitToWindowCommand.Execute(null); // 可选：自动调整预览窗口大小 
                    LogUtil.Info($"设备选择已同步: {selectedDevice.Name}");
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"设备选择变更处理失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 刷新设备按钮点击事件
        /// 刷新设备列表并更新本地下拉框
        /// </summary>
        private async void RefreshDevicesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 显示加载指示器
                DeviceLoadingIndicator.Visibility = Visibility.Visible;
                RefreshDevicesButton.IsEnabled = false;

                // 执行IndustrialCameraControl的刷新命令
                IndustrialCameraControl?.RefreshDevicesCommand?.Execute(null);

                // 等待一小段时间让设备检测完成
                await Task.Delay(500);

                // 更新本地设备列表
                UpdateLocalDeviceList();

                LogUtil.Info("设备列表已刷新");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"刷新设备失败: {ex.Message}");
            }
            finally
            {
                // 隐藏加载指示器
                DeviceLoadingIndicator.Visibility = Visibility.Collapsed;
                RefreshDevicesButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// 更新本地设备下拉框列表
        /// 从IndustrialCameraControl同步设备列表，并根据保存的设备名称默认选中设备
        /// </summary>
        private void UpdateLocalDeviceList()
        {
            try
            {
                DeviceComboBox.Items.Clear();

                if (IndustrialCameraControl?.AvailableDevices != null)
                {
                    foreach (var device in IndustrialCameraControl.AvailableDevices)
                    {
                        DeviceComboBox.Items.Add(device);
                    }

                    // 根据保存的设备名称选择默认设备
                    CameraDevice selectedDevice = null;

                    // 首先尝试根据保存的设备名称查找
                    if (!string.IsNullOrEmpty(AppStatic.VideoConfig.VideoDecive))
                    {
                        selectedDevice = IndustrialCameraControl.AvailableDevices
                            .FirstOrDefault(d => d.Name == AppStatic.VideoConfig.VideoDecive);

                        if (selectedDevice != null)
                        {
                            LogUtil.Info($"找到保存的设备: {selectedDevice.Name}");
                        }
                        else
                        {
                            LogUtil.Warning($"未找到保存的设备: {AppStatic.VideoConfig.VideoDecive}，将选择第一个可用设备");
                        }
                    }

                    // 如果没有找到保存的设备或没有保存的设备配置，选择第一个可用设备
                    if (selectedDevice == null && IndustrialCameraControl.AvailableDevices.Count > 0)
                    {
                        selectedDevice = IndustrialCameraControl.AvailableDevices.First();
                        LogUtil.Info($"选择第一个可用设备: {selectedDevice.Name}");
                    }

                    // 设置选中的设备
                    if (selectedDevice != null)
                    {
                        DeviceComboBox.SelectedItem = selectedDevice;
                        // 同时更新IndustrialCameraControl的选中设备
                        IndustrialCameraControl.SelectedDevice = selectedDevice;
                    }
                }

                LogUtil.Info($"本地设备列表已更新，共 {DeviceComboBox.Items.Count} 个设备");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"更新本地设备列表失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 开始预览按钮点击事件
        /// </summary>
        private async void StartPreviewButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (IndustrialCameraControl.IsPreviewRunning)
                {
                    LogUtil.Info("停止预览");
                    IndustrialCameraControl.StopPreviewCommand?.Execute(null);
                }
                else
                {
                    // 检查是否已连接设备
                    if (!IndustrialCameraControl.IsConnected)
                    {
                        LogUtil.Info("设备未连接，请先连接设备");
                        return;
                    }

                    LogUtil.Info("开始预览");
                    IndustrialCameraControl.StartPreviewCommand?.Execute(null);
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"预览操作失败: {ex.Message}");
                LogUtil.Error($"异常详情: {ex}");
            }
        }

        /// <summary>
        /// 开始录像按钮点击事件
        /// </summary>
        private void StartRecordButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(AppVideoConfig.TempPath))
            {
                if (System.Windows.MessageBox.Show("视频路径未设置！请先配置", "系统提示", MessageBoxButton.OKCancel,
                        MessageBoxImage.Warning) == MessageBoxResult.OK)
                {
                    UCSetting setting = new UCSetting();
                    setting.Owner = AppStatic.MainWindow;
                    setting.ShowDialog();
                    return;
                }
            }

            if (!IndustrialCameraControl.IsPreviewRunning)
            {
                LogUtil.Warning("请先开始预览");
                return;
            }

            if (IndustrialCameraControl.IsRecording)
            {
                StopRecording();
            }
            else
            {
                StartRecording(AppVideoConfig.TempPath + $"Video_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");
            }
        }

        /// <summary>
        /// 开始录像
        /// </summary>
        private async void StartRecording(string path)
        {
            try
            {
                // 使用IndustrialCameraControl的录像功能
                IndustrialCameraControl.StartRecording(path);

                // 更新UI状态
                StartRecordButton.Content = "停止录像";
                RecordingTimePanel.Visibility = Visibility.Visible;
                _recordingTimer.Start(); // 继续使用定时器来更新UI显示

                LogUtil.Info("开始录像");
                VideoModel.ExposureModel = new Exposure() { IsAuto = VideoModel.ExposureModel.IsAuto, IsEnable = false };
            }
            catch (Exception ex)
            {
                LogUtil.Error($"启动录像失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 停止录像
        /// </summary>
        private async void StopRecording()
        {
            try
            {
                StartRecordButton.IsEnabled = false;
                StartRecordButton.Content = "正在停止..";

                // 使用IndustrialCameraControl停止录像
                var path = await IndustrialCameraControl.StopRecording();

                // 更新UI状态
                VideoModel.ExposureModel = new Exposure() { IsAuto = VideoModel.ExposureModel.IsAuto, IsEnable = true };
                StartRecordButton.Content = "开始录像";
                RecordingTimePanel.Visibility = Visibility.Hidden;
                _recordingTimer.Stop();
                var img = AppVideoConfig.TempThumbnailPath + $"{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                var scuess = await IndustrialCameraControl.CaptureImage(img);
                if (tInfo.Result.Vedios == null)
                    tInfo.Result.Vedios = new List<MediaItem>();

                var old = tInfo.Result;
                tInfo.Result = new TestResult();
                old.Vedios.Add(new MediaItem() { Source = path, Name = path.Replace(AppVideoConfig.TempPath, ""), IsEdit = false, ThumbnailSource = img });
                tInfo.Result = old;
                LogUtil.Info("录像已停止");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"停止录像失败: {ex.Message}");
            }
            finally
            {
                StartRecordButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// 拍照按钮点击事件
        /// </summary>
        private async void TakeSnapshotButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(AppVideoConfig.TempPath))
            {
                if (System.Windows.MessageBox.Show("拍照路径未设置！请先配置", "系统提示", MessageBoxButton.OKCancel,
                        MessageBoxImage.Warning) == MessageBoxResult.OK)
                {
                    UCSetting setting = new UCSetting();
                    setting.Owner = AppStatic.MainWindow;
                    setting.ShowDialog();
                    return;
                }
            }

            // 检查是否可以拍照
            if (!IndustrialCameraControl.CanCapture)
            {
                LogUtil.Warning("当前无法拍照，请确保设备已连接并正在预览");
                //Growl.Warning("当前无法拍照，请确保设备已连接并正在预览");
                return;
            }

            // 显示拍照进度指示器
            TakeSnapshotButton.IsEnabled = false;
            TakeSnapshotButton.Content = "📷 拍照中...";

            // 显示进度提示
            //Growl.Info("正在拍照，请稍候...");

            try
            {
                if (tInfo?.Result == null)
                {
                    tInfo.Result = new TestResult();
                    if (tInfo.Result.Images == null)
                        tInfo.Result.Images = new List<ImageItem>();
                }
                string fullName = $"0{tInfo.Result?.Images?.Count + 1}.{AppStatic.VideoConfig.ImageType}";
                // 使用IndustrialCameraControl的拍照功能
                var scuess = await IndustrialCameraControl.CaptureImage(AppVideoConfig.TempPath + fullName);
                if (scuess)
                {
                    LogUtil.Info("拍照完成");
                    //Growl.Success("拍照成功！");
                }
                else
                {
                    LogUtil.Error("拍照失败");
                    //Growl.Error("拍照失败");
                    return;
                }
                if (tInfo.Result.Images == null)
                    tInfo.Result.Images = new List<ImageItem>();

                var old = tInfo.Result;
                tInfo.Result = new TestResult();

                // 使用优化的图片加载方式，避免内存泄漏
                try
                {
                    using (var bitmap = new Bitmap(AppVideoConfig.TempPath + fullName))
                    {
                        var imageSource = bitmap.BitmapToImageSource();
                        old.Images.Add(new ImageItem() { ImageSource = imageSource, Name = fullName, IsEdit = false });
                    }
                }
                catch (Exception imgEx)
                {
                    LogUtil.Warning($"图片加载失败: {imgEx.Message}");
                    // 如果图片加载失败，仍然添加记录但不包含图像源
                    old.Images.Add(new ImageItem() { ImageSource = null, Name = fullName, IsEdit = false });
                }

                tInfo.Result = old;
            }
            catch (Exception exception)
            {
                LogUtil.Error($"拍照失败: {exception.Message}");
                Growl.Error($"拍照失败: {exception.Message}");
            }
            finally
            {
                // 恢复按钮状态
                TakeSnapshotButton.IsEnabled = true;
                TakeSnapshotButton.Content = "📷 拍照";
            }
        }


        /// <summary>
        /// 音频开启复选框选中事件
        /// </summary>
        private void ToggleButton_OnChecked(object sender, RoutedEventArgs e)
        {
            // 音频开启处理逻辑
        }

        /// <summary>
        /// 音频开启复选框取消选中事件
        /// </summary>
        private void ToggleButton_OnUnchecked(object sender, RoutedEventArgs e)
        {
            // 音频关闭处理逻辑
        }

        /// <summary>
        /// 遮罩按钮点击事件
        /// </summary>
        private void MaskButton_Click(object sender, RoutedEventArgs e)
        {
            // 遮罩功能处理逻辑
        }

        /// <summary>
        /// 截图按钮点击事件
        /// </summary>
        private void ScreenshotButton_Click(object sender, RoutedEventArgs e)
        {
            // 截图功能处理逻辑
        }

        #endregion

        #region 文件操作事件

        /// <summary>
        /// 图片点击事件
        /// </summary>
        private void FilesControl_ImagesClick(object sender, TestInfo e)
        {
            IndustrialCameraControl.StopPreviewCommand?.Execute(null);
            FilesControl.SelectedImageItem = (sender as UCFiles).SelectedImageItem;
            FrmBackModule pet = new FrmBackModule(new FrmPetImage(e, (sender as UCFiles).SelectedImageItem));
            pet.title.Text = "查看";
            pet.Owner = AppStatic.MainWindow;
            pet.ShowDialog();
            tInfo = new TestInfo();
            tInfo = e;
            IndustrialCameraControl.StartPreviewCommand?.Execute(null);
        }

        /// <summary>
        /// 视频点击事件
        /// </summary>
        private void FilesControl_VideoClick(object sender, MediaItem e)
        {
            //(AppStatic.uCVideo as UCLocalVideo)?.InitVodio(e.Source);
            //(AppStatic.uCVideo as UCLocalVideo).ShowDialog();
            IndustrialCameraControl.StopPreviewCommand?.Execute(null);
            UCOpenCvVideoPlayer uc = new UCOpenCvVideoPlayer(e.Source);
            uc.ShowDialog();
            IndustrialCameraControl.StartPreviewCommand?.Execute(null);
        }

        /// <summary>
        /// 截图保存事件
        /// </summary>
        private void Screenshot_Snapped(object sender, FunctionEventArgs<ImageSource> e)
        {
            var old = tInfo.Result;
            tInfo.Result = new TestResult();
            old.Images.Add(new ImageItem { Name = $"截图{DateTime.Now:yyyyMMddHHmmss}", ImageSource = e.Info });
            tInfo.Result = old;
        }

        #endregion

        #region 保存和打印

        /// <summary>
        /// 保存检查按钮点击事件
        /// </summary>
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (IndustrialCameraControl.IsRecording)
            {
                Growl.Warning("正在录像中，请先停止！");
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
            Growl.Success("保存成功！");
        }

        /// <summary>
        /// 打印按钮点击事件
        /// 优化：使用PrintNotesService获取预加载的UCPrintNotes实例，提升加载速度
        /// </summary>
        private async void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            // 检查模板文件是否存在
            if (!System.IO.File.Exists(tInfo.TestPath))
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    HandyControl.Controls.MessageBox.Warning("打印模板文件不存在！", "系统提示");
                });
                return;
            }
            
            try
            {

                // 在后台线程保存数据
                //await Task.Run(() =>
                //{
                //    TestLogic.Save(tInfo);
                //});
                TestLogic.Save(tInfo);
                // 确保UI操作在UI线程中执行
                await Dispatcher.InvokeAsync(() =>
                {
                    IndustrialCameraControl.StopPreviewCommand.Execute(null);
                    
                    // 显示加载指示器
                    if (DeviceLoadingIndicator != null)
                    {
                        DeviceLoadingIndicator.Visibility = Visibility.Visible;
                    }
                });
                // 异步预加载打印控件（如果还没有预加载）
                await PrintNotesService.Instance.PreloadPrintNotesAsync(tInfo);

                // 获取优化的UCPrintNotes实例
                var printNotes = await PrintNotesService.Instance.GetOrCreatePrintNotesAsync(tInfo);

                if (printNotes != null)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        FrmModule f = new FrmModule(printNotes);
                        f.Title = "打印报告";
                        f.ShowDialog();
                    });

                    LogUtil.Info($"打印报告窗口已打开: {tInfo.TestName}");
                }
                else
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        HandyControl.Controls.MessageBox.Error("创建打印控件失败！", "系统提示");
                    });
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"打印按钮点击处理失败: {ex.Message}");
                
                // 确保错误消息框在UI线程中显示
                await Dispatcher.InvokeAsync(() =>
                {
                    HandyControl.Controls.MessageBox.Error($"打开打印报告失败：{ex.Message}", "系统提示");
                });
            }
            finally
            {
                // 确保UI操作在UI线程中执行
                await Dispatcher.InvokeAsync(() =>
                {
                    // 隐藏加载指示器
                    if (DeviceLoadingIndicator != null)
                    {
                        DeviceLoadingIndicator.Visibility = Visibility.Collapsed;
                    }
                    IndustrialCameraControl.StartPreviewCommand.Execute(null);
                });
            }
        }

        #endregion

        #region 生命周期管理

        /// <summary>
        /// 用户控件加载事件
        /// </summary>
        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                //InitializeSystem();

                // 异步加载预加载的IndustrialCameraControl
                await LoadPreloadedCameraControlAsync();

                // 初始化设备列表
                InitializeDeviceList();
            }
            catch (Exception ex)
            {
                LogUtil.Error($"控件加载异常: {ex.Message}");
                //Growl.Error($"控件加载异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载预加载的IndustrialCameraControl
        /// </summary>
        private async Task LoadPreloadedCameraControlAsync()
        {
            try
            {
                LogUtil.Info("开始加载预加载的IndustrialCameraControl...");

                // 获取预加载的控件实例
                _industrialCameraControl = await IndustrialCameraPreloadService.Instance.GetOrCreateControlAsync();

                if (_industrialCameraControl != null)
                {
                    // 将控件添加到容器中
                    CameraControlContainer.Child = _industrialCameraControl;

                    // 隐藏加载指示器
                    CameraLoadingIndicator.Visibility = Visibility.Collapsed;

                    LogUtil.Info("预加载的IndustrialCameraControl加载成功");
                }
                else
                {
                    LogUtil.Error("获取预加载的IndustrialCameraControl失败");

                    // 创建新实例作为备用
                    _industrialCameraControl = new IndustrialCameraControl();
                    CameraControlContainer.Child = _industrialCameraControl;
                    CameraLoadingIndicator.Visibility = Visibility.Collapsed;

                    LogUtil.Info("使用新创建的IndustrialCameraControl实例");
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"加载IndustrialCameraControl异常: {ex.Message}");

                // 创建新实例作为备用
                try
                {
                    _industrialCameraControl = new IndustrialCameraControl();
                    CameraControlContainer.Child = _industrialCameraControl;
                    CameraLoadingIndicator.Visibility = Visibility.Collapsed;

                    LogUtil.Info("异常情况下使用新创建的IndustrialCameraControl实例");
                }
                catch (Exception createEx)
                {
                    LogUtil.Error($"创建备用IndustrialCameraControl实例失败: {createEx.Message}");
                    CameraLoadingIndicator.Visibility = Visibility.Collapsed;
                }
            }
        }

        /// <summary>
        /// 初始化设备列表
        /// 等待IndustrialCameraControl初始化完成，然后更新本地设备列表，并自动连接和开始预览
        /// </summary>
        private async void InitializeDeviceList()
        {
            try
            {
                // 确保控件已加载
                if (_industrialCameraControl == null)
                {
                    LogUtil.Warning("IndustrialCameraControl未加载，跳过设备初始化");
                    return;
                }

                // 延迟500ms，确保控件完全加载
                //await Task.Delay(500);

                // 刷新设备列表
                _industrialCameraControl.RefreshDevicesCommand?.Execute(null);

                // 等待设备列表更新
                //await Task.Delay(1000);

                // 获取设备列表
                var devices = await CameraInitializationService.Instance.GetDevicesAsync();
                LogUtil.Info($"检测到 {devices?.Count ?? 0} 个设备");

                if (devices != null && devices.Count > 0)
                {
                    // 自动选择第一个设备（通过UI操作）
                    // 注意：这里需要通过UI操作来选择设备，而不是直接调用方法
                    LogUtil.Info($"检测到设备: {string.Join(", ", devices)}");

                    // 延迟一下再开始预览
                    await Task.Delay(500);

                    // 开始预览（通过命令）
                    _industrialCameraControl.StartPreviewCommand?.Execute(null);
                    LogUtil.Info("自动开始预览");
                }

                // 更新本地设备列表
                UpdateLocalDeviceList();

                // 如果有选中的设备，自动连接和开始预览
                if (DeviceComboBox.SelectedItem is CameraDevice selectedDevice)
                {
                    LogUtil.Info($"开始自动连接设备: {selectedDevice.Name}");

                    // 自动连接设备
                    await AutoConnectAndStartPreview(selectedDevice);
                }

                LogUtil.Info("设备列表初始化完成");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"设备列表初始化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 自动连接设备并开始预览
        /// </summary>
        /// <param name="device">要连接的设备</param>
        private async Task AutoConnectAndStartPreview(CameraDevice device)
        {
            try
            {
                // 确保IndustrialCameraControl已选中正确的设备
                IndustrialCameraControl.SelectedDevice = device;

                // 等待设备设置完成
                await Task.Delay(200);

                // 连接设备
                LogUtil.Info($"正在连接设备: {device.Name}");
                if (IndustrialCameraControl.ConnectCommand?.CanExecute(null) == true)
                {
                    IndustrialCameraControl.ConnectCommand.Execute(null);

                    // 等待连接完成
                    await Task.Delay(1000);

                    // 检查连接状态并开始预览
                    if (IndustrialCameraControl.IsConnected)
                    {
                        LogUtil.Info($"设备连接成功: {device.Name}，开始预览");

                        // 开始预览
                        if (IndustrialCameraControl.StartPreviewCommand?.CanExecute(null) == true)
                        {
                            IndustrialCameraControl.StartPreviewCommand.Execute(null);

                            // 等待连接完成
                            await Task.Delay(1000);
                            IndustrialCameraControl.FitToWindowCommand.Execute(null); // 可选：自动调整预览窗口大小 
                            LogUtil.Info("自动预览已启动");
                        }
                        else
                        {
                            LogUtil.Warning("无法启动预览：StartPreviewCommand不可用");
                        }
                    }
                    else
                    {
                        LogUtil.Warning($"设备连接失败: {device.Name}");
                    }
                }
                else
                {
                    LogUtil.Warning("无法连接设备：ConnectCommand不可用");
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"自动连接和预览失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 关闭资源
        /// </summary>
        public async void Closed()
        {
            try
            {
                await Task.Run(() =>
                {

                    if (IndustrialCameraControl.IsPreviewRunning)
                    {
                        //IndustrialCameraControl?.StopPreviewCommand.Execute(null);
                        IndustrialCameraControl.Dispose();
                    }

                    // 停止定时器
                    _recordingTimer?.Stop();
                    _clickTimer?.Stop();

                    
                });
                // 处理图像资源
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

                // 删除指定目录下所有文件 
                ObjectExtension.DeleteDirectoryContents(AppVideoConfig.TempPath);
                tInfo = null;

                // 清理大对象堆（仅在必要时使用）
                if (GC.CollectionCount(2) < 1) // 检查大对象堆回收次数
                {
                    GC.Collect(2, GCCollectionMode.Forced); // 强制回收大对象堆
                    GC.WaitForPendingFinalizers();
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"资源释放异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 刷新界面
        /// </summary>
        public void Refresh()
        {
            // 刷新逻辑
        }

        #endregion

        /// <summary>
        /// 点击标题五次弹出性能监控页面和日志信息
        /// </summary>
        private void HeaderTextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _headerClickCount++;

            if (_headerClickCount == 1)
            {
                // 启动计时器
                _clickTimer.Start();
            }
            else if (_headerClickCount >= 5)
            {
                // 停止计时器
                _clickTimer.Stop();
                _headerClickCount = 0;

                // 显示性能监控窗口
                ShowPerformanceMonitor();
            }
        }

        /// <summary>
        /// 显示性能监控窗口
        /// </summary>
        private void ShowPerformanceMonitor()
        {
            try
            {
                //var performanceWindow = new PerformanceMonitorWindow(CameraPreview);
                //performanceWindow.Show();
            }
            catch (Exception ex)
            {
                LogUtil.Error($"显示性能监控窗口失败: {ex.Message}");
                //Growl.Error($"显示性能监控窗口失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 分辨率选择变更事件（功能已迁移到IndustrialCameraControl）
        /// </summary>
        private void ResolutionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 分辨率选择功能已迁移到IndustrialCameraControl控件
            LogUtil.Info("分辨率选择功能已迁移到IndustrialCameraControl控件");
        }

        /// <summary>
        /// 应用分辨率按钮点击事件（已由IndustrialCameraControl处理）
        /// </summary>
        private async void ApplyResolutionButton_Click(object sender, RoutedEventArgs e)
        {
            // 分辨率设置功能现在由IndustrialCameraControl内部处理
            // 这个方法保留以防XAML中仍有引用，但功能已迁移
            LogUtil.Info("分辨率设置功能已迁移到IndustrialCameraControl");
        }
    }
}