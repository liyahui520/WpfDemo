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
using Size = System.Windows.Size;

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
            ShowPrintPreview();
        }


        private void ShowPrintPreview()
        {
            // 创建打印内容 
            var header = new TextBlock { Text = "固定表头", FontSize = 20 };
            var footer = new TextBlock { Text = "第 {0} 页", FontSize = 12 };

            var contents = Enumerable.Range(1, 50)
                .Select(i => new StackPanel
                {
                    Children = {
                        new TextBlock { Text = $"内容项 {i}" },
                        new System.Windows.Controls.Image { Source = new BitmapImage(new Uri("https://i-blog.csdnimg.cn/blog_migrate/2c6942fcbe234b5b9ff65476e534f1ce.jpeg")),  Height = 50 }
                    }
                });

            // 生成文档 
            var print = new PrintHelper(header, contents, footer, new Size(794, 1123));
            var doc = new FixedDocument();
            foreach (var page in print._pages)
            {
                var pageContent = new PageContent();
                var fixedPage = new FixedPage();
                fixedPage.Children.Add(page);
                pageContent.Child = fixedPage;
                doc.Pages.Add(pageContent);
            }
            Viewer.Document = doc;

        }
    }


}
