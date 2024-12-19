using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using AForge.Video.DirectShow;
using HandyControl.Data;

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
            FrmModule pet = new FrmModule(new FrmPetImage());
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
            VideoEntity.ExposureModel.IsAuto = true;
            VideoEntity.ExposureModel.IsEdit = false;
            VideoEntity.ExposureModel.Value = 0;
            Video?.OnVideoSetCamera(VideoProcAmpProperty.Brightness, 0, VideoProcAmpFlags.Auto);
        }

        private void CameraUCSetting_OnUnchecked(object sender, RoutedEventArgs e)
        {
            VideoEntity.ExposureModel.IsAuto = false;
            VideoEntity.ExposureModel.IsEdit = true;
            VideoEntity.ExposureModel.Value = 0;
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