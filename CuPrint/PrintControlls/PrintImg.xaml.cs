using Entity.Entity;
using System;
using System.Collections.Generic;
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

namespace CuPrint.PrintControlls
{
    /// <summary>
    /// PrintImg.xaml 的交互逻辑
    /// </summary>
    public partial class PrintImg : UserControl
    {
        public static readonly DependencyProperty ParentDataProperty = DependencyProperty.Register(
            "ParentData", typeof(TestInfo), typeof(PrintImg), new PropertyMetadata(default(TestInfo)));

        public TestInfo ParentData
        {
            get => (TestInfo)GetValue(ParentDataProperty);
            set => SetValue(ParentDataProperty, value);
        }

        public PrintImg(TestInfo info)
        {
            InitializeComponent();
            ParentData =info;
        }
    }
}
