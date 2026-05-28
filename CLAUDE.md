# VHDX Manager - 虚拟磁盘管理工具

## 1. 项目概述

### 1.1 目标
基于 C#(.NET 8.0) 调用 Windows 虚拟磁盘 API，实现 VHDX/VHD 虚拟磁盘文件的加载状态查询、挂载/卸载与开机自动挂载管理。

### 1.2 技术栈
- **框架**: .NET 8.0, WPF (MVVM)
- **核心依赖**: virtdisk.dll (P/Invoke), System.Management (WMI)
- **UI库**: WPF 原生 + 自定义样式
- **权限**: 管理员权限 (UAC 提升)

### 1.3 核心原则
- 简洁、美观、实用的现代界面设计
- 稳定可靠的磁盘操作
- 清晰的状态反馈

---

## 2. 启动环境检查

### 2.1 检查时机
程序启动时（MainWindow 初始化前）执行环境检查，检查通过后再加载主界面。

### 2.2 检查项目与处理策略

| 检查项 | 检测方法 | 缺失时的处理 |
|--------|----------|--------------|
| **virtdisk.dll** | `LoadLibrary("virtdisk.dll")` | 降级到 WMI-only 模式，禁用挂载/卸载功能，仅保留查询 |
| **WMI 服务** | 尝试连接 `root\microsoft\windows\storage` | 降级到 virtdisk-only 模式，隐藏自动挂载状态列 |
| **管理员权限** | `new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)` | 弹窗提示，提供"以管理员重新启动"按钮 |
| **Windows 版本** | `Environment.OSVersion.Version >= 10.0` | 提示需要 Windows 10 或更高版本 |
| **.NET 运行时** | 检查 `RuntimeInformation.FrameworkDescription` | 显示当前版本，提示需要 .NET 8.0 |

### 2.3 降级模式说明

**WMI-only 模式（virtdisk.dll 缺失）:**
- 可用功能：查询已挂载磁盘状态、显示列表
- 禁用功能：挂载、卸载、设置自动挂载
- 界面变化：操作按钮置灰，显示提示"缺少 virtdisk.dll，部分功能不可用"

**virtdisk-only 模式（WMI 不可用）:**
- 可用功能：挂载、卸载（需手动输入路径）
- 禁用功能：自动扫描、自动挂载状态查询
- 界面变化：隐藏状态列，显示提示"WMI 服务不可用，需手动指定磁盘路径"

### 2.4 环境检查结果模型
```csharp
public class EnvironmentCheckResult
{
    public bool IsVirtdiskAvailable { get; set; }
    public bool IsWmiAvailable { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsWindowsVersionSupported { get; set; }
    public string WindowsVersion { get; set; }
    public string DotNetVersion { get; set; }

    public bool CanMount => IsVirtdiskAvailable && IsAdmin;
    public bool CanQuery => IsWmiAvailable;
    public bool IsFullyFunctional => CanMount && CanQuery;
}
```

### 2.5 检查流程
```
1. 检查 Windows 版本
   └─ 不满足 → 弹窗提示，退出

2. 检查管理员权限
   └─ 无权限 → 弹窗提示，提供"以管理员重新启动"按钮
      └─ 用户选择退出 → 关闭程序
      └─ 用户选择重启 → 执行 `Process.Start("runas", ...)` 后退出

3. 检查 virtdisk.dll
   └─ 缺失 → 记录警告，启用 WMI-only 模式

4. 检查 WMI 服务
   └─ 不可用 → 记录警告，启用 virtdisk-only 模式

5. 全部检查完成 → 显示环境状态摘要（如有降级则在主界面顶部显示信息栏）
```

### 2.6 环境状态显示
主界面顶部信息栏（仅在降级模式下显示）：
```
┌──────────────────────────────────────────────────────────┐
│ ⚠️ 运行在降级模式：缺少 virtdisk.dll，挂载功能不可用    │
│    [下载 virtdisk.dll]  [了解更多]                        │
└──────────────────────────────────────────────────────────┘
```

---

## 3. 功能需求

