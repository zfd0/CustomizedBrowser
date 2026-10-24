// CustomizedBrowser\Views\InputWindow.xaml.cs
using System.Windows;
using System.Windows.Controls;

namespace CustomizedBrowser.Views
{
    /// <summary>
    /// 通用文本输入对话框
    /// 用于重命名实例（单行）与修改实例描述（多行）
    /// </summary>
    public partial class InputWindow : Window
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="title">窗口标题</param>
        /// <param name="label">输入项说明</param>
        /// <param name="initialValue">初始文本</param>
        /// <param name="multiline">是否多行输入</param>
        public InputWindow(string title, string label, string initialValue, bool multiline)
        {
            InitializeComponent();

            Title = title;
            LabelText.Text = label;
            InputBox.Text = initialValue;

            if (multiline)
            {
                // 多行模式：放大窗口并允许换行与滚动
                Height = 360;
                InputBox.AcceptsReturn = true;
                InputBox.TextWrapping = TextWrapping.Wrap;
                InputBox.VerticalContentAlignment = VerticalAlignment.Top;
                InputBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                InputBox.MinHeight = 180;
            }

            // 打开后自动聚焦并全选，便于直接替换原文本
            Loaded += (s, e) =>
            {
                InputBox.Focus();
                InputBox.SelectAll();
            };
        }

        /// <summary>
        /// 以模态方式显示输入对话框
        /// </summary>
        /// <param name="owner">所属窗口</param>
        /// <param name="title">窗口标题</param>
        /// <param name="label">输入项说明</param>
        /// <param name="initialValue">初始文本</param>
        /// <param name="multiline">是否多行输入</param>
        /// <returns>确定时返回输入的文本，取消时返回 null</returns>
        public static string? Show(
            Window? owner, string title, string label, string initialValue, bool multiline = false)
        {
            var window = new InputWindow(title, label, initialValue, multiline);
            if (owner != null)
            {
                window.Owner = owner;
            }

            return window.ShowDialog() == true ? window.InputBox.Text : null;
        }

        /// <summary>
        /// 确定按钮点击事件
        /// </summary>
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}