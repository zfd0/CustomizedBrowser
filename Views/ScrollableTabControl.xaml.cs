// CustomizedBrowser\Views\ScrollableTabControl.xaml.cs
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CustomizedBrowser.Views
{
    /// <summary>
    /// 可滚动标签页控件
    /// 支持大量标签页时的横向滚动显示
    /// 使用自定义Border作为标签UI，保持与TabItem的兼容性
    /// </summary>
    public partial class ScrollableTabControl : UserControl
    {
        private readonly List<TabItem> _tabItems = new();
        private readonly Dictionary<TabItem, Border> _tabBorders = new();
        private TabItem? _selectedItem;

        /// <summary>
        /// 当前选中项变化事件
        /// </summary>
        public event EventHandler? SelectionChanged;

        /// <summary>
        /// 标签关闭事件（用于通知外部释放资源）
        /// </summary>
        public event Action<TabItem>? TabClosing;

        /// <summary>
        /// 获取所有标签项
        /// </summary>
        public IReadOnlyList<TabItem> TabItems => _tabItems;

        /// <summary>
        /// 获取或设置当前选中的标签项
        /// </summary>
        public TabItem? SelectedItem
        {
            get => _selectedItem;
            set => SelectTab(value);
        }

        /// <summary>
        /// 获取当前选中标签的索引
        /// </summary>
        public int SelectedIndex
        {
            get => _selectedItem != null ? _tabItems.IndexOf(_selectedItem) : -1;
            set
            {
                if (value >= 0 && value < _tabItems.Count)
                {
                    SelectTab(_tabItems[value]);
                }
            }
        }

        /// <summary>
        /// 标签项数量
        /// </summary>
        public int ItemCount => _tabItems.Count;

        /// <summary>
        /// ScrollViewer引用（供外部访问）
        /// </summary>
        public ScrollViewer TabScrollViewerControl => TabScrollViewer;

        public ScrollableTabControl()
        {
            InitializeComponent();
            Loaded += ScrollableTabControl_Loaded;
            SizeChanged += ScrollableTabControl_SizeChanged;
        }

        /// <summary>
        /// 加载完成事件
        /// </summary>
        private void ScrollableTabControl_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateScrollButtonsState();
        }

        /// <summary>
        /// 尺寸变化事件
        /// </summary>
        private void ScrollableTabControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateScrollButtonsState();
            EnsureSelectedTabVisible();
        }

        /// <summary>
        /// 添加标签项
        /// </summary>
        public void AddTab(TabItem tabItem)
        {
            if (tabItem == null) return;

            _tabItems.Add(tabItem);
            
            // 创建标签UI
            var tabBorder = CreateTabBorder(tabItem);
            TabPanel.Children.Add(tabBorder);
            _tabBorders[tabItem] = tabBorder;
            
            // 绑定标签项的点击事件
            tabBorder.MouseLeftButtonDown += TabBorder_MouseLeftButtonDown;
            
            // 自动选中新添加的标签
            SelectTab(tabItem);
            
            UpdateScrollButtonsState();
        }

        /// <summary>
        /// 创建标签边框UI组件
        /// </summary>
        private Border CreateTabBorder(TabItem tabItem)
        {
            var border = new Border
            {
                Style = (Style)FindResource("TabItemBorderStyle"),
                Tag = tabItem,
                MinWidth = 80,
                MaxWidth = 200,
                Height = 32
            };

            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            var headerText = new TextBlock
            {
                Text = tabItem.Header?.ToString() ?? "新标签页",
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = tabItem.Header?.ToString() ?? "新标签页",
                MaxWidth = 150,
                Foreground = new SolidColorBrush(Color.FromRgb(51, 51, 51))
            };

            var closeButton = new Button
            {
                Width = 20,
                Height = 20,
                Margin = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Content = new TextBlock
                {
                    Text = "✕",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            };
            closeButton.Click += (s, e) =>
            {
                e.Handled = true;
                // 先触发关闭事件，通知外部释放资源
                TabClosing?.Invoke(tabItem);
                RemoveTab(tabItem);
            };

            stackPanel.Children.Add(headerText);
            stackPanel.Children.Add(closeButton);
            border.Child = stackPanel;

            return border;
        }

        /// <summary>
        /// 更新指定标签的标题
        /// </summary>
        public void UpdateTabHeader(TabItem tabItem, string header)
        {
            if (_tabBorders.TryGetValue(tabItem, out Border? border))
            {
                if (border.Child is StackPanel stackPanel && stackPanel.Children[0] is TextBlock textBlock)
                {
                    textBlock.Text = header;
                    textBlock.ToolTip = header;
                    tabItem.Header = header;
                }
            }
        }

        /// <summary>
        /// 标签边框鼠标点击事件
        /// </summary>
        private void TabBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is TabItem tabItem)
            {
                SelectTab(tabItem);
                e.Handled = true;
            }
        }

        /// <summary>
        /// 移除标签项
        /// </summary>
        public void RemoveTab(TabItem tabItem)
        {
            if (tabItem == null) return;
            if (!_tabItems.Contains(tabItem)) return;

            int index = _tabItems.IndexOf(tabItem);
            
            _tabItems.Remove(tabItem);
            
            if (_tabBorders.TryGetValue(tabItem, out Border? border))
            {
                border.MouseLeftButtonDown -= TabBorder_MouseLeftButtonDown;
                TabPanel.Children.Remove(border);
                _tabBorders.Remove(tabItem);
            }
            
            // 如果移除的是当前选中项，选择相邻的标签
            if (_selectedItem == tabItem)
            {
                _selectedItem = null;
                if (_tabItems.Count > 0)
                {
                    int newIndex = Math.Min(index, _tabItems.Count - 1);
                    SelectTab(_tabItems[newIndex]);
                }
                else
                {
                    ContentPresenter.Content = null;
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            
            UpdateScrollButtonsState();
        }

        /// <summary>
        /// 根据索引移除标签项
        /// </summary>
        public void RemoveTabAt(int index)
        {
            if (index >= 0 && index < _tabItems.Count)
            {
                RemoveTab(_tabItems[index]);
            }
        }

        /// <summary>
        /// 清空所有标签
        /// </summary>
        public void ClearAll()
        {
            foreach (var tabItem in _tabItems.ToList())
            {
                RemoveTab(tabItem);
            }
        }

        /// <summary>
        /// 选中指定标签
        /// </summary>
        public void SelectTab(TabItem? tabItem)
        {
            if (tabItem == null) return;
            if (!_tabItems.Contains(tabItem)) return;
            
            // 更新选中状态
            _selectedItem = tabItem;
            
            // 更新ContentPresenter显示内容
            ContentPresenter.Content = tabItem.Content;
            
            // 更新TabItem的选中样式
            UpdateTabItemSelectionState();
            
            // 确保选中标签可见
            EnsureSelectedTabVisible();
            
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 根据索引选中标签
        /// </summary>
        public void SelectIndex(int index)
        {
            if (index >= 0 && index < _tabItems.Count)
            {
                SelectTab(_tabItems[index]);
            }
        }

        /// <summary>
        /// 更新所有TabItem的选中样式
        /// </summary>
        private void UpdateTabItemSelectionState()
        {
            foreach (TabItem tabItem in _tabItems)
            {
                if (_tabBorders.TryGetValue(tabItem, out Border? border))
                {
                    if (tabItem == _selectedItem)
                    {
                        border.Style = (Style)FindResource("SelectedTabItemBorderStyle");
                        if (border.Child is StackPanel stackPanel && stackPanel.Children[0] is TextBlock textBlock)
                        {
                            textBlock.FontWeight = FontWeights.Bold;
                        }
                    }
                    else
                    {
                        border.Style = (Style)FindResource("TabItemBorderStyle");
                        if (border.Child is StackPanel stackPanel && stackPanel.Children[0] is TextBlock textBlock)
                        {
                            textBlock.FontWeight = FontWeights.Normal;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 确保选中的标签在可视区域内
        /// </summary>
        private void EnsureSelectedTabVisible()
        {
            if (_selectedItem == null) return;
            
            if (_tabBorders.TryGetValue(_selectedItem, out Border? border))
            {
                try
                {
                    double scrollOffset = TabScrollViewer.HorizontalOffset;
                    double viewportWidth = TabScrollViewer.ViewportWidth;
                    
                    // 检查元素是否在视觉树中
                    if (!border.IsVisible || !TabScrollViewer.IsVisible)
                    {
                        return;
                    }
                    
                    // 获取标签在ScrollViewer中的位置
                    GeneralTransform transform = border.TransformToVisual(TabScrollViewer);
                    Rect bounds = transform.TransformBounds(new Rect(0, 0, border.ActualWidth, border.ActualHeight));
                    
                    // 如果标签在左侧超出视野
                    if (bounds.Left < 0)
                    {
                        TabScrollViewer.ScrollToHorizontalOffset(scrollOffset + bounds.Left - 10);
                    }
                    // 如果标签在右侧超出视野
                    else if (bounds.Right > viewportWidth)
                    {
                        TabScrollViewer.ScrollToHorizontalOffset(scrollOffset + bounds.Right - viewportWidth + 10);
                    }
                    
                    UpdateScrollButtonsState();
                }
                catch (InvalidOperationException)
                {
                    // 视觉树尚未完全构建，跳过本次滚动调整
                }
            }
        }

        /// <summary>
        /// 左滚动按钮点击
        /// </summary>
        private void LeftScrollButton_Click(object sender, RoutedEventArgs e)
        {
            TabScrollViewer.LineLeft();
            UpdateScrollButtonsState();
        }

        /// <summary>
        /// 右滚动按钮点击
        /// </summary>
        private void RightScrollButton_Click(object sender, RoutedEventArgs e)
        {
            TabScrollViewer.LineRight();
            UpdateScrollButtonsState();
        }

        /// <summary>
        /// 鼠标滚轮事件（垂直滚动转水平滚动）
        /// </summary>
        private void TabScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // 将垂直滚动转换为水平滚动
            double delta = e.Delta;
            TabScrollViewer.ScrollToHorizontalOffset(
                TabScrollViewer.HorizontalOffset - delta);
            UpdateScrollButtonsState();
            e.Handled = true;
        }

        /// <summary>
        /// 更新滚动按钮状态（启用/禁用）
        /// </summary>
        private void UpdateScrollButtonsState()
        {
            double scrollOffset = TabScrollViewer.HorizontalOffset;
            double extentWidth = TabScrollViewer.ExtentWidth;
            double viewportWidth = TabScrollViewer.ViewportWidth;
            
            // 如果内容宽度大于视口宽度，显示滚动按钮
            bool canScroll = extentWidth > viewportWidth + 1;
            
            LeftScrollButton.Visibility = canScroll ? Visibility.Visible : Visibility.Collapsed;
            RightScrollButton.Visibility = canScroll ? Visibility.Visible : Visibility.Collapsed;
            
            // 根据滚动位置禁用按钮
            LeftScrollButton.IsEnabled = scrollOffset > 0;
            RightScrollButton.IsEnabled = scrollOffset < extentWidth - viewportWidth - 1;
        }

        /// <summary>
        /// 刷新滚动按钮状态（供外部调用）
        /// </summary>
        public void RefreshScrollState()
        {
            UpdateScrollButtonsState();
        }
    }
}
