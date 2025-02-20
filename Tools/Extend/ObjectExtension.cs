using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Serialization;
using Newtonsoft.Json;

namespace Tools.Extend
{
    public static partial class ObjectExtension
    { 

        /// <summary>
        /// 将字典转化为QueryString格式
        /// </summary>
        /// <param name="dict"></param>
        /// <param name="urlEncode"></param>
        /// <returns></returns>
        public static string ToQueryString(this Dictionary<string, string> dict, bool urlEncode = true)
        {
            return string.Join("&", dict.Select(p => $"{(urlEncode ? p.Key?.UrlEncode() : "")}={(urlEncode ? p.Value?.UrlEncode() : "")}"));
        }

        /// <summary>
        /// 将字符串URL编码
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string UrlEncode(this string str)
        {
            return string.IsNullOrEmpty(str) ? "" : System.Uri.EscapeDataString(str);
        }
         

        /// <summary>
        /// 将object转换为long，若失败则返回0
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static long ParseToLong(this object obj)
        {
            try
            {
                return long.Parse(obj.ToString());
            }
            catch
            {
                return 0L;
            }
        }

        /// <summary>
        /// 将object转换为long，若失败则返回指定值
        /// </summary>
        /// <param name="str"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public static long ParseToLong(this string str, long defaultValue)
        {
            try
            {
                return long.Parse(str);
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// 将object转换为double，若失败则返回0
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static double ParseToDouble(this object obj)
        {
            try
            {
                return double.Parse(obj.ToString());
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 将object转换为double，若失败则返回指定值
        /// </summary>
        /// <param name="str"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public static double ParseToDouble(this object str, double defaultValue)
        {
            try
            {
                return double.Parse(str.ToString());
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// 将string转换为DateTime，若失败则返回日期最小值
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static DateTime ParseToDateTime(this string str)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(str))
                {
                    return DateTime.MinValue;
                }
                if (str.Contains('-') || str.Contains('/'))
                {
                    return DateTime.Parse(str);
                }
                else
                {
                    int length = str.Length;
                    switch (length)
                    {
                        case 4:
                            return DateTime.ParseExact(str, "yyyy", System.Globalization.CultureInfo.CurrentCulture);

                        case 6:
                            return DateTime.ParseExact(str, "yyyyMM", System.Globalization.CultureInfo.CurrentCulture);

                        case 8:
                            return DateTime.ParseExact(str, "yyyyMMdd", System.Globalization.CultureInfo.CurrentCulture);

                        case 10:
                            return DateTime.ParseExact(str, "yyyyMMddHH", System.Globalization.CultureInfo.CurrentCulture);

                        case 12:
                            return DateTime.ParseExact(str, "yyyyMMddHHmm", System.Globalization.CultureInfo.CurrentCulture);

                        case 14:
                            return DateTime.ParseExact(str, "yyyyMMddHHmmss", System.Globalization.CultureInfo.CurrentCulture);

                        default:
                            return DateTime.ParseExact(str, "yyyyMMddHHmmss", System.Globalization.CultureInfo.CurrentCulture);
                    }
                }
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        /// <summary>
        /// 获取周数 开始结束时间
        /// </summary>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <returns></returns>
        public static List<WeekYearNumber> GetWeekYearNumber(DateTime startDate, DateTime endDate)
        {
            List<WeekYearNumber> r = new List<WeekYearNumber>();

            // 找到距离 startDate 最近的星期日，作为第一周的开始日期
            DateTime firstSunday = startDate.AddDays(DayOfWeek.Monday - startDate.DayOfWeek);

            r.Add(new WeekYearNumber() { WeekStartTime = firstSunday, WeekEndTime = firstSunday.AddDays(6), YearStr = firstSunday.AddDays(6).ToString("yyyy"), YearWeekNumber = GetWeekNumber(firstSunday) });

            // 循环计算每周的开始和结束日期以及所在的年内周数
            DateTime weekStartDate = firstSunday.AddDays(7);
            while (weekStartDate <= endDate)
            {
                DateTime weekEndDate = weekStartDate.AddDays(6);
                int weekNumber = GetWeekNumber(weekEndDate);
                r.Add(new WeekYearNumber() { WeekStartTime = weekStartDate, WeekEndTime = weekEndDate, YearStr = weekEndDate.ToString("yyyy"), YearWeekNumber = weekNumber });
                weekStartDate = weekStartDate.AddDays(7);
            }

            // 获取指定日期所在的年内周数
            int GetWeekNumber(DateTime date)
            {
                System.Globalization.Calendar calendar = CultureInfo.InvariantCulture.Calendar;
                return calendar.GetWeekOfYear(date, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Friday);
            }

            return r;
        }

        public class WeekYearNumber
        {
            /// <summary>
            /// 周开始时间
            /// </summary>
            public DateTime WeekStartTime { get; set; }

            /// <summary>
            /// 周结束时间
            /// </summary>
            public DateTime WeekEndTime { get; set; }

            /// <summary>
            /// 所属年份
            /// </summary>
            public string YearStr { get; set; }

            /// <summary>
            /// 周所在年份的周期数
            /// </summary>
            public int YearWeekNumber { get; set; }
        }

        /// <summary>
        /// 将string转换为DateTime，若失败则返回默认值
        /// </summary>
        /// <param name="str"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public static DateTime ParseToDateTime(this string str, DateTime? defaultValue)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(str))
                {
                    return defaultValue.GetValueOrDefault();
                }
                if (str.Contains('-') || str.Contains('/'))
                {
                    return DateTime.Parse(str);
                }
                else
                {
                    int length = str.Length;
                    switch (length)
                    {
                        case 4:
                            return DateTime.ParseExact(str, "yyyy", System.Globalization.CultureInfo.CurrentCulture);

                        case 6:
                            return DateTime.ParseExact(str, "yyyyMM", System.Globalization.CultureInfo.CurrentCulture);

                        case 8:
                            return DateTime.ParseExact(str, "yyyyMMdd", System.Globalization.CultureInfo.CurrentCulture);

                        case 10:
                            return DateTime.ParseExact(str, "yyyyMMddHH", System.Globalization.CultureInfo.CurrentCulture);

                        case 12:
                            return DateTime.ParseExact(str, "yyyyMMddHHmm", System.Globalization.CultureInfo.CurrentCulture);

                        case 14:
                            return DateTime.ParseExact(str, "yyyyMMddHHmmss", System.Globalization.CultureInfo.CurrentCulture);

                        default:
                            return DateTime.ParseExact(str, "yyyyMMddHHmmss", System.Globalization.CultureInfo.CurrentCulture);
                    }
                }
            }
            catch
            {
                return defaultValue.GetValueOrDefault();
            }
        } 
        /// <summary>
        /// 是否有值
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static bool IsNullOrEmpty(this object obj)
        {
            return obj == null || string.IsNullOrEmpty(obj.ToString());
        }
         

        /// <summary>
        /// 将字符串转为值类型，若没有得到或者错误返回为空
        /// </summary>
        /// <typeparam name="T">指定值类型</typeparam>
        /// <param name="str">传入字符串</param>
        /// <returns>可空值</returns>
        public static T? ParseTo<T>(this string str) where T : struct
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(str))
                {
                    MethodInfo method = typeof(T).GetMethod("Parse", new Type[] { typeof(string) });
                    if (method != null)
                    {
                        T result = (T)method.Invoke(null, new string[] { str });
                        return result;
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        /// <summary>
        /// 将字符串转为值类型，若没有得到或者错误返回为空
        /// </summary>
        /// <param name="str">传入字符串</param>
        /// <param name="type">目标类型</param>
        /// <returns>可空值</returns>
        public static object ParseTo(this string str, Type type)
        {
            try
            {
                if (type.Name == "String")
                    return str;

                if (!string.IsNullOrWhiteSpace(str))
                {
                    var _type = type;
                    if (type.Name.StartsWith("Nullable"))
                        _type = type.GetGenericArguments()[0];

                    MethodInfo method = _type.GetMethod("Parse", new Type[] { typeof(string) });
                    if (method != null)
                        return method.Invoke(null, new string[] { str });
                }
            }
            catch
            {
            }
            return null;
        }

        /// <summary>
        /// 将一个对象属性值赋给另一个指定对象属性, 只复制相同属性的
        /// </summary>
        /// <param name="src">原数据对象</param>
        /// <param name="target">目标数据对象</param>
        /// <param name="changeProperties">属性集，键为原属性，值为目标属性</param>
        /// <param name="unChangeProperties">属性集，目标不修改的属性</param>
        public static void CopyTo(object src, object target, Dictionary<string, string> changeProperties = null, string[] unChangeProperties = null)
        {
            if (src == null || target == null)
                throw new ArgumentException("src == null || target == null ");

            var SourceType = src.GetType();
            var TargetType = target.GetType();

            if (changeProperties == null || changeProperties.Count == 0)
            {
                var fields = TargetType.GetProperties();
                changeProperties = fields.Select(m => m.Name).ToDictionary(m => m);
            }

            if (unChangeProperties == null || unChangeProperties.Length == 0)
            {
                foreach (var item in changeProperties)
                {
                    var srcProperty = SourceType.GetProperty(item.Key);
                    if (srcProperty != null)
                    {
                        var sourceVal = srcProperty.GetValue(src, null);

                        var tarProperty = TargetType.GetProperty(item.Value);
                        tarProperty?.SetValue(target, sourceVal, null);
                    }
                }
            }
            else
            {
                foreach (var item in changeProperties)
                {
                    if (!unChangeProperties.Any(m => m == item.Value))
                    {
                        var srcProperty = SourceType.GetProperty(item.Key);
                        if (srcProperty != null)
                        {
                            var sourceVal = srcProperty.GetValue(src, null);

                            var tarProperty = TargetType.GetProperty(item.Value);
                            tarProperty?.SetValue(target, sourceVal, null);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 日期转换年月
        /// </summary>
        /// <param name="date"></param>
        /// <param name="yearStr"></param>
        /// <param name="monthStr"></param>
        /// <param name="dayStr"></param>
        /// <returns></returns>
        public static string DateTimeToStr(this DateTime? date, string yearStr = "岁", string monthStr = "个月", string dayStr = "天")
        {
            if (!date.HasValue)
                return "";
            var startDate = date.Value;
            var endDate = DateTime.Now.AddDays(1).Date;
            // 计算年数差值
            int yearDiff = endDate.Year - startDate.Year;
            // 计算月数差值
            int monthDiff = endDate.Month - startDate.Month;
            // 计算天数差值
            int dayDiff = endDate.Day - startDate.Day;
            // 如果天数差值为负数，需要向月数和年数借位
            if (dayDiff < 0)
            {
                // 借位月数
                monthDiff--;
                // 如果月数差值为负数，需要向年数借位
                if (monthDiff < 0)
                {
                    yearDiff--;
                    monthDiff += 12;
                }
                // 计算实际的天数差值
                dayDiff += DateTime.DaysInMonth(startDate.Year, endDate.Month);
            }
            string year = yearDiff > 0 ? yearDiff.ToString() + yearStr : "";
            string month = monthDiff > 0 ? monthDiff.ToString() + monthStr : "";
            string days = dayDiff > 0 ? dayDiff.ToString() + dayStr : "";
            return $"{year}{month}{days}";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="xmlPath"></param>
        public static void ObjectSaveToXml(this object obj, string xmlPath)
        {
            if (obj == null && File.Exists(xmlPath))
            {
                File.Delete(xmlPath);
                return;
            }
            string path = Path.GetDirectoryName(Path.GetFullPath(xmlPath));
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            using (FileStream fs = File.Create(xmlPath))
            {
                XmlSerializer xml = new XmlSerializer(obj.GetType());
                xml.Serialize(fs, obj);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="xmlPath"></param>
        /// <returns></returns>
        public static T GetObjectByXml<T>(string xmlPath)
        {
            if (!File.Exists(xmlPath))
                return default(T);
            try
            {
                using (FileStream fs = File.Open(xmlPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    XmlSerializer xml = new XmlSerializer(typeof(T));
                    return (T)xml.Deserialize(fs);
                }
            }
            catch (Exception ex)
            { 
                return default(T);
            }
        }

        public static T GetObjectByJson<T>(string xmlpath)
        {
            if (!File.Exists(xmlpath))
                return default;
            return JsonConvert.DeserializeObject<T>(File.ReadAllText(xmlpath));
        }

        public static void ObjectSaveToFile(this object obj, string filePath)
        {
            using (FileStream fs = File.Create(filePath))
            {
                System.Runtime.Serialization.Formatters.Binary.BinaryFormatter format = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                format.Serialize(fs, obj);
            }
        }
        public static T GetObjectByFile<T>(string filePath)
        {
            using (FileStream fs = File.OpenRead(filePath))
            {
                System.Runtime.Serialization.Formatters.Binary.BinaryFormatter format = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                return (T)format.Deserialize(fs);
            }
        }


        public static T GetObjectByClass<T>(string dllName, string typeName)
        {
            string dll = AppDomain.CurrentDomain.BaseDirectory + dllName;
            if (!File.Exists(dll))
                return default(T);
            try
            {
                System.Reflection.Assembly assembly = System.Reflection.Assembly.LoadFile(dll);
                return (T)assembly.CreateInstance(typeName);
            }
            catch (Exception ex)
            { 
            }
            return default(T);
        }

        public static byte[] GetBytes(this string str, System.Text.Encoding encoder)
        {
            return encoder.GetBytes(str);
        }

        public static BitmapImage FromStream(this string path)
        {
            BitmapImage bitmapImage = new BitmapImage();
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    fileStream.CopyTo(memoryStream);
                }
                memoryStream.Seek(0, SeekOrigin.Begin); // 重置流的位置  

                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.StreamSource = memoryStream; // 从流解码  
                bitmapImage.EndInit();
                bitmapImage.Freeze();
            }
            return bitmapImage;
        }

        public static MemoryStream ToStream(this BitmapImage bitmapImage)
        {
            MemoryStream memoryStream = new MemoryStream();
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmapImage)); // 将 BitmapImage 添加到编码器  
            encoder.Save(memoryStream); // 保存到 MemoryStream   
            memoryStream.Seek(0, SeekOrigin.Begin); // 重置流的位置以供后续使用  
            return memoryStream;
        }

        public static BitmapImage FromByteArray(this byte[] byteArray)
        {
            using (MemoryStream memoryStream = new MemoryStream(byteArray))
            {
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memoryStream;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();
                return bitmapImage;
            }
        }

        public static byte[] ToByteArray(this BitmapImage bitmapImage)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                PngBitmapEncoder encoder = new PngBitmapEncoder();   // 选择合适的编码器  
                encoder.Frames.Add(BitmapFrame.Create(bitmapImage)); // 将 BitmapImage 添加到编码器  
                encoder.Save(memoryStream);     // 保存到 MemoryStream  
                return memoryStream.ToArray();  // 转换为 byte[]  
            }
        }
        /// <summary>
        /// Bitmap图像格式转为字节
        /// 多线程启用时经常出问题
        /// </summary>
        /// <param name="bitmap"></param>
        /// <returns></returns>
        public static byte[] Bitmap2Byte(this Bitmap bitmap)
        {
            using (Stream stream1 = new MemoryStream())
            {
                bitmap.Save(stream1, ImageFormat.Png);
                byte[] arr = new byte[stream1.Length];
                stream1.Position = 0;
                stream1.Read(arr, 0, (int)stream1.Length);
                stream1.Close();
                return arr;
            }
        }

        public static Bitmap Byte2Bitmap(this byte[] bytes)
        {
            byte[] bytelist = bytes;
            Bitmap bitmap;
            using (MemoryStream ms1 = new MemoryStream(bytelist))
            {
                bitmap = (Bitmap)System.Drawing.Image.FromStream(ms1);
                ms1.Close();
            }
            return bitmap;
        } 

        #region Bitmap与ImageSource互转
        /// <summary>
        /// Bitmap 转为ImageSource
        /// </summary>
        /// <param name="bitmap">Bitmap 对象</param>
        /// <returns>ImageSource 位图对象</returns>
        public static ImageSource BitmapToImageSource(this System.Drawing.Bitmap bitmap)
        {
            try
            {
                IntPtr intPtr = bitmap.GetHbitmap();
                ImageSource imageSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(intPtr, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                return imageSource;
            }
            catch (Exception ex)
            { 
            }
            return null;
        }

        /// <summary>
        /// ImageSource 转为Bitmap
        /// </summary>
        /// <param name="imageSource">imageSource 对象</param>
        /// <returns>返回 Bitmap 对象</returns>
        public static System.Drawing.Bitmap ImageSourceToBitmap(this ImageSource imageSource)
        {
            try
            {
                BitmapSource bitmapSource = (BitmapSource)imageSource;
                System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(bitmapSource.PixelWidth, bitmapSource.PixelHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                System.Drawing.Imaging.BitmapData data = bitmap.LockBits(new System.Drawing.Rectangle(System.Drawing.Point.Empty, bitmap.Size), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                bitmapSource.CopyPixels(Int32Rect.Empty, data.Scan0, data.Height * data.Stride, data.Stride);
                bitmap.UnlockBits(data);
                return bitmap;
            }
            catch (Exception ex)
            { 
            }
            return null;
        }
        #endregion

        #region  Bitmap与BitmapImage互转
        //将Bitmap对象转换成bitmapImage对象
        public static BitmapImage ConvertBitmapToBitmapImage(this Bitmap bitmap)
        {
            MemoryStream stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Bmp);
            BitmapImage image = new BitmapImage();
            image.BeginInit();
            image.StreamSource = stream;
            image.EndInit();
            return image;
        }

        //将bitmapImage对象转换成Bitmap对象
        public static System.Drawing.Bitmap BitmapImage2Bitmap(this BitmapImage bitmapImage)
        {
            using (System.IO.MemoryStream outStream = new System.IO.MemoryStream())
            {
                BitmapEncoder enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bitmapImage));
                enc.Save(outStream);
                System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(outStream);
                return bitmap;
            }
        }
        #endregion

        #region BitmapImage 转为byte[]
        /// <summary>
        /// BitmapImage 转为byte[]
        /// </summary>
        /// <param name="bitmapImage">BitmapImage 对象</param>
        /// <returns>byte[] 数组</returns>
        public static byte[] BitmapImageToByteArray(this BitmapImage bitmapImage)
        {
            byte[] buffer = new byte[] { };
            try
            {
                Stream stream = bitmapImage.StreamSource;
                if (stream != null && stream.Length > 0)
                {
                    stream.Position = 0;
                    using (BinaryReader binary = new BinaryReader(stream))
                    {
                        buffer = binary.ReadBytes((int)stream.Length);
                    }
                }
            }
            catch (Exception ex)
            { 
            }
            return buffer;
        }
        #endregion

        #region 图片压缩
        /// <summary>
        /// 图片压缩
        /// </summary>
        /// <param name="bitmap">要压缩的源图像</param>
        /// <param name="height">要求的高</param>
        /// <param name="width">要求的宽</param>
        /// <returns></returns>
        public static System.Drawing.Bitmap GetPicThumbnail(this System.Drawing.Bitmap bitmap, int height, int width)
        {
            try
            {
                lock (bitmap)
                {
                    System.Drawing.Bitmap iSource = bitmap;
                    System.Drawing.Imaging.ImageFormat imageFormat = iSource.RawFormat;
                    int sw = width, sh = height;
                    //按比例缩放
                    System.Drawing.Bitmap ob = new System.Drawing.Bitmap(width, height);
                    System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(ob);
                    graphics.Clear(System.Drawing.Color.WhiteSmoke);
                    graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                    graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    graphics.DrawImage(iSource, new System.Drawing.Rectangle((width - sw) / 2, (height - sh) / 2, sw, sh), 0, 0, iSource.Width, iSource.Height, System.Drawing.GraphicsUnit.Pixel);
                    graphics.Dispose();
                    return ob;
                }
            }
            catch (Exception ex)
            { 
            }
            return bitmap;
        }
        #endregion

        /// <summary>
        /// 保存再内存中
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        public static BitmapImage ToBitmapImage(this System.Drawing.Image image)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                image.Save(memory, image.RawFormat);
                memory.Position = 0;
                BitmapImage bitmapimage = new BitmapImage();
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad; // 缓存选项，根据需要选择
                bitmapimage.EndInit();
                bitmapimage.Freeze(); // 防止后续修改，提高性能
                return bitmapimage;
            }
        }

        /// <summary>
        ///视频获取缩略图
        /// </summary>
        /// <param name="mp4">视频路径</param>
        /// <param name="jpg">输出图片路径</param>
        /// <param name="frames">视频帧数</param>
        public static void ffmpeg(string mp4, string jpg, int frames)
        {
            try
            {
                System.Diagnostics.Process process = new System.Diagnostics.Process();
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.FileName = AppDomain.CurrentDomain.BaseDirectory + "/ffmpeg/" + "\\ffmpeg.exe";
                process.StartInfo.Arguments = $@"-i {mp4} -ss {frames} -f image2 {jpg}";
                process.Start();
                process.WaitForExit();
                process.Close();
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
            }
        }

        public static string GetDescription(Enum value)
        {
            FieldInfo field = value.GetType().GetField(value.ToString());
            DescriptionAttribute attribute = field.GetCustomAttribute<DescriptionAttribute>();
            return attribute == null ? value.ToString() : attribute.Description;
        }

        public static List<string> GetEnumDescriptions<T>() where T : Enum
        {
            return Enum.GetValues(typeof(T)).Cast<T>().Select(e => GetDescription(e)).ToList();
        }

        /// <summary>
        /// 获取文件夹下所有文件
        /// </summary>
        /// <param name="folderPath"></param>
        /// <returns></returns>
        public static List<string> GetAllFiles(this string folderPath)
        {
            List<string> filePaths = new List<string>();

            try
            {
                // 获取文件夹下的所有文件（包括子文件夹中的文件）
                string[] files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);

                // 将文件路径添加到列表中
                filePaths.AddRange(files);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred: {ex.Message}");
            }

            return filePaths;
        }
    }
}
