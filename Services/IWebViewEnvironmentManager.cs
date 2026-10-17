// CustomizedBrowser\Services\IWebViewEnvironmentManager.cs
using Microsoft.Web.WebView2.Core;
using System;
using System.Threading.Tasks;

namespace CustomizedBrowser.Services
{
    /// <summary>
    /// WebView2 Environment 管理器接口
    /// 负责按实例ID缓存CoreWebView2Environment，避免重复创建
    /// </summary>
    public interface IWebViewEnvironmentManager : IDisposable
    {
        /// <summary>
        /// 获取或创建指定实例的CoreWebView2Environment
        /// </summary>
        Task<CoreWebView2Environment> GetOrCreateEnvironmentAsync(string instanceId);
        
        /// <summary>
        /// 移除指定实例的Environment缓存（实例删除时调用）
        /// </summary>
        void RemoveEnvironment(string instanceId);
        
        /// <summary>
        /// 清空所有缓存
        /// </summary>
        void Clear();
    }
}
