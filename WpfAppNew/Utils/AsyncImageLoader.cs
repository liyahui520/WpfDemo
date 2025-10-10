using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Tools.App;
using Tools.Extend;

namespace WpfAppNew.Utils
{
    /// <summary>
    /// 异步图像加载器
    /// 用于优化图像的异步加载，减少UI阻塞
    /// 适用于宠物医疗设备的图像显示场景
    /// </summary>
    public static class AsyncImageLoader
    {
        #region 私有字段

        private static readonly SemaphoreSlim _loadingSemaphore = new SemaphoreSlim(Environment.ProcessorCount, Environment.ProcessorCount);

        #endregion

        #region 公共方法

        /// <summary>
        /// 异步加载图像
        /// </summary>
        /// <param name="imagePath">图像文件路径</param>
        /// <param name="maxWidth">最大宽度，0表示不限制</param>
        /// <param name="maxHeight">最大高度，0表示不限制</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>加载的位图图像</returns>
        /// <exception cref="ArgumentException">图像路径为空或无效时抛出</exception>
        /// <exception cref="FileNotFoundException">图像文件不存在时抛出</exception>
        /// <exception cref="OperationCanceledException">操作被取消时抛出</exception>
        public static async Task<BitmapImage> LoadImageAsync(
            string imagePath, 
            int maxWidth = 0, 
            int maxHeight = 0, 
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
                throw new ArgumentException("图像路径不能为空", nameof(imagePath));

            if (!File.Exists(imagePath))
                throw new FileNotFoundException($"图像文件不存在: {imagePath}");

            // 限制并发加载数量
            await _loadingSemaphore.WaitAsync(cancellationToken);

            try
            {
                return await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);

                    // 设置解码尺寸以节省内存
                    if (maxWidth > 0)
                        bitmap.DecodePixelWidth = maxWidth;
                    if (maxHeight > 0)
                        bitmap.DecodePixelHeight = maxHeight;

                    bitmap.EndInit();
                    bitmap.Freeze(); // 冻结以便跨线程使用

                    LogUtil.Debug($"异步加载图像完成: {Path.GetFileName(imagePath)}");
                    return bitmap;
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                LogUtil.Error($"异步加载图像失败: {imagePath}, 错误: {ex.Message}");
                throw;
            }
            finally
            {
                _loadingSemaphore.Release();
            }
        }

        /// <summary>
        /// 异步加载缩略图
        /// </summary>
        /// <param name="imagePath">图像文件路径</param>
        /// <param name="thumbnailSize">缩略图尺寸，默认120像素</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>加载的缩略图</returns>
        /// <exception cref="ArgumentException">图像路径为空或无效时抛出</exception>
        /// <exception cref="ArgumentOutOfRangeException">缩略图尺寸小于等于0时抛出</exception>
        public static async Task<BitmapImage> LoadThumbnailAsync(
            string imagePath, 
            int thumbnailSize = 120, 
            CancellationToken cancellationToken = default)
        {
            if (thumbnailSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(thumbnailSize), "缩略图尺寸必须大于0");

            return await LoadImageAsync(imagePath, thumbnailSize, thumbnailSize, cancellationToken);
        }

        /// <summary>
        /// 异步加载图像并在UI线程更新
        /// </summary>
        /// <param name="imagePath">图像文件路径</param>
        /// <param name="onImageLoaded">图像加载完成回调</param>
        /// <param name="onError">错误回调</param>
        /// <param name="dispatcher">UI调度器</param>
        /// <param name="maxWidth">最大宽度，0表示不限制</param>
        /// <param name="maxHeight">最大高度，0表示不限制</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        /// <exception cref="ArgumentNullException">必要参数为空时抛出</exception>
        public static async Task LoadImageWithCallbackAsync(
            string imagePath,
            Action<BitmapImage> onImageLoaded,
            Action<Exception> onError = null,
            Dispatcher dispatcher = null,
            int maxWidth = 0,
            int maxHeight = 0,
            CancellationToken cancellationToken = default)
        {
            if (onImageLoaded == null)
                throw new ArgumentNullException(nameof(onImageLoaded));

            dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;

            try
            {
                var bitmap = await LoadImageAsync(imagePath, maxWidth, maxHeight, cancellationToken);

                // 在UI线程更新
                await dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    try
                    {
                        onImageLoaded(bitmap);
                    }
                    catch (Exception ex)
                    {
                        LogUtil.Error($"图像加载回调执行异常: {ex.Message}");
                        onError?.Invoke(ex);
                    }
                }));
            }
            catch (Exception ex)
            {
                LogUtil.Error($"异步加载图像异常: {imagePath}, 错误: {ex.Message}");

                // 在UI线程调用错误回调
                if (onError != null)
                {
                    await dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                    {
                        onError(ex);
                    }));
                }
            }
        }

        /// <summary>
        /// 预加载图像到内存
        /// </summary>
        /// <param name="imagePaths">图像路径列表</param>
        /// <param name="maxConcurrency">最大并发数，默认为处理器核心数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        /// <exception cref="ArgumentNullException">图像路径列表为空时抛出</exception>
        public static async Task PreloadImagesAsync(
            string[] imagePaths,
            int maxConcurrency = 0,
            CancellationToken cancellationToken = default)
        {
            if (imagePaths == null)
                throw new ArgumentNullException(nameof(imagePaths));

            if (maxConcurrency <= 0)
                maxConcurrency = Environment.ProcessorCount;

            var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
            var tasks = new Task[imagePaths.Length];

            for (int i = 0; i < imagePaths.Length; i++)
            {
                var imagePath = imagePaths[i];
                tasks[i] = PreloadSingleImageAsync(imagePath, semaphore, cancellationToken);
            }

            await Task.WhenAll(tasks);
            LogUtil.Info($"预加载{imagePaths.Length}张图像完成");
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 预加载单张图像
        /// </summary>
        /// <param name="imagePath">图像路径</param>
        /// <param name="semaphore">信号量</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        private static async Task PreloadSingleImageAsync(
            string imagePath, 
            SemaphoreSlim semaphore, 
            CancellationToken cancellationToken)
        {
            await semaphore.WaitAsync(cancellationToken);

            try
            {
                if (File.Exists(imagePath))
                {
                    await LoadThumbnailAsync(imagePath, 120, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"预加载图像失败: {imagePath}, 错误: {ex.Message}");
            }
            finally
            {
                semaphore.Release();
            }
        }

        #endregion
    }
}