// CustomizedBrowser\Core\ProcessCleanupHelper.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CustomizedBrowser.Core
{
    /// <summary>
    /// 进程清理助手
    /// 负责清理残留的 WebView2 进程
    /// </summary>
    public static class ProcessCleanupHelper
    {
        /// <summary>
        /// 清理指定实例的进程
        /// </summary>
        public static void CleanupInstanceProcesses(string instanceId)
        {
            try
            {
                string processIdFile = AppConstants.GetProcessIdsFilePath(instanceId);
                var processIdsToKill = new List<int>();
                
                if (File.Exists(processIdFile))
                {
                    string json = File.ReadAllText(processIdFile);
                    var content = JsonSerializer.Deserialize<ProcessIdsContent>(json);
                    if (content?.ProcessIds != null)
                    {
                        processIdsToKill.AddRange(content.ProcessIds);
                    }
                }
                
                // 查找可能相关的 msedgewebview2 进程（根据用户数据目录）
                string userDataFolder = AppConstants.GetInstanceDataPath(instanceId);
                var additionalProcessIds = FindWebViewProcessesByUserDataFolder(userDataFolder);
                processIdsToKill.AddRange(additionalProcessIds);
                
                // 去重
                processIdsToKill = processIdsToKill.Distinct().ToList();
                
                if (processIdsToKill.Count > 0)
                {
                    KillProcesses(processIdsToKill);
                }

                // 清理文件
                try
                {
                    if (File.Exists(processIdFile))
                    {
                        File.Delete(processIdFile);
                    }
                }
                catch (Exception)
                {
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 根据用户数据目录查找相关的 WebView2 进程
        /// </summary>
        private static List<int> FindWebViewProcessesByUserDataFolder(string userDataFolder)
        {
            var result = new List<int>();
            try
            {
                var processes = Process.GetProcessesByName("msedgewebview2");
                foreach (var process in processes)
                {
                    try
                    {
                        // 检查进程是否仍然存在
                        if (process.HasExited)
                        {
                            continue;
                        }
                        
                        // 尝试获取命令行参数（可能需要管理员权限）
                        try
                        {
                            string cmdLine = GetCommandLine(process);
                            if (!string.IsNullOrEmpty(cmdLine) && cmdLine.Contains(userDataFolder))
                            {
                                result.Add(process.Id);
                            }
                        }
                        catch
                        {
                            // 如果无法获取命令行，我们保守处理，不添加
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            catch (Exception)
            {
            }
            return result;
        }

        /// <summary>
        /// 获取进程的命令行参数
        /// </summary>
        private static string GetCommandLine(Process process)
        {
            try
            {
                using (var searcher = new System.Management.ManagementObjectSearcher(
                    "SELECT CommandLine FROM Win32_Process WHERE ProcessId = " + process.Id))
                {
                    foreach (var managementObject in searcher.Get())
                    {
                        object? commandLineObj = managementObject["CommandLine"];
                        if (commandLineObj != null)
                        {
                            return commandLineObj.ToString() ?? string.Empty;
                        }
                    }
                }
            }
            catch
            {
            }
            return string.Empty;
        }

        /// <summary>
        /// 清理所有残留进程（应用退出时调用）
        /// 先清理通过 processIdFile 记录的进程，然后查找可能的残留
        /// </summary>
        public static void CleanupAllProcesses()
        {
            try
            {
                string[] instancePaths = AppConstants.GetAllInstanceDataPaths();
                foreach (string instancePath in instancePaths)
                {
                    string instanceId = Path.GetFileName(instancePath);
                    CleanupInstanceProcesses(instanceId);
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 杀掉指定的进程ID列表
        /// </summary>
        private static void KillProcesses(List<int> processIds)
        {
            foreach (int processId in processIds)
            {
                try
                {
                    Process? process = Process.GetProcessById(processId);
                    if (process != null && !process.HasExited)
                    {
                        process.Kill();
                        // 等待进程退出
                        process.WaitForExit(3000);
                    }
                }
                catch (ArgumentException)
                {
                    // 进程不存在
                }
                catch (Exception)
                {
                }
            }
        }

        /// <summary>
        /// 进程ID内容模型
        /// </summary>
        private class ProcessIdsContent
        {
            public List<int>? ProcessIds { get; set; }
        }
    }
}
