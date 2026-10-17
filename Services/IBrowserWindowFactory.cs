// CustomizedBrowser\Services\IBrowserWindowFactory.cs
using CustomizedBrowser.Views;

namespace CustomizedBrowser.Services
{
    /// <summary>
    /// 浏览器窗口工厂接口
    /// 负责创建和管理浏览器窗口实例
    /// </summary>
    public interface IBrowserWindowFactory
    {
        /// <summary>
        /// 创建浏览器窗口
        /// </summary>
        BrowserWindow CreateBrowserWindow(string instanceId);
    }
}
