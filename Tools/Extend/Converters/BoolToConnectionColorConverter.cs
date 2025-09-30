using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Tools.Extend.Converters
{
    /// <summary>
    /// 布尔值到连接状态颜色的转换器
    /// 用于将设备连接状态（true/false）转换为对应的颜色显示
    /// </summary>
    /// <remarks>
    /// 转换规则：
    /// - true（已连接）：绿色 (#4CAF50)
    /// - false（未连接）：红色 (#F44336)
    /// </remarks>
    public class BoolToConnectionColorConverter : IValueConverter
    {
        /// <summary>
        /// 将布尔值转换为连接状态颜色
        /// </summary>
        /// <param name="value">布尔值，表示连接状态</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="parameter">转换参数（未使用）</param>
        /// <param name="culture">区域信息</param>
        /// <returns>对应的颜色值</returns>
        /// <exception cref="ArgumentException">当输入值不是布尔类型时抛出</exception>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is bool isConnected)
                {
                    // 已连接：绿色，未连接：红色
                    return isConnected ? Colors.LimeGreen : Colors.Crimson;
                }
                
                // 默认返回灰色表示未知状态
                return Colors.Gray;
            }
            catch (Exception)
            {
                // 异常情况下返回灰色
                return Colors.Gray;
            }
        }

        /// <summary>
        /// 反向转换（不支持）
        /// </summary>
        /// <param name="value">颜色值</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="parameter">转换参数</param>
        /// <param name="culture">区域信息</param>
        /// <returns>抛出NotSupportedException</returns>
        /// <exception cref="NotSupportedException">不支持反向转换</exception>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("BoolToConnectionColorConverter不支持反向转换");
        }
    }
}