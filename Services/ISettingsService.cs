// CustomizedBrowser\Services\ISettingsService.cs
using CustomizedBrowser.Models;

namespace CustomizedBrowser.Services
{
    /// <summary>
    /// 设置服务接口
    /// 负责浏览器全局设置的加载、保存和管理
    /// </summary>
    public interface ISettingsService
    {
        /// <summary>
        /// 获取当前设置
        /// </summary>
        BrowserSettings CurrentSettings { get; }

        /// <summary>
        /// 加载设置
        /// </summary>
        void LoadSettings();

        /// <summary>
        /// 保存设置
        /// </summary>
        void SaveSettings();

        /// <summary>
        /// 更新设置
        /// </summary>
        void UpdateSettings(BrowserSettings newSettings);

        /// <summary>
        /// 重置为默认设置
        /// </summary>
        void ResetToDefaults();
    }
}
