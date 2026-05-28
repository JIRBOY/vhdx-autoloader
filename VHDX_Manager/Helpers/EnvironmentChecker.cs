using System;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;
using VHDX_Manager.Models;

namespace VHDX_Manager.Helpers
{
    /// <summary>
    /// 环境检查器
    /// </summary>
    public static class EnvironmentChecker
    {
        /// <summary>
        /// 执行环境检查
        /// </summary>
        public static EnvironmentCheckResult Check()
        {
            var result = new EnvironmentCheckResult();

            // 检查 Windows 版本
            result.IsWindowsVersionSupported = CheckWindowsVersion();
            result.WindowsVersion = Environment.OSVersion.Version.ToString();

            // 检查 .NET 版本
            result.DotNetVersion = RuntimeInformation.FrameworkDescription;

            // 检查管理员权限
            result.IsAdmin = CheckAdminPrivileges();

            // 检查 virtdisk.dll
            result.IsVirtdiskAvailable = CheckVirtdiskDll();

            // 检查 WMI 服务
            result.IsWmiAvailable = CheckWmiService();

            return result;
        }

        /// <summary>
        /// 检查 Windows 版本 (需要 Windows 10 或更高版本)
        /// </summary>
        private static bool CheckWindowsVersion()
        {
            try
            {
                var version = Environment.OSVersion.Version;
                // Windows 10 版本号为 10.0.x
                return version.Major >= 10;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 检查管理员权限
        /// </summary>
        private static bool CheckAdminPrivileges()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 检查 virtdisk.dll 是否可用
        /// </summary>
        private static bool CheckVirtdiskDll()
        {
            IntPtr handle = IntPtr.Zero;
            try
            {
                handle = NativeMethods.LoadLibrary("virtdisk.dll");
                return handle != IntPtr.Zero;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (handle != IntPtr.Zero)
                {
                    NativeMethods.FreeLibrary(handle);
                }
            }
        }

        /// <summary>
        /// 检查 WMI 服务是否可用
        /// </summary>
        private static bool CheckWmiService()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "root\\microsoft\\windows\\storage",
                    "SELECT * FROM MSFT_Disk WHERE BusType = 15");

                // 尝试执行查询
                searcher.Get();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 尝试以管理员身份重新启动程序
        /// </summary>
        public static bool TryRestartAsAdmin()
        {
            try
            {
                var exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath))
                    return false;

                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exePath,
                    Verb = "runas",
                    UseShellExecute = true
                };

                System.Diagnostics.Process.Start(startInfo);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}