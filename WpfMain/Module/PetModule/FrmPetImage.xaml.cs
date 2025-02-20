using DrawTools;
using PacsCore;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Entity.Entity;
using Tools.App;
using WpfMain.Logic;

namespace WpfMain.Module.PetModule
{
    /// <summary>
    /// FrmPet.xaml 的交互逻辑
    /// </summary>
    public partial class FrmPetImage : UserControl
    {
        public List<object> ResolutionDataList = new List<object>();
        //private TestInfo tInfo;
        public UCImageItemView UCD
        {
            get => (UCImageItemView)GetValue(UCDProperty);
            set => SetValue(UCDProperty, value);
        }
        public TestInfo tInfos
        {
            get => (TestInfo)GetValue(TestInfoProperty);
            set => SetValue(TestInfoProperty, value);
        }
        public PropertyGridDemoModel DemoModel1
        {
            get => (PropertyGridDemoModel)GetValue(DemoModel1Property);
            set => SetValue(DemoModel1Property, value);
        }



        public static readonly DependencyProperty DemoModel1Property = DependencyProperty.Register(
            nameof(DemoModel1), typeof(PropertyGridDemoModel), typeof(FrmPet), new PropertyMetadata(default(PropertyGridDemoModel)));

        public static readonly DependencyProperty TestInfoProperty = DependencyProperty.Register(
            nameof(tInfos), typeof(TestInfo), typeof(FrmPet), new PropertyMetadata(default(TestInfo)));

        public static readonly DependencyProperty UCDProperty = DependencyProperty.Register(
    nameof(UCD), typeof(UCImageItemView), typeof(FrmPetImage));


        public FrmPetImage(TestInfo info)
        {
            tInfos = info;
            InitializeComponent();
            DemoModel1 = new PropertyGridDemoModel
            {
                String = "TestString",
                Enum = Gender.Female,
                Boolean = true,
                Integer = 98,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            UCD = new UCImageItemView(tInfos);
            BorderImageContent.Child = UCD;

            ColorPicker.SelectedColorChanged += ColorPicker_SelectedColorChanged;
            HandyControl.Controls.Screenshot.Snapped += Screenshot_Snapped;
            DataContext = this;
        }



        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Click_Zoom(object sender, RoutedEventArgs e)
        {
            //if (UCD != null)
            //    UCD.Zoom += double.Parse(((Control)sender).Tag.ToString());
            if (UCD != null)
                UCD.DowheelZoom(double.Parse(((Control)sender).Tag.ToString()));
        }

        private void Button_Click_huanyuan(object sender, RoutedEventArgs e)
        {
            if (UCD != null)
                UCD.Reduction();
        }

        private void Button_Click_Duibi(object sender, RoutedEventArgs e)
        {
            if (UCD != null)
                UCD.Threshold += int.Parse(((Control)sender).Tag.ToString());

        }

        private void Button_Click_xuanzhuan(object sender, RoutedEventArgs e)
        {
            if (UCD != null)
                UCD.Rotate(90);
        }

        private void Button_Click_fanzhuan(object sender, RoutedEventArgs e)
        {
            UCD.Flip();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (UCD == null)
                return;

            if (!(sender is Button bt))
                return;

            string tag = bt.Tag.ToString();

            if (tag == "Clear")
            {
                UCD.Clear();
                return;
            }
            UCD.Draw((DrawToolType)Enum.Parse(typeof(DrawToolType), bt.Tag.ToString()));
        }

        private void Button_Click_save(object sender, RoutedEventArgs e)
        {
            if (UCD != null)
                UCD.SaveImage();
        }



        /// <summary>
        /// 打开对比图片
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Click_Comparison(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog odf = new System.Windows.Forms.OpenFileDialog();
            odf.Filter = "png文件(*.png;*.PNG)|*.png;*.PNG|JPG(*.jpg;*.jpeg)|*.jpg;*.jpeg";
            if (odf.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            BorderImageDuibi.Background = new ImageBrush { ImageSource = new BitmapImage(new Uri(odf.FileName)) };
            GridRowContent2.Height = new GridLength(5, GridUnitType.Star);
        }

        /// <summary>
        /// 关闭图片对比
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Click_CloseCmp(object sender, RoutedEventArgs e)
        {
            GridRowContent2.Height = new GridLength(0, GridUnitType.Star);
        }

        /// <summary>
        /// 打开/并闭 拾色器
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Click_Coloe(object sender, RoutedEventArgs e)
        {
            ColorPicker.Visibility = ColorPicker.Visibility == Visibility.Visible ? Visibility.Hidden : Visibility.Visible;
        }
        /// <summary>
        /// 设置涂鸦画笔颜色
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void ColorPicker_SelectedColorChanged(object sender, HandyControl.Data.FunctionEventArgs<Color> e)
        {
            ButtonColor.Background = ColorPicker.SelectedBrush;
            if (UCD != null)
                UCD.SetDrawingCanvasPinfo("Brush", ColorPicker.SelectedBrush);
        }

        /// <summary>
        /// 设置涂鸦画笔 粗细
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NumericUpDown_ValueChanged(object sender, HandyControl.Data.FunctionEventArgs<double> e)
        {
            if (UCD != null)
                UCD.SetDrawingCanvasPinfo("StrokeThickness", e.Info);
        }

        /// <summary>
        /// 保存自带的截图功能的图片
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Screenshot_Snapped(object sender, HandyControl.Data.FunctionEventArgs<ImageSource> e)
        {
            var old = tInfos.Result;
            tInfos.Result = new TestResult();
            old.Images.Add(new ImageItem { Name = $"截图{DateTime.Now:yyyyMMddHHmmss}", ImageSource = e.Info });
            tInfos.Result = old;
        }

        /// <summary>
        /// 对比度拖拽事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RangeBase_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (UCD != null)
                UCD.Threshold = Convert.ToInt32(e.NewValue);
        }

        /// <summary>
        /// 保存检查
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_SaveTest(object sender, RoutedEventArgs e)
        { 
            TestLogic.Save(tInfos);
            MessageBox.Show(AppStatic.MainWindow, "保存成功！", "系统提示", MessageBoxButton.OK, MessageBoxImage.None);
        }

        private void UCFiles_ImagesClick(object sender, TestInfo e)
        {

        }
    }
}

