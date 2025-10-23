using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Entity.Entity;

namespace WpfAppNew.Controlls
{
    /// <summary>
    /// TestInfo详情页面用户控件
    /// 用于显示宠物检查的详细信息，包括基本信息、图片和视频
    /// </summary>
    public partial class TestInfoDetailPage : UserControl, INotifyPropertyChanged
    {
        #region 私有字段
        private TestInfo _testInfo;
        private ImageItem _selectedImage;
        private MediaItem _selectedVideo;
        #endregion

        #region 构造函数
        /// <summary>
        /// 初始化TestInfo详情页面
        /// </summary>
        public TestInfoDetailPage()
        {
            InitializeComponent();
            this.DataContext = this;
            InitializeConverters();
        }
        #endregion

        #region 公共属性
        /// <summary>
        /// 获取或设置TestInfo数据源
        /// </summary>
        public TestInfo TestInfo
        {
            get => _testInfo;
            set
            {
                if (_testInfo != value)
                {
                    _testInfo = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasImages));
                    OnPropertyChanged(nameof(HasVideos));
                    
                    // 设置默认选中的图片和视频
                    if (_testInfo?.Result?.Images?.Count > 0)
                    {
                        SelectedImage = _testInfo.Result.Images.First();
                    }
                    
                    //if (_testInfo?.Result?.Vedios?.Count > 0)
                    //{
                    //    SelectedVideo = _testInfo.Result.Vedios.First();
                    //}
                }
            }
        }

        /// <summary>
        /// 获取或设置当前选中的图片
        /// </summary>
        public ImageItem SelectedImage
        {
            get => _selectedImage;
            set
            {
                if (_selectedImage != value)
                {
                    // 取消之前选中的图片
                    if (_selectedImage != null)
                        _selectedImage.IsSelected = false;
                    
                    _selectedImage = value;
                    
                    // 设置新选中的图片
                    if (_selectedImage != null)
                        _selectedImage.IsSelected = true;
                    
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 获取或设置当前选中的视频
        /// </summary>
        public MediaItem SelectedVideo
        {
            get => _selectedVideo;
            set
            {
                if (_selectedVideo != value)
                {
                    // 取消之前选中的视频
                    if (_selectedVideo != null)
                        _selectedVideo.IsSelected = false;
                    
                    _selectedVideo = value;
                    
                    // 设置新选中的视频
                    if (_selectedVideo != null)
                        _selectedVideo.IsSelected = true;
                    
                    OnPropertyChanged();
                    
                    // 更新视频播放器源
                    UpdateVideoSource();
                }
            }
        }

        /// <summary>
        /// 获取是否有图片数据
        /// </summary>
        public bool HasImages => TestInfo?.Result?.Images?.Count > 0;

        /// <summary>
        /// 获取是否没有图片数据（用于显示"暂无图片"文本）
        /// </summary>
        public bool NoImages => !HasImages;

        /// <summary>
        /// 获取是否有视频数据
        /// </summary>
        public bool HasVideos => TestInfo?.Result?.Vedios?.Count > 0;

        /// <summary>
        /// 获取是否没有视频数据（用于显示"暂无视频"文本）
        /// </summary>
        public bool NoVideos => !HasVideos;

        /// <summary>
        /// 获取病历号
        /// </summary>
        public string RecordNo => TestInfo?.RecordNo ?? "";

        /// <summary>
        /// 获取检查名称
        /// </summary>
        public string TestName => TestInfo?.TestName ?? "";

        /// <summary>
        /// 获取检查时间
        /// </summary>
        public DateTime TestDate => TestInfo?.TestDate ?? DateTime.Now;

        /// <summary>
        /// 获取宠物名
        /// </summary>
        public string Pet => TestInfo?.Pet ?? "";

        /// <summary>
        /// 获取宠主姓名
        /// </summary>
        public string Customer => TestInfo?.Customer ?? "";

        /// <summary>
        /// 获取联系电话
        /// </summary>
        public string CustomerPhone => TestInfo?.CustomerPhone ?? "";

        /// <summary>
        /// 获取性别
        /// </summary>
        public string Gender => TestInfo?.Gender ?? "";

        /// <summary>
        /// 获取宠物种类
        /// </summary>
        public string Type => TestInfo?.Type ?? "";

        /// <summary>
        /// 获取宠物品种
        /// </summary>
        public string Variety => TestInfo?.Variety ?? "";

        /// <summary>
        /// 获取年龄
        /// </summary>
        public string Age => TestInfo?.Age ?? "";

        /// <summary>
        /// 获取绝育状态
        /// </summary>
        public string Neuter => TestInfo?.Neuter ?? "";

        /// <summary>
        /// 获取检查医生
        /// </summary>
        public string DCOperation => TestInfo?.DCOperation ?? "";

        /// <summary>
        /// 获取大小
        /// </summary>
        public string Size => TestInfo?.Size ?? "";

        /// <summary>
        /// 获取数量
        /// </summary>
        public string Count => TestInfo?.Count ?? "";

        /// <summary>
        /// 获取所见
        /// </summary>
        public string See => TestInfo?.See ?? "";
        #endregion

        #region 私有方法
        /// <summary>
        /// 初始化转换器
        /// </summary>
        private void InitializeConverters()
        {
            //// 添加布尔值到可见性转换器
            //this.Resources.Add("BooleanToVisibilityConverter", new BooleanToVisibilityConverter());
            
            //// 添加布尔值到画刷转换器
            //this.Resources.Add("BooleanToBrushConverter", new BooleanToBrushConverter());
        }

        /// <summary>
        /// 更新视频播放器源
        /// </summary>
        private void UpdateVideoSource()
        {
            try
            {
                if (SelectedVideo != null && !string.IsNullOrEmpty(SelectedVideo.Source))
                {
                    VideoPlayer.Source = new Uri(SelectedVideo.Source, UriKind.RelativeOrAbsolute);
                }
                else
                {
                    VideoPlayer.Source = null;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"视频加载失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        #endregion

        #region 事件处理
        /// <summary>
        /// 缩略图图片点击事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void ThumbnailImage_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (sender is Image image && image.DataContext is ImageItem imageItem)
                {
                    SelectedImage = imageItem;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"图片选择失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// 缩略图视频点击事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void ThumbnailVideo_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (sender is Image image && image.DataContext is MediaItem videoItem)
                {
                    SelectedVideo = videoItem;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"视频选择失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// 播放按钮点击事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (VideoPlayer.Source != null)
                {
                    VideoPlayer.Play();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"视频播放失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// 暂停按钮点击事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                VideoPlayer.Pause();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"视频暂停失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// 停止按钮点击事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                VideoPlayer.Stop();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"视频停止失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// 视频媒体打开事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void VideoPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            // 视频加载完成后的处理逻辑
        }

        /// <summary>
        /// 视频播放结束事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void VideoPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            // 视频播放结束后的处理逻辑，可以选择重新播放或停止
            VideoPlayer.Position = TimeSpan.Zero;
        }
        #endregion

        #region INotifyPropertyChanged实现
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 触发属性变更通知
        /// </summary>
        /// <param name="propertyName">属性名称</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }

    /// <summary>
    /// 布尔值到画刷转换器
    /// 用于根据选中状态改变背景色
    /// </summary>
    public class BooleanToBrushConverter : IValueConverter
    {
        /// <summary>
        /// 将布尔值转换为画刷
        /// </summary>
        /// <param name="value">布尔值</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="parameter">参数</param>
        /// <param name="culture">文化信息</param>
        /// <returns>画刷对象</returns>
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool isSelected && isSelected)
            {
                return new SolidColorBrush(Color.FromRgb(0, 122, 204)); // 选中时的蓝色背景
            }
            return new SolidColorBrush(Colors.Transparent); // 未选中时透明背景
        }

        /// <summary>
        /// 反向转换（未实现）
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="parameter">参数</param>
        /// <param name="culture">文化信息</param>
        /// <returns>转换结果</returns>
        /// <exception cref="NotImplementedException">未实现异常</exception>
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}