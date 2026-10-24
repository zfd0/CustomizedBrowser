// CustomizedBrowser\Views\InstanceManagerWindow.xaml.cs
using CustomizedBrowser.Models;
using CustomizedBrowser.Services;
using CustomizedBrowser.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace CustomizedBrowser.Views
{
    /// <summary>
    /// 实例管理窗口
    /// 实例列表支持鼠标框选（按住左键拖拽矩形多选实例）以及右键菜单
    /// （实例操作 / 导出导入 / 提取网站 / 合并实例）
    /// </summary>
    public partial class InstanceManagerWindow : Window
    {
        /// <summary>
        /// 判定为框选的最小拖拽距离（像素）
        /// 小于该距离视为普通单击，避免与单击选中的操作冲突
        /// </summary>
        private const double BandSelectThreshold = 4;

        private readonly InstanceManagerViewModel _viewModel;
        private readonly IServiceProvider _serviceProvider;
        private readonly IInstanceService _instanceService;

        /// <summary>
        /// 是否正在框选
        /// </summary>
        private bool _isBandSelecting;

        /// <summary>
        /// 是否具备框选条件（左键按下时未命中滚动条与列表表头）
        /// </summary>
        private bool _bandSelectCandidate;

        /// <summary>
        /// 框选起点（相对于实例列表的坐标）
        /// </summary>
        private Point _bandOrigin;

        /// <summary>
        /// 框选开始前已选中的实例（按住 Ctrl 框选时予以保留）
        /// </summary>
        private readonly HashSet<InstanceInfo> _bandBaseSelection = new();

        /// <summary>
        /// 框选过程中暂不同步选中集合，待框选结束后统一同步，避免频繁刷新
        /// </summary>
        private bool _suppressSelectionSync;

        public InstanceManagerWindow(
            InstanceManagerViewModel viewModel, IServiceProvider serviceProvider, IInstanceService instanceService)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _serviceProvider = serviceProvider;
            _instanceService = instanceService;
            DataContext = viewModel;

            // 框选与右键菜单相关的鼠标事件在代码中订阅，便于统一维护
            InstancesListView.PreviewMouseLeftButtonDown += InstancesListView_PreviewMouseLeftButtonDown;
            InstancesListView.PreviewMouseMove += InstancesListView_PreviewMouseMove;
            InstancesListView.PreviewMouseLeftButtonUp += InstancesListView_PreviewMouseLeftButtonUp;
            InstancesListView.LostMouseCapture += InstancesListView_LostMouseCapture;
            InstancesListView.ContextMenuOpening += InstancesListView_ContextMenuOpening;
        }

        /// <summary>
        /// 窗口关闭事件 - 隐藏而不是关闭，保持应用程序运行
        /// </summary>
        protected override void OnClosing(CancelEventArgs e)
        {
            // 检查是否还有其他窗口打开
            bool hasOtherWindows = false;
            foreach (Window window in Application.Current.Windows)
            {
                if (window != this && window.IsVisible)
                {
                    hasOtherWindows = true;
                    break;
                }
            }
            
            // 如果还有其他窗口打开，则只是隐藏此窗口
            if (hasOtherWindows)
            {
                e.Cancel = true;
                Hide();
            }
            
            base.OnClosing(e);
        }

        /// <summary>
        /// ListView 鼠标双击事件
        /// </summary>
        private void InstancesListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement element)
            {
                var item = element.DataContext;
                if (item != null && _viewModel.OpenInstanceCommand.CanExecute(null))
                {
                    _viewModel.OpenInstanceCommand.Execute(null);
                }
            }
        }

        /// <summary>
        /// 实例列表选中项变化 - 同步到视图模型（供批量命令与右键菜单使用）
        /// </summary>
        private void InstancesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelectionSync)
            {
                return;
            }

            _viewModel.SelectedInstances = InstancesListView.SelectedItems.Cast<InstanceInfo>().ToList();
        }

        /// <summary>
        /// 左键按下 - 记录框选起点；命中滚动条或列表表头时不启动框选
        /// </summary>
        private void InstancesListView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isBandSelecting = false;
            _bandOrigin = e.GetPosition(InstancesListView);
            _bandSelectCandidate = !IsInScrollBarOrHeader(e.OriginalSource as DependencyObject);
        }

        /// <summary>
        /// 鼠标移动 - 拖拽超过阈值后开始框选，并实时更新选中项
        /// </summary>
        private void InstancesListView_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            Point current = e.GetPosition(InstancesListView);

            if (!_isBandSelecting)
            {
                // 未达到拖拽阈值时保持普通单击行为
                if (!_bandSelectCandidate ||
                    (Math.Abs(current.X - _bandOrigin.X) < BandSelectThreshold &&
                     Math.Abs(current.Y - _bandOrigin.Y) < BandSelectThreshold))
                {
                    return;
                }

                StartBandSelection();
            }

            UpdateBandSelection(new Rect(_bandOrigin, current));
            e.Handled = true;
        }

        /// <summary>
        /// 左键抬起 - 结束框选并同步选中集合
        /// </summary>
        private void InstancesListView_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isBandSelecting)
            {
                return;
            }

            EndBandSelection();
            e.Handled = true;
        }

        /// <summary>
        /// 丢失鼠标捕获 - 兜底结束框选，避免选择矩形残留
        /// </summary>
        private void InstancesListView_LostMouseCapture(object sender, MouseEventArgs e)
        {
            if (_isBandSelecting)
            {
                EndBandSelection();
            }
        }

        /// <summary>
        /// 右键菜单弹出前 - 若右键点击的实例未被选中，则将其切换为当前选中项，
        /// 使菜单中的操作（如导出实例、提取网站）作用于右键点击的实例
        /// </summary>
        private void InstancesListView_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var hit = InstancesListView.InputHitTest(Mouse.GetPosition(InstancesListView)) as DependencyObject;
            var item = FindAncestor<ListViewItem>(hit);

            if (item != null && !item.IsSelected)
            {
                InstancesListView.SelectedItems.Clear();
                item.IsSelected = true;
            }
        }

        /// <summary>
        /// 开始框选：记录基线选中项、接管鼠标捕获并显示选择矩形
        /// </summary>
        private void StartBandSelection()
        {
            _isBandSelecting = true;
            _suppressSelectionSync = true;

            // 按住 Ctrl 时在原有选中项基础上追加，否则从空选开始
            _bandBaseSelection.Clear();
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                foreach (InstanceInfo item in InstancesListView.SelectedItems)
                {
                    _bandBaseSelection.Add(item);
                }
            }
            else
            {
                InstancesListView.SelectedItems.Clear();
            }

            InstancesListView.CaptureMouse();
            SelectionRectangle.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// 结束框选：释放鼠标捕获、隐藏选择矩形并统一同步选中集合
        /// </summary>
        private void EndBandSelection()
        {
            _isBandSelecting = false;
            _bandSelectCandidate = false;

            SelectionRectangle.Visibility = Visibility.Collapsed;
            SelectionRectangle.Width = 0;
            SelectionRectangle.Height = 0;

            if (Mouse.Captured == InstancesListView)
            {
                InstancesListView.ReleaseMouseCapture();
            }

            _bandBaseSelection.Clear();
            _suppressSelectionSync = false;

            _viewModel.SelectedInstances = InstancesListView.SelectedItems.Cast<InstanceInfo>().ToList();
        }

        /// <summary>
        /// 更新选择矩形的位置尺寸，并按矩形与列表项的交集更新选中状态
        /// </summary>
        /// <param name="band">当前框选矩形（相对于实例列表的坐标）</param>
        private void UpdateBandSelection(Rect band)
        {
            Canvas.SetLeft(SelectionRectangle, band.X);
            Canvas.SetTop(SelectionRectangle, band.Y);
            SelectionRectangle.Width = band.Width;
            SelectionRectangle.Height = band.Height;

            foreach (var item in InstancesListView.Items)
            {
                var container = InstancesListView.ItemContainerGenerator.ContainerFromItem(item) as ListViewItem;
                if (container == null)
                {
                    // 虚拟化未生成的项不参与框选
                    continue;
                }

                Rect bounds = container.TransformToAncestor(InstancesListView)
                    .TransformBounds(new Rect(0, 0, container.ActualWidth, container.ActualHeight));

                container.IsSelected = bounds.IntersectsWith(band) ||
                                      (item is InstanceInfo info && _bandBaseSelection.Contains(info));
            }
        }

        /// <summary>
        /// 判断视觉元素是否位于滚动条或列表表头内（这些区域不启动框选）
        /// </summary>
        private static bool IsInScrollBarOrHeader(DependencyObject? source)
        {
            return FindAncestor<ScrollBar>(source) != null || FindAncestor<GridViewColumnHeader>(source) != null;
        }

        /// <summary>
        /// 在视觉树上向上查找指定类型的祖先元素
        /// </summary>
        private static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
        {
            while (source != null)
            {
                if (source is T match)
                {
                    return match;
                }

                if (source is FrameworkContentElement contentElement)
                {
                    source = contentElement.Parent;
                }
                else if (source is Visual || source is System.Windows.Media.Media3D.Visual3D)
                {
                    source = VisualTreeHelper.GetParent(source);
                }
                else
                {
                    return null;
                }
            }

            return null;
        }

        /// <summary>
        /// 重命名实例按钮（工具栏按钮与右键菜单共用）
        /// 实例 ID 同时是数据目录名，重命名会先停止实例再移动数据目录
        /// </summary>
        private async void RenameInstanceButton_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetSingleSelectedInstance(out var instance))
            {
                return;
            }

            string? input = InputWindow.Show(this, "重命名实例", "新的实例名称：", instance.InstanceId);
            if (input == null)
            {
                return;
            }

            string newName = input.Trim();
            if (string.Equals(newName, instance.InstanceId, StringComparison.Ordinal))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(newName) || newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                MessageBox.Show("实例名称不能为空，且不能包含 \\ / : * ? \" < > | 等字符。", "重命名实例",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_instanceService.InstanceExists(newName))
            {
                MessageBox.Show($"实例 \"{newName}\" 已存在，请换一个名称。", "重命名实例",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (instance.Status != InstanceStatus.Stopped &&
                MessageBox.Show($"实例 \"{instance.InstanceId}\" 正在运行，重命名会先停止它，是否继续？",
                    "重命名实例", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            bool success = await _instanceService.RenameInstanceAsync(instance.InstanceId, newName);
            if (!success)
            {
                MessageBox.Show("重命名失败。请确认实例已完全关闭后重试。", "重命名实例",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 刷新列表并保持选中重命名后的实例
            _viewModel.RefreshInstances(newName);
        }

        /// <summary>
        /// 修改描述按钮（工具栏按钮与右键菜单共用）
        /// 描述保存在实例备注文件中，不影响实例数据
        /// </summary>
        private void EditNoteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetSingleSelectedInstance(out var instance))
            {
                return;
            }

            string? input = InputWindow.Show(
                this, "修改描述", "实例描述（可留空）：", instance.Note, multiline: true);
            if (input == null)
            {
                return;
            }

            _instanceService.UpdateInstanceNote(instance.InstanceId, input.Trim());
            _viewModel.RefreshInstances(instance.InstanceId);
        }

        /// <summary>
        /// 获取唯一选中的实例（重命名、修改描述等操作只允许针对单个实例）
        /// </summary>
        private bool TryGetSingleSelectedInstance(out InstanceInfo instance)
        {
            instance = null!;

            if (_viewModel.SelectedInstances.Count > 1)
            {
                MessageBox.Show("该操作只能针对单个实例，请只选择一个实例。", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }

            var target = _viewModel.SelectedInstances.Count == 1
                ? _viewModel.SelectedInstances[0]
                : _viewModel.SelectedInstance;

            if (target == null)
            {
                MessageBox.Show("请先选择一个实例。", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }

            instance = target;
            return true;
        }

        /// <summary>
        /// 设置按钮点击事件
        /// </summary>
        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settingsWindow = _serviceProvider.GetRequiredService<SettingsWindow>();
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
        /// 提取网站按钮（工具栏按钮与右键菜单共用）
        /// 把所选实例中指定网站的登录状态（Cookie + 站点存储）独立到新实例
        /// </summary>
        private void ExtractSiteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedInstance == null)
            {
                MessageBox.Show("请先选择要提取网站的源实例。", "提取网站",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                // 源实例 ID 需要动态传入，因此通过 ActivatorUtilities 创建窗口
                var window = ActivatorUtilities.CreateInstance<ExtractSiteWindow>(
                    _serviceProvider, _viewModel.SelectedInstance.InstanceId);
                window.Owner = this;

                if (window.ShowDialog() == true)
                {
                    // 提取过程会停止源实例并创建新实例，刷新列表以更新状态
                    _viewModel.RefreshInstances();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开提取网站窗口失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 合并实例按钮（工具栏按钮与右键菜单共用）
        /// 把多个实例的网站数据合并到目标实例，相同网站由用户选择保留哪个
        /// </summary>
        private void MergeInstancesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var window = _serviceProvider.GetRequiredService<MergeInstancesWindow>();
                window.Owner = this;

                if (window.ShowDialog() == true)
                {
                    // 合并过程会停止涉及的实例并可能创建新实例，刷新列表以更新状态
                    _viewModel.RefreshInstances();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开合并实例窗口失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
