using VHDX_Manager.Models;

namespace VHDX_Manager.Services
{
    /// <summary>
    /// 虚拟磁盘服务接口
    /// </summary>
    public interface IVirtualDiskService
    {
        /// <summary>
        /// 挂载虚拟磁盘
        /// </summary>
        /// <param name="filePath">VHDX/VHD 文件路径</param>
        /// <param name="autoMount">是否设置开机自动挂载</param>
        /// <param name="mountPreference">挂载路径偏好</param>
        /// <param name="targetMountPath">目标挂载文件夹路径（仅 FolderMountPoint 模式）</param>
        /// <param name="savedDriveLetter">上次挂载时分配的盘符（不带冒号，如 "E"），为空则不分配盘符</param>
        /// <returns>操作结果</returns>
        Task<(bool Success, string Message)> MountAsync(
            string filePath,
            bool autoMount = false,
            MountPreference mountPreference = MountPreference.AutoDriveLetter,
            string targetMountPath = "",
            string savedDriveLetter = "");

        /// <summary>
        /// 卸载虚拟磁盘
        /// </summary>
        /// <param name="filePath">VHDX/VHD 文件路径</param>
        /// <returns>操作结果</returns>
        Task<(bool Success, string Message)> UnmountAsync(string filePath);

        /// <summary>
        /// 设置自动挂载状态
        /// </summary>
        /// <param name="filePath">VHDX/VHD 文件路径</param>
        /// <param name="autoMount">是否自动挂载</param>
        /// <returns>操作结果</returns>
        Task<(bool Success, string Message)> SetAutoMountAsync(string filePath, bool autoMount);

        /// <summary>
        /// 获取虚拟磁盘大小信息
        /// </summary>
        /// <param name="filePath">VHDX/VHD 文件路径</param>
        /// <returns>磁盘大小信息</returns>
        Task<(long TotalSize, long FreeSpace)> GetDiskSizeAsync(string filePath);
    }
}
