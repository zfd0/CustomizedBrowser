// CustomizedBrowser\Converters\BoolInvertConverter.cs
using System;
using System.Windows.Data;

namespace CustomizedBrowser.Converters
{
    /// <summary>
    /// 布尔值取反转换器
    /// </summary>
    public class BoolInvertConverter : IValueConverter
    {
        /// <summary>
        /// 正向转换：true -> false, false -> true
        /// </summary>
        public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return true;
        }

        /// <summary>
        /// 反向转换：true -> false, false -> true
        /// </summary>
        public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return true;
        }
    }
}