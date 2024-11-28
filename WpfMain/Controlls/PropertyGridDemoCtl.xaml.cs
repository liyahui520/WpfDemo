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
                String = "TestString",
                Enum = Gender.Female,
                Boolean = true,
                Integer = 98,
                VerticalAlignment = VerticalAlignment.Stretch
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
}
