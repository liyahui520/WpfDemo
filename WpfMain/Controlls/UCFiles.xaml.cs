using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Entity.Entity; 

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


        public ImageItem SelectedImageItem { get; set; }

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
            RaiseSomeActionTriggered(ParentData);
        }


        private void UIElement_Video_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var wntity = (MediaItem)((System.Windows.FrameworkElement)sender).Tag;
            RaiseVideoActionTriggered(wntity);
        }
    }
}
