VHDX Manager - 虚拟磁盘管理工具 算法说明
=============================================

一、自动挂载的实现机制
----------------------

Windows 虚拟磁盘 API (virtdisk.dll) 中的关键标志位：

1. ATTACH_VIRTUAL_DISK_FLAG_PERMANENT_LIFETIME = 0x00000004
   含义: "Will decouple the disk lifetime from that of the VirtualDiskHandle.
          The disk will be attached until an explicit call is made to
          DetachVirtualDisk, even if all handles are closed."
   作用: 让磁盘在所有句柄关闭后仍然保持挂载状态（脱离句柄生命周期）。

2. ATTACH_VIRTUAL_DISK_FLAG_AT_BOOT = 0x00000400
   含义: "Reattach a virtual disk the next time the system boots."
   作用: 注册该虚拟磁盘为开机自动挂载（系统启动时自动重新挂载）。
   状态: 在 Windows 10 19045 上，此标志通过用户态 API 始终返回
          ERROR_INVALID_PARAMETER (0x57)，不支持使用。

重要发现（算法测试验证）:
  不使用 PERMANENT_LIFETIME 标志时（ATTACH_VIRTUAL_DISK_FLAG_NONE），
  AttachVirtualDisk 成功后关闭句柄，磁盘会立即自动分离（Detach 返回
  ERROR_NOT_READY 0x15）。因此，所有挂载操作都必须使用
  PERMANENT_LIFETIME 标志，否则句柄关闭后磁盘就不可用了。

  "自动挂载"与"手动挂载"的区别仅在于本地配置记录（automount.json），
  不影响 API 调用参数。两种挂载方式都使用相同的 PERMANENT_LIFETIME 标志。

参考: Windows SDK virtdisk.h (10.0.26100.0)
  https://learn.microsoft.com/en-us/windows/win32/api/virtdisk/ne-virtdisk-attach_virtual_disk_flag


二、自动挂载状态查询的限制
--------------------------

经过详细验证，系统 API 无法可靠查询某个 VHDX 是否设置了开机自动挂载：

1. MSFT_VirtualDisk 类 (root\microsoft\windows\storage)
   - 该类代表存储池(Storage Spaces)虚拟磁盘，不代表通过 virtdisk API 挂载的 VHD/VHDX。
   - 在本系统上查询返回空结果。
   - 其 IsManualAttach 属性无法使用。

2. MSFT_Disk 类
   - 可以通过 BusType=15 (SCSI Virtual Disk) 过滤出虚拟磁盘。
   - Location 属性包含 VHDX 文件路径。
   - 但没有 IsManualAttach 或等效的自动挂载状态属性。

3. GetVirtualDiskInformation API
   - 支持查询的类型包括: SIZE, IDENTIFIER, PARENT_LOCATION, IS_LOADED 等。
   - 不包含 attach flags 或自动挂载状态的查询选项。

4. 其他途径
   - 注册表中无明确的 VHD 自动挂载记录。
   - Get-VHD cmdlet 也不提供 IsManualAttach 属性。

结论: Windows 系统没有公开的 API 来查询 VHDX 是否设置了开机自动挂载。
方案: 本软件使用本地配置文件 (automount.json) 追踪自动挂载状态。


三、虚拟磁盘发现算法
--------------------

本软件通过 WMI MSFT_Disk 类发现已挂载的虚拟磁盘：

  WMI 命名空间: root\microsoft\windows\storage
  查询语句:     SELECT * FROM MSFT_Disk WHERE BusType = 15

  BusType = 15 表示 SCSI Virtual Disk (File Backed Virtual)

关键属性:
  Location    - VHDX/VHD 文件完整路径 (如 D:\Workshop\Studio\MyEmail.vhdx)
  Size        - 磁盘总容量 (字节)
  FriendlyName - 显示名称 (通常为 "Msft Virtual Disk")
  IsOffline   - 是否离线
  IsReadOnly  - 是否只读
  Number      - 磁盘编号

盘符获取 (WMI 关联链):
  MSFT_Disk -> GetRelated("MSFT_Partition") -> GetRelated("MSFT_Volume") -> DriveLetter

错误的查询方式 (不适用于本场景):
  SELECT * FROM MSFT_VirtualDisk  -- 此类仅含存储池虚拟磁盘，不含 VHD/VHDX


四、挂载/卸载操作流程
----------------------

挂载:
  1. 验证文件存在且扩展名为 .vhd/.vhdx
  2. 使用 DeviceId=0（自动检测）构建 VIRTUAL_STORAGE_TYPE
  3. OpenVirtualDisk 获取句柄 (VIRTUAL_DISK_ACCESS_ATTACH_RW)
  4. AttachVirtualDisk 执行挂载，flags = PERMANENT_LIFETIME
     （所有挂载都使用此标志，否则句柄关闭后磁盘会自动分离）
  5. 关闭句柄，更新本地 automount.json

