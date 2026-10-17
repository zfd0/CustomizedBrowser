// CustomizedBrowser\Models\BrowserSettings.cs
using System.Text.Json.Serialization;

namespace CustomizedBrowser.Models
{
    /// <summary>
    /// 通知偏好设置类
    /// </summary>
    public class NotificationSettings
    {
        /// <summary>
        /// 是否启用通知
        /// </summary>
        public bool EnableNotifications { get; set; } = true;

        /// <summary>
        /// 是否显示下载通知
        /// </summary>
        public bool ShowDownloadNotifications { get; set; } = true;

        /// <summary>
        /// 是否显示更新通知
        /// </summary>
        public bool ShowUpdateNotifications { get; set; } = true;

        /// <summary>
        /// 是否启用声音
        /// </summary>
        public bool SoundEnabled { get; set; } = false;
    }

    /// <summary>
    /// 数据同步设置类
    /// </summary>
    public class SyncSettings
    {
        /// <summary>
        /// 是否启用同步
        /// </summary>
        public bool EnableSync { get; set; } = false;

        /// <summary>
        /// 是否同步书签
        /// </summary>
        public bool SyncBookmarks { get; set; } = true;

        /// <summary>
        /// 是否同步历史记录
        /// </summary>
        public bool SyncHistory { get; set; } = true;

        /// <summary>
        /// 是否同步浏览器设置
        /// </summary>
        public bool SyncBrowserSettings { get; set; } = true;

        /// <summary>
        /// 同步服务器地址
        /// </summary>
        public string SyncServerUrl { get; set; } = string.Empty;
    }

    /// <summary>
    /// 账户安全设置类
    /// </summary>
    public class SecuritySettings
    {
        /// <summary>
        /// 是否启用自动锁定
        /// </summary>
        public bool EnableAutoLock { get; set; } = false;

        /// <summary>
        /// 自动锁定时间（分钟）
        /// </summary>
        public int AutoLockMinutes { get; set; } = 30;

        /// <summary>
        /// 启动时是否需要密码
        /// </summary>
        public bool RequirePasswordOnStartup { get; set; } = false;

        /// <summary>
        /// 默认是否启用隐私模式
        /// </summary>
        public bool EnablePrivateModeByDefault { get; set; } = false;

        /// <summary>
        /// 是否阻止跟踪 Cookie
        /// </summary>
        public bool BlockTrackingCookies { get; set; } = true;
    }

    /// <summary>
    /// 缓存管理设置类
    /// </summary>
    public class CacheSettings
    {
        /// <summary>
        /// 最大缓存大小（MB），0 表示不限制
        /// </summary>
        public int MaxCacheSizeMB { get; set; } = 0;

        /// <summary>
        /// 退出时是否自动清除缓存
        /// </summary>
        public bool AutoClearOnExit { get; set; } = false;

        /// <summary>
        /// 自动清除缓存间隔天数（0 表示不自动清除）
        /// </summary>
        public int ClearCacheIntervalDays { get; set; } = 0;

        /// <summary>
        /// 上次清除缓存的时间
        /// </summary>
        public string LastClearCacheTime { get; set; } = string.Empty;
    }

    /// <summary>
    /// 完整的浏览器设置数据类
    /// </summary>
    public class BrowserSettings
    {
        /// <summary>
        /// 隐藏窗口设置
        /// </summary>
        public HideWindowOption HideWindowOption { get; set; } = HideWindowOption.Close;

        /// <summary>
        /// 是否在启动时恢复上次访问的页面
        /// </summary>
        public bool RestoreLastPageOnStartup { get; set; } = true;

        /// <summary>
        /// 是否保存并恢复所有标签页状态
        /// </summary>
        public bool RestoreSessionOnStartup { get; set; } = true;

        /// <summary>
        /// 默认页面 URL
        /// </summary>
        public string DefaultPageUrl { get; set; } = "https://www.google.com";

        /// <summary>
        /// 是否使用本地默认页面（新标签页样式）
        /// </summary>
        public bool UseLocalDefaultPage { get; set; } = false;

        /// <summary>
        /// 自定义本地默认页面文件路径（如果为空则使用内置模板）
        /// </summary>
        public string CustomLocalPagePath { get; set; } = string.Empty;

        /// <summary>
        /// 默认窗口宽度
        /// </summary>
        public double DefaultWindowWidth { get; set; } = 1920;

        /// <summary>
        /// 默认窗口高度
        /// </summary>
        public double DefaultWindowHeight { get; set; } = 1080;

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

        /// <summary>
        /// 缓存管理设置
        /// </summary>
        public CacheSettings Cache { get; set; } = new CacheSettings();
    }
}
