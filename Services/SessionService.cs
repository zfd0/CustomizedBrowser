// CustomizedBrowser\Services\SessionService.cs
using CustomizedBrowser.Core;
using CustomizedBrowser.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace CustomizedBrowser.Services
{
    /// <summary>
    /// 会话服务实现
    /// 负责浏览器实例会话状态的保存和恢复
    /// </summary>
    public class SessionService : ISessionService
    {
        /// <summary>
        /// 保存会话状态
        /// </summary>
        public void SaveSession(string instanceId, SessionState sessionState)
        {
            try
            {
                string instancePath = AppConstants.GetInstanceDataPath(instanceId);
                Directory.CreateDirectory(instancePath);

                string filePath = AppConstants.GetSessionStateFilePath(instanceId);
                sessionState.SavedAt = DateTime.Now;

                string jsonContent = JsonSerializer.Serialize(sessionState, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(filePath, jsonContent);
            }
            catch (Exception)
            {
                // 保存失败时静默处理
            }
        }

        /// <summary>
        /// 加载会话状态
        /// </summary>
        public SessionState? LoadSession(string instanceId)
        {
            try
            {
                string filePath = AppConstants.GetSessionStateFilePath(instanceId);

                if (File.Exists(filePath))
                {
                    string jsonContent = File.ReadAllText(filePath);
                    return JsonSerializer.Deserialize<SessionState>(jsonContent);
                }
            }
            catch (Exception)
            {
                // 加载失败时返回 null
            }

            return null;
        }

        /// <summary>
        /// 保存上次访问的页面
        /// </summary>
        public void SaveLastPage(string instanceId, string url)
        {
            try
            {
                string instancePath = AppConstants.GetInstanceDataPath(instanceId);
                Directory.CreateDirectory(instancePath);

                string filePath = AppConstants.GetLastPageFilePath(instanceId);
                var content = new { Url = url };
                string json = JsonSerializer.Serialize(content, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
            }
            catch (Exception)
            {
                // 保存失败时静默处理
            }
        }

        /// <summary>
        /// 加载上次访问的页面
        /// </summary>
        public string LoadLastPage(string instanceId, string defaultUrl)
        {
            try
            {
                string filePath = AppConstants.GetLastPageFilePath(instanceId);

                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    var content = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    return content?.ContainsKey("Url") == true ? content["Url"] : defaultUrl;
                }
            }
            catch (Exception)
            {
                // 加载失败时返回默认 URL
            }

            return defaultUrl;
        }
    }
}
