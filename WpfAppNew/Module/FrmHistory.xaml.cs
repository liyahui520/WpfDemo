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
using WpfAppNew.Logic;
using HandyControl.Controls;
using MessageBox = HandyControl.Controls.MessageBox;
using WpfAppNew.Controlls;
using Tools.Extend;
using WpfAppNew.EmguPlugs;
using WpfAppNew.Services;

namespace WpfAppNew.Module
{
    /// <summary>
    /// 宠物检查历史记录管理页面
    /// 提供历史记录查询、查看、删除等功能
    /// </summary>
    public partial class FrmHistory : UserControl
    {
        /// <summary>
        /// 构造函数
        /// 初始化页面并加载数据
        /// </summary>
        public FrmHistory()
        {
            InitializeComponent(); 
            Loaded += UserControl_Loaded;
        }

        /// <summary>
        /// 页面加载完成事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            InitData();
        }

        /// <summary>
        /// 初始化数据
        /// </summary>
        private void InitData()
        {
            // 加载检查历史数据 - 使用默认时间范围
            var startTime = DateTime.Today.AddDays(-30);
            var endTime = DateTime.Today.AddDays(1);
            var dataList = TestLogic.Load(startTime, endTime);
            
            // 绑定到DataGrid
            HistoryDataGrid.ItemsSource = dataList;
            
            // 更新状态栏
            StatusText.Text = "数据加载完成";
            RecordCountText.Text = $"共 {dataList.Count} 条记录";
        }

        /// <summary>
        /// 查询按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            // 获取搜索条件
            var startDate = StartDatePicker.SelectedDate ?? DateTime.Today.AddDays(-30);
            var endDate = EndDatePicker.SelectedDate ?? DateTime.Today.AddDays(1);
            var searchText = SearchTextBox.Text?.Trim();
            
            // 加载数据
            var allData = TestLogic.Load(startDate, endDate);
            var filteredData = allData.AsEnumerable();
            
            // 按关键词筛选
            if (!string.IsNullOrEmpty(searchText))
            {
                filteredData = filteredData.Where(x => 
                    x.Pet?.Contains(searchText) == true ||
                    x.RecordNo?.Contains(searchText) == true ||
                    x.Customer?.Contains(searchText) == true ||
                    x.TestName?.Contains(searchText) == true);
            }
            
            var resultList = filteredData.ToList();
            
            // 绑定筛选后的数据
            HistoryDataGrid.ItemsSource = resultList;
            
            // 更新状态栏
            StatusText.Text = "查询完成";
            RecordCountText.Text = $"共 {resultList.Count} 条记录";
            
            //MessageBox.Success($"查询完成，找到 {resultList.Count} 条记录！");
        }

        /// <summary>
        /// 刷新按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void RefreshButton_OnClick(object sender, RoutedEventArgs e)
        {
            InitData();
            //MessageBox.Success("数据刷新成功！");
        }

        /// <summary>
        /// 查询按钮点击事件（新UI）
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void QueryButton_OnClick(object sender, RoutedEventArgs e)
        {
            ButtonBase_OnClick(sender, e);
        }

        /// <summary>
        /// 查看图片按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void Img_OnClick(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            if (button?.DataContext is TestInfo testInfo)
            {
                var detail = new TestInfoDetailPage();
                detail.TestInfo = testInfo;
                FrmModule frm = new FrmModule(detail);
                frm.ShowDialog();
                //if (testInfo.Result?.Images?.Count > 0)
                //{
                //    // 显示图片查看窗口
                //    MessageBox.Info($"查看图片：{testInfo.TestName} - 共 {testInfo.Result.Images.Count} 张图片");
                //}
                //else
                //{
                //    MessageBox.Warning("该记录没有关联的图片！");
                //}
            }
        }

        /// <summary>
        /// 查看视频按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void Video_OnClick(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            if (button?.DataContext is TestInfo testInfo)
            {
                if (testInfo.Result?.Vedios?.Count > 0)
                {
                    MessageBox.Info($"查看视频：{testInfo.TestName} - 共 {testInfo.Result.Vedios.Count} 个视频");
                }
                else
                {
                    MessageBox.Warning("该记录没有关联的视频！");
                }
            }
        }

        /// <summary>
        /// 查看报告按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private async void ButtonBase_San_OnClick(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            if (button?.DataContext is TestInfo testInfo)
            {
                // 显示报告查看窗口
                try
                {
                    // 在后台线程保存数据
                    await Task.Run(() =>
                    {
                        TestLogic.Save(testInfo);
                    });
                     
                    // 确保UI操作在UI线程中执行
                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (!System.IO.File.Exists(testInfo.TestPath))
                        {
                            HandyControl.Controls.MessageBox.Warning("打印模板文件不存在！", "系统提示");
                            return;
                        }
                    });

                    // 异步预加载打印控件（如果还没有预加载）
                    await PrintNotesService.Instance.PreloadPrintNotesAsync(testInfo);

                    // 获取优化的UCPrintNotes实例
                    var printNotes = await PrintNotesService.Instance.GetOrCreatePrintNotesAsync(testInfo);

                    if (printNotes != null)
                    {
                        await Dispatcher.InvokeAsync(() =>
                        {
                            FrmModule f = new FrmModule(printNotes);
                            f.Title = "打印报告";
                            f.ShowDialog();
                        });

                        LogUtil.Info($"打印报告窗口已打开: {testInfo.TestName}");
                    }
                    else
                    {
                        await Dispatcher.InvokeAsync(() =>
                        {
                            HandyControl.Controls.MessageBox.Error("创建打印控件失败！", "系统提示");
                        });
                    }
                }
                catch (Exception ex)
                {
                    LogUtil.Error($"打印按钮点击处理失败: {ex.Message}");
                    
                    // 确保错误消息在UI线程中显示
                    await Dispatcher.InvokeAsync(() =>
                    {
                        HandyControl.Controls.MessageBox.Error($"打开打印报告失败：{ex.Message}", "系统提示");
                    });
                }
                finally
                { 
                }
            }
        }

        /// <summary>
        /// 删除记录按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void Delete_OnClick(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            if (button?.DataContext is TestInfo testInfo)
            {
                // 确认删除对话框
                var result = MessageBox.Ask($"确定要删除 {testInfo.Pet} 的检查记录吗？", "确认删除");
                if (result == MessageBoxResult.OK)
                {
                    try
                    {
                        // 删除记录
                        TestLogic.Delete(testInfo);

                        // 刷新数据
                        ButtonBase_OnClick(null, null);


                        MessageBox.Success("删除成功！");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Error($"删除失败：{ex.Message}");
                    }
                }
            }
        }
    }
}
