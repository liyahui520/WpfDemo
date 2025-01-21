using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AForge.Video.DirectShow;
using WpfMain.Entity;

namespace WpfMain.Module.PetModule
{
    /// <summary>
    /// FrmPet.xaml 的交互逻辑
    /// </summary>
    public partial class FrmPet : UserControl
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
        private TestInfo tInfo;


        public PropertyVideoModel VideoEntity = new PropertyVideoModel();
        public static readonly DependencyProperty VideoEntityProperty = DependencyProperty.Register(
            nameof(VideoModel), typeof(PropertyVideoModel), typeof(FrmPet), new PropertyMetadata(default(PropertyVideoModel)));

        public PropertyVideoModel VideoModel
        {
            get => (PropertyVideoModel)GetValue(VideoEntityProperty);
            set => SetValue(VideoEntityProperty, value);
        }

        public FrmPet()
        {
            InitializeComponent();
            VideoModel = new PropertyVideoModel();
            DataContext = this;
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            
        }

        private void Image_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (tInfo == null)
            {
                tInfo = new TestInfo();
                tInfo.Result = new TestResult();
                tInfo.Result.Images = new System.Collections.Generic.List<ImageItem>
                {
                    new ImageItem { ImageSource = ImageTest.Source }
                };
            }

            FrmModule pet = new FrmModule(new FrmPetImage(tInfo));
            pet.title.Text = "查看";
            
            pet.ShowDialog();
        } 

        /// <summary>
        /// 设置曝光
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CameraUC_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Video?.OnVideoSetCamera(VideoProcAmpProperty.Brightness, int.Parse(e.NewValue.ToString()),VideoProcAmpFlags.Manual);
        }

        /// <summary>
        /// 自动选择
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void CameraUCSetting_OnChecked(object sender, RoutedEventArgs e)
        {
            //VideoEntity.ExposureModel.IsAuto = true;
            //VideoEntity.ExposureModel.IsEdit = false;
            //VideoEntity.ExposureModel.Value = 0;
            //Video?.OnVideoSetCamera(VideoProcAmpProperty.Brightness, 0, VideoProcAmpFlags.Auto);
        }

        private void CameraUCSetting_OnUnchecked(object sender, RoutedEventArgs e)
        {
            VideoEntity.ExposureModel.IsAuto = false;
            VideoEntity.ExposureModel.IsEdit = true;
            VideoEntity.ExposureModel.Value = 0;
        }

        private void StartCamp_OnClick(object sender, RoutedEventArgs e)
        {
            if (Video.isStart)
            {
                Video?.End();
                StartCamp.Content = "开始录像";
            }
            else
            {

                Video?.Start();
                StartCamp.Content = "停止录像";
            }
        }

        private void StopCamp_OnClick(object sender, RoutedEventArgs e)
        {
            Video?.Stop();
        }

        private void EndCamp_OnClick(object sender, RoutedEventArgs e)
        {
            Video?.End();
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

public class PropertyVideoModel
{
    public Exposure ExposureModel { get; set; }=new Exposure();
}

public class Exposure
{
    public bool IsAuto { get; set; } = true;

    public bool IsEdit { get; set; } = false;

    public double Value { get; set; } = 0;
}