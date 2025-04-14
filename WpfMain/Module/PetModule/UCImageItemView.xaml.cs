using DrawTools;
using DrawTools.Utils;
using Entity.Entity;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Tools.Extend;
using WpfMain.Logic;

namespace PacsCore
{
    /// <summary>
    /// UserControl1.xaml 的交互逻辑
    /// </summary>
    public partial class UCImageItemView : UserControl
    {
        private List<Operate> operates = new List<Operate>();
        private bool mouseDown;
        private Point mouseXY;
        private ImageItem tinfo;

        public event EventHandler<ImageItem> SaveClick;

        private void RaiseSomeActionTriggered(ImageItem entity)
        {
            SaveClick?.Invoke(this, entity);
        }

        /// <summary>
        /// 对比度
        /// </summary>
        public int Threshold
        {
            get => (int)GetValue(ThresholdProperty);
            set
            {
                if (value == 0)
                {
                    //还原
                    dicomImage1.Source = tinfo.ImageSource;
                }
                else
                {
                    // 绘制灰度图
                    System.Drawing.Bitmap newBitmap = ScreenUtils.Contrast(tinfo.BitBuffer.Byte2Bitmap(), value);
                    dicomImage1.Source = ScreenUtils.ConvertBitmapToBitmapImage(newBitmap);
                    operates.Add(new Operate(OperateType.Threshold, Threshold));

                }

                SetValue(ThresholdProperty, value);
            }
        }

        /// <summary>
        /// 缩放比例
        /// </summary>
        public double Zoom
        {
            get => (double)GetValue(ZoomProperty);
            set
            {
                SetValue(ZoomProperty, value);
            }
        }

        public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register(nameof(Zoom), typeof(double), typeof(UCImageItemView));
        public static readonly DependencyProperty ThresholdProperty = DependencyProperty.Register(nameof(Threshold), typeof(int), typeof(UCImageItemView));

        /// <summary>
        /// 当前检查信息
        /// </summary>
        //private TestInfo tInfo;


        public UCImageItemView(TestInfo info)
        {
            //TestInfo tInfo = info;
            InitializeComponent();
            if (info?.Result?.Images == null && info?.Result?.Images.Count > 0)
                return;

            tinfo = info.Result?.Images[0];
            dicomImage1.Source = tinfo.ImageSource;
            Loaded += UCImageItemView_Loaded;
        }
        public UCImageItemView(ImageItem info)
        {
            InitializeComponent();
            tinfo = info;
            Loaded += UCImageItemView_Loaded;
            drawingCanvas.OnVisualChildrenAdd += DrawingCanvas_OnVisualChildrenAdd;
        }

        private void DrawingCanvas_OnVisualChildrenAdd(Visual obj)
        {
            operates.Add(new Operate( OperateType.Dring, obj));
        }

        private void UCImageItemView_Loaded(object sender, RoutedEventArgs e)
        {
            SetDataset();
        }

        public UCImageItemView()
        {
            InitializeComponent();
        }
        #region 公有
        /// <summary>
        /// 设置图片
        /// </summary>
        /// <param name="dicomdataset"></param>
        /// <param name="dicomimage"></param>
        public void SetDataset()
        {
            LoadDicomImage();
            FileInfo();
            LoadRuler();
            //iszoom = true;
        }

        /// <summary>
        /// 调整比例 缩放
        /// </summary>
        /// <param name="point"></param>
        /// <param name="delta"></param>
        public void DowheelZoom(Point point, double delta)
        {
            TransformGroup group = GridImage.RenderTransform as TransformGroup;
            ScaleTransform transform = group.Children[0] as ScaleTransform;

            transform.CenterX = point.X;
            transform.CenterY = point.Y;

            if (transform.CenterX > 0)
            {
                if (transform.ScaleX + delta > 4) return;
                if (transform.ScaleX + delta < 0.1) return;
                transform.ScaleX += delta;
                transform.ScaleY += delta;
            }
            else
            {
                if (transform.ScaleX - delta < -4) return;
                if (transform.ScaleX - delta > -0.1) return;
                transform.ScaleX -= delta;
                transform.ScaleY -= delta;
            }
            operates.Add(new Operate(OperateType.Zoom,new Rect(transform.CenterX, transform.CenterY, transform.ScaleX, transform.ScaleY)));
            LoadRuler();
        }

        /// <summary>
        /// 缩放
        /// </summary>
        /// <param name="delta"></param>
        public void DowheelZoom(double delta)
        {
            DowheelZoom(new Point(IMG.ActualWidth / 2, IMG.ActualHeight / 2), delta);
        }
        /// <summary>
        /// 旋转
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void Rotate(int angle)
        {
            var group = GridImage.RenderTransform as TransformGroup;
            var transform = group.Children[2] as RotateTransform;
            transform.CenterX = IMG.ActualWidth / 2;
            transform.CenterY = IMG.ActualHeight / 2;
            operates.Add(new Operate(OperateType.Rotate, transform.Angle));
            if (transform.Angle + angle == 360)
            {
                transform.Angle = 0;
                return;
            }
            transform.Angle += angle;
        }

        /// <summary>
        /// 翻转
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void Flip()
        {
            var group = GridImage.RenderTransform as TransformGroup;
            var scale = group.Children[0] as ScaleTransform;
            scale.CenterX = IMG.ActualWidth / 2;
            scale.CenterY = IMG.ActualHeight / 2;
            scale.ScaleX = -scale.ScaleX;
            operates.Add(new Operate(OperateType.Flip, 0));
        }

        /// <summary>
        /// 还原
        /// </summary>
        public void Reduction()
        {
            Threshold = 0;
            LoadDicomImage();
            LoadRuler();
            operates.Clear();
        }

        /// <summary>
        /// 撤销上一步操作
        /// </summary>
        public void Revoke()
        {
            if (operates.Count == 0)
                return;
            Operate ot = operates.Last();
            var group = GridImage.RenderTransform as TransformGroup;
            switch (ot.Type)
            {
                case OperateType.Dring:
                    this.drawingCanvas.DeleteVisual((Visual)ot.Value);
                    break;
                case OperateType.Threshold:
                    System.Drawing.Bitmap newBitmap = ScreenUtils.Contrast(tinfo.BitBuffer.Byte2Bitmap(), (int)ot.Value);
                    dicomImage1.Source = ScreenUtils.ConvertBitmapToBitmapImage(newBitmap);
                    SetValue(ThresholdProperty, (int)ot.Value);
            break;
                case OperateType.Flip:
                    var scale = group.Children[0] as ScaleTransform;
                    scale.CenterX = IMG.ActualWidth / 2;
                    scale.CenterY = IMG.ActualHeight / 2;
                    scale.ScaleX = -scale.ScaleX;
                    break;
                case OperateType.Rotate:
                    var ortateform = group.Children[2] as RotateTransform;
                    ortateform.CenterX = IMG.ActualWidth / 2;
                    ortateform.CenterY = IMG.ActualHeight / 2;
                    ortateform.Angle = (double)ot.Value;
                    break;
                case OperateType.Zoom:
                    ScaleTransform scaleform = group.Children[0] as ScaleTransform;
                    Rect rect = (Rect)ot.Value;
                    scaleform.CenterX = rect.X;
                    scaleform.CenterY = rect.Y;
                    scaleform.ScaleX = rect.Width;
                    scaleform.ScaleY = rect.Height;
                    break;
                case OperateType.Move:
                    var transform = group.Children[3] as TranslateTransform;
                    var po = (Point)ot.Value;
                    transform.X = po.X;
                    transform.Y = po.Y;
                    mouseXY = po;
                    break;

            }
            operates.Remove(ot);
        }


        /// <summary>
        /// 清理所有画图
        /// </summary>
        public void Clear()
        {
            drawingCanvas.Clear();
        }

        /// <summary>
        /// 设置画图工具
        /// </summary>
        /// <param name="type"></param>
        public void Draw(DrawToolType type)
        {
            drawingCanvas.DrawingToolType = type;
            if (type == DrawToolType.Pointer)
            {
                Bordermove.Visibility = Visibility.Visible;
                return;
            }

            Bordermove.Visibility = Visibility.Hidden;

        }

        /// <summary>
        /// 保存图片
        /// </summary>
        public void SaveImage()
        {
            var frame = ToBitmapFrame();

            // 创建 PNG 编码器
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(frame);
            tinfo.ImageSource = frame;
            dicomImage1.Source = frame;
            drawingCanvas.Clear();
            RaiseSomeActionTriggered(tinfo);
        }
        #endregion