卸载:
  1. 使用 DeviceId=0（自动检测）构建 VIRTUAL_STORAGE_TYPE
  2. OpenVirtualDisk 获取句柄 (VIRTUAL_DISK_ACCESS_DETACH)
  3. DetachVirtualDisk 执行卸载
  4. 关闭句柄，从 automount.json 移除记录

注意: DeviceId 必须使用 0（自动检测）。使用显式 DeviceId（如 2=VHDX）
      可能导致 OpenVirtualDisk 返回 0x570 (ERROR_FILE_CORRUPT)。

设置自动挂载 (切换):
  所有挂载都使用相同的 PERMANENT_LIFETIME 标志，切换只需更新本地配置
  记录（automount.json），无需卸载再重新挂载。


五、开机自动挂载（Windows 服务）
---------------------------------

由于 ATTACH_VIRTUAL_DISK_FLAG_AT_BOOT 在用户态 API 上不可用（始终返回
ERROR_INVALID_PARAMETER 0x57），本软件采用 Windows 服务实现开机自动挂载，
参考 VhdManager (sordum.org) 的实现方式。

方案: 同一程序通过命令行参数切换 GUI/服务模式
  - 无参数: 启动 WPF 图形界面
  - --service: 以 Windows 服务方式运行，自动挂载 automount.json 中标记的磁盘
  - --install: 安装 Windows 服务（需要管理员权限）
  - --uninstall: 卸载 Windows 服务

服务实现:
  服务名称: VHDXManager
  显示名称: VHDX Manager 自动挂载服务
  启动类型: AUTO_START（开机自动启动）
  运行身份: LocalSystem
  可执行:  VHDX_Manager.exe --service

  服务启动流程:
  1. 等待 3 秒让系统服务就绪
  2. 读取 automount.json 中所有标记为 true 的 VHDX 路径
  3. 对每个路径调用 MountVhdx（使用 PERMANENT_LIFETIME 标志）
  4. 每 60 秒检查一次挂载状态，自动重新挂载丢失的磁盘

安装方法:
  方法 1（命令行）:
    sc create VHDXManager binPath= "VHDX_Manager.exe --service" start= auto
    sc description VHDXManager "自动挂载标记为开机自动挂载的 VHDX 虚拟磁盘"

  方法 2（GUI 界面）:
    在主界面点击"安装服务"按钮

算法测试验证:
  测试环境: test.vhdx (100MB 动态磁盘)
  测试项目:
    1. Mount(NONE) 后句柄关闭 -> 磁盘自动分离，Detach 返回 0x15
    2. Mount(PERMANENT_LIFETIME) 后句柄关闭 -> 磁盘保持挂载，Detach 正常
    3. Mount(NONE) -> 卸载 -> Mount(PERMANENT) -> 正常（因为 NONE 已分离）
    4. Mount(PERMANENT) -> 卸载 -> Mount(PERMANENT) -> 正常
  结论: PERMANENT_LIFETIME 是保持磁盘挂载的必要条件


五、本地配置文件 (automount.json)
----------------------------------

格式: JSON 字典，键为 VHDX 文件路径（大写规范化），值为布尔型。

  {
    "D:\\WORKSHOP\\STUDIO\\MYEMAIL.VHDX": true,
    "D:\\SOFTWARE\\PROGRAMS.VHDX": false
  }

文件位置: 与 VHDX_Manager.exe 同目录
用途:     追踪通过本软件挂载的 VHDX 的自动挂载状态
限制:     仅记录通过本软件操作过的磁盘，外部挂载的磁盘无记录


六、项目结构
------------

  VHDX_Manager/
  ├── App.xaml / App.xaml.cs          - 应用入口，管理员权限检查
  ├── Program.cs                      - 入口点，--service/--install/--uninstall 参数
  ├── Models/
  │   ├── VirtualDiskInfo.cs          - 虚拟磁盘数据模型
  │   └── EnvironmentCheckResult.cs   - 环境检查结果模型
  ├── Services/
  │   ├── IVirtualDiskService.cs      - 磁盘操作接口
  │   ├── VirtualDiskService.cs       - virtdisk.dll P/Invoke 实现
  │   ├── WmiQueryService.cs          - WMI 虚拟磁盘发现服务
  │   ├── AutoMountTracker.cs         - 自动挂载状态本地追踪
  │   └── VhdService.cs              - Windows 服务，开机自动挂载
  ├── ViewModels/
  │   ├── MainViewModel.cs            - 主窗口 ViewModel
  │   └── RelayCommand.cs             - ICommand 实现
  ├── Views/
  │   └── MainWindow.xaml             - 主窗口界面
  ├── Converters/
  │   ├── BoolToStatusConverter.cs    - 布尔 → 状态文本
  │   ├── BoolToColorConverter.cs     - 布尔 → 颜色
  │   └── ...                         - 其他转换器
  ├── Helpers/
  │   ├── EnvironmentChecker.cs       - 启动环境检查
  │   └── NativeMethods.cs            - P/Invoke 声明与结构体
  └── Resources/
      └── Styles.xaml                 - 全局样式
