// CustomizedBrowser\Converters\MultiValueConverter.cs
using System;
using System.Globalization;
using System.Windows.Data;

namespace CustomizedBrowser.Converters
{
    /// <summary>
    /// 多值转换器：将多个绑定值合并为对象数组
    /// </summary>
    public class MultiValueConverter : IMultiValueConverter
    {
        /// <summary>
        /// 将多个绑定值转换为对象数组
        /// </summary>
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            return values.Clone();
        }

        /// <summary>
        /// 反向转换（不支持）
        /// </summary>
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}