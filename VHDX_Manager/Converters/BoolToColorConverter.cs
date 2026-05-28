using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace VHDX_Manager.Converters
{
    /// <summary>
    /// 布尔值转颜色转换器
    /// </summary>
    public class BoolToColorConverter : IValueConverter
    {
        /// <summary>
        /// 转换布尔值为颜色
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                // 检查参数是否指定了反转
                bool invert = parameter is string str && str.Equals("Invert", StringComparison.OrdinalIgnoreCase);

                if (invert)
                {
                    return boolValue
                        ? new SolidColorBrush(Color.FromRgb(158, 158, 158)) // 灰色
                        : new SolidColorBrush(Color.FromRgb(76, 175, 80));  // 绿色
                }

                return boolValue
                    ? new SolidColorBrush(Color.FromRgb(76, 175, 80))   // 绿色
                    : new SolidColorBrush(Color.FromRgb(158, 158, 158)); // 灰色
            }

            return new SolidColorBrush(Color.FromRgb(158, 158, 158)); // 灰色
        }

        /// <summary>
        /// 反向转换（未实现）
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 布尔值转状态颜色转换器
    /// </summary>
    public class BoolToStatusColorConverter : IValueConverter
    {
        /// <summary>
        /// 转换布尔值为状态颜色
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue
                    ? new SolidColorBrush(Color.FromRgb(76, 175, 80))   // 绿色 - 已挂载
                    : new SolidColorBrush(Color.FromRgb(158, 158, 158)); // 灰色 - 未挂载
            }

            return new SolidColorBrush(Color.FromRgb(158, 158, 158)); // 灰色
        }

        /// <summary>
        /// 反向转换（未实现）
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 布尔值转可见性转换器
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// 转换布尔值为可见性
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                // 检查参数是否指定了反转
                bool invert = parameter is string str && str.Equals("Invert", StringComparison.OrdinalIgnoreCase);

                if (invert)
                {
                    return boolValue ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
                }

                return boolValue ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            }

            return System.Windows.Visibility.Collapsed;
        }

        /// <summary>
        /// 反向转换（未实现）
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}