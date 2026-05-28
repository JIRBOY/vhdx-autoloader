using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.ServiceProcess;
using Microsoft.Win32;
using VHDX_Manager.Helpers;
using VHDX_Manager.Models;
using VHDX_Manager.Services;

namespace VHDX_Manager.ViewModels
{
    /// <summary>
    /// 主窗口 ViewModel
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly IVirtualDiskService _diskService;
        private readonly AutoMountTracker _autoMountTracker;
        private readonly WmiQueryService _wmiService;
        private readonly DispatcherTimer _refreshTimer;

        private ObservableCollection<VirtualDiskInfo> _virtualDisks = new();
        private VirtualDiskInfo? _selectedDisk;
        private bool _isLoading;
        private string _statusMessage = string.Empty;
        private string _degradedModeMessage = string.Empty;
        private bool _isDegradedMode;
        private EnvironmentCheckResult? _environmentCheckResult;
        private DateTime _lastRefreshTime = DateTime.MinValue;

        private bool _isServiceInstalled;
        private string _serviceStatusText = "服务未安装";
        private System.Windows.Media.Brush _serviceStatusBrush = new SolidColorBrush(Color.FromRgb(158, 158, 158));
        private int _checkIntervalMinutes = 10;

        public MainViewModel()
        {
            _autoMountTracker = new AutoMountTracker();
            _diskService = new VirtualDiskService(_autoMountTracker);
            _wmiService = new WmiQueryService(_autoMountTracker);

            // 初始化命令
            MountCommand = new RelayCommand(ExecuteMount, CanExecuteMount);
            UnmountCommand = new RelayCommand<VirtualDiskInfo>(ExecuteUnmount, CanExecuteUnmount);
            RefreshCommand = new AsyncRelayCommand(ExecuteRefreshAsync);
            ToggleAutoMountCommand = new AsyncRelayCommand<VirtualDiskInfo>(ExecuteToggleAutoMountAsync);
            SelectFileCommand = new RelayCommand(ExecuteSelectFile);
            CycleMountPreferenceCommand = new AsyncRelayCommand<VirtualDiskInfo>(ExecuteShowMountPreferenceMenuAsync);
            InstallServiceCommand = new AsyncRelayCommand(ExecuteInstallServiceAsync);
            UninstallServiceCommand = new AsyncRelayCommand(ExecuteUninstallServiceAsync);

            // 初始化定时刷新
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(120)
            };
            _refreshTimer.Tick += async (s, e) => await RefreshAsync();
        }

        /// <summary>
        /// 窗口加载后调用的初始化方法
        /// </summary>
        public async Task InitializeOnLoadAsync()
        {
            await InitializeAsync();
        }

        #region 属性

        /// <summary>
        /// 虚拟磁盘列表
        /// </summary>
        public ObservableCollection<VirtualDiskInfo> VirtualDisks
        {
            get => _virtualDisks;
            set => SetProperty(ref _virtualDisks, value);
        }

        /// <summary>
        /// 选中的虚拟磁盘
        /// </summary>
        public VirtualDiskInfo? SelectedDisk
        {
            get => _selectedDisk;
            set
            {
                if (SetProperty(ref _selectedDisk, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        /// <summary>
        /// 是否正在加载
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        /// <summary>
        /// 状态消息
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        /// <summary>
        /// 降级模式提示信息
        /// </summary>
        public string DegradedModeMessage
        {
            get => _degradedModeMessage;
            set => SetProperty(ref _degradedModeMessage, value);
        }

        /// <summary>
        /// 是否处于降级模式
        /// </summary>
        public bool IsDegradedMode
        {
            get => _isDegradedMode;
            set => SetProperty(ref _isDegradedMode, value);
        }

        /// <summary>
        /// 环境检查结果
        /// </summary>
        public EnvironmentCheckResult? EnvironmentCheckResult
        {
            get => _environmentCheckResult;
            set
            {
                if (SetProperty(ref _environmentCheckResult, value))
                {
                    OnPropertyChanged(nameof(CanMount));
                    OnPropertyChanged(nameof(CanQuery));
                    OnPropertyChanged(nameof(IsFullyFunctional));
                }
            }
        }

        /// <summary>
        /// 是否可以执行挂载操作
        /// </summary>
        public bool CanMount => _environmentCheckResult?.CanMount ?? false;

        /// <summary>
        /// 是否可以查询状态
        /// </summary>
        public bool CanQuery => _environmentCheckResult?.CanQuery ?? false;

        /// <summary>
        /// 是否所有功能都可用
        /// </summary>
        public bool IsFullyFunctional => _environmentCheckResult?.IsFullyFunctional ?? false;

        /// <summary>
        /// 上次刷新时间
        /// </summary>
        public string LastRefreshTimeText => _lastRefreshTime == DateTime.MinValue
            ? "未刷新"
            : $"上次刷新: {_lastRefreshTime:yyyy-MM-dd HH:mm:ss}";

        /// <summary>
        /// 磁盘数量
        /// </summary>
        public string DiskCountText => $"共 {VirtualDisks.Count} 个虚拟磁盘";

        /// <summary>
        /// 服务是否已安装
        /// </summary>
        public bool IsServiceInstalled
        {
            get => _isServiceInstalled;
            set => SetProperty(ref _isServiceInstalled, value);
        }

        /// <summary>
        /// 服务状态文本
        /// </summary>
        public string ServiceStatusText
        {
            get => _serviceStatusText;
            set => SetProperty(ref _serviceStatusText, value);
        }

        /// <summary>
        /// 服务状态颜色画刷
        /// </summary>
        public System.Windows.Media.Brush ServiceStatusBrush
        {
            get => _serviceStatusBrush;
            set => SetProperty(ref _serviceStatusBrush, value);
        }

        /// <summary>
        /// 检查间隔（分钟）
        /// </summary>
        public int CheckIntervalMinutes
        {
            get => _checkIntervalMinutes;
            set
            {
                if (SetProperty(ref _checkIntervalMinutes, value))
                {
                    _autoMountTracker.SetCheckIntervalMinutes(value);
                }
            }
        }

        #endregion

        #region 命令

        /// <summary>
        /// 挂载命令
        /// </summary>
        public ICommand MountCommand { get; }

        /// <summary>
        /// 卸载命令
        /// </summary>
        public ICommand UnmountCommand { get; }

        /// <summary>
        /// 刷新命令
        /// </summary>
        public ICommand RefreshCommand { get; }

        /// <summary>
        /// 切换自动挂载命令
        /// </summary>
        public ICommand ToggleAutoMountCommand { get; }

        /// <summary>
        /// 选择文件命令
        /// </summary>
        public ICommand SelectFileCommand { get; }

        /// <summary>
        /// 切换挂载偏好命令
        /// </summary>
        public ICommand CycleMountPreferenceCommand { get; }

        /// <summary>
        /// 安装服务命令
        /// </summary>
        public ICommand InstallServiceCommand { get; }

        /// <summary>
        /// 卸载服务命令
        /// </summary>
        public ICommand UninstallServiceCommand { get; }

        #endregion

        #region 命令实现

        private bool CanExecuteMount()
        {
            return CanMount && !IsLoading;
        }

        private async void ExecuteMount()
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择虚拟磁盘文件",
                Filter = "虚拟磁盘文件 (*.vhd;*.vhdx)|*.vhd;*.vhdx|所有文件 (*.*)|*.*",
                FilterIndex = 1
            };

            if (dialog.ShowDialog() == true)
            {
                await MountAsync(dialog.FileName, true);
            }
        }

        private bool CanExecuteUnmount(VirtualDiskInfo? disk)
        {
            return CanMount && disk?.IsMounted == true && !IsLoading;
        }

        private async void ExecuteUnmount(VirtualDiskInfo? disk)
        {
            if (disk == null) return;

            var result = MessageBox.Show(
                $"确定要卸载虚拟磁盘吗？\n\n{disk.FilePath}",
                "确认卸载",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                await UnmountAsync(disk.FilePath);
            }
        }

        private async Task ExecuteRefreshAsync()
        {
            await RefreshAsync();
        }

        private async Task ExecuteToggleAutoMountAsync(VirtualDiskInfo? disk)
        {
            if (disk == null) return;
            await SetAutoMountAsync(disk, !disk.IsAutoMount);
        }

        private void ExecuteSelectFile()
        {
            ExecuteMount();
        }

        private async Task ExecuteShowMountPreferenceMenuAsync(VirtualDiskInfo? disk)
        {
            if (disk == null) return;

            var menu = new System.Windows.Controls.ContextMenu();

            var options = new[]
            {
                (MountPreference.AutoDriveLetter, "自动盘符", "系统自动分配盘符"),
                (MountPreference.ManualDriveLetter, "手动盘符", "指定盘符挂载"),
                (MountPreference.ManualFolderPath, "手动路径", "挂载到指定文件夹"),
                (MountPreference.AutoFolderPath, "自动路径", "系统自动管理路径")
            };

            foreach (var (pref, label, desc) in options)
            {
                var item = new System.Windows.Controls.MenuItem
                {
                    Header = $"{label}    {desc}",
                    FontWeight = disk.MountPreference == pref
                        ? System.Windows.FontWeights.Bold
                        : System.Windows.FontWeights.Normal,
                    Tag = pref
                };

                var capturedDisk = disk;
                var capturedPref = pref;
                item.Click += async (s, e) => await SetMountPreferenceAsync(capturedDisk, capturedPref);
                menu.Items.Add(item);
            }

            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.IsOpen = true;

            await Task.CompletedTask;
        }

        private async Task SetMountPreferenceAsync(VirtualDiskInfo disk, MountPreference newPref)
        {
            // 手动盘符模式：需要输入盘符
            if (newPref == MountPreference.ManualDriveLetter)
            {
                var existing = _autoMountTracker.GetSavedDriveLetter(disk.FilePath);
                var dialog = new Views.InputDialog("请输入盘符（如 E）:", existing);
                dialog.Title = "设置盘符";

                if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.InputText))
                    return;

                _autoMountTracker.SetSavedDriveLetter(disk.FilePath, dialog.InputText.Trim().TrimEnd(':'));
            }

            // 手动路径模式：需要选择目标文件夹
            if (newPref == MountPreference.ManualFolderPath)
            {
                var folderDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "选择挂载文件夹",
                    FileName = "选择此文件夹",
                    Filter = "文件夹|*.",
                    ValidateNames = false,
                    CheckFileExists = false,
                    CheckPathExists = true,
                    InitialDirectory = _autoMountTracker.GetTargetMountPath(disk.FilePath)
                };

                if (folderDialog.ShowDialog() != true)
                    return;

                var folderPath = System.IO.Path.GetDirectoryName(folderDialog.FileName);
                if (string.IsNullOrWhiteSpace(folderPath))
                    return;

                // 确保文件夹存在
                if (!System.IO.Directory.Exists(folderPath))
                {
                    try { System.IO.Directory.CreateDirectory(folderPath); }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"无法创建文件夹: {ex.Message}", "错误",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                _autoMountTracker.SetTargetMountPath(disk.FilePath, folderPath);
            }

            _autoMountTracker.SetMountPreference(disk.FilePath, newPref);

            // 如果已挂载，直接通过 WMI 修改挂载点（不卸载磁盘）
            if (disk.IsMounted)
            {
                var result = MessageBox.Show(
                    $"挂载模式已改为「{GetMountPreferenceText(newPref)}」。\n是否立即生效？",
                    "切换挂载模式",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    switch (newPref)
                    {
                        case MountPreference.ManualDriveLetter:
                            var savedLetter = _autoMountTracker.GetSavedDriveLetter(disk.FilePath);
                            if (!string.IsNullOrEmpty(savedLetter))
                            {
                                string desired = savedLetter.TrimEnd(':').ToUpperInvariant();
                                string current = WmiQueryService.QueryAssignedDriveLetter(disk.FilePath, 5);

                                if (!string.Equals(current, desired, StringComparison.OrdinalIgnoreCase))
                                {
                                    // 移除旧盘符，分配新盘符
                                    if (!string.IsNullOrEmpty(current))
                                        WmiQueryService.RemoveDriveLetter(disk.FilePath, current);

                                    var assignResult = WmiQueryService.AssignDriveLetter(disk.FilePath, desired);
                                    if (!assignResult.Success)
                                    {
                                        MessageBox.Show($"切换盘符失败: {assignResult.Message}", "错误",
                                            MessageBoxButton.OK, MessageBoxImage.Error);
                                    }
                                }
                            }
                            break;

                        case MountPreference.AutoDriveLetter:
                            // 移除手动盘符，让系统自动分配
                            var currentLetter = WmiQueryService.QueryAssignedDriveLetter(disk.FilePath, 5);
                            if (!string.IsNullOrEmpty(currentLetter))
                            {
                                WmiQueryService.RemoveDriveLetter(disk.FilePath, currentLetter);
                                await Task.Delay(500);
                            }
                            _autoMountTracker.SetSavedDriveLetter(disk.FilePath, string.Empty);
                            break;

                        case MountPreference.ManualFolderPath:
                            var targetPath = _autoMountTracker.GetTargetMountPath(disk.FilePath);
                            if (!string.IsNullOrWhiteSpace(targetPath))
                            {
                                var mountResult = VirtualDiskService.CreateFolderMountPoint(disk.FilePath, targetPath);
                                if (!mountResult.Success)
                                {
                                    MessageBox.Show($"设置文件夹挂载点失败: {mountResult.Message}", "错误",
                                        MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                            }
                            break;

                        case MountPreference.AutoFolderPath:
                            _autoMountTracker.SetSavedDriveLetter(disk.FilePath, string.Empty);
                            break;
                    }
                }
            }

            await RefreshAsync();
        }

        private static string GetMountPreferenceText(MountPreference pref) => pref switch
        {
            MountPreference.AutoDriveLetter => "自动盘符",
            MountPreference.ManualDriveLetter => "手动盘符",
            MountPreference.ManualFolderPath => "手动路径",
            MountPreference.AutoFolderPath => "自动路径",
            _ => "自动盘符"
        };

        private async Task ExecuteInstallServiceAsync()
        {
            IsLoading = true;
            StatusMessage = "正在安装服务...";

            try
            {
                var (success, message) = await WindowsServiceManager.InstallAsync();
                if (success)
                {
                    // 安装后同步启动类型
                    var hasAutoMount = _autoMountTracker.GetAllStates().Any(s => s.Value);
                    await WindowsServiceManager.SyncStartModeAsync(hasAutoMount);
                    StatusMessage = message;
                }
                else
                {
                    StatusMessage = message;
                    MessageBox.Show(message, "安装失败", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"安装服务失败: {ex.Message}";
            }
            finally
            {
                await RefreshServiceStatusAsync();
                IsLoading = false;
            }
        }

        private async Task ExecuteUninstallServiceAsync()
        {
            var result = MessageBox.Show(
                "确定要卸载服务吗？卸载后开机将不会自动挂载虚拟磁盘。",
                "确认卸载",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            IsLoading = true;
            StatusMessage = "正在卸载服务...";

            try
            {
                var (success, message) = await WindowsServiceManager.UninstallAsync();
                StatusMessage = message;

                if (!success)
                {
                    MessageBox.Show(message, "卸载失败", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"卸载服务失败: {ex.Message}";
                MessageBox.Show($"卸载服务时发生异常:\n{ex.Message}\n\n{ex.StackTrace}",
                    "卸载异常", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                try
                {
                    await RefreshServiceStatusAsync();
                }
                catch { }
                IsLoading = false;
            }
        }

        #endregion

        #region 业务方法

        /// <summary>
        /// 刷新服务状态显示
        /// </summary>
        private async Task RefreshServiceStatusAsync()
        {
            var result = await Task.Run(() =>
            {
                var installed = WindowsServiceManager.IsInstalled();

                if (!installed)
                {
                    return (installed, "服务未安装", 0); // 0=灰色
                }

                var status = WindowsServiceManager.GetStatus();
                var startMode = WindowsServiceManager.GetStartMode();

                string modeText = startMode switch
                {
                    ServiceStartMode.Automatic => "自动",
                    ServiceStartMode.Manual => "手动",
                    ServiceStartMode.Disabled => "禁用",
                    _ => "未知"
                };

                string statusText = status switch
                {
                    ServiceControllerStatus.Running => "运行中",
                    ServiceControllerStatus.Stopped => "已停止",
                    ServiceControllerStatus.StartPending => "启动中",
                    ServiceControllerStatus.StopPending => "停止中",
                    _ => "未知"
                };

                var text = $"服务: {statusText} ({modeText})";
                var colorCode = status == ServiceControllerStatus.Running ? 1 : 2; // 1=绿色, 2=橙色

                return (installed, text, colorCode);
            });

            // 在 UI 线程上创建 Brush
            IsServiceInstalled = result.installed;
            ServiceStatusText = result.Item2;
            ServiceStatusBrush = result.Item3 switch
            {
                1 => new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                2 => new SolidColorBrush(Color.FromRgb(255, 152, 0)),
                _ => new SolidColorBrush(Color.FromRgb(158, 158, 158))
            };
        }

        /// <summary>
        /// 初始化
        /// </summary>
        private async Task InitializeAsync()
        {
            IsLoading = true;
            StatusMessage = "正在检查运行环境...";

            try
            {
                // 执行环境检查
                EnvironmentCheckResult = EnvironmentChecker.Check();

                if (!EnvironmentCheckResult.IsWindowsVersionSupported)
                {
                    MessageBox.Show(
                        $"需要 Windows 10 或更高版本。\n当前版本: {EnvironmentCheckResult.WindowsVersion}",
                        "系统版本不支持",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    Application.Current.Shutdown();
                    return;
                }

                if (!EnvironmentCheckResult.IsAdmin)
                {
                    var result = MessageBox.Show(
                        "程序需要管理员权限才能正常工作。\n是否以管理员身份重新启动？",
                        "需要管理员权限",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        if (EnvironmentChecker.TryRestartAsAdmin())
                        {
                            Application.Current.Shutdown();
                            return;
                        }
                        else
                        {
                            MessageBox.Show(
                                "无法以管理员身份启动程序。",
                                "启动失败",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                        }
                    }
                }

                // 更新降级模式状态
                IsDegradedMode = EnvironmentCheckResult.IsDegradedMode;
                DegradedModeMessage = EnvironmentCheckResult.GetDegradedModeMessage();

                // 刷新磁盘列表
                if (CanQuery)
                {
                    await RefreshAsync();
                }

                // 刷新服务状态
                await RefreshServiceStatusAsync();
                CheckIntervalMinutes = _autoMountTracker.GetCheckIntervalMinutes();

                // 启动定时刷新
                _refreshTimer.Start();

                StatusMessage = "就绪";
            }
            catch (Exception ex)
            {
                StatusMessage = $"初始化失败: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 挂载虚拟磁盘
        /// </summary>
        private async Task MountAsync(string filePath, bool autoMount)
        {
            IsLoading = true;
            StatusMessage = $"正在挂载: {filePath}";

            try
            {
                // 读取已保存的挂载偏好和盘符
                var preference = _autoMountTracker.GetMountPreference(filePath);
                var targetPath = _autoMountTracker.GetTargetMountPath(filePath);
                var savedLetter = _autoMountTracker.GetSavedDriveLetter(filePath);

                var (success, message) = await _diskService.MountAsync(filePath, autoMount, preference, targetPath, savedLetter);

                if (success)
                {
                    StatusMessage = message;
                    await RefreshAsync();
                }
                else
                {
                    StatusMessage = $"挂载失败: {message}";
                    MessageBox.Show(message, "挂载失败", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"挂载失败: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 卸载虚拟磁盘
        /// </summary>
        private async Task UnmountAsync(string filePath)
        {
            IsLoading = true;
            StatusMessage = $"正在卸载: {filePath}";

            try
            {
                var (success, message) = await _diskService.UnmountAsync(filePath);

                if (success)
                {
                    StatusMessage = message;
                    await RefreshAsync();
                }
                else
                {
                    StatusMessage = $"卸载失败: {message}";
                    MessageBox.Show(message, "卸载失败", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"卸载失败: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 设置自动挂载状态
        /// </summary>
        private async Task SetAutoMountAsync(VirtualDiskInfo disk, bool autoMount)
        {
            IsLoading = true;
            StatusMessage = $"正在设置自动挂载: {disk.FilePath}";

            try
            {
                var (success, message) = await _diskService.SetAutoMountAsync(disk.FilePath, autoMount);

                if (success)
                {
                    StatusMessage = message;

                    // 同步服务启动类型
                    if (WindowsServiceManager.IsInstalled())
                    {
                        var hasAutoMount = _autoMountTracker.GetAllStates().Any(s => s.Value);
                        await WindowsServiceManager.SyncStartModeAsync(hasAutoMount);
                    }

                    await RefreshAsync();
                }
                else
                {
                    StatusMessage = $"设置失败: {message}";
                    MessageBox.Show(message, "设置失败", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"设置失败: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 刷新虚拟磁盘列表
        /// </summary>
        private async Task RefreshAsync()
        {
            if (!CanQuery) return;

            IsLoading = true;
            StatusMessage = "正在刷新...";

            try
            {
                var disks = await _wmiService.QueryVirtualDisksAsync();

                VirtualDisks.Clear();
                foreach (var disk in disks)
                {
                    VirtualDisks.Add(disk);
                }

                _lastRefreshTime = DateTime.Now;
                OnPropertyChanged(nameof(LastRefreshTimeText));
                OnPropertyChanged(nameof(DiskCountText));

                StatusMessage = "刷新完成";
            }
            catch (Exception ex)
            {
                StatusMessage = $"刷新失败: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion

        /// <summary>
        /// 尝试移除文件夹挂载点（切换挂载模式时调用）
        /// </summary>
        private static void TryRemoveFolderMountPoint(string folderPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folderPath)) return;

                string mountPoint = folderPath.TrimEnd('\\') + "\\";
                NativeMethods.DeleteVolumeMountPoint(mountPoint);
            }
            catch
            {
                // 移除失败不影响其他操作，卸载磁盘时系统通常会自动清理
            }
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

    /// <summary>
    /// 异步 RelayCommand
    /// </summary>
    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<Task> _execute;
        private readonly Func<bool>? _canExecute;
        private bool _isExecuting;

        public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter)
        {
            return !_isExecuting && (_canExecute == null || _canExecute());
        }

        public async void Execute(object? parameter)
        {
            if (_isExecuting) return;

            _isExecuting = true;
            CommandManager.InvalidateRequerySuggested();

            try
            {
                await _execute();
            }
            finally
            {
                _isExecuting = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    /// <summary>
    /// 泛型异步 RelayCommand
    /// </summary>
    public class AsyncRelayCommand<T> : ICommand
    {
        private readonly Func<T?, Task> _execute;
        private readonly Predicate<T?>? _canExecute;
        private bool _isExecuting;

        public AsyncRelayCommand(Func<T?, Task> execute, Predicate<T?>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter)
        {
            if (_isExecuting) return false;

            if (parameter is T typedParameter)
            {
                return _canExecute == null || _canExecute(typedParameter);
            }

            return _canExecute == null || _canExecute(default);
        }

        public async void Execute(object? parameter)
        {
            if (_isExecuting) return;

            _isExecuting = true;
            CommandManager.InvalidateRequerySuggested();

            try
            {
                if (parameter is T typedParameter)
                {
                    await _execute(typedParameter);
                }
                else
                {
                    await _execute(default);
                }
            }
            finally
            {
                _isExecuting = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }
}