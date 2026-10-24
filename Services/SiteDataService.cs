// CustomizedBrowser\Services\SiteDataService.cs
using CustomizedBrowser.Core;
using CustomizedBrowser.Models;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace CustomizedBrowser.Services
{
    /// <summary>
    /// 站点数据服务实现
    /// 提供实例中「站点级」数据的读写原语，是提取网站/合并实例功能的底层支撑：
    /// - Cookie：通过隐藏窗口的 CookieManager 明文读写（由本机密钥重新加密），
    ///   Cookie 会缓存在浏览器进程内存中，因此读取运行中的实例需复用其环境才能拿到最新值；
    /// - 站点存储：IndexedDB / WebStorage 目录按来源（origin）分目录存放，可按网站整体复制；
    ///   Local Storage 则是整个配置文件共享的单体库，无法按网站复制目录，
    ///   需通过隐藏窗口访问站点后用页面脚本按来源读取/写入/清空
    /// </summary>
    public class SiteDataService : ISiteDataService
    {
        /// <summary>会话 Cookie 写入时转换为持久 Cookie 的有效期（天）</summary>
        private const int SessionCookiePersistDays = 30;

        /// <summary>导航到站点并等待加载完成的最长时间（毫秒）</summary>
        private const int NavigationTimeoutMs = 20000;

        /// <summary>Cookie 操作后等待数据落盘的时间（毫秒）</summary>
        private const int CookieFlushDelayMs = 1500;

        /// <summary>按来源（origin）分目录存储、可按网站整体复制的存储子目录名称</summary>
        private static readonly string[] OriginScopedStorageFolderNames = { "IndexedDB", "WebStorage" };

        /// <summary>来源目录名称的后缀（例如 https_chat.deepseek.com_0.indexeddb.leveldb）</summary>
        private static readonly string[] OriginFolderSuffixes = { ".indexeddb.leveldb", ".indexeddb.blob" };

        /// <summary>读取 localStorage 的页面脚本（返回内容为 JSON 字符串，失败时返回空串）</summary>
        private const string LocalStorageReadScript =
            "(function(){try{var o={};for(var i=0;i<localStorage.length;i++){var k=localStorage.key(i);o[k]=localStorage.getItem(k);}return JSON.stringify(o);}catch(e){return '';}})()";

        /// <summary>写入 localStorage 的页面脚本模板（__DATA__ 替换为 JSON 数据）</summary>
        private const string LocalStorageWriteScriptTemplate =
            "(function(){try{var d=__DATA__;for(var k in d){localStorage.setItem(k,d[k]);}return 'ok';}catch(e){return 'err:'+e.message;}})()";

        /// <summary>清空 localStorage 的页面脚本</summary>
        private const string LocalStorageClearScript =
            "(function(){try{localStorage.clear();return 'ok';}catch(e){return 'err:'+e.message;}})()";

        private readonly IWebViewEnvironmentManager _environmentManager;

        /// <summary>
        /// 构造函数
        /// </summary>
        public SiteDataService(IWebViewEnvironmentManager environmentManager)
        {
            _environmentManager = environmentManager;
        }

        /// <summary>
        /// 读取实例的全部 Cookie
        /// 优先复用实例已缓存的环境（实例运行时可读到浏览器进程内存中的实时 Cookie），
        /// 环境失效（例如实例刚被停止）时自动回退为重建环境后再读取
        /// </summary>
        public Task<List<CookieData>> ReadCookiesAsync(string instanceId)
        {
            return WithReadHostAsync(instanceId, async host =>
            {
                // uri 传 null 表示获取该实例配置文件下的全部 Cookie
                var cookies = await host.Core!.CookieManager.GetCookiesAsync(null!);
                var list = new List<CookieData>(cookies.Count);
                foreach (var cookie in cookies)
                {
                    list.Add(new CookieData
                    {
                        Name = cookie.Name,
                        Value = cookie.Value,
                        Domain = cookie.Domain,
                        Path = cookie.Path,
                        Expires = cookie.Expires,
                        IsHttpOnly = cookie.IsHttpOnly,
                        IsSecure = cookie.IsSecure,
                        IsSession = cookie.IsSession,
                        SameSite = cookie.SameSite.ToString()
                    });
                }
                return list;
            });
        }

        /// <summary>
        /// 将 Cookie 写入实例（同名 Cookie 会被覆盖，由 WebView2 自动用本机密钥重新加密）
        /// 会话 Cookie 会被转换为持久 Cookie，否则关闭浏览器进程后即失效
        /// </summary>
        public async Task WriteCookiesAsync(string instanceId, IEnumerable<CookieData> cookies)
        {
            var list = cookies?.ToList() ?? new List<CookieData>();
            if (list.Count == 0)
            {
                return;
            }

            await WithHostAsync(instanceId, true, async host =>
            {
                var cookieManager = host.Core!.CookieManager;
                foreach (var data in list)
                {
                    try
                    {
                        var cookie = cookieManager.CreateCookie(
                            data.Name,
                            data.Value,
                            data.Domain,
                            string.IsNullOrEmpty(data.Path) ? "/" : data.Path);

                        cookie.IsHttpOnly = data.IsHttpOnly;
                        cookie.IsSecure = data.IsSecure;
                        cookie.SameSite = ParseSameSite(data.SameSite);

                        // 会话 Cookie 无法持久化，转换为带有效期的持久 Cookie
                        cookie.Expires = data.IsSession || data.Expires <= DateTime.MinValue
                            ? DateTime.Now.AddDays(SessionCookiePersistDays)
                            : data.Expires;

                        cookieManager.AddOrUpdateCookie(cookie);
                    }
                    catch (Exception)
                    {
                        // 单条 Cookie 写入失败不影响其余 Cookie
                    }
                }

                // 等待 Cookie 存储写入磁盘
                await Task.Delay(CookieFlushDelayMs);
                return true;
            });
        }

        /// <summary>
        /// 从实例中删除指定 Cookie（按名称 + 域 + 路径在现有 Cookie 中定位后删除）
        /// </summary>
        public async Task DeleteCookiesAsync(string instanceId, IEnumerable<CookieData> cookies)
        {
            var list = cookies?.ToList() ?? new List<CookieData>();
            if (list.Count == 0)
            {
                return;
            }

            await WithHostAsync(instanceId, true, async host =>
            {
                var cookieManager = host.Core!.CookieManager;
                var existing = await cookieManager.GetCookiesAsync(null!);
                foreach (var data in list)
                {
                    var matched = existing.Where(c =>
                        string.Equals(c.Name, data.Name, StringComparison.Ordinal) &&
                        string.Equals(c.Domain, data.Domain, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(c.Path, data.Path, StringComparison.Ordinal)).ToList();

                    foreach (var cookie in matched)
                    {
                        try
                        {
                            cookieManager.DeleteCookie(cookie);
                        }
                        catch (Exception)
                        {
                            // 单条 Cookie 删除失败不影响其余 Cookie
                        }
                    }
                }

                // 等待 Cookie 存储写入磁盘
                await Task.Delay(CookieFlushDelayMs);
                return true;
            });
        }

        /// <summary>
        /// 枚举实例中带站点存储（IndexedDB / WebStorage）的来源地址
        /// </summary>
        public List<string> GetStorageOrigins(string instanceId)
        {
            var origins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string profilePath = AppConstants.GetWebViewDefaultProfilePath(instanceId);

            foreach (var folderName in OriginScopedStorageFolderNames)
            {
                string root = Path.Combine(profilePath, folderName);
                if (!Directory.Exists(root))
                {
                    continue;
                }

                try
                {
                    foreach (var dir in Directory.GetDirectories(root))
                    {
                        string? origin = TryGetOriginFromFolderName(Path.GetFileName(dir));
                        if (origin != null)
                        {
                            origins.Add(origin);
                        }
                    }
                }
                catch (Exception)
                {
                    // 目录不可读时跳过
                }
            }

            return origins.ToList();
        }

        /// <summary>
        /// 复制指定网站的 IndexedDB / WebStorage 存储目录（目标实例需处于停止状态）
        /// 目标中已有的同名目录会先被删除，保证复制结果为来源的完整数据
        /// </summary>
        /// <param name="exactHostOnly">
        /// true 时只匹配主机名完全相同的来源目录；合并场景使用该模式，
        /// 避免父域网站的复制步骤误把子域网站的目录覆盖成非归属来源的数据
        /// </param>
        public List<string> CopyStorageForHosts(
            string sourceInstanceId, string targetInstanceId, IEnumerable<string> hosts, bool exactHostOnly = false)
        {
            var warnings = new List<string>();
            var hostList = NormalizeHosts(hosts);
            if (hostList.Count == 0)
            {
                return warnings;
            }

            string sourceProfile = AppConstants.GetWebViewDefaultProfilePath(sourceInstanceId);
            string targetProfile = AppConstants.GetWebViewDefaultProfilePath(targetInstanceId);

            foreach (var folderName in OriginScopedStorageFolderNames)
            {
                foreach (var dir in EnumerateMatchingStorageDirs(sourceProfile, folderName, hostList, exactHostOnly))
               
                {
                    string destDir = Path.Combine(targetProfile, folderName, Path.GetFileName(dir));
                    try
                    {
                        TryDeleteDirectory(destDir);
                        CopyDirectory(dir, destDir);
                    }
                    catch (Exception ex)
                    {
                        warnings.Add($"站点存储复制失败（{Path.GetFileName(dir)}）：{ex.Message}");
                    }
                }
            }

            return warnings;
        }

        /// <summary>
        /// 删除指定网站的 IndexedDB / WebStorage 存储目录（实例需处于停止状态）
        /// </summary>
        /// <param name="exactHostOnly">
        /// true 时只匹配主机名完全相同的来源目录；合并场景使用该模式，
        /// 避免清理父域网站时误删未被合并的子域网站数据
        /// </param>
        public List<string> DeleteStorageForHosts(string instanceId, IEnumerable<string> hosts, bool exactHostOnly = false)
        {
            var warnings = new List<string>();
            var hostList = NormalizeHosts(hosts);
            if (hostList.Count == 0)
            {
                return warnings;
            }

            string profilePath = AppConstants.GetWebViewDefaultProfilePath(instanceId);

            foreach (var folderName in OriginScopedStorageFolderNames)
            {
                foreach (var dir in EnumerateMatchingStorageDirs(profilePath, folderName, hostList, exactHostOnly))
                {
                    try
                    {
                        TryDeleteDirectory(dir);
                    }
                    catch (Exception ex)
                    {
                        warnings.Add($"站点存储删除失败（{Path.GetFileName(dir)}）：{ex.Message}");
                    }
                }
            }

            return warnings;
        }

        /// <summary>
        /// 批量读取指定来源的 Local Storage 内容
        /// 通过隐藏窗口依次访问各来源，由页面脚本读取；读取为空的来源不会出现在结果中
        /// </summary>
        public Task<Dictionary<string, Dictionary<string, string>>> ReadLocalStorageBatchAsync(
            string instanceId, IEnumerable<string> origins, List<string> warnings)
        {
            var originList = NormalizeOrigins(origins);
            var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            if (originList.Count == 0)
            {
                return Task.FromResult(result);
            }

            return WithReadHostAsync(instanceId, async host =>
            {
                int skippedCount = 0;
                var skippedExamples = new List<string>();

                foreach (var origin in originList)
                {
                    var read = await NavigateAndReadLocalStorageAsync(host, origin, warnings);

                    if (read.Status == StorageNavigationStatus.Redirected)
                    {
                        // 跨源跳转已由读取方法单独记录警告（含实际跳转地址），不再计入“无法访问”汇总
                        continue;
                    }

                    if (read.Status == StorageNavigationStatus.Failed)
                    {
                        // 汇总统计无法访问的来源，避免逐条提示把结果信息淹没
                        skippedCount++;
                        if (skippedExamples.Count < 5)
                        {
                            skippedExamples.Add(origin);
                        }
                        continue;
                    }

                    if (read.Data != null && read.Data.Count > 0)
                    {
                        result[origin] = read.Data;
                    }
                }

                if (skippedCount > 0)
                {
                    string examples = string.Join("、", skippedExamples);
                    warnings.Add($"{skippedCount} 个站点无法访问，已跳过其 Local Storage 数据（例如：{examples}）。");
                }

                return result;
            });
        }

        /// <summary>
        /// 批量写入 Local Storage 内容（逐来源访问站点后由页面脚本写入）
        /// </summary>
        public async Task WriteLocalStorageBatchAsync(
            string instanceId, IDictionary<string, Dictionary<string, string>> data, List<string> warnings)
        {
            if (data == null || data.Count == 0)
            {
                return;
            }

            await WithHostAsync(instanceId, true, async host =>
            {
                foreach (var pair in data)
                {
                    if (pair.Value == null || pair.Value.Count == 0)
                    {
                        continue;
                    }

                    await NavigateAndWriteLocalStorageAsync(host, pair.Key, pair.Value, warnings);
                }

                return true;
            });
        }

        /// <summary>
        /// 批量清空指定来源的 Local Storage
        /// </summary>
        public async Task ClearLocalStorageBatchAsync(string instanceId, IEnumerable<string> origins, List<string> warnings)
        {
            var originList = NormalizeOrigins(origins);
            if (originList.Count == 0)
            {
                return;
            }

            await WithHostAsync(instanceId, true, async host =>
            {
                foreach (var origin in originList)
                {
                    await NavigateAndClearLocalStorageAsync(host, origin, warnings);
                }

                return true;
            });
        }

        /// <summary>
        /// 在同一个隐藏会话中批量「先清空再写入」Local Storage
        /// 清空与写入连续完成，避免两次打开浏览器进程导致清空结果未落盘、旧数据与新数据混合
        /// </summary>
        public async Task ReplaceLocalStorageBatchAsync(
            string instanceId, IEnumerable<string> originsToClear,
            IDictionary<string, Dictionary<string, string>> data, List<string> warnings)
        {
            var clearOrigins = NormalizeOrigins(originsToClear);
            var writeData = data ?? new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            var originList = new List<string>(clearOrigins);
            foreach (var origin in writeData.Keys)
            {
                if (!originList.Contains(origin, StringComparer.OrdinalIgnoreCase))
                {
                    originList.Add(origin);
                }
            }

            if (originList.Count == 0)
            {
                return;
            }

            await WithHostAsync(instanceId, true, async host =>
            {
                foreach (var origin in originList)
                {
                    bool clear = clearOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase);
                    writeData.TryGetValue(origin, out var values);
                    await NavigateAndReplaceLocalStorageAsync(host, origin, clear, values, warnings);
                }

                return true;
            });
        }

        /// <summary>
        /// 判断 Cookie 域或来源主机是否属于指定网站（含父子域双向匹配）
        /// </summary>
        public bool IsHostMatch(string domainOrHost, string host)
        {
            return HostMatches(domainOrHost, host);
        }

        /// <summary>
        /// 从域名或来源地址中取出主机名（去掉协议、端口与前导点）
        /// </summary>
        public string GetHost(string domainOrOrigin)
        {
            return GetHostName(domainOrOrigin);
        }

        /// <summary>
        /// 在隐藏窗口宿主中执行操作
        /// </summary>
        /// <param name="instanceId">实例 ID</param>
        /// <param name="fresh">是否强制重建环境（写入/删除前使用，避免复用已失效的缓存环境）</param>
        /// <param name="action">操作委托</param>
        private async Task<T> WithHostAsync<T>(string instanceId, bool fresh, Func<CookieHostWindow, Task<T>> action)
        {
            CoreWebView2Environment environment = fresh
                ? await GetFreshEnvironmentAsync(instanceId)
                : await _environmentManager.GetOrCreateEnvironmentAsync(instanceId);

            var host = new CookieHostWindow();
            try
            {
                await host.InitializeAsync(environment);
                return await action(host);
            }
            finally
            {
                host.Shutdown();
            }
        }

        /// <summary>
        /// 只读场景下在隐藏窗口宿主中执行操作
        /// 优先复用缓存环境（可读取运行中实例内存里的实时数据），失败时回退为重建环境
        /// </summary>
        private async Task<T> WithReadHostAsync<T>(string instanceId, Func<CookieHostWindow, Task<T>> action)
        {
            try
            {
                return await WithHostAsync(instanceId, false, action);
            }
            catch (Exception)
            {
                return await WithHostAsync(instanceId, true, action);
            }
        }

        /// <summary>
        /// 获取实例的全新 WebView2 环境
        /// 先移除可能已失效的缓存环境，再重新创建，避免在已失效的环境上创建控制器
        /// </summary>
        private async Task<CoreWebView2Environment> GetFreshEnvironmentAsync(string instanceId)
        {
            _environmentManager.RemoveEnvironment(instanceId);
            return await _environmentManager.GetOrCreateEnvironmentAsync(instanceId);
        }

        /// <summary>
        /// 站点存储导航结果
        /// 用于区分“导航失败”与“导航成功但发生跨源跳转”两种情况
        /// </summary>
        private enum StorageNavigationStatus
        {
            /// <summary>成功停留在目标来源</summary>
            Success,

            /// <summary>导航失败（超时、无法访问等）</summary>
            Failed,

            /// <summary>导航成功但跳转到了其他来源</summary>
            Redirected
        }

        /// <summary>
        /// 导航到目标来源并校验未发生跨源跳转
        /// 导航失败与跨源跳转均不在此处写警告，由调用方按场景分别处理
        /// </summary>
        /// <param name="host">隐藏窗口宿主</param>
        /// <param name="origin">目标来源地址</param>
        /// <returns>导航状态；发生跨源跳转时同时返回实际停留的地址</returns>
        private static async Task<(StorageNavigationStatus Status, string ActualUrl)> NavigateForStorageAsync(
            CookieHostWindow host, string origin)
        {
            bool success = await host.NavigateAndWaitAsync(origin, NavigationTimeoutMs);
            if (!success)
            {
                return (StorageNavigationStatus.Failed, string.Empty);
            }

            string current = host.Core?.Source ?? string.Empty;
            return IsSameOrigin(current, origin)
                ? (StorageNavigationStatus.Success, current)
                : (StorageNavigationStatus.Redirected, current);
        }

        /// <summary>
        /// 导航到指定来源并读取其 Local Storage 内容
        /// </summary>
        /// <returns>导航状态与键值对字典；失败、跨源跳转或读取异常时 Data 为 null</returns>
        private static async Task<(StorageNavigationStatus Status, Dictionary<string, string>? Data)>
            NavigateAndReadLocalStorageAsync(CookieHostWindow host, string origin, List<string> warnings)
        {
            var navigation = await NavigateForStorageAsync(host, origin);

            if (navigation.Status == StorageNavigationStatus.Redirected)
            {
                // 跨源跳转说明当前页面不再是目标网站，此时读取的会是错误站点的数据，需明确告知实际地址
                warnings?.Add($"{origin} 实际跳转到了 {navigation.ActualUrl}，已跳过其 Local Storage 数据。");
                return (StorageNavigationStatus.Redirected, null);
            }

            if (navigation.Status == StorageNavigationStatus.Failed)
            {
                return (StorageNavigationStatus.Failed, null);
            }

            try
            {
                string raw = await host.Core!.ExecuteScriptAsync(LocalStorageReadScript);

                // ExecuteScriptAsync 返回的是脚本结果值的 JSON 表示，需先解出内层 JSON 字符串
                string? json = JsonSerializer.Deserialize<string>(raw);
                if (string.IsNullOrEmpty(json) || json == "{}")
                {
                    return (StorageNavigationStatus.Success, new Dictionary<string, string>());
                }

                return (StorageNavigationStatus.Success,
                    JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                    ?? new Dictionary<string, string>());
            }
            catch (Exception ex)
            {
                warnings?.Add($"读取 {origin} 的 Local Storage 失败：{ex.Message}");
                return (StorageNavigationStatus.Success, null);
            }
        }

        /// <summary>
        /// 导航到指定来源并写入其 Local Storage 内容
        /// </summary>
        private static async Task NavigateAndWriteLocalStorageAsync(
            CookieHostWindow host, string origin, Dictionary<string, string> data, List<string> warnings)
        {
            var navigation = await NavigateForStorageAsync(host, origin);
            if (navigation.Status != StorageNavigationStatus.Success)
            {
                warnings?.Add(navigation.Status == StorageNavigationStatus.Redirected
                    ? $"{origin} 实际跳转到了 {navigation.ActualUrl}，未写入其 Local Storage 数据。"
                    : $"无法访问 {origin}，未写入其 Local Storage 数据。");
                return;
            }

            try
            {
                string payload = JsonSerializer.Serialize(data);
                string script = LocalStorageWriteScriptTemplate.Replace("__DATA__", payload);
                string raw = await host.Core!.ExecuteScriptAsync(script);
                string? result = JsonSerializer.Deserialize<string>(raw);
                if (result != "ok")
                {
                    warnings?.Add($"{origin} 的 Local Storage 写入未完全成功（{result}）。");
                }
            }
            catch (Exception ex)
            {
                warnings?.Add($"写入 {origin} 的 Local Storage 失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 导航到指定来源并清空其 Local Storage
        /// </summary>
        private static async Task NavigateAndClearLocalStorageAsync(
            CookieHostWindow host, string origin, List<string> warnings)
        {
            var navigation = await NavigateForStorageAsync(host, origin);
            if (navigation.Status != StorageNavigationStatus.Success)
            {
                warnings?.Add(navigation.Status == StorageNavigationStatus.Redirected
                    ? $"{origin} 实际跳转到了 {navigation.ActualUrl}，未清空其 Local Storage 数据。"
                    : $"无法访问 {origin}，未清空其 Local Storage 数据。");
                return;
            }

            try
            {
                string raw = await host.Core!.ExecuteScriptAsync(LocalStorageClearScript);
                string? result = JsonSerializer.Deserialize<string>(raw);
                if (result != "ok")
                {
                    warnings?.Add($"{origin} 的 Local Storage 清空未完全成功（{result}）。");
                }
            }
            catch (Exception ex)
            {
                warnings?.Add($"清空 {origin} 的 Local Storage 失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 导航到指定来源并「先清空再写入」其 Local Storage（单次导航内完成，保证覆盖语义）
        /// </summary>
        /// <param name="clear">是否需要先清空该来源的旧数据</param>
        /// <param name="data">需要写入的键值对；为空时只清空不写入</param>
        private static async Task NavigateAndReplaceLocalStorageAsync(
            CookieHostWindow host, string origin, bool clear, Dictionary<string, string>? data, List<string> warnings)
        {
            var navigation = await NavigateForStorageAsync(host, origin);
            if (navigation.Status != StorageNavigationStatus.Success)
            {
                warnings?.Add(navigation.Status == StorageNavigationStatus.Redirected
                    ? $"{origin} 实际跳转到了 {navigation.ActualUrl}，未更新其 Local Storage 数据。"
                    : $"无法访问 {origin}，未更新其 Local Storage 数据。");
                return;
            }

            try
            {
                if (clear)
                {
                    string clearRaw = await host.Core!.ExecuteScriptAsync(LocalStorageClearScript);
                    string? clearResult = JsonSerializer.Deserialize<string>(clearRaw);
                    if (clearResult != "ok")
                    {
                        warnings?.Add($"{origin} 的 Local Storage 清空未完全成功（{clearResult}）。");
                    }
                }

                if (data != null && data.Count > 0)
                {
                    string payload = JsonSerializer.Serialize(data);
                    string script = LocalStorageWriteScriptTemplate.Replace("__DATA__", payload);
                    string raw = await host.Core!.ExecuteScriptAsync(script);
                    string? result = JsonSerializer.Deserialize<string>(raw);
                    if (result != "ok")
                    {
                        warnings?.Add($"{origin} 的 Local Storage 写入未完全成功（{result}）。");
                    }
                }
            }
            catch (Exception ex)
            {
                warnings?.Add($"更新 {origin} 的 Local Storage 失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 枚举某个存储子目录下匹配指定网站的子目录
        /// </summary>
        /// <param name="exactHostOnly">true 时只匹配主机名完全相同的来源，false 时含父子域匹配</param>
        private static List<string> EnumerateMatchingStorageDirs(
            string profilePath, string folderName, List<string> hosts, bool exactHostOnly)
        {
            var result = new List<string>();
            string root = Path.Combine(profilePath, folderName);
            if (!Directory.Exists(root))
            {
                return result;
            }

            try
            {
                foreach (var dir in Directory.GetDirectories(root))
                {
                    string? origin = TryGetOriginFromFolderName(Path.GetFileName(dir));
                    if (origin == null)
                    {
                        continue;
                    }

                    if (IsOriginMatched(origin, hosts, exactHostOnly))
                    {
                        result.Add(dir);
                    }
                }
            }
            catch (Exception)
            {
                // 目录不可读时跳过
            }

            return result;
        }

        /// <summary>
        /// 判断来源地址是否匹配网站列表
        /// </summary>
        /// <param name="exactHostOnly">true 时仅主机名完全相同才算匹配</param>
        private static bool IsOriginMatched(string origin, List<string> hosts, bool exactHostOnly)
        {
            if (!exactHostOnly)
            {
                return hosts.Any(h => HostMatches(origin, h));
            }

            string originHost = GetHostName(origin);
            return hosts.Any(h => string.Equals(originHost, GetHostName(h), StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 从存储目录名称解析来源地址
        /// 目录名形如 https_chat.deepseek.com_0（末尾数字为端口，0 表示默认端口）
        /// </summary>
        /// <returns>来源地址；无法解析时返回 null</returns>
        private static string? TryGetOriginFromFolderName(string folderName)
        {
            if (string.IsNullOrWhiteSpace(folderName))
            {
                return null;
            }

            string name = folderName;
            foreach (var suffix in OriginFolderSuffixes)
            {
                if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    name = name.Substring(0, name.Length - suffix.Length);
                    break;
                }
            }

            int schemeSeparator = name.IndexOf('_');
            if (schemeSeparator <= 0)
            {
                return null;
            }

            string scheme = name.Substring(0, schemeSeparator);
            if (!string.Equals(scheme, "http", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(scheme, "https", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string rest = name.Substring(schemeSeparator + 1);
            int port = 0;
            int portSeparator = rest.LastIndexOf('_');
            if (portSeparator > 0)
            {
                string portPart = rest.Substring(portSeparator + 1);
                if (portPart.Length > 0 && portPart.All(char.IsDigit))
                {
                    int.TryParse(portPart, out port);
                    rest = rest.Substring(0, portSeparator);
                }
            }

            if (rest.Length == 0)
            {
                return null;
            }

            return port == 0
                ? $"{scheme.ToLowerInvariant()}://{rest}"
                : $"{scheme.ToLowerInvariant()}://{rest}:{port}";
        }

        /// <summary>
        /// 判断两个地址是否同源（协议、主机、端口均一致）
        /// </summary>
        private static bool IsSameOrigin(string url, string origin)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var current) &&
                Uri.TryCreate(origin, UriKind.Absolute, out var target))
            {
                return string.Equals(current.Scheme, target.Scheme, StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(current.Host, target.Host, StringComparison.OrdinalIgnoreCase) &&
                       current.Port == target.Port;
            }

            return false;
        }

        /// <summary>
        /// 主机匹配判断（含父子域双向匹配）
        /// 例如 Cookie 域 .deepseek.com 与网站 chat.deepseek.com 视为同一网站
        /// </summary>
        private static bool HostMatches(string domainOrHost, string host)
        {
            string a = GetHostName(domainOrHost);
            string b = GetHostName(host);
            if (a.Length == 0 || b.Length == 0)
            {
                return false;
            }

            if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // IP 地址不做父子域匹配，仅支持完全相同
            if (IsIpAddress(a) || IsIpAddress(b))
            {
                return false;
            }

            return a.EndsWith("." + b, StringComparison.OrdinalIgnoreCase) ||
                   b.EndsWith("." + a, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 规范化为小写主机名（去掉协议、端口、路径与前导点）
        /// </summary>
        private static string GetHostName(string domainOrOrigin)
        {
            if (string.IsNullOrWhiteSpace(domainOrOrigin))
            {
                return string.Empty;
            }

            string value = domainOrOrigin.Trim();

            int schemeIndex = value.IndexOf("://", StringComparison.Ordinal);
            if (schemeIndex >= 0)
            {
                value = value.Substring(schemeIndex + 3);
            }

            int pathIndex = value.IndexOfAny(new[] { '/', '?', '#' });
            if (pathIndex >= 0)
            {
                value = value.Substring(0, pathIndex);
            }

            // IPv6 地址形如 [::1]:8080
            if (value.StartsWith("[", StringComparison.Ordinal))
            {
                int end = value.IndexOf(']');
                if (end > 0)
                {
                    return value.Substring(1, end - 1).ToLowerInvariant();
                }
            }

            int colonIndex = value.LastIndexOf(':');
            if (colonIndex > 0)
            {
                string portPart = value.Substring(colonIndex + 1);
                if (portPart.Length > 0 && portPart.All(char.IsDigit))
                {
                    value = value.Substring(0, colonIndex);
                }
            }

            return value.TrimStart('.').ToLowerInvariant();
        }

        /// <summary>
        /// 是否为 IP 地址（IPv4）
        /// </summary>
        private static bool IsIpAddress(string host)
        {
            return host.Length > 0 && host.All(c => char.IsDigit(c) || c == '.');
        }

        /// <summary>
        /// 规范网站主机名列表（去重、去空）
        /// </summary>
        private static List<string> NormalizeHosts(IEnumerable<string> hosts)
        {
            var result = new List<string>();
            if (hosts == null)
            {
                return result;
            }

            foreach (var host in hosts)
            {
                if (string.IsNullOrWhiteSpace(host))
                {
                    continue;
                }

                string value = host.Trim();
                if (!result.Contains(value, StringComparer.OrdinalIgnoreCase))
                {
                    result.Add(value);
                }
            }

            return result;
        }

        /// <summary>
        /// 规范来源地址列表（去重、去空，缺少协议时默认补 https）
        /// </summary>
        private static List<string> NormalizeOrigins(IEnumerable<string> origins)
        {
            var result = new List<string>();
            if (origins == null)
            {
                return result;
            }

            foreach (var origin in origins)
            {
                if (string.IsNullOrWhiteSpace(origin))
                {
                    continue;
                }

                string value = origin.Trim();
                if (value.IndexOf("://", StringComparison.Ordinal) < 0)
                {
                    value = "https://" + value;
                }

                if (!result.Contains(value, StringComparer.OrdinalIgnoreCase))
                {
                    result.Add(value);
                }
            }

            return result;
        }

        /// <summary>
        /// 解析 SameSite 字符串为 WebView2 枚举
        /// </summary>
        private static CoreWebView2CookieSameSiteKind ParseSameSite(string sameSite)
        {
            return Enum.TryParse<CoreWebView2CookieSameSiteKind>(sameSite, true, out var kind)
                ? kind
                : CoreWebView2CookieSameSiteKind.None;
        }

        /// <summary>
        /// 递归复制目录（跳过 LevelDB 锁文件）
        /// </summary>
        private static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                if (string.Equals(Path.GetFileName(file), "LOCK", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
            }
        }

        /// <summary>
        /// 尝试删除目录（失败时忽略）
        /// </summary>
        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch (Exception)
            {
            }
        }
    }
}