using System;
using System.Windows;

namespace WpfAppNew.Controlls.VideoPlayer
{
    /// <summary>
    /// 视频播放器统一接口
    /// 定义了所有视频播放器必须实现的基本功能
    /// </summary>
    public interface IVideoPlayer
    {
        #region 属性

        /// <summary>
        /// 当前播放状态
        /// </summary>
        PlaybackState State { get; }

        /// <summary>
        /// 当前播放位置（秒）
        /// </summary>
        double Position { get; set; }

        /// <summary>
        /// 视频总时长（秒）
        /// </summary>
        double Duration { get; }

        /// <summary>
        /// 音量（0-100）
        /// </summary>
        int Volume { get; set; }

        /// <summary>
        /// 是否静音
        /// </summary>
        bool IsMuted { get; set; }

        /// <summary>
        /// 播放速度（1.0为正常速度）
        /// </summary>
        double PlaybackRate { get; set; }

        /// <summary>
        /// 当前加载的媒体文件路径
        /// </summary>
        string MediaPath { get; }

        /// <summary>
        /// 播放器控件（用于在UI中显示）
        /// </summary>
        FrameworkElement PlayerControl { get; }

        #endregion

        #region 事件

        /// <summary>
        /// 播放状态改变事件
        /// </summary>
        event EventHandler<PlaybackStateChangedEventArgs> StateChanged;

        /// <summary>
        /// 播放位置改变事件
        /// </summary>
        event EventHandler<PositionChangedEventArgs> PositionChanged;

        /// <summary>
        /// 媒体加载完成事件
        /// </summary>
        event EventHandler<MediaLoadedEventArgs> MediaLoaded;

        /// <summary>
        /// 播放结束事件
        /// </summary>
        event EventHandler MediaEnded;

        /// <summary>
        /// 错误发生事件
        /// </summary>
        event EventHandler<ErrorEventArgs> ErrorOccurred;

        #endregion

        #region 方法

        /// <summary>
        /// 加载媒体文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>是否加载成功</returns>
        bool LoadMedia(string filePath);

        /// <summary>
        /// 开始播放
        /// </summary>
        void Play();

        /// <summary>
        /// 暂停播放
        /// </summary>
        void Pause();

        /// <summary>
        /// 停止播放
        /// </summary>
        void Stop();

        /// <summary>
        /// 跳转到指定位置
        /// </summary>
        /// <param name="position">位置（秒）</param>
        void Seek(double position);

        /// <summary>
        /// 释放资源
        /// </summary>
        void Dispose();

        #endregion
    }

    /// <summary>
    /// 播放状态枚举
    /// </summary>
    public enum PlaybackState
    {
        /// <summary>
        /// 未加载
        /// </summary>
        None,
        /// <summary>
        /// 加载中
        /// </summary>
        Loading,
        /// <summary>
        /// 已停止
        /// </summary>
        Stopped,
        /// <summary>
        /// 播放中
        /// </summary>
        Playing,
        /// <summary>
        /// 已暂停
        /// </summary>
        Paused,
        /// <summary>
        /// 播放结束
        /// </summary>
        Ended,
        /// <summary>
        /// 错误状态
        /// </summary>
        Error
    }

    /// <summary>
    /// 播放状态改变事件参数
    /// </summary>
    public class PlaybackStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 旧状态
        /// </summary>
        public PlaybackState OldState { get; }

        /// <summary>
        /// 新状态
        /// </summary>
        public PlaybackState NewState { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="oldState">旧状态</param>
        /// <param name="newState">新状态</param>
        public PlaybackStateChangedEventArgs(PlaybackState oldState, PlaybackState newState)
        {
            OldState = oldState;
            NewState = newState;
        }
    }

    /// <summary>
    /// 播放位置改变事件参数
    /// </summary>
    public class PositionChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 当前位置（秒）
        /// </summary>
        public double Position { get; }

        /// <summary>
        /// 总时长（秒）
        /// </summary>
        public double Duration { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="position">当前位置</param>
        /// <param name="duration">总时长</param>
        public PositionChangedEventArgs(double position, double duration)
        {
            Position = position;
            Duration = duration;
        }
    }

    /// <summary>
    /// 媒体加载完成事件参数
    /// </summary>
    public class MediaLoadedEventArgs : EventArgs
    {
        /// <summary>
        /// 媒体文件路径
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// 视频时长（秒）
        /// </summary>
        public double Duration { get; }

        /// <summary>
        /// 视频宽度
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// 视频高度
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="duration">时长</param>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        public MediaLoadedEventArgs(string filePath, double duration, int width = 0, int height = 0)
        {
            FilePath = filePath;
            Duration = duration;
            Width = width;
            Height = height;
        }
    }

    /// <summary>
    /// 错误事件参数
    /// </summary>
    public class ErrorEventArgs : EventArgs
    {
        /// <summary>
        /// 错误消息
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// 异常对象
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="message">错误消息</param>
        /// <param name="exception">异常对象</param>
        public ErrorEventArgs(string message, Exception exception = null)
        {
            Message = message;
            Exception = exception;
        }
    }
}