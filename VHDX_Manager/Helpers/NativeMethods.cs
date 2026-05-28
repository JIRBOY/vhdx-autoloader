using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace VHDX_Manager.Helpers
{
    #region 枚举定义

    /// <summary>
    /// 虚拟磁盘访问掩码
    /// </summary>
    [Flags]
    public enum VIRTUAL_DISK_ACCESS_MASK : uint
    {
        VIRTUAL_DISK_ACCESS_NONE = 0x00000000,
        VIRTUAL_DISK_ACCESS_ATTACH_RO = 0x00010000,
        VIRTUAL_DISK_ACCESS_ATTACH_RW = 0x00020000,
        VIRTUAL_DISK_ACCESS_DETACH = 0x00040000,
        VIRTUAL_DISK_ACCESS_GET_INFO = 0x00080000,
        VIRTUAL_DISK_ACCESS_CREATE = 0x00100000,
        VIRTUAL_DISK_ACCESS_METAOPS = 0x00200000,
        VIRTUAL_DISK_ACCESS_READ = 0x000d0000,
        VIRTUAL_DISK_ACCESS_ALL = 0x003f0000,
        VIRTUAL_DISK_ACCESS_WRITABLE = 0x00320000
    }

    /// <summary>
    /// 打开虚拟磁盘标志
    /// </summary>
    [Flags]
    public enum OPEN_VIRTUAL_DISK_FLAG : uint
    {
        OPEN_VIRTUAL_DISK_FLAG_NONE = 0x00000000,
        OPEN_VIRTUAL_DISK_FLAG_NO_PARENTS = 0x00000001,
        OPEN_VIRTUAL_DISK_FLAG_BLANK_FILE = 0x00000002,
        OPEN_VIRTUAL_DISK_FLAG_BOOT_DRIVE = 0x00000004,
        OPEN_VIRTUAL_DISK_FLAG_CACHED_IO = 0x00000008,
        OPEN_VIRTUAL_DISK_FLAG_CUSTOM_DIFF_CHAIN = 0x00000010,
        OPEN_VIRTUAL_DISK_FLAG_PARENT_CACHED_IO = 0x00000020,
        OPEN_VIRTUAL_DISK_FLAG_VHDSET_FILE_ONLY = 0x00000040,
        OPEN_VIRTUAL_DISK_FLAG_IGNORE_RELATIVE_PARENT_LOCATOR = 0x00000080,
        OPEN_VIRTUAL_DISK_FLAG_NO_WRITE_HARDENING = 0x00000100,
        OPEN_VIRTUAL_DISK_FLAG_SUPPORT_COMPRESSED_VOLUMES = 0x00000200,
        OPEN_VIRTUAL_DISK_FLAG_SUPPORT_SPARSE_FILES_ANY_FS = 0x00000400,
        OPEN_VIRTUAL_DISK_FLAG_SUPPORT_ENCRYPTED_FILES = 0x00000800
    }

    /// <summary>
    /// 挂载虚拟磁盘标志
    /// </summary>
    [Flags]
    public enum ATTACH_VIRTUAL_DISK_FLAG : uint
    {
        ATTACH_VIRTUAL_DISK_FLAG_NONE = 0x00000000,
        ATTACH_VIRTUAL_DISK_FLAG_READ_ONLY = 0x00000001,
        ATTACH_VIRTUAL_DISK_FLAG_NO_DRIVE_LETTER = 0x00000002,
        ATTACH_VIRTUAL_DISK_FLAG_PERMANENT_LIFETIME = 0x00000004,
        ATTACH_VIRTUAL_DISK_FLAG_NO_LOCAL_HOST = 0x00000008,
        ATTACH_VIRTUAL_DISK_FLAG_NO_SECURITY_DESCRIPTOR = 0x00000010,
        ATTACH_VIRTUAL_DISK_FLAG_BYPASS_DEFAULT_ENCRYPTION_POLICY = 0x00000020,
        ATTACH_VIRTUAL_DISK_FLAG_NON_PNP = 0x00000040,
        ATTACH_VIRTUAL_DISK_FLAG_RESTRICTED_RANGE = 0x00000080,
        ATTACH_VIRTUAL_DISK_FLAG_SINGLE_PARTITION = 0x00000100,
        ATTACH_VIRTUAL_DISK_FLAG_REGISTER_VOLUME = 0x00000200,
        ATTACH_VIRTUAL_DISK_FLAG_AT_BOOT = 0x00000400
    }

    /// <summary>
    /// 卸载虚拟磁盘标志
    /// </summary>
    [Flags]
    public enum DETACH_VIRTUAL_DISK_FLAG : uint
    {
        DETACH_VIRTUAL_DISK_FLAG_NONE = 0x00000000
    }

    /// <summary>
    /// 打开虚拟磁盘版本
    /// </summary>
    public enum OPEN_VIRTUAL_DISK_VERSION : uint
    {
        OPEN_VIRTUAL_DISK_VERSION_UNSPECIFIED = 0,
        OPEN_VIRTUAL_DISK_VERSION_1 = 1,
        OPEN_VIRTUAL_DISK_VERSION_2 = 2,
        OPEN_VIRTUAL_DISK_VERSION_3 = 3
    }

    /// <summary>
    /// 挂载虚拟磁盘版本
    /// </summary>
    public enum ATTACH_VIRTUAL_DISK_VERSION : uint
    {
        ATTACH_VIRTUAL_DISK_VERSION_UNSPECIFIED = 0,
        ATTACH_VIRTUAL_DISK_VERSION_1 = 1,
        ATTACH_VIRTUAL_DISK_VERSION_2 = 2
    }

    /// <summary>
    /// 获取虚拟磁盘信息版本
    /// </summary>
    public enum GET_VIRTUAL_DISK_INFO_VERSION : uint
    {
        GET_VIRTUAL_DISK_INFO_UNSPECIFIED = 0,
        GET_VIRTUAL_DISK_INFO_SIZE = 1,
        GET_VIRTUAL_DISK_INFO_IDENTIFIER = 2,
        GET_VIRTUAL_DISK_INFO_PARENT_LOCATION = 3,
        GET_VIRTUAL_DISK_INFO_PHYSICAL_SECTOR_SIZE = 4,
        GET_VIRTUAL_DISK_INFO_VIRTUAL_SECTOR_SIZE = 5,
        GET_VIRTUAL_DISK_INFO_SAFE_FILE_SIZE = 6,
        GET_VIRTUAL_DISK_INFO_PHYSICAL_DISK = 7,
        GET_VIRTUAL_DISK_INFO_VHD_PHYSICAL_SECTOR_SIZE = 8,
        GET_VIRTUAL_DISK_INFO_SMALLEST_SAFE_VIRTUAL_SIZE = 9,
        GET_VIRTUAL_DISK_INFO_SMALLEST_SAFE_FILE_SIZE = 10,
        GET_VIRTUAL_DISK_INFO_LINKED_ID = 11
    }

    #endregion

    #region 结构体定义

    /// <summary>
    /// 虚拟存储类型
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct VIRTUAL_STORAGE_TYPE
    {
        /// <summary>
        /// 设备ID (1=VHD, 2=VHDX)
        /// </summary>
        public uint DeviceId;

        /// <summary>
        /// 供应商ID (Microsoft)
        /// </summary>
        public Guid VendorId;
    }

    /// <summary>
    /// 打开虚拟磁盘参数 V1
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct OPEN_VIRTUAL_DISK_PARAMETERS_V1
    {
        public uint RWDepth;
    }

    /// <summary>
    /// 打开虚拟磁盘参数 V2
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct OPEN_VIRTUAL_DISK_PARAMETERS_V2
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool GetInfoOnly;

        [MarshalAs(UnmanagedType.Bool)]
        public bool ReadOnly;

        public Guid ResiliencyGuid;
    }

    /// <summary>
    /// 打开虚拟磁盘参数
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct OPEN_VIRTUAL_DISK_PARAMETERS
    {
        [FieldOffset(0)]
        public OPEN_VIRTUAL_DISK_VERSION Version;

        [FieldOffset(4)]
        public OPEN_VIRTUAL_DISK_PARAMETERS_V1 Version1;

        [FieldOffset(4)]
        public OPEN_VIRTUAL_DISK_PARAMETERS_V2 Version2;
    }

    /// <summary>
    /// 挂载虚拟磁盘参数 V1
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ATTACH_VIRTUAL_DISK_PARAMETERS_V1
    {
        public uint Reserved;
    }

    /// <summary>
    /// 挂载虚拟磁盘参数 V2
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ATTACH_VIRTUAL_DISK_PARAMETERS_V2
    {
        public ulong RestrictedOffset;
        public ulong RestrictedLength;
    }

    /// <summary>
    /// 挂载虚拟磁盘参数
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct ATTACH_VIRTUAL_DISK_PARAMETERS
    {
        [FieldOffset(0)]
        public ATTACH_VIRTUAL_DISK_VERSION Version;

        [FieldOffset(4)]
        public ATTACH_VIRTUAL_DISK_PARAMETERS_V1 Version1;

        [FieldOffset(4)]
        public ATTACH_VIRTUAL_DISK_PARAMETERS_V2 Version2;
    }

    /// <summary>
    /// 虚拟磁盘大小信息
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct VIRTUAL_DISK_SIZE
    {
        public ulong VirtualSize;
        public ulong PhysicalSize;
        public uint BlockSize;
        public uint SectorSize;
    }

    /// <summary>
    /// 获取虚拟磁盘信息
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct GET_VIRTUAL_DISK_INFO
    {
        [FieldOffset(0)]
        public GET_VIRTUAL_DISK_INFO_VERSION Version;

        [FieldOffset(8)]
        public VIRTUAL_DISK_SIZE Size;
    }

    #endregion

    #region P/Invoke 声明

    /// <summary>
    /// Windows 虚拟磁盘 API 原生方法
    /// </summary>
    public static class NativeMethods
    {
        // Microsoft 供应商 ID
        public static readonly Guid VIRTUAL_STORAGE_TYPE_VENDOR_MICROSOFT =
            new Guid("EC984AEC-A0F9-47E9-901F-71415A66345B");

        // VHD 设备 ID
        public const uint VIRTUAL_STORAGE_TYPE_DEVICE_VHD = 1;

        // VHDX 设备 ID
        public const uint VIRTUAL_STORAGE_TYPE_DEVICE_VHDX = 2;

        /// <summary>
        /// 打开虚拟磁盘
        /// </summary>
        [DllImport("virtdisk.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern uint OpenVirtualDisk(
            ref VIRTUAL_STORAGE_TYPE VirtualStorageType,
            string Path,
            VIRTUAL_DISK_ACCESS_MASK VirtualDiskAccessMask,
            OPEN_VIRTUAL_DISK_FLAG Flags,
            ref OPEN_VIRTUAL_DISK_PARAMETERS Parameters,
            out SafeFileHandle Handle);

        /// <summary>
        /// 挂载虚拟磁盘
        /// </summary>
        [DllImport("virtdisk.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern uint AttachVirtualDisk(
            SafeFileHandle VirtualDiskHandle,
            IntPtr SecurityDescriptor,
            ATTACH_VIRTUAL_DISK_FLAG Flags,
            uint ProviderSpecificFlags,
            ref ATTACH_VIRTUAL_DISK_PARAMETERS Parameters,
            IntPtr Overlapped);

        /// <summary>
        /// 卸载虚拟磁盘
        /// </summary>
        [DllImport("virtdisk.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern uint DetachVirtualDisk(
            SafeFileHandle VirtualDiskHandle,
            DETACH_VIRTUAL_DISK_FLAG Flags,
            uint ProviderSpecificFlags);

        /// <summary>
        /// 获取虚拟磁盘信息
        /// </summary>
        [DllImport("virtdisk.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern uint GetVirtualDiskInformation(
            SafeFileHandle VirtualDiskHandle,
            ref uint VirtualDiskInfoSize,
            ref GET_VIRTUAL_DISK_INFO VirtualDiskInfo,
            out uint SizeUsed);

        /// <summary>
        /// 获取虚拟磁盘物理路径
        /// </summary>
        [DllImport("virtdisk.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern uint GetVirtualDiskPhysicalPath(
            SafeFileHandle VirtualDiskHandle,
            ref uint DiskPathSizeInBytes,
            IntPtr DiskPath);

        /// <summary>
        /// 获取指定卷的所有挂载点（盘符和文件夹挂载路径）
        /// </summary>
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetVolumePathNamesForVolumeName(
            string lpszVolumeName,
            IntPtr lpszVolumePathNames,
            uint cchBufferLength,
            out uint lpcchReturnLength);

        /// <summary>
        /// 根据挂载点获取卷设备名称（卷 GUID 路径）
        /// </summary>
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetVolumeNameForVolumeMountPoint(
            string lpszVolumeMountPoint,
            IntPtr lpszVolumeName,
            uint cchBufferLength);

        /// <summary>
        /// 加载库
        /// </summary>
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr LoadLibrary(string lpFileName);

        /// <summary>
        /// 释放库
        /// </summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FreeLibrary(IntPtr hModule);

        /// <summary>
        /// 设置卷挂载点（将卷挂载到文件夹路径）
        /// </summary>
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetVolumeMountPoint(
            string lpszVolumeMountPoint,
            string lpszVolumeName);

        /// <summary>
        /// 删除卷挂载点（移除文件夹挂载路径）
        /// </summary>
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteVolumeMountPoint(
            string lpszVolumeMountPoint);

        /// <summary>
        /// 获取虚拟磁盘信息大小
        /// </summary>
        public static uint GetVirtualDiskInfoSize(SafeFileHandle handle, out VIRTUAL_DISK_SIZE sizeInfo)
        {
            uint infoSize = (uint)Marshal.SizeOf<GET_VIRTUAL_DISK_INFO>();
            var info = new GET_VIRTUAL_DISK_INFO
            {
                Version = GET_VIRTUAL_DISK_INFO_VERSION.GET_VIRTUAL_DISK_INFO_SIZE
            };

            uint result = GetVirtualDiskInformation(handle, ref infoSize, ref info, out _);
            sizeInfo = info.Size;
            return result;
        }

        /// <summary>
        /// 通过卷 GUID 路径获取该卷的所有挂载点（盘符和文件夹挂载路径）
        /// volumeName 格式应为 \\?\Volume{guid}\
        /// </summary>
        public static List<string> GetVolumeMountPaths(string volumeName)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(volumeName))
                return result;

            // 先查询所需缓冲区大小
            GetVolumePathNamesForVolumeName(volumeName, IntPtr.Zero, 0, out uint requiredLength);
            if (requiredLength == 0)
                return result;

            IntPtr buffer = Marshal.AllocHGlobal((int)(requiredLength + 1) * 2);
            try
            {
                if (GetVolumePathNamesForVolumeName(volumeName, buffer, requiredLength + 1, out uint returnLength))
                {
                    // 返回的内容为多个以 null 结尾的字符串，最后以一个额外的 null 结尾
                    int offset = 0;
                    while (offset < returnLength)
                    {
                        string path = Marshal.PtrToStringUni(buffer + offset * 2) ?? string.Empty;
                        if (string.IsNullOrEmpty(path))
                            break;
                        result.Add(path);
                        offset += path.Length + 1;
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }

            return result;
        }

        /// <summary>
        /// 通过盘符或挂载路径获取卷的 GUID 路径（格式 \\?\Volume{guid}\）
        /// </summary>
        public static string? GetVolumeGuidName(string mountPoint)
        {
            if (string.IsNullOrEmpty(mountPoint))
                return null;

            IntPtr buffer = Marshal.AllocHGlobal(MAX_PATH * 2);
            try
            {
                if (GetVolumeNameForVolumeMountPoint(mountPoint, buffer, MAX_PATH))
                {
                    return Marshal.PtrToStringUni(buffer);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }

            return null;
        }

        private const int MAX_PATH = 260;
    }

    #endregion
}