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
    /// UCCard.xaml 的交互逻辑
    /// </summary>
    public partial class UCCard
    {
        public UCCard()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty LogoProperty =
            DependencyProperty.Register("Logo", typeof(string), typeof(UCCard), new PropertyMetadata(string.Empty, OnMyPropertyChanged));

        public string Logo
        {
            get { return (string)GetValue(LogoProperty); }
            set { SetValue(LogoProperty, value); }
        }

        private static void OnMyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            UCCard control = d as UCCard;
            if (control != null)
            {
                control.ImageLogo.Source = new BitmapImage(new Uri(e.NewValue.ToString()));
            }
        }

        public static new readonly DependencyProperty ContentProperty =
            DependencyProperty.Register("Content", typeof(string), typeof(UCCard), new PropertyMetadata(string.Empty, OnContentProperty));

        public new string Content
        {
            get { return (string)GetValue(ContentProperty); }
            set { SetValue(ContentProperty, value); }
        }

        private static void OnContentProperty(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            UCCard control = d as UCCard;
            if (control != null)
            {
                control.content.Text = e.NewValue.ToString();
            }
        }
    }
}
