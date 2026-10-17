// CustomizedBrowser\Core\CookieHostWindow.cs
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace CustomizedBrowser.Core
{
    /// <summary>
    /// 隐藏的 WebView2 宿主窗口
    /// 用于在不打开可见浏览器窗口的情况下，读取/写入指定实例的 Cookie（登录状态）
    /// </summary>
    public sealed class CookieHostWindow : Window
    {
        private readonly WebView2 _webView = new WebView2();

        /// <summary>
        /// 当前宿主窗口内的 CoreWebView2 实例
        /// </summary>
        public CoreWebView2? Core { get; private set; }

        /// <summary>
        /// 构造函数
        /// 将窗口配置为位于屏幕外、不激活、不显示在任务栏的不可见窗口
        /// </summary>
        public CookieHostWindow()
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Width = 100;
            Height = 100;
            Left = -32000;
            Top = -32000;
            Content = _webView;
        }

        /// <summary>
        /// 使用指定实例的环境初始化 WebView2
        /// </summary>
        /// <param name="environment">实例对应的 CoreWebView2Environment</param>
        public async Task InitializeAsync(CoreWebView2Environment environment)
        {
            // 显示离屏窗口，使 WebView2 控件获得有效的窗口句柄，从而能够创建控制器
            Show();
            await _webView.EnsureCoreWebView2Async(environment);
            Core = _webView.CoreWebView2;

            // 隐藏窗口仅用于数据读写，禁用脚本对话框，避免站点弹窗阻塞脚本执行
            Core.Settings.AreDefaultScriptDialogsEnabled = false;
        }

        /// <summary>
        /// 导航到指定地址并等待加载完成（用于读取/写入站点存储）
        /// </summary>
        /// <param name="url">目标地址</param>
        /// <param name="timeoutMs">等待超时时间（毫秒）</param>
        /// <returns>是否导航成功</returns>
        public async Task<bool> NavigateAndWaitAsync(string url, int timeoutMs)
        {
            if (Core == null)
            {
                throw new InvalidOperationException("WebView2 尚未初始化，无法导航。");
            }

            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
            {
                completion.TrySetResult(e.IsSuccess);
            }

            Core.NavigationCompleted += OnNavigationCompleted;
            try
            {
                Core.Navigate(url);

                // 等待导航完成或超时，超时视为失败
                var finished = await Task.WhenAny(completion.Task, Task.Delay(timeoutMs));
                return finished == completion.Task && completion.Task.Result;
            }
            finally
            {
                Core.NavigationCompleted -= OnNavigationCompleted;
            }
        }

        /// <summary>
        /// 关闭并释放 WebView2 资源
        /// </summary>
        public void Shutdown()
        {
            try
            {
                _webView.Dispose();
            }
            catch (Exception)
            {
            }

            try
            {
                Close();
            }
            catch (Exception)
            {
            }
        }
    }
}
