// CustomizedBrowser\Services\WebViewService.cs
using System;
using System.Threading.Tasks;

namespace CustomizedBrowser.Services
{
    /// <summary>
    /// WebView 服务实现
    /// 负责 WebView2 控件的创建、配置和管理
    /// </summary>
    public class WebViewService : IWebViewService
    {
        public event EventHandler<string>? NavigationCompleted;
        public event EventHandler<string>? SourceChanged;

        public bool CanGoBack => false;
        public bool CanGoForward => false;
        public string? CurrentUrl => null;

        /// <summary>
        /// 创建 WebView2 环境
        /// </summary>
        public Task CreateEnvironmentAsync(string userDataFolder)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 导航到指定 URL
        /// </summary>
        public void Navigate(string url)
        {
        }

        /// <summary>
        /// 后退
        /// </summary>
        public void GoBack()
        {
        }

        /// <summary>
        /// 前进
        /// </summary>
        public void GoForward()
        {
        }

        /// <summary>
        /// 刷新
        /// </summary>
        public void Refresh()
        {
        }

        /// <summary>
        /// 打开开发者工具
        /// </summary>
        public void OpenDevTools()
        {
        }

        /// <summary>
        /// 触发导航完成事件
        /// </summary>
        protected virtual void OnNavigationCompleted(string url)
        {
            NavigationCompleted?.Invoke(this, url);
        }

        /// <summary>
        /// 触发源变化事件
        /// </summary>
        protected virtual void OnSourceChanged(string url)
        {
            SourceChanged?.Invoke(this, url);
        }
    }
}
