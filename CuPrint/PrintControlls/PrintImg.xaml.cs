using Entity.Entity;
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

namespace CuPrint.PrintControlls
{
    /// <summary>
    /// PrintImg.xaml 的交互逻辑
    /// </summary>
    public partial class PrintImg : UserControl
    { 
        public PrintImg(List<ImageItem> info)
        {
            InitializeComponent(); 
            ImgListBox.ItemsSource = info;
            DataContext = info;
            DisableEvents(this);
        }

        public static void DisableEvents(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is UIElement uiElement)
                {
                    uiElement.IsHitTestVisible = false; // 禁用命中测试 
                    uiElement.Focusable = false;        // 禁用焦点 
                }
                DisableEvents(child);
            }
        }
    }
}
