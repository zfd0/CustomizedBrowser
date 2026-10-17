// CustomizedBrowser\Services\BrowserWindowFactory.cs
using CustomizedBrowser.Views;
using Microsoft.Extensions.DependencyInjection;

namespace CustomizedBrowser.Services
{
    /// <summary>
    /// 浏览器窗口工厂实现
    /// 负责创建和管理浏览器窗口实例
    /// </summary>
    public class BrowserWindowFactory : IBrowserWindowFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ISettingsService _settingsService;

        /// <summary>
        /// 构造函数
        /// </summary>
        public BrowserWindowFactory(IServiceProvider serviceProvider, ISettingsService settingsService)
        {
            _serviceProvider = serviceProvider;
            _settingsService = settingsService;
        }

        /// <summary>
        /// 创建浏览器窗口
        /// </summary>
        public BrowserWindow CreateBrowserWindow(string instanceId)
        {
            var sessionService = _serviceProvider.GetRequiredService<ISessionService>();
            var instanceService = _serviceProvider.GetRequiredService<IInstanceService>();
            var environmentManager = _serviceProvider.GetRequiredService<IWebViewEnvironmentManager>();
            
            // 创建浏览器窗口并应用分辨率设置
            var window = new BrowserWindow(instanceId, sessionService, instanceService, _serviceProvider, environmentManager, _settingsService);
            
            // 从设置中获取默认分辨率并应用到窗口
            var settings = _settingsService.CurrentSettings;
            window.Width = settings.DefaultWindowWidth;
            window.Height = settings.DefaultWindowHeight;
            
            return window;
        }
    }
}
