using PdfiumViewer;
using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Reflection;
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

namespace WpfMain.Controlls
{
    /// <summary>
    /// UCWpfViewer.xaml 的交互逻辑
    /// </summary>
    public partial class UCWpfViewer : UserControl
    {
        public UCWpfViewer()
        {
            InitializeComponent();
        }

        private void SelectWpf_OnClick(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "文本文件|*.pdf";
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            openFileDialog.Title = "打开文本文件";
            openFileDialog.Multiselect = false;

            // 显示文件对话框
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            { 
                var document = PdfiumViewer.PdfDocument.Load(openFileDialog.FileName); 
                pdfViewer.Document = document;

            }
             
        }

        private void Print_OnClick(object sender, RoutedEventArgs e)
        {
            var document = pdfViewer.Document;
            var printerSettings = new PrinterSettings
            {
                PrinterName = "" //指定打印机名称
            };
            using (var printDocument = document.CreatePrintDocument())
            {
                printDocument.PrinterSettings = printerSettings; 
                printDocument.PrintController = new StandardPrintController();
                printDocument.Print();
            }
        }
         
    }
}