### 3.1 启动与自动扫描
- 程序启动时自动扫描系统中已挂载的虚拟磁盘
- 查询每个虚拟磁盘的挂载状态和自动挂载配置
- 以列表形式展示所有虚拟磁盘信息

### 3.2 虚拟磁盘列表显示
每条记录需显示以下信息：

| 字段 | 说明 |
|------|------|
| 文件路径 | VHDX/VHD 文件的完整路径 |
| 挂载状态 | 已挂载 / 未挂载 |
| 盘符/挂载点 | 映射的驱动器号或目录路径 |
| 自动挂载 | 是否设置开机自动挂载 (开关) |
| 磁盘容量 | 总容量与可用空间 |
| 文件格式 | VHD / VHDX |

### 3.3 挂载操作
- 支持通过文件选择对话框选择 VHDX/VHD 文件
- 挂载时可勾选"开机自动挂载"选项
- 挂载成功后自动刷新列表

### 3.4 卸载操作
- 支持卸载已挂载的虚拟磁盘
- 卸载前提示用户确认
- 卸载成功后自动刷新列表

### 3.5 自动挂载管理
- 已挂载的磁盘可通过开关切换自动挂载状态
- 切换流程：直接更新本地配置记录（所有挂载均使用 PERMANENT_LIFETIME 标志，无需卸载再重新挂载）

### 3.6 动态刷新
- 每 120 秒自动刷新列表状态
- 提供手动刷新按钮
- 刷新时保留用户选中状态

### 3.7 系统服务管理
- 支持安装/卸载 Windows 服务（VHDXManager）
- 服务安装后默认为手动启动类型
- 当存在自动挂载的虚拟磁盘时，自动将服务设为自动启动
- 当所有自动挂载取消后，自动将服务设为手动启动
- 可配置定时检查间隔（默认 10 分钟），服务自动检查并恢复未挂载的磁盘
- 主界面显示服务安装状态和运行状态
- 安装/卸载按钮根据服务状态动态切换显示

### 3.8 挂载模式
点击挂载模式文字弹出菜单，支持 4 种挂载模式：

| 模式 | 说明 | flags |
|------|------|-------|
| **自动盘符** | 系统自动分配盘符，不设 NO_DRIVE_LETTER | PERMANENT_LIFETIME |
| **手动盘符** | 用户指定盘符，程序通过 WMI AddAccessPath 分配 | PERMANENT_LIFETIME + NO_DRIVE_LETTER |
| **手动路径** | 用户选择文件夹路径，程序创建文件夹挂载点 | PERMANENT_LIFETIME + NO_DRIVE_LETTER |
| **自动路径** | 系统自动管理路径（实验性） | PERMANENT_LIFETIME |

- 手动盘符选择时弹出输入框，输入盘符如 "E"
- 手动路径选择时弹出文件夹选择对话框，自动创建目标文件夹
- 配置保存在 automount.json 的 MountConfig 中（SavedDriveLetter / TargetMountPath）
- 服务启动时按配置的挂载模式自动挂载

**⚠️ 盘符切换逻辑（已挂载磁盘）:**
切换挂载方式时，磁盘保持挂载状态，直接通过 WMI 修改挂载点，不卸载再重新挂载：
- **手动盘符**: 查询当前盘符 → 一致则跳过 → 不一致则 `RemoveAccessPath` 移除旧盘符后 `AddAccessPath` 分配新盘符
- **自动盘符**: 移除当前手动盘符，让系统自动分配
- **手动路径**: 直接调用 `SetVolumeMountPoint` 添加文件夹挂载点
- **自动路径**: 清除保存的盘符配置

---

## 4. 技术架构

### 4.1 整体架构
```
┌─────────────────────────────────────────────┐
│                   View Layer                 │
│         (MainWindow.xaml + Styles)           │
├─────────────────────────────────────────────┤
│               ViewModel Layer                │
│      (MainViewModel, DiskItemViewModel)      │
├─────────────────────────────────────────────┤
│                Service Layer                 │
│   (VirtualDiskService, WmiService, Timer)    │
├─────────────────────────────────────────────┤
│              Platform Layer (P/Invoke)        │
│          (virtdisk.dll, kernel32.dll)         │
└─────────────────────────────────────────────┘
```

