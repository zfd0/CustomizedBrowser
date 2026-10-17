// CustomizedBrowser\Services\ISiteTransferService.cs
using CustomizedBrowser.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CustomizedBrowser.Services
{
    /// <summary>
    /// 网站数据迁移服务接口
    /// 负责两类操作：
    /// 1. 提取网站：把实例中指定网站的登录状态（Cookie + 站点存储）独立到新实例
    /// 2. 合并实例：把多个实例的网站数据合并到目标实例，相同网站的冲突由用户选择
    /// </summary>
    public interface ISiteTransferService
    {
        /// <summary>
        /// 分析实例中的网站列表（来源于 Cookie 域与站点存储来源）
        /// </summary>
        Task<List<SiteEntry>> GetSitesAsync(string instanceId);

        /// <summary>
        /// 将源实例中指定网站的登录状态提取到新实例
        /// </summary>
        /// <param name="sourceInstanceId">源实例 ID</param>
        /// <param name="hosts">要提取的网站主机名列表</param>
        /// <param name="targetInstanceId">新实例 ID（必须不存在，由调用方保证）</param>
        /// <param name="removeFromSource">是否同时从源实例移除所选网站的数据</param>
        Task<SiteTransferResult> ExtractAsync(
            string sourceInstanceId, List<string> hosts, string targetInstanceId, bool removeFromSource);

        /// <summary>
        /// 预览合并结果（分析各来源实例的网站与冲突情况，不修改任何数据）
        /// </summary>
        Task<MergePreview> PreviewMergeAsync(List<string> sourceInstanceIds);

        /// <summary>
        /// 合并多个实例的网站数据到目标实例
        /// </summary>
        /// <param name="sourceInstanceIds">来源实例 ID 列表</param>
        /// <param name="hostWinners">冲突网站的归属选择（网站主机 → 保留数据的来源实例 ID），未提供的网站自动取第一个来源</param>
        /// <param name="targetInstanceId">目标实例 ID</param>
        /// <param name="targetExists">目标实例是否已存在（false 表示需要新建）</param>
        Task<SiteTransferResult> MergeAsync(
            List<string> sourceInstanceIds, Dictionary<string, string> hostWinners,
            string targetInstanceId, bool targetExists);
    }
}