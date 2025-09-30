using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using OpenCvSharp;

namespace WpfAppNew.Services
{
    /// <summary>
    /// 摄像头信息服务接口
    /// </summary>
    public interface ICameraService
    {
        /// <summary>
        /// 获取可用摄像头列表
        /// </summary>
        /// <returns>摄像头信息列表</returns>
        Task<List<CameraInfo>> GetAvailableCamerasAsync();

        /// <summary>
        /// 初始化摄像头
        /// </summary>
        /// <param name="cameraIndex">摄像头索引</param>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        /// <returns>是否初始化成功</returns>
        bool InitializeCamera(int cameraIndex, int width = 640, int height = 480);

        /// <summary>
        /// 开始预览
        /// </summary>
        /// <param name="frameCallback">帧回调函数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>预览任务</returns>
        Task StartPreviewAsync(Action<Mat> frameCallback, CancellationToken cancellationToken);

        /// <summary>
        /// 停止预览
        /// </summary>
        void StopPreview();

        /// <summary>
        /// 开始录制
        /// </summary>
        /// <param name="fileName">文件名</param>
        /// <param name="fps">帧率</param>
        /// <param name="quality">质量</param>
        /// <returns>是否开始成功</returns>
        bool StartRecording(string fileName, int fps = 30, string quality = "medium");

        /// <summary>
        /// 停止录制
        /// </summary>
        void StopRecording();

        /// <summary>
        /// 释放资源
        /// </summary>
        void Dispose();

        /// <summary>
        /// 是否正在预览
        /// </summary>
        bool IsPreviewing { get; }

        /// <summary>
        /// 是否正在录制
        /// </summary>
        bool IsRecording { get; }
    }
}