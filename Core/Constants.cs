// CustomizedBrowser\Core\Constants.cs
using System;
using System.IO;

namespace CustomizedBrowser.Core
{
    /// <summary>
    /// 应用程序常量定义
    /// 统一管理所有硬编码的常量值
    /// </summary>
    public static class AppConstants
    {
        /// <summary>
        /// 应用程序名称
        /// </summary>
        public const string AppName = "Customized Browser";

        /// <summary>
        /// 应用程序版本
        /// </summary>
        public const string AppVersion = "2.0.0";

        /// <summary>
        /// 数据根目录名称
        /// </summary>
        public const string DataRootFolderName = "SimpleBrowser";

        /// <summary>
        /// 实例数据目录名称
        /// </summary>
        public const string InstancesFolderName = "Instances";

        /// <summary>
        /// 设置文件名
        /// </summary>
        public const string SettingsFileName = "settings.json";

        /// <summary>
        /// 实例备注文件名
        /// </summary>
        public const string InstanceNotesFileName = "instance_notes.json";

        /// <summary>
        /// 会话状态文件名
        /// </summary>
        public const string SessionStateFileName = "session_state.json";

        /// <summary>
        /// 进程ID文件名
        /// </summary>
        public const string ProcessIdsFileName = "process_ids.json";

        /// <summary>
        /// 最后访问页面文件名
        /// </summary>
        public const string LastPageFileName = "last_page.json";

        /// <summary>
        /// 本地默认页面文件名
        /// </summary>
        public const string LocalDefaultPageFileName = "newtab.html";

        /// <summary>
        /// WebView2 用户数据文件夹名称
        /// </summary>
        public const string WebViewDataFolderName = "EBWebView";

        /// <summary>
        /// WebView2 默认配置文件文件夹名称
        /// </summary>
        public const string WebViewDefaultProfileFolderName = "Default";

        /// <summary>
        /// 可跨机迁移的站点存储子目录名称（相对于 WebView2 默认配置文件目录）
        /// 这些数据未使用 DPAPI 加密，可直接复制；Cookie 因加密需另行通过 API 搬运
        /// </summary>
        public static readonly string[] PortableStorageFolderNames = new[]
        {
            "Local Storage",
            "IndexedDB",
            "WebStorage",
            "Session Storage"
        };

        /// <summary>
        /// 自动锁定时间（毫秒）
        /// </summary>
        public const int AutoLockTimeMs = 60000;

        /// <summary>
        /// 缓存大小（字节）- 1GB
        /// </summary>
        public const long DiskCacheSizeBytes = 1073741824;

        /// <summary>
        /// 获取应用程序数据根目录路径
        /// </summary>
        public static string GetDataRootPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                DataRootFolderName
            );
        }

        /// <summary>
        /// 获取实例数据目录路径
        /// </summary>
        public static string GetInstancesPath()
        {
            return Path.Combine(GetDataRootPath(), InstancesFolderName);
        }

        /// <summary>
        /// 获取指定实例的数据目录路径
        /// </summary>
        public static string GetInstanceDataPath(string instanceId)
        {
            return Path.Combine(GetInstancesPath(), instanceId);
        }

        /// <summary>
        /// 获取设置文件路径
        /// </summary>
        public static string GetSettingsFilePath()
        {
            return Path.Combine(GetDataRootPath(), SettingsFileName);
        }

        /// <summary>
        /// 获取实例备注文件路径
        /// </summary>
        public static string GetInstanceNotesFilePath()
        {
            return Path.Combine(GetDataRootPath(), InstanceNotesFileName);
        }

        /// <summary>
        /// 获取指定实例的会话状态文件路径
        /// </summary>
        public static string GetSessionStateFilePath(string instanceId)
        {
            return Path.Combine(GetInstanceDataPath(instanceId), SessionStateFileName);
        }

        /// <summary>
        /// 获取指定实例的进程ID文件路径
        /// </summary>
        public static string GetProcessIdsFilePath(string instanceId)
        {
            return Path.Combine(GetInstanceDataPath(instanceId), ProcessIdsFileName);
        }

        /// <summary>
        /// 获取最后访问页面文件路径
        /// </summary>
        public static string GetLastPageFilePath(string instanceId)
        {
            return Path.Combine(GetInstanceDataPath(instanceId), "LastPage.json");
        }

        /// <summary>
        /// 获取指定实例的 WebView2 默认配置文件目录路径
        /// </summary>
        public static string GetWebViewDefaultProfilePath(string instanceId)
        {
            return Path.Combine(
                GetInstanceDataPath(instanceId),
                WebViewDataFolderName,
                WebViewDefaultProfileFolderName
            );
        }

        /// <summary>
        /// 获取本地默认页面文件路径
        /// </summary>
        public static string GetLocalDefaultPagePath()
        {
            return Path.Combine(GetDataRootPath(), LocalDefaultPageFileName);
        }

        /// <summary>
        /// 获取所有实例的数据目录
        /// </summary>
        public static string[] GetAllInstanceDataPaths()
        {
            try
            {
                string basePath = GetInstancesPath();
                if (Directory.Exists(basePath))
                {
                    return Directory.GetDirectories(basePath);
                }
            }
            catch (Exception)
            {
            }
            return Array.Empty<string>();
        }
    }
}
