using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Entity.Entity;
using Entity.Enum;

namespace WpfAppNew
{
    /// <summary>
    /// TestInfo详情页面演示窗口
    /// 用于展示和测试TestInfoDetailPage控件的功能
    /// </summary>
    public partial class TestDetailPageDemo : Window
    {
        #region 构造函数
        /// <summary>
        /// 初始化演示窗口
        /// </summary>
        public TestDetailPageDemo()
        {
            InitializeComponent();
            LoadTestData_Click(null, null); // 自动加载测试数据
        }
        #endregion

        #region 事件处理
        /// <summary>
        /// 加载测试数据按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void LoadTestData_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var testInfo = CreateTestData();
                DetailPage.TestInfo = testInfo;
                
                MessageBox.Show("测试数据加载成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载测试数据失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region 私有方法
        /// <summary>
        /// 创建测试数据
        /// </summary>
        /// <returns>TestInfo测试对象</returns>
        private TestInfo CreateTestData()
        {
            var testInfo = new TestInfo
            {
                Id = Guid.NewGuid().ToString(),
                RecordNo = "PET20241201001",
                TestName = "血液常规检查",
                See = "宠物精神状态良好，食欲正常。血液检查显示白细胞计数略高，可能存在轻微感染。建议继续观察并进行抗炎治疗。其他指标均在正常范围内。",
                Size = "中等",
                Count = "1",
                DCOperation = "张医生",
                Pet = "小白",
                Gender = GenderEnum.公.ToString(),
                Customer = "李先生",
                CustomerPhone = "13800138000",
                TestDate = DateTime.Now.AddDays(-1),
                Type = "犬",
                Variety = "金毛寻回犬",
                Age = "2年6个月",
                Neuter = NeuterEnum.已绝育.ToString(),
                TestPath = @"D:\TestReports\PET20241201001"
            };

            // 创建测试结果
            testInfo.Result = new TestResult
            {
                Remark = "建议定期复查，注意观察宠物的精神状态和食欲变化。如有异常请及时就医。",
                Datas = CreateTestResultData(),
                Images = CreateTestImages(),
                Vedios = CreateTestVideos()
            };

            return testInfo;
        }

        /// <summary>
        /// 创建测试结果数据
        /// </summary>
        /// <returns>结果项列表</returns>
        private List<ResultItem> CreateTestResultData()
        {
            return new List<ResultItem>
            {
                new ResultItem { Name = "白细胞计数", Unit = "×10⁹/L", Value = "12.5", PN = false, Remarks = "略高" },
                new ResultItem { Name = "红细胞计数", Unit = "×10¹²/L", Value = "6.8", PN = false, Remarks = "正常" },
                new ResultItem { Name = "血红蛋白", Unit = "g/L", Value = "145", PN = false, Remarks = "正常" },
                new ResultItem { Name = "血小板计数", Unit = "×10⁹/L", Value = "320", PN = false, Remarks = "正常" },
                new ResultItem { Name = "中性粒细胞", Unit = "%", Value = "68", PN = false, Remarks = "正常" }
            };
        }

        /// <summary>
        /// 创建测试图片数据
        /// </summary>
        /// <returns>图片项列表</returns>
        private List<ImageItem> CreateTestImages()
        {
            var images = new List<ImageItem>();

            try
            {
                // 创建示例图片（彩色渐变图）
                for (int i = 0; i < 3; i++)
                {
                    var imageItem = new ImageItem
                    {
                        Name = $"检查图片_{i + 1}",
                        Type = MediaSourceType.Base64,
                        IsSelected = i == 0 // 第一张图片默认选中
                    };

                    // 创建示例图片
                    var bitmap = CreateSampleImage(300, 200, i);
                    imageItem.ImageSource = bitmap;
                    
                    images.Add(imageItem);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建测试图片失败: {ex.Message}", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            return images;
        }

        /// <summary>
        /// 创建测试视频数据
        /// </summary>
        /// <returns>视频项列表</returns>
        private List<MediaItem> CreateTestVideos()
        {
            var videos = new List<MediaItem>();

            try
            {
                // 创建示例视频项（使用缩略图）
                for (int i = 0; i < 2; i++)
                {
                    var videoItem = new MediaItem
                    {
                        Name = $"检查视频_{i + 1}",
                        Source = "", // 实际项目中这里应该是视频文件路径
                        Type = MediaSourceType.LocalPath,
                        IsSelected = i == 0 // 第一个视频默认选中
                    };

                    // 创建视频缩略图
                    var thumbnailBitmap = CreateVideoThumbnail(160, 120, i);
                    videoItem.ThumbnailSource = ""; // 实际项目中这里应该是缩略图路径
                    
                    videos.Add(videoItem);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建测试视频失败: {ex.Message}", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            return videos;
        }

        /// <summary>
        /// 创建示例图片
        /// </summary>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        /// <param name="index">索引</param>
        /// <returns>位图图像源</returns>
        private BitmapSource CreateSampleImage(int width, int height, int index)
        {
            var drawingVisual = new DrawingVisual();
            using (var drawingContext = drawingVisual.RenderOpen())
            {
                // 根据索引创建不同颜色的渐变背景
                var colors = new[]
                {
                    new Color[] { Colors.LightBlue, Colors.DarkBlue },
                    new Color[] { Colors.LightGreen, Colors.DarkGreen },
                    new Color[] { Colors.LightCoral, Colors.DarkRed }
                };

                var selectedColors = colors[index % colors.Length];
                var brush = new LinearGradientBrush(selectedColors[0], selectedColors[1], 45);
                
                drawingContext.DrawRectangle(brush, null, new Rect(0, 0, width, height));
                
                // 添加文本
                var formattedText = new FormattedText(
                    $"检查图片 {index + 1}",
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Microsoft YaHei"),
                    16,
                    Brushes.White);

                drawingContext.DrawText(formattedText, 
                    new Point((width - formattedText.Width) / 2, (height - formattedText.Height) / 2));
            }

            var renderTargetBitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            renderTargetBitmap.Render(drawingVisual);
            return renderTargetBitmap;
        }

        /// <summary>
        /// 创建视频缩略图
        /// </summary>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        /// <param name="index">索引</param>
        /// <returns>位图图像源</returns>
        private BitmapSource CreateVideoThumbnail(int width, int height, int index)
        {
            var drawingVisual = new DrawingVisual();
            using (var drawingContext = drawingVisual.RenderOpen())
            {
                // 创建深色背景
                drawingContext.DrawRectangle(Brushes.Black, null, new Rect(0, 0, width, height));
                
                // 添加播放按钮图标
                var playButtonSize = Math.Min(width, height) * 0.3;
                var centerX = width / 2.0;
                var centerY = height / 2.0;
                
                var playButton = new EllipseGeometry(new Point(centerX, centerY), playButtonSize / 2, playButtonSize / 2);
                drawingContext.DrawGeometry(Brushes.White, null, playButton);
                
                // 绘制三角形播放符号
                var triangleSize = playButtonSize * 0.4;
                var triangle = new PathGeometry();
                var figure = new PathFigure();
                figure.StartPoint = new Point(centerX - triangleSize / 3, centerY - triangleSize / 2);
                figure.Segments.Add(new LineSegment(new Point(centerX + triangleSize / 2, centerY), true));
                figure.Segments.Add(new LineSegment(new Point(centerX - triangleSize / 3, centerY + triangleSize / 2), true));
                figure.IsClosed = true;
                triangle.Figures.Add(figure);
                
                drawingContext.DrawGeometry(Brushes.Black, null, triangle);
                
                // 添加文本
                var formattedText = new FormattedText(
                    $"视频 {index + 1}",
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Microsoft YaHei"),
                    12,
                    Brushes.White);

                drawingContext.DrawText(formattedText, 
                    new Point((width - formattedText.Width) / 2, height - formattedText.Height - 5));
            }

            var renderTargetBitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            renderTargetBitmap.Render(drawingVisual);
            return renderTargetBitmap;
        }
        #endregion
    }
}