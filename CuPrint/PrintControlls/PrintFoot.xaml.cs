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
    /// PrintFoot.xaml 的交互逻辑
    /// </summary>
    public partial class PrintFoot : UserControl
    {
        public static readonly DependencyProperty TestInfoProperty = DependencyProperty.Register(
            nameof(tInfo), typeof(TestInfo), typeof(PrintFoot), new PropertyMetadata(default(TestInfo)));

        public TestInfo tInfo
        {
            get => (TestInfo)GetValue(TestInfoProperty);
            set => SetValue(TestInfoProperty, value);
        }
        public PrintFoot(TestInfo info,int pageIndex)
        {
            InitializeComponent(); 
            tInfo = info;
            pageCount.Text = "第"+pageIndex.ToString()+"页";
            DataContext = info; 
    }
    }
}
