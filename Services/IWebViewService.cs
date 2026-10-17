// CustomizedBrowser\Services\IWebViewService.cs
using System;
using System.Threading.Tasks;

namespace CustomizedBrowser.Services
{
    /// <summary>
    /// WebView 服务接口
    /// 负责 WebView2 控件的创建、配置和管理
    /// </summary>
    public interface IWebViewService
    {
        /// <summary>
        /// 创建 WebView2 环境
        /// </summary>
        Task CreateEnvironmentAsync(string userDataFolder);

        /// <summary>
        /// 导航到指定 URL
        /// </summary>
        void Navigate(string url);

        /// <summary>
        /// 后退
        /// </summary>
        void GoBack();

        /// <summary>
        /// 前进
        /// </summary>
        void GoForward();

        /// <summary>
        /// 刷新
        /// </summary>
        void Refresh();

        /// <summary>
        /// 打开开发者工具
        /// </summary>
        void OpenDevTools();

        /// <summary>
        /// 是否可以后退
        /// </summary>
        bool CanGoBack { get; }

        /// <summary>
        /// 是否可以前进
        /// </summary>
        bool CanGoForward { get; }

        /// <summary>
        /// 当前 URL
        /// </summary>
        string? CurrentUrl { get; }

        /// <summary>
        /// 导航完成事件
        /// </summary>
        event EventHandler<string>? NavigationCompleted;

        /// <summary>
        /// 源变化事件
        /// </summary>
        event EventHandler<string>? SourceChanged;
    }
}
