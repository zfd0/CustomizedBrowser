using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SimpleBrowser
{
    /// <summary>
    /// 主题选项枚举
    /// </summary>
    public enum ThemeOption
    {
        Light = 0,
        Dark = 1,
        System = 2
    }

    /// <summary>
    /// 通知偏好设置类
    /// </summary>
    public class NotificationSettings
    {
        public bool EnableNotifications { get; set; } = true;
        public bool ShowDownloadNotifications { get; set; } = true;
        public bool ShowUpdateNotifications { get; set; } = true;
        public bool SoundEnabled { get; set; } = false;
    }

    /// <summary>
    /// 数据同步设置类
    /// </summary>
    public class SyncSettings
    {
        public bool EnableSync { get; set; } = false;
        public bool SyncBookmarks { get; set; } = true;
        public bool SyncHistory { get; set; } = true;
        public bool SyncBrowserSettings { get; set; } = true;
        public string SyncServerUrl { get; set; } = "";
    }

    /// <summary>
    /// 账户安全设置类
    /// </summary>
    public class SecuritySettings
    {
        public bool EnableAutoLock { get; set; } = false;
        public int AutoLockMinutes { get; set; } = 30;
        public bool RequirePasswordOnStartup { get; set; } = false;
        public bool EnablePrivateModeByDefault { get; set; } = false;
        public bool BlockTrackingCookies { get; set; } = true;
    }

    /// <summary>
    /// 页面状态数据类
    /// </summary>
    public class TabState
    {
        public string Url { get; set; }
        public double ScrollPositionY { get; set; }
        public string Title { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// 会话状态数据类
    /// </summary>
    public class SessionState
    {
        public List<TabState> Tabs { get; set; } = new List<TabState>();
        public int ActiveTabIndex { get; set; } = 0;
        public DateTime SavedAt { get; set; }
    }

    /// <summary>
    /// 完整的浏览器设置数据类
    /// </summary>
    public class BrowserSettings
    {
        /// <summary>
        /// 隐藏窗口设置
        /// </summary>
        public MainForm.HideWindowOption HideWindowOption { get; set; } = MainForm.HideWindowOption.Close;

        /// <summary>
        /// 是否在启动时恢复上次访问的页面
        /// </summary>
        public bool RestoreLastPageOnStartup { get; set; } = true;

        /// <summary>
        /// 是否保存并恢复所有标签页状态
        /// </summary>
        public bool RestoreSessionOnStartup { get; set; } = true;

        /// <summary>
        /// 默认页面URL
        /// </summary>
        public string DefaultPageUrl { get; set; } = "https://www.google.com";

        /// <summary>
        /// 系统托盘显示文本
        /// </summary>
        public string TrayIconText { get; set; } = "Customized Browser";

        /// <summary>
        /// 界面主题设置
        /// </summary>
        public ThemeOption Theme { get; set; } = ThemeOption.Light;

        /// <summary>
        /// 通知偏好设置
        /// </summary>
        public NotificationSettings Notifications { get; set; } = new NotificationSettings();

        /// <summary>
        /// 数据同步设置
        /// </summary>
        public SyncSettings Sync { get; set; } = new SyncSettings();

        /// <summary>
        /// 账户安全设置
        /// </summary>
        public SecuritySettings Security { get; set; } = new SecuritySettings();
    }

    /// <summary>
    /// 设置管理器，用于保存和加载浏览器设置
    /// </summary>
    public static class SettingsManager
    {
        private static string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SimpleBrowser",
            "settings.json"
        );

        /// <summary>
        /// 获取会话状态文件路径
        /// </summary>
        /// <param name="instanceId">实例ID</param>
        /// <returns>会话状态文件路径</returns>
        public static string GetSessionStateFilePath(string instanceId)
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SimpleBrowser",
                "Instances",
                instanceId,
                "session_state.json"
            );
        }

        /// <summary>
        /// 加载保存的设置
        /// </summary>
        /// <returns>浏览器设置</returns>
        public static BrowserSettings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string jsonContent = File.ReadAllText(SettingsFilePath);
                    return JsonSerializer.Deserialize<BrowserSettings>(jsonContent);
                }
            }
            catch (Exception)
            {
                return new BrowserSettings();
            }

            return new BrowserSettings();
        }

        /// <summary>
        /// 保存设置到文件
        /// </summary>
        /// <param name="settings">要保存的浏览器设置</param>
        public static void SaveSettings(BrowserSettings settings)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath));

                string jsonContent = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
                });

                File.WriteAllText(SettingsFilePath, jsonContent);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 保存会话状态
        /// </summary>
        /// <param name="instanceId">实例ID</param>
        /// <param name="sessionState">会话状态</param>
        public static void SaveSessionState(string instanceId, SessionState sessionState)
        {
            try
            {
                string filePath = GetSessionStateFilePath(instanceId);
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                sessionState.SavedAt = DateTime.Now;

                string jsonContent = JsonSerializer.Serialize(sessionState, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(filePath, jsonContent);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 加载会话状态
        /// </summary>
        /// <param name="instanceId">实例ID</param>
        /// <returns>会话状态，如果不存在则返回null</returns>
        public static SessionState LoadSessionState(string instanceId)
        {
            try
            {
                string filePath = GetSessionStateFilePath(instanceId);
                if (File.Exists(filePath))
                {
                    string jsonContent = File.ReadAllText(filePath);
                    return JsonSerializer.Deserialize<SessionState>(jsonContent);
                }
            }
            catch (Exception)
            {
            }

            return null;
        }
    }
}
