// CustomizedBrowser\App.xaml.cs
using CustomizedBrowser.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows;

namespace CustomizedBrowser
{
    /// <summary>
    /// 应用程序入口点
    /// 负责依赖注入容器配置和应用程序初始化
    /// </summary>
    public partial class App : Application
    {
        private IHost? _host;
        private Views.InstanceManagerWindow? _instanceManagerWindow;

        /// <summary>
        /// 应用程序启动事件处理
        /// </summary>
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // 设置为在最后一个窗口关闭时退出
            ShutdownMode = ShutdownMode.OnLastWindowClose;
            
            // 启动时清理之前残留的进程
            try
            {
                Core.ProcessCleanupHelper.CleanupAllProcesses();
            }
            catch (Exception)
            {
            }

            // 构建主机
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    ConfigureServices(services);
                })
                .Build();

            // 启动主机
            await _host.StartAsync();

            // 获取并显示实例管理窗口
            _instanceManagerWindow = _host.Services.GetRequiredService<Views.InstanceManagerWindow>();
            _instanceManagerWindow.Show();
        }

        /// <summary>
        /// 应用程序退出事件处理
        /// </summary>
        protected override async void OnExit(ExitEventArgs e)
        {
            try
            {
                // 先停止所有窗口，清理进程
                if (_host != null)
                {
                    var instanceService = _host.Services.GetService<Services.IInstanceService>();
                    if (instanceService != null)
                    {
                        await instanceService.StopAllInstancesAsync();
                    }
                }
                
                // 清理所有残留进程
                Core.ProcessCleanupHelper.CleanupAllProcesses();
            }
            catch (Exception)
            {
            }
            
            if (_host != null)
            {
                await _host.StopAsync();
                // 清理EnvironmentManager
                var environmentManager = _host.Services.GetService<Services.IWebViewEnvironmentManager>();
                environmentManager?.Dispose();
                _host.Dispose();
            }
            base.OnExit(e);
        }

        /// <summary>
        /// 配置依赖注入服务
        /// </summary>
        private void ConfigureServices(IServiceCollection services)
        {
            // 注册服务
            services.AddSingleton<Services.ISettingsService, Services.SettingsService>();
            services.AddSingleton<Services.IInstanceService, Services.InstanceService>();
            services.AddSingleton<Services.ISessionService, Services.SessionService>();
            services.AddSingleton<Services.ITrayIconService, Services.TrayIconService>();
            services.AddTransient<Services.IWebViewService, Services.WebViewService>();
            services.AddSingleton<Services.IBrowserWindowFactory, Services.BrowserWindowFactory>();
            services.AddSingleton<Services.IWebViewEnvironmentManager, Services.WebViewEnvironmentManager>();
            services.AddSingleton<Services.ICacheService, Services.CacheService>();
            services.AddSingleton<Services.IInstancePackageService, Services.InstancePackageService>();
            services.AddSingleton<Services.ISiteDataService, Services.SiteDataService>();
            services.AddSingleton<Services.ISiteTransferService, Services.SiteTransferService>();

            // 注册视图模型
            services.AddTransient<ViewModels.InstanceManagerViewModel>();
            services.AddTransient<ViewModels.SettingsViewModel>();

            // 注册视图
            services.AddSingleton<Views.InstanceManagerWindow>();
            services.AddTransient<Views.SettingsWindow>();
            services.AddTransient<Views.MergeInstancesWindow>();
            // ExtractSiteWindow 需要动态传入源实例 ID，通过 ActivatorUtilities 创建，不直接注册
            // BrowserWindow 通过工厂创建，不直接注册
        }
    }
}
