using System;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Data;

namespace Tools.Extend.Converters
{
    /// <summary>
    /// 枚举值转枚举名称 （parameter传枚举类型）
    /// </summary>
    [ValueConversion(typeof(int), typeof(string))]
    public class EnumValueToEnumNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value == null) return DependencyProperty.UnsetValue;
                Assembly assem = Assembly.GetExecutingAssembly();//.GetAssembly();
                Type type = assem.GetType(parameter.ToString());
                var enumName = Enum.Parse(type, value.ToString());
                return enumName;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
