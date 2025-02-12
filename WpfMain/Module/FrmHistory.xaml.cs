using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Policy;
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
using WpfMain.Controlls;
using WpfMain.Entity;
using WpfMain.Logic;
using WpfMain.Module.PetModule;

namespace WpfMain.Module
{
    /// <summary>
    /// FrmHistory.xaml 的交互逻辑
    /// </summary>
    public partial class FrmHistory : UserControl
    {

        public static readonly DependencyProperty DataListProperty = DependencyProperty.Register(
            nameof(DataList), typeof(List<TestInfo>), typeof(FrmHistory), new PropertyMetadata(default(List<TestInfo>)));

        public List<TestInfo> DataList
        {
            get => (List<TestInfo>)GetValue(DataListProperty);
            set => SetValue(DataListProperty, value);
        }

        public FrmHistory()
        {
            InitializeComponent();

            startTime.Text = DateTime.Now.AddDays(-7).ToString("yyyy-MM-dd")+" 00:00:00";
            endTime.Text = DateTime.Now.ToString("yyyy-MM-dd") + " 23:59:59";
            InitData(); 
        }

        public void InitData()
        { 
            DataList = TestLogic.Load(DateTime.Parse(startTime.Text.Trim()), DateTime.Parse(endTime.Text.Trim()));
        }

        /// <summary>
        /// 点击查询
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            DateTime d;
            if (string.IsNullOrWhiteSpace(startTime.Text.Trim()) ||
                !DateTime.TryParse(startTime.Text.Trim().ToString(), out d))
            { 
                MessageBox.Show(AppStatic.MainWindow, "开始时间不能为空！", "系统提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(endTime.Text.Trim()) ||
                !DateTime.TryParse(endTime.Text.Trim().ToString(), out d))
            {
                MessageBox.Show(AppStatic.MainWindow, "结束时间不能为空！", "系统提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            } 
            InitData();
        }

        /// <summary>
        /// 查看影像
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void Img_OnClick(object sender, RoutedEventArgs e)
        {
           var entity= (TestInfo)((System.Windows.FrameworkElement)e.Source).Tag;
           FrmImgView view = new FrmImgView(entity);
           view.Owner = AppStatic.MainWindow;
           view.ShowDialog();
        }
    }

    //public class PropertyGridDataList
    //{
    //    public int Index { get; set; }
    //    public string Name { get; set; }
    //    public string Phone { get; set; }
    //    public string Sex { get; set; }
    //    public string DeviceName { get; set; }
    //    public string Remark { get; set; }
    //}
}
