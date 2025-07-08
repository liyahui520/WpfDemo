using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Entity.Entity;
using HandyControl.Controls;
using Newtonsoft.Json.Linq;

namespace WpfMain.Controlls
{
    /// <summary>
    /// UCFiles.xaml 的交互逻辑
    /// </summary>
    public partial class UCFiles : UserControl
    {
        public UCFiles()
        {
            InitializeComponent();
        }

        #region 点击事件

        public event EventHandler<TestInfo> ImagesClick;

        private void RaiseSomeActionTriggered(TestInfo entity)
        {
            ImagesClick?.Invoke(this, entity);
        }


        public event EventHandler<MediaItem> VideoClick;

        private void RaiseVideoActionTriggered(MediaItem entity)
        {
            VideoClick?.Invoke(this, entity);
        }

        #endregion

        public static readonly DependencyProperty ParentDataProperty = DependencyProperty.Register(
            "ParentData", typeof(TestInfo), typeof(UCFiles), new PropertyMetadata(default(TestInfo)));

        public TestInfo ParentData
        {
            get => (TestInfo)GetValue(ParentDataProperty);
            set => SetValue(ParentDataProperty, value);
        }

        private ImageItem _selectedImageItem;

        public ImageItem SelectedImageItem
        {
            get
            {
                return _selectedImageItem;
            }
            set
            {
                ImgListBox.SelectedItem = value;
                _selectedImageItem = value;
            }
        }

        private MediaItem _selectedVideoItem;

        public MediaItem SelectedVideoItem
        {
            get
            {
                return _selectedVideoItem;
            }
            set
            {
                videoList.SelectedItem = value;
                _selectedVideoItem = value;
            }
        }

        private void MenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            if (ParentData != null)
            {
                SelectedImageItem = (ImageItem)((System.Windows.FrameworkElement)e.Source).DataContext;
                var old = ParentData.Result;
                ParentData.Result = new TestResult();
                old.Images.Remove(SelectedImageItem);
                ParentData.Result = old;
            }
        }

        private void UIElement_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            SelectedImageItem = (sender as HandyControl.Controls.Card)?.DataContext as ImageItem;
            SelectedImageItem.IsSelected = !SelectedImageItem.IsSelected;
            RaiseSomeActionTriggered(ParentData);
        }


        private void UIElement_Video_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var wntity = (MediaItem)((System.Windows.FrameworkElement)sender).Tag;
            RaiseVideoActionTriggered(wntity);
        }

        private void Close_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (ParentData != null)
            {
                SelectedImageItem = (ImageItem)((System.Windows.FrameworkElement)e.Source).DataContext;
                var old = ParentData.Result;
                // 释放BitmapSource资源
                SelectedImageItem.ImageSource.Freeze();
                ParentData.Result = new TestResult();
                old.Images.Remove(SelectedImageItem);
                ParentData.Result = old;
                Growl.Success("删除成功");
            }
        }

        private void Video_Close_OnMouseLeftButtonDown(object sender, RoutedEventArgs e)
        {
            if (ParentData != null)
            {
                SelectedVideoItem = (MediaItem)((System.Windows.FrameworkElement)e.Source).DataContext;
                var old = ParentData.Result; 
                ParentData.Result = new TestResult();
                old.Vedios.Remove(SelectedVideoItem);
                ParentData.Result = old;
                Growl.Success("删除成功");
            }
        }

        private void Close_OnMouseLeftButtonDown(object sender, RoutedEventArgs e)
        {
            if (ParentData != null)
            {
                SelectedImageItem = (ImageItem)((System.Windows.FrameworkElement)e.Source).DataContext;
                var old = ParentData.Result;
                ParentData.Result = new TestResult();
                old.Images.Remove(SelectedImageItem);
                ParentData.Result = old;
                Growl.Success("删除成功");
            }
        }

        private void ToggleButton_OnChecked(object sender, RoutedEventArgs e)
        {
            if (ParentData != null)
            { 
                ParentData.Result.Images.ForEach(s => s.IsSelected = true); 
            }
        }

        private void ToggleButton_OnUnchecked(object sender, RoutedEventArgs e)
        {
            if (ParentData != null)
            {
                ParentData.Result.Images.ForEach(s => s.IsSelected = false);
            }
        }
    }
}
