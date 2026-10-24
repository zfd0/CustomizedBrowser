// CustomizedBrowser\Views\MergeInstancesWindow.xaml.cs
using CustomizedBrowser.Models;
using CustomizedBrowser.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace CustomizedBrowser.Views
{
    /// <summary>
    /// 「合并实例」对话框
    /// 把多个实例的网站数据（Cookie + 站点存储）合并到目标实例；
    /// 同一网站存在于多个来源时列为冲突，由用户选择保留哪个实例的数据
    /// </summary>
    public partial class MergeInstancesWindow : Window
    {
        private readonly ISiteTransferService _transferService;
        private readonly IInstanceService _instanceService;
        private readonly ObservableCollection<MergeSourceItem> _sources = new();
        private readonly ObservableCollection<MergeHostEntry> _conflicts = new();
        private readonly ObservableCollection<MergeTargetOption> _targets = new();

        /// <summary>是否已完成分析（分析结果仍有效，可以合并）</summary>
        private bool _previewReady;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="transferService">网站数据迁移服务</param>
        /// <param name="instanceService">实例服务</param>
        public MergeInstancesWindow(ISiteTransferService transferService, IInstanceService instanceService)
        {
            InitializeComponent();
            _transferService = transferService;
            _instanceService = instanceService;

            SourcesListView.ItemsSource = _sources;
            ConflictsListView.ItemsSource = _conflicts;
            TargetComboBox.ItemsSource = _targets;
            Loaded += MergeInstancesWindow_Loaded;
        }

        /// <summary>
        /// 窗口加载后初始化来源实例列表与目标选项
        /// </summary>
        private void MergeInstancesWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var instances = _instanceService.GetAllInstances();

            foreach (var instance in instances)
            {
                string label = string.IsNullOrWhiteSpace(instance.Note)
                    ? instance.InstanceId
                    : $"{instance.InstanceId}（{instance.Note}）";

                var item = new MergeSourceItem { InstanceId = instance.InstanceId, Label = label };
                item.PropertyChanged += SourceItem_PropertyChanged;
                _sources.Add(item);
            }

            _targets.Clear();
            // 新建目标的名称在加载时生成并保存，避免合并时再生成导致界面显示的名称与实际创建的名称不一致
            string newInstanceId = _instanceService.GenerateUniqueInstanceId();
            _targets.Add(new MergeTargetOption
            {
                InstanceId = newInstanceId,
                IsNew = true,
                Label = $"新建实例（{newInstanceId}）"
            });
            foreach (var instance in instances)
            {
                _targets.Add(new MergeTargetOption
                {
                    InstanceId = instance.InstanceId,
                    Label = string.IsNullOrWhiteSpace(instance.Note)
                        ? instance.InstanceId
                        : $"{instance.InstanceId}（{instance.Note}）"
                });
            }

            TargetComboBox.SelectedIndex = 0;
            StatusText.Text = _sources.Count < 2
                ? "至少需要两个实例才能进行合并。"
                : "勾选要参与合并的实例，然后点击「分析」。";
        }

        /// <summary>
        /// 来源勾选状态变化后清空上一次的分析结果，避免使用过期的预览
        /// </summary>
        private void SourceItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(MergeSourceItem.IsSelected))
            {
                return;
            }

            ResetPreview();
        }

        /// <summary>
        /// 目标实例选择变化后清空上一次的分析结果
        /// </summary>
        private void TargetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ResetPreview();
        }

        /// <summary>
        /// 清空预览结果与统计信息
        /// </summary>
        private void ResetPreview()
        {
            if (!_previewReady)
            {
                return;
            }

            _previewReady = false;
            _conflicts.Clear();
            SummaryText.Text = string.Empty;
            MergeButton.IsEnabled = false;
        }

        /// <summary>
        /// 分析按钮点击事件（预览各来源实例的网站与冲突情况）
        /// </summary>
        private async void AnalyzeButton_Click(object sender, RoutedEventArgs e)
        {
            var sources = GetSelectedSourceIds();
            if (sources.Count == 0)
            {
                MessageBox.Show("请至少勾选一个来源实例（目标实例不能同时作为来源）。", "合并实例",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SetBusy(true, "正在分析各实例的网站数据（实例正在运行时可能需要几秒）...");
            try
            {
                var preview = await _transferService.PreviewMergeAsync(sources);

                _conflicts.Clear();
                foreach (var entry in preview.Conflicts)
                {
                    _conflicts.Add(entry);
                }

                SummaryText.Text = $"共检测到 {preview.Hosts.Count} 个网站：冲突 {preview.Conflicts.Count} 个" +
                                   $"（需选择保留哪个实例的数据），无冲突 {preview.NonConflictCount} 个（自动合并）。";
                _previewReady = preview.Hosts.Count > 0;
                SetBusy(false, string.Empty);
            }
            catch (Exception ex)
            {
                SetBusy(false, string.Empty);
                MessageBox.Show($"分析失败：{ex.Message}", "合并实例",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 合并按钮点击事件
        /// </summary>
        private async void MergeButton_Click(object sender, RoutedEventArgs e)
        {
            var target = TargetComboBox.SelectedItem as MergeTargetOption;
            if (target == null)
            {
                MessageBox.Show("请选择目标实例。", "合并实例",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sources = GetSelectedSourceIds();
            if (sources.Count == 0)
            {
                MessageBox.Show("请至少勾选一个来源实例（目标实例不能同时作为来源）。", "合并实例",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_conflicts.Any(c => c.SelectedOption == null))
            {
                MessageBox.Show("请为所有冲突网站选择要保留的实例。", "合并实例",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 冲突网站的归属选择（网站 → 保留数据的来源实例）
            var hostWinners = _conflicts
                .Where(c => c.SelectedOption != null)
                .ToDictionary(c => c.Host, c => c.SelectedOption!.InstanceId, StringComparer.OrdinalIgnoreCase);

            // 新建目标沿用界面上显示的名称；理论上不会为空，兜底再生成一个唯一名称
            string targetId = string.IsNullOrWhiteSpace(target.InstanceId)
                ? _instanceService.GenerateUniqueInstanceId()
                : target.InstanceId;
            bool targetExists = !target.IsNew && _instanceService.InstanceExists(targetId);

            string confirmText = $"将把 {sources.Count} 个来源实例的网站数据合并到" +
                                 (target.IsNew ? $"新实例 \"{targetId}\"。" : $"实例 \"{targetId}\"。") + "\n\n" +
                                 "处理过程中涉及的实例会被停止，目标实例中相同网站的旧登录状态会被覆盖。\n\n是否继续？";
            if (MessageBox.Show(confirmText, "合并实例", MessageBoxButton.YesNo, MessageBoxImage.Question)
                != MessageBoxResult.Yes)
            {
                return;
            }

            SetBusy(true, "正在合并实例数据，请稍候...");
            try
            {
                var result = await _transferService.MergeAsync(sources, hostWinners, targetId, targetExists);
                SetBusy(false, string.Empty);

                // 先让界面完成忙碌状态的重绘，再打开模态结果窗口，避免父窗口停留在旧画面
                await System.Windows.Threading.Dispatcher.Yield(
                    System.Windows.Threading.DispatcherPriority.Render);

                // 提示可能很多，使用可滚动结果窗口显示，避免 MessageBox 超出屏幕
                ResultWindow.Show(
                    this,
                    result.Success,
                    result.Success ? "合并成功" : "合并失败",
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
                MessageBox.Show($"合并失败：{ex.Message}", "合并实例",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 获取已勾选且不等于目标实例的来源实例 ID 列表
        /// </summary>
        private List<string> GetSelectedSourceIds()
        {
            var target = TargetComboBox.SelectedItem as MergeTargetOption;
            return _sources
                .Where(s => s.IsSelected)
                .Select(s => s.InstanceId)
                .Where(id => target == null || target.IsNew ||
                             !string.Equals(id, target.InstanceId, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        /// <summary>
        /// 切换忙碌状态（禁用交互并显示进度）
        /// </summary>
        private void SetBusy(bool busy, string status)
        {
            BusyProgress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            StatusText.Text = status;
            SourcesListView.IsEnabled = !busy;
            ConflictsListView.IsEnabled = !busy;
            TargetComboBox.IsEnabled = !busy;
            AnalyzeButton.IsEnabled = !busy;
            MergeButton.IsEnabled = !busy && _previewReady;
        }
    }
}