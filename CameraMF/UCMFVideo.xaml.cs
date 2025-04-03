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
using Windows.UI.Xaml.Controls;
using WPFMediaKit.DirectShow.Controls;

namespace CameraMF
{
    /// <summary>
    /// UCMFVideo.xaml 的交互逻辑
    /// </summary>
    public partial class UCMFVideo : System.Windows.Controls.UserControl
    { 
        public UCMFVideo()
        {
            InitializeComponent(); 
        }

        private void UCMFVideo_OnLoaded(object sender, RoutedEventArgs e)
        {
            vce.Pause();
            vce.VideoCaptureSource = MultimediaUtil.VideoInputNames[0];
            vce.Play();
        }
    }
}
