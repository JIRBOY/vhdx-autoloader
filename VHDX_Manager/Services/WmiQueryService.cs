using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Text.Json;
using System.Threading;
using VHDX_Manager.Helpers;
using VHDX_Manager.Models;

namespace VHDX_Manager.Services
{
    /// <summary>
    /// WMI 查询服务
    /// </summary>
    public class WmiQueryService
    {
        private const string WmiNamespace = @"root\microsoft\windows\storage";
        private const uint BusTypeVirtual = 15; // SCSI Virtual Disk (VHD/VHDX)
        private readonly AutoMountTracker _autoMountTracker;

        public WmiQueryService(AutoMountTracker autoMountTracker)
        {
            _autoMountTracker = autoMountTracker;
        }

        /// <summary>
        /// 查询所有虚拟磁盘
        /// </summary>
        public async Task<List<VirtualDiskInfo>> QueryVirtualDisksAsync()
        {
            return await Task.Run(() =>
            {
                var disks = new List<VirtualDiskInfo>();

                try
                {
                    using var searcher = new ManagementObjectSearcher(
                        WmiNamespace,
                        $"SELECT * FROM MSFT_Disk WHERE BusType = {BusTypeVirtual}");

                    foreach (ManagementObject disk in searcher.Get())
                    {
                        try
                        {
                            var filePath = disk["Location"]?.ToString() ?? string.Empty;

                            // 跳过非 VHDX/VHD 文件路径的磁盘
                            if (string.IsNullOrEmpty(filePath))
                                continue;
                            var format = DetectFormat(filePath);
                            if (format == "未知")
                                continue;

                            var info = new VirtualDiskInfo
                            {
                                FilePath = filePath,
                                FriendlyName = disk["FriendlyName"]?.ToString() ?? string.Empty,
                                IsMounted = true,
                                IsAutoMount = _autoMountTracker.GetAutoMount(filePath),
                                Format = DetectFormat(filePath),
                                TotalSize = Convert.ToInt64(disk["Size"] ?? 0),
                                MountPreference = _autoMountTracker.GetMountPreference(filePath),
                                TargetMountPath = _autoMountTracker.GetTargetMountPath(filePath)
                            };

                            // 获取实际挂载路径（盘符或文件夹挂载点）
                            var (driveLetter, folderPath) = GetMountPath(disk);
                            info.DriveLetter = !string.IsNullOrEmpty(driveLetter) ? driveLetter : folderPath;

                            // 获取可用空间
                            if (!string.IsNullOrEmpty(info.DriveLetter))
                            {
                                info.FreeSpace = GetFreeSpace(info.DriveLetter);
                            }

                            disks.Add(info);
                        }
                        catch
                        {
                            // 跳过解析失败的条目
                        }
                    }
                }
                catch (Exception)
                {
                    // WMI 查询失败
                    throw;
                }

                return disks;
            });
        }

        /// <summary>
        /// 检测文件格式
        /// </summary>
        private static string DetectFormat(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return "未知";

            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".vhdx" => "VHDX",
                ".vhd" => "VHD",
                _ => "未知"
            };
        }

        /// <summary>
        /// 获取虚拟磁盘的实际挂载路径（盘符优先，其次文件夹挂载点）
        /// 通过 MSFT_Partition.AccessPaths 获取所有访问路径，包括盘符和文件夹挂载点，
        /// 并辅以 GetVolumePathNamesForVolumeName 做兜底查询。
        /// </summary>
        /// <returns>(driveLetter: 盘符如 "E:"，folderPath: 文件夹挂载路径)</returns>
        internal static (string DriveLetter, string FolderPath) GetMountPath(ManagementObject disk)
        {
            string driveLetter = string.Empty;
            string folderPath = string.Empty;

            try
            {
                var partitions = disk.GetRelated("MSFT_Partition");
                foreach (ManagementObject partition in partitions)
                {
                    // 策略1: 从 MSFT_Partition.AccessPaths 获取所有访问路径
                    var accessPaths = partition["AccessPaths"] as string[];
                    if (accessPaths != null && accessPaths.Length > 0)
                    {
                        foreach (var ap in accessPaths)
                        {
                            if (string.IsNullOrEmpty(ap)) continue;
                            string trimmed = ap.TrimEnd('\\');

                            if (IsDriveLetterPath(ap))
                            {
                                if (string.IsNullOrEmpty(driveLetter))
                                    driveLetter = trimmed;
                            }
                            else if (trimmed.Length > 1)
                            {
                                if (string.IsNullOrEmpty(folderPath))
                                    folderPath = trimmed;
                            }
                        }

                        // 通过 AccessPaths 找到卷 GUID，再用 GetVolumePathNamesForVolumeName 补全
                        if (string.IsNullOrEmpty(driveLetter) || string.IsNullOrEmpty(folderPath))
                        {
                            var (vdl, vfp) = GetMountPathViaVolumeGuid(accessPaths);
                            if (string.IsNullOrEmpty(driveLetter) && !string.IsNullOrEmpty(vdl))
                                driveLetter = vdl;
                            if (string.IsNullOrEmpty(folderPath) && !string.IsNullOrEmpty(vfp))
                                folderPath = vfp;
                        }
                    }

                    // 策略2: 后备 - 通过 MSFT_Volume.DriveLetter
                    if (string.IsNullOrEmpty(driveLetter))
                    {
                        var volumes = partition.GetRelated("MSFT_Volume");
                        foreach (ManagementObject volume in volumes)
                        {
                            var dl = volume["DriveLetter"]?.ToString();
                            if (!string.IsNullOrEmpty(dl))
                            {
                                driveLetter = dl;
                                break;
                            }
                        }
                    }
                }
            }
            catch
            {
                // 关联查询失败
            }

            return (driveLetter, folderPath);
        }

        /// <summary>
        /// 通过卷 GUID 补全挂载路径（用于 AccessPaths 不包含全部路径时）
        /// </summary>
        private static (string DriveLetter, string FolderPath) GetMountPathViaVolumeGuid(string[] accessPaths)
        {
            string driveLetter = string.Empty;
            string folderPath = string.Empty;

            foreach (var ap in accessPaths)
            {
                if (string.IsNullOrEmpty(ap)) continue;

                // 构造合法的挂载点字符串（需以反斜杠结尾）
                string mountPoint = ap.TrimEnd('\\');
                if (IsDriveLetterPath(ap))
                {
                    mountPoint += "\\";
                }
                else
                {
                    mountPoint += "\\";
                }

                var volumeGuid = NativeMethods.GetVolumeGuidName(mountPoint);
                if (volumeGuid == null) continue;

                var paths = NativeMethods.GetVolumeMountPaths(volumeGuid);
                foreach (var p in paths)
                {
                    string pTrimmed = p.TrimEnd('\\');
                    if (IsDriveLetterPath(p))
                    {
                        if (string.IsNullOrEmpty(driveLetter))
                            driveLetter = pTrimmed;
                    }
                    else if (pTrimmed.Length > 1)
                    {
                        if (string.IsNullOrEmpty(folderPath))
                            folderPath = pTrimmed;
                    }
                }

                if (!string.IsNullOrEmpty(driveLetter) && !string.IsNullOrEmpty(folderPath))
                    break;
            }

            return (driveLetter, folderPath);
        }

        /// <summary>
        /// 判断路径是否为盘符格式（如 "E:\" 或 "E:"）
        /// </summary>
        internal static bool IsDriveLetterPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            var trimmed = path.TrimEnd('\\');
            return trimmed.Length == 2 &&
                   char.IsLetter(trimmed[0]) &&
                   trimmed[1] == ':';
        }

        /// <summary>
        /// 通过 WMI 查询指定 VHDX 文件的当前挂载盘符（用于挂载后保存盘符）
        /// 支持等待磁盘在系统中出现（最多等待 timeoutSeconds 秒）
        /// </summary>
        public static string QueryAssignedDriveLetter(string vhdxPath, int timeoutSeconds = 15)
        {
            var deadline = DateTime.Now.AddSeconds(timeoutSeconds);

            while (DateTime.Now < deadline)
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher(
                        WmiNamespace,
                        $"SELECT * FROM MSFT_Disk WHERE Location = '{vhdxPath.Replace("\\", "\\\\").Replace("'", "\\'")}'");

                    foreach (ManagementObject disk in searcher.Get())
                    {
                        var partitions = disk.GetRelated("MSFT_Partition");
                        foreach (ManagementObject partition in partitions)
                        {
                            var accessPaths = partition["AccessPaths"] as string[];
                            if (accessPaths != null)
                            {
                                foreach (var ap in accessPaths)
                                {
                                    if (!string.IsNullOrEmpty(ap) && IsDriveLetterPath(ap))
                                    {
                                        return ap.TrimEnd('\\').TrimEnd(':');
                                    }
                                }
                            }

                            // 后备：检查 MSFT_Volume.DriveLetter
                            var volumes = partition.GetRelated("MSFT_Volume");
                            foreach (ManagementObject volume in volumes)
                            {
                                var dl = volume["DriveLetter"]?.ToString();
                                if (!string.IsNullOrEmpty(dl))
                                    return dl;
                            }
                        }
                    }
                }
                catch { }

                Thread.Sleep(500);
            }

            return string.Empty;
        }

        /// <summary>
        /// 通过 WMI 为指定 VHDX 文件分配区添加盘符访问路径
        /// 使用 MSFT_Partition.AddAccessPath 方法
        /// </summary>
        /// <returns>(成功, 错误信息)</returns>
        public static (bool Success, string Message) AssignDriveLetter(string vhdxPath, string driveLetter, int timeoutSeconds = 15)
        {
            string accessPath = driveLetter.TrimEnd(':') + ":\\";
            var deadline = DateTime.Now.AddSeconds(timeoutSeconds);

            while (DateTime.Now < deadline)
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher(
                        WmiNamespace,
                        $"SELECT * FROM MSFT_Disk WHERE Location = '{vhdxPath.Replace("\\", "\\\\").Replace("'", "\\'")}'");

                    foreach (ManagementObject disk in searcher.Get())
                    {
                        var partitions = disk.GetRelated("MSFT_Partition");
                        foreach (ManagementObject partition in partitions)
                        {
                            // 检查是否已有该盘符
                            var existingPaths = partition["AccessPaths"] as string[];
                            if (existingPaths != null)
                            {
                                foreach (var ep in existingPaths)
                                {
                                    if (!string.IsNullOrEmpty(ep) &&
                                        ep.TrimEnd('\\').Equals(driveLetter.TrimEnd(':') + ":", StringComparison.OrdinalIgnoreCase))
                                    {
                                        return (true, $"盘符已分配: {driveLetter.TrimEnd(':')}:");
                                    }
                                }
                            }

                            // 调用 AddAccessPath 添加盘符访问路径
                            try
                            {
                                var inParams = partition.GetMethodParameters("AddAccessPath");
                                inParams["AccessPath"] = accessPath;
                                var outParams = partition.InvokeMethod("AddAccessPath", inParams, null);

                                // MSFT_Partition 方法返回码：0=成功, 1=Method参数已检查（需重试）
                                uint returnCode = Convert.ToUInt32(outParams["ReturnValue"] ?? uint.MaxValue);

                                if (returnCode == 0)
                                {
                                    return (true, $"盘符已分配: {driveLetter.TrimEnd(':')}:");
                                }
                                else if (returnCode == 1)
                                {
                                    // 方法参数已检查但操作未完成，跳出分区循环，外层 while 等待后重试
                                    break;
                                }
                                else
                                {
                                    return (false, $"添加盘符失败，返回码: {returnCode}");
                                }
                            }
                            catch (ManagementException mex)
                            {
                                return (false, $"添加盘符失败: {mex.Message}");
                            }
                        }
                    }
                }
                catch { }

                Thread.Sleep(500);
            }

            return (false, "等待磁盘超时，无法分配盘符");
        }

        /// <summary>
        /// 通过 WMI 移除指定 VHDX 文件分区的盘符访问路径
        /// 使用 MSFT_Partition.RemoveAccessPath 方法
        /// </summary>
        public static (bool Success, string Message) RemoveDriveLetter(string vhdxPath, string driveLetter, int timeoutSeconds = 15)
        {
            string accessPath = driveLetter.TrimEnd(':') + ":\\";
            var deadline = DateTime.Now.AddSeconds(timeoutSeconds);

            while (DateTime.Now < deadline)
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher(
                        WmiNamespace,
                        $"SELECT * FROM MSFT_Disk WHERE Location = '{vhdxPath.Replace("\\", "\\\\").Replace("'", "\\'")}'");

                    foreach (ManagementObject disk in searcher.Get())
                    {
                        var partitions = disk.GetRelated("MSFT_Partition");
                        foreach (ManagementObject partition in partitions)
                        {
                            // 检查是否存在该盘符
                            var existingPaths = partition["AccessPaths"] as string[];
                            bool hasPath = false;
                            if (existingPaths != null)
                            {
                                foreach (var ep in existingPaths)
                                {
                                    if (!string.IsNullOrEmpty(ep) &&
                                        ep.TrimEnd('\\').Equals(driveLetter.TrimEnd(':') + ":", StringComparison.OrdinalIgnoreCase))
                                    {
                                        hasPath = true;
                                        break;
                                    }
                                }
                            }

                            if (!hasPath)
                            {
                                return (true, "盘符已移除或不存在");
                            }

                            // 调用 RemoveAccessPath 移除盘符访问路径
                            try
                            {
                                var inParams = partition.GetMethodParameters("RemoveAccessPath");
                                inParams["AccessPath"] = accessPath;
                                var outParams = partition.InvokeMethod("RemoveAccessPath", inParams, null);

                                uint returnCode = Convert.ToUInt32(outParams["ReturnValue"] ?? uint.MaxValue);

                                if (returnCode == 0)
                                {
                                    return (true, $"盘符已移除: {driveLetter.TrimEnd(':')}:");
                                }
                                else if (returnCode == 1)
                                {
                                    break; // 需要重试
                                }
                                else
                                {
                                    return (false, $"移除盘符失败，返回码: {returnCode}");
                                }
                            }
                            catch (ManagementException mex)
                            {
                                return (false, $"移除盘符失败: {mex.Message}");
                            }
                        }
                    }
                }
                catch { }

                Thread.Sleep(500);
            }

            return (false, "等待磁盘超时，无法移除盘符");
        }

        /// <summary>
        /// 获取指定盘符或挂载路径的可用空间
        /// </summary>
        private static long GetFreeSpace(string driveLetterOrPath)
        {
            try
            {
                string path = driveLetterOrPath.TrimEnd('\\');

                // 盘符格式 "E:" 或 "E"
                if (path.Length >= 1 && path.Length <= 2 && char.IsLetter(path[0]))
                {
                    var driveInfo = new DriveInfo(path[0].ToString());
                    return driveInfo.AvailableFreeSpace;
                }

                // 文件夹路径
                if (Directory.Exists(path))
                {
                    var root = Path.GetPathRoot(path);
                    if (!string.IsNullOrEmpty(root))
                    {
                        var driveInfo = new DriveInfo(root);
                        return driveInfo.AvailableFreeSpace;
                    }
                }
            }
            catch { }

            return 0;
        }

        /// <summary>
        /// 检查 WMI 服务是否可用
        /// </summary>
        public static bool IsWmiAvailable()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    WmiNamespace,
                    $"SELECT * FROM MSFT_Disk WHERE BusType = {BusTypeVirtual}");

                searcher.Get();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
