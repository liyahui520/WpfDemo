using System;
using System.Globalization;
using System.Windows.Data;

namespace Tools.Extend.Converters
{
    /// <summary>
    /// 布尔值反转转换器
    /// 用于将布尔值进行反转操作（true转false，false转true）
    /// 常用于控件的启用/禁用状态反转
    /// </summary>
    /// <remarks>
    /// 转换规则：
    /// - true → false
    /// - false → true
    /// - null → false
    /// </remarks>
    public class InverseBoolConverter : IValueConverter
    {
        /// <summary>
        /// 将布尔值进行反转
        /// </summary>
        /// <param name="value">输入的布尔值</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="parameter">转换参数（未使用）</param>
        /// <param name="culture">区域信息</param>
        /// <returns>反转后的布尔值</returns>
        /// <exception cref="ArgumentException">当输入值不是布尔类型时抛出</exception>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is bool boolValue)
                {
                    // 返回反转后的布尔值
                    return !boolValue;
                }
                
                // 如果输入为null或其他类型，默认返回false
                return false;
            }
            catch (Exception)
            {
                // 异常情况下返回false
                return false;
            }
        }

        /// <summary>
        /// 反向转换（将反转后的布尔值转换回原值）
        /// </summary>
        /// <param name="value">反转后的布尔值</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="parameter">转换参数（未使用）</param>
        /// <param name="culture">区域信息</param>
        /// <returns>原始布尔值</returns>
        /// <exception cref="ArgumentException">当输入值不是布尔类型时抛出</exception>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is bool boolValue)
                {
                    // 再次反转得到原值
                    return !boolValue;
                }
                
                // 如果输入为null或其他类型，默认返回false
                return false;
            }
            catch (Exception)
            {
                // 异常情况下返回false
                return false;
            }
        }
    }
}