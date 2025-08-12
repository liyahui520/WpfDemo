using System;
using System.Runtime.InteropServices;
using DirectShowLib;
using WPFMediaKit.DirectShow.MediaPlayers;
using System.Windows;
using Tools.App;

namespace WPFMediaKit.DirectShow.Controls
{
    /// <summary>
    /// 高级视频捕获元素，提供更好的录制参数控制
    /// </summary>
    public class AdvancedVideoCaptureElement : VideoCaptureElement
    {
        #region 依赖属性

        public static readonly DependencyProperty VideoBitRateProperty =
            DependencyProperty.Register("VideoBitRate", typeof(int), typeof(AdvancedVideoCaptureElement),
                new FrameworkPropertyMetadata(2000000)); // 默认2Mbps

        public static readonly DependencyProperty VideoCodecProperty =
            DependencyProperty.Register("VideoCodec", typeof(string), typeof(AdvancedVideoCaptureElement),
                new FrameworkPropertyMetadata("H264"));

        public static readonly DependencyProperty RecordingQualityProperty =
            DependencyProperty.Register("RecordingQuality", typeof(RecordingQuality), typeof(AdvancedVideoCaptureElement),
                new FrameworkPropertyMetadata(RecordingQuality.High));

        /// <summary>
        /// 视频比特率 (bps)
        /// </summary>
        public int VideoBitRate
        {
            get => (int)GetValue(VideoBitRateProperty);
            set => SetValue(VideoBitRateProperty, value);
        }

        /// <summary>
        /// 视频编解码器
        /// </summary>
        public string VideoCodec
        {
            get => (string)GetValue(VideoCodecProperty);
            set => SetValue(VideoCodecProperty, value);
        }

        /// <summary>
        /// 录制质量
        /// </summary>
        public RecordingQuality RecordingQuality
        {
            get => (RecordingQuality)GetValue(RecordingQualityProperty);
            set => SetValue(RecordingQualityProperty, value);
        }

        #endregion

        protected override MediaPlayerBase OnRequestMediaPlayer()
        {
            return new AdvancedVideoCapturePlayer();
        }

        /// <summary>
        /// 高级录制开始方法
        /// </summary>
        /// <param name="outputPath">输出路径</param>
        /// <param name="customWidth">自定义宽度（0使用默认）</param>
        /// <param name="customHeight">自定义高度（0使用默认）</param>
        /// <param name="customFps">自定义帧率（0使用默认）</param>
        public void StartAdvancedRecording(string outputPath, int customWidth = 0, int customHeight = 0, int customFps = 0)
        {
            var player = VideoCapturePlayer as AdvancedVideoCapturePlayer;
            if (player == null) return;

            player.VideoBitRate = VideoBitRate;
            player.VideoCodec = VideoCodec;
            player.RecordingQuality = RecordingQuality;

            // 应用自定义参数
            if (customWidth > 0) player.DesiredWidth = customWidth;
            if (customHeight > 0) player.DesiredHeight = customHeight;
            if (customFps > 0) player.FPS = customFps;

            OutputFileName = outputPath;
            
            player.Dispatcher.BeginInvoke((Action)(() =>
            {
                player.VideoCaptureDevice = initCapture();
                player.SetupAdvancedGraph();
                player.Play();
            }));
        }
    }

    /// <summary>
    /// 录制质量枚举
    /// </summary>
    public enum RecordingQuality
    {
        Low,      // 低质量
        Medium,   // 中等质量
        High,     // 高质量
        Ultra     // 超高质量
    }

    /// <summary>
    /// 高级视频捕获播放器
    /// </summary>
    public class AdvancedVideoCapturePlayer : VideoCapturePlayer
    {
        public int VideoBitRate { get; set; } = 2000000;
        public string VideoCodec { get; set; } = "H264";
        public RecordingQuality RecordingQuality { get; set; } = RecordingQuality.High;

        /// <summary>
        /// 设置高级DirectShow图形
        /// </summary>
        public void SetupAdvancedGraph()
        {
            try
            {
                // 根据录制质量调整参数
                AdjustParametersByQuality();
                
                // 调用基类的SetupGraph
                SetupGraph();
            }
            catch (Exception ex)
            {
                InvokeMediaFailed(new MediaFailedEventArgs($"高级图形设置失败: {ex.Message}", ex));
            }
        }

        /// <summary>
        /// 根据质量等级调整参数
        /// </summary>
        private void AdjustParametersByQuality()
        {
            switch (RecordingQuality)
            {
                case RecordingQuality.Low:
                    VideoBitRate = 800000;  // 800 Kbps
                    FPS = Math.Min(FPS, 15); // 最高15fps
                    break;
                case RecordingQuality.Medium:
                    VideoBitRate = 1500000; // 1.5 Mbps
                    FPS = Math.Min(FPS, 24); // 最高24fps
                    break;
                case RecordingQuality.High:
                    VideoBitRate = 3000000; // 3 Mbps
                    FPS = Math.Min(FPS, 30); // 最高30fps
                    break;
                case RecordingQuality.Ultra:
                    VideoBitRate = 5000000; // 5 Mbps
                    FPS = Math.Min(FPS, 60); // 最高60fps
                    break;
            }
        }
    }
}