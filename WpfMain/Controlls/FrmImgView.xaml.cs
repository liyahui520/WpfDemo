using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Entity.Entity;
using Tools.Extend;

namespace WpfMain.Controlls
{
    /// <summary>
    /// UCImgView.xaml 的交互逻辑
    /// </summary>
    public partial class FrmImgView 
    {
        public static readonly DependencyProperty ParentDataProperty = DependencyProperty.Register(
            "Data", typeof(TestInfo), typeof(FrmImgView), new PropertyMetadata(default(TestInfo)));

        public TestInfo Data
        {
            get => (TestInfo)GetValue(ParentDataProperty);
            set => SetValue(ParentDataProperty, value);
        }


        public new static readonly DependencyProperty NameProperty = DependencyProperty.Register(
            "SelectName", typeof(string), typeof(FrmImgView), new PropertyMetadata(default(string)));

        public string SelectName
        {
            get => (string)GetValue(NameProperty);
            set => SetValue(NameProperty, value);
        }

        public FrmImgView(TestInfo testInfo)
        {
            InitializeComponent();
            Data = testInfo;
            DataContext = Data;
            imgView.Height = SystemParameters.PrimaryScreenHeight - 30;
            imgView.Width = SystemParameters.PrimaryScreenWidth - 200;
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// 点击图片
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void UIElement_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var entity = (ImageItem)((System.Windows.FrameworkElement)e.Source).DataContext;
            SelectName = entity.Name;
            imgView.ImageSource = BitmapFrame.Create(entity.BitBuffer.FromByteArray());
            //imgView.ImageSource = entity.BitBuffer.Byte2Bitmap();
        }
    }
}
