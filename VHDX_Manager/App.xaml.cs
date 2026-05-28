using System;
using System.Security.Principal;
using System.Windows;

namespace VHDX_Manager
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 全局异常处理
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                MessageBox.Show(
                    $"未处理的异常:\n{ex?.Message}\n\n{ex?.StackTrace}",
                    "致命错误", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show(
                    $"UI线程异常:\n{args.Exception.Message}\n\n{args.Exception.StackTrace}",
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            // 检查管理员权限
            if (!IsRunAsAdmin())
            {
                // 尝试以管理员身份重新启动
                var result = MessageBox.Show(
                    "程序需要管理员权限才能正常工作。\n是否以管理员身份重新启动？",
                    "需要管理员权限",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        var exePath = Environment.ProcessPath;
                        if (!string.IsNullOrEmpty(exePath))
                        {
                            var startInfo = new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = exePath,
                                Verb = "runas",
                                UseShellExecute = true
                            };

                            System.Diagnostics.Process.Start(startInfo);
                            Shutdown();
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            $"无法以管理员身份启动程序: {ex.Message}",
                            "启动失败",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            }
        }

        /// <summary>
        /// 检查是否以管理员身份运行
        /// </summary>
        public static bool IsRunAsAdmin()
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
    }
}