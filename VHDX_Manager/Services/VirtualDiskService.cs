using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using VHDX_Manager.Helpers;
using VHDX_Manager.Models;

namespace VHDX_Manager.Services
{
    /// <summary>
    /// 虚拟磁盘服务实现（基于 virtdisk.dll）
    /// </summary>
    public class VirtualDiskService : IVirtualDiskService
    {
        private readonly AutoMountTracker _autoMountTracker;

        public VirtualDiskService(AutoMountTracker autoMountTracker)
        {
            _autoMountTracker = autoMountTracker;
        }

        /// <summary>
        /// 挂载虚拟磁盘
        /// 始终使用 NO_DRIVE_LETTER 标志防止 Windows 自动分配不可预测的盘符，
        /// 之后按需通过 WMI AddAccessPath 手动指定保存的盘符或分配新盘符。
        /// </summary>
        public async Task<(bool Success, string Message)> MountAsync(
            string filePath,
            bool autoMount = false,
            MountPreference mountPreference = MountPreference.AutoDriveLetter,
            string targetMountPath = "",
            string savedDriveLetter = "")
        {
            return await Task.Run(() =>
            {
                try
                {
                    // 验证文件存在
                    if (!File.Exists(filePath))
                    {
                        return (false, "文件不存在");
                    }

                    // 验证文件扩展名
                    var extension = Path.GetExtension(filePath).ToLowerInvariant();
                    if (extension != ".vhd" && extension != ".vhdx")
                    {
                        return (false, "不支持的文件格式，请选择 .vhd 或 .vhdx 文件");
                    }

                    // 确定存储类型（使用 DeviceId=0 让系统自动检测磁盘类型）
                    var storageType = new VIRTUAL_STORAGE_TYPE
                    {
                        DeviceId = 0,
                        VendorId = NativeMethods.VIRTUAL_STORAGE_TYPE_VENDOR_MICROSOFT
                    };

                    // 打开虚拟磁盘
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
                        out SafeFileHandle handle);

                    if (result != 0)
                    {
                        return (false, $"打开虚拟磁盘失败，错误码: 0x{result:X8}");
                    }

                    // 根据挂载模式决定 flags
                    var flags = ATTACH_VIRTUAL_DISK_FLAG.ATTACH_VIRTUAL_DISK_FLAG_PERMANENT_LIFETIME;

                    // 自动盘符模式：不设 NO_DRIVE_LETTER，让系统自动分配盘符
                    // 其他模式：设 NO_DRIVE_LETTER，由程序自行管理挂载路径
                    if (mountPreference != MountPreference.AutoDriveLetter)
                    {
                        flags |= ATTACH_VIRTUAL_DISK_FLAG.ATTACH_VIRTUAL_DISK_FLAG_NO_DRIVE_LETTER;
                    }

                    using (handle)
                    {
                        var attachParams = new ATTACH_VIRTUAL_DISK_PARAMETERS
                        {
                            Version = ATTACH_VIRTUAL_DISK_VERSION.ATTACH_VIRTUAL_DISK_VERSION_1
                        };

                        result = NativeMethods.AttachVirtualDisk(
                            handle,
                            IntPtr.Zero,
                            flags,
                            0,
                            ref attachParams,
                            IntPtr.Zero);

                        if (result != 0)
                        {
                            return (false, $"挂载虚拟磁盘失败，错误码: 0x{result:X8}");
                        }
                    }

                    // 挂载成功后，按模式处理挂载路径
                    string assignedDriveLetter = string.Empty;

                    switch (mountPreference)
                    {
                        case MountPreference.AutoDriveLetter:
                            // 系统自动分配盘符，等待并查询实际分配的盘符
                            assignedDriveLetter = WmiQueryService.QueryAssignedDriveLetter(filePath, 15);
                            break;

                        case MountPreference.ManualDriveLetter:
                            // 手动分配指定盘符：检查当前盘符 → 一致则不修改，不一致则移除旧盘符后添加新盘符
                            if (!string.IsNullOrEmpty(savedDriveLetter))
                            {
                                string desiredLetter = savedDriveLetter.TrimEnd(':').ToUpperInvariant();
                                string currentLetter = WmiQueryService.QueryAssignedDriveLetter(filePath, 10);

                                if (string.Equals(currentLetter, desiredLetter, StringComparison.OrdinalIgnoreCase))
                                {
                                    // 当前盘符已匹配，无需修改
                                    assignedDriveLetter = desiredLetter;
                                }
                                else
                                {
                                    // 移除旧盘符（如果存在）
                                    if (!string.IsNullOrEmpty(currentLetter))
                                    {
                                        WmiQueryService.RemoveDriveLetter(filePath, currentLetter);
                                    }

                                    // 分配新盘符
                                    var assignResult = WmiQueryService.AssignDriveLetter(filePath, desiredLetter);
                                    if (assignResult.Success)
                                    {
                                        assignedDriveLetter = desiredLetter;
                                    }
                                    else
                                    {
                                        // 分配失败，尝试查询系统实际分配的盘符
                                        assignedDriveLetter = WmiQueryService.QueryAssignedDriveLetter(filePath, 5);
                                    }
                                }
                            }
                            break;

                        case MountPreference.ManualFolderPath:
                            // 手动路径：挂载到指定文件夹
                            if (!string.IsNullOrWhiteSpace(targetMountPath))
                            {
                                var mountResult = CreateFolderMountPoint(filePath, targetMountPath);
                                if (!mountResult.Success)
                                {
                                    return mountResult;
                                }
                            }
                            break;

                        case MountPreference.AutoFolderPath:
                            // 自动路径：系统管理文件夹挂载（暂与自动盘符相同，后续测试验证）
                            assignedDriveLetter = WmiQueryService.QueryAssignedDriveLetter(filePath, 15);
                            break;
                    }

                    // 保存实际分配的盘符
                    if (!string.IsNullOrEmpty(assignedDriveLetter))
                    {
                        _autoMountTracker.SetSavedDriveLetter(filePath, assignedDriveLetter);
                    }

                    // 记录配置
                    _autoMountTracker.SetAutoMount(filePath, autoMount);
                    _autoMountTracker.SetMountPreference(filePath, mountPreference);
                    if (mountPreference == MountPreference.ManualFolderPath || mountPreference == MountPreference.AutoFolderPath)
                    {
                        _autoMountTracker.SetTargetMountPath(filePath, targetMountPath);
                    }

                    return (true, "挂载成功");
                }
                catch (Exception ex)
                {
                    return (false, $"挂载失败: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// 卸载虚拟磁盘
        /// </summary>
        public async Task<(bool Success, string Message)> UnmountAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // 验证文件存在
                    if (!File.Exists(filePath))
                    {
                        return (false, "文件不存在");
                    }

                    // 确定存储类型（使用 DeviceId=0 让系统自动检测磁盘类型）
                    var storageType = new VIRTUAL_STORAGE_TYPE
                    {
                        DeviceId = 0, // VIRTUAL_STORAGE_TYPE_DEVICE_UNKNOWN - 自动检测
                        VendorId = NativeMethods.VIRTUAL_STORAGE_TYPE_VENDOR_MICROSOFT
                    };

                    // 打开虚拟磁盘
                    var openParams = new OPEN_VIRTUAL_DISK_PARAMETERS
                    {
                        Version = OPEN_VIRTUAL_DISK_VERSION.OPEN_VIRTUAL_DISK_VERSION_1,
                        Version1 = new OPEN_VIRTUAL_DISK_PARAMETERS_V1 { RWDepth = 1 }
                    };

                    uint result = NativeMethods.OpenVirtualDisk(
                        ref storageType,
                        filePath,
                        VIRTUAL_DISK_ACCESS_MASK.VIRTUAL_DISK_ACCESS_DETACH,
                        OPEN_VIRTUAL_DISK_FLAG.OPEN_VIRTUAL_DISK_FLAG_NONE,
                        ref openParams,
                        out SafeFileHandle handle);

                    if (result != 0)
                    {
                        return (false, $"打开虚拟磁盘失败，错误码: 0x{result:X8}");
                    }

                    using (handle)
                    {
                        // 卸载虚拟磁盘
                        result = NativeMethods.DetachVirtualDisk(
                            handle,
                            DETACH_VIRTUAL_DISK_FLAG.DETACH_VIRTUAL_DISK_FLAG_NONE,
                            0);

                        if (result != 0)
                        {
                            return (false, $"卸载虚拟磁盘失败，错误码: 0x{result:X8}");
                        }
                    }

                    // 卸载后移除自动挂载记录
                    _autoMountTracker.Remove(filePath);

                    return (true, "卸载成功");
                }
                catch (Exception ex)
                {
                    return (false, $"卸载失败: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// 设置自动挂载状态
        /// </summary>
        /// <remarks>
        /// 所有挂载都使用 PERMANENT_LIFETIME 标志（脱离句柄生命周期），
        /// 因此切换自动挂载只需更新本地配置记录，无需卸载再重新挂载。
        /// </remarks>
        public async Task<(bool Success, string Message)> SetAutoMountAsync(string filePath, bool autoMount)
        {
            return await Task.Run(() =>
            {
                try
                {
                    _autoMountTracker.SetAutoMount(filePath, autoMount);
                    return (true, autoMount ? "已设置开机自动挂载" : "已取消开机自动挂载");
                }
                catch (Exception ex)
                {
                    return (false, $"设置失败: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// 创建文件夹挂载点
        /// </summary>
        internal static (bool Success, string Message) CreateFolderMountPoint(string vhdxPath, string folderPath)
        {
            try
            {
                // 确保目标文件夹存在
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                // 获取卷 GUID 路径（如 \\?\Volume{xxx}\）
                string? volumeGuidPath = GetVolumeGuidPath(vhdxPath);

                if (string.IsNullOrEmpty(volumeGuidPath))
                {
                    return (false, "无法获取卷路径，文件夹挂载失败");
                }

                // 确保文件夹路径以反斜杠结尾（SetVolumeMountPoint 要求）
                if (!folderPath.EndsWith("\\"))
                {
                    folderPath += "\\";
                }

                bool ok = NativeMethods.SetVolumeMountPoint(folderPath, volumeGuidPath);
                if (!ok)
                {
                    int err = Marshal.GetLastWin32Error();
                    return (false, $"设置文件夹挂载点失败，错误码: 0x{err:X8}");
                }

                return (true, "挂载成功");
            }
            catch (Exception ex)
            {
                return (false, $"文件夹挂载失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取虚拟磁盘的卷 GUID 路径（\\?\Volume{xxx}\）
        /// 优先通过当前盘符获取，失败则回退到 WMI 查询
        /// </summary>
        private static string? GetVolumeGuidPath(string vhdxPath)
        {
            // 策略1: 通过当前盘符直接获取卷 GUID（最可靠）
            string driveLetter = WmiQueryService.QueryAssignedDriveLetter(vhdxPath, 3);
            if (!string.IsNullOrEmpty(driveLetter))
            {
                string mountPoint = driveLetter.TrimEnd(':') + ":\\";
                var guid = NativeMethods.GetVolumeGuidName(mountPoint);
                if (!string.IsNullOrEmpty(guid))
                {
                    return guid;
                }
            }

            // 策略2: 回退到 WMI 查询（用于刚挂载尚未分配盘符的情况）
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    using var searcher = new System.Management.ManagementObjectSearcher(
                        @"root\microsoft\windows\storage",
                        $"SELECT * FROM MSFT_Disk WHERE Location = '{vhdxPath.Replace("\\", "\\\\").Replace("'", "\\'")}'");

                    foreach (System.Management.ManagementObject disk in searcher.Get())
                    {
                        var partitions = disk.GetRelated("MSFT_Partition");
                        foreach (System.Management.ManagementObject partition in partitions)
                        {
                            // 从 AccessPaths 中提取卷 GUID 路径
                            var accessPaths = partition["AccessPaths"] as string[];
                            if (accessPaths != null)
                            {
                                foreach (var ap in accessPaths)
                                {
                                    if (!string.IsNullOrEmpty(ap) && ap.StartsWith("\\\\?\\Volume{"))
                                    {
                                        return ap.EndsWith("\\") ? ap : ap + "\\";
                                    }
                                }
                            }

                            // 后备：从 MSFT_Volume 获取 Path
                            var volumes = partition.GetRelated("MSFT_Volume");
                            foreach (System.Management.ManagementObject volume in volumes)
                            {
                                var path = volume["Path"]?.ToString();
                                if (!string.IsNullOrEmpty(path))
                                {
                                    return path.EndsWith("\\") ? path : path + "\\";
                                }
                            }
                        }
                    }
                }
                catch { }

                System.Threading.Thread.Sleep(500);
            }

            return null;
        }

        /// <summary>
        /// 获取虚拟磁盘大小信息
        /// </summary>
        public async Task<(long TotalSize, long FreeSpace)> GetDiskSizeAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(filePath))
                    {
                        return (0, 0);
                    }

                    var extension = Path.GetExtension(filePath).ToLowerInvariant();
                    var storageType = new VIRTUAL_STORAGE_TYPE
                    {
                        DeviceId = 0, // 自动检测磁盘类型
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
                        out SafeFileHandle handle);

                    if (result != 0)
                    {
                        return (0, 0);
                    }

                    using (handle)
                    {
                        result = NativeMethods.GetVirtualDiskInfoSize(handle, out VIRTUAL_DISK_SIZE sizeInfo);
                        if (result == 0)
                        {
                            return ((long)sizeInfo.VirtualSize, (long)(sizeInfo.VirtualSize - sizeInfo.PhysicalSize));
                        }
                    }

                    return (0, 0);
                }
                catch
                {
                    return (0, 0);
                }
            });
        }
    }
}
