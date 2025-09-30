using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using OpenCvSharp;

namespace WpfAppNew.Services
{
    /// <summary>
    /// 摄像头信息类
    /// </summary>
    public class CameraInfo
    {
        public int Index { get; set; }
        public string Name { get; set; }

        public CameraInfo(int index, string name)
        {
            Index = index;
            Name = name;
        }

        public override string ToString()
        {
            return Name;
        }
    }
    /// <summary>
    /// 摄像头服务实现
    /// </summary>
    public class CameraService : ICameraService, IDisposable
    {
        private VideoCapture _capture;
        private Mat _frame;
        private VideoWriter _videoWriter;
        private bool _isPreviewing = false;
        private bool _isRecording = false;
        private readonly object _lockObject = new object();
        private int _currentWidth = 640;
        private int _currentHeight = 480;

        /// <summary>
        /// 是否正在预览
        /// </summary>
        public bool IsPreviewing 
        { 
            get
            {
                lock (_lockObject)
                {
                    return _isPreviewing;
                }
            }
        }

        /// <summary>
        /// 是否正在录制
        /// </summary>
        public bool IsRecording 
        { 
            get
            {
                lock (_lockObject)
                {
                    return _isRecording;
                }
            }
        }

        /// <summary>
        /// 获取可用摄像头列表
        /// </summary>
        /// <returns>摄像头信息列表</returns>
        public async Task<List<CameraInfo>> GetAvailableCamerasAsync()
        {
            return await Task.Run(() =>
            {
                var cameraList = new List<CameraInfo>();

                for (int i = 0; i < 10; i++) // 检查前10个摄像头索引
                {
                    try
                    {
                        using (var capture = new VideoCapture(i))
                        {
                            if (capture.IsOpened())
                            {
                                // 尝试读取一帧来确认摄像头是否真正可用
                                var frame = new Mat();
                                if (capture.Read(frame) && !frame.Empty())
                                {
                                    cameraList.Add(new CameraInfo(i, "摄像头 " + i));
                                }
                                frame.Dispose();
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // 忽略单个摄像头的错误
                        continue;
                    }
                }

                return cameraList;
            });
        }

        /// <summary>
        /// 初始化摄像头
        /// </summary>
        /// <param name="cameraIndex">摄像头索引</param>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        /// <returns>是否初始化成功</returns>
        public bool InitializeCamera(int cameraIndex, int width = 640, int height = 480)
        {
            lock (_lockObject)
            {
                try
                {
                    // 释放之前的资源
                    ReleaseResources();

                    // 初始化摄像头
                    _capture = new VideoCapture(cameraIndex);
                    _capture.Set(VideoCaptureProperties.FrameWidth, width);
                    _capture.Set(VideoCaptureProperties.FrameHeight, height);

                    if (!_capture.IsOpened())
                    {
                        return false;
                    }

                    _currentWidth = width;
                    _currentHeight = height;
                    _frame = new Mat();
                    return true;
                }
                catch (Exception)
                {
                    ReleaseResources();
                    return false;
                }
            }
        }

        /// <summary>
        /// 开始预览
        /// </summary>
        /// <param name="frameCallback">帧回调函数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>预览任务</returns>
        public async Task StartPreviewAsync(Action<Mat> frameCallback, CancellationToken cancellationToken)
        {
            lock (_lockObject)
            {
                if (_capture == null || !_capture.IsOpened())
                {
                    throw new InvalidOperationException("摄像头未初始化或无法打开");
                }

                _isPreviewing = true;
            }

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    lock (_lockObject)
                    {
                        if (!_isPreviewing)
                            break;
                    }

                    bool frameRead = false;
                    Mat currentFrame = null;
                    
                    lock (_lockObject)
                    {
                        if (_capture != null && _capture.IsOpened())
                        {
                            // 创建一个新的Mat来存储当前帧
                            currentFrame = new Mat();
                            frameRead = _capture.Read(currentFrame);
                            
                            // 如果成功读取帧且正在录制，写入帧
                            if (frameRead && _isRecording && _videoWriter != null && _videoWriter.IsOpened())
                            {
                                _videoWriter.Write(currentFrame);
                            }
                            
                            // 如果读取失败，释放创建的Mat
                            if (!frameRead)
                            {
                                currentFrame?.Dispose();
                                currentFrame = null;
                            }
                        }
                    }

                    // 如果成功读取帧，调用回调函数
                    if (frameRead && currentFrame != null)
                    {
                        try
                        {
                            // 克隆帧以确保调用者可以安全使用它
                            // 这样即使我们释放了currentFrame，调用者仍然有有效的数据
                            var clonedFrame = currentFrame.Clone();
                            frameCallback?.Invoke(clonedFrame);
                        }
                        catch (Exception)
                        {
                            // 忽略回调中的异常
                        }
                        finally
                        {
                            // 使用完后释放帧
                            currentFrame?.Dispose();
                        }
                    }
                    else
                    {
                        // 检查是否应该继续循环
                        lock (_lockObject)
                        {
                            if (!_isPreviewing || _capture == null || !_capture.IsOpened())
                                break;
                        }
                    }

                    // 控制帧率
                    await Task.Delay(33); // 约30 FPS
                }
            }
            catch (Exception)
            {
                lock (_lockObject)
                {
                    _isPreviewing = false;
                }
                throw;
            }
            finally
            {
                lock (_lockObject)
                {
                    _isPreviewing = false;
                }
            }
        }

        /// <summary>
        /// 停止预览
        /// </summary>
        public void StopPreview()
        {
            lock (_lockObject)
            {
                _isPreviewing = false;
            }
        }

        /// <summary>
        /// 开始录制
        /// </summary>
        /// <param name="fileName">文件名</param>
        /// <param name="fps">帧率</param>
        /// <param name="quality">质量</param>
        /// <returns>是否开始成功</returns>
        public bool StartRecording(string fileName, int fps = 30, string quality = "medium")
        {
            lock (_lockObject)
            {
                if (!_isPreviewing)
                {
                    return false;
                }

                try
                {
                    // 创建视频写入器
                    _videoWriter = new VideoWriter(fileName, FourCC.MP4V, fps, new Size(_currentWidth, _currentHeight));

                    if (!_videoWriter.IsOpened())
                    {
                        return false;
                    }

                    _isRecording = true;
                    return true;
                }
                catch (Exception)
                {
                    _isRecording = false;
                    return false;
                }
            }
        }

        /// <summary>
        /// 停止录制
        /// </summary>
        public void StopRecording()
        {
            lock (_lockObject)
            {
                if (_videoWriter != null)
                {
                    _videoWriter.Release();
                    _videoWriter = null;
                }
                _isRecording = false;
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        private void ReleaseResources()
        {
            lock (_lockObject)
            {
                try
                {
                    _isPreviewing = false;
                    _isRecording = false;

                    _capture?.Release();
                    _capture = null;

                    _frame?.Dispose();
                    _frame = null;

                    _videoWriter?.Release();
                    _videoWriter = null;
                }
                catch (Exception)
                {
                    // 忽略释放资源时的异常
                }
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            ReleaseResources();
        }
    }
}