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

namespace WpfMain.Controlls
{
    /// <summary>
    /// PropertyGridDemoCtl.xaml 的交互逻辑
    /// </summary>
    public partial class PropertyGridDemoCtl : UserControl
    {
        public PropertyGridDemoCtl()
        {
            InitializeComponent();
            DemoModel = new PropertyGridDemoModel
            {
                姓名 = "TestString",
                Gender = Gender.Female,
                绝育 = true,
                电话 = 98,
                杂项2 = VerticalAlignment.Stretch
            };
        }

        public static readonly DependencyProperty DemoModelProperty = DependencyProperty.Register(
            nameof(DemoModel), typeof(PropertyGridDemoModel), typeof(PropertyGridDemoCtl), new PropertyMetadata(default(PropertyGridDemoModel)));

        public PropertyGridDemoModel DemoModel
        {
            get => (PropertyGridDemoModel)GetValue(DemoModelProperty);
            set => SetValue(DemoModelProperty, value);
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
        public Gender Gender { get; set; }


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

    public enum Gender
    {
        Male,
        Female
    }
}
