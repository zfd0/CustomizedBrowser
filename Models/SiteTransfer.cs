// CustomizedBrowser\Models\SiteTransfer.cs
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Linq;

namespace CustomizedBrowser.Models
{
    /// <summary>
    /// 网站条目
    /// 用于「提取网站到新实例」对话框中的网站列表
    /// </summary>
    public partial class SiteEntry : ObservableObject
    {
        /// <summary>
        /// 网站主机名
        /// </summary>
        public string Host { get; set; } = string.Empty;

        /// <summary>
        /// 该网站的 Cookie 条数
        /// </summary>
        public int CookieCount { get; set; }

        /// <summary>
        /// 该网站是否带有站点存储（IndexedDB / WebStorage）
        /// </summary>
        public bool HasStorage { get; set; }

        /// <summary>
        /// 存储情况显示文本
        /// </summary>
        public string StorageText => HasStorage ? "有" : "无";

        /// <summary>
        /// 是否被勾选
        /// </summary>
        [ObservableProperty]
        private bool _isSelected;
    }

    /// <summary>
    /// 合并来源选项（下拉框项）
    /// </summary>
    public class SourceOption
    {
        /// <summary>
        /// 实例 ID
        /// </summary>
        public string InstanceId { get; set; } = string.Empty;

        /// <summary>
        /// 显示文本
        /// </summary>
        public string Label { get; set; } = string.Empty;
    }

    /// <summary>
    /// 合并来源实例条目（「合并实例」对话框中的勾选列表项）
    /// </summary>
    public partial class MergeSourceItem : ObservableObject
    {
        /// <summary>
        /// 实例 ID
        /// </summary>
        public string InstanceId { get; set; } = string.Empty;

        /// <summary>
        /// 显示文本（实例名称 + 备注）
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// 是否被勾选为合并来源
        /// </summary>
        [ObservableProperty]
        private bool _isSelected;
    }

    /// <summary>
    /// 合并目标选项（下拉框项）
    /// </summary>
    public class MergeTargetOption
    {
        /// <summary>
        /// 目标实例 ID（新建目标时为空）
        /// </summary>
        public string InstanceId { get; set; } = string.Empty;

        /// <summary>
        /// 是否为「新建实例」
        /// </summary>
        public bool IsNew { get; set; }

        /// <summary>
        /// 显示文本
        /// </summary>
        public string Label { get; set; } = string.Empty;
    }

    /// <summary>
    /// 合并冲突条目
    /// 表示同时存在于多个来源实例中的网站，需由用户选择保留哪个实例的数据
    /// </summary>
    public partial class MergeHostEntry : ObservableObject
    {
        /// <summary>
        /// 网站主机名
        /// </summary>
        public string Host { get; set; } = string.Empty;

        /// <summary>
        /// 各来源中该网站的 Cookie 条数合计
        /// </summary>
        public int CookieCount { get; set; }

        /// <summary>
        /// 该网站是否带有站点存储
        /// </summary>
        public bool HasStorage { get; set; }

        /// <summary>
        /// 包含该网站的来源实例 ID 列表
        /// </summary>
        public List<string> SourceInstanceIds { get; set; } = new();

        /// <summary>
        /// 可供选择的来源选项
        /// </summary>
        public List<SourceOption> Options { get; set; } = new();

        /// <summary>
        /// 用户选择的来源
        /// </summary>
        [ObservableProperty]
        private SourceOption? _selectedOption;

        /// <summary>
        /// 该网站数据类型显示文本
        /// </summary>
        public string DataText => $"Cookie {CookieCount} 条{(HasStorage ? "，含站点存储" : "")}";
    }

    /// <summary>
    /// 合并预览结果
    /// </summary>
    public class MergePreview
    {
        /// <summary>
        /// 参与合并的来源实例 ID 列表
        /// </summary>
        public List<string> SourceInstanceIds { get; set; } = new();

        /// <summary>
        /// 所有网站条目（含无冲突网站）
        /// </summary>
        public List<MergeHostEntry> Hosts { get; set; } = new();

        /// <summary>
        /// 存在冲突（同时存在于多个来源）的网站
        /// </summary>
        public List<MergeHostEntry> Conflicts => Hosts.Where(h => h.SourceInstanceIds.Count >= 2).ToList();

        /// <summary>
        /// 无冲突网站数量
        /// </summary>
        public int NonConflictCount => Hosts.Count - Conflicts.Count;
    }

    /// <summary>
    /// 网站提取/合并结果
    /// </summary>
    public class SiteTransferResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 结果描述信息
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// 目标实例 ID
        /// </summary>
        public string TargetInstanceId { get; set; } = string.Empty;

        /// <summary>
        /// 迁移的 Cookie 条数
        /// </summary>
        public int CookieCount { get; set; }

        /// <summary>
        /// 迁移站点存储的网站数量
        /// </summary>
        public int StorageSiteCount { get; set; }

        /// <summary>
        /// 过程中的警告信息
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// 警告信息显示文本
        /// </summary>
        public string WarningText => Warnings.Count == 0
            ? string.Empty
            : "\n\n提示：\n- " + string.Join("\n- ", Warnings);
    }
}