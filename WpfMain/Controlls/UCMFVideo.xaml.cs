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
using Microsoft.Win32;
using WPFMediaKit.DirectShow.Controls;

namespace WpfMain.Controlls
{
    /// <summary>
    /// UCMFVideo.xaml 的交互逻辑
    /// </summary>
    public partial class UCMFVideo : UserControl
    {
        private bool sliderDrag;
        private bool sliderMediaChange;

        public UCMFVideo()
        {
            InitializeComponent();
            SetCameraCaptureElementVisible(false);
        }

        private void SetCameraCaptureElementVisible(bool visible)
        {
            btnStop_Click(null, null);
        }

        private void SetPlayButtons(bool playing)
        {

        }

        private void btnOpen_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog();
            var result = dlg.ShowDialog();
            if (result != true)
                return;
            SetCameraCaptureElementVisible(false);
            SetPlayButtons(true);
        }

        private void MediaUriElement_MediaFailed(object sender, WPFMediaKit.DirectShow.MediaPlayers.MediaFailedEventArgs e)
        {

        }


        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            SetPlayButtons(false);
        }

        private void btnPause_Click(object sender, RoutedEventArgs e)
        {
        }

        private void MediaUriPlayer_MediaPositionChanged(object sender, EventArgs e)
        {
            if (sliderDrag)
                return;
            this.Dispatcher.BeginInvoke(new Action(ChangeSlideValue), null);
        }

        private void ChangeSlideValue()
        {
            if (sliderDrag)
                return;

            sliderMediaChange = true;

            sliderMediaChange = false;
        }

        private void ChangeMediaPosition()
        {
            if (sliderMediaChange)
                return;

            sliderDrag = true;
            sliderDrag = false;
        }

        private void slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sliderMediaChange)
                return;

            this.Dispatcher.BeginInvoke(new Action(ChangeMediaPosition), null);
        }

        private void cobVideoSource_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            SetCameraCaptureElementVisible(true);
            cameraCaptureElement.VideoCaptureDevice = MultimediaUtil.VideoInputDevices[1];
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            cobVideoSource_SelectionChanged(null,null);
        }
    }
}
