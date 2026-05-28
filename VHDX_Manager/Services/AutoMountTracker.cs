using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using VHDX_Manager.Models;

namespace VHDX_Manager.Services
{
    /// <summary>
    /// 自动挂载状态追踪器（本地持久化配置）
    /// 系统 API 无法可靠检测 VHDX 的自动挂载状态，因此使用本地配置文件追踪。
    /// </summary>
    public class AutoMountTracker
    {
        private readonly string _configPath;
        private Dictionary<string, MountConfig> _configs;
        private ServiceConfig _serviceConfig;

        public AutoMountTracker()
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            _configPath = Path.Combine(appDir, "automount.json");
            var (configs, serviceConfig) = Load();
            _configs = configs;
            _serviceConfig = serviceConfig;
        }

        /// <summary>
        /// 获取指定 VHDX 文件的自动挂载状态
        /// </summary>
        public bool GetAutoMount(string filePath)
        {
            var key = NormalizePath(filePath);
            return _configs.TryGetValue(key, out var config) && config.AutoMount;
        }

        /// <summary>
        /// 设置指定 VHDX 文件的自动挂载状态
        /// </summary>
        public void SetAutoMount(string filePath, bool autoMount)
        {
            var key = NormalizePath(filePath);
            if (!_configs.TryGetValue(key, out var config))
            {
                config = new MountConfig();
                _configs[key] = config;
            }
            config.AutoMount = autoMount;
            Save();
        }

        /// <summary>
        /// 获取指定 VHDX 文件的挂载路径偏好
        /// 当无保存的偏好时，默认为无盘符（不映射到任何路径）
        /// </summary>
        public MountPreference GetMountPreference(string filePath)
        {
            var key = NormalizePath(filePath);
            return _configs.TryGetValue(key, out var config) ? config.MountPreference : MountPreference.AutoDriveLetter;
        }

        /// <summary>
        /// 设置指定 VHDX 文件的挂载路径偏好
        /// </summary>
        public void SetMountPreference(string filePath, MountPreference preference)
        {
            var key = NormalizePath(filePath);
            if (!_configs.TryGetValue(key, out var config))
            {
                config = new MountConfig();
                _configs[key] = config;
            }
            config.MountPreference = preference;
            Save();
        }

        /// <summary>
        /// 获取指定 VHDX 文件的目标挂载文件夹路径
        /// </summary>
        public string GetTargetMountPath(string filePath)
        {
            var key = NormalizePath(filePath);
            return _configs.TryGetValue(key, out var config) ? config.TargetMountPath : string.Empty;
        }

        /// <summary>
        /// 设置指定 VHDX 文件的目标挂载文件夹路径
        /// </summary>
        public void SetTargetMountPath(string filePath, string path)
        {
            var key = NormalizePath(filePath);
            if (!_configs.TryGetValue(key, out var config))
            {
                config = new MountConfig();
                _configs[key] = config;
            }
            config.TargetMountPath = path;
            Save();
        }

        /// <summary>
        /// 获取指定 VHDX 文件保存的实际盘符（如 "E"，不带冒号）
        /// </summary>
        public string GetSavedDriveLetter(string filePath)
        {
            var key = NormalizePath(filePath);
            return _configs.TryGetValue(key, out var config) ? config.SavedDriveLetter : string.Empty;
        }

        /// <summary>
        /// 保存 VHDX 文件的实际分配盘符
        /// </summary>
        public void SetSavedDriveLetter(string filePath, string driveLetter)
        {
            var key = NormalizePath(filePath);
            if (!_configs.TryGetValue(key, out var config))
            {
                config = new MountConfig();
                _configs[key] = config;
            }
            // 存储不带冒号的盘符（如 "E"）
            config.SavedDriveLetter = driveLetter?.TrimEnd(':') ?? string.Empty;
            Save();
        }

        /// <summary>
        /// 移除指定 VHDX 文件的记录
        /// </summary>
        public void Remove(string filePath)
        {
            var key = NormalizePath(filePath);
            if (_configs.Remove(key))
            {
                Save();
            }
        }

