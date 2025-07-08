using CuPrint;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Entity.Entity;
using Newtonsoft.Json;
using Tools.App;
using WpfMain.Controlls;
using WpfMain.Logic;
using System.Windows.Forms;

namespace WpfMain.Module
{
    /// <summary>
    /// FrmHistory.xaml 的交互逻辑
    /// </summary>
    public partial class FrmHistory : System.Windows.Controls.UserControl
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

            startTime.Text = DateTime.Now.AddDays(-7).ToString("yyyy-MM-dd") + " 00:00:00";
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
                HandyControl.Controls.MessageBox.Error("开始时间不能为空！", "系统提示");
                return;
            }
            if (string.IsNullOrWhiteSpace(endTime.Text.Trim()) ||
                !DateTime.TryParse(endTime.Text.Trim().ToString(), out d))
            {
                HandyControl.Controls.MessageBox.Error("结束时间不能为空！", "系统提示");
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
            var entity = (TestInfo)((System.Windows.FrameworkElement)e.Source).Tag;
            FrmImgView view = new FrmImgView(entity);
            view.Owner = AppStatic.MainWindow;
            view.ShowDialog();
        }

        /// <summary>
        /// 查看报告
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void ButtonBase_San_OnClick(object sender, RoutedEventArgs e)
        {
            var entity = (TestInfo)(((System.Windows.FrameworkElement)sender).Tag);
            //FrmModule frm = new FrmModule(new UCPrint(entity));
            //frm.ShowDialog();
            if (!System.IO.File.Exists(entity.TestPath))
            {
                HandyControl.Controls.MessageBox.Error( "打印模板文件不存在！", "系统提示");
                return;
            }

            FrmModule f = new FrmModule(new UCPrintNotes(entity));
            f.Title = "打印模板";
            f.ShowDialog();

        }

        private void Delete_OnClick(object sender, RoutedEventArgs e)
        {
            if (HandyControl.Controls.MessageBox.Ask("确定删除当前记录吗？", "系统提示") ==
                MessageBoxResult.OK)
            {
                var entity = (TestInfo)((System.Windows.FrameworkElement)e.Source).Tag;
                string name = $"{entity.TestDate:yyyyMMddHHmmss}_{entity.Id}";
                string jsonfileName = Path.Combine(TestLogic.JsonDataPath, $"{name}.json");
                File.Delete(jsonfileName);
                TestLogic.Delete(entity);
                HandyControl.Controls.MessageBox.Success("删除成功！", "系统提示");
                InitData();
            }

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
