// CustomizedBrowser\Services\ITrayIconService.cs
namespace CustomizedBrowser.Services
{
    /// <summary>
    /// 托盘图标服务接口
    /// 负责系统托盘图标的显示、菜单和交互
    /// </summary>
    public interface ITrayIconService
    {
        /// <summary>
        /// 显示托盘图标
        /// </summary>
        void Show();

        /// <summary>
        /// 隐藏托盘图标
        /// </summary>
        void Hide();

        /// <summary>
        /// 锁定托盘
        /// </summary>
        void Lock();

        /// <summary>
        /// 解锁托盘
        /// </summary>
        void Unlock();

        /// <summary>
        /// 显示气泡通知
        /// </summary>
        void ShowBalloonTip(string title, string message, ToolTipIcon icon);

        /// <summary>
        /// 更新托盘工具提示文本
        /// </summary>
        void UpdateToolTipText(string text);
    }

    /// <summary>
    /// 工具提示图标枚举
    /// </summary>
    public enum ToolTipIcon
    {
        None,
        Info,
        Warning,
        Error
    }
}