        /// <summary>
        /// 获取所有配置
        /// </summary>
        public IReadOnlyDictionary<string, MountConfig> GetAllConfigs()
        {
            return _configs;
        }

        /// <summary>
        /// 获取所有自动挂载状态（兼容旧接口）
        /// </summary>
        public IReadOnlyDictionary<string, bool> GetAllStates()
        {
            var states = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in _configs)
            {
                states[kvp.Key] = kvp.Value.AutoMount;
            }
            return states;
        }

        public int GetCheckIntervalMinutes() => _serviceConfig.CheckIntervalMinutes;

        public void SetCheckIntervalMinutes(int minutes)
        {
            if (minutes < 1) minutes = 1;
            if (minutes > 1440) minutes = 1440;
            _serviceConfig.CheckIntervalMinutes = minutes;
            Save();
        }

        private (Dictionary<string, MountConfig> Configs, ServiceConfig ServiceConfig) Load()
        {
            var emptyConfigs = new Dictionary<string, MountConfig>(StringComparer.OrdinalIgnoreCase);
            var defaultService = new ServiceConfig();

            try
            {
                if (!File.Exists(_configPath))
                    return (emptyConfigs, defaultService);

                var json = File.ReadAllText(_configPath);
                if (string.IsNullOrWhiteSpace(json))
                    return (emptyConfigs, defaultService);

                // 尝试解析为包装格式 {"Mounts": {...}, "Service": {...}}
                var element = JsonSerializer.Deserialize<JsonElement>(json);
                if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("Mounts", out var mountsElement))
                {
                    var configs = JsonSerializer.Deserialize<Dictionary<string, MountConfig>>(mountsElement.GetRawText(),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    ServiceConfig service = defaultService;
                    if (element.TryGetProperty("Service", out var serviceElement))
                    {
                        service = JsonSerializer.Deserialize<ServiceConfig>(serviceElement.GetRawText(),
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? defaultService;
                    }

                    return (configs != null
                        ? new Dictionary<string, MountConfig>(configs, StringComparer.OrdinalIgnoreCase)
                        : emptyConfigs, service);
                }

                // 尝试旧格式（Dictionary<string, MountConfig>）
                var data = JsonSerializer.Deserialize<Dictionary<string, MountConfig>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (data != null)
                    return (new Dictionary<string, MountConfig>(data, StringComparer.OrdinalIgnoreCase), defaultService);

                // 尝试旧格式（纯 bool）
                var oldData = JsonSerializer.Deserialize<Dictionary<string, bool>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (oldData != null)
                {
                    var result = new Dictionary<string, MountConfig>(StringComparer.OrdinalIgnoreCase);
                    foreach (var kvp in oldData)
                    {
                        result[kvp.Key] = new MountConfig { AutoMount = kvp.Value };
                    }
                    return (result, defaultService);
                }

                return (emptyConfigs, defaultService);
            }
            catch
            {
                return (emptyConfigs, defaultService);
            }
        }

        private void Save()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var wrapper = new { Mounts = _configs, Service = _serviceConfig };
                var json = JsonSerializer.Serialize(wrapper, options);
                File.WriteAllText(_configPath, json);
            }
            catch
            {
                // 保存失败不影响功能
            }
        }

        private static string NormalizePath(string filePath)
        {
            return filePath?.Trim().ToUpperInvariant() ?? string.Empty;
        }
    }

    /// <summary>
    /// 挂载配置
    /// </summary>
    public class MountConfig
    {
        public bool AutoMount { get; set; }
        public MountPreference MountPreference { get; set; }
        public string TargetMountPath { get; set; } = string.Empty;
        /// <summary>保存的实际分配盘符（不带冒号，如 "E"）</summary>
        public string SavedDriveLetter { get; set; } = string.Empty;
    }

    public class ServiceConfig
    {
        public int CheckIntervalMinutes { get; set; } = 10;
    }
}