        #region 私有



        /// <summary>
        /// 显示图片信息
        /// </summary>
        private void FileInfo()
        {
            StackPanelInfo.Children.Clear();
            //imageSize = new Point();
        }


        /// <summary>
        /// 调整标尺
        /// </summary>
        /// <param name="dataset"></param>
        /// <param name="zoom"></param>
        private void LoadRuler()
        {
            StackPanelLeftNum.Children.Clear();
            StackPanelButtonNum.Children.Clear();


            //每像素0.25mm
            //每毫米一格  共10厘米
            double value = 1;
            if (tinfo.PixelSpacing == 0)
                return;

            //resolution
            Thickness ltk = new Thickness(0, 0, 0, 0);
            Thickness btk = new Thickness(0, 0, 0, 0);
            double x1, y2, cl = 100 / value * Zoom;
            double c2 = cl / 100;

            {
                //右侧竖标尺
                StackPanelLeftNum.Children.Add(new Line() { X1 = 15, X2 = 15, Y1 = 0, Y2 = cl });
                for (int i = 0; i < 101; i++)
                {
                    x1 = i % 10 == 0 ? 0 : 10;
                    StackPanelLeftNum.Children.Add(new Line { Margin = ltk, X1 = x1 });
                    ltk.Top += c2;
                }
            }


            {
                StackPanelButtonNum.Children.Add(new Line { X1 = 0, Y1 = 15, Y2 = 15, X2 = cl });
                for (int i = 0; i < 101; i++)
                {
                    y2 = i % 10 == 0 ? 0 : 10;
                    StackPanelButtonNum.Children.Add(new Line { Margin = btk, Y2 = y2 });
                    btk.Left += c2;
                }
            }

            TextBlockZoom.Text = Zoom.ToString("0.00") + "X";
        }

        /// <summary>
        /// 重新加载图像
        /// </summary>
        private void LoadDicomImage()
        {

            IMG.MinWidth = drawingCanvas.MinWidth = drawingCanvas.Width = dicomImage1.MinWidth = dicomImage1.Width = tinfo.ImageSource.Width;
            IMG.MinHeight = drawingCanvas.MinHeight = drawingCanvas.Height = dicomImage1.MinHeight = dicomImage1.Height = tinfo.ImageSource.Height;
            dicomImage1.Source = tinfo.ImageSource;

            Zoom = 1;
            TransformGroup group = GridImage.RenderTransform as TransformGroup;
            ScaleTransform transform = group.Children[0] as ScaleTransform;
            transform.ScaleX = Zoom;
            transform.ScaleY = Zoom;
            transform.CenterX = 0;
            transform.CenterY = 0;

            TranslateTransform transform1 = group.Children[3] as TranslateTransform;
            transform1.X = transform1.Y = 0;

            RotateTransform transform2 = group.Children[2] as RotateTransform;
            transform2.CenterX = 0;
            transform2.CenterY = 0;
            transform2.Angle = 0;

        }


        /// <summary>
        /// 转为图片
        /// </summary>
        /// <param name="pixelWidth"></param>
        /// <param name="pixelHeight"></param>
        /// <param name="dpi"></param>
        /// <param name="image"></param>
        /// <returns></returns>
        public BitmapFrame ToBitmapFrame()
        {
            //var backgroundImage = this.dicomImage1.Source;
            //var frame = this.drawingCanvas.ToBitmapFrame((int)backgroundImage.Width, (int)backgroundImage.Height, DpiHelper.GetDpiFromVisual(this.drawingCanvas), backgroundImage);

            var backgroundImage = this.dicomImage1.Source;
            Dpi dpi = DpiHelper.GetDpiFromVisual(drawingCanvas);
            var visual = GetDrawingVisual(backgroundImage.Width, backgroundImage.Height, dpi, backgroundImage);

            if (visual == null)
                return null;

            var renderBitmap = new RenderTargetBitmap((int)backgroundImage.Width, (int)backgroundImage.Height, dpi.DpiX, dpi.DpiY, PixelFormats.Pbgra32);
            renderBitmap.Render(visual);

            return BitmapFrame.Create(renderBitmap);
        }

