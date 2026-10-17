// CustomizedBrowser\Services\ICacheService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CustomizedBrowser.Services
{
    /// <summary>
    /// 缓存域名信息
    /// </summary>
    public class CacheDomainInfo
    {
        /// <summary>
        /// 域名
        /// </summary>
        public string Domain { get; set; } = string.Empty;

        /// <summary>
        /// 访问次数（来自 History 数据库）
        /// </summary>
        public int VisitCount { get; set; }

        /// <summary>
        /// 最后访问时间
        /// </summary>
        public DateTime LastVisitTime { get; set; }

        /// <summary>
        /// Cookie 数量
        /// </summary>
        public int CookieCount { get; set; }

        /// <summary>
        /// 是否有 Cookie
        /// </summary>
        public bool HasCookies => CookieCount > 0;

        /// <summary>
        /// 是否有缓存
        /// </summary>
        public bool HasCache => VisitCount > 0 || CookieCount > 0;
    }

    /// <summary>
    /// 缓存管理服务接口
    /// 负责各实例的缓存管理和清除
    /// </summary>
    public interface ICacheService
    {
        /// <summary>
        /// 获取指定实例的缓存总大小（字节），包括所有缓存类型
        /// </summary>
        Task<long> GetTotalCacheSizeAsync(string instanceId);

        /// <summary>
        /// 获取指定实例的缓存域名列表（从 History 和 Cookies 数据库提取）
        /// </summary>
        Task<List<CacheDomainInfo>> GetCacheDomainsAsync(string instanceId);

        /// <summary>
        /// 清除指定实例的所有缓存和浏览数据
        /// </summary>
        Task ClearAllCacheAsync(string instanceId);

        /// <summary>
        /// 清除指定实例中指定域名的缓存和浏览数据
        /// </summary>
        Task ClearDomainCacheAsync(string instanceId, string domain);

        /// <summary>
        /// 获取实例数据目录路径
        /// </summary>
        string GetInstanceDataDirectory(string instanceId);
    }
}