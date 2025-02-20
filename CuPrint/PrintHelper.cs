using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows;
using System.Windows.Markup;
using System.Xml.Linq;
using HandyControl.Controls;
using System.Drawing.Printing;
using System.Runtime.Remoting.Messaging;
using System.IO;

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
            _header = DeepCopyFrameworkElement(header);
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
                currentPage.Children.Add(DeepCopyFrameworkElement(_header));
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
                        currentPage.Children.Add(DeepCopyFrameworkElement(_header));
                        currentHeight += header.DesiredSize.Height;
                    }
                }
                currentPage.Children.Add(content);
                currentHeight += content.DesiredSize.Height;
            }
            FinalizePage(currentPage, pageSize);
        }

        public FrameworkElement DeepCopyFrameworkElement(FrameworkElement element)
        {
            StringWriter stringWriter = new StringWriter();
            XamlWriter.Save(element, stringWriter);
            string xaml = stringWriter.ToString();
            return (FrameworkElement)XamlReader.Parse(xaml);
        }

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
