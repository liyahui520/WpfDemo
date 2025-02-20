using Entity.Entity;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
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

namespace CuPrint
{
    /// <summary>
    /// UCPrint.xaml 的交互逻辑
    /// </summary>
    public partial class UCPrint : UserControl
    {
        public static readonly DependencyProperty DataListProperty = DependencyProperty.Register(
            nameof(DataList), typeof(List<TestInfo>), typeof(UCPrint), new PropertyMetadata(default(List<TestInfo>)));

        public List<TestInfo> DataList
        {
            get => (List<TestInfo>)GetValue(DataListProperty);
            set => SetValue(DataListProperty, value);
        }
        public UCPrint(List<TestInfo> dataList)
        {
            InitializeComponent();
           
        }
         
    }

     
}
