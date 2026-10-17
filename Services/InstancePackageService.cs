// CustomizedBrowser\Services\InstancePackageService.cs
using CustomizedBrowser.Core;
using CustomizedBrowser.Models;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading.Tasks;

namespace CustomizedBrowser.Services
{
    /// <summary>
    /// 实例包服务实现
    /// 登录状态由两部分构成，需分别处理：
    /// 1. Cookie：由 Chromium 加密且密钥受 Windows DPAPI（机器 + 用户绑定）保护，
    ///    因此通过 WebView2 的 CookieManager 以明文方式导出/导入；
    /// 2. 站点存储（Local Storage / IndexedDB 等）：未加密，直接复制目录即可。
    /// 另外，Cookie 在浏览器进程内存中可能存在未落盘的部分，导出时必须在关闭实例前读取。
    /// </summary>
    public class InstancePackageService : IInstancePackageService
    {
        /// <summary>实例包清单文件名</summary>
        private const string ManifestEntryName = "manifest.json";

        /// <summary>Cookie 数据文件名</summary>
        private const string CookiesEntryName = "cookies.json";

        /// <summary>备注文件名</summary>
        private const string NoteEntryName = "note.txt";

        /// <summary>站点存储数据在实例包内的目录前缀</summary>
        private const string StorageEntryPrefix = "storage";

        /// <summary>
        /// 会话 Cookie 在导入时转换为持久 Cookie 的有效期（天）
        /// 因为 WebView2 不会把会话 Cookie 持久化到磁盘，若不转换则导入后立即失效
        /// </summary>
        private const int SessionCookiePersistDays = 30;

        private readonly IInstanceService _instanceService;
        private readonly IWebViewEnvironmentManager _environmentManager;
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { WriteIndented = true };

        /// <summary>
        /// 构造函数
        /// </summary>
        public InstancePackageService(IInstanceService instanceService, IWebViewEnvironmentManager environmentManager)
        {
            _instanceService = instanceService;
            _environmentManager = environmentManager;
        }

        /// <summary>
        /// 读取实例包清单（不执行导入）
        /// </summary>
        public InstancePackageManifest? ReadManifest(string packagePath)
        {
            try
            {
                if (!File.Exists(packagePath))
                {
                    return null;
                }

                using var archive = ZipFile.OpenRead(packagePath);
                var entry = archive.GetEntry(ManifestEntryName);
                if (entry == null)
                {
                    return null;
                }

                using var stream = entry.Open();
                return JsonSerializer.Deserialize<InstancePackageManifest>(stream);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 将指定实例导出为实例包
        /// 流程：先在实例运行状态下读取 Cookie（可获取内存中未落盘的部分），
        /// 再关闭实例以刷写会话状态并释放文件占用，随后读取会话文件与站点存储目录
        /// </summary>
        public async Task<InstanceExportResult> ExportAsync(string instanceId, string packagePath)
        {
            var result = new InstanceExportResult { FilePath = packagePath };

            if (!_instanceService.InstanceExists(instanceId))
            {
                result.Message = $"实例 \"{instanceId}\" 不存在。";
                return result;
            }

            string note = _instanceService.GetInstanceNote(instanceId);

            // 1. 读取 Cookie（必须在本步之前不关闭实例，否则内存中的 Cookie 会丢失）
            var cookies = new List<CookieData>();
            string cookieWarning = string.Empty;
            try
            {
                cookies = await ReadCookiesAsync(instanceId);
            }
            catch (Exception ex)
            {
                cookieWarning = $"（Cookie 读取失败，未包含 Cookie 登录状态：{ex.Message}）";
            }

            // 2. 关闭实例，确保会话状态写入磁盘并释放站点存储目录的文件占用
            await _instanceService.StopInstanceAsync(instanceId);

            // 3. 读取会话状态与最后访问页面
            string? sessionJson = ReadTextIfExists(AppConstants.GetSessionStateFilePath(instanceId));
            string? lastPageJson = ReadTextIfExists(AppConstants.GetLastPageFilePath(instanceId));

            int tabCount = 0;
            if (!string.IsNullOrEmpty(sessionJson))
            {
                try
                {
                    var session = JsonSerializer.Deserialize<SessionState>(sessionJson);
                    tabCount = session?.Tabs.Count ?? 0;
                }
                catch (Exception)
                {
                }
            }

            bool includesStorage = HasPortableStorage(instanceId);

            var manifest = new InstancePackageManifest
            {
                InstanceId = instanceId,
                Note = note,
                ExportedAt = DateTime.Now,
                AppVersion = AppConstants.AppVersion,
                TabCount = tabCount,
                CookieCount = cookies.Count,
                IncludesStorage = includesStorage
            };

            try
            {
                if (File.Exists(packagePath))
                {
                    File.Delete(packagePath);
                }

                using var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create);
                WriteEntry(archive, ManifestEntryName, JsonSerializer.Serialize(manifest, _jsonOptions));
                WriteEntry(archive, CookiesEntryName, JsonSerializer.Serialize(cookies, _jsonOptions));
                if (!string.IsNullOrEmpty(sessionJson))
                {
                    WriteEntry(archive, AppConstants.SessionStateFileName, sessionJson);
                }
                if (!string.IsNullOrEmpty(lastPageJson))
                {
                    WriteEntry(archive, System.IO.Path.GetFileName(AppConstants.GetLastPageFilePath(instanceId)), lastPageJson);
                }
                WriteEntry(archive, NoteEntryName, note);

                // 4. 打包站点存储目录（Local Storage / IndexedDB 等）
                AddStorageToArchive(archive, instanceId);
            }
            catch (Exception ex)
            {
                result.Message = $"导出失败：{ex.Message}";
                return result;
            }

            result.Success = true;
            result.CookieCount = cookies.Count;
            result.TabCount = tabCount;
            result.Message = $"实例 \"{instanceId}\" 导出成功。\n" +
                             $"标签页：{tabCount} 个\n" +
                             $"Cookie：{cookies.Count} 条\n" +
                             $"站点存储：{(includesStorage ? "已包含" : "无")}{cookieWarning}";
            return result;
        }

        /// <summary>
        /// 从实例包导入实例
        /// </summary>
        public async Task<InstanceImportResult> ImportAsync(string packagePath, string targetInstanceId)
        {
            var result = new InstanceImportResult { InstanceId = targetInstanceId };

            if (!File.Exists(packagePath))
            {
                result.Message = "实例包文件不存在。";
                return result;
            }

            // 校验目标实例名称是否合法（防止路径穿越或非法字符）
            if (string.IsNullOrWhiteSpace(targetInstanceId) ||
                targetInstanceId.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
            {
                result.Message = "目标实例名称无效。";
                return result;
            }

            string tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CustomizedBrowserImport_" + Guid.NewGuid().ToString("N"));
            try
            {
                // 解压到临时目录
                ZipFile.ExtractToDirectory(packagePath, tempDir);

                var manifest = ReadManifestFromDirectory(tempDir);
                if (manifest == null)
                {
                    result.Message = "不是有效的实例包（缺少 manifest.json）。";
                    return result;
                }

                // 停止目标实例，避免文件占用
                await _instanceService.StopInstanceAsync(targetInstanceId);

                // 创建或复用实例目录
                if (!_instanceService.InstanceExists(targetInstanceId))
                {
                    var created = await _instanceService.CreateInstanceAsync(targetInstanceId, manifest.Note);
                    if (created == null)
                    {
                        result.Message = $"创建实例 \"{targetInstanceId}\" 失败。";
                        return result;
                    }
                }
                else if (!string.IsNullOrEmpty(manifest.Note))
                {
                    _instanceService.UpdateInstanceNote(targetInstanceId, manifest.Note);
                }

                string instanceDir = AppConstants.GetInstanceDataPath(targetInstanceId);
                Directory.CreateDirectory(instanceDir);

                // 恢复站点存储（Local Storage / IndexedDB 等），必须在创建 WebView2 环境之前
                RestoreStorageFromDirectory(tempDir, targetInstanceId);

                // 恢复会话状态与最后访问页面
                CopyIfExists(
                    System.IO.Path.Combine(tempDir, AppConstants.SessionStateFileName),
                    AppConstants.GetSessionStateFilePath(targetInstanceId));
                CopyIfExists(
                    System.IO.Path.Combine(tempDir, System.IO.Path.GetFileName(AppConstants.GetLastPageFilePath(targetInstanceId))),
                    AppConstants.GetLastPageFilePath(targetInstanceId));

                // 写入 Cookie（登录状态）
                var cookies = ReadCookiesFromDirectory(tempDir);
                string cookieWarning = string.Empty;
                if (cookies.Count > 0)
                {
                    try
                    {
                        await WriteCookiesAsync(targetInstanceId, cookies);
                    }
                    catch (Exception ex)
                    {
                        cookieWarning = $"（Cookie 写入失败，导入后可能需要重新登录：{ex.Message}）";
                    }
                }

                result.Success = true;
                result.CookieCount = cookies.Count;
                result.Message = $"实例已导入为 \"{targetInstanceId}\"。\n" +
                                 $"Cookie：{cookies.Count} 条\n" +
                                 $"站点存储：{(manifest.IncludesStorage ? "已恢复" : "无")}{cookieWarning}";
                return result;
            }
            catch (Exception ex)
            {
                result.Message = $"导入失败：{ex.Message}";
                return result;
            }
            finally
            {
                TryDeleteDirectory(tempDir);
            }
        }

        /// <summary>
        /// 读取指定实例的全部 Cookie
        /// 若实例正在运行，则复用其已缓存的 Environment，从而读取到浏览器进程内存中的实时 Cookie
        /// （包含尚未写入磁盘的部分），这是保证登录状态不丢失的关键
        /// </summary>
        private async Task<List<CookieData>> ReadCookiesAsync(string instanceId)
        {
            var environment = await _environmentManager.GetOrCreateEnvironmentAsync(instanceId);
            var host = new CookieHostWindow();
            try
            {
                await host.InitializeAsync(environment);

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
            }
            finally
            {
                host.Shutdown();
            }
        }

        /// <summary>
        /// 将 Cookie 写入指定实例（由 WebView2 自动用本机密钥重新加密）
        /// 会话 Cookie 会被转换为持久 Cookie，否则关闭浏览器进程后即失效
        /// </summary>
        private async Task WriteCookiesAsync(string instanceId, List<CookieData> cookies)
        {
            var environment = await GetFreshEnvironmentAsync(instanceId);
            var host = new CookieHostWindow();
            try
            {
                await host.InitializeAsync(environment);
                var cookieManager = host.Core!.CookieManager;

                foreach (var data in cookies)
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
                await Task.Delay(1500);
            }
            finally
            {
                host.Shutdown();
            }
        }

        /// <summary>
        /// 获取实例的 WebView2 环境
        /// 导入前会关闭实例并清理其进程，因此需先移除可能已失效的缓存环境，
        /// 再创建新的环境，避免在已失效的环境上创建控制器
        /// </summary>
        private async Task<CoreWebView2Environment> GetFreshEnvironmentAsync(string instanceId)
        {
            _environmentManager.RemoveEnvironment(instanceId);
            return await _environmentManager.GetOrCreateEnvironmentAsync(instanceId);
        }

        /// <summary>
        /// 判断实例是否存在可迁移的站点存储目录
        /// </summary>
        private static bool HasPortableStorage(string instanceId)
        {
            string profilePath = AppConstants.GetWebViewDefaultProfilePath(instanceId);
            foreach (var folderName in AppConstants.PortableStorageFolderNames)
            {
                if (Directory.Exists(System.IO.Path.Combine(profilePath, folderName)))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 将实例的站点存储目录写入压缩包
        /// </summary>
        private static void AddStorageToArchive(ZipArchive archive, string instanceId)
        {
            string profilePath = AppConstants.GetWebViewDefaultProfilePath(instanceId);
            foreach (var folderName in AppConstants.PortableStorageFolderNames)
            {
                string sourceDir = System.IO.Path.Combine(profilePath, folderName);
                if (!Directory.Exists(sourceDir))
                {
                    continue;
                }

                foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
                {
                    // 跳过 LevelDB 的锁文件
                    if (string.Equals(System.IO.Path.GetFileName(file), "LOCK", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string relative = System.IO.Path.GetRelativePath(sourceDir, file).Replace('\\', '/');
                    string entryName = $"{StorageEntryPrefix}/{folderName}/{relative}";
                    try
                    {
                        archive.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
                    }
                    catch (IOException)
                    {
                        // 个别文件被占用时跳过
                    }
                }
            }
        }

        /// <summary>
        /// 从实例包临时目录恢复站点存储目录
        /// 恢复前会清空目标目录，避免与目标实例原有数据混合
        /// </summary>
        private static void RestoreStorageFromDirectory(string tempDir, string instanceId)
        {
            string storageRoot = System.IO.Path.Combine(tempDir, StorageEntryPrefix);
            if (!Directory.Exists(storageRoot))
            {
                return;
            }

            string profilePath = AppConstants.GetWebViewDefaultProfilePath(instanceId);
            foreach (var folderName in AppConstants.PortableStorageFolderNames)
            {
                string sourceDir = System.IO.Path.Combine(storageRoot, folderName);
                if (!Directory.Exists(sourceDir))
                {
                    continue;
                }

                string destDir = System.IO.Path.Combine(profilePath, folderName);
                TryDeleteDirectory(destDir);
                CopyDirectory(sourceDir, destDir);
            }
        }

        /// <summary>
        /// 递归复制目录（跳过 LevelDB 锁文件）
        /// </summary>
        private static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                if (string.Equals(System.IO.Path.GetFileName(file), "LOCK", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                File.Copy(file, System.IO.Path.Combine(destDir, System.IO.Path.GetFileName(file)), true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                CopyDirectory(dir, System.IO.Path.Combine(destDir, System.IO.Path.GetFileName(dir)));
            }
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
        /// 从实例包目录读取清单
        /// </summary>
        private static InstancePackageManifest? ReadManifestFromDirectory(string directory)
        {
            string manifestPath = System.IO.Path.Combine(directory, ManifestEntryName);
            if (!File.Exists(manifestPath))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<InstancePackageManifest>(File.ReadAllText(manifestPath));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 从实例包目录读取 Cookie 列表
        /// </summary>
        private static List<CookieData> ReadCookiesFromDirectory(string directory)
        {
            string cookiesPath = System.IO.Path.Combine(directory, CookiesEntryName);
            if (!File.Exists(cookiesPath))
            {
                return new List<CookieData>();
            }

            try
            {
                return JsonSerializer.Deserialize<List<CookieData>>(File.ReadAllText(cookiesPath)) ?? new List<CookieData>();
            }
            catch (Exception)
            {
                return new List<CookieData>();
            }
        }

        /// <summary>
        /// 读取文本文件内容，文件不存在时返回 null
        /// </summary>
        private static string? ReadTextIfExists(string path)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 复制文件（源文件不存在时跳过）
        /// </summary>
        private static void CopyIfExists(string sourcePath, string destPath)
        {
            if (File.Exists(sourcePath))
            {
                File.Copy(sourcePath, destPath, true);
            }
        }

        /// <summary>
        /// 向压缩包写入一个文本条目
        /// </summary>
        private void WriteEntry(ZipArchive archive, string entryName, string content)
        {
            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using var writer = new StreamWriter(entry.Open(), new System.Text.UTF8Encoding(false));
            writer.Write(content);
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
