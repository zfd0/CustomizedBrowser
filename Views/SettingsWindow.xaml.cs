// CustomizedBrowser\Views\SettingsWindow.xaml.cs
using CustomizedBrowser.ViewModels;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;

namespace CustomizedBrowser.Views
{
    /// <summary>
    /// 设置窗口
    /// </summary>
    public partial class SettingsWindow : Window
    {
        public SettingsWindow(SettingsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            
            // 初始化时根据当前分辨率选择对应的预设
            InitializeResolutionPreset();
        }

        /// <summary>
        /// 根据当前分辨率初始化预设选择
        /// </summary>
        private void InitializeResolutionPreset()
        {
            if (DataContext is SettingsViewModel viewModel)
            {
                double width = viewModel.CurrentSettings.DefaultWindowWidth;
                double height = viewModel.CurrentSettings.DefaultWindowHeight;
                
                foreach (ComboBoxItem item in ResolutionPresetComboBox.Items)
                {
                    string tag = item.Tag?.ToString() ?? string.Empty;
                    if (!string.IsNullOrEmpty(tag) && tag != "Custom")
                    {
                        string[] parts = tag.Split(',');
                        if (parts.Length == 2 && 
                            double.TryParse(parts[0], out double presetWidth) && 
                            double.TryParse(parts[1], out double presetHeight) &&
                            presetWidth == width && presetHeight == height)
                        {
                            ResolutionPresetComboBox.SelectedItem = item;
                            return;
                        }
                    }
                }
                
                // 如果没有匹配的预设，选择"自定义"
                ResolutionPresetComboBox.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// 分辨率预设选择变更事件
        /// </summary>
        private void ResolutionPresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is SettingsViewModel viewModel && 
                ResolutionPresetComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string tag = selectedItem.Tag?.ToString() ?? string.Empty;
                if (!string.IsNullOrEmpty(tag) && tag != "Custom")
                {
                    string[] parts = tag.Split(',');
                    if (parts.Length == 2)
                    {
                        if (double.TryParse(parts[0], out double width))
                        {
                            viewModel.CurrentSettings.DefaultWindowWidth = width;
                        }
                        if (double.TryParse(parts[1], out double height))
                        {
                            viewModel.CurrentSettings.DefaultWindowHeight = height;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 关闭按钮点击事件
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// 浏览本地页面文件按钮点击事件
        /// </summary>
        private void BrowseLocalPageButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "HTML 文件|*.html;*.htm|所有文件|*.*",
                    Title = "选择本地默认页面文件"
                };

                if (DataContext is SettingsViewModel viewModel)
                {
                    string currentPath = viewModel.CurrentSettings.CustomLocalPagePath;
                    if (!string.IsNullOrWhiteSpace(currentPath) && System.IO.File.Exists(currentPath))
                    {
                        string? directory = System.IO.Path.GetDirectoryName(currentPath);
                        if (!string.IsNullOrEmpty(directory))
                        {
                            openFileDialog.InitialDirectory = directory;
                        }
                    }
                }

                if (openFileDialog.ShowDialog() == true && DataContext is SettingsViewModel vm)
                {
                    vm.CurrentSettings.CustomLocalPagePath = openFileDialog.FileName;
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"选择文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// TabControl 滚轮事件：转发到外层 ScrollViewer
        /// </summary>
        private void TabControl_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (e.Handled) return;
            e.Handled = true;

            double newOffset = RootScrollViewer.VerticalOffset - e.Delta;
            if (newOffset < 0) newOffset = 0;
            if (newOffset > RootScrollViewer.ScrollableHeight)
                newOffset = RootScrollViewer.ScrollableHeight;

            RootScrollViewer.ScrollToVerticalOffset(newOffset);
        }
    }
}
