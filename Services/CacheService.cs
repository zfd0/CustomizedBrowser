// CustomizedBrowser\Services\CacheService.cs
using CustomizedBrowser.Core;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CustomizedBrowser.Services
{
    /// <summary>
    /// 缓存管理服务实现
    /// 从 History/Cookies 等 Chromium 数据库读取域名信息，
    /// 扫描实际缓存目录计算大小
    /// </summary>
    public class CacheService : ICacheService
    {
        // 实例根目录下的缓存子目录
        private static readonly string[] CacheSubDirs = new[]
        {
            Path.Combine("Cache", "Default"),
        };

        // EBWebView/Default 下的缓存相关子目录
        private static readonly string[] BrowsingDataSubDirs = new[]
        {
            Path.Combine("EBWebView", "Default", "GPUCache"),
            Path.Combine("EBWebView", "Default", "Local Storage"),
            Path.Combine("EBWebView", "Default", "Session Storage"),
            Path.Combine("EBWebView", "Default", "Network"),
            Path.Combine("EBWebView", "Default", "WebStorage"),
            Path.Combine("EBWebView", "Default", "SharedStorage"),
            Path.Combine("EBWebView", "Default", "Shared Dictionary"),
        };

        /// <summary>
        /// 获取实例数据目录路径
        /// </summary>
        public string GetInstanceDataDirectory(string instanceId)
        {
            return AppConstants.GetInstanceDataPath(instanceId);
        }

        /// <summary>
        /// 获取 EBWebView/Default 目录路径
        /// </summary>
        private string GetEBWebViewDefaultPath(string instanceId)
        {
            return Path.Combine(GetInstanceDataDirectory(instanceId), "EBWebView", "Default");
        }

        /// <summary>
        /// 获取指定实例的缓存总大小（字节）
        /// </summary>
        public async Task<long> GetTotalCacheSizeAsync(string instanceId)
        {
            return await Task.Run(() =>
            {
                string instanceDir = GetInstanceDataDirectory(instanceId);
                long totalSize = 0;

                // 计算 Cache/Default 目录大小
                foreach (string subDir in CacheSubDirs)
                {
                    totalSize += GetDirectorySize(Path.Combine(instanceDir, subDir));
                }

                // 计算 EBWebView/Default 下的缓存目录大小
                foreach (string subDir in BrowsingDataSubDirs)
                {
                    totalSize += GetDirectorySize(Path.Combine(instanceDir, subDir));
                }

                return totalSize;
            });
        }

        /// <summary>
        /// 获取指定实例的缓存域名列表
        /// 从 History 和 Cookies 数据库提取域名信息
        /// </summary>
        public async Task<List<CacheDomainInfo>> GetCacheDomainsAsync(string instanceId)
        {
            return await Task.Run(() =>
            {
                string ebwebViewPath = GetEBWebViewDefaultPath(instanceId);
                var domainDict = new Dictionary<string, CacheDomainInfo>(StringComparer.OrdinalIgnoreCase);

                // 1. 从 History 数据库读取访问过的域名
                string historyPath = Path.Combine(ebwebViewPath, "History");
                if (File.Exists(historyPath))
                {
                    LoadDomainsFromHistory(historyPath, domainDict);
                }

                // 2. 从 Cookies 数据库读取 Cookie 域名
                string cookiesPath = Path.Combine(ebwebViewPath, "Network", "Cookies");
                if (File.Exists(cookiesPath))
                {
                    LoadDomainsFromCookies(cookiesPath, domainDict);
                }

                // 按最后访问时间降序排列
                return domainDict.Values
                    .OrderByDescending(d => d.LastVisitTime)
                    .ToList();
            });
        }

        /// <summary>
        /// 从 History 数据库加载域名信息
        /// </summary>
        private void LoadDomainsFromHistory(string historyPath, Dictionary<string, CacheDomainInfo> domainDict)
        {
            try
            {
                // 使用共享只读模式，避免数据库被 Chromium 锁定时无法访问
                var connectionString = new SqliteConnectionStringBuilder
                {
                    DataSource = historyPath,
                    Mode = SqliteOpenMode.ReadOnly,
                    Cache = SqliteCacheMode.Shared,
                    // 限制等待数据库锁的时间，避免实例运行中数据库被占用时长时间无响应
                    DefaultTimeout = 3
                }.ToString();

                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                // 读取 urls 表：获取域名、访问次数、最后访问时间
                string sql = @"
                    SELECT url, visit_count, last_visit_time 
                    FROM urls 
                    WHERE last_visit_time > 0 
                    ORDER BY last_visit_time DESC";

                using var command = new SqliteCommand(sql, connection);
                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    string url = reader.GetString(0);
                    int visitCount = reader.GetInt32(1);
                    long chromeTimestamp = reader.GetInt64(2);

                    // 从 URL 中提取域名
                    string domain = ExtractDomainFromUrl(url);
                    if (string.IsNullOrEmpty(domain)) continue;

                    // 转换 Chrome 时间戳到 DateTime
                    DateTime visitTime = ChromeTimestampToDateTime(chromeTimestamp);

                    if (domainDict.TryGetValue(domain, out var existing))
                    {
                        existing.VisitCount += visitCount;
                        if (visitTime > existing.LastVisitTime)
                            existing.LastVisitTime = visitTime;
                    }
                    else
                    {
                        domainDict[domain] = new CacheDomainInfo
                        {
                            Domain = domain,
                            VisitCount = visitCount,
                            LastVisitTime = visitTime,
                            CookieCount = 0
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                // 数据库被锁定或无法访问时，静默失败
                System.Diagnostics.Debug.WriteLine($"读取 History 数据库失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从 Cookies 数据库加载域名信息
        /// </summary>
        private void LoadDomainsFromCookies(string cookiesPath, Dictionary<string, CacheDomainInfo> domainDict)
        {
            try
            {
                var connectionString = new SqliteConnectionStringBuilder
                {
                    DataSource = cookiesPath,
                    Mode = SqliteOpenMode.ReadOnly,
                    Cache = SqliteCacheMode.Shared,
                    // 限制等待数据库锁的时间，避免实例运行中数据库被占用时长时间无响应
                    DefaultTimeout = 3
                }.ToString();

                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                // 按域名分组统计 Cookie 数量
                string sql = @"
                    SELECT host_key, COUNT(*) as cnt 
                    FROM cookies 
                    GROUP BY host_key 
                    ORDER BY cnt DESC";

                using var command = new SqliteCommand(sql, connection);
                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    string hostKey = reader.GetString(0);
                    int cookieCount = reader.GetInt32(1);

                    string domain = hostKey.TrimStart('.');
                    if (string.IsNullOrEmpty(domain)) continue;

                    if (domainDict.TryGetValue(domain, out var existing))
                    {
                        existing.CookieCount = cookieCount;
                    }
                    else
                    {
                        domainDict[domain] = new CacheDomainInfo
                        {
                            Domain = domain,
                            VisitCount = 0,
                            LastVisitTime = DateTime.MinValue,
                            CookieCount = cookieCount
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"读取 Cookies 数据库失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 清除指定实例的所有缓存和浏览数据
        /// </summary>
        public async Task ClearAllCacheAsync(string instanceId)
        {
            await Task.Run(() =>
            {
                string instanceDir = GetInstanceDataDirectory(instanceId);

                // 清除 Cache/Default 目录（磁盘缓存 + 代码缓存）
                foreach (string subDir in CacheSubDirs)
                {
                    ClearDirectoryContent(Path.Combine(instanceDir, subDir));
                }

                // 清除 EBWebView/Default 下的缓存目录
                foreach (string subDir in BrowsingDataSubDirs)
                {
                    ClearDirectoryContent(Path.Combine(instanceDir, subDir));
                }

                // 清除其他可以用目录方式清除的浏览数据
                string ebwebviewPath = GetEBWebViewDefaultPath(instanceId);
                ClearDirectoryContent(Path.Combine(ebwebviewPath, "DawnGraphiteCache"));
                ClearDirectoryContent(Path.Combine(ebwebviewPath, "DawnWebGPUCache"));
                ClearDirectoryContent(Path.Combine(ebwebviewPath, "GrShaderCache"));
                ClearDirectoryContent(Path.Combine(ebwebviewPath, "ShaderCache"));
                ClearDirectoryContent(Path.Combine(ebwebviewPath, "blob_storage"));
                ClearDirectoryContent(Path.Combine(ebwebviewPath, "File System"));
            });
        }

        /// <summary>
        /// 清除指定实例中指定域名的缓存和浏览数据
        /// </summary>
        public async Task ClearDomainCacheAsync(string instanceId, string domain)
        {
            await Task.Run(() =>
            {
                string ebwebviewPath = GetEBWebViewDefaultPath(instanceId);

                // 1. 清除 History 数据库中该域名的记录
                string historyPath = Path.Combine(ebwebviewPath, "History");
                if (File.Exists(historyPath))
                {
                    DeleteDomainFromHistory(historyPath, domain);
                }

                // 2. 清除 Cookies 数据库中该域名的 Cookie
                string cookiesPath = Path.Combine(ebwebviewPath, "Network", "Cookies");
                if (File.Exists(cookiesPath))
                {
                    DeleteDomainFromCookies(cookiesPath, domain);
                }
            });
        }

        /// <summary>
        /// 从 History 数据库中删除指定域名的记录
        /// </summary>
        private void DeleteDomainFromHistory(string historyPath, string domain)
        {
            try
            {
                // 先复制一份，避免直接操作被锁定的数据库
                string tempPath = Path.GetTempFileName();
                File.Copy(historyPath, tempPath, true);

                var connectionString = new SqliteConnectionStringBuilder
                {
                    DataSource = tempPath,
                    Mode = SqliteOpenMode.ReadWrite
                }.ToString();

                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                // 删除匹配该域名的 URL 记录
                string pattern = $"%{domain}%";
                using var command = new SqliteCommand(
                    "DELETE FROM urls WHERE url LIKE @pattern", connection);
                command.Parameters.AddWithValue("@pattern", pattern);
                command.ExecuteNonQuery();

                connection.Close();

                // 用修改后的副本替换原文件
                File.Copy(tempPath, historyPath, true);
                File.Delete(tempPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清除 History 域名 {domain} 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从 Cookies 数据库中删除指定域名的 Cookie
        /// </summary>
        private void DeleteDomainFromCookies(string cookiesPath, string domain)
        {
            try
            {
                string tempPath = Path.GetTempFileName();
                File.Copy(cookiesPath, tempPath, true);

                var connectionString = new SqliteConnectionStringBuilder
                {
                    DataSource = tempPath,
                    Mode = SqliteOpenMode.ReadWrite
                }.ToString();

                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                // 删除匹配该域名的 Cookie
                using var command = new SqliteCommand(
                    "DELETE FROM cookies WHERE host_key = @domain OR host_key LIKE @pattern", connection);
                command.Parameters.AddWithValue("@domain", domain);
                command.Parameters.AddWithValue("@pattern", $"%{domain}");
                command.ExecuteNonQuery();

                connection.Close();

                File.Copy(tempPath, cookiesPath, true);
                File.Delete(tempPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清除 Cookies 域名 {domain} 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从 URL 中提取域名
        /// </summary>
        private string ExtractDomainFromUrl(string url)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url)) return string.Empty;

                // 处理没有 scheme 的 URL
                if (!url.Contains("://"))
                    url = "https://" + url;

                if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    string host = uri.Host.ToLowerInvariant();

                    // 过滤掉非标准域名（IP 地址、localhost 等）
                    if (host == "localhost" || host == "127.0.0.1" || host == "::1")
                        return string.Empty;

                    // 过滤掉没有点号的域名
                    if (!host.Contains('.'))
                        return string.Empty;

                    return host;
                }
            }
            catch
            {
                // 忽略无法解析的 URL
            }

            return string.Empty;
        }

        /// <summary>
        /// 将 Chrome 时间戳（微秒，从 1601-01-01 开始）转换为 DateTime
        /// </summary>
        private DateTime ChromeTimestampToDateTime(long chromeTimestamp)
        {
            try
            {
                // Chrome 时间戳单位是微秒，Windows FILETIME 单位是 100 纳秒
                long fileTime = chromeTimestamp * 10;
                return DateTime.FromFileTime(fileTime);
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        /// <summary>
        /// 计算目录大小
        /// </summary>
        private long GetDirectorySize(string directoryPath)
        {
            if (!Directory.Exists(directoryPath)) return 0;

            try
            {
                return Directory.GetFiles(directoryPath, "*.*", SearchOption.AllDirectories)
                    .Sum(f => { try { return new FileInfo(f).Length; } catch { return 0L; } });
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 清空目录内容（删除所有文件和子目录）
        /// </summary>
        private void ClearDirectoryContent(string dirPath)
        {
            if (!Directory.Exists(dirPath)) return;

            try
            {
                // 删除所有文件
                foreach (string file in Directory.GetFiles(dirPath, "*.*", SearchOption.AllDirectories))
                {
                    try { File.Delete(file); } catch { }
                }

                // 删除所有空目录（从最深层开始）
                var dirs = Directory.GetDirectories(dirPath, "*", SearchOption.AllDirectories)
                    .OrderByDescending(d => d.Length);
                foreach (string dir in dirs)
                {
                    try
                    {
                        if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                            Directory.Delete(dir, false);
                    }
                    catch { }
                }
            }
            catch { }
        }
    }
}