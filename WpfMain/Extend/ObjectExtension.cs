using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Xml.Serialization;
using Newtonsoft.Json;

namespace WpfMain.Extend
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
                Calendar calendar = CultureInfo.InvariantCulture.Calendar;
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

    }
}
