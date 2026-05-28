using System;
using System.Diagnostics;
using System.Security.Principal;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace VHDX_Manager.Services
{
    public static class WindowsServiceManager
    {
        private const string ServiceName = "VHDXManager";
        private const string DisplayName = "VHDX Manager 自动挂载服务";
        private const string Description = "自动挂载标记为开机自动挂载的 VHDX 虚拟磁盘";

        /// <summary>
        /// 检查 VHDXManager 服务是否已安装。
        /// </summary>
        public static bool IsInstalled()
        {
            try
            {
                using var controller = new ServiceController(ServiceName);
                // 访问 ServiceName 即可验证服务存在
                var status = controller.Status;
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        /// <summary>
        /// 获取服务当前状态。服务未安装时返回 null。
        /// </summary>
        public static ServiceControllerStatus? GetStatus()
        {
            try
            {
                using var controller = new ServiceController(ServiceName);
                return controller.Status;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        /// <summary>
        /// 获取服务启动模式。服务未安装时返回 null。
        /// </summary>
        public static ServiceStartMode? GetStartMode()
        {
            try
            {
                using var controller = new ServiceController(ServiceName);
                return controller.StartType;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        /// <summary>
        /// 安装 Windows 服务（sc create，start= demand）。
        /// </summary>
        public static async Task<(bool Success, string Error)> InstallAsync()
        {
            var binPath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(binPath))
                return (false, "无法获取程序路径");

            var arguments = $"create \"{ServiceName}\" binPath= \"{binPath}\" start= demand DisplayName= \"{DisplayName}\"";
            var result = await RunScAsync(arguments);

            if (result.Success)
            {
                // 设置服务描述
                await RunScAsync($"description \"{ServiceName}\" \"{Description}\"");
            }

            return result;
        }

        /// <summary>
        /// 卸载 Windows 服务（先停止再 sc delete）。
        /// </summary>
        public static async Task<(bool Success, string Error)> UninstallAsync()
        {
            try
            {
                if (!IsInstalled())
                    return (true, "服务未安装，无需卸载");

                // 先停止服务（如果正在运行）
                try
                {
                    using var controller = new ServiceController(ServiceName);
                    if (controller.Status != ServiceControllerStatus.Stopped)
                    {
                        controller.Stop();
                        controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                    }
                }
                catch (Exception ex)
                {
                    return (false, $"停止服务失败: {ex.Message}");
                }

                // 删除服务
                return await RunScAsync($"delete \"{ServiceName}\"");
            }
            catch (Exception ex)
            {
                return (false, $"卸载服务失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 设置服务启动模式（auto 或 demand）。
        /// </summary>
        public static async Task<(bool Success, string Error)> SetStartModeAsync(string startMode)
        {
            if (startMode != "auto" && startMode != "demand")
                return (false, $"无效的启动模式: {startMode}，仅支持 auto 或 demand");

            return await RunScAsync($"config \"{ServiceName}\" start= {startMode}");
        }

        /// <summary>
        /// 启动 VHDXManager 服务。
        /// </summary>
        public static async Task<(bool Success, string Error)> StartAsync()
        {
            return await Task.Run(() =>
            {
                if (!IsInstalled())
                    return (false, "服务未安装");

                try
                {
                    using var controller = new ServiceController(ServiceName);
                    if (controller.Status == ServiceControllerStatus.Running)
                        return (true, "服务已在运行中");

                    controller.Start();
                    controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                    return (true, string.Empty);
                }
                catch (Exception ex)
                {
                    return (false, ex.Message);
                }
            });
        }

        /// <summary>
        /// 停止 VHDXManager 服务。
        /// </summary>
        public static async Task<(bool Success, string Error)> StopAsync()
        {
            return await Task.Run(() =>
            {
                if (!IsInstalled())
                    return (false, "服务未安装");

                try
                {
                    using var controller = new ServiceController(ServiceName);
                    if (controller.Status == ServiceControllerStatus.Stopped)
                        return (true, "服务已停止");

                    controller.Stop();
                    controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                    return (true, string.Empty);
                }
                catch (Exception ex)
                {
                    return (false, ex.Message);
                }
            });
        }

        /// <summary>
        /// 根据是否存在自动挂载磁盘，同步服务启动模式。
        /// 有自动挂载磁盘时设为 auto，否则设为 demand。
        /// </summary>
        public static async Task<(bool Success, string Error)> SyncStartModeAsync(bool hasAutoMountDisks)
        {
            if (!IsInstalled())
                return (true, "服务未安装，跳过同步");

            var currentMode = GetStartMode();
            var targetMode = hasAutoMountDisks ? "auto" : "demand";

            // 当前启动模式已经是目标模式，无需更改
            if (currentMode == (hasAutoMountDisks ? ServiceStartMode.Automatic : ServiceStartMode.Manual))
                return (true, "启动模式已是目标状态");

            return await SetStartModeAsync(targetMode);
        }

        /// <summary>
        /// 运行 sc.exe 命令并返回结果。
        /// </summary>
        private static Task<(bool Success, string Error)> RunScAsync(string arguments)
        {
            try
            {
                var isAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent())
                    .IsInRole(WindowsBuiltInRole.Administrator);

                ProcessStartInfo startInfo;

                if (isAdmin)
                {
                    // 已经是管理员，直接运行，不需要 UAC 提权
                    startInfo = new ProcessStartInfo
                    {
                        FileName = "sc.exe",
                        Arguments = arguments,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                }
                else
                {
                    // 非管理员，通过 runas 提权
                    startInfo = new ProcessStartInfo
                    {
                        FileName = "sc.exe",
                        Arguments = arguments,
                        Verb = "runas",
                        UseShellExecute = true,
                        CreateNoWindow = true
                    };
                }

                using var process = Process.Start(startInfo);
                if (process == null)
                    return Task.FromResult((false, "无法启动 sc.exe"));

                process.WaitForExit(30000);

                if (process.ExitCode == 0)
                    return Task.FromResult((true, string.Empty));

                if (isAdmin)
                {
                    var error = process.StandardError.ReadToEnd();
                    var output = process.StandardOutput.ReadToEnd();
                    var msg = string.IsNullOrWhiteSpace(error) ? output.Trim() : error.Trim();
                    return Task.FromResult((false, string.IsNullOrEmpty(msg) ? $"sc.exe 返回错误码: {process.ExitCode}" : msg));
                }

                return Task.FromResult((false, $"sc.exe 返回错误码: {process.ExitCode}"));
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                return Task.FromResult((false, "用户取消了操作，需要管理员权限"));
            }
            catch (Exception ex)
            {
                return Task.FromResult((false, ex.Message));
            }
        }
    }
}
