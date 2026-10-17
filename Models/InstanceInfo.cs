// CustomizedBrowser\Models\InstanceInfo.cs
using System;

namespace CustomizedBrowser.Models
{
    /// <summary>
    /// 实例信息类 - 表示一个浏览器实例的完整信息
    /// </summary>
    public class InstanceInfo
    {
        /// <summary>
        /// 实例唯一标识符（即实例名称）
        /// </summary>
        public string InstanceId { get; set; } = string.Empty;

        /// <summary>
        /// 实例显示名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 实例状态
        /// </summary>
        public InstanceStatus Status { get; set; } = InstanceStatus.Stopped;

        /// <summary>
        /// 实例备注
        /// </summary>
        public string Note { get; set; } = string.Empty;

        /// <summary>
        /// 最后使用时间
        /// </summary>
        public DateTime LastUsedTime { get; set; } = DateTime.MinValue;

        /// <summary>
        /// 数据存储路径
        /// </summary>
        public string DataPath { get; set; } = string.Empty;

        /// <summary>
        /// 是否被选中（用于批量操作）
        /// </summary>
        public bool IsSelected { get; set; } = false;

        /// <summary>
        /// 获取状态显示文本
        /// </summary>
        public string StatusText
        {
            get
            {
                return Status switch
                {
                    InstanceStatus.Stopped => "已关闭",
                    InstanceStatus.Running => "运行中",
                    InstanceStatus.Background => "后台运行",
                    InstanceStatus.Starting => "启动中",
                    InstanceStatus.Stopping => "停止中",
                    _ => "未知"
                };
            }
        }

        /// <summary>
        /// 克隆实例信息
        /// </summary>
        public InstanceInfo Clone()
        {
            return new InstanceInfo
            {
                InstanceId = InstanceId,
                Name = Name,
                Status = Status,
                Note = Note,
                LastUsedTime = LastUsedTime,
                DataPath = DataPath,
                IsSelected = IsSelected
            };
        }
    }
}
