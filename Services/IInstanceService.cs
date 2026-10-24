// CustomizedBrowser\Services\IInstanceService.cs
using CustomizedBrowser.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CustomizedBrowser.Services
{
    /// <summary>
    /// 实例服务接口
    /// 负责浏览器实例的创建、管理、启动、停止等操作
    /// </summary>
    public interface IInstanceService
    {
        /// <summary>
        /// 获取所有实例列表
        /// </summary>
        List<InstanceInfo> GetAllInstances();

        /// <summary>
        /// 创建新实例
        /// </summary>
        Task<InstanceInfo?> CreateInstanceAsync(string name, string note = "");

        /// <summary>
        /// 启动实例
        /// </summary>
        Task<bool> StartInstanceAsync(string instanceId);

        /// <summary>
        /// 停止实例
        /// </summary>
        Task<bool> StopInstanceAsync(string instanceId);

        /// <summary>
        /// 停止所有正在运行的实例
        /// </summary>
        Task StopAllInstancesAsync();

        /// <summary>
        /// 删除实例
        /// </summary>
        Task<bool> DeleteInstanceAsync(string instanceId);

        /// <summary>
        /// 更新实例备注
        /// </summary>
        bool UpdateInstanceNote(string instanceId, string note);

        /// <summary>
        /// 重命名实例（同步重命名数据目录与备注记录，运行中的实例会先被停止）
        /// </summary>
        Task<bool> RenameInstanceAsync(string oldInstanceId, string newInstanceId);

        /// <summary>
        /// 获取实例备注
        /// </summary>
        string GetInstanceNote(string instanceId);

        /// <summary>
        /// 批量启动实例
        /// </summary>
        Task<int> BatchStartInstancesAsync(List<string> instanceIds);

        /// <summary>
        /// 批量停止实例
        /// </summary>
        Task<int> BatchStopInstancesAsync(List<string> instanceIds);

        /// <summary>
        /// 批量删除实例
        /// </summary>
        Task<int> BatchDeleteInstancesAsync(List<string> instanceIds);

        /// <summary>
        /// 检查实例是否存在
        /// </summary>
        bool InstanceExists(string instanceId);

        /// <summary>
        /// 生成唯一实例ID
        /// </summary>
        string GenerateUniqueInstanceId();
    }
}
