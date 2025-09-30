using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace OpenCv.Converters
{
    /// <summary>
    /// 反转布尔值到可见性转换器
    /// 当布尔值为 false 时返回 Visible，为 true 时返回 Collapsed
    /// </summary>
    public class InvertedBooleanToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// 将布尔值转换为可见性枚举（反转逻辑）
        /// </summary>
        /// <param name="value">要转换的布尔值</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="parameter">转换参数</param>
        /// <param name="culture">区域信息</param>
        /// <returns>反转后的可见性枚举值</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                // 反转逻辑：false 显示，true 隐藏
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            }
            
            return Visibility.Visible;
        }

        /// <summary>
        /// 将可见性枚举转换回布尔值（反转逻辑）
        /// </summary>
        /// <param name="value">要转换的可见性枚举值</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="parameter">转换参数</param>
        /// <param name="culture">区域信息</param>
        /// <returns>反转后的布尔值</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                // 反转逻辑：Visible 对应 false，Collapsed 对应 true
                return visibility != Visibility.Visible;
            }
            
            return false;
        }
    }
}