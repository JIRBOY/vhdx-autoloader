using System;
using System.IO;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using VHDX_Manager.Helpers;
using VHDX_Manager.Services;

namespace VHDX_Manager.Services
{
    /// <summary>
    /// Windows 服务：开机自动挂载标记为自动挂载的 VHDX
    /// </summary>
    public class VhdService : ServiceBase
    {
        private CancellationTokenSource? _cts;
        private Task? _monitorTask;

        public VhdService()
        {
            ServiceName = "VHDXManager";
            CanStop = true;
            CanShutdown = true;
            AutoLog = true;
        }

        protected override void OnStart(string[] args)
        {
            _cts = new CancellationTokenSource();
            _monitorTask = Task.Run(() => RunAsync(_cts.Token));
        }

        protected override void OnStop()
        {
            _cts?.Cancel();
            _monitorTask?.Wait(TimeSpan.FromSeconds(10));
            _cts?.Dispose();
        }

        protected override void OnShutdown()
        {
            OnStop();
        }

        private async Task RunAsync(CancellationToken token)
        {
            try
            {
                var tracker = new AutoMountTracker();

                // 等待系统服务就绪
                await Task.Delay(3000, token);

                // 挂载所有标记为自动挂载的 VHDX
                var mounts = tracker.GetAllStates();
                foreach (var entry in mounts)
                {
                    if (token.IsCancellationRequested) break;
                    if (!entry.Value) continue;
                    if (!File.Exists(entry.Key)) continue;

                    MountVhdx(entry.Key);
                }

                // 按配置间隔定期检查，确保磁盘保持挂载
                var intervalMs = tracker.GetCheckIntervalMinutes() * 60 * 1000;
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(intervalMs, token);

                    // 重新读取配置以捕获新增的自动挂载磁盘
                    mounts = tracker.GetAllStates();
                    foreach (var entry in mounts)
                    {
                        if (token.IsCancellationRequested) break;
                        if (!entry.Value) continue;
                        if (!File.Exists(entry.Key)) continue;

                        if (!IsMounted(entry.Key))
                        {
                            MountVhdx(entry.Key);
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch { }
        }

        private static void MountVhdx(string filePath)
        {
            try
            {
                var storageType = new VIRTUAL_STORAGE_TYPE
                {
                    DeviceId = 0,
                    VendorId = NativeMethods.VIRTUAL_STORAGE_TYPE_VENDOR_MICROSOFT
                };

                var openParams = new OPEN_VIRTUAL_DISK_PARAMETERS
                {
                    Version = OPEN_VIRTUAL_DISK_VERSION.OPEN_VIRTUAL_DISK_VERSION_1,
                    Version1 = new OPEN_VIRTUAL_DISK_PARAMETERS_V1 { RWDepth = 1 }
                };

                uint result = NativeMethods.OpenVirtualDisk(
                    ref storageType,
                    filePath,
                    VIRTUAL_DISK_ACCESS_MASK.VIRTUAL_DISK_ACCESS_ATTACH_RW,
                    OPEN_VIRTUAL_DISK_FLAG.OPEN_VIRTUAL_DISK_FLAG_NONE,
                    ref openParams,
                    out var handle);

                if (result != 0) return;

                using (handle)
                {
                    var attachParams = new ATTACH_VIRTUAL_DISK_PARAMETERS
                    {
                        Version = ATTACH_VIRTUAL_DISK_VERSION.ATTACH_VIRTUAL_DISK_VERSION_1
                    };

                    NativeMethods.AttachVirtualDisk(
                        handle,
                        IntPtr.Zero,
                        ATTACH_VIRTUAL_DISK_FLAG.ATTACH_VIRTUAL_DISK_FLAG_PERMANENT_LIFETIME,
                        0,
                        ref attachParams,
                        IntPtr.Zero);
                }
            }
            catch { }
        }

        private static bool IsMounted(string filePath)
        {
            try
            {
                var storageType = new VIRTUAL_STORAGE_TYPE
                {
                    DeviceId = 0,
                    VendorId = NativeMethods.VIRTUAL_STORAGE_TYPE_VENDOR_MICROSOFT
                };

                var openParams = new OPEN_VIRTUAL_DISK_PARAMETERS
                {
                    Version = OPEN_VIRTUAL_DISK_VERSION.OPEN_VIRTUAL_DISK_VERSION_1,
                    Version1 = new OPEN_VIRTUAL_DISK_PARAMETERS_V1 { RWDepth = 1 }
                };

                uint result = NativeMethods.OpenVirtualDisk(
                    ref storageType,
                    filePath,
                    VIRTUAL_DISK_ACCESS_MASK.VIRTUAL_DISK_ACCESS_GET_INFO,
                    OPEN_VIRTUAL_DISK_FLAG.OPEN_VIRTUAL_DISK_FLAG_NONE,
                    ref openParams,
                    out var handle);

                if (result != 0) return false;
                handle.Dispose();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
