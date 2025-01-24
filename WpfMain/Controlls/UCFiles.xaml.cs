using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfMain.Entity;

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

        #endregion

        public static readonly DependencyProperty ParentDataProperty = DependencyProperty.Register(
            "ParentData", typeof(TestInfo), typeof(UCFiles), new PropertyMetadata(default(TestInfo)));

        public TestInfo ParentData
        {
            get => (TestInfo)GetValue(ParentDataProperty);
            set => SetValue(ParentDataProperty, value);
        }

        private void MenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            if (ParentData != null)
            {
                var entity = (ImageItem)((System.Windows.FrameworkElement)e.Source).DataContext;
                var old = ParentData.Result;
                ParentData.Result = new TestResult();
                old.Images.Remove(entity);
                ParentData.Result = old;
            }
        }

        private void UIElement_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        { 
            RaiseSomeActionTriggered(ParentData);
        }
    }
}
