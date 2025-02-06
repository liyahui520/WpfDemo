
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using DrawTools;
using DrawTools.Utils;
using Newtonsoft.Json.Linq;
using WpfMain.Entity;
using WpfMain.Logic;
using WpfMain.Module.PetModule;

namespace PacsCore
{
    /// <summary>
    /// UserControl1.xaml 的交互逻辑
    /// </summary>
    public partial class UCImageItemView : UserControl
    {


        private bool mouseDown;
        private Point mouseXY;
        private Point imageSize;
        private double zoom = 1;
        private double szoom = 1;
        private int oldthreshold = 0;
        private ImageItem dinfo;

        public double Zoom
        {
            get => (double)GetValue(ZoomProperty);
            set
            {
                //if (value == zoom)
                //    return;
                //DowheelZoom(zoom > value ? 0.25 : -0.25);

                SetValue(ZoomProperty, value);
            }
        }

        public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register(nameof(Zoom), typeof(double), typeof(UCImageItemView));

        /// <summary>
        /// 当前检查信息
        /// </summary>
        private TestInfo tInfo;
        public UCImageItemView(TestInfo info)
        {
            tInfo = info;
            InitializeComponent();
            if (tInfo?.Result?.Images == null)
                return;

            dinfo = tInfo.Result.Images[0];
            dicomImage1.Source = dinfo.ImageSource;
            Loaded += UCImageItemView_Loaded;


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

        }

        /// <summary>
        /// 调对比度
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void SetThreshold(int threshold)
        {
            oldthreshold += threshold;
            if (threshold == 0)
            {
                dicomImage1.Source = dinfo.ImageSource;
                return;
            }

            // 绘制灰度图
            System.Drawing.Bitmap newBitmap = ScreenUtils.Contrast(dinfo.Bitmap, oldthreshold);
            dicomImage1.Source = ScreenUtils.ConvertBitmapToBitmapImage(newBitmap);
        }

        /// <summary>
        /// 调整比例 缩放
        /// </summary>
        /// <param name="point"></param>
        /// <param name="delta"></param>
        public void DowheelZoom(Point point, double delta, bool zoomcheck = true)
        {
            TransformGroup group = IMG.FindResource("Imageview") as TransformGroup;
            ScaleTransform transform = group.Children[0] as ScaleTransform;

            if (transform.ScaleX + delta > 4) return;
            if (transform.ScaleX + delta < 0.1) return;

            transform.CenterX = point.X;
            transform.CenterY = point.Y;
            transform.ScaleX += delta;
            transform.ScaleY += delta;

            zoom = transform.ScaleX * szoom;
            Zoom = zoom;
            //SetValue(ZoomProperty, zoom);
 

            LoadRuler();
        }
        public void DowheelZoom(double delta, bool zoomcheck = true)
        {
            DowheelZoom(new Point(IMG.ActualWidth / 2, IMG.ActualHeight / 2), delta, zoomcheck);
        }
        /// <summary>
        /// 旋转
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void Rotate(int angle)
        {

            var group = IMG.FindResource("Imageview") as TransformGroup;
            var transform = group.Children[2] as RotateTransform;
            transform.CenterX = IMG.ActualWidth / 2;
            transform.CenterY = IMG.ActualHeight / 2;
            if (transform.Angle + angle == 360)
            {
                transform.Angle = 0;
                return;
            }
            transform.Angle += angle;
        }


        public void Clear()
        {
            drawingCanvas.Clear();
        }

        /// <summary>
        /// 翻转
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void Flip()
        {
            dicomImage1.FlowDirection = dicomImage1.FlowDirection == FlowDirection.LeftToRight ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        }

        /// <summary>
        /// 还原
        /// </summary>
        public void Reduction()
        {
            SetThreshold(0);
            LoadDicomImage();
            LoadRuler();
        }



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


        public void SaveImage()
        {
            var renderBitmap = new RenderTargetBitmap((int)dicomImage1.ActualWidth, (int)dicomImage1.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            renderBitmap.Render(dicomImage1);

            PngBitmapEncoder pbe = new PngBitmapEncoder();
            pbe.Frames.Add(BitmapFrame.Create(renderBitmap));

            using (Stream sf = File.OpenWrite(DateTime.Now.ToString("yyyyMMddHHmmss") + ".png"))
                pbe.Save(sf);



            var backgroundImage = this.dicomImage1.Source;

            var frame = this.drawingCanvas.ToBitmapFrame((int)backgroundImage.Width, (int)backgroundImage.Height, DpiHelper.GetDpiFromVisual(this.drawingCanvas), backgroundImage);

            dicomImage1.Source = frame;
            drawingCanvas.Clear();
            drawingCanvas.Visibility = Visibility.Hidden;

        }
        #endregion

        #region 私有

        /// <summary>
        /// 显示图片信息
        /// </summary>
        private void FileInfo()
        {
            StackPanelInfo.Children.Clear();
            imageSize = new Point();

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
            if (dinfo.PixelSpacing == 0)
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

            BackFrame.Width = dinfo.ImageSource.Width;
            BackFrame.Height = dinfo.ImageSource.Height;

            TransformGroup group = IMG.FindResource("Imageview") as TransformGroup;
            ScaleTransform transform = group.Children[0] as ScaleTransform;
            double w = IMG.ActualWidth / dinfo.ImageSource.Width;
            double h = IMG.ActualHeight / dinfo.ImageSource.Height;
            if (w < 1 || h < 1)
            {
                Zoom = w > h ? h : w;
                transform.CenterX = (IMG.ActualWidth - (dinfo.ImageSource.Width * Zoom)) / 2;
                transform.CenterY = (IMG.ActualHeight - (dinfo.ImageSource.Height * Zoom)) / 2;
                transform.ScaleX = Zoom;
                transform.ScaleY = Zoom;
            }
            else
            {
                transform.ScaleX = 1;
                transform.ScaleY = 1;
                Zoom = 1;
            }

            TranslateTransform transform1 = group.Children[1] as TranslateTransform;
            transform1.X = transform1.Y = 0;

            RotateTransform transform2 = group.Children[2] as RotateTransform;
            transform2.CenterX = 0;
            transform2.CenterY = 0;
            transform2.Angle = 0;

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
            //var img = sender as Border;// ContentControl;
            //if (img == null)
            //{
            //    return;
            //}
            var img = BorderImg;
            //img.ReleaseMouseCapture();
            mouseDown = false;
        }
        private void IMG1_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            //var img = sender as Border;//ContentControl;
            //if (img == null)
            //    return;

            var img = BorderImg;

            //img.CaptureMouse();
            mouseDown = true;
            mouseXY = e.GetPosition(img);
        }

        private void Domousemove(Border img, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }
            var group = IMG.FindResource("Imageview") as TransformGroup;
            var transform = group.Children[1] as TranslateTransform;

            var rotatetransform = group.Children[2] as RotateTransform;

            var position = e.GetPosition(img);

            Console.WriteLine($"X:{position.X}--{mouseXY.X} \r\n Y:{position.Y}--{mouseXY.Y}");

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





}
