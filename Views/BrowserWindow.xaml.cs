// CustomizedBrowser\Views\BrowserWindow.xaml.cs
using CustomizedBrowser.Core;
using CustomizedBrowser.Models;
using CustomizedBrowser.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace CustomizedBrowser.Views
{
    /// <summary>
    /// 标签页数据
    /// </summary>
    public class TabData
    {
        public WebView2 WebView { get; set; } = null!;
        public string InitialUrl { get; set; } = string.Empty;
    }

    /// <summary>
    /// 浏览器窗口
    /// 负责显示 WebView2 控件和提供基本的浏览器功能
    /// </summary>
    public partial class BrowserWindow : Window, IDisposable
    {
        private readonly string _instanceId;
        private readonly string _userDataFolder;
        private readonly ISessionService _sessionService;
        private readonly IInstanceService _instanceService;
        private readonly IServiceProvider _serviceProvider;
        private readonly Services.IWebViewEnvironmentManager _environmentManager;
        private readonly ISettingsService _settingsService;
        private CoreWebView2Environment? _webViewEnvironment;
        private readonly Dictionary<WebView2, TabItem> _webViewToTabItem = new();
        private bool _disposed = false;
        private System.Threading.Timer? _processIdsSaveTimer;
        private bool _processIdsSavePending = false;
        
        // 加载进度条相关
        private DispatcherTimer? _loadingProgressTimer;
        private DispatcherTimer? _hideProgressTimer;
        private double _currentLoadingProgress = 0;
        private bool _isLoading = false;
        
        // UI更新去抖相关
        private DateTime _lastTitleUpdateTime = DateTime.MinValue;
        private const int TitleUpdateDebounceMs = 500;

        /// <summary>
        /// 实例ID
        /// </summary>
        public string InstanceId => _instanceId;

        /// <summary>
        /// 当前活动的 WebView
        /// </summary>
        private WebView2? CurrentWebView
        {
            get
            {
                if (TabControl.SelectedItem is TabItem tabItem && tabItem.Content is WebView2 wv)
                {
                    return wv;
                }
                return null;
            }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public BrowserWindow(
            string instanceId,
            ISessionService sessionService,
            IInstanceService instanceService,
            IServiceProvider serviceProvider,
            Services.IWebViewEnvironmentManager environmentManager,
            ISettingsService settingsService)
        {
            _instanceId = instanceId;
            _sessionService = sessionService;
            _instanceService = instanceService;
            _serviceProvider = serviceProvider;
            _environmentManager = environmentManager;
            _settingsService = settingsService;
            _userDataFolder = AppConstants.GetInstanceDataPath(instanceId);

            InitializeComponent();
            KeyDown += BrowserWindow_KeyDown;
            Closed += BrowserWindow_Closed;
            
            // 订阅标签页事件
            TabControl.SelectionChanged += TabControl_SelectionChanged;
            TabControl.TabClosing += TabControl_TabClosing;

            Title = $"Customized Browser - {instanceId}";
            
            _ = InitializeBrowserAsync();
        }

        /// <summary>
        /// 获取当前默认页面URL（根据用户设置动态计算）
        /// </summary>
        /// <returns>默认页面URL</returns>
        private string GetDefaultPageUrl()
        {
            var settings = _settingsService.CurrentSettings;
            
            // 如果启用了本地默认页面，返回本地页面URL
            if (settings.UseLocalDefaultPage)
            {
                return LocalPageHelper.GetLocalPageUrl(settings.CustomLocalPagePath);
            }
            
            // 否则返回用户设置的默认页面URL
            return settings.DefaultPageUrl;
        }

        /// <summary>
        /// 初始化浏览器
        /// </summary>
        private async System.Threading.Tasks.Task InitializeBrowserAsync()
        {
            try
            {
                Directory.CreateDirectory(_userDataFolder);

                // 使用缓存的Environment，避免重复创建
                _webViewEnvironment = await _environmentManager.GetOrCreateEnvironmentAsync(_instanceId);

                await LoadSessionStateAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化浏览器失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 加载会话状态
        /// </summary>
        private async System.Threading.Tasks.Task LoadSessionStateAsync()
        {
            if (_webViewEnvironment == null)
            {
                return;
            }

            var sessionState = _sessionService.LoadSession(_instanceId);
            if (sessionState != null && sessionState.Tabs.Count > 0)
            {
                TabItem? activeTabItem = null;
                string activeUrl = GetDefaultPageUrl();
                var webViewTasks = new List<System.Threading.Tasks.Task>();
                
                // 第一步：先一次性创建所有标签页UI，让用户立即看到所有标签页
                for (int i = 0; i < sessionState.Tabs.Count; i++)
                {
                    var tabState = sessionState.Tabs[i];
                    bool isActive = i == sessionState.ActiveTabIndex;
                    
                    string url = tabState.Url;
                    if (string.IsNullOrWhiteSpace(url))
                    {
                        url = GetDefaultPageUrl();
                    }
                    
                    var tabItem = new TabItem
                    {
                        Header = GetShortUrl(url)
                    };
                    var webView = new WebView2();
                    webView.Tag = new TabData { WebView = webView, InitialUrl = url };
                    tabItem.Content = webView;
                    
                    TabControl.AddTab(tabItem);
                    _webViewToTabItem[webView] = tabItem;
                    
                    if (isActive)
                    {
                        activeTabItem = tabItem;
                        activeUrl = url;
                    }
                }
                
                // 立即设置活动标签页，更新UI
                if (activeTabItem != null)
                {
                    TabControl.SelectTab(activeTabItem);
                    AddressBar.Text = activeUrl;
                    UpdateWindowTitle();
                }
                
                // 第二步：异步初始化所有WebView，不阻塞UI
                for (int i = 0; i < TabControl.TabItems.Count; i++)
                {
                    if (TabControl.TabItems[i] is TabItem tabItem && tabItem.Content is WebView2 webView)
                    {
                        var tabState = sessionState.Tabs[i];
                        bool isActive = i == sessionState.ActiveTabIndex;
                        string url = tabState.Url;
                        if (string.IsNullOrWhiteSpace(url))
                        {
                            url = GetDefaultPageUrl();
                        }
                        
                        webViewTasks.Add(InitializeWebViewAsync(webView, tabItem, url, isActive));
                    }
                }
                
                // 等待所有WebView初始化完成（但UI已经显示了）
                await System.Threading.Tasks.Task.WhenAll(webViewTasks);
                
                // 所有WebView初始化完成后，触发保存进程ID（去抖机制）
                TriggerSaveProcessIds();
            }
            else
            {
                string lastPageUrl = _sessionService.LoadLastPage(_instanceId, GetDefaultPageUrl());
                await CreateNewTabAsync(lastPageUrl);
            }
        }

        /// <summary>
        /// 获取短URL作为临时标题
        /// </summary>
        private string GetShortUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                return uri.Host.Length > 20 ? uri.Host.Substring(0, 20) + "..." : uri.Host;
            }
            catch
            {
                return "新标签页";
            }
        }

        /// <summary>
        /// 初始化WebView
        /// </summary>
        private async System.Threading.Tasks.Task InitializeWebViewAsync(WebView2 webView, TabItem tabItem, string url, bool isActive)
        {
            try
            {
                if (_webViewEnvironment == null)
                {
                    return;
                }

                // 强制WebView2初始化，即使在不可见状态
                await webView.EnsureCoreWebView2Async(_webViewEnvironment);

                webView.CoreWebView2.Settings.IsGeneralAutofillEnabled = true;
                webView.CoreWebView2.Settings.IsPasswordAutosaveEnabled = true;
                webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = true;
                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
                webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
                webView.CoreWebView2.Settings.IsScriptEnabled = true;
                webView.CoreWebView2.Settings.IsWebMessageEnabled = true;

                webView.SourceChanged += WebView_SourceChanged;
                webView.NavigationStarting += WebView_NavigationStarting;
                webView.NavigationCompleted += WebView_NavigationCompleted;
                webView.CoreWebView2.DocumentTitleChanged += WebView_DocumentTitleChanged;
                webView.CoreWebView2.NewWindowRequested += WebView_NewWindowRequested;
                
                webView.CoreWebView2.Navigate(url);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化WebView失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 保存会话状态（异步执行，避免阻塞UI线程）
        /// </summary>
        private void SaveSessionState()
        {
            try
            {
                var sessionState = new SessionState();
                sessionState.ActiveTabIndex = TabControl.SelectedIndex;

                for (int i = 0; i < TabControl.ItemCount; i++)
                {
                    if (TabControl.TabItems[i] is TabItem tabItem && tabItem.Content is WebView2 webView)
                    {
                        // 统一使用GetTabUrl获取URL（优先Source，回退Tag的InitialUrl，最后默认页）
                        var tabState = new TabState
                        {
                            Url = GetTabUrl(webView)
                        };
                        sessionState.Tabs.Add(tabState);
                    }
                }

                // 在后台线程执行文件I/O，避免阻塞UI线程
                System.Threading.Tasks.Task.Run(() =>
                {
                    _sessionService.SaveSession(_instanceId, sessionState);
                });
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 创建新标签页
        /// </summary>
        private async System.Threading.Tasks.Task CreateNewTabAsync(string url, bool setActive = true)
        {
            try
            {
                var tabItem = new TabItem
                {
                    Header = "新标签页"
                };

                var webView = new WebView2();
                webView.Tag = new TabData { WebView = webView, InitialUrl = url };
                tabItem.Content = webView;

                TabControl.AddTab(tabItem);
                _webViewToTabItem[webView] = tabItem;

                if (setActive)
                {
                    TabControl.SelectTab(tabItem);
                }

                if (_webViewEnvironment != null)
                {
                    await InitializeWebViewAsync(webView, tabItem, url, setActive);
                    // 新标签页创建完成后，触发保存进程ID（去抖机制）
                    TriggerSaveProcessIds();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建标签页失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 关闭当前标签页
        /// </summary>
        private void CloseCurrentTab()
        {
            if (TabControl.ItemCount <= 1)
            {
                return;
            }

            // 先获取当前选中的索引，确保我们关闭的是正确的标签页
            int selectedIndex = TabControl.SelectedIndex;
            if (selectedIndex >= 0 && selectedIndex < TabControl.ItemCount)
            {
                if (TabControl.TabItems[selectedIndex] is TabItem tabItem)
                {
                    // 先释放WebView资源（内部会清理_webViewToTabItem字典）
                    DisposeWebView(tabItem);
                    
                    // 计算新的选中索引（避免越界）
                    int newIndex = Math.Min(selectedIndex, TabControl.ItemCount - 1);
                    
                    // 从TabControl中移除
                    TabControl.RemoveTabAt(selectedIndex);

                    // 移除后确保有新的标签页被选中
                    if (TabControl.ItemCount > 0)
                    {
                        TabControl.SelectIndex(newIndex);

                        // 手动更新地址栏和窗口标题（确保切换立即生效）
                        if (TabControl.SelectedItem is TabItem newTabItem && newTabItem.Content is WebView2 newWebView)
                        {
                            AddressBar.Text = GetTabUrl(newWebView);
                            UpdateWindowTitle();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 释放 WebView 资源
        /// </summary>
        private void DisposeWebView(TabItem tabItem)
        {
            if (tabItem.Content is WebView2 webView)
            {
                try
                {
                    _webViewToTabItem.Remove(webView);

                    webView.SourceChanged -= WebView_SourceChanged;
                    webView.NavigationStarting -= WebView_NavigationStarting;
                    webView.NavigationCompleted -= WebView_NavigationCompleted;
                    
                    if (webView.CoreWebView2 != null)
                    {
                        webView.CoreWebView2.DocumentTitleChanged -= WebView_DocumentTitleChanged;
                        webView.CoreWebView2.NewWindowRequested -= WebView_NewWindowRequested;
                        try
                        {
                            webView.CoreWebView2.Stop();
                        }
                        catch
                        {
                        }
                    }
                    
                    webView.Dispose();
                }
                catch (Exception)
                {
                }
            }
        }

        /// <summary>
        /// 触发保存进程ID（使用去抖机制，避免频繁写入）
        /// </summary>
        private void TriggerSaveProcessIds()
        {
            _processIdsSavePending = true;
            
            // 如果定时器尚未创建，创建一个（延迟500ms执行）
            if (_processIdsSaveTimer == null)
            {
                _processIdsSaveTimer = new System.Threading.Timer(
                    async (state) => await SaveProcessIdsAsync(),
                    null,
                    500,
                    System.Threading.Timeout.Infinite);
            }
            else
            {
                // 重置定时器，延迟500ms后执行
                _processIdsSaveTimer.Change(500, System.Threading.Timeout.Infinite);
            }
        }

        /// <summary>
        /// 异步保存进程ID
        /// </summary>
        private async System.Threading.Tasks.Task SaveProcessIdsAsync()
        {
            if (!_processIdsSavePending)
            {
                return;
            }
            _processIdsSavePending = false;

            try
            {
                var processIds = new List<int>();
                
                // 在UI线程获取进程ID
                await Dispatcher.InvokeAsync(() =>
                {
                    processIds.Clear();
                    foreach (TabItem tabItem in TabControl.TabItems)
                    {
                        if (tabItem.Content is WebView2 webView && webView.CoreWebView2 != null)
                        {
                            int processId = (int)webView.CoreWebView2.BrowserProcessId;
                            if (processId > 0 && !processIds.Contains(processId))
                            {
                                processIds.Add(processId);
                            }
                        }
                    }
                });

                // 在后台线程写入文件
                var content = new { ProcessIds = processIds };
                string json = JsonSerializer.Serialize(content, new JsonSerializerOptions { WriteIndented = true });
                string processIdFile = AppConstants.GetProcessIdsFilePath(_instanceId);
                await System.IO.File.WriteAllTextAsync(processIdFile, json);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 更新窗口标题
        /// </summary>
        private void UpdateWindowTitle()
        {
            if (TabControl.SelectedItem is TabItem tabItem)
            {
                string title = tabItem.Header?.ToString() ?? "新标签页";
                Title = $"{title} - Customized Browser - {_instanceId}";
            }
        }

        /// <summary>
        /// 后退按钮点击事件
        /// </summary>
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentWebView?.CanGoBack == true)
            {
                CurrentWebView.GoBack();
            }
        }

        /// <summary>
        /// 前进按钮点击事件
        /// </summary>
        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentWebView?.CanGoForward == true)
            {
                CurrentWebView.GoForward();
            }
        }

        /// <summary>
        /// 刷新按钮点击事件
        /// </summary>
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            CurrentWebView?.Reload();
        }

        /// <summary>
        /// 主页按钮点击事件
        /// </summary>
        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            var instanceManagerWindow = _serviceProvider.GetRequiredService<InstanceManagerWindow>();
            
            if (!instanceManagerWindow.IsVisible)
            {
                instanceManagerWindow.Show();
            }
            else
            {
                instanceManagerWindow.Activate();
            }
        }

        /// <summary>
        /// 转到按钮点击事件
        /// </summary>
        private void GoButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateToAddress();
        }

        /// <summary>
        /// 地址栏键盘事件
        /// </summary>
        private void AddressBar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                NavigateToAddress();
            }
        }

        /// <summary>
        /// 判断输入是否像是URL地址
        /// </summary>
        private bool LooksLikeUrl(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            // 如果包含空格，视为搜索词
            if (input.Contains(' '))
            {
                return false;
            }

            // 如果已经是标准URL格式
            if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // 如果是localhost或IP地址
            if (input.StartsWith("localhost", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // 如果包含点号（如 google.com），视为URL
            if (input.Contains('.'))
            {
                // 检查是否有非法URL字符（简单检查）
                return true;
            }

            return false;
        }

        /// <summary>
        /// 导航到地址（支持搜索词）
        /// </summary>
        private void NavigateToAddress()
        {
            string input = AddressBar.Text.Trim();
            if (!string.IsNullOrEmpty(input) && CurrentWebView?.CoreWebView2 != null)
            {
                string url;
                if (LooksLikeUrl(input))
                {
                    // 是URL，补全协议
                    if (!input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                        !input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        url = "https://" + input;
                    }
                    else
                    {
                        url = input;
                    }
                }
                else
                {
                    // 不是URL，使用搜索引擎搜索
                    url = $"https://www.google.com/search?q={Uri.EscapeDataString(input)}";
                }

                CurrentWebView.CoreWebView2.Navigate(url);
            }
        }

        /// <summary>
        /// 新建标签页按钮点击事件
        /// </summary>
        private async void NewTabButton_Click(object sender, RoutedEventArgs e)
        {
            await CreateNewTabAsync(GetDefaultPageUrl());
        }

        /// <summary>
        /// 关闭标签页按钮点击事件
        /// </summary>
        private void CloseTabButton_Click(object sender, RoutedEventArgs e)
        {
            CloseCurrentTab();
        }

        /// <summary>
        /// 开发者工具按钮点击事件
        /// </summary>
        private void DevToolsButton_Click(object sender, RoutedEventArgs e)
        {
            CurrentWebView?.CoreWebView2?.OpenDevToolsWindow();
        }

        /// <summary>
        /// 设置按钮点击事件
        /// </summary>
        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settingsWindow = _serviceProvider.GetRequiredService<Views.SettingsWindow>();
                settingsWindow.Owner = this;
                settingsWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开设置窗口失败: {ex.Message}\n\n{ex.ToString()}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 获取标签页的URL（优先使用 Source，回退到 Tag 中保存的 InitialUrl）
        /// </summary>
        private string GetTabUrl(WebView2 webView)
        {
            if (webView?.Source != null)
            {
                return webView.Source.ToString();
            }
            if (webView?.Tag is TabData tabData && !string.IsNullOrEmpty(tabData.InitialUrl))
            {
                return tabData.InitialUrl;
            }
            return GetDefaultPageUrl();
        }

        /// <summary>
        /// 标签页选择变化事件
        /// </summary>
        private void TabControl_SelectionChanged(object? sender, EventArgs e)
        {
            if (TabControl.SelectedItem is TabItem tabItem && tabItem.Content is WebView2 webView)
            {
                AddressBar.Text = GetTabUrl(webView);
                UpdateWindowTitle();
                
                // 检查WebView是否正在加载，如果是则显示进度条
                if (webView.CoreWebView2 != null)
                {
                    // 如果CoreWebView2还在初始化或正在导航，显示加载进度
                    if (webView.CoreWebView2.Source == null || 
                        webView.CoreWebView2.Source.ToString() == "about:blank")
                    {
                        StartLoadingProgress();
                    }
                    else
                    {
                        // 已有内容，隐藏进度条
                        HideLoadingProgress();
                    }
                }
            }
        }

        /// <summary>
        /// 标签页关闭事件（释放WebView2资源）
        /// </summary>
        private void TabControl_TabClosing(TabItem tabItem)
        {
            if (tabItem != null)
            {
                // 释放WebView2资源
                DisposeWebView(tabItem);
                
                // 保存会话状态
                SaveSessionState();
                
                // 触发保存进程ID
                TriggerSaveProcessIds();
            }
        }

        /// <summary>
        /// WebView 源变化事件
        /// </summary>
        private void WebView_SourceChanged(object? sender, CoreWebView2SourceChangedEventArgs e)
        {
            if (sender is WebView2 webView && webView == CurrentWebView)
            {
                AddressBar.Text = webView.Source?.ToString() ?? "";
            }
        }

        /// <summary>
        /// WebView 导航开始事件
        /// </summary>
        private void WebView_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            // 如果是当前活动的WebView，显示加载进度
            if (sender is WebView2 webView && webView == CurrentWebView)
            {
                StartLoadingProgress();
            }
        }

        /// <summary>
        /// WebView 导航完成事件
        /// </summary>
        private void WebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (sender is WebView2 webView)
            {
                // 如果是当前活动的WebView，完成加载进度
                if (webView == CurrentWebView)
                {
                    CompleteLoadingProgress();
                }
                
                if (webView.Source != null)
                {
                    // 异步保存，避免阻塞 UI 线程
                    System.Threading.Tasks.Task.Run(() => {
                        _sessionService.SaveLastPage(_instanceId, webView.Source.ToString());
                    });
                }
            }
        }

        /// <summary>
        /// 开始加载进度显示
        /// </summary>
        private void StartLoadingProgress()
        {
            _isLoading = true;
            _currentLoadingProgress = 0;
            
            // 显示进度条
            LoadingProgressBar.Visibility = Visibility.Visible;
            LoadingProgressBar.Value = 0;
            
            // 初始化并启动进度定时器（使用200ms间隔减少Dispatcher压力）
            if (_loadingProgressTimer == null)
            {
                _loadingProgressTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(200)
                };
                _loadingProgressTimer.Tick += LoadingProgressTimer_Tick;
            }
            _loadingProgressTimer.Start();
        }

        /// <summary>
        /// 进度条定时器Tick事件
        /// </summary>
        private void LoadingProgressTimer_Tick(object? sender, EventArgs e)
        {
            if (!_isLoading)
            {
                return;
            }
            
            // 渐进式增加进度，模拟真实加载
            // 前期快，中后期慢
            if (_currentLoadingProgress < 30)
            {
                _currentLoadingProgress += 5;
            }
            else if (_currentLoadingProgress < 70)
            {
                _currentLoadingProgress += 2;
            }
            else if (_currentLoadingProgress < 90)
            {
                _currentLoadingProgress += 1;
            }
            else
            {
                _currentLoadingProgress += 0.5;
            }
            
            // 限制最大值为95%，等导航完成时跳到100%
            if (_currentLoadingProgress > 95)
            {
                _currentLoadingProgress = 95;
            }
            
            LoadingProgressBar.Value = _currentLoadingProgress;
        }

        /// <summary>
        /// 完成加载进度
        /// </summary>
        private void CompleteLoadingProgress()
        {
            _isLoading = false;
            
            if (_loadingProgressTimer != null)
            {
                _loadingProgressTimer.Stop();
            }
            
            // 快速填满到100%然后隐藏
            LoadingProgressBar.Value = 100;
            
            // 延迟一小段时间后隐藏，让用户看到完成状态
            // 先停止旧的定时器，避免堆积
            if (_hideProgressTimer != null)
            {
                _hideProgressTimer.Stop();
                _hideProgressTimer.Tick -= HideProgressTimer_Tick;
                _hideProgressTimer = null;
            }
            
            _hideProgressTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _hideProgressTimer.Tick += HideProgressTimer_Tick;
            _hideProgressTimer.Start();
        }
        
        /// <summary>
        /// 隐藏进度条定时器Tick事件
        /// </summary>
        private void HideProgressTimer_Tick(object? sender, EventArgs e)
        {
            LoadingProgressBar.Visibility = Visibility.Collapsed;
            LoadingProgressBar.Value = 0;
            
            if (_hideProgressTimer != null)
            {
                _hideProgressTimer.Stop();
                _hideProgressTimer.Tick -= HideProgressTimer_Tick;
                _hideProgressTimer = null;
            }
        }

        /// <summary>
        /// 隐藏加载进度条
        /// </summary>
        private void HideLoadingProgress()
        {
            _isLoading = false;
            
            if (_loadingProgressTimer != null)
            {
                _loadingProgressTimer.Stop();
            }
            
            LoadingProgressBar.Visibility = Visibility.Collapsed;
            LoadingProgressBar.Value = 0;
        }

        /// <summary>
        /// WebView 文档标题变化事件（带去抖逻辑，避免高频UI刷新）
        /// </summary>
        private void WebView_DocumentTitleChanged(object? sender, object e)
        {
            if (sender is WebView2 webView)
            {
                // 使用字典快速查找对应的 TabItem
                if (_webViewToTabItem.TryGetValue(webView, out TabItem? tabItem))
                {
                    // 去抖逻辑：距离上次更新不足500ms则跳过
                    // 但如果是当前活动标签页，则立即更新，以确保用户体验
                    bool isActiveTab = TabControl.SelectedItem == tabItem;
                    DateTime now = DateTime.Now;
                    
                    if (!isActiveTab && (now - _lastTitleUpdateTime).TotalMilliseconds < TitleUpdateDebounceMs)
                    {
                        return;
                    }
                    _lastTitleUpdateTime = now;
                    
                    string title = webView.CoreWebView2?.DocumentTitle ?? "新标签页";
                    string displayTitle = title.Length > 20 ? title.Substring(0, 20) + "..." : title;
                    
                    // 使用ScrollableTabControl的UpdateTabHeader方法更新标题
                    TabControl.UpdateTabHeader(tabItem, displayTitle);
                    
                    // 检查是否是当前活动标签页
                    if (isActiveTab)
                    {
                        UpdateWindowTitle();
                    }
                }
            }
        }

        /// <summary>
        /// WebView 新窗口请求事件
        /// </summary>
        private async void WebView_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            e.Handled = true;
            await CreateNewTabAsync(e.Uri);
        }

        /// <summary>
        /// 键盘事件处理（支持标准浏览器快捷键）
        /// </summary>
        private void BrowserWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.T:
                        NewTabButton_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.W:
                        CloseTabButton_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.R:
                        RefreshButton_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.L:
                        // Ctrl+L：聚焦地址栏并选中全部文本
                        AddressBar.Focus();
                        AddressBar.SelectAll();
                        e.Handled = true;
                        break;
                }
            }
            else if (Keyboard.Modifiers == ModifierKeys.Alt)
            {
                switch (e.Key)
                {
                    case Key.Left:
                        BackButton_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.Right:
                        ForwardButton_Click(sender, e);
                        e.Handled = true;
                        break;
                }
            }
            else if (e.Key == Key.F12)
            {
                DevToolsButton_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.F5)
            {
                // F5：刷新
                RefreshButton_Click(sender, e);
                e.Handled = true;
            }
        }

        /// <summary>
        /// 窗口关闭事件
        /// </summary>
        private void BrowserWindow_Closed(object? sender, EventArgs e)
        {
            Dispose();
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
        /// 同步保存进程ID（用于Dispose时立即保存）
        /// </summary>
        private void SaveProcessIdsSync()
        {
            try
            {
                var processIds = new List<int>();
                foreach (TabItem tabItem in TabControl.TabItems)
                {
                    if (tabItem.Content is WebView2 webView && webView.CoreWebView2 != null)
                    {
                        int processId = (int)webView.CoreWebView2.BrowserProcessId;
                        if (processId > 0 && !processIds.Contains(processId))
                        {
                            processIds.Add(processId);
                        }
                    }
                }

                var content = new { ProcessIds = processIds };
                string json = JsonSerializer.Serialize(content, new JsonSerializerOptions { WriteIndented = true });
                string processIdFile = AppConstants.GetProcessIdsFilePath(_instanceId);
                System.IO.File.WriteAllText(processIdFile, json);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 清理所有定时器
        /// </summary>
        private void CleanupTimers()
        {
            // 清理进程ID保存定时器
            _processIdsSaveTimer?.Dispose();
            _processIdsSaveTimer = null;
            
            // 清理加载进度条定时器
            if (_loadingProgressTimer != null)
            {
                _loadingProgressTimer.Stop();
                _loadingProgressTimer.Tick -= LoadingProgressTimer_Tick;
                _loadingProgressTimer = null;
            }
            
            // 清理隐藏进度条定时器
            if (_hideProgressTimer != null)
            {
                _hideProgressTimer.Stop();
                _hideProgressTimer.Tick -= HideProgressTimer_Tick;
                _hideProgressTimer = null;
            }
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
                try
                {
                    // 清理所有定时器
                    CleanupTimers();

                    SaveSessionState();
                    
                    if (TabControl.SelectedItem is TabItem selectedTab && selectedTab.Content is WebView2 webView && webView.Source != null)
                    {
                        _sessionService.SaveLastPage(_instanceId, webView.Source.ToString());
                    }

                    // 先同步保存进程ID，再清理WebView
                    SaveProcessIdsSync();

                    // 清理所有WebView
                    foreach (TabItem tabItem in TabControl.TabItems)
                    {
                        DisposeWebView(tabItem);
                    }

                    // 清理WebView后，杀掉相关进程
                    Core.ProcessCleanupHelper.CleanupInstanceProcesses(_instanceId);
                }
                catch (Exception)
                {
                }
            }

            _disposed = true;
        }

        /// <summary>
        /// 析构函数
        /// </summary>
        ~BrowserWindow()
        {
            Dispose(false);
        }
    }
}
