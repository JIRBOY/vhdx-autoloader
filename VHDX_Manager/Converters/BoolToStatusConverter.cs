using System;
using System.Globalization;
using System.Windows.Data;
using VHDX_Manager.Models;

namespace VHDX_Manager.Converters
{
    /// <summary>
    /// 布尔值转状态文本转换器
    /// </summary>
    public class BoolToStatusConverter : IValueConverter
    {
        /// <summary>
        /// 转换布尔值为状态文本
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                // 检查参数是否指定了反转
                bool invert = parameter is string str && str.Equals("Invert", StringComparison.OrdinalIgnoreCase);

                if (invert)
                {
                    return boolValue ? "否" : "是";
                }

                return boolValue ? "是" : "否";
            }

            return "未知";
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
    /// 布尔值转挂载状态文本转换器
    /// </summary>
    public class BoolToMountStatusConverter : IValueConverter
    {
        /// <summary>
        /// 转换布尔值为挂载状态文本
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? "已挂载" : "未挂载";
            }

            return "未知";
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
    /// 布尔值转自动挂载状态文本转换器
    /// </summary>
    public class BoolToAutoMountStatusConverter : IValueConverter
    {
        /// <summary>
        /// 转换布尔值为自动挂载状态文本
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? "自动挂载" : "手动挂载";
            }

            return "未知";
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
    /// 挂载偏好转文本转换器
    /// </summary>
    public class MountPreferenceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is MountPreference pref)
            {
                return pref switch
                {
                    MountPreference.AutoDriveLetter => "自动盘符",
                    MountPreference.ManualDriveLetter => "手动盘符",
                    MountPreference.ManualFolderPath => "手动路径",
                    MountPreference.AutoFolderPath => "自动路径",
                    _ => "自动盘符"
                };
            }
            return "自动盘符";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 布尔值转服务按钮文本转换器
    /// </summary>
    public class BoolToServiceButtonConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool installed)
                return installed ? "卸载服务" : "安装服务";
            return "安装服务";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}