// CustomizedBrowser\Models\Enums.cs
namespace CustomizedBrowser.Models
{
    /// <summary>
    /// 主题选项枚举
    /// </summary>
    public enum ThemeOption
    {
        /// <summary>
        /// 浅色主题
        /// </summary>
        Light = 0,

        /// <summary>
        /// 深色主题
        /// </summary>
        Dark = 1,

        /// <summary>
        /// 跟随系统
        /// </summary>
        System = 2
    }

    /// <summary>
    /// 隐藏窗口选项枚举
    /// </summary>
    public enum HideWindowOption
    {
        /// <summary>
        /// 关闭窗口即退出程序
        /// </summary>
        Close = 0,

        /// <summary>
        /// 关闭窗口时询问
        /// </summary>
        Ask = 1,

        /// <summary>
        /// 关闭窗口时保持后台运行
        /// </summary>
        KeepRunning = 2
    }

    /// <summary>
    /// 实例状态枚举
    /// </summary>
    public enum InstanceStatus
    {
        /// <summary>
        /// 已关闭
        /// </summary>
        Stopped = 0,

        /// <summary>
        /// 运行中
        /// </summary>
        Running = 1,

        /// <summary>
        /// 后台运行
        /// </summary>
        Background = 2,

        /// <summary>
        /// 启动中
        /// </summary>
        Starting = 3,

        /// <summary>
        /// 停止中
        /// </summary>
        Stopping = 4
    }
}
