// CustomizedBrowser\Services\InstanceService.cs
using CustomizedBrowser.Core;
using CustomizedBrowser.Models;
using CustomizedBrowser.Views;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace CustomizedBrowser.Services
{
    /// <summary>
    /// 实例服务实现
    /// 负责浏览器实例的创建、管理、启动、停止等操作
    /// </summary>
    public class InstanceService : IInstanceService
    {
        private Dictionary<string, InstanceInfo> _instanceCache = new();
        private Dictionary<string, string> _instanceNotes = new();
        private Dictionary<string, BrowserWindow> _openBrowserWindows = new();
        private readonly IBrowserWindowFactory _browserWindowFactory;
        private readonly IWebViewEnvironmentManager _environmentManager;

        /// <summary>
        /// 构造函数
        /// </summary>
        public InstanceService(IBrowserWindowFactory browserWindowFactory, IWebViewEnvironmentManager environmentManager)
        {
            _browserWindowFactory = browserWindowFactory;
            _environmentManager = environmentManager;
            LoadInstanceNotes();
        }

        /// <summary>
        /// 获取所有实例列表
        /// </summary>
        public List<InstanceInfo> GetAllInstances()
        {
            var instances = new List<InstanceInfo>();
            string instancesPath = AppConstants.GetInstancesPath();

            if (Directory.Exists(instancesPath))
            {
                var dirs = Directory.GetDirectories(instancesPath);
                foreach (var dir in dirs)
                {
                    string instanceId = Path.GetFileName(dir);
                    
                    // 如果缓存中已有该实例且正在运行，使用缓存的（状态更准确）
                    if (_instanceCache.TryGetValue(instanceId, out var cachedInstance) && 
                        cachedInstance.Status == InstanceStatus.Running)
                    {
                        instances.Add(cachedInstance);
                    }
                    else
                    {
                        var instance = CreateInstanceInfoFromDirectory(instanceId, dir);
                        // 检查是否正在运行
                        if (_openBrowserWindows.ContainsKey(instanceId))
                        {
                            instance.Status = InstanceStatus.Running;
                        }
                        instances.Add(instance);
                    }
                }
            }

            // 更新缓存
            _instanceCache = instances.ToDictionary(i => i.InstanceId);
            return instances;
        }

        /// <summary>
        /// 从目录创建实例信息
        /// </summary>
        private InstanceInfo CreateInstanceInfoFromDirectory(string instanceId, string dirPath)
        {
            var dirInfo = new DirectoryInfo(dirPath);

            return new InstanceInfo
            {
                InstanceId = instanceId,
                Name = instanceId,
                Status = InstanceStatus.Stopped,
                Note = _instanceNotes.ContainsKey(instanceId) ? _instanceNotes[instanceId] : string.Empty,
                LastUsedTime = dirInfo.LastWriteTime,
                DataPath = dirPath
            };
        }

        /// <summary>
        /// 创建新实例
        /// </summary>
        public Task<InstanceInfo?> CreateInstanceAsync(string name, string note = "")
        {
            if (InstanceExists(name))
            {
                return Task.FromResult<InstanceInfo?>(null);
            }

            try
            {
                string instancePath = AppConstants.GetInstanceDataPath(name);
                Directory.CreateDirectory(instancePath);

                if (!string.IsNullOrEmpty(note))
                {
                    _instanceNotes[name] = note;
                    SaveInstanceNotes();
                }

                var instance = new InstanceInfo
                {
                    InstanceId = name,
                    Name = name,
                    Status = InstanceStatus.Stopped,
                    Note = note,
                    LastUsedTime = DateTime.Now,
                    DataPath = instancePath
                };

                _instanceCache[name] = instance;
                return Task.FromResult<InstanceInfo?>(instance);
            }
            catch (Exception)
            {
                return Task.FromResult<InstanceInfo?>(null);
            }
        }

        /// <summary>
        /// 启动实例
        /// </summary>
        public Task<bool> StartInstanceAsync(string instanceId)
        {
            try
            {
                if (_openBrowserWindows.ContainsKey(instanceId))
                {
                    _openBrowserWindows[instanceId].Activate();
                    return Task.FromResult(true);
                }

                if (!InstanceExists(instanceId))
                {
                    return Task.FromResult(false);
                }

                var browserWindow = _browserWindowFactory.CreateBrowserWindow(instanceId);
                browserWindow.Closed += (s, e) => OnBrowserWindowClosed(instanceId);
                _openBrowserWindows[instanceId] = browserWindow;
                
                // 更新缓存中的状态
                if (_instanceCache.ContainsKey(instanceId))
                {
                    _instanceCache[instanceId].Status = InstanceStatus.Running;
                }
                
                browserWindow.Show();

                return Task.FromResult(true);
            }
            catch (Exception)
            {
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// 浏览器窗口关闭事件处理
        /// </summary>
        private void OnBrowserWindowClosed(string instanceId)
        {
            _openBrowserWindows.Remove(instanceId);
            
            // 更新缓存中的状态
            if (_instanceCache.ContainsKey(instanceId))
            {
                _instanceCache[instanceId].Status = InstanceStatus.Stopped;
            }
        }

        /// <summary>
        /// 停止实例
        /// </summary>
        public async Task<bool> StopInstanceAsync(string instanceId)
        {
            try
            {
                if (_openBrowserWindows.TryGetValue(instanceId, out var window))
                {
                    window.Close();
                    // 等待窗口完全关闭
                    await System.Threading.Tasks.Task.Delay(500);
                }
                
                // 确保清理进程
                Core.ProcessCleanupHelper.CleanupInstanceProcesses(instanceId);
                
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 停止所有正在运行的实例
        /// </summary>
        public async Task StopAllInstancesAsync()
        {
            try
            {
                // 创建一个副本，避免在迭代时修改集合
                var openInstances = _openBrowserWindows.Keys.ToList();
                
                foreach (var instanceId in openInstances)
                {
                    await StopInstanceAsync(instanceId);
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 删除实例
        /// </summary>
        public async Task<bool> DeleteInstanceAsync(string instanceId)
        {
            try
            {
                await StopInstanceAsync(instanceId);

                // 确保即使窗口关闭事件还没触发，也先移除
                _openBrowserWindows.Remove(instanceId);
                
                // 再次确保清理进程
                Core.ProcessCleanupHelper.CleanupInstanceProcesses(instanceId);

                string instancePath = AppConstants.GetInstanceDataPath(instanceId);
                if (Directory.Exists(instancePath))
                {
                    Directory.Delete(instancePath, true);
                }

                // 清理Environment缓存
                _environmentManager.RemoveEnvironment(instanceId);

                _instanceNotes.Remove(instanceId);
                SaveInstanceNotes();
                _instanceCache.Remove(instanceId);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 更新实例备注
        /// </summary>
        public bool UpdateInstanceNote(string instanceId, string note)
        {
            _instanceNotes[instanceId] = note;
            SaveInstanceNotes();

            if (_instanceCache.ContainsKey(instanceId))
            {
                _instanceCache[instanceId].Note = note;
            }

            return true;
        }

        /// <summary>
        /// 重命名实例
        /// 实例 ID 同时是数据目录名，因此重命名本质是移动实例数据目录，并同步备注记录；
        /// 运行中的实例会先被停止，避免 WebView2 进程占用目录导致移动失败
        /// </summary>
        public async Task<bool> RenameInstanceAsync(string oldInstanceId, string newInstanceId)
        {
            if (string.IsNullOrWhiteSpace(oldInstanceId) || string.IsNullOrWhiteSpace(newInstanceId))
            {
                return false;
            }

            oldInstanceId = oldInstanceId.Trim();
            newInstanceId = newInstanceId.Trim();

            if (string.Equals(oldInstanceId, newInstanceId, StringComparison.Ordinal))
            {
                return true;
            }

            // 校验新名称合法性与是否已被占用
            if (newInstanceId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                newInstanceId == "." || newInstanceId == ".." ||
                !InstanceExists(oldInstanceId) || InstanceExists(newInstanceId))
            {
                return false;
            }

            try
            {
                // 先停止实例并清理进程、环境缓存，释放数据目录占用
                await StopInstanceAsync(oldInstanceId);
                _openBrowserWindows.Remove(oldInstanceId);
                Core.ProcessCleanupHelper.CleanupInstanceProcesses(oldInstanceId);
                _environmentManager.RemoveEnvironment(oldInstanceId);

                string oldPath = AppConstants.GetInstanceDataPath(oldInstanceId);
                string newPath = AppConstants.GetInstanceDataPath(newInstanceId);
                Directory.Move(oldPath, newPath);

                // 备注以实例 ID 为键保存，需要同步迁移
                string note = GetInstanceNote(oldInstanceId);
                if (_instanceNotes.Remove(oldInstanceId))
                {
                    _instanceNotes[newInstanceId] = note;
                    SaveInstanceNotes();
                }

                _instanceCache.Remove(oldInstanceId);
                _instanceCache.Remove(newInstanceId);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 获取实例备注
        /// </summary>
        public string GetInstanceNote(string instanceId)
        {
            return _instanceNotes.ContainsKey(instanceId) ? _instanceNotes[instanceId] : string.Empty;
        }

        /// <summary>
        /// 批量启动实例
        /// </summary>
        public async Task<int> BatchStartInstancesAsync(List<string> instanceIds)
        {
            int successCount = 0;
            foreach (var id in instanceIds)
            {
                if (await StartInstanceAsync(id))
                {
                    successCount++;
                }
            }
            return successCount;
        }

        /// <summary>
        /// 批量停止实例
        /// </summary>
        public async Task<int> BatchStopInstancesAsync(List<string> instanceIds)
        {
            int successCount = 0;
            foreach (var id in instanceIds)
            {
                if (await StopInstanceAsync(id))
                {
                    successCount++;
                }
            }
            return successCount;
        }

        /// <summary>
        /// 批量删除实例
        /// </summary>
        public async Task<int> BatchDeleteInstancesAsync(List<string> instanceIds)
        {
            int successCount = 0;
            foreach (var id in instanceIds)
            {
                if (await DeleteInstanceAsync(id))
                {
                    successCount++;
                }
            }
            return successCount;
        }

        /// <summary>
        /// 检查实例是否存在
        /// </summary>
        public bool InstanceExists(string instanceId)
        {
            string instancePath = AppConstants.GetInstanceDataPath(instanceId);
            return Directory.Exists(instancePath);
        }

        /// <summary>
        /// 生成唯一实例ID
        /// </summary>
        public string GenerateUniqueInstanceId()
        {
            string baseName = "Instance_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string candidate = baseName;
            int index = 1;

            while (InstanceExists(candidate))
            {
                candidate = $"{baseName}_{index}";
                index++;
            }

            return candidate;
        }

        /// <summary>
        /// 加载实例备注
        /// </summary>
        private void LoadInstanceNotes()
        {
            try
            {
                string notesFilePath = AppConstants.GetInstanceNotesFilePath();
                if (File.Exists(notesFilePath))
                {
                    string json = File.ReadAllText(notesFilePath);
                    _instanceNotes = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
                }
            }
            catch (Exception)
            {
                _instanceNotes = new Dictionary<string, string>();
            }
        }

        /// <summary>
        /// 保存实例备注
        /// </summary>
        private void SaveInstanceNotes()
        {
            try
            {
                string notesFilePath = AppConstants.GetInstanceNotesFilePath();
                string? directoryPath = Path.GetDirectoryName(notesFilePath);
                if (!string.IsNullOrEmpty(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                string json = JsonSerializer.Serialize(_instanceNotes, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(notesFilePath, json);
            }
            catch (Exception)
            {
            }
        }
    }
}
