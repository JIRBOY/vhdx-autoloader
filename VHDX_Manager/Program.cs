using System;
using System.Linq;
using System.ServiceProcess;
using VHDX_Manager.Services;

namespace VHDX_Manager
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                var cmd = args[0].ToLowerInvariant();

                if (cmd == "--service")
                {
                    // 服务模式：运行 VHDX 自动挂载服务
                    ServiceBase.Run(new VhdService());
                    return;
                }

                if (cmd == "--install")
                {
                    InstallService();
                    return;
                }

                if (cmd == "--uninstall")
                {
                    UninstallService();
                    return;
                }
            }

            // GUI 模式：启动 WPF 界面
            var app = new App();
            app.InitializeComponent();
            app.Run(new Views.MainWindow());
        }

        private static void InstallService()
        {
            try
            {
                var exePath = Environment.ProcessPath;
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = $"create VHDXManager binPath= \"{exePath} --service\" start= auto DisplayName= \"VHDX Manager 自动挂载服务\"",
                    Verb = "runas",
                    UseShellExecute = true,
                    CreateNoWindow = true
                };
                var p = System.Diagnostics.Process.Start(psi);
                p?.WaitForExit();

                // 设置描述
                psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = "description VHDXManager \"自动挂载标记为开机自动挂载的 VHDX 虚拟磁盘\"",
                    Verb = "runas",
                    UseShellExecute = true,
                    CreateNoWindow = true
                };
                p = System.Diagnostics.Process.Start(psi);
                p?.WaitForExit();

                Console.WriteLine("服务安装成功。重启后将自动挂载标记的 VHDX。");
                Console.WriteLine("按任意键退出...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"安装服务失败: {ex.Message}");
            }
        }

        private static void UninstallService()
        {
            try
            {
                // 先停止服务
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = "stop VHDXManager",
                    Verb = "runas",
                    UseShellExecute = true,
                    CreateNoWindow = true
                };
                var p = System.Diagnostics.Process.Start(psi);
                p?.WaitForExit();

                // 删除服务
                psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = "delete VHDXManager",
                    Verb = "runas",
                    UseShellExecute = true,
                    CreateNoWindow = true
                };
                p = System.Diagnostics.Process.Start(psi);
                p?.WaitForExit();

                Console.WriteLine("服务已卸载。");
                Console.WriteLine("按任意键退出...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"卸载服务失败: {ex.Message}");
            }
        }
    }
}
