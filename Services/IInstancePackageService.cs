// CustomizedBrowser\Services\IInstancePackageService.cs
using CustomizedBrowser.Models;
using System.Threading.Tasks;

namespace CustomizedBrowser.Services
{
    /// <summary>
    /// 实例包服务接口
    /// 负责将实例（含登录状态）导出为实例包，以及从实例包导入实例
    /// </summary>
    public interface IInstancePackageService
    {
        /// <summary>
        /// 读取实例包清单（不执行导入）
        /// </summary>
        /// <param name="packagePath">实例包文件路径</param>
        /// <returns>实例包清单；文件无效时返回 null</returns>
        InstancePackageManifest? ReadManifest(string packagePath);

        /// <summary>
        /// 将指定实例导出为实例包
        /// </summary>
        /// <param name="instanceId">要导出的实例 ID</param>
        /// <param name="packagePath">导出的实例包文件路径</param>
        /// <returns>导出结果</returns>
        Task<InstanceExportResult> ExportAsync(string instanceId, string packagePath);

        /// <summary>
        /// 从实例包导入实例
        /// </summary>
        /// <param name="packagePath">实例包文件路径</param>
        /// <param name="targetInstanceId">导入后的实例 ID（可与包内实例 ID 不同）</param>
        /// <returns>导入结果</returns>
        Task<InstanceImportResult> ImportAsync(string packagePath, string targetInstanceId);
    }
}
