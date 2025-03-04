using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows;
using System.Xaml;
using System.Xml;
using CuPrint.PrintControlls;
using Entity.Entity;
using Tools.Extend;

namespace CuPrint
{
    public class CustomerPrintHelper : DocumentPaginator
    {
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
                var img = new PrintImg(o);
                panels.Add(img);
            });
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
