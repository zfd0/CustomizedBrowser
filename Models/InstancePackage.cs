// CustomizedBrowser\Models\InstancePackage.cs
using System;
using System.Collections.Generic;

namespace CustomizedBrowser.Models
{
    /// <summary>
    /// 实例包清单（manifest.json）
    /// 描述一个导出的实例包的基本信息
    /// </summary>
    public class InstancePackageManifest
    {
        /// <summary>
        /// 实例包格式版本
        /// </summary>
        public string FormatVersion { get; set; } = "1.0";

        /// <summary>
        /// 导出时的实例 ID
        /// </summary>
        public string InstanceId { get; set; } = string.Empty;

        /// <summary>
        /// 实例备注
        /// </summary>
        public string Note { get; set; } = string.Empty;

        /// <summary>
        /// 导出时间
        /// </summary>
        public DateTime ExportedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 导出时的应用程序版本
        /// </summary>
        public string AppVersion { get; set; } = string.Empty;

        /// <summary>
        /// 标签页数量
        /// </summary>
        public int TabCount { get; set; }

        /// <summary>
        /// Cookie 数量
        /// </summary>
        public int CookieCount { get; set; }

        /// <summary>
        /// 是否包含站点存储数据（Local Storage / IndexedDB 等）
        /// </summary>
        public bool IncludesStorage { get; set; }
    }

    /// <summary>
    /// Cookie 数据（cookies.json）
    /// 以明文形式保存，导入时由目标机器重新加密写入，从而绕过 DPAPI 的机器/用户绑定限制
    /// </summary>
    public class CookieData
    {
        /// <summary>
        /// Cookie 名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Cookie 值（明文）
        /// </summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// Cookie 所属域
        /// </summary>
        public string Domain { get; set; } = string.Empty;

        /// <summary>
        /// Cookie 路径
        /// </summary>
        public string Path { get; set; } = "/";

        /// <summary>
        /// 过期时间（DateTime.MinValue 表示会话 Cookie）
        /// </summary>
        public DateTime Expires { get; set; }

        /// <summary>
        /// 是否为 HttpOnly Cookie
        /// </summary>
        public bool IsHttpOnly { get; set; }

        /// <summary>
        /// 是否为 Secure Cookie
        /// </summary>
        public bool IsSecure { get; set; }

        /// <summary>
        /// 是否为会话 Cookie（关闭浏览器后失效）
        /// </summary>
        public bool IsSession { get; set; }

        /// <summary>
        /// SameSite 属性（None / Lax / Strict）
        /// </summary>
        public string SameSite { get; set; } = "None";
    }

    /// <summary>
    /// 实例导出结果
    /// </summary>
    public class InstanceExportResult
    {
        /// <summary>
        /// 是否导出成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 结果描述信息
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// 实例包文件路径
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// 导出的 Cookie 数量
        /// </summary>
        public int CookieCount { get; set; }

        /// <summary>
        /// 导出的标签页数量
        /// </summary>
        public int TabCount { get; set; }
    }

    /// <summary>
    /// 实例导入结果
    /// </summary>
    public class InstanceImportResult
    {
        /// <summary>
        /// 是否导入成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 结果描述信息
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// 实际导入后的实例 ID
        /// </summary>
        public string InstanceId { get; set; } = string.Empty;

        /// <summary>
        /// 写入的 Cookie 数量
        /// </summary>
        public int CookieCount { get; set; }
    }
}