        private DrawingVisual GetDrawingVisual(double pixelWidth, double pixelHeight, Dpi dpi, ImageSource image = null)
        {
            var root = new DrawingVisual();
            var dc = root.RenderOpen();

            if (image != null)
                dc.DrawImage(image, new Rect(new Size(pixelWidth * dpi.Px2WpfX, pixelHeight * dpi.Px2WpfY)));

            foreach (var draw in drawingCanvas.GetDrawGeometries())
            {
                dc.DrawDrawing(draw.Drawing);
            }
            dc.Close();

            return root;
        }
        #endregion

        #region 事件

        /// <summary>
        /// 鼠标中间滚轮缩放
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void IMG1_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            Border img = sender as Border;//;
            if (img == null)
                return;
            Point point = e.GetPosition(img);
            double delta = e.Delta > 0 ? 0.25 : -0.25;// * 0.001;
            DowheelZoom(point, delta);
        }

        /// <summary>
        /// 移动图片
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void IMG1_MouseMove(object sender, MouseEventArgs e)
        {
            if (mouseDown)
            {
                Domousemove(this.BorderImg, e);
            }
        }

        private void IMG1_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            mouseDown = false;


            if (operates.Count == 0)
                return;

            var op = operates.Last();
            if (op.Type != OperateType.Move)
                return;

            var po = (Point)op.Value;
            var group = GridImage.RenderTransform as TransformGroup;
            var transform = group.Children[3] as TranslateTransform;
            if (po.X == transform.X && po.Y == transform.Y)
                operates.Remove(op);
        }
        private void IMG1_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var img = BorderImg;
            mouseDown = true;
            mouseXY = e.GetPosition(img);

            var group = GridImage.RenderTransform as TransformGroup;
            var transform = group.Children[3] as TranslateTransform;
            operates.Add(new Operate(OperateType.Move, new Point(transform.X, transform.Y)));
        }

        /// <summary>
        /// 移动
        /// </summary>
        /// <param name="img"></param>
        /// <param name="e"></param>
        private void Domousemove(Border img, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }
            var group = GridImage.RenderTransform as TransformGroup;
            var transform = group.Children[3] as TranslateTransform;

            var rotatetransform = group.Children[2] as RotateTransform;

            var position = e.GetPosition(img);

            if (rotatetransform.Angle == 90)
            {
                transform.X += position.Y - mouseXY.Y;
                transform.Y += mouseXY.X - position.X;
                mouseXY = position;
                return;
            }

            if (rotatetransform.Angle == 180)
            {
                transform.X += mouseXY.X - position.X;
                transform.Y += mouseXY.Y - position.Y;
                mouseXY = position;
                return;
            }
            if (rotatetransform.Angle == 270)
            {
                transform.X += mouseXY.Y - position.Y;
                transform.Y += position.X - mouseXY.X;
                mouseXY = position;
                return;
            }
            transform.X += position.X - mouseXY.X;
            transform.Y += position.Y - mouseXY.Y;
            mouseXY = position;
        }

        public void SetDrawingCanvasPinfo(string pname, object pvalue)
        {
            switch (pname)
            {
                case "Brush":
                    drawingCanvas.Brush = (SolidColorBrush)pvalue;
                    break;
                case "StrokeThickness":
                    drawingCanvas.StrokeThickness = Convert.ToUInt32(pvalue);
                    break;
                case "FontSize":
                    drawingCanvas.FontSize = Convert.ToDouble(pvalue);
                    break;
            }

        }



        #endregion
    }



    public class Operate
    {
        public Operate() { }
        public Operate(OperateType type, object value)
        {
            Type = type;
            Value = value;
        }
        public OperateType Type { get; set; }

        public object Value { get; set; }

    }

    public enum OperateType
    {
        /// <summary>
        /// 画图
        /// </summary>
        Dring = 1,

        /// <summary>
        /// 对比度
        /// </summary>
        Threshold = 2,

        /// <summary>
        /// 翻转
        /// </summary>
        Flip = 3,

        /// <summary>
        /// 旋转
        /// </summary>
        Rotate = 4,

        /// <summary>
        /// 缩放
        /// </summary>
        Zoom = 5,

        /// <summary>
        /// 移动
        /// </summary>
        Move=6,
    }

}
