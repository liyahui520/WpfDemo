using AForge.Video;
using AForge.Video.DirectShow;

namespace Record.Interfaces
{
    public interface IRecorder
    {
        void Start(NewFrameEventHandler frameEventHandler = null);

        void Pause();

        string End();

        /// <summary>
        /// 初始化摄像头
        /// </summary>
        /// <param name="monikerString"></param>
        /// <returns></returns>
        VideoCaptureDevice initCapture(string monikerString);
    }
}
