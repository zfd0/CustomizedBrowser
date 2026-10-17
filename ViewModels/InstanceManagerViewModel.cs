// CustomizedBrowser\ViewModels\InstanceManagerViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CustomizedBrowser.Models;
using CustomizedBrowser.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace CustomizedBrowser.ViewModels
{
    /// <summary>
    /// 实例管理窗口视图模型
    /// 负责实例管理窗口的业务逻辑和数据绑定
    /// </summary>
    public partial class InstanceManagerViewModel : ObservableObject
    {
        private readonly IInstanceService _instanceService;
        private readonly IInstancePackageService _packageService;

        [ObservableProperty]
        private ObservableCollection<InstanceInfo> _instances = new();

        [ObservableProperty]
        private InstanceInfo? _selectedInstance;

        /// <summary>
        /// 当前选中的实例集合（支持鼠标框选与 Ctrl/Shift 多选，由视图同步）
        /// </summary>
        [ObservableProperty]
        private List<InstanceInfo> _selectedInstances = new();

        [ObservableProperty]
        private bool _isMultiSelectMode = false;

        [ObservableProperty]
        private int _selectedInstanceCount = 0;

        /// <summary>
        /// 构造函数
        /// </summary>
        public InstanceManagerViewModel(IInstanceService instanceService, IInstancePackageService packageService)
        {
            _instanceService = instanceService;
            _packageService = packageService;
            LoadInstances();
        }

        /// <summary>
        /// 加载实例列表
        /// </summary>
        private void LoadInstances()
        {
            var instances = _instanceService.GetAllInstances();
            Instances.Clear();
            foreach (var instance in instances)
            {
                Instances.Add(instance);
            }
        }

        /// <summary>
        /// 刷新实例列表（供对话框完成提取/合并等操作后调用）
        /// </summary>
        public void RefreshInstances()
        {
            LoadInstances();
        }

        /// <summary>
        /// 选中实例集合变化 - 通知依赖选中状态的命令刷新可用性
        /// </summary>
        partial void OnSelectedInstancesChanged(List<InstanceInfo> value)
        {
            NotifySelectionCommands();
        }

        /// <summary>
        /// 主选中实例变化 - 通知依赖选中状态的命令刷新可用性
        /// </summary>
        partial void OnSelectedInstanceChanged(InstanceInfo? value)
        {
            NotifySelectionCommands();
        }

        /// <summary>
        /// 获取操作目标的实例 ID 列表
        /// 优先使用多选集合，未多选时回退为当前主选中实例
        /// </summary>
        private List<string> GetTargetInstanceIds()
        {
            if (SelectedInstances.Count > 0)
            {
                return SelectedInstances.Select(i => i.InstanceId).ToList();
            }

            return SelectedInstance != null
                ? new List<string> { SelectedInstance.InstanceId }
                : new List<string>();
        }

        /// <summary>
        /// 是否存在可操作的选中实例（用于命令可用性判断）
        /// </summary>
        private bool HasTargetInstances()
        {
            return SelectedInstances.Count > 0 || SelectedInstance != null;
        }

        /// <summary>
        /// 通知依赖选中状态的命令重新计算 CanExecute
        /// </summary>
        private void NotifySelectionCommands()
        {
            OpenInstanceCommand.NotifyCanExecuteChanged();
            StopInstanceCommand.NotifyCanExecuteChanged();
            DeleteInstanceCommand.NotifyCanExecuteChanged();
        }

        /// <summary>
        /// 打开实例命令（可批量打开所选实例）
        /// </summary>
        [RelayCommand(CanExecute = nameof(HasTargetInstances))]
        private async System.Threading.Tasks.Task OpenInstanceAsync()
        {
            var instanceIds = GetTargetInstanceIds();
            if (instanceIds.Count == 0)
            {
                return;
            }

            await _instanceService.BatchStartInstancesAsync(instanceIds);
            LoadInstances();
        }

        /// <summary>
        /// 创建新实例命令
        /// </summary>
        [RelayCommand]
        private async System.Threading.Tasks.Task CreateNewInstanceAsync()
        {
            string name = _instanceService.GenerateUniqueInstanceId();
            var newInstance = await _instanceService.CreateInstanceAsync(name);
            if (newInstance != null)
            {
                LoadInstances();
            }
        }

        /// <summary>
        /// 停止实例命令（可批量停止所选实例）
        /// </summary>
        [RelayCommand(CanExecute = nameof(HasTargetInstances))]
        private async System.Threading.Tasks.Task StopInstanceAsync()
        {
            var instanceIds = GetTargetInstanceIds();
            if (instanceIds.Count == 0)
            {
                return;
            }

            await _instanceService.BatchStopInstancesAsync(instanceIds);
            LoadInstances();
        }

        /// <summary>
        /// 删除实例命令（可批量删除所选实例）
        /// </summary>
        [RelayCommand(CanExecute = nameof(HasTargetInstances))]
        private async System.Threading.Tasks.Task DeleteInstanceAsync()
        {
            var instanceIds = GetTargetInstanceIds();
            if (instanceIds.Count == 0)
            {
                return;
            }

            string confirmText = instanceIds.Count == 1 && SelectedInstance != null
                ? $"确定要删除实例 \"{SelectedInstance.Name}\" 吗？此操作不可恢复！"
                : $"确定要删除选中的 {instanceIds.Count} 个实例吗？此操作不可恢复！";

            var result = MessageBox.Show(
                confirmText,
                "确认删除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            await _instanceService.BatchDeleteInstancesAsync(instanceIds);
            LoadInstances();
        }

        /// <summary>
        /// 导出实例命令（含登录状态）
        /// 导出前会先关闭该实例，以保存当前标签页并读取 Cookie
        /// </summary>
        [RelayCommand]
        private async System.Threading.Tasks.Task ExportInstanceAsync()
        {
            if (SelectedInstance == null)
            {
                MessageBox.Show("请先选择一个实例。", "导出实例", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 若实例正在运行，提示用户导出前会被关闭
            if (SelectedInstance.Status == InstanceStatus.Running)
            {
                var confirm = MessageBox.Show(
                    $"导出前会先关闭实例 \"{SelectedInstance.Name}\"，以保存当前标签页和登录状态。\n\n是否继续？",
                    "导出实例",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirm != MessageBoxResult.Yes)
                {
                    return;
                }
            }
            else
            {
                var confirm = MessageBox.Show(
                    $"实例 \"{SelectedInstance.Name}\" 当前未运行。\n" +
                    "建议先打开该实例并确认已处于登录状态后再导出，否则可能无法导出最新的登录状态。\n\n是否仍要继续？",
                    "导出实例",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (confirm != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            var dialog = new SaveFileDialog
            {
                Title = "导出实例",
                FileName = SelectedInstance.Name,
                DefaultExt = ".cbx",
                Filter = "Customized Browser 实例包 (*.cbx)|*.cbx|Zip 压缩包 (*.zip)|*.zip|所有文件 (*.*)|*.*"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var result = await _packageService.ExportAsync(SelectedInstance.InstanceId, dialog.FileName);

            // 导出过程会关闭实例，刷新列表以更新状态
            LoadInstances();

            MessageBox.Show(
                result.Message,
                result.Success ? "导出成功" : "导出失败",
                MessageBoxButton.OK,
                result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
        }

        /// <summary>
        /// 导入实例命令
        /// 从实例包中恢复实例（含登录状态）
        /// </summary>
        [RelayCommand]
        private async System.Threading.Tasks.Task ImportInstanceAsync()
        {
            var dialog = new OpenFileDialog
            {
                Title = "导入实例",
                DefaultExt = ".cbx",
                Filter = "Customized Browser 实例包 (*.cbx)|*.cbx|Zip 压缩包 (*.zip)|*.zip|所有文件 (*.*)|*.*"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var manifest = _packageService.ReadManifest(dialog.FileName);
            if (manifest == null)
            {
                MessageBox.Show("不是有效的实例包（缺少清单文件）。", "导入实例", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 确定目标实例 ID
            string targetId = manifest.InstanceId;
            if (string.IsNullOrWhiteSpace(targetId) || targetId.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
            {
                targetId = _instanceService.GenerateUniqueInstanceId();
            }

            // 处理同名实例冲突
            if (_instanceService.InstanceExists(targetId))
            {
                var conflict = MessageBox.Show(
                    $"实例 \"{targetId}\" 已存在。\n\n是：覆盖该实例\n否：以新名称导入",
                    "实例已存在",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (conflict == MessageBoxResult.Cancel)
                {
                    return;
                }
                if (conflict == MessageBoxResult.No)
                {
                    targetId = _instanceService.GenerateUniqueInstanceId();
                }
            }

            var result = await _packageService.ImportAsync(dialog.FileName, targetId);

            LoadInstances();

            MessageBox.Show(
                result.Message,
                result.Success ? "导入成功" : "导入失败",
                MessageBoxButton.OK,
                result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
        }

        /// <summary>
        /// 批量操作命令
        /// </summary>
        [RelayCommand]
        private void ToggleMultiSelectMode()
        {
            IsMultiSelectMode = !IsMultiSelectMode;
        }
    }
}
