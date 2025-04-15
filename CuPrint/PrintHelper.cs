using System.Collections.Generic;
using System.Text;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows;
using System.Windows.Markup;
using System.Xml;
using System.IO;
using System.Xaml;
using System;
using System.Xml.Linq;
using Tools.Extend;

namespace CuPrint
{
    public class PrintHelper : DocumentPaginator
    {
        private readonly FrameworkElement _header;
        private readonly FrameworkElement _footer;
        public readonly List<FrameworkElement> _pages = new List<FrameworkElement>();
        public PrintHelper(FrameworkElement header,
            IEnumerable<FrameworkElement> contents,
            FrameworkElement footer,
            Size pageSize)
        {
            _header = CloneExpressionBuilder<FrameworkElement>.Clone(header);
            _footer = DeepCopyFrameworkElement(footer);
            CreateFixedDocument(_header, contents, _footer, pageSize);
        }

        public void CreateFixedDocument(
            FrameworkElement header,
            IEnumerable<FrameworkElement> contents,
            FrameworkElement footer,
            Size pageSize)
        {
            PageSize = pageSize;
            double currentHeight = 0;
            var currentPage = CreatePageContainer();
            // 添加固定表头 
            if (_header != null)
            {
                _header.Measure(pageSize);
                currentPage.Children.Add(CloneExpressionBuilder<FrameworkElement>.Clone(header));
                currentHeight += _header.DesiredSize.Height;
            }
            // 分页处理 
            foreach (var content in contents)
            {
                content.Measure(pageSize);
                if (currentHeight + content.DesiredSize.Height > pageSize.Height)
                {
                    FinalizePage(currentPage, pageSize);
                    currentPage = CreatePageContainer();
                    currentHeight = 0;
                    // 添加固定表头 
                    if (_header != null)
                    { 
                        currentPage.Children.Add(CloneExpressionBuilder<FrameworkElement>.Clone(header));
                        currentHeight += header.DesiredSize.Height;
                    }
                }
                currentPage.Children.Add(content);
                currentHeight += content.DesiredSize.Height;
            }
            FinalizePage(currentPage, pageSize);
        }

        // 预编译属性复制委托（示例）
        //private static Action<FrameworkElement, FrameworkElement> _cloneDelegate;
         

        public static FrameworkElement DeepCopyFrameworkElement(FrameworkElement source)
        {
            return DeepCopy(source);
            //// 创建序列化和反序列化的设置 
            //var settings = new XamlReaderSettings();
            //// 这里可以根据需要添加更多设置 
            //// 例如，设置资源字典的解析行为 
            //settings.IgnoreUidsOnPropertyElements = true;

            //try
            //{
            //    // 将 FrameworkElement 序列化为 XAML 字符串 
            //    string xaml = System.Windows.Markup.XamlWriter.Save(source);
            //    // 使用设置反序列化 XAML 字符串为新的 FrameworkElement 对象 
            //    using (StringReader stringReader = new StringReader(xaml))
            //    {
            //        using (XmlReader xmlReader = XmlReader.Create(stringReader))
            //        {
            //            return (FrameworkElement)System.Windows.Markup.XamlReader.Load(xmlReader);
            //        }
            //    }
            //}
            //catch (Exception ex)
            //{
            //    // 处理异常 
            //    Console.WriteLine($"深拷贝失败: {ex.Message}");
            //    return null;
            //}
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
            // 添加固定表尾 
            if (_footer != null)
            {
                _footer.Measure(pageSize);
                page.Children.Add(DeepCopyFrameworkElement(_footer));
            }
            page.Measure(pageSize);
            page.Arrange(new Rect(pageSize));
            _pages.Add(page);
        }

        private static StackPanel CreatePageContainer() => new StackPanel
        {
            Width = 1123,
            Height = 794
        };
    }

    // 扩展方法 
    public static class PrintExtensions
    {
        public static PageContent Clone(this PageContent source)
        {
            var newPage = new FixedPage();
            foreach (UIElement child in source.Child.Children)
            {
                if (child is FrameworkElement fe)
                {
                    var clone = new FrameworkElement();
                    clone.Width = fe.Width;
                    clone.Height = fe.Height;
                    newPage.Children.Add(clone);
                }
            }
            return new PageContent { Child = newPage };
        }
    }
}
