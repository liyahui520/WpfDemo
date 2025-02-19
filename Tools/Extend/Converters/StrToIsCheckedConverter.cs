using System;
using System.Globalization;
using System.Windows.Data;

namespace Tools.Extend.Converters
{
    public class StrToIsCheckedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.ToString().ToUpper() == parameter?.ToString().ToUpper();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (bool.TryParse(value?.ToString(), out var isChecked))
            {
                if (isChecked)
                {
                    return parameter;
                }
            }
            return value;
        }
    }
}
