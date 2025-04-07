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
using DirectShowLib;

namespace WpfMain.Controlls
{
    /// <summary>
    /// UCMFVideo.xaml 的交互逻辑
    /// </summary>
    public partial class UCMFVideo : UserControl
    {
        public UCMFVideo()
        {
            InitializeComponent();
            VideoInit();


        }

        public void VideoInit()
        {
            var devices = DsDevice.GetDevicesOfCat(DirectShowLib.FilterCategory.VideoInputDevice);
            var device = devices[0];
            if (device != null)
            {
                vce.Pause();
                vce.EnableSampleGrabbing = true;
                vce.VideoCaptureDevice = device;
                vce.Play();
                vce.ShowPropertyPage();

            }
            else
            {
                MessageBox.Show("Video device not found.");
            }
        }

        public void Stop()
        {
            vce.Pause();
        }
    }
}
