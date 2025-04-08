using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using WPFMediaKit.DirectShow.Controls;

namespace WPFMediaKit.Manager
{
    // 封装资源释放类 
    public class MediaResourceManager : IDisposable
    {
        private MediaPlayer _player;
        private VideoCaptureElement _captureElement;

        public void Dispose()
        {
            _player?.Close();
            _captureElement?.Close();
            GC.SuppressFinalize(this);
        }
    }
}
