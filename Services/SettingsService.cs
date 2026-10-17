// CustomizedBrowser\Services\SettingsService.cs
using CustomizedBrowser.Core;
using CustomizedBrowser.Models;
using System;
using System.IO;
using System.Text.Json;

namespace CustomizedBrowser.Services
{
    /// <summary>
    /// 设置服务实现
    /// 负责浏览器全局设置的加载、保存和管理
    /// </summary>
    public class SettingsService : ISettingsService
    {
        private BrowserSettings _settings = null!;

        /// <summary>
        /// 获取当前设置
        /// </summary>
        public BrowserSettings CurrentSettings => _settings;

        /// <summary>
        /// 构造函数
        /// </summary>
        public SettingsService()
        {
            _settings = new BrowserSettings();
            LoadSettings();
        }

        /// <summary>
        /// 加载设置
        /// </summary>
        public void LoadSettings()
        {
            try
            {
                string filePath = AppConstants.GetSettingsFilePath();
                if (File.Exists(filePath))
                {
                    string jsonContent = File.ReadAllText(filePath);
                    var loadedSettings = JsonSerializer.Deserialize<BrowserSettings>(jsonContent);
                    if (loadedSettings != null)
                    {
                        _settings = loadedSettings;
                    }
                }
            }
            catch (Exception)
            {
                // 加载失败时使用默认设置
                _settings = new BrowserSettings();
            }
        }

        /// <summary>
        /// 保存设置
        /// </summary>
        public void SaveSettings()
        {
            try
            {
                string filePath = AppConstants.GetSettingsFilePath();
                string? directoryPath = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                string jsonContent = JsonSerializer.Serialize(_settings, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault
                });

                File.WriteAllText(filePath, jsonContent);
            }
            catch (Exception)
            {
                // 保存失败时静默处理
            }
        }

        /// <summary>
        /// 更新设置
        /// </summary>
        public void UpdateSettings(BrowserSettings newSettings)
        {
            _settings = newSettings;
            SaveSettings();
        }

        /// <summary>
        /// 重置为默认设置
        /// </summary>
        public void ResetToDefaults()
        {
            _settings = new BrowserSettings();
            SaveSettings();
        }
    }
}
