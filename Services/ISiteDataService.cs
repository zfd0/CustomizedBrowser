// CustomizedBrowser\Services\ISiteDataService.cs
using CustomizedBrowser.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CustomizedBrowser.Services
{
    /// <summary>
    /// 站点数据服务接口
    /// 提供实例中「站点级」数据的读写能力，是提取网站/合并实例功能的底层支撑：
    /// - Cookie：通过 WebView2 的 CookieManager 读写（明文搬运，自动使用本机密钥加密）
    /// - 站点存储：IndexedDB / WebStorage 以来源为单位直接复制目录；Local Storage 因
    ///   为单库共享存储，需通过隐藏窗口访问对应站点用脚本读取/写入
    /// </summary>
    public interface ISiteDataService
    {
        /// <summary>
        /// 读取实例的全部 Cookie
        /// 实例正在运行时会复用其环境，可读到浏览器进程内存中的实时 Cookie
        /// </summary>
        Task<List<CookieData>> ReadCookiesAsync(string instanceId);

        /// <summary>
        /// 将 Cookie 写入实例（同名 Cookie 会被覆盖；会话 Cookie 转为持久 Cookie）
        /// </summary>
        Task WriteCookiesAsync(string instanceId, IEnumerable<CookieData> cookies);

        /// <summary>
        /// 从实例中删除指定 Cookie
        /// </summary>
        Task DeleteCookiesAsync(string instanceId, IEnumerable<CookieData> cookies);

        /// <summary>
        /// 枚举实例中带站点存储（IndexedDB / WebStorage）的来源地址
        /// </summary>
        List<string> GetStorageOrigins(string instanceId);

        /// <summary>
        /// 复制指定网站的 IndexedDB / WebStorage 存储目录
        /// </summary>
        /// <param name="exactHostOnly">是否只匹配主机名完全相同的来源（合并场景用于精确归属，避免父域网站覆盖子域网站的数据）</param>
        /// <returns>警告信息列表</returns>
        List<string> CopyStorageForHosts(
            string sourceInstanceId, string targetInstanceId, IEnumerable<string> hosts, bool exactHostOnly = false);

        /// <summary>
        /// 删除指定网站的 IndexedDB / WebStorage 存储目录
        /// </summary>
        /// <param name="exactHostOnly">是否只匹配主机名完全相同的来源（合并场景用于精确归属，避免误删子域网站的数据）</param>
        /// <returns>警告信息列表</returns>
        List<string> DeleteStorageForHosts(string instanceId, IEnumerable<string> hosts, bool exactHostOnly = false);

        /// <summary>
        /// 批量读取指定来源的 Local Storage 内容
        /// （通过隐藏窗口访问站点，由页面脚本读取；读取为空的来源不会出现在结果中）
        /// </summary>
        Task<Dictionary<string, Dictionary<string, string>>> ReadLocalStorageBatchAsync(
            string instanceId, IEnumerable<string> origins, List<string> warnings);

        /// <summary>
        /// 批量写入 Local Storage 内容
        /// </summary>
        Task WriteLocalStorageBatchAsync(
            string instanceId, IDictionary<string, Dictionary<string, string>> data, List<string> warnings);

        /// <summary>
        /// 批量清空指定来源的 Local Storage
        /// </summary>
        Task ClearLocalStorageBatchAsync(string instanceId, IEnumerable<string> origins, List<string> warnings);

        /// <summary>
        /// 在同一个隐藏会话中批量「先清空再写入」Local Storage
        /// 合并在覆盖目标网站数据时使用：清空与写入必须连续完成，避免中间关闭浏览器进程导致
        /// 清空结果尚未落盘、旧数据与新数据混合
        /// </summary>
        /// <param name="instanceId">目标实例 ID</param>
        /// <param name="originsToClear">需要清空的来源地址（旧数据）</param>
        /// <param name="data">需要写入的来源数据（来源地址 → 键值对）</param>
        /// <param name="warnings">警告信息收集列表</param>
        Task ReplaceLocalStorageBatchAsync(
            string instanceId, IEnumerable<string> originsToClear,
            IDictionary<string, Dictionary<string, string>> data, List<string> warnings);

        /// <summary>
        /// 判断 Cookie 域或来源主机是否属于指定网站（含父子域双向匹配）
        /// </summary>
        bool IsHostMatch(string domainOrHost, string host);

        /// <summary>
        /// 从域名或来源地址中取出主机名
        /// </summary>
        string GetHost(string domainOrOrigin);
    }
}