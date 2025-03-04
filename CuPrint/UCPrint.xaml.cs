using Entity.Entity;
using System.Windows.Controls;
using System.Windows.Documents;
using Size = System.Windows.Size;

namespace CuPrint
{
    /// <summary>
    /// UCPrint.xaml 的交互逻辑
    /// </summary>
    public partial class UCPrint : UserControl
    {

        public UCPrint(TestInfo dataList)
        {
            InitializeComponent();
            ShowPrintPreview(dataList);
        }


        private void ShowPrintPreview(TestInfo dataList)
        {
            // 生成文档 
            var print = new CustomerPrintHelper(dataList, new Size(784, 1103));
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
