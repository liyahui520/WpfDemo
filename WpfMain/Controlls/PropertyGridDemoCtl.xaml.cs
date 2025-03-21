using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Entity.Entity;
using Entity.Enum;
using Tools.App;
using Tools.Extend;

namespace WpfMain.Controlls
{
    /// <summary>
    /// PropertyGridDemoCtl.xaml 的交互逻辑
    /// </summary>
    public partial class PropertyGridDemoCtl : UserControl
    {

        public static readonly DependencyProperty ParentDataProperty = DependencyProperty.Register(
            "ParentData", typeof(TestInfo), typeof(PropertyGridDemoCtl), new PropertyMetadata(default(TestInfo)));

        public TestInfo ParentData
        {
            get => (TestInfo)GetValue(ParentDataProperty);
            set => SetValue(ParentDataProperty, value);
        }

        public PropertyGridDemoCtl()
        {
            InitializeComponent();
            DemoModel = new PropertyGridDemoModel
            {
                姓名 = "TestString",
                性别 = GenderEnum.公,
                绝育 = true,
                电话 = 98,
                杂项2 = VerticalAlignment.Stretch
            };
            gender.ItemsSource = ObjectExtension.GetEnumDescriptions<GenderEnum>();

            baogaos.ItemsSource = new List<string>() { "1", "2" };

            petTypes.ItemsSource = AppStatic.PetInfo.PetTypes;
            if (AppStatic.PetInfo?.PetTypes.Count > 0)
            {
                petTypes.SelectedItem = AppStatic.PetInfo.PetTypes[0];
                if (ParentData != null)
                    ParentData.Type = AppStatic.PetInfo.PetTypes[0].Name;
                //if (AppStatic.PetInfo.PetTypes[0]?.PetVarietys.Count > 0)
                //{
                //    petVariety.SelectedItem = AppStatic.PetInfo.PetTypes[0].PetVarietys[0];
                //    petVariety.SelectedValue = AppStatic.PetInfo.PetTypes[0].PetVarietys[0].Name;
                //}
            }
        }

        public static readonly DependencyProperty DemoModelProperty = DependencyProperty.Register(
            nameof(DemoModel), typeof(PropertyGridDemoModel), typeof(PropertyGridDemoCtl), new PropertyMetadata(default(PropertyGridDemoModel)));

        public PropertyGridDemoModel DemoModel
        {
            get => (PropertyGridDemoModel)GetValue(DemoModelProperty);
            set => SetValue(DemoModelProperty, value);
        }

        private void PetTypes_OnSelected(object sender, RoutedEventArgs e)
        {
            if (petTypes.SelectedItem == null || ParentData == null) return;
            petVariety.ItemsSource = ((PetType)petTypes.SelectedItem).PetVarietys;
            if (((PetType)petTypes.SelectedItem).PetVarietys.Count > 0)
            {
                petVariety.SelectedItem = ((PetType)petTypes.SelectedItem).PetVarietys[0]; 
                ParentData.Variety = ((PetType)petTypes.SelectedItem).PetVarietys[0].Name;
            }
        }
    }



    public class PropertyGridDemoModel
    {
        [Category("0宠主")]
        public string 姓名 { get; set; }

        [Category("0宠主")]
        public int 电话 { get; set; }

        [Category("1宠物信息")]
        public bool 绝育 { get; set; }

        [Category("1宠物信息")]
        public GenderEnum 性别 { get; set; }


        [Category("2杂项")]
        public HorizontalAlignment 杂项1 { get; set; }
        [Category("2杂项")]
        public VerticalAlignment 杂项2 { get; set; }
        [Category("2杂项")]
        public ImageSource 头像 { get; set; }



        [Category("3检查")]
        public string 检查名称 { get; set; }

        [Category("3检查")]
        public string 检查医生 { get; set; }

        [Category("3检查")]
        public string 所见 { get; set; }

        [Category("3检查")]
        public string 备注 { get; set; }

        [Category("3检查")]
        public string 大小 { get; set; }

        [Category("3检查")]
        public int 数量 { get; set; }
    }
}
