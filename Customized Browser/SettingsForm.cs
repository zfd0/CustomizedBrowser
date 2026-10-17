using System;
using System.Windows.Forms;
using System.Drawing;

namespace SimpleBrowser
{
    /// <summary>
    /// 完整的设置窗体，包含所有浏览器配置选项
    /// </summary>
    public partial class SettingsForm : Form
    {
        /// <summary>
        /// 当前浏览器设置
        /// </summary>
        public BrowserSettings CurrentSettings { get; private set; }

        private TabControl settingsTabControl;
        private bool settingsChanged = false;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="settings">当前浏览器设置</param>
        public SettingsForm(BrowserSettings settings)
        {
            CurrentSettings = new BrowserSettings
            {
                HideWindowOption = settings.HideWindowOption,
                RestoreLastPageOnStartup = settings.RestoreLastPageOnStartup,
                RestoreSessionOnStartup = settings.RestoreSessionOnStartup,
                DefaultPageUrl = settings.DefaultPageUrl,
                TrayIconText = settings.TrayIconText,
                Theme = settings.Theme,
                Notifications = new NotificationSettings
                {
                    EnableNotifications = settings.Notifications.EnableNotifications,
                    ShowDownloadNotifications = settings.Notifications.ShowDownloadNotifications,
                    ShowUpdateNotifications = settings.Notifications.ShowUpdateNotifications,
                    SoundEnabled = settings.Notifications.SoundEnabled
                },
                Sync = new SyncSettings
                {
                    EnableSync = settings.Sync.EnableSync,
                    SyncBookmarks = settings.Sync.SyncBookmarks,
                    SyncHistory = settings.Sync.SyncHistory,
                    SyncBrowserSettings = settings.Sync.SyncBrowserSettings,
                    SyncServerUrl = settings.Sync.SyncServerUrl
                },
                Security = new SecuritySettings
                {
                    EnableAutoLock = settings.Security.EnableAutoLock,
                    AutoLockMinutes = settings.Security.AutoLockMinutes,
                    RequirePasswordOnStartup = settings.Security.RequirePasswordOnStartup,
                    EnablePrivateModeByDefault = settings.Security.EnablePrivateModeByDefault,
                    BlockTrackingCookies = settings.Security.BlockTrackingCookies
                }
            };

            InitializeUI();
        }

        /// <summary>
        /// 初始化用户界面
        /// </summary>
        private void InitializeUI()
        {
            this.Text = "浏览器设置";
            this.Size = new Size(600, 500);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Font = new Font("微软雅黑", 9);
            this.BackColor = Color.FromArgb(240, 240, 240);
            this.ForeColor = Color.Black;

            TableLayoutPanel mainPanel = new TableLayoutPanel();
            mainPanel.Dock = DockStyle.Fill;
            mainPanel.ColumnCount = 1;
            mainPanel.RowCount = 2;
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 90));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 10));
            this.Controls.Add(mainPanel);

            settingsTabControl = new TabControl();
            settingsTabControl.Dock = DockStyle.Fill;
            settingsTabControl.Font = new Font("微软雅黑", 9);
            mainPanel.Controls.Add(settingsTabControl, 0, 0);

            CreateGeneralTab();
            CreateAppearanceTab();
            CreateNotificationsTab();
            CreateSyncTab();
            CreateSecurityTab();
            CreateDataTab();

            FlowLayoutPanel buttonPanel = new FlowLayoutPanel();
            buttonPanel.Dock = DockStyle.Fill;
            buttonPanel.FlowDirection = FlowDirection.RightToLeft;
            buttonPanel.Padding = new Padding(10);
            mainPanel.Controls.Add(buttonPanel, 0, 1);

            Button okButton = new Button();
            okButton.Text = "确定";
            okButton.DialogResult = DialogResult.OK;
            okButton.Size = new Size(80, 30);
            okButton.Click += OkButton_Click;
            buttonPanel.Controls.Add(okButton);

            Button cancelButton = new Button();
            cancelButton.Text = "取消";
            cancelButton.DialogResult = DialogResult.Cancel;
            cancelButton.Size = new Size(80, 30);
            cancelButton.Margin = new Padding(10, 0, 0, 0);
            buttonPanel.Controls.Add(cancelButton);

            this.AcceptButton = okButton;
            this.CancelButton = cancelButton;
        }

        /// <summary>
        /// 创建常规设置标签页
        /// </summary>
        private void CreateGeneralTab()
        {
            TabPage generalTab = new TabPage("常规");
            generalTab.BackColor = Color.White;
            settingsTabControl.TabPages.Add(generalTab);

            TableLayoutPanel panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.ColumnCount = 1;
            panel.Padding = new Padding(20);
            generalTab.Controls.Add(panel);

            GroupBox startupGroup = new GroupBox();
            startupGroup.Text = "启动设置";
            startupGroup.Dock = DockStyle.Top;
            startupGroup.Height = 180;
            panel.Controls.Add(startupGroup);

            TableLayoutPanel startupPanel = new TableLayoutPanel();
            startupPanel.Dock = DockStyle.Fill;
            startupPanel.ColumnCount = 1;
            startupPanel.RowCount = 4;
            startupGroup.Controls.Add(startupPanel);

            CheckBox restoreSessionCheckBox = new CheckBox();
            restoreSessionCheckBox.Text = "启动时恢复所有标签页及状态（包括滚动位置）";
            restoreSessionCheckBox.Checked = CurrentSettings.RestoreSessionOnStartup;
            restoreSessionCheckBox.Dock = DockStyle.Fill;
            restoreSessionCheckBox.CheckedChanged += (s, e) => { CurrentSettings.RestoreSessionOnStartup = restoreSessionCheckBox.Checked; settingsChanged = true; };
            startupPanel.Controls.Add(restoreSessionCheckBox, 0, 0);

            CheckBox restoreLastPageCheckBox = new CheckBox();
            restoreLastPageCheckBox.Text = "启动时恢复上次访问的页面";
            restoreLastPageCheckBox.Checked = CurrentSettings.RestoreLastPageOnStartup;
            restoreLastPageCheckBox.Dock = DockStyle.Fill;
            restoreLastPageCheckBox.CheckedChanged += (s, e) => { CurrentSettings.RestoreLastPageOnStartup = restoreLastPageCheckBox.Checked; settingsChanged = true; };
            startupPanel.Controls.Add(restoreLastPageCheckBox, 0, 1);

            Label defaultPageLabel = new Label();
            defaultPageLabel.Text = "默认页面URL:";
            defaultPageLabel.AutoSize = true;
            defaultPageLabel.Dock = DockStyle.Fill;
            defaultPageLabel.TextAlign = ContentAlignment.MiddleLeft;
            startupPanel.Controls.Add(defaultPageLabel, 0, 2);

            TextBox defaultPageTextBox = new TextBox();
            defaultPageTextBox.Text = CurrentSettings.DefaultPageUrl;
            defaultPageTextBox.Dock = DockStyle.Fill;
            defaultPageTextBox.Margin = new Padding(0, 0, 0, 10);
            defaultPageTextBox.TextChanged += (s, e) => { CurrentSettings.DefaultPageUrl = defaultPageTextBox.Text; settingsChanged = true; };
            startupPanel.Controls.Add(defaultPageTextBox, 0, 3);

            GroupBox hideWindowGroup = new GroupBox();
            hideWindowGroup.Text = "隐藏窗口设置";
            hideWindowGroup.Dock = DockStyle.Top;
            hideWindowGroup.Height = 150;
            hideWindowGroup.Margin = new Padding(0, 10, 0, 0);
            panel.Controls.Add(hideWindowGroup);

            TableLayoutPanel hideWindowPanel = new TableLayoutPanel();
            hideWindowPanel.Dock = DockStyle.Fill;
            hideWindowPanel.ColumnCount = 1;
            hideWindowPanel.RowCount = 3;
            hideWindowGroup.Controls.Add(hideWindowPanel);

            RadioButton closeOption = new RadioButton();
            closeOption.Text = "关闭（默认）：关闭窗口即退出程序";
            closeOption.Checked = CurrentSettings.HideWindowOption == MainForm.HideWindowOption.Close;
            closeOption.Dock = DockStyle.Fill;
            closeOption.CheckedChanged += (s, e) => { if (closeOption.Checked) { CurrentSettings.HideWindowOption = MainForm.HideWindowOption.Close; settingsChanged = true; } };
            hideWindowPanel.Controls.Add(closeOption, 0, 0);

            RadioButton askOption = new RadioButton();
            askOption.Text = "关闭窗口时询问：每次关闭前询问用户";
            askOption.Checked = CurrentSettings.HideWindowOption == MainForm.HideWindowOption.Ask;
            askOption.Dock = DockStyle.Fill;
            askOption.CheckedChanged += (s, e) => { if (askOption.Checked) { CurrentSettings.HideWindowOption = MainForm.HideWindowOption.Ask; settingsChanged = true; } };
            hideWindowPanel.Controls.Add(askOption, 0, 1);

            RadioButton keepRunningOption = new RadioButton();
            keepRunningOption.Text = "打开：关闭窗口时保持程序运行";
            keepRunningOption.Checked = CurrentSettings.HideWindowOption == MainForm.HideWindowOption.KeepRunning;
            keepRunningOption.Dock = DockStyle.Fill;
            keepRunningOption.CheckedChanged += (s, e) => { if (keepRunningOption.Checked) { CurrentSettings.HideWindowOption = MainForm.HideWindowOption.KeepRunning; settingsChanged = true; } };
            hideWindowPanel.Controls.Add(keepRunningOption, 0, 2);

            GroupBox trayGroup = new GroupBox();
            trayGroup.Text = "系统托盘设置";
            trayGroup.Dock = DockStyle.Top;
            trayGroup.Height = 80;
            trayGroup.Margin = new Padding(0, 10, 0, 0);
            panel.Controls.Add(trayGroup);

            TableLayoutPanel trayPanel = new TableLayoutPanel();
            trayPanel.Dock = DockStyle.Fill;
            trayPanel.ColumnCount = 1;
            trayPanel.RowCount = 2;
            trayGroup.Controls.Add(trayPanel);

            Label trayTextLabel = new Label();
            trayTextLabel.Text = "系统托盘显示文本:";
            trayTextLabel.AutoSize = true;
            trayTextLabel.Dock = DockStyle.Fill;
            trayTextLabel.TextAlign = ContentAlignment.MiddleLeft;
            trayPanel.Controls.Add(trayTextLabel, 0, 0);

            TextBox trayTextTextBox = new TextBox();
            trayTextTextBox.Text = CurrentSettings.TrayIconText;
            trayTextTextBox.Dock = DockStyle.Fill;
            trayTextTextBox.Margin = new Padding(0, 0, 0, 10);
            trayTextTextBox.TextChanged += (s, e) => { CurrentSettings.TrayIconText = trayTextTextBox.Text; settingsChanged = true; };
            trayPanel.Controls.Add(trayTextTextBox, 0, 1);
        }

        /// <summary>
        /// 创建外观设置标签页
        /// </summary>
        private void CreateAppearanceTab()
        {
            TabPage appearanceTab = new TabPage("外观");
            appearanceTab.BackColor = Color.White;
            settingsTabControl.TabPages.Add(appearanceTab);

            TableLayoutPanel panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.ColumnCount = 1;
            panel.Padding = new Padding(20);
            appearanceTab.Controls.Add(panel);

            GroupBox themeGroup = new GroupBox();
            themeGroup.Text = "主题设置";
            themeGroup.Dock = DockStyle.Top;
            themeGroup.Height = 150;
            panel.Controls.Add(themeGroup);

            TableLayoutPanel themePanel = new TableLayoutPanel();
            themePanel.Dock = DockStyle.Fill;
            themePanel.ColumnCount = 1;
            themePanel.RowCount = 3;
            themeGroup.Controls.Add(themePanel);

            RadioButton lightTheme = new RadioButton();
            lightTheme.Text = "浅色主题";
            lightTheme.Checked = CurrentSettings.Theme == ThemeOption.Light;
            lightTheme.Dock = DockStyle.Fill;
            lightTheme.CheckedChanged += (s, e) => { if (lightTheme.Checked) { CurrentSettings.Theme = ThemeOption.Light; settingsChanged = true; } };
            themePanel.Controls.Add(lightTheme, 0, 0);

            RadioButton darkTheme = new RadioButton();
            darkTheme.Text = "深色主题";
            darkTheme.Checked = CurrentSettings.Theme == ThemeOption.Dark;
            darkTheme.Dock = DockStyle.Fill;
            darkTheme.CheckedChanged += (s, e) => { if (darkTheme.Checked) { CurrentSettings.Theme = ThemeOption.Dark; settingsChanged = true; } };
            themePanel.Controls.Add(darkTheme, 0, 1);

            RadioButton systemTheme = new RadioButton();
            systemTheme.Text = "跟随系统";
            systemTheme.Checked = CurrentSettings.Theme == ThemeOption.System;
            systemTheme.Dock = DockStyle.Fill;
            systemTheme.CheckedChanged += (s, e) => { if (systemTheme.Checked) { CurrentSettings.Theme = ThemeOption.System; settingsChanged = true; } };
            themePanel.Controls.Add(systemTheme, 0, 2);
        }

        /// <summary>
        /// 创建通知设置标签页
        /// </summary>
        private void CreateNotificationsTab()
        {
            TabPage notificationsTab = new TabPage("通知");
            notificationsTab.BackColor = Color.White;
            settingsTabControl.TabPages.Add(notificationsTab);

            TableLayoutPanel panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.ColumnCount = 1;
            panel.Padding = new Padding(20);
            notificationsTab.Controls.Add(panel);

            GroupBox notificationGroup = new GroupBox();
            notificationGroup.Text = "通知偏好";
            notificationGroup.Dock = DockStyle.Top;
            notificationGroup.Height = 200;
            panel.Controls.Add(notificationGroup);

            TableLayoutPanel notificationPanel = new TableLayoutPanel();
            notificationPanel.Dock = DockStyle.Fill;
            notificationPanel.ColumnCount = 1;
            notificationPanel.RowCount = 4;
            notificationGroup.Controls.Add(notificationPanel);

            CheckBox enableNotifications = new CheckBox();
            enableNotifications.Text = "启用通知";
            enableNotifications.Checked = CurrentSettings.Notifications.EnableNotifications;
            enableNotifications.Dock = DockStyle.Fill;
            enableNotifications.CheckedChanged += (s, e) => { CurrentSettings.Notifications.EnableNotifications = enableNotifications.Checked; settingsChanged = true; };
            notificationPanel.Controls.Add(enableNotifications, 0, 0);

            CheckBox showDownloadNotifications = new CheckBox();
            showDownloadNotifications.Text = "显示下载通知";
            showDownloadNotifications.Checked = CurrentSettings.Notifications.ShowDownloadNotifications;
            showDownloadNotifications.Dock = DockStyle.Fill;
            showDownloadNotifications.CheckedChanged += (s, e) => { CurrentSettings.Notifications.ShowDownloadNotifications = showDownloadNotifications.Checked; settingsChanged = true; };
            notificationPanel.Controls.Add(showDownloadNotifications, 0, 1);

            CheckBox showUpdateNotifications = new CheckBox();
            showUpdateNotifications.Text = "显示更新通知";
            showUpdateNotifications.Checked = CurrentSettings.Notifications.ShowUpdateNotifications;
            showUpdateNotifications.Dock = DockStyle.Fill;
            showUpdateNotifications.CheckedChanged += (s, e) => { CurrentSettings.Notifications.ShowUpdateNotifications = showUpdateNotifications.Checked; settingsChanged = true; };
            notificationPanel.Controls.Add(showUpdateNotifications, 0, 2);

            CheckBox soundEnabled = new CheckBox();
            soundEnabled.Text = "启用通知声音";
            soundEnabled.Checked = CurrentSettings.Notifications.SoundEnabled;
            soundEnabled.Dock = DockStyle.Fill;
            soundEnabled.CheckedChanged += (s, e) => { CurrentSettings.Notifications.SoundEnabled = soundEnabled.Checked; settingsChanged = true; };
            notificationPanel.Controls.Add(soundEnabled, 0, 3);
        }

        /// <summary>
        /// 创建同步设置标签页
        /// </summary>
        private void CreateSyncTab()
        {
            TabPage syncTab = new TabPage("同步");
            syncTab.BackColor = Color.White;
            settingsTabControl.TabPages.Add(syncTab);

            TableLayoutPanel panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.ColumnCount = 1;
            panel.Padding = new Padding(20);
            syncTab.Controls.Add(panel);

            GroupBox syncGroup = new GroupBox();
            syncGroup.Text = "数据同步";
            syncGroup.Dock = DockStyle.Top;
            syncGroup.Height = 250;
            panel.Controls.Add(syncGroup);

            TableLayoutPanel syncPanel = new TableLayoutPanel();
            syncPanel.Dock = DockStyle.Fill;
            syncPanel.ColumnCount = 1;
            syncPanel.RowCount = 5;
            syncGroup.Controls.Add(syncPanel);

            CheckBox enableSync = new CheckBox();
            enableSync.Text = "启用数据同步";
            enableSync.Checked = CurrentSettings.Sync.EnableSync;
            enableSync.Dock = DockStyle.Fill;
            enableSync.CheckedChanged += (s, e) => { CurrentSettings.Sync.EnableSync = enableSync.Checked; settingsChanged = true; };
            syncPanel.Controls.Add(enableSync, 0, 0);

            CheckBox syncBookmarks = new CheckBox();
            syncBookmarks.Text = "同步书签";
            syncBookmarks.Checked = CurrentSettings.Sync.SyncBookmarks;
            syncBookmarks.Dock = DockStyle.Fill;
            syncBookmarks.CheckedChanged += (s, e) => { CurrentSettings.Sync.SyncBookmarks = syncBookmarks.Checked; settingsChanged = true; };
            syncPanel.Controls.Add(syncBookmarks, 0, 1);

            CheckBox syncHistory = new CheckBox();
            syncHistory.Text = "同步历史记录";
            syncHistory.Checked = CurrentSettings.Sync.SyncHistory;
            syncHistory.Dock = DockStyle.Fill;
            syncHistory.CheckedChanged += (s, e) => { CurrentSettings.Sync.SyncHistory = syncHistory.Checked; settingsChanged = true; };
            syncPanel.Controls.Add(syncHistory, 0, 2);

            CheckBox syncSettings = new CheckBox();
            syncSettings.Text = "同步设置";
            syncSettings.Checked = CurrentSettings.Sync.SyncBrowserSettings;
            syncSettings.Dock = DockStyle.Fill;
            syncSettings.CheckedChanged += (s, e) => { CurrentSettings.Sync.SyncBrowserSettings = syncSettings.Checked; settingsChanged = true; };
            syncPanel.Controls.Add(syncSettings, 0, 3);

            Label syncServerLabel = new Label();
            syncServerLabel.Text = "同步服务器地址:";
            syncServerLabel.AutoSize = true;
            syncServerLabel.Dock = DockStyle.Fill;
            syncServerLabel.TextAlign = ContentAlignment.MiddleLeft;
            syncPanel.Controls.Add(syncServerLabel, 0, 4);

            TextBox syncServerTextBox = new TextBox();
            syncServerTextBox.Text = CurrentSettings.Sync.SyncServerUrl;
            syncServerTextBox.Dock = DockStyle.Fill;
            syncServerTextBox.Margin = new Padding(0, 0, 0, 10);
            syncServerTextBox.TextChanged += (s, e) => { CurrentSettings.Sync.SyncServerUrl = syncServerTextBox.Text; settingsChanged = true; };
            syncPanel.Controls.Add(syncServerTextBox, 0, 5);
        }

        /// <summary>
        /// 创建安全设置标签页
        /// </summary>
        private void CreateSecurityTab()
        {
            TabPage securityTab = new TabPage("安全");
            securityTab.BackColor = Color.White;
            settingsTabControl.TabPages.Add(securityTab);

            TableLayoutPanel panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.ColumnCount = 1;
            panel.Padding = new Padding(20);
            securityTab.Controls.Add(panel);

            GroupBox securityGroup = new GroupBox();
            securityGroup.Text = "账户安全";
            securityGroup.Dock = DockStyle.Top;
            securityGroup.Height = 280;
            panel.Controls.Add(securityGroup);

            TableLayoutPanel securityPanel = new TableLayoutPanel();
            securityPanel.Dock = DockStyle.Fill;
            securityPanel.ColumnCount = 1;
            securityPanel.RowCount = 6;
            securityGroup.Controls.Add(securityPanel);

            CheckBox requirePassword = new CheckBox();
            requirePassword.Text = "启动时要求输入密码";
            requirePassword.Checked = CurrentSettings.Security.RequirePasswordOnStartup;
            requirePassword.Dock = DockStyle.Fill;
            requirePassword.CheckedChanged += (s, e) => { CurrentSettings.Security.RequirePasswordOnStartup = requirePassword.Checked; settingsChanged = true; };
            securityPanel.Controls.Add(requirePassword, 0, 0);

            CheckBox enableAutoLock = new CheckBox();
            enableAutoLock.Text = "启用自动锁定";
            enableAutoLock.Checked = CurrentSettings.Security.EnableAutoLock;
            enableAutoLock.Dock = DockStyle.Fill;
            enableAutoLock.CheckedChanged += (s, e) => { CurrentSettings.Security.EnableAutoLock = enableAutoLock.Checked; settingsChanged = true; };
            securityPanel.Controls.Add(enableAutoLock, 0, 1);

            Label autoLockLabel = new Label();
            autoLockLabel.Text = "自动锁定时间（分钟）:";
            autoLockLabel.AutoSize = true;
            autoLockLabel.Dock = DockStyle.Fill;
            autoLockLabel.TextAlign = ContentAlignment.MiddleLeft;
            securityPanel.Controls.Add(autoLockLabel, 0, 2);

            NumericUpDown autoLockMinutes = new NumericUpDown();
            autoLockMinutes.Minimum = 1;
            autoLockMinutes.Maximum = 120;
            autoLockMinutes.Value = CurrentSettings.Security.AutoLockMinutes;
            autoLockMinutes.Dock = DockStyle.Fill;
            autoLockMinutes.ValueChanged += (s, e) => { CurrentSettings.Security.AutoLockMinutes = (int)autoLockMinutes.Value; settingsChanged = true; };
            securityPanel.Controls.Add(autoLockMinutes, 0, 3);

            CheckBox privateModeDefault = new CheckBox();
            privateModeDefault.Text = "默认启用隐私模式";
            privateModeDefault.Checked = CurrentSettings.Security.EnablePrivateModeByDefault;
            privateModeDefault.Dock = DockStyle.Fill;
            privateModeDefault.CheckedChanged += (s, e) => { CurrentSettings.Security.EnablePrivateModeByDefault = privateModeDefault.Checked; settingsChanged = true; };
            securityPanel.Controls.Add(privateModeDefault, 0, 4);

            CheckBox blockTrackingCookies = new CheckBox();
            blockTrackingCookies.Text = "阻止追踪Cookie";
            blockTrackingCookies.Checked = CurrentSettings.Security.BlockTrackingCookies;
            blockTrackingCookies.Dock = DockStyle.Fill;
            blockTrackingCookies.CheckedChanged += (s, e) => { CurrentSettings.Security.BlockTrackingCookies = blockTrackingCookies.Checked; settingsChanged = true; };
            securityPanel.Controls.Add(blockTrackingCookies, 0, 5);
        }

        /// <summary>
        /// 创建数据管理标签页
        /// </summary>
        private void CreateDataTab()
        {
            TabPage dataTab = new TabPage("数据");
            dataTab.BackColor = Color.White;
            settingsTabControl.TabPages.Add(dataTab);

            TableLayoutPanel panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.ColumnCount = 1;
            panel.Padding = new Padding(20);
            dataTab.Controls.Add(panel);

            GroupBox dataGroup = new GroupBox();
            dataGroup.Text = "数据管理";
            dataGroup.Dock = DockStyle.Top;
            dataGroup.Height = 200;
            panel.Controls.Add(dataGroup);

            TableLayoutPanel dataPanel = new TableLayoutPanel();
            dataPanel.Dock = DockStyle.Fill;
            dataPanel.ColumnCount = 1;
            dataPanel.RowCount = 3;
            dataGroup.Controls.Add(dataPanel);

            Button clearCacheButton = new Button();
            clearCacheButton.Text = "清除缓存";
            clearCacheButton.Size = new Size(150, 35);
            clearCacheButton.Dock = DockStyle.Top;
            clearCacheButton.Margin = new Padding(0, 10, 0, 0);
            clearCacheButton.Click += ClearCacheButton_Click;
            dataPanel.Controls.Add(clearCacheButton, 0, 0);

            Button clearHistoryButton = new Button();
            clearHistoryButton.Text = "清除历史记录";
            clearHistoryButton.Size = new Size(150, 35);
            clearHistoryButton.Dock = DockStyle.Top;
            clearHistoryButton.Margin = new Padding(0, 10, 0, 0);
            clearHistoryButton.Click += ClearHistoryButton_Click;
            dataPanel.Controls.Add(clearHistoryButton, 0, 1);

            Button clearCookiesButton = new Button();
            clearCookiesButton.Text = "清除Cookie";
            clearCookiesButton.Size = new Size(150, 35);
            clearCookiesButton.Dock = DockStyle.Top;
            clearCookiesButton.Margin = new Padding(0, 10, 0, 0);
            clearCookiesButton.Click += ClearCookiesButton_Click;
            dataPanel.Controls.Add(clearCookiesButton, 0, 2);
        }

        /// <summary>
        /// 确定按钮点击事件
        /// </summary>
        private void OkButton_Click(object sender, EventArgs e)
        {
            if (settingsChanged)
            {
                SettingsManager.SaveSettings(CurrentSettings);
            }
            this.DialogResult = DialogResult.OK;
        }

        /// <summary>
        /// 清除缓存按钮点击事件
        /// </summary>
        private void ClearCacheButton_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show(
                "确定要清除所有缓存数据吗？",
                "确认清除",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (result == DialogResult.Yes)
            {
                MessageBox.Show("缓存已清除！", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        /// <summary>
        /// 清除历史记录按钮点击事件
        /// </summary>
        private void ClearHistoryButton_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show(
                "确定要清除所有浏览历史吗？",
                "确认清除",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (result == DialogResult.Yes)
            {
                MessageBox.Show("历史记录已清除！", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        /// <summary>
        /// 清除Cookie按钮点击事件
        /// </summary>
        private void ClearCookiesButton_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show(
                "确定要清除所有Cookie吗？",
                "确认清除",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (result == DialogResult.Yes)
            {
                MessageBox.Show("Cookie已清除！", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}
