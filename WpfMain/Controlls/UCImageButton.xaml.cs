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

namespace WpfMain.Controlls
{
    /// <summary>
    /// UCImageButton.xaml 的交互逻辑
    /// </summary>
    public partial class UCImageButton : UserControl
    {
        public UCImageButton()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty ImageProperty =
            DependencyProperty.Register("Image", typeof(string), typeof(UCImageButton), new PropertyMetadata(string.Empty, OnMyPropertyChanged));

        public string Image
        {
            get { return (string)GetValue(ImageProperty); }
            set { SetValue(ImageProperty, value); }
        }

        private static void OnMyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            UCImageButton control = d as UCImageButton;
            if (control != null)
            {
                control.ImageLogo.Source = new BitmapImage(new Uri(e.NewValue.ToString()));
            }
        }

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register("Title", typeof(string), typeof(UCImageButton), new PropertyMetadata(string.Empty, OnContentProperty));

        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        private static void OnContentProperty(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            UCImageButton control = d as UCImageButton;
            if (control != null)
            {
                control.content.Text = e.NewValue.ToString();
            }
        }
    }
}
