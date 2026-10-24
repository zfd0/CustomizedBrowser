// CustomizedBrowser\Services\SiteTransferService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CustomizedBrowser.Models;

namespace CustomizedBrowser.Services
{
    /// <summary>
    /// 网站数据迁移服务实现
    /// 负责两类操作：
    /// 1. 提取网站：把实例中指定网站的登录状态（Cookie + 站点存储）独立到新实例，可选从源实例移除
    /// 2. 合并实例：把多个实例的网站数据合并到目标实例，同一网站存在于多个来源时由用户选择保留哪个
    /// 关键时序：读取运行中实例的实时数据（必须在此之前不停止实例）→ 停止实例释放文件占用 →
    /// 复制/删除站点存储目录（必须在创建目标实例的 WebView2 环境之前）→ 写入 Cookie 与 Local Storage
    /// </summary>
    public class SiteTransferService : ISiteTransferService
    {
        private readonly IInstanceService _instanceService;
        private readonly ISiteDataService _siteData;

        /// <summary>
        /// 构造函数
        /// </summary>
        public SiteTransferService(IInstanceService instanceService, ISiteDataService siteData)
        {
            _instanceService = instanceService;
            _siteData = siteData;
        }

        /// <summary>
        /// 分析实例中的网站列表（来源于 Cookie 域与站点存储来源）
        /// </summary>
        public async Task<List<SiteEntry>> GetSitesAsync(string instanceId)
        {
            var cookies = await _siteData.ReadCookiesAsync(instanceId);
            var origins = _siteData.GetStorageOrigins(instanceId);

            var map = new Dictionary<string, SiteEntry>(StringComparer.OrdinalIgnoreCase);

            foreach (var cookie in cookies)
            {
                string host = _siteData.GetHost(cookie.Domain);
                if (host.Length == 0)
                {
                    continue;
                }

                var entry = GetOrCreateSiteEntry(map, host);
                entry.CookieCount++;
            }

            foreach (var origin in origins)
            {
                string host = _siteData.GetHost(origin);
                if (host.Length == 0)
                {
                    continue;
                }

                GetOrCreateSiteEntry(map, host).HasStorage = true;
            }

            return map.Values
                .OrderByDescending(e => e.CookieCount)
                .ThenBy(e => e.Host, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// 将源实例中指定网站的登录状态提取到新实例
        /// </summary>
        public async Task<SiteTransferResult> ExtractAsync(
            string sourceInstanceId, List<string> hosts, string targetInstanceId, bool removeFromSource)
        {
            var result = new SiteTransferResult { TargetInstanceId = targetInstanceId };

            if (hosts == null || hosts.Count == 0)
            {
                result.Message = "未选择任何网站。";
                return result;
            }

            if (!_instanceService.InstanceExists(sourceInstanceId))
            {
                result.Message = $"源实例 \"{sourceInstanceId}\" 不存在。";
                return result;
            }

            if (string.IsNullOrWhiteSpace(targetInstanceId) ||
                targetInstanceId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                result.Message = "新实例名称无效。";
                return result;
            }

            if (_instanceService.InstanceExists(targetInstanceId))
            {
                result.Message = $"实例 \"{targetInstanceId}\" 已存在，请换一个名称。";
                return result;
            }

            try
            {
                // 1. 读取源实例的登录状态（实例可能仍在运行，读取可获得内存中的最新数据）
                var allCookies = await _siteData.ReadCookiesAsync(sourceInstanceId);
                var selectedCookies = allCookies
                    .Where(c => hosts.Any(h => _siteData.IsHostMatch(c.Domain, h)))
                    .ToList();

                var storageOrigins = _siteData.GetStorageOrigins(sourceInstanceId);
                var candidateOrigins = GetLocalStorageCandidates(storageOrigins, hosts);
                var localStorageData = await _siteData.ReadLocalStorageBatchAsync(
                    sourceInstanceId, candidateOrigins, result.Warnings);

                // 2. 先创建新实例（只建立目录，不启动浏览器），失败时无需停止源实例
                var created = await _instanceService.CreateInstanceAsync(
                    targetInstanceId, $"从 \"{sourceInstanceId}\" 提取的网站数据");
                if (created == null)
                {
                    result.Message = $"创建实例 \"{targetInstanceId}\" 失败。";
                    return result;
                }

                // 3. 停止源实例，释放站点存储目录的文件占用（目录复制要求进程已退出）
                await _instanceService.StopInstanceAsync(sourceInstanceId);

                // 4. 复制站点存储目录（必须在创建新实例的 WebView2 环境之前完成）
                result.Warnings.AddRange(_siteData.CopyStorageForHosts(sourceInstanceId, targetInstanceId, hosts));

                // 5. 写入 Cookie 与 Local Storage（由 WebView2 自动用本机密钥重新加密）
                if (selectedCookies.Count > 0)
                {
                    await _siteData.WriteCookiesAsync(targetInstanceId, selectedCookies);
                }

                await _siteData.WriteLocalStorageBatchAsync(targetInstanceId, localStorageData, result.Warnings);

                // 6. 可选：从源实例移除所选网站的数据，实现「独立出来」
                if (removeFromSource)
                {
                    result.Warnings.AddRange(_siteData.DeleteStorageForHosts(sourceInstanceId, hosts));

                    if (selectedCookies.Count > 0)
                    {
                        await _siteData.DeleteCookiesAsync(sourceInstanceId, selectedCookies);
                    }

                    await _siteData.ClearLocalStorageBatchAsync(sourceInstanceId, candidateOrigins, result.Warnings);
                }

                result.Success = true;
                result.CookieCount = selectedCookies.Count;
                // 只统计所选网站中确实带有站点存储的，避免把源实例未选网站也算进来
                result.StorageSiteCount = hosts.Count(h =>
                    storageOrigins.Any(o => _siteData.IsHostMatch(o, h)) ||
                    localStorageData.Keys.Any(o => _siteData.IsHostMatch(o, h)));
                result.Message = $"已从 \"{sourceInstanceId}\" 提取 {hosts.Count} 个网站的登录状态到新实例 \"{targetInstanceId}\"。\n" +
                                 $"Cookie：{result.CookieCount} 条\n" +
                                 $"含站点存储的网站：{result.StorageSiteCount} 个\n" +
                                 (removeFromSource ? "已同时从源实例移除所选网站的数据。" : "源实例数据保持不变。");
                return result;
            }
            catch (Exception ex)
            {
                result.Message = $"提取失败：{ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// 预览合并结果（分析各来源实例的网站与冲突情况，不修改任何数据）
        /// </summary>
        public async Task<MergePreview> PreviewMergeAsync(List<string> sourceInstanceIds)
        {
            var preview = new MergePreview
            {
                SourceInstanceIds = new List<string>(sourceInstanceIds ?? new List<string>())
            };

            var map = new Dictionary<string, MergeHostEntry>(StringComparer.OrdinalIgnoreCase);
            var sourcesOfHost = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var sourceId in preview.SourceInstanceIds)
            {
                if (!_instanceService.InstanceExists(sourceId))
                {
                    continue;
                }

                var cookies = await _siteData.ReadCookiesAsync(sourceId);
                foreach (var cookie in cookies)
                {
                    string host = _siteData.GetHost(cookie.Domain);
                    if (host.Length == 0)
                    {
                        continue;
                    }

                    GetOrCreateEntry(map, host).CookieCount++;
                    AddSource(sourcesOfHost, host, sourceId);
                }

                foreach (var origin in _siteData.GetStorageOrigins(sourceId))
                {
                    string host = _siteData.GetHost(origin);
                    if (host.Length == 0)
                    {
                        continue;
                    }

                    GetOrCreateEntry(map, host).HasStorage = true;
                    AddSource(sourcesOfHost, host, sourceId);
                }
            }

            foreach (var pair in map)
            {
                var entry = pair.Value;
                entry.SourceInstanceIds = sourcesOfHost.TryGetValue(pair.Key, out var list)
                    ? list
                    : new List<string>();
                entry.Options = entry.SourceInstanceIds
                    .Select(id => new SourceOption { InstanceId = id, Label = GetSourceLabel(id) })
                    .ToList();
                // 默认保留第一个来源的数据，冲突网站由用户在对话框中改选
                entry.SelectedOption = entry.Options.Count > 0 ? entry.Options[0] : null;
            }

            preview.Hosts = map.Values
                .OrderByDescending(h => h.SourceInstanceIds.Count)
                .ThenBy(h => h.Host, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return preview;
        }

        /// <summary>
        /// 合并多个实例的网站数据到目标实例
        /// 每个网站（含其 Cookie 与站点存储）整体取自「归属来源」；目标实例中同网站的旧数据会被覆盖
        /// </summary>
        public async Task<SiteTransferResult> MergeAsync(
            List<string> sourceInstanceIds, Dictionary<string, string> hostWinners,
            string targetInstanceId, bool targetExists)
        {
            var result = new SiteTransferResult { TargetInstanceId = targetInstanceId };

            var sources = (sourceInstanceIds ?? new List<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Where(id => !string.Equals(id, targetInstanceId, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (sources.Count == 0)
            {
                result.Message = "未选择任何来源实例。";
                return result;
            }

            var missing = sources.Where(id => !_instanceService.InstanceExists(id)).ToList();
            if (missing.Count > 0)
            {
                result.Message = $"来源实例不存在：{string.Join("、", missing)}";
                return result;
            }

            if (string.IsNullOrWhiteSpace(targetInstanceId) ||
                targetInstanceId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                result.Message = "目标实例名称无效。";
                return result;
            }

            try
            {
                // 1. 读取各来源实例的登录状态（实例可能仍在运行，读取可获得内存中的最新数据）
                var sourceData = new Dictionary<string, SourceSnapshot>(StringComparer.OrdinalIgnoreCase);
                foreach (var sourceId in sources)
                {
                    var snapshot = new SourceSnapshot
                    {
                        Cookies = await _siteData.ReadCookiesAsync(sourceId),
                        Origins = _siteData.GetStorageOrigins(sourceId)
                    };
                    // Local Storage 需逐个站点访问读取，候选来源由该实例的网站列表推导（只取主机名相同的来源）
                    snapshot.LocalStorage = await _siteData.ReadLocalStorageBatchAsync(
                        sourceId,
                        GetLocalStorageCandidates(snapshot.Origins, CollectHosts(new[] { snapshot }), true),
                        result.Warnings);
                    sourceData[sourceId] = snapshot;
                }

                // 2. 统计所有网站及各自的归属来源（无冲突网站自动取第一个来源，冲突网站按用户选择）
                //    提前判空：没有可合并的数据时直接返回，避免无谓地停止来源实例或新建空目标实例
                var allHosts = CollectHosts(sourceData.Values);
                var winners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var host in allHosts)
                {
                    var owners = sources.Where(id => SnapshotContains(sourceData[id], host)).ToList();
                    if (owners.Count == 0)
                    {
                        continue;
                    }

                    string? winner = null;
                    if (hostWinners != null &&
                        hostWinners.TryGetValue(host, out var chosen) &&
                        owners.Contains(chosen, StringComparer.OrdinalIgnoreCase))
                    {
                        winner = owners.First(id => string.Equals(id, chosen, StringComparison.OrdinalIgnoreCase));
                    }

                    winners[host] = winner ?? owners[0];
                }

                var mergeHosts = winners.Keys.ToList();
                if (mergeHosts.Count == 0)
                {
                    result.Message = "所选来源实例中没有可合并的网站数据。";
                    return result;
                }

                var mergeHostSet = new HashSet<string>(mergeHosts, StringComparer.OrdinalIgnoreCase);

                // 3. 目标实例不存在时先创建（创建只建立目录，不会启动浏览器），失败时无需停止来源
                bool targetCreated = false;
                if (!_instanceService.InstanceExists(targetInstanceId))
                {
                    var created = await _instanceService.CreateInstanceAsync(targetInstanceId, "合并创建的实例");
                    if (created == null)
                    {
                        result.Message = $"创建实例 \"{targetInstanceId}\" 失败。";
                        return result;
                    }
                    targetCreated = true;
                }

                // 4. 读取目标实例的原有数据（同样在停止前读取），用于按网站覆盖并清除旧登录状态
                var targetCookies = new List<CookieData>();
                var targetOrigins = new List<string>();
                var targetOriginsToClear = new List<string>();
                if (!targetCreated)
                {
                    targetCookies = await _siteData.ReadCookiesAsync(targetInstanceId);
                    targetOrigins = _siteData.GetStorageOrigins(targetInstanceId);

                    // 只读取目标中与本次合并网站相关的 Local Storage（避免访问无关站点）
                    var targetHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var cookie in targetCookies)
                    {
                        AddHostIfMerged(targetHosts, mergeHostSet, _siteData.GetHost(cookie.Domain));
                    }
                    foreach (var origin in targetOrigins)
                    {
                        AddHostIfMerged(targetHosts, mergeHostSet, _siteData.GetHost(origin));
                    }

                    var targetCandidates = GetLocalStorageCandidates(
                        targetOrigins.Where(o => mergeHostSet.Contains(_siteData.GetHost(o))),
                        targetHosts,
                        true);
                    var targetLocalStorage = await _siteData.ReadLocalStorageBatchAsync(
                        targetInstanceId, targetCandidates, result.Warnings);
                    targetOriginsToClear = targetLocalStorage.Keys
                        .Where(o => mergeHostSet.Contains(_siteData.GetHost(o)))
                        .ToList();
                }

                // 5. 停止所有涉及的实例，释放站点存储目录的文件占用
                await _instanceService.StopInstanceAsync(targetInstanceId);
                foreach (var sourceId in sources)
                {
                    await _instanceService.StopInstanceAsync(sourceId);
                }

                // 6. 站点存储目录：先按网站精确清空目标旧目录，再按归属来源复制（必须在创建目标环境之前）
                //    精确匹配可避免父域网站（如 deepseek.com）的步骤覆盖或删除子域网站（如 chat.deepseek.com）的数据
                result.Warnings.AddRange(_siteData.DeleteStorageForHosts(targetInstanceId, mergeHosts, true));

                foreach (var group in winners.GroupBy(p => p.Value))
                {
                    result.Warnings.AddRange(_siteData.CopyStorageForHosts(
                        group.Key, targetInstanceId, group.Select(p => p.Key).ToList(), true));
                }

                // 7. Cookie：每个 Cookie 按自身域名归属网站，只取该网站归属来源的 Cookie，
                //    避免父域 Cookie（如 .deepseek.com）被多个网站分组重复收集、去重时随机保留
                var cookiesToWrite = new List<CookieData>();
                foreach (var host in mergeHosts)
                {
                    cookiesToWrite.AddRange(sourceData[winners[host]].Cookies
                        .Where(c => string.Equals(_siteData.GetHost(c.Domain), host, StringComparison.OrdinalIgnoreCase)));
                }

                cookiesToWrite = cookiesToWrite
                    .GroupBy(c => $"{c.Name}|{c.Domain}|{c.Path}", StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();

                var targetCookiesToDelete = targetCookies
                    .Where(c => mergeHostSet.Contains(_siteData.GetHost(c.Domain)))
                    .ToList();

                if (targetCookiesToDelete.Count > 0)
                {
                    await _siteData.DeleteCookiesAsync(targetInstanceId, targetCookiesToDelete);
                }

                if (cookiesToWrite.Count > 0)
                {
                    await _siteData.WriteCookiesAsync(targetInstanceId, cookiesToWrite);
                }

                // 8. Local Storage：只取归属来源的数据，并在同一个隐藏会话中「先清空再写入」
                //    清空范围同时包含目标实例中已有数据的来源，避免归属来源没有数据时残留目标旧登录状态
                var localStorageToWrite = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
                foreach (var sourceId in sources)
                {
                    foreach (var pair in sourceData[sourceId].LocalStorage)
                    {
                        string host = _siteData.GetHost(pair.Key);
                        if (winners.TryGetValue(host, out var winner) &&
                            string.Equals(winner, sourceId, StringComparison.OrdinalIgnoreCase))
                        {
                            localStorageToWrite[pair.Key] = pair.Value;
                        }
                    }
                }

                var originsToClear = new List<string>(targetOriginsToClear);
                foreach (var origin in localStorageToWrite.Keys)
                {
                    if (!originsToClear.Contains(origin, StringComparer.OrdinalIgnoreCase))
                    {
                        originsToClear.Add(origin);
                    }
                }

                await _siteData.ReplaceLocalStorageBatchAsync(
                    targetInstanceId, originsToClear, localStorageToWrite, result.Warnings);

                result.Success = true;
                result.CookieCount = cookiesToWrite.Count;
                result.StorageSiteCount = mergeHosts.Count(h =>
                    sourceData[winners[h]].Origins.Any(o =>
                        string.Equals(_siteData.GetHost(o), h, StringComparison.OrdinalIgnoreCase)) ||
                    sourceData[winners[h]].LocalStorage.Keys.Any(o =>
                        string.Equals(_siteData.GetHost(o), h, StringComparison.OrdinalIgnoreCase)));
                result.Message = $"已把 {sources.Count} 个实例的 {mergeHosts.Count} 个网站合并到 \"{targetInstanceId}\"。\n" +
                                 $"Cookie：{result.CookieCount} 条\n" +
                                 $"含站点存储的网站：{result.StorageSiteCount} 个\n" +
                                 "来源实例的数据保持不变（仅合并时被停止）。";
                return result;
            }
            catch (Exception ex)
            {
                result.Message = $"合并失败：{ex.Message}\n目标实例可能只写入了部分数据，请检查后再使用或重试。";
                return result;
            }
        }

        /// <summary>
        /// 单个来源实例的登录状态快照
        /// </summary>
        private class SourceSnapshot
        {
            /// <summary>该实例的全部 Cookie</summary>
            public List<CookieData> Cookies { get; set; } = new();

            /// <summary>该实例带站点存储（IndexedDB / WebStorage）的来源地址</summary>
            public List<string> Origins { get; set; } = new();

            /// <summary>该实例的 Local Storage 内容（来源地址 → 键值对）</summary>
            public Dictionary<string, Dictionary<string, string>> LocalStorage { get; set; }
                = new(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 收集所有来源实例中出现的网站主机名（去重）
        /// </summary>
        private List<string> CollectHosts(IEnumerable<SourceSnapshot> snapshots)
        {
            var hostSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<string>();

            foreach (var snapshot in snapshots)
            {
                foreach (var cookie in snapshot.Cookies)
                {
                    AddHost(hostSet, result, _siteData.GetHost(cookie.Domain));
                }

                foreach (var origin in snapshot.Origins)
                {
                    AddHost(hostSet, result, _siteData.GetHost(origin));
                }
            }

            return result;
        }

        /// <summary>
        /// 向主机名集合与列表中添加（去重）
        /// </summary>
        private static void AddHost(HashSet<string> set, List<string> list, string host)
        {
            if (host.Length > 0 && set.Add(host))
            {
                list.Add(host);
            }
        }

        /// <summary>
        /// 仅当主机属于本次合并网站时加入目标集合
        /// </summary>
        private static void AddHostIfMerged(HashSet<string> target, HashSet<string> mergeHosts, string host)
        {
            if (host.Length > 0 && mergeHosts.Contains(host))
            {
                target.Add(host);
            }
        }

        /// <summary>
        /// 判断来源快照中是否包含指定网站的数据（按主机名精确匹配，与合并时的数据归属规则保持一致）
        /// </summary>
        private bool SnapshotContains(SourceSnapshot snapshot, string host)
        {
            return snapshot.Cookies.Any(c =>
                       string.Equals(_siteData.GetHost(c.Domain), host, StringComparison.OrdinalIgnoreCase)) ||
                   snapshot.Origins.Any(o =>
                       string.Equals(_siteData.GetHost(o), host, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 生成 Local Storage 的候选来源地址
        /// 除已知的存储来源外，还补充 https://主机名，以覆盖仅有 Local Storage 数据的网站
        /// </summary>
        /// <param name="storageOrigins">实例中已知的站点存储来源</param>
        /// <param name="hosts">要处理的网站主机名列表</param>
        /// <param name="exactHostOnly">true 时已知来源也按主机名精确匹配（合并场景使用）</param>
        private List<string> GetLocalStorageCandidates(
            IEnumerable<string> storageOrigins, IEnumerable<string> hosts, bool exactHostOnly = false)
        {
            var originList = storageOrigins?.ToList() ?? new List<string>();
            var result = new List<string>();

            foreach (var host in hosts)
            {
                foreach (var origin in originList.Where(o => exactHostOnly
                    ? string.Equals(_siteData.GetHost(o), host, StringComparison.OrdinalIgnoreCase)
                    : _siteData.IsHostMatch(o, host)))
                {
                    AddCandidate(result, origin);
                }

                AddCandidate(result, "https://" + host);
            }

            return result;
        }

        /// <summary>
        /// 添加候选来源地址（去重、去空）
        /// </summary>
        private static void AddCandidate(List<string> result, string origin)
        {
            if (!string.IsNullOrWhiteSpace(origin) &&
                !result.Contains(origin, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(origin);
            }
        }

        /// <summary>
        /// 获取来源实例的显示文本（实例名称 + 备注）
        /// </summary>
        private string GetSourceLabel(string instanceId)
        {
            string note = _instanceService.GetInstanceNote(instanceId);
            return string.IsNullOrWhiteSpace(note) ? instanceId : $"{instanceId}（{note}）";
        }

        /// <summary>
        /// 获取或创建网站条目（提取网站对话框使用）
        /// </summary>
        private static SiteEntry GetOrCreateSiteEntry(Dictionary<string, SiteEntry> map, string host)
        {
            if (!map.TryGetValue(host, out var entry))
            {
                entry = new SiteEntry { Host = host };
                map[host] = entry;
            }

            return entry;
        }

        /// <summary>
        /// 获取或创建网站条目
        /// </summary>
        private static MergeHostEntry GetOrCreateEntry(Dictionary<string, MergeHostEntry> map, string host)
        {
            if (!map.TryGetValue(host, out var entry))
            {
                entry = new MergeHostEntry { Host = host };
                map[host] = entry;
            }

            return entry;
        }

        /// <summary>
        /// 记录某网站的包含来源（去重、保持来源顺序）
        /// </summary>
        private static void AddSource(Dictionary<string, List<string>> map, string host, string sourceId)
        {
            if (!map.TryGetValue(host, out var list))
            {
                list = new List<string>();
                map[host] = list;
            }

            if (!list.Contains(sourceId, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(sourceId);
            }
        }
    }
}