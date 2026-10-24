// CustomizedBrowser\Views\ResultWindow.xaml.cs
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace CustomizedBrowser.Views
{
    /// <summary>
    /// 通用操作结果窗口
    /// 用于替代超长 MessageBox：摘要始终固定可见，提示明细折叠在可滚动区域中，
    /// 避免提示条数过多时把按钮与结果信息挤出屏幕
    /// </summary>
    public partial class ResultWindow : Window
    {
        private readonly List<string> _details;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="success">是否操作成功（决定图标与颜色）</param>
        /// <param name="title">结果标题</param>
        /// <param name="summary">结果摘要（始终显示）</param>
        /// <param name="details">提示明细列表（可为空）</param>
        public ResultWindow(bool success, string title, string summary, IEnumerable<string>? details)
        {
            InitializeComponent();

            _details = details == null ? new List<string>() : new List<string>(details);

            ResultIcon.Kind = success ? PackIconKind.CheckCircle : PackIconKind.AlertCircle;
            ResultIcon.Foreground = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(success ? "#2E7D32" : "#C62828"));

            TitleText.Text = title;
            SummaryText.Text = summary;

            if (_details.Count > 0)
            {
                DetailsHeader.Text = $"提示（{_details.Count} 条，点击展开）";
                DetailsTextBox.Text = string.Join(Environment.NewLine, _details);
            }
            else
            {
                DetailsExpander.Visibility = Visibility.Collapsed;
                CopyButton.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// 以模态方式显示操作结果窗口
        /// </summary>
        /// <param name="owner">所属窗口</param>
        /// <param name="success">是否操作成功</param>
        /// <param name="title">结果标题</param>
        /// <param name="summary">结果摘要</param>
        /// <param name="details">提示明细列表</param>
        public static void Show(Window? owner, bool success, string title, string summary, IEnumerable<string>? details)
        {
            var window = new ResultWindow(success, title, summary, details);
            if (owner != null)
            {
                window.Owner = owner;
            }

            window.ShowDialog();
        }

        /// <summary>
        /// 复制全部提示到剪贴板
        /// </summary>
        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(string.Join(Environment.NewLine, _details));
            }
            catch (Exception)
            {
                // 剪贴板被其他程序占用时忽略
            }
        }

        /// <summary>
        /// 关闭窗口
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}