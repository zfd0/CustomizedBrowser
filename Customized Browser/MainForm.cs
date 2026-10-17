// MainForm.cs
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace SimpleBrowser
{
    public partial class MainForm : Form
    {
        // 浏览器控件
        private TabControl tabControl;
        private ToolStrip toolStrip;
        private TextBox addressBar;
        private StatusStrip statusBar;

        // 缓存目录 - 每个实例独立
        private string userDataFolder;
        private string instanceId;

        // 当前浏览器设置
        private BrowserSettings browserSettings;

        // 标签页数据结构
        private class TabData
        {
            public WebView2 WebView { get; set; }
            public string InitialUrl { get; set; }
            public string CustomTitle { get; set; } // 自定义标签页标题
            public double ScrollPositionY { get; set; } // 滚动位置Y坐标
        }
        
        // 实例ID属性（用于实例管理）
        public string InstanceId
        {
            get { return instanceId; }
        }

        // 隐藏窗口选项
        public enum HideWindowOption
        {
            Close = 0,           // 默认：关闭窗口即退出程序
            Ask = 1,             // 关闭窗口时询问
            KeepRunning = 2      // 关闭窗口时保持运行
        }

        // 实例级别的后台运行状态
        public bool isRunningInBackground = false;
        
        // 窗口置顶状态
        private bool isWindowPinned = false;
        
        // 全局应用程序上下文
        public static BrowserApplicationContext AppContext { get; set; }
        
        // 自定义消息常量
        private const uint WM_SHOW_INSTANCE_MANAGER = 0x0400 + 1001; // WM_USER + 1001

        public MainForm(string instanceName = "default")
        {
            instanceId = instanceName;
            
            // 为每个实例创建独立的用户数据目录
            userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SimpleBrowser",
                "Instances",
                instanceId
            );
            
            // 加载设置
            LoadBrowserSettings();
            
            InitializeUI();
            
            // 添加FormClosed事件处理程序
            this.FormClosed += MainForm_FormClosed;
        }

        private void InitializeUI()
        {
            // 窗体设置
            this.Text = $"Customized Browser - {instanceId}";
            this.Size = new Size(1200, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            // Windows 10/11风格设置
            this.Font = new Font("Segoe UI", 9);
            this.BackColor = Color.FromArgb(248, 248, 248);
            this.ForeColor = Color.FromArgb(31, 31, 35); // #1F1F23 深灰
            
            // 启用键盘快捷键处理
            this.KeyPreview = true;
            this.KeyDown += MainForm_KeyDown;

            // 工具栏
            toolStrip = new ToolStrip();
            // Windows 10/11风格工具栏设置
            toolStrip.Font = new Font("Segoe UI", 9);
            toolStrip.BackColor = Color.White;
            toolStrip.ForeColor = Color.FromArgb(31, 31, 35);
            toolStrip.RenderMode = ToolStripRenderMode.System;
            
            var backBtn = new ToolStripButton("←");
            var forwardBtn = new ToolStripButton("→");
            var refreshBtn = new ToolStripButton("↻");
            var homeBtn = new ToolStripButton("🏠");
            var newTabBtn = new ToolStripButton("+ 新建标签");
            var closeTabBtn = new ToolStripButton("× 关闭标签");
            var pinWindowBtn = new ToolStripButton("置顶");
            var devToolsBtn = new ToolStripButton("开发者工具 (F12)");
            var minimizeToTrayBtn = new ToolStripButton("最小化到托盘");
            var newInstanceBtn = new ToolStripButton("新建实例");
            var instanceManagerBtn = new ToolStripButton("实例管理");
            var settingsBtn = new ToolStripButton("设置");

            backBtn.Click += BackButton_Click;
            forwardBtn.Click += ForwardButton_Click;
            refreshBtn.Click += RefreshButton_Click;
            homeBtn.Click += HomeButton_Click;
            newTabBtn.Click += NewTabButton_Click;
            closeTabBtn.Click += CloseTabButton_Click;
            pinWindowBtn.Click += PinWindowButton_Click;
            devToolsBtn.Click += DevToolsButton_Click;
            minimizeToTrayBtn.Click += MinimizeToTrayButton_Click;
            newInstanceBtn.Click += NewInstanceButton_Click;
            instanceManagerBtn.Click += InstanceManagerButton_Click;
            settingsBtn.Click += SettingsButton_Click;

            // 设置按钮样式
            backBtn.ToolTipText = "后退 (Alt+左箭头)";
            forwardBtn.ToolTipText = "前进 (Alt+右箭头)";
            refreshBtn.ToolTipText = "刷新页面 (Ctrl+R)";
            homeBtn.ToolTipText = "返回默认首页";
            newTabBtn.ToolTipText = "新建标签页 (Ctrl+T)";
            closeTabBtn.ToolTipText = "关闭标签页 (Ctrl+W)";
            pinWindowBtn.ToolTipText = "置顶窗口 (Ctrl+Shift+T)";
            devToolsBtn.ToolTipText = "打开开发者工具 (F12)";
            minimizeToTrayBtn.ToolTipText = "最小化到托盘";
            newInstanceBtn.ToolTipText = "新建浏览器实例";
            instanceManagerBtn.ToolTipText = "管理浏览器实例";
            settingsBtn.ToolTipText = "打开设置";

            toolStrip.Items.Add(backBtn);
            toolStrip.Items.Add(forwardBtn);
            toolStrip.Items.Add(refreshBtn);
            toolStrip.Items.Add(homeBtn);
            toolStrip.Items.Add(new ToolStripSeparator());
            toolStrip.Items.Add(newTabBtn);
            toolStrip.Items.Add(closeTabBtn);
            toolStrip.Items.Add(new ToolStripSeparator());
            toolStrip.Items.Add(pinWindowBtn);
            toolStrip.Items.Add(devToolsBtn);
            toolStrip.Items.Add(minimizeToTrayBtn);
            toolStrip.Items.Add(new ToolStripSeparator());
            toolStrip.Items.Add(newInstanceBtn);
            toolStrip.Items.Add(instanceManagerBtn);
            toolStrip.Items.Add(settingsBtn);

            // 地址栏
            addressBar = new TextBox();
            addressBar.Dock = DockStyle.Fill;
            addressBar.KeyPress += AddressBar_KeyPress;
            addressBar.MinimumSize = new Size(400, 32); // 设置最小宽度
            // Windows 10/11风格地址栏设置
            addressBar.Font = new Font("Segoe UI", 9);
            addressBar.BorderStyle = BorderStyle.FixedSingle;
            addressBar.BackColor = Color.White;
            addressBar.ForeColor = Color.FromArgb(31, 31, 35);
            addressBar.Text = "输入网址、搜索关键词或粘贴链接";
            addressBar.GotFocus += (s, e) => { if (addressBar.Text == "输入网址、搜索关键词或粘贴链接") addressBar.Text = ""; };
            addressBar.LostFocus += (s, e) => { if (string.IsNullOrEmpty(addressBar.Text)) addressBar.Text = "输入网址、搜索关键词或粘贴链接"; };

            var goBtn = new ToolStripButton("转到");
            goBtn.Click += GoButton_Click;
            goBtn.ForeColor = Color.FromArgb(0, 120, 215); // #0078D7 微软蓝

            // 创建地址栏宿主并设置Spring属性使其自动填充空间
            ToolStripControlHost addressBarHost = new ToolStripControlHost(addressBar);
            addressBarHost.Width = 800; // 设置初始宽度
            addressBarHost.AutoSize = false; // 禁用自动大小，以便手动控制宽度

            toolStrip.Items.Add(new ToolStripSeparator());
            toolStrip.Items.Add(addressBarHost);
            toolStrip.Items.Add(goBtn);
            
            // 处理工具条大小变化事件，确保地址栏始终占用合适空间
            toolStrip.SizeChanged += (s, e) => {
                int totalButtonsWidth = 0;
                foreach (ToolStripItem item in toolStrip.Items) {
                    if (item != addressBarHost && !(item is ToolStripSeparator)) {
                        totalButtonsWidth += item.Width;
                    }
                }
                
                // 计算地址栏可用宽度（减去按钮和分隔符的宽度）
                int availableWidth = toolStrip.Width - totalButtonsWidth - 20; // 20是一些边距
                if (availableWidth > addressBar.MinimumSize.Width) {
                    addressBarHost.Width = availableWidth;
                }
                else {
                    addressBarHost.Width = addressBar.MinimumSize.Width;
                }
            };

            // 状态栏
            statusBar = new StatusStrip();
            // Windows 10/11风格状态栏设置
            statusBar.Font = new Font("Segoe UI", 11);
            statusBar.BackColor = Color.White;
            statusBar.ForeColor = Color.FromArgb(110, 110, 115); // #6E6E73 中灰
            statusBar.RenderMode = ToolStripRenderMode.System;
            
            var statusLabel = new ToolStripStatusLabel("就绪") { Name = "statusLabel" };
            var hideWindowOptionLabel = new ToolStripStatusLabel($"隐藏窗口: {browserSettings.HideWindowOption}") { Name = "hideWindowOptionLabel" };
            var instanceStatusLabel = new ToolStripStatusLabel($"实例: {instanceId}") { Name = "instanceStatusLabel" };
            
            statusBar.Items.Add(statusLabel);
            statusBar.Items.Add(new ToolStripStatusLabel("   "));
            statusBar.Items.Add(hideWindowOptionLabel);
            statusBar.Items.Add(new ToolStripStatusLabel("   "));
            statusBar.Items.Add(instanceStatusLabel);

            // 标签控件
            tabControl = new TabControl();
            tabControl.Dock = DockStyle.Fill;
            tabControl.Alignment = TabAlignment.Top;
            tabControl.Multiline = false;
            tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabControl.DrawItem += TabControl_DrawItem;
            tabControl.MouseDown += TabControl_MouseDown;
            tabControl.SelectedIndexChanged += TabControl_SelectedIndexChanged;
            tabControl.AllowDrop = true;
            tabControl.DragOver += TabControl_DragOver;
            tabControl.DragDrop += TabControl_DragDrop;
            
            // 设置标签页样式
            tabControl.Font = new Font("Segoe UI", 9);
            tabControl.BackColor = Color.White;
            tabControl.ForeColor = Color.FromArgb(31, 31, 35);

            // 添加到窗体
            this.Controls.Add(tabControl);
            this.Controls.Add(toolStrip);
            this.Controls.Add(statusBar);

            toolStrip.Dock = DockStyle.Top;
            statusBar.Dock = DockStyle.Bottom;

            // 根据设置初始化标签页
            InitializeTabs();
        }

        /// <summary>
        /// 初始化标签页
        /// </summary>
        private void InitializeTabs()
        {
            // 首先尝试恢复完整的会话状态
            if (browserSettings.RestoreSessionOnStartup)
            {
                SessionState sessionState = SettingsManager.LoadSessionState(instanceId);
                if (sessionState != null && sessionState.Tabs.Count > 0)
                {
                    for (int i = 0; i < sessionState.Tabs.Count; i++)
                    {
                        TabState tabState = sessionState.Tabs[i];
                        CreateNewTab(tabState, i == sessionState.ActiveTabIndex);
                    }
                    return;
                }
            }

            // 如果没有会话状态，则尝试恢复上次访问的页面
            string initialUrl = browserSettings.RestoreLastPageOnStartup ? LoadLastVisitedPage() : browserSettings.DefaultPageUrl;
            CreateNewTab(initialUrl);
        }

        /// <summary>
        /// 创建新标签页（带滚动位置恢复）
        /// </summary>
        /// <param name="tabState">标签页状态</param>
        /// <param name="setActive">是否设置为活动标签页</param>
        private async void CreateNewTab(TabState tabState, bool setActive = true)
        {
            // 调用重载方法，传递URL和滚动位置
            CreateNewTab(tabState.Url, setActive, tabState.ScrollPositionY);
        }

        /// <summary>
        /// 创建新标签页
        /// </summary>
        /// <param name="url">初始URL</param>
        /// <param name="setActive">是否设置为活动标签页</param>
        /// <param name="scrollPositionY">滚动位置Y坐标</param>
        private async void CreateNewTab(string url, bool setActive = true, double scrollPositionY = 0)
        {
            try
            {
                // 创建新的标签页
                TabPage tabPage = new TabPage();
                tabPage.Text = "加载中...";

                // 创建WebView2控件
                WebView2 webView = new WebView2();
                webView.Dock = DockStyle.Fill;
                webView.Tag = new TabData { WebView = webView, InitialUrl = url, CustomTitle = null, ScrollPositionY = scrollPositionY };

                // 绑定事件
                webView.NavigationStarting += WebView_NavigationStarting;
                webView.NavigationCompleted += WebView_NavigationCompleted;
                webView.SourceChanged += WebView_SourceChanged;
                webView.CoreWebView2InitializationCompleted += WebView_CoreWebView2InitializationCompleted;

                // 添加到标签页
                tabPage.Controls.Add(webView);
                tabControl.TabPages.Add(tabPage);
                if (setActive)
                {
                    tabControl.SelectedTab = tabPage;
                }

                // 确保用户数据目录存在
                Directory.CreateDirectory(userDataFolder);

                // 创建环境设置，配置长期缓存
                var environment = await CoreWebView2Environment.CreateAsync(
                    userDataFolder: userDataFolder,
                    options: new CoreWebView2EnvironmentOptions()
                    {
                        // 优化缓存设置
                        AdditionalBrowserArguments = "--disk-cache-size=1073741824 " + // 1GB缓存
                                                    "--disk-cache-dir=" + userDataFolder + "\\Cache " +
                                                    "--no-default-browser-check " +
                                                    "--disable-features=PasswordImport"
                    }
                );

                // 初始化WebView2
                await webView.EnsureCoreWebView2Async(environment);

                // 配置缓存设置，确保登录状态长期保存
                webView.CoreWebView2.Settings.IsGeneralAutofillEnabled = true;
                webView.CoreWebView2.Settings.IsPasswordAutosaveEnabled = true;
                webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = true;

                // 导航到指定URL
                webView.CoreWebView2.Navigate(url);
            }
            catch (Exception ex)
            {
                ShowStatusMessage($"创建标签页失败: {ex.Message}", Color.FromArgb(232, 17, 35)); // #E81123 红色
            }
        }

        /// <summary>
        /// 保存当前会话状态（所有标签页）
        /// </summary>
        private void SaveSessionState()
        {
            try
            {
                if (!browserSettings.RestoreSessionOnStartup)
                    return;

                SessionState sessionState = new SessionState();
                sessionState.ActiveTabIndex = tabControl.SelectedIndex;

                for (int i = 0; i < tabControl.TabPages.Count; i++)
                {
                    TabPage tabPage = tabControl.TabPages[i];
                    if (tabPage.Controls.Count > 0 && tabPage.Controls[0] is WebView2 webView)
                    {
                        TabState tabState = new TabState
                        {
                            Url = webView.Source?.ToString() ?? browserSettings.DefaultPageUrl,
                            Title = tabPage.Text,
                            IsActive = i == tabControl.SelectedIndex,
                            ScrollPositionY = 0 // 不保存滚动位置
                        };

                        sessionState.Tabs.Add(tabState);
                    }
                }

                SettingsManager.SaveSessionState(instanceId, sessionState);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 保存当前活跃标签页的URL到本地文件
        /// </summary>
        private void SaveLastVisitedPage()
        {
            try
            {
                if (tabControl.SelectedTab != null && tabControl.SelectedTab.Controls.Count > 0)
                {
                    var webView = (WebView2)tabControl.SelectedTab.Controls[0];
                    string url = webView.Source?.ToString() ?? "";
                    
                    if (!string.IsNullOrEmpty(url))
                    {
                        string lastPageFile = Path.Combine(userDataFolder, "last_page.json");
                        var content = new { Url = url };
                        string json = System.Text.Json.JsonSerializer.Serialize(content, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(lastPageFile, json);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存上次访问页面失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从本地文件加载上次访问的页面URL
        /// </summary>
        /// <returns>上次访问的页面URL，如果没有则返回默认URL</returns>
        private string LoadLastVisitedPage()
        {
            try
            {
                string lastPageFile = Path.Combine(userDataFolder, "last_page.json");
                if (File.Exists(lastPageFile))
                {
                    string json = File.ReadAllText(lastPageFile);
                    // 使用字典替代dynamic类型来避免依赖Microsoft.CSharp
                    var content = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, string>>(json);
                    return content?.ContainsKey("Url") == true ? content["Url"] : browserSettings.DefaultPageUrl;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载上次访问页面失败: {ex.Message}");
            }
            
            return "https://www.google.com";
        }
        
        /// <summary>
        /// 保存当前实例的进程ID到本地文件
        /// </summary>
        private void SaveProcessId()
        {
            try
            {
                List<int> processIds = new List<int>();
                
                // 遍历所有标签页，获取每个WebView2控件的进程ID
                foreach (TabPage tabPage in tabControl.TabPages)
                {
                    if (tabPage.Controls.Count > 0 && tabPage.Controls[0] is WebView2 webView && webView.CoreWebView2 != null)
                    {
                        try
                        {
                            int processId = (int)webView.CoreWebView2.BrowserProcessId;
                            if (processId > 0 && !processIds.Contains(processId))
                            {
                                processIds.Add(processId);
                            }
                        }
                        catch (Exception)
                        {
                            continue;
                        }
                    }
                }
                
                // 保存到文件
                string processIdFile = Path.Combine(userDataFolder, "process_ids.json");
                var content = new { ProcessIds = processIds };
                string json = System.Text.Json.JsonSerializer.Serialize(content, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(processIdFile, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存进程ID失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载浏览器设置
        /// </summary>
        private void LoadBrowserSettings()
        {
            browserSettings = SettingsManager.LoadSettings();
        }

        /// <summary>
        /// 保存浏览器设置
        /// </summary>
        private void SaveBrowserSettings()
        {
            SettingsManager.SaveSettings(browserSettings);
        }

        private void AddressBar_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                NavigateToAddress();
            }
        }

        private void GoButton_Click(object sender, EventArgs e)
        {
            NavigateToAddress();
        }

        private void NavigateToAddress()
        {
            try
            {
                if (tabControl.SelectedTab != null && tabControl.SelectedTab.Controls.Count > 0)
                {
                    var webView = (WebView2)tabControl.SelectedTab.Controls[0];
                    var url = addressBar.Text.Trim();
                    if (!string.IsNullOrEmpty(url))
                    {
                        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                        {
                            url = "https://" + url;
                        }
                        webView.CoreWebView2.Navigate(url);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导航错误: {ex.Message}");
            }
        }

        private void WebView_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            var webView = (WebView2)sender;
            ShowStatusMessage($"正在加载: {e.Uri}", Color.FromArgb(110, 110, 115));
        }

        private void WebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            var webView = (WebView2)sender;
            var tabPage = (TabPage)webView.Parent;
            
            if (tabPage != null)
            {
                // 检查是否有自定义标题
                var tabData = (TabData)webView.Tag;
                if (tabData.CustomTitle == null)
                {
                    // 如果没有自定义标题，使用网页标题
                    string title = webView.CoreWebView2.DocumentTitle;
                    tabPage.Text = title.Length > 20 ? title.Substring(0, 20) + "..." : title;
                }
                // 如果有自定义标题，保持不变
            }
            
            ShowStatusMessage("已加载完成", Color.FromArgb(110, 110, 115));
        }

        private void WebView_SourceChanged(object sender, CoreWebView2SourceChangedEventArgs e)
        {
            var webView = (WebView2)sender;
            if (tabControl.SelectedTab != null && tabControl.SelectedTab.Controls[0] == webView)
            {
                addressBar.Text = webView.Source?.ToString() ?? "";
            }
        }

        /// <summary>
        /// WebView2 初始化完成事件处理程序
        /// </summary>
        private void WebView_CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                var webView = (WebView2)sender;
                webView.CoreWebView2.NewWindowRequested += WebView_NewWindowRequested;
            }
        }

        /// <summary>
        /// 新窗口请求事件处理程序 - 拦截新窗口请求，在新标签页打开
        /// </summary>
        private void WebView_NewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            e.Handled = true;
            CreateNewTab(e.Uri);
        }

        /// <summary>
        /// 创建新的浏览器实例
        /// </summary>
        private void NewInstanceButton_Click(object sender, EventArgs e)
        {
            // 生成唯一的实例名称
            string newInstanceId = "Instance_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            
            // 创建新的浏览器窗口
            MainForm newInstance = new MainForm(newInstanceId);
            newInstance.Show();
        }
        
        /// <summary>
        /// 打开实例管理窗口
        /// </summary>
        private void InstanceManagerButton_Click(object sender, EventArgs e)
        {
            InstanceManagerForm managerForm = new InstanceManagerForm();
            managerForm.ShowDialog();
        }

        /// <summary>
        /// 新建标签页按钮点击事件
        /// </summary>
        private void NewTabButton_Click(object sender, EventArgs e)
        {
            CreateNewTab(browserSettings.DefaultPageUrl);
        }

        /// <summary>
        /// 关闭标签页按钮点击事件
        /// </summary>
        private void CloseTabButton_Click(object sender, EventArgs e)
        {
            if (tabControl.SelectedTab != null && tabControl.TabPages.Count > 1)
            {
                int selectedIndex = tabControl.SelectedIndex;
                TabPage tabPage = tabControl.TabPages[selectedIndex];
                
                // 释放WebView2控件资源
                DisposeWebView2(tabPage);
                
                tabControl.TabPages.RemoveAt(selectedIndex);
            }
        }

        /// <summary>
        /// 后退按钮点击事件
        /// </summary>
        private void BackButton_Click(object sender, EventArgs e)
        {
            if (tabControl.SelectedTab != null && tabControl.SelectedTab.Controls.Count > 0)
            {
                var webView = (WebView2)tabControl.SelectedTab.Controls[0];
                if (webView.CanGoBack)
                {
                    webView.GoBack();
                }
            }
        }

        /// <summary>
        /// 前进按钮点击事件
        /// </summary>
        private void ForwardButton_Click(object sender, EventArgs e)
        {
            if (tabControl.SelectedTab != null && tabControl.SelectedTab.Controls.Count > 0)
            {
                var webView = (WebView2)tabControl.SelectedTab.Controls[0];
                if (webView.CanGoForward)
                {
                    webView.GoForward();
                }
            }
        }

        /// <summary>
        /// 刷新按钮点击事件
        /// </summary>
        private void RefreshButton_Click(object sender, EventArgs e)
        {
            if (tabControl.SelectedTab != null && tabControl.SelectedTab.Controls.Count > 0)
            {
                var webView = (WebView2)tabControl.SelectedTab.Controls[0];
                webView.Reload();
            }
        }

        /// <summary>
        /// 主页按钮点击事件
        /// </summary>
        private void HomeButton_Click(object sender, EventArgs e)
        {
            if (tabControl.SelectedTab != null && tabControl.SelectedTab.Controls.Count > 0)
            {
                var webView = (WebView2)tabControl.SelectedTab.Controls[0];
                webView.CoreWebView2.Navigate(browserSettings.DefaultPageUrl);
            }
        }

        /// <summary>
        /// 置顶窗口按钮点击事件
        /// </summary>
        private void PinWindowButton_Click(object sender, EventArgs e)
        {
            isWindowPinned = !isWindowPinned;
            this.TopMost = isWindowPinned;
            ShowStatusMessage(isWindowPinned ? "窗口已置顶" : "窗口取消置顶", Color.FromArgb(0, 120, 215));
        }

        /// <summary>
        /// 开发者工具按钮点击事件
        /// </summary>
        private void DevToolsButton_Click(object sender, EventArgs e)
        {
            if (tabControl.SelectedTab != null && tabControl.SelectedTab.Controls.Count > 0)
            {
                var webView = (WebView2)tabControl.SelectedTab.Controls[0];
                webView.CoreWebView2.OpenDevToolsWindow();
            }
        }

        /// <summary>
        /// 最小化到托盘按钮点击事件
        /// </summary>
        private void MinimizeToTrayButton_Click(object sender, EventArgs e)
        {
            this.Hide();
            isRunningInBackground = true;
            ShowStatusMessage("浏览器已最小化到后台运行", Color.FromArgb(0, 120, 215));
        }

        /// <summary>
        /// 设置按钮点击事件
        /// </summary>
        private void SettingsButton_Click(object sender, EventArgs e)
        {
            SettingsForm settingsForm = new SettingsForm(browserSettings);
            DialogResult result = settingsForm.ShowDialog(this);
            
            if (result == DialogResult.OK)
            {
                browserSettings = settingsForm.CurrentSettings;
                UpdateStatusBar();
                ShowStatusMessage("设置已保存", Color.FromArgb(0, 120, 215));
            }
        }

        /// <summary>
        /// 更新状态栏显示
        /// </summary>
        private void UpdateStatusBar()
        {
            var hideWindowOptionLabel = statusBar.Items["hideWindowOptionLabel"] as ToolStripStatusLabel;
            if (hideWindowOptionLabel != null)
            {
                hideWindowOptionLabel.Text = $"隐藏窗口: {browserSettings.HideWindowOption}";
            }
        }

        /// <summary>
        /// 显示状态栏消息
        /// </summary>
        private void ShowStatusMessage(string message, Color color)
        {
            var statusLabel = statusBar.Items["statusLabel"] as ToolStripStatusLabel;
            if (statusLabel != null)
            {
                statusLabel.Text = message;
                statusLabel.ForeColor = color;
            }
        }

        /// <summary>
        /// 标签控件绘制事件
        /// </summary>
        private void TabControl_DrawItem(object sender, DrawItemEventArgs e)
        {
            TabPage tabPage = tabControl.TabPages[e.Index];
            Rectangle tabRect = tabControl.GetTabRect(e.Index);
            
            // 绘制标签背景
            using (SolidBrush brush = new SolidBrush(e.Index == tabControl.SelectedIndex ? Color.White : Color.FromArgb(240, 240, 240)))
            {
                e.Graphics.FillRectangle(brush, tabRect);
            }
            
            // 绘制标签边框
            using (Pen pen = new Pen(Color.FromArgb(200, 200, 200)))
            {
                e.Graphics.DrawRectangle(pen, tabRect);
            }
            
            // 绘制标签文本
            TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter;
            using (SolidBrush brush = new SolidBrush(e.Index == tabControl.SelectedIndex ? Color.FromArgb(0, 120, 215) : Color.FromArgb(31, 31, 35)))
            {
                TextRenderer.DrawText(e.Graphics, tabPage.Text, tabControl.Font, tabRect, brush.Color, flags);
            }
            
            // 绘制关闭按钮
            Rectangle closeButtonRect = new Rectangle(tabRect.Right - 20, tabRect.Top + 4, 16, 16);
            using (Pen pen = new Pen(Color.FromArgb(110, 110, 115)))
            {
                e.Graphics.DrawLine(pen, closeButtonRect.Left, closeButtonRect.Top, closeButtonRect.Right, closeButtonRect.Bottom);
                e.Graphics.DrawLine(pen, closeButtonRect.Right, closeButtonRect.Top, closeButtonRect.Left, closeButtonRect.Bottom);
            }
        }

        /// <summary>
        /// 标签控件鼠标点击事件
        /// </summary>
        private void TabControl_MouseDown(object sender, MouseEventArgs e)
        {
            for (int i = 0; i < tabControl.TabPages.Count; i++)
            {
                Rectangle tabRect = tabControl.GetTabRect(i);
                Rectangle closeButtonRect = new Rectangle(tabRect.Right - 20, tabRect.Top + 4, 16, 16);
                
                if (closeButtonRect.Contains(e.Location) && tabControl.TabPages.Count > 1)
                {
                    TabPage tabPage = tabControl.TabPages[i];
                    
                    // 释放WebView2控件资源
                    DisposeWebView2(tabPage);
                    
                    tabControl.TabPages.RemoveAt(i);
                    break;
                }
            }
        }
        
        /// <summary>
        /// 释放WebView2控件资源
        /// </summary>
        /// <param name="tabPage">包含WebView2控件的标签页</param>
        private void DisposeWebView2(TabPage tabPage)
        {
            try
            {
                if (tabPage.Controls.Count > 0 && tabPage.Controls[0] is WebView2 webView)
                {
                    // 取消订阅事件
                    webView.NavigationStarting -= WebView_NavigationStarting;
                    webView.NavigationCompleted -= WebView_NavigationCompleted;
                    webView.SourceChanged -= WebView_SourceChanged;
                    webView.CoreWebView2InitializationCompleted -= WebView_CoreWebView2InitializationCompleted;
                    
                    if (webView.CoreWebView2 != null)
                    {
                        webView.CoreWebView2.NewWindowRequested -= WebView_NewWindowRequested;
                    }
                    
                    // 停止所有导航和脚本执行
                    if (webView.CoreWebView2 != null)
                    {
                        try
                        {
                            webView.CoreWebView2.Stop();
                        }
                        catch (Exception)
                        {
                            // 忽略可能的异常
                        }
                    }
                    
                    // 从标签页中移除
                    tabPage.Controls.Remove(webView);
                    
                    // 释放资源
                    webView.Dispose();
                }
            }
            catch (Exception)
            {
                // 忽略可能的异常
            }
        }

        /// <summary>
        /// 标签控件选中索引变化事件
        /// </summary>
        private void TabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabControl.SelectedTab != null && tabControl.SelectedTab.Controls.Count > 0)
            {
                var webView = (WebView2)tabControl.SelectedTab.Controls[0];
                addressBar.Text = webView.Source?.ToString() ?? "";
            }
        }

        /// <summary>
        /// 标签控件拖动悬停事件
        /// </summary>
        private void TabControl_DragOver(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Move;
        }

        /// <summary>
        /// 标签控件拖放事件
        /// </summary>
        private void TabControl_DragDrop(object sender, DragEventArgs e)
        {
            // 实现标签页拖动功能
        }

        /// <summary>
        /// 处理键盘快捷键
        /// </summary>
        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Alt && e.KeyCode == Keys.Left)
            {
                BackButton_Click(sender, e);
            }
            else if (e.Alt && e.KeyCode == Keys.Right)
            {
                ForwardButton_Click(sender, e);
            }
            else if (e.Control && e.KeyCode == Keys.R)
            {
                RefreshButton_Click(sender, e);
            }
            else if (e.Control && e.KeyCode == Keys.T)
            {
                NewTabButton_Click(sender, e);
            }
            else if (e.Control && e.KeyCode == Keys.W)
            {
                CloseTabButton_Click(sender, e);
            }
            else if (e.Control && e.Shift && e.KeyCode == Keys.T)
            {
                PinWindowButton_Click(sender, e);
            }
            else if (e.KeyCode == Keys.F12)
            {
                DevToolsButton_Click(sender, e);
            }
        }

        /// <summary>
        /// 处理窗口关闭事件
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            // 保存会话状态（所有标签页）
            SaveSessionState();
            
            // 保存当前页面URL
            SaveLastVisitedPage();
            
            // 保存进程ID（无论何种模式都保存，以便实例管理器识别）
            SaveProcessId();

            // 确保点击关闭按钮时能够正常关闭程序
            // 保持保存会话状态的功能，以便下次启动时恢复所有页面
            isRunningInBackground = false;
        }
        
        /// <summary>
        /// MainForm窗口关闭事件处理程序
        /// </summary>
        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            // 检查是否还有其他MainForm窗口在运行
            bool hasOtherMainForms = false;
            foreach (Form form in Application.OpenForms)
            {
                if (form is MainForm && form != this)
                {
                    hasOtherMainForms = true;
                    break;
                }
            }
            
            // 如果没有其他MainForm窗口，检查是否还有集成主窗口或实例管理窗口
            if (!hasOtherMainForms)
            {
                bool hasOtherForms = false;
                foreach (Form form in Application.OpenForms)
                {
                    if (form is IntegratedMainForm || form is InstanceManagerForm)
                    {
                        hasOtherForms = true;
                        break;
                    }
                }
                
                // 如果没有其他窗口，退出应用程序
                if (!hasOtherForms && AppContext != null)
                {
                    // 退出应用程序上下文
                    AppContext.ExitThread();
                }
            }
        }
    }
}