### 4.2 核心 API 说明

#### 4.2.1 virtdisk.dll (底层 P/Invoke)

**函数签名:**
```csharp
// 打开虚拟磁盘
[DllImport("virtdisk.dll", CharSet = CharSet.Unicode)]
static extern uint OpenVirtualDisk(
    ref VIRTUAL_STORAGE_TYPE VirtualStorageType,
    string Path,
    VIRTUAL_DISK_ACCESS_MASK VirtualDiskAccessMask,
    OPEN_VIRTUAL_DISK_FLAG Flags,
    ref OPEN_VIRTUAL_DISK_PARAMETERS Parameters,
    out SafeFileHandle Handle);

// 挂载虚拟磁盘
[DllImport("virtdisk.dll", CharSet = CharSet.Unicode)]
static extern uint AttachVirtualDisk(
    SafeFileHandle VirtualDiskHandle,
    IntPtr SecurityDescriptor,
    ATTACH_VIRTUAL_DISK_FLAG Flags,
    uint ProviderSpecificFlags,
    ref ATTACH_VIRTUAL_DISK_PARAMETERS Parameters,
    IntPtr Overlapped);

// 卸载虚拟磁盘
[DllImport("virtdisk.dll", CharSet = CharSet.Unicode)]
static extern uint DetachVirtualDisk(
    SafeFileHandle VirtualDiskHandle,
    DETACH_VIRTUAL_DISK_FLAG Flags,
    uint ProviderSpecificFlags);
```

**关键数据结构:**
```csharp
struct VIRTUAL_STORAGE_TYPE
{
    public uint DeviceId;   // 2 = VHDX, 1 = VHD
    public Guid VendorId;   // Microsoft: EC984AEC-A0F9-47E9-901F-71415A66345B
}
```

**挂载标志 (ATTACH_VIRTUAL_DISK_FLAG):**
| 标志 | 值 | 说明 |
|------|-----|------|
| NONE | 0x00000000 | 默认 |
| READ_ONLY | 0x00000001 | 只读挂载 |
| NO_DRIVE_LETTER | 0x00000002 | 不分配盘符 |
| PERMANENT_LIFETIME | 0x00000004 | 开机自动挂载 (持久化) |

**⚠️ 重要说明:**
- 不存在 `ATTACH_VIRTUAL_DISK_FLAG_AUTO_ATTACH` 标志
- 自动挂载通过 `ATTACH_VIRTUAL_DISK_FLAG_PERMANENT_LIFETIME` (0x00000004) 实现

#### 4.2.2 WMI (管理接口)

**命名空间:** `root\microsoft\windows\storage`
**类:** `MSFT_VirtualDisk`

**关键属性:**
| 属性 | 类型 | 说明 |
|------|------|------|
| Path | String | VHDX 文件完整路径 |
| IsManualAttach | Boolean | true=手动挂载, false=自动挂载 |
| OperationalStatus | String[] | 磁盘运行状态 |
| FriendlyName | String | 显示名称 |
| Size | UInt64 | 磁盘大小 |

**查询示例:**
```csharp
var searcher = new ManagementObjectSearcher(
    "root\\microsoft\\windows\\storage",
    "SELECT * FROM MSFT_VirtualDisk");

foreach (ManagementObject disk in searcher.Get())
{
    string path = disk["Path"]?.ToString();
    bool isManualAttach = (bool)disk["IsManualAttach"];
    // isManualAttach = false → 已设置自动挂载
}
```

**⚠️ WQL 字符串转义规则:**
WQL 字符串字面量中反斜杠 `\` 是转义字符，文件路径必须将 `\` 转义为 `\\`：
```csharp
// ❌ 错误：路径中的反斜杠未转义，查询会报 "无效查询"
$"SELECT * FROM MSFT_Disk WHERE Location = '{vhdxPath}'"

// ✅ 正确：先转义反斜杠，再转义单引号
$"SELECT * FROM MSFT_Disk WHERE Location = '{vhdxPath.Replace("\\", "\\\\").Replace("'", "\\'")}'"
```
注意：PowerShell 的 `Get-Disk` 等 cmdlet 会自动处理转义，但 C# 的 `ManagementObjectSearcher` 不会。

**关键 WMI 方法 (MSFT_Partition):**
| 方法 | 说明 |
|------|------|
| `AddAccessPath` | 添加盘符或文件夹挂载点，参数 AccessPath 如 `"E:\"` |
| `RemoveAccessPath` | 移除盘符或文件夹挂载点，参数 AccessPath 如 `"E:\"` |

返回码：0=成功，1=参数已检查（需重试）。

### 4.3 关键实现逻辑

#### 4.3.1 挂载虚拟磁盘
```
1. 验证文件路径存在且为 .vhd/.vhdx
2. 定义 VIRTUAL_STORAGE_TYPE (DeviceId 根据扩展名选择)
3. 调用 OpenVirtualDisk 获取句柄
4. 设置 ATTACH_VIRTUAL_DISK_PARAMETERS (Version=1)
5. 调用 AttachVirtualDisk
   - 如需自动挂载: Flags = PERMANENT_LIFETIME
   - 如仅临时挂载: Flags = NONE
6. 关闭句柄
7. 返回操作结果
```

#### 4.3.2 手动盘符分配（挂载后）
```
1. 查询当前盘符: QueryAssignedDriveLetter(filePath, 10)
2. 若当前盘符与目标一致 → 跳过，直接返回
3. 若不一致:
   a. 移除旧盘符: RemoveAccessPath(filePath, currentLetter)
   b. 分配新盘符: AssignDriveLetter(filePath, desiredLetter)
   c. 分配失败则回退查询系统实际分配的盘符
4. 保存实际盘符到配置
```

#### 4.3.2 卸载虚拟磁盘
```
1. 获取虚拟磁盘句柄
2. 调用 DetachVirtualDisk
3. 关闭句柄
4. 返回操作结果
```

#### 4.3.3 查询挂载状态
```
1. WMI 查询 MSFT_VirtualDisk
2. 对每个磁盘:
   - 获取 Path, IsManualAttach
3. 同时获取已挂载磁盘的盘符映射
4. 合并信息返回
```

---

## 5. 项目结构

```
VHDX_Manager/
├── App.xaml                          # 应用入口
├── App.xaml.cs                       # 应用启动逻辑 (UAC 提升)
├── CLAUDE.md                         # 项目文档
├── virtdisk.dll                      # Windows 虚拟磁盘 API DLL
├── virtdisk.h                        # API 头文件参考
│
├── Models/
│   └── VirtualDiskInfo.cs           # 虚拟磁盘数据模型
│
├── Services/
│   ├── IVirtualDiskService.cs       # 磁盘操作接口
│   ├── VirtualDiskService.cs        # virtdisk.dll P/Invoke 实现
│   ├── WmiQueryService.cs           # WMI 状态查询服务
│   ├── AutoMountTracker.cs          # 自动挂载配置持久化
│   ├── VhdService.cs                # Windows 服务（开机自动挂载）
│   └── WindowsServiceManager.cs     # 服务生命周期管理
│
├── ViewModels/
│   ├── MainViewModel.cs             # 主窗口 ViewModel
│   └── RelayCommand.cs              # ICommand 实现
│
├── Views/
│   └── MainWindow.xaml              # 主窗口 XAML
│
├── Converters/
│   ├── BoolToStatusConverter.cs     # 布尔 → 状态文本
│   └── BoolToColorConverter.cs      # 布尔 → 颜色
│
├── Helpers/
│   ├── EnvironmentChecker.cs        # 启动环境检查
│   └── NativeMethods.cs             # P/Invoke 声明与结构体
│
└── Resources/
    └── Styles.xaml                  # 全局样式资源
```

---

## 6. 数据模型

### 6.1 VirtualDiskInfo
```csharp
public class VirtualDiskInfo : INotifyPropertyChanged
{
    public string FilePath { get; set; }        // VHDX 文件路径
    public string DriveLetter { get; set; }     // 盘符 (如 "D:")
    public bool IsMounted { get; set; }         // 是否已挂载
    public bool IsAutoMount { get; set; }       // 是否自动挂载
    public string Format { get; set; }          // VHD / VHDX
    public long TotalSize { get; set; }         // 总容量 (字节)
    public long FreeSpace { get; set; }         // 可用空间 (字节)
    public string Status { get; set; }          // 状态文本
}
```

### 6.2 EnvironmentCheckResult
```csharp
public class EnvironmentCheckResult
{
    public bool IsVirtdiskAvailable { get; set; }
    public bool IsWmiAvailable { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsWindowsVersionSupported { get; set; }
    public string WindowsVersion { get; set; }
    public string DotNetVersion { get; set; }

    public bool CanMount => IsVirtdiskAvailable && IsAdmin;
    public bool CanQuery => IsWmiAvailable;
    public bool IsFullyFunctional => CanMount && CanQuery;
}
```

---

## 7. 界面设计规范

### 7.1 整体风格
- **设计语言**: 现代扁平化设计
- **配色方案**: 深色/浅色主题 (跟随系统)
- **字体**: Segoe UI (系统默认)
- **圆角**: 8px
- **间距**: 16px 基础间距

### 7.2 主窗口布局
```
┌──────────────────────────────────────────────────┐
│  VHDX Manager                          [─][□][✕] │
├──────────────────────────────────────────────────┤
│  [+ 挂载新磁盘]  [🔄 刷新]              状态栏   │
├──────────────────────────────────────────────────┤
│                                                  │
│  ┌────────────────────────────────────────────┐  │
│  │ 📁 D:\Data.vhdx                           │  │
│  │    盘符: E:  |  已挂载  |  自动挂载: ☑    │  │
│  │    500 GB / 200 GB 可用                    │  │
│  │                        [卸载] [取消自动]   │  │
│  ├────────────────────────────────────────────┤  │
│  │ 📁 E:\Backup.vhdx                         │  │
│  │    盘符: F:  |  已挂载  |  自动挂载: ☐    │  │
│  │    1 TB / 800 GB 可用                      │  │
│  │                        [卸载] [设为自动]   │  │
│  └────────────────────────────────────────────┘  │
│                                                  │
├──────────────────────────────────────────────────┤
│  共 2 个虚拟磁盘  |  上次刷新: 2024-01-01 12:00  │
└──────────────────────────────────────────────────┘
```

### 7.3 控件规范
- **按钮**: 主操作使用强调色，次要操作使用灰色
- **开关**: ToggleSwitch 控件显示自动挂载状态
- **列表**: 使用卡片式布局，每个磁盘一个卡片
- **状态**: 使用图标 + 文字组合表示状态

---

## 8. 错误处理

### 8.1 错误类型
| 类型 | 处理方式 |
|------|----------|
| 文件不存在 | 提示用户检查路径 |
| 权限不足 | 提示以管理员身份运行 |
| API 调用失败 | 显示错误码和描述 |
| 磁盘正在使用 | 提示关闭占用程序后重试 |
| WMI 查询失败 | 记录日志，显示通用错误 |

### 8.2 错误显示
- 使用 Toast 通知或内联提示
- 错误信息简洁明了，包含操作建议
- 详细错误记录到日志文件

---

## 9. 性能要求

- 启动扫描时间 < 3 秒
- 刷新操作 < 1 秒
- 内存占用 < 50 MB
- UI 响应无卡顿

---

## 10. 测试要求

### 10.1 单元测试
- VirtualDiskService 各方法
- WmiQueryService 查询逻辑
- ViewModel 状态管理

### 10.2 集成测试
- 完整挂载/卸载流程
- 自动挂载设置切换
- 定时刷新功能

### 10.3 UI 测试
- 按钮点击响应
- 列表显示正确性
- 错误提示显示

---

## 11. 开发规范

### 11.1 代码规范
- 遵循 C# 编码规范
- 使用 MVVM 模式
- 异步操作使用 async/await
- 资源释放使用 using/Dispose

### 11.2 Git 规范
- 提交信息格式: `[类型] 描述`
- 类型: feat, fix, refactor, docs, test

### 11.3 依赖管理
- 最小化第三方依赖
- 优先使用 .NET 内置库
- 记录所有依赖项及版本