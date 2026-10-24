// CustomizedBrowser\Views\ExtractSiteWindow.xaml.cs
using CustomizedBrowser.Models;
using CustomizedBrowser.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace CustomizedBrowser.Views
{
    /// <summary>
    /// 「提取网站到新实例」对话框
    /// 列出源实例中检测到的网站（来源于 Cookie 域与站点存储），
    /// 用户勾选后把对应登录状态（Cookie / IndexedDB / WebStorage / Local Storage）独立到新实例
    /// </summary>
    public partial class ExtractSiteWindow : Window
    {
        private readonly string _sourceInstanceId;
        private readonly ISiteTransferService _transferService;
        private readonly IInstanceService _instanceService;
        private readonly ObservableCollection<SiteEntry> _sites = new();

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="sourceInstanceId">源实例 ID</param>
        /// <param name="transferService">网站数据迁移服务</param>
        /// <param name="instanceService">实例服务</param>
        public ExtractSiteWindow(
            string sourceInstanceId, ISiteTransferService transferService, IInstanceService instanceService)
        {
            InitializeComponent();
            _sourceInstanceId = sourceInstanceId;
            _transferService = transferService;
            _instanceService = instanceService;

            SitesListView.ItemsSource = _sites;
            SourceText.Text = $"源实例：{sourceInstanceId}";
            NewInstanceTextBox.Text = _instanceService.GenerateUniqueInstanceId();
            Loaded += ExtractSiteWindow_Loaded;
        }

        /// <summary>
        /// 窗口加载后读取源实例的网站列表
        /// </summary>
        private async void ExtractSiteWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadSitesAsync();
        }

        /// <summary>
        /// 读取源实例的网站列表并刷新界面
        /// </summary>
        private async Task LoadSitesAsync()
        {
            SetBusy(true, "正在读取网站数据（源实例正在运行时可能需要几秒）...");
            try
            {
                var sites = await _transferService.GetSitesAsync(_sourceInstanceId);
                _sites.Clear();
                foreach (var site in sites)
                {
                    _sites.Add(site);
                }

                SequenceText.Text = $"共 {_sites.Count} 个网站";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"读取网站数据失败：{ex.Message}", "提取网站",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetBusy(false, _sites.Count == 0 ? "未检测到任何网站数据。" : string.Empty);
            }
        }

        /// <summary>
        /// 全选按钮点击事件
        /// </summary>
        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var site in _sites)
            {
                site.IsSelected = true;
            }
        }

        /// <summary>
        /// 全不选按钮点击事件
        /// </summary>
        private void SelectNoneButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var site in _sites)
            {
                site.IsSelected = false;
            }
        }

        /// <summary>
        /// 刷新按钮点击事件
        /// </summary>
        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadSitesAsync();
        }

        /// <summary>
        /// 提取按钮点击事件
        /// </summary>
        private async void ExtractButton_Click(object sender, RoutedEventArgs e)
        {
            var hosts = _sites.Where(s => s.IsSelected).Select(s => s.Host).ToList();
            if (hosts.Count == 0)
            {
                MessageBox.Show("请至少勾选一个网站。", "提取网站",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string targetId = NewInstanceTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(targetId))
            {
                MessageBox.Show("请输入新实例名称。", "提取网站",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_instanceService.InstanceExists(targetId))
            {
                MessageBox.Show($"实例 \"{targetId}\" 已存在，请换一个名称。", "提取网站",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            bool removeFromSource = RemoveSourceCheckBox.IsChecked == true;
            string confirmText = $"将把 {hosts.Count} 个网站的登录状态提取到新实例 \"{targetId}\"。\n\n" +
                                 "处理过程中源实例会被停止，是否继续？";
            if (removeFromSource)
            {
                confirmText += "\n\n注意：所选网站的数据将同时从源实例中移除（不可恢复）。";
            }

            if (MessageBox.Show(confirmText, "提取网站", MessageBoxButton.YesNo, MessageBoxImage.Question)
                != MessageBoxResult.Yes)
            {
                return;
            }

            SetBusy(true, "正在提取网站数据，请稍候...");
            try
            {
                var result = await _transferService.ExtractAsync(
                    _sourceInstanceId, hosts, targetId, removeFromSource);
                SetBusy(false, string.Empty);

                // 提示可能很多，使用可滚动结果窗口显示，避免 MessageBox 超出屏幕
                ResultWindow.Show(
                    this,
                    result.Success,
                    result.Success ? "提取成功" : "提取失败",
                    result.Message,
                    result.Warnings);

                if (result.Success)
                {
                    DialogResult = true;
                }
            }
            catch (Exception ex)
            {
                SetBusy(false, string.Empty);
                MessageBox.Show($"提取失败：{ex.Message}", "提取网站",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 切换忙碌状态（禁用交互并显示进度）
        /// </summary>
        private void SetBusy(bool busy, string status)
        {
            BusyProgress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            StatusText.Text = status;
            SitesListView.IsEnabled = !busy;
            SelectAllButton.IsEnabled = !busy;
            SelectNoneButton.IsEnabled = !busy;
            RefreshButton.IsEnabled = !busy;
            ExtractButton.IsEnabled = !busy;
            NewInstanceTextBox.IsEnabled = !busy;
            RemoveSourceCheckBox.IsEnabled = !busy;
        }
    }
}