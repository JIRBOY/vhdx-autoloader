namespace VHDX_Manager.Models
{
    /// <summary>
    /// 环境检查结果
    /// </summary>
    public class EnvironmentCheckResult
    {
        /// <summary>
        /// virtdisk.dll 是否可用
        /// </summary>
        public bool IsVirtdiskAvailable { get; set; }

        /// <summary>
        /// WMI 服务是否可用
        /// </summary>
        public bool IsWmiAvailable { get; set; }

        /// <summary>
        /// 是否以管理员身份运行
        /// </summary>
        public bool IsAdmin { get; set; }

        /// <summary>
        /// Windows 版本是否支持
        /// </summary>
        public bool IsWindowsVersionSupported { get; set; }

        /// <summary>
        /// Windows 版本号
        /// </summary>
        public string WindowsVersion { get; set; } = string.Empty;

        /// <summary>
        /// .NET 版本
        /// </summary>
        public string DotNetVersion { get; set; } = string.Empty;

        /// <summary>
        /// 是否可以执行挂载操作
        /// </summary>
        public bool CanMount => IsVirtdiskAvailable && IsAdmin;

        /// <summary>
        /// 是否可以查询状态
        /// </summary>
        public bool CanQuery => IsWmiAvailable;

        /// <summary>
        /// 是否所有功能都可用
        /// </summary>
        public bool IsFullyFunctional => CanMount && CanQuery;

        /// <summary>
        /// 是否处于降级模式
        /// </summary>
        public bool IsDegradedMode => !IsFullyFunctional;

        /// <summary>
        /// 获取降级模式提示信息
        /// </summary>
        public string GetDegradedModeMessage()
        {
            if (IsFullyFunctional)
                return string.Empty;

            var messages = new List<string>();

            if (!IsVirtdiskAvailable)
                messages.Add("缺少 virtdisk.dll，挂载/卸载功能不可用");

            if (!IsWmiAvailable)
                messages.Add("WMI 服务不可用，无法自动扫描虚拟磁盘");

            if (!IsAdmin)
                messages.Add("未以管理员身份运行，部分功能受限");

            return string.Join("；", messages);
        }

        /// <summary>
        /// 获取降级模式类型
        /// </summary>
        public DegradedModeType GetDegradedModeType()
        {
            if (!IsVirtdiskAvailable && IsWmiAvailable)
                return DegradedModeType.WmiOnly;

            if (IsVirtdiskAvailable && !IsWmiAvailable)
                return DegradedModeType.VirtdiskOnly;

            if (!IsAdmin)
                return DegradedModeType.NoAdmin;

            return DegradedModeType.None;
        }
    }

    /// <summary>
    /// 降级模式类型
    /// </summary>
    public enum DegradedModeType
    {
        /// <summary>
        /// 无降级
        /// </summary>
        None,

        /// <summary>
        /// 仅 WMI 可用（只能查询）
        /// </summary>
        WmiOnly,

        /// <summary>
        /// 仅 virtdisk 可用（需手动输入路径）
        /// </summary>
        VirtdiskOnly,

        /// <summary>
        /// 无管理员权限
        /// </summary>
        NoAdmin
    }
}