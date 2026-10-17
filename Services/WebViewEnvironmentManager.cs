// CustomizedBrowser\Services\WebViewEnvironmentManager.cs
using CustomizedBrowser.Core;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

namespace CustomizedBrowser.Services
{
    /// <summary>
    /// WebView2 Environment 管理器
    /// 按实例ID缓存CoreWebView2Environment，避免重复创建，同时保持多实例隔离
    /// </summary>
    public class WebViewEnvironmentManager : IWebViewEnvironmentManager
    {
        private readonly ConcurrentDictionary<string, Lazy<Task<CoreWebView2Environment>>> _environments = new();
        private bool _disposed = false;

        /// <summary>
        /// 获取或创建指定实例的CoreWebView2Environment
        /// </summary>
        /// <param name="instanceId">实例ID</param>
        /// <returns>CoreWebView2Environment实例</returns>
        public Task<CoreWebView2Environment> GetOrCreateEnvironmentAsync(string instanceId)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(WebViewEnvironmentManager));
            }

            return _environments.GetOrAdd(instanceId, _ => new Lazy<Task<CoreWebView2Environment>>(() => CreateEnvironmentAsync(instanceId))).Value;
        }

        /// <summary>
        /// 创建新的CoreWebView2Environment
        /// </summary>
        private async Task<CoreWebView2Environment> CreateEnvironmentAsync(string instanceId)
        {
            string userDataFolder = AppConstants.GetInstanceDataPath(instanceId);
            Directory.CreateDirectory(userDataFolder);

            var options = new CoreWebView2EnvironmentOptions
            {
                AdditionalBrowserArguments = $"--disk-cache-size={AppConstants.DiskCacheSizeBytes} " +
                                            $"--disk-cache-dir={Path.Combine(userDataFolder, "Cache")} " +
                                            "--no-default-browser-check " +
                                            "--disable-features=PasswordImport"
            };

            return await CoreWebView2Environment.CreateAsync(
                userDataFolder: userDataFolder,
                options: options
            );
        }

        /// <summary>
        /// 移除指定实例的Environment缓存（实例删除时调用）
        /// </summary>
        public void RemoveEnvironment(string instanceId)
        {
            if (_environments.TryRemove(instanceId, out _))
            {
                // Environment由WebView2内部管理，不需要手动Dispose
            }
        }

        /// <summary>
        /// 清空所有缓存
        /// </summary>
        public void Clear()
        {
            _environments.Clear();
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                Clear();
            }

            _disposed = true;
        }

        /// <summary>
        /// 析构函数
        /// </summary>
        ~WebViewEnvironmentManager()
        {
            Dispose(false);
        }
    }
}
