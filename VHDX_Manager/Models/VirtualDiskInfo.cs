using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VHDX_Manager.Models
{
    /// <summary>
    /// 挂载路径偏好
    /// </summary>
    public enum MountPreference
    {
        /// <summary>自动盘符 — 系统自动分配</summary>
        AutoDriveLetter = 0,
        /// <summary>手动盘符 — 按配置文件指定的盘符挂载</summary>
        ManualDriveLetter = 1,
        /// <summary>手动路径 — 挂载到指定文件夹路径</summary>
        ManualFolderPath = 2,
        /// <summary>自动路径 — 系统自动管理文件夹挂载</summary>
        AutoFolderPath = 3
    }

    /// <summary>
    /// 虚拟磁盘信息模型
    /// </summary>
    public class VirtualDiskInfo : INotifyPropertyChanged
    {
        private string _filePath = string.Empty;
        private string _driveLetter = string.Empty;
        private bool _isMounted;
        private bool _isAutoMount;
        private string _format = "VHDX";
        private long _totalSize;
        private long _freeSpace;
        private string _status = "未知";
        private string _friendlyName = string.Empty;
        private MountPreference _mountPreference;
        private string _targetMountPath = string.Empty;

        /// <summary>
        /// VHDX/VHD 文件路径
        /// </summary>
        public string FilePath
        {
            get => _filePath;
            set => SetProperty(ref _filePath, value);
        }

        /// <summary>
        /// 盘符 (如 "D:") 或文件夹挂载路径（如 "C:\Mount\Disk"）
        /// </summary>
        public string DriveLetter
        {
            get => _driveLetter;
            set
            {
                if (SetProperty(ref _driveLetter, value))
                {
                    OnPropertyChanged(nameof(MountPointDisplay));
                }
            }
        }

        /// <summary>
        /// 是否已挂载
        /// </summary>
        public bool IsMounted
        {
            get => _isMounted;
            set
            {
                if (SetProperty(ref _isMounted, value))
                {
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(StatusColor));
                }
            }
        }

        /// <summary>
        /// 是否开机自动挂载
        /// </summary>
        public bool IsAutoMount
        {
            get => _isAutoMount;
            set => SetProperty(ref _isAutoMount, value);
        }

        /// <summary>
        /// 文件格式 (VHD / VHDX)
        /// </summary>
        public string Format
        {
            get => _format;
            set => SetProperty(ref _format, value);
        }

        /// <summary>
        /// 总容量 (字节)
        /// </summary>
        public long TotalSize
        {
            get => _totalSize;
            set
            {
                if (SetProperty(ref _totalSize, value))
                {
                    OnPropertyChanged(nameof(TotalSizeText));
                }
            }
        }

        /// <summary>
        /// 可用空间 (字节)
        /// </summary>
        public long FreeSpace
        {
            get => _freeSpace;
            set
            {
                if (SetProperty(ref _freeSpace, value))
                {
                    OnPropertyChanged(nameof(FreeSpaceText));
                }
            }
        }

        /// <summary>
        /// 状态文本
        /// </summary>
        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        /// <summary>
        /// 友好名称
        /// </summary>
        public string FriendlyName
        {
            get => _friendlyName;
            set => SetProperty(ref _friendlyName, value);
        }

        /// <summary>
        /// 挂载路径偏好
        /// </summary>
        public MountPreference MountPreference
        {
            get => _mountPreference;
            set => SetProperty(ref _mountPreference, value);
        }

        /// <summary>
        /// 目标挂载文件夹路径（仅 FolderMountPoint 模式使用）
        /// </summary>
        public string TargetMountPath
        {
            get => _targetMountPath;
            set => SetProperty(ref _targetMountPath, value);
        }

        /// <summary>
        /// 状态显示文本
        /// </summary>
        public string StatusText => IsMounted ? "已挂载" : "未挂载";

        /// <summary>
        /// 挂载点显示文本（DriveLetter 为空时显示 "N/A"）
        /// DriveLetter 属性可能包含盘符（如 "E:"）或文件夹挂载路径（如 "C:\Mount\Disk"）
        /// </summary>
        public string MountPointDisplay => string.IsNullOrEmpty(_driveLetter) ? "N/A" : _driveLetter;

        /// <summary>
        /// 状态颜色
        /// </summary>
        public string StatusColor => IsMounted ? "#4CAF50" : "#9E9E9E";

        /// <summary>
        /// 总容量显示文本
        /// </summary>
        public string TotalSizeText => FormatSize(TotalSize);

        /// <summary>
        /// 可用空间显示文本
        /// </summary>
        public string FreeSpaceText => FormatSize(FreeSpace);

        /// <summary>
        /// 格式化文件大小
        /// </summary>
        private static string FormatSize(long bytes)
        {
            if (bytes <= 0) return "0 B";

            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;

            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:0.##} {sizes[order]}";
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }
}