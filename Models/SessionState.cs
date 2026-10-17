// CustomizedBrowser\Models\SessionState.cs
using System;
using System.Collections.Generic;

namespace CustomizedBrowser.Models
{
    /// <summary>
    /// 标签页状态数据类
    /// </summary>
    public class TabState
    {
        /// <summary>
        /// 页面 URL
        /// </summary>
        public string Url { get; set; } = string.Empty;
    }

    /// <summary>
    /// 会话状态数据类 - 保存一个实例的完整会话状态
    /// </summary>
    public class SessionState
    {
        /// <summary>
        /// 标签页列表
        /// </summary>
        public List<TabState> Tabs { get; set; } = new List<TabState>();

        /// <summary>
        /// 活动标签页索引
        /// </summary>
        public int ActiveTabIndex { get; set; } = 0;

        /// <summary>
        /// 保存时间
        /// </summary>
        public DateTime SavedAt { get; set; } = DateTime.Now;
    }
}
