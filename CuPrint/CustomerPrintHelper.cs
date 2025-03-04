using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows;
using System.Windows.Media;
using System.Xaml;
using System.Xml;
using CuPrint.PrintControlls;
using Entity.Entity;
using Tools.Extend;
using System.Windows.Media.Imaging;

namespace CuPrint
{
    public class CustomerPrintHelper : DocumentPaginator
    {
        private readonly FrameworkElement _header;
        private readonly FrameworkElement _footer;
        public readonly List<FrameworkElement> _pages = new List<FrameworkElement>();
        public CustomerPrintHelper(TestInfo data, Size pageSize)
        {
            CreateFixedDocument(data, pageSize);
        }

        public void CreateFixedDocument(TestInfo data, Size pageSize)
        {
            PageSize = pageSize;
            double currentHeight = 0;
            var currentPage = CreatePageContainer();
            // 添加固定表头  
            var cus_header = new PrintHeader(data);
            cus_header.Measure(pageSize);
            currentPage.Children.Add(cus_header);
            currentHeight += cus_header.DesiredSize.Height;

            var a = ObjectExtension.ChunkBy(data.Result.Images, 2).ToList();
            List<PrintImg> panels = new List<PrintImg>();
            a.ForEach(o =>
            {
                //var p = new System.Windows.Controls.StackPanel(){Margin = new Thickness(40,1,20,1), Orientation = Orientation.Horizontal,Background = Brushes.Red};
                var img = new PrintImg(o);
                panels.Add(img);
                //o.ForEach(i =>
                //{

                //    //p.Children.Add(new System.Windows.Controls.Image { Source = i.ImageSource, Height = 200, Width = 220, Margin = new Thickness(10, 0, 0, 0) })
                //});
                //panels.Add(p);
            });
            //      .Select<List<ImageItem>, WrapPanel>(o =>
            //{
            //    var a = new WrapPanel();
            //    o.Select<ImageItem, UIElementCollection>(i =>
            //    {
            //         a.Children.Add(new System.Windows.Controls.Image
            //        { Source = i.ImageSource, Height = 100, Width = 120 });
            //         return a.Children;
            //    });
            //    return a;
            //});
            int page = 1;
            var cus_footer = new PrintFoot(data, page);
            cus_footer.Measure(pageSize);
            currentHeight += cus_footer.DesiredSize.Height;
            // 分页处理 
            foreach (var content in panels)
            {
                content.Measure(pageSize);
                if (currentHeight + content.DesiredSize.Height > pageSize.Height - 10)
                {
                    var panelHeight = (pageSize.Height - 10) - (currentHeight + content.DesiredSize.Height);
                    if (panelHeight > 0)
                    {
                        var sp = new StackPanel
                        {
                            Width = 794,
                            Height = panelHeight
                        };
                        sp.Measure(pageSize);
                        currentPage.Children.Add(sp);
                    }
                    // 添加固定表尾  
                    cus_footer = new PrintFoot(data, page);
                    cus_footer.Measure(pageSize);
                    currentPage.Children.Add(cus_footer);
                    FinalizePage(currentPage, pageSize);
                    page++;
                    currentPage = CreatePageContainer();
                    currentHeight = 0;
                    // 添加固定表头 
                    cus_header = new PrintHeader(data);
                    cus_header.Measure(pageSize);
                    currentPage.Children.Add(cus_header);
                    currentHeight += cus_header.DesiredSize.Height + cus_footer.DesiredSize.Height;
                }
                currentPage.Children.Add(content);
                currentHeight += content.DesiredSize.Height;
            }
            // 添加固定表尾  
            cus_footer = new PrintFoot(data, page);
            cus_footer.Measure(pageSize);
            var panelHeight1 = (pageSize.Height - 10) - currentHeight - cus_footer.DesiredSize.Height;
            if (panelHeight1 > 0)
            {
                var sp = new StackPanel
                {
                    Width = 794,
                    Height = panelHeight1
                };
                sp.Measure(pageSize);
                currentPage.Children.Add(sp);
            }
            currentPage.Children.Add(cus_footer);
            FinalizePage(currentPage, pageSize);
        }

        // 预编译属性复制委托（示例）
        private static Action<FrameworkElement, FrameworkElement> _cloneDelegate;


        public static FrameworkElement DeepCopyFrameworkElement(FrameworkElement source)
        {
            return DeepCopy(source);
            // 创建序列化和反序列化的设置 
            var settings = new XamlReaderSettings();
            // 这里可以根据需要添加更多设置 
            // 例如，设置资源字典的解析行为 
            settings.IgnoreUidsOnPropertyElements = true;

            try
            {
                // 将 FrameworkElement 序列化为 XAML 字符串 
                string xaml = System.Windows.Markup.XamlWriter.Save(source);
                // 使用设置反序列化 XAML 字符串为新的 FrameworkElement 对象 
                using (StringReader stringReader = new StringReader(xaml))
                {
                    using (XmlReader xmlReader = XmlReader.Create(stringReader))
                    {
                        return (FrameworkElement)System.Windows.Markup.XamlReader.Load(xmlReader);
                    }
                }
            }
            catch (Exception ex)
            {
                // 处理异常 
                Console.WriteLine($"深拷贝失败: {ex.Message}");
                return null;
            }
        }

        public static FrameworkElement DeepCopy(FrameworkElement element)
        {
            var sb = new StringBuilder();
            var xmlWriter = XmlWriter.Create(sb, new XmlWriterSettings { Indent = true });

            // 关键配置：启用表达式模式以保留绑定 
            var manager = new XamlDesignerSerializationManager(xmlWriter)
            {
                XamlWriterMode = XamlWriterMode.Expression
            };

            System.Windows.Markup.XamlWriter.Save(element, manager);
            var xamlString = sb.ToString();

            // 反序列化生成新对象 
            var stringReader = new StringReader(xamlString);
            var xmlReader = XmlReader.Create(stringReader);
            return (FrameworkElement)System.Windows.Markup.XamlReader.Load(xmlReader);
        }

        //public FrameworkElement DeepCopyFrameworkElement(FrameworkElement element)
        //{
        //    var settings = new XmlWriterSettings { Indent = true };
        //    var sb = new StringBuilder();
        //    using (var writer = XmlWriter.Create(sb, settings))
        //    {
        //        XamlWriter.Save(element, writer);
        //    }

        //    return (FrameworkElement)XamlReader.Parse(sb.ToString());
        //}
        //StringWriter stringWriter = new StringWriter(); 
        //XamlWriter.Save(element, stringWriter);
        //string xaml = stringWriter.ToString();
        public override DocumentPage GetPage(int pageNumber) =>
            new DocumentPage(_pages[pageNumber]);

        public override bool IsPageCountValid { get; }
        public override int PageCount => _pages.Count;
        public override Size PageSize { get; set; }
        public override IDocumentPaginatorSource Source { get; }

        private void FinalizePage(StackPanel page, Size pageSize)
        {
            page.Measure(pageSize);
            page.Arrange(new Rect(pageSize));
            _pages.Add(page);
        }

        private static StackPanel CreatePageContainer() => new StackPanel
        {
            Width = 794,
            Height = 1113
        };
    }

}
