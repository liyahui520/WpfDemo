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
using Entity.Entity;
using Tools.App;

namespace CuPrint.PrintControlls
{
    /// <summary>
    /// PrintHeader.xaml 的交互逻辑
    /// </summary>
    public partial class PrintHeader : UserControl
    {
        public static readonly DependencyProperty TestInfoProperty = DependencyProperty.Register(
            nameof(tInfo), typeof(TestInfo), typeof(PrintHeader), new PropertyMetadata(default(TestInfo)));

        public TestInfo tInfo
        {
            get => (TestInfo)GetValue(TestInfoProperty);
            set => SetValue(TestInfoProperty, value);
        }
        public PrintHeader(TestInfo info)
        {
            InitializeComponent();
            hospitalName.Text = AppStatic.AppHospital.HospitalName;
            tInfo = info;
        }
    }
}
