// CustomizedBrowser\Services\ISessionService.cs
using CustomizedBrowser.Models;

namespace CustomizedBrowser.Services
{
    /// <summary>
    /// 会话服务接口
    /// 负责浏览器实例会话状态的保存和恢复
    /// </summary>
    public interface ISessionService
    {
        /// <summary>
        /// 保存会话状态
        /// </summary>
        void SaveSession(string instanceId, SessionState sessionState);

        /// <summary>
        /// 加载会话状态
        /// </summary>
        SessionState? LoadSession(string instanceId);

        /// <summary>
        /// 保存上次访问的页面
        /// </summary>
        void SaveLastPage(string instanceId, string url);

        /// <summary>
        /// 加载上次访问的页面
        /// </summary>
        string LoadLastPage(string instanceId, string defaultUrl);
    }
}
