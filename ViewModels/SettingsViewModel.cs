// CustomizedBrowser\ViewModels\SettingsViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CustomizedBrowser.Models;
using CustomizedBrowser.Services;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CustomizedBrowser.ViewModels
{
    /// <summary>
    /// 实例缓存信息（用于UI显示）
    /// </summary>
    public partial class InstanceCacheInfo : ObservableObject
    {
        [ObservableProperty]
        private string _instanceId = string.Empty;

        [ObservableProperty]
        private string _cacheSizeText = "计算中...";

        [ObservableProperty]
        private long _cacheSizeBytes = 0;

        [ObservableProperty]
        private bool _isClearingCache = false;

        [ObservableProperty]
        private bool _isExpanded = false;

        [ObservableProperty]
        private bool _isLoadingDetails = false;

        [ObservableProperty]
        private bool _showEmptyDetailsHint = false;

        [ObservableProperty]
        private ObservableCollection<CacheDomainInfo> _domains = new();

        /// <summary>
        /// 是否已尝试加载过域名详情（避免重复读取数据库）
        /// </summary>
        public bool HasLoadedDetails { get; set; }
    }

    /// <summary>
    /// 设置窗口视图模型
    /// 负责设置窗口的业务逻辑和数据绑定
    /// </summary>
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly ISettingsService _settingsService;
        private readonly ICacheService _cacheService;
        private readonly IInstanceService _instanceService;

        [ObservableProperty]
        private BrowserSettings _currentSettings = null!;

        [ObservableProperty]
        private ObservableCollection<InstanceCacheInfo> _instanceCacheInfos = new();

        [ObservableProperty]
        private bool _isCacheLoading = false;

        [ObservableProperty]
        private long _totalCacheSizeBytes = 0;

        [ObservableProperty]
        private string _totalCacheSizeText = "0 B";

        /// <summary>
        /// 构造函数
        /// </summary>
        public SettingsViewModel(ISettingsService settingsService, ICacheService cacheService, IInstanceService instanceService)
        {
            _settingsService = settingsService;
            _cacheService = cacheService;
            _instanceService = instanceService;
            CurrentSettings = _settingsService.CurrentSettings;
        }

        /// <summary>
        /// 保存设置命令
        /// </summary>
        [RelayCommand]
        private void SaveSettings()
        {
            _settingsService.UpdateSettings(CurrentSettings);
        }

        /// <summary>
        /// 重置设置命令
        /// </summary>
        [RelayCommand]
        private void ResetSettings()
        {
            _settingsService.ResetToDefaults();
            CurrentSettings = _settingsService.CurrentSettings;
        }

        /// <summary>
        /// 刷新缓存信息命令
        /// </summary>
        [RelayCommand]
        private async Task RefreshCacheInfo()
        {
            if (IsCacheLoading) return;
            IsCacheLoading = true;

            try
            {
                var instances = _instanceService.GetAllInstances();
                InstanceCacheInfos.Clear();
                TotalCacheSizeBytes = 0;

                foreach (var instance in instances)
                {
                    var cacheInfo = new InstanceCacheInfo
                    {
                        InstanceId = instance.InstanceId,
                        CacheSizeText = "计算中...",
                        CacheSizeBytes = 0
                    };
                    InstanceCacheInfos.Add(cacheInfo);

                    // 异步计算缓存大小
                    long size = await _cacheService.GetTotalCacheSizeAsync(instance.InstanceId);
                    cacheInfo.CacheSizeBytes = size;
                    cacheInfo.CacheSizeText = FormatSize(size);
                    TotalCacheSizeBytes += size;
                }

                TotalCacheSizeText = FormatSize(TotalCacheSizeBytes);
            }
            finally
            {
                IsCacheLoading = false;
            }
        }

        /// <summary>
        /// 展开/折叠实例缓存详情命令
        /// 切换动作必须保持同步立即生效；域名详情在后台单独加载，
        /// 避免某个实例读取数据库较慢时阻塞其它实例的展开按钮
        /// </summary>
        [RelayCommand]
        private void ToggleInstanceCacheDetail(InstanceCacheInfo? cacheInfo)
        {
            if (cacheInfo == null) return;

            cacheInfo.IsExpanded = !cacheInfo.IsExpanded;

            // 已加载过且确实有数据时不重复读取；无数据时允许再次展开重试（例如实例关闭后数据库不再被占用）
            if (cacheInfo.IsExpanded && (!cacheInfo.HasLoadedDetails || cacheInfo.Domains.Count == 0))
            {
                _ = LoadInstanceCacheDetailsAsync(cacheInfo);
            }
        }

        /// <summary>
        /// 加载实例的域名缓存详情
        /// 在后台读取并更新加载状态，异常时显示空数据提示而不是静默留白
        /// </summary>
        private async Task LoadInstanceCacheDetailsAsync(InstanceCacheInfo cacheInfo)
        {
            cacheInfo.IsLoadingDetails = true;
            cacheInfo.ShowEmptyDetailsHint = false;

            try
            {
                var domains = await _cacheService.GetCacheDomainsAsync(cacheInfo.InstanceId);
                cacheInfo.Domains.Clear();
                foreach (var domain in domains)
                {
                    cacheInfo.Domains.Add(domain);
                }

                cacheInfo.HasLoadedDetails = true;
                cacheInfo.ShowEmptyDetailsHint = cacheInfo.Domains.Count == 0;
            }
            catch (Exception)
            {
                cacheInfo.Domains.Clear();
                cacheInfo.ShowEmptyDetailsHint = true;
            }
            finally
            {
                cacheInfo.IsLoadingDetails = false;
            }
        }

        /// <summary>
        /// 清除所有实例缓存命令
        /// </summary>
        [RelayCommand]
        private async Task ClearAllCache()
        {
            var result = MessageBox.Show("确定要清除所有实例的缓存吗？\n此操作不可撤销。", "确认清除缓存",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            IsCacheLoading = true;

            try
            {
                foreach (var cacheInfo in InstanceCacheInfos)
                {
                    await _cacheService.ClearAllCacheAsync(cacheInfo.InstanceId);
                    cacheInfo.CacheSizeBytes = 0;
                    cacheInfo.CacheSizeText = "0 B";
                    cacheInfo.Domains.Clear();
                }

                TotalCacheSizeBytes = 0;
                TotalCacheSizeText = "0 B";

                MessageBox.Show("缓存已清除完成。", "清除成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            finally
            {
                IsCacheLoading = false;
            }
        }

        /// <summary>
        /// 清除指定实例缓存命令
        /// </summary>
        [RelayCommand]
        private async Task ClearInstanceCache(InstanceCacheInfo? cacheInfo)
        {
            if (cacheInfo == null) return;

            var result = MessageBox.Show($"确定要清除实例 \"{cacheInfo.InstanceId}\" 的缓存吗？\n此操作不可撤销。",
                "确认清除缓存", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            cacheInfo.IsClearingCache = true;

            try
            {
                await _cacheService.ClearAllCacheAsync(cacheInfo.InstanceId);
                cacheInfo.CacheSizeBytes = 0;
                cacheInfo.CacheSizeText = "0 B";
                cacheInfo.Domains.Clear();
                cacheInfo.HasLoadedDetails = false;
                cacheInfo.ShowEmptyDetailsHint = true;

                // 更新总大小
                TotalCacheSizeBytes = InstanceCacheInfos.Sum(c => c.CacheSizeBytes);
                TotalCacheSizeText = FormatSize(TotalCacheSizeBytes);
            }
            finally
            {
                cacheInfo.IsClearingCache = false;
            }
        }

        /// <summary>
        /// 清除指定域名缓存命令
        /// </summary>
        [RelayCommand]
        private async Task ClearDomainCache(object? parameter)
        {
            if (parameter is not object[] args || args.Length < 2) return;

            var instanceId = args[0] as string;
            var domain = args[1] as string;

            if (string.IsNullOrEmpty(instanceId) || string.IsNullOrEmpty(domain)) return;

            var result = MessageBox.Show($"确定要清除 \"{domain}\" 的缓存吗？", "确认清除域名缓存",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            await _cacheService.ClearDomainCacheAsync(instanceId, domain);

            // 刷新实例缓存信息
            var cacheInfo = InstanceCacheInfos.FirstOrDefault(c => c.InstanceId == instanceId);
            if (cacheInfo != null)
            {
                long size = await _cacheService.GetTotalCacheSizeAsync(instanceId);
                cacheInfo.CacheSizeBytes = size;
                cacheInfo.CacheSizeText = FormatSize(size);
                cacheInfo.Domains.Clear();
                var domains = await _cacheService.GetCacheDomainsAsync(instanceId);
                foreach (var d in domains)
                {
                    cacheInfo.Domains.Add(d);
                }

                cacheInfo.HasLoadedDetails = true;
                cacheInfo.ShowEmptyDetailsHint = cacheInfo.Domains.Count == 0;

                // 更新总大小
                TotalCacheSizeBytes = InstanceCacheInfos.Sum(c => c.CacheSizeBytes);
                TotalCacheSizeText = FormatSize(TotalCacheSizeBytes);
            }
        }

        /// <summary>
        /// 导出访问域名列表命令
        /// </summary>
        [RelayCommand]
        private async Task ExportDomainList()
        {
            if (InstanceCacheInfos.Count == 0)
            {
                var allDomains = new ObservableCollection<CacheDomainInfo>();
                var instances = _instanceService.GetAllInstances();
                foreach (var instance in instances)
                {
                    var domains = await _cacheService.GetCacheDomainsAsync(instance.InstanceId);
                    foreach (var d in domains)
                    {
                        if (!allDomains.Any(x => x.Domain == d.Domain))
                            allDomains.Add(d);
                    }
                }
            }

            // 收集所有已加载的域名
            var domainList = InstanceCacheInfos
                .SelectMany(c => c.Domains)
                .GroupBy(d => d.Domain)
                .Select(g =>
                {
                    var first = g.First();
                    return new CacheDomainInfo
                    {
                        Domain = g.Key,
                        VisitCount = g.Sum(d => d.VisitCount),
                        CookieCount = g.Max(d => d.CookieCount),
                        LastVisitTime = g.Max(d => d.LastVisitTime)
                    };
                })
                .OrderByDescending(d => d.LastVisitTime)
                .ToList();

            if (domainList.Count == 0)
            {
                MessageBox.Show("没有可导出的域名数据。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var saveDialog = new SaveFileDialog
            {
                Filter = "CSV 文件|*.csv|文本文件|*.txt|所有文件|*.*",
                FileName = $"域名列表_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv",
                Title = "导出访问域名列表"
            };

            if (saveDialog.ShowDialog() != true) return;

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("域名,访问次数,Cookie数量,最后访问时间");

                foreach (var domain in domainList)
                {
                    string lastVisit = domain.LastVisitTime > DateTime.MinValue
                        ? domain.LastVisitTime.ToString("yyyy-MM-dd HH:mm:ss")
                        : "";
                    sb.AppendLine($"{domain.Domain},{domain.VisitCount},{domain.CookieCount},{lastVisit}");
                }

                await File.WriteAllTextAsync(saveDialog.FileName, sb.ToString(), Encoding.UTF8);

                MessageBox.Show($"域名列表已导出到：\n{saveDialog.FileName}\n\n共 {domainList.Count} 个域名。",
                    "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"导出失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 格式化字节大小为可读文本
        /// </summary>
        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
        }
    }
}
