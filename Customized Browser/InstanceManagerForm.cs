using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;
using System.Management;
using System.Linq;

namespace SimpleBrowser
{
    /// <summary>
    /// 实例管理窗口，用于显示和管理所有浏览器实例
    /// </summary>
    public partial class InstanceManagerForm : Form
    {
        /// <summary>
        /// 实例信息类
        /// </summary>
        private class InstanceInfo
        {
            public string Name { get; set; }
            public string Status { get; set; }
            public string LastUsed { get; set; }
            public bool IsRunning { get; set; }
            public string DataPath { get; set; }
            public string Note { get; set; } // 实例备注
            public bool IsSelected { get; set; } // 是否被选中用于批量操作
        }

        // 实例列表
        private ListView instanceListView;
        private List<InstanceInfo> instances = new List<InstanceInfo>();
        
        // 备注文件路径
        private const string NOTES_FILE = "instance_notes.json";
        
        // 批量操作相关
        private bool isMultiSelectMode = false;
        private int selectedInstanceCount = 0;

        /// <summary>
        /// 构造函数
        /// </summary>
        public InstanceManagerForm()
        {
            InitializeUI();
            LoadInstances();
        }

        /// <summary>
        /// 初始化用户界面
        /// </summary>
        private void InitializeUI()
        {
            // 设置窗口属性
            this.Text = "实例管理";
            this.Size = new Size(900, 600);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            // Windows 10/11风格设置
            this.Font = new Font("Segoe UI", 9);
            this.BackColor = Color.FromArgb(248, 248, 248);
            this.ForeColor = Color.FromArgb(31, 31, 35);

            // 创建主面板
            TableLayoutPanel mainPanel = new TableLayoutPanel();
            mainPanel.Dock = DockStyle.Fill;
            mainPanel.ColumnCount = 1;
            mainPanel.RowCount = 3;
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
            this.Controls.Add(mainPanel);

            // 创建顶部按钮面板
            FlowLayoutPanel topButtonPanel = new FlowLayoutPanel();
            topButtonPanel.Dock = DockStyle.Fill;
            topButtonPanel.FlowDirection = FlowDirection.LeftToRight;
            topButtonPanel.Padding = new Padding(10);
            topButtonPanel.BackColor = Color.White;
            mainPanel.Controls.Add(topButtonPanel, 0, 0);

            // 新建实例按钮
            Button newInstanceBtn = new Button();
            newInstanceBtn.Text = "新建实例";
            newInstanceBtn.Size = new Size(120, 36);
            newInstanceBtn.BackColor = Color.FromArgb(0, 120, 215); // 微软蓝
            newInstanceBtn.ForeColor = Color.White;
            newInstanceBtn.FlatStyle = FlatStyle.Flat;
            newInstanceBtn.FlatAppearance.BorderSize = 0;
            newInstanceBtn.Click += NewInstanceButton_Click;
            topButtonPanel.Controls.Add(newInstanceBtn);

            // 批量操作按钮
            Button batchOperationBtn = new Button();
            batchOperationBtn.Text = "批量操作";
            batchOperationBtn.Size = new Size(120, 36);
            batchOperationBtn.BackColor = Color.FromArgb(248, 248, 248);
            batchOperationBtn.ForeColor = Color.FromArgb(31, 31, 35);
            batchOperationBtn.FlatStyle = FlatStyle.Flat;
            batchOperationBtn.FlatAppearance.BorderSize = 1;
            batchOperationBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            batchOperationBtn.Click += BatchOperationButton_Click;
            topButtonPanel.Controls.Add(batchOperationBtn);

            // 刷新按钮
            Button refreshBtn = new Button();
            refreshBtn.Text = "刷新";
            refreshBtn.Size = new Size(100, 36);
            refreshBtn.BackColor = Color.FromArgb(248, 248, 248);
            refreshBtn.ForeColor = Color.FromArgb(31, 31, 35);
            refreshBtn.FlatStyle = FlatStyle.Flat;
            refreshBtn.FlatAppearance.BorderSize = 1;
            refreshBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            refreshBtn.Click += RefreshButton_Click;
            topButtonPanel.Controls.Add(refreshBtn);

            // 状态标签
            Label statusLabel = new Label();
            statusLabel.Text = "请选择需要操作的实例";
            statusLabel.AutoSize = true;
            statusLabel.Font = new Font("Segoe UI", 11);
            statusLabel.ForeColor = Color.FromArgb(110, 110, 115);
            statusLabel.Margin = new Padding(20, 8, 0, 0);
            topButtonPanel.Controls.Add(statusLabel);

            // 创建实例列表容器
            Panel instanceContainer = new Panel();
            instanceContainer.Dock = DockStyle.Fill;
            instanceContainer.AutoScroll = true;
            mainPanel.Controls.Add(instanceContainer, 0, 1);

            // 创建实例列表视图
            instanceListView = new ListView();
            instanceListView.Dock = DockStyle.Fill;
            instanceListView.View = View.Details;
            instanceListView.FullRowSelect = true;
            instanceListView.GridLines = true;
            instanceListView.MultiSelect = true;
            instanceListView.BackColor = Color.White;
            instanceListView.ForeColor = Color.FromArgb(31, 31, 35);
            instanceListView.Font = new Font("Segoe UI", 9);
            instanceListView.DoubleClick += InstanceListView_DoubleClick;
            instanceListView.MouseClick += InstanceListView_MouseClick;
            instanceContainer.Controls.Add(instanceListView);

            // 添加列
            instanceListView.Columns.Add("实例名称", 150);
            instanceListView.Columns.Add("状态", 100);
            instanceListView.Columns.Add("备注", 250);
            instanceListView.Columns.Add("上次使用", 150);
            instanceListView.Columns.Add("操作", 200);

            // 创建底部按钮面板
            FlowLayoutPanel bottomButtonPanel = new FlowLayoutPanel();
            bottomButtonPanel.Dock = DockStyle.Fill;
            bottomButtonPanel.FlowDirection = FlowDirection.RightToLeft;
            bottomButtonPanel.Padding = new Padding(10);
            bottomButtonPanel.BackColor = Color.White;
            mainPanel.Controls.Add(bottomButtonPanel, 0, 2);

            // 关闭按钮
            Button closeBtn = new Button();
            closeBtn.Text = "关闭";
            closeBtn.Size = new Size(100, 36);
            closeBtn.BackColor = Color.FromArgb(248, 248, 248);
            closeBtn.ForeColor = Color.FromArgb(31, 31, 35);
            closeBtn.FlatStyle = FlatStyle.Flat;
            closeBtn.FlatAppearance.BorderSize = 1;
            closeBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            closeBtn.Click += CloseButton_Click;
            bottomButtonPanel.Controls.Add(closeBtn);
        }

        /// <summary>
        /// 加载所有实例信息
        /// </summary>
        private void LoadInstances()
        {
            instances.Clear();
            instanceListView.Items.Clear();

            // 加载实例备注
            Dictionary<string, string> instanceNotes = LoadInstanceNotes();

            // 获取实例数据目录
            string instancesPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SimpleBrowser",
                "Instances"
            );

            if (Directory.Exists(instancesPath))
            {
                // 遍历所有实例目录
                string[] instanceDirs = Directory.GetDirectories(instancesPath);
                foreach (string dir in instanceDirs)
                {
                    string instanceName = Path.GetFileName(dir);
                    DirectoryInfo dirInfo = new DirectoryInfo(dir);

                    // 创建实例信息
                    InstanceInfo instance = new InstanceInfo
                    {
                        Name = instanceName,
                        Status = "已关闭",
                        LastUsed = dirInfo.LastWriteTime.ToString(),
                        IsRunning = false,
                        DataPath = dir,
                        Note = instanceNotes.ContainsKey(instanceName) ? instanceNotes[instanceName] : string.Empty,
                        IsSelected = false
                    };

                    // 检查实例是否正在当前进程中运行（包括后台运行的隐藏实例）
                    bool isVisibleRunning = false;
                    foreach (Form form in Application.OpenForms)
                    {
                        if (form is MainForm browserForm)
                        {
                            // 获取实例ID（通过公共属性）
                            string runningInstanceId = browserForm.InstanceId;
                            if (runningInstanceId == instanceName)
                            {
                                instance.Status = browserForm.Visible ? "运行中" : "后台运行";
                                instance.IsRunning = true;
                                isVisibleRunning = true;
                                break;
                            }
                        }
                    }
                    
                    // 如果没有在当前进程的可见窗口中找到实例，检查是否有相关的WebView进程在运行
                    if (!isVisibleRunning)
                    {
                        if (HasRunningWebViewProcesses(instanceName))
                        {
                            instance.Status = "后台运行";
                            instance.IsRunning = true;
                        }
                    }

                    instances.Add(instance);

                    // 创建列表项
                    ListViewItem item = new ListViewItem(instance.Name);
                    item.SubItems.Add(instance.Status);
                    item.SubItems.Add(string.IsNullOrEmpty(instance.Note) ? "无备注" : instance.Note);
                    item.SubItems.Add(instance.LastUsed);
                    item.SubItems.Add(""); // 操作列
                    item.Tag = instance;
                    
                    // 设置状态颜色
                    item.SubItems[1].ForeColor = GetStatusColor(instance.Status);
                    
                    instanceListView.Items.Add(item);
                }
            }
            else
            {
                // 显示空状态
                ShowEmptyState();
            }
        }

        /// <summary>
        /// 显示空状态
        /// </summary>
        private void ShowEmptyState()
        {
            // 清空列表
            instanceListView.Items.Clear();
            
            // 创建空状态提示项
            ListViewItem emptyItem = new ListViewItem("");
            emptyItem.SubItems.Add("");
            emptyItem.SubItems.Add("暂无实例，点击「新建实例」创建第一个浏览器实例");
            emptyItem.SubItems.Add("");
            emptyItem.SubItems.Add("");
            emptyItem.ForeColor = Color.FromArgb(110, 110, 115);
            instanceListView.Items.Add(emptyItem);
            
            // 调整列宽以适应空状态提示
            instanceListView.Columns[2].Width = 400;
        }

        /// <summary>
        /// 创建实例卡片
        /// </summary>
        /// <param name="instance">实例信息</param>
        /// <returns>实例卡片面板</returns>
        private Panel CreateInstanceCard(InstanceInfo instance)
        {
            Panel card = new Panel();
            card.Size = new Size(850, 120);
            card.BackColor = Color.White;
            card.BorderStyle = BorderStyle.None;
            card.Margin = new Padding(10);
            card.Padding = new Padding(15);
            card.Tag = instance;
            
            // 添加阴影效果
            card.Paint += (s, e) =>
            {
                Control control = (Control)s;
                using (Pen pen = new Pen(Color.FromArgb(200, 200, 200), 1))
                {
                    e.Graphics.DrawLine(pen, 0, control.Height - 1, control.Width, control.Height - 1);
                }
            };

            // 创建选中状态边框
            if (instance.IsSelected)
            {
                card.BorderStyle = BorderStyle.FixedSingle;
                // 使用默认边框颜色
            }

            // 创建布局面板
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 4;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            card.Controls.Add(layout);

            // 实例名称
            Label nameLabel = new Label();
            nameLabel.Text = instance.Name;
            nameLabel.Font = new Font("Segoe UI", 14, FontStyle.Bold);
            nameLabel.ForeColor = Color.FromArgb(31, 31, 35);
            nameLabel.AutoSize = true;
            layout.Controls.Add(nameLabel, 0, 0);

            // 状态标签
            Label statusLabel = new Label();
            statusLabel.Text = instance.Status;
            statusLabel.Font = new Font("Segoe UI", 12);
            statusLabel.ForeColor = GetStatusColor(instance.Status);
            statusLabel.AutoSize = true;
            layout.Controls.Add(statusLabel, 1, 0);

            // 备注
            Label noteLabel = new Label();
            noteLabel.Text = string.IsNullOrEmpty(instance.Note) ? "无备注" : instance.Note;
            noteLabel.Font = new Font("Segoe UI", 12);
            noteLabel.ForeColor = Color.FromArgb(110, 110, 115);
            noteLabel.AutoSize = true;
            noteLabel.MaximumSize = new Size(200, 0);
            layout.Controls.Add(noteLabel, 2, 0);

            // 最后使用时间
            Label lastUsedLabel = new Label();
            lastUsedLabel.Text = "上次使用: " + instance.LastUsed;
            lastUsedLabel.Font = new Font("Segoe UI", 11);
            lastUsedLabel.ForeColor = Color.FromArgb(110, 110, 115);
            lastUsedLabel.AutoSize = true;
            layout.Controls.Add(lastUsedLabel, 0, 1);

            // 操作按钮面板
            FlowLayoutPanel buttonPanel = new FlowLayoutPanel();
            buttonPanel.FlowDirection = FlowDirection.RightToLeft;
            buttonPanel.AutoSize = true;
            layout.Controls.Add(buttonPanel, 3, 0);
            layout.SetRowSpan(buttonPanel, 2);

            // 打开按钮
            Button openBtn = new Button();
            openBtn.Text = instance.IsRunning ? "唤醒" : "打开";
            openBtn.Size = new Size(80, 32);
            openBtn.BackColor = Color.FromArgb(0, 120, 215);
            openBtn.ForeColor = Color.White;
            openBtn.FlatStyle = FlatStyle.Flat;
            openBtn.FlatAppearance.BorderSize = 0;
            openBtn.Click += (s, e) => OpenInstance(instance.Name);
            buttonPanel.Controls.Add(openBtn);

            // 编辑备注按钮
            Button editNoteBtn = new Button();
            editNoteBtn.Text = "编辑备注";
            editNoteBtn.Size = new Size(100, 32);
            editNoteBtn.BackColor = Color.FromArgb(248, 248, 248);
            editNoteBtn.ForeColor = Color.FromArgb(31, 31, 35);
            editNoteBtn.FlatStyle = FlatStyle.Flat;
            editNoteBtn.FlatAppearance.BorderSize = 1;
            editNoteBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            editNoteBtn.Click += (s, e) => EditNoteButton_Click(instance);
            buttonPanel.Controls.Add(editNoteBtn);

            // 关闭按钮（仅当实例运行时显示）
            if (instance.IsRunning)
            {
                Button closeBtn = new Button();
                closeBtn.Text = "关闭";
                closeBtn.Size = new Size(80, 32);
                closeBtn.BackColor = Color.FromArgb(248, 248, 248);
                closeBtn.ForeColor = Color.FromArgb(31, 31, 35);
                closeBtn.FlatStyle = FlatStyle.Flat;
                closeBtn.FlatAppearance.BorderSize = 1;
                closeBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
                closeBtn.Click += (s, e) => CloseInstance(instance.Name);
                buttonPanel.Controls.Add(closeBtn);
            }

            // 删除按钮（仅当实例未运行时显示）
            if (!instance.IsRunning)
            {
                Button deleteBtn = new Button();
                deleteBtn.Text = "删除";
                deleteBtn.Size = new Size(80, 32);
                deleteBtn.BackColor = Color.FromArgb(248, 248, 248);
                deleteBtn.ForeColor = Color.FromArgb(232, 17, 35);
                deleteBtn.FlatStyle = FlatStyle.Flat;
                deleteBtn.FlatAppearance.BorderSize = 1;
                deleteBtn.FlatAppearance.BorderColor = Color.FromArgb(232, 17, 35);
                deleteBtn.Click += (s, e) => DeleteInstance(instance);
                buttonPanel.Controls.Add(deleteBtn);
            }

            // 批量选择复选框
            if (isMultiSelectMode)
            {
                CheckBox selectCheckBox = new CheckBox();
                selectCheckBox.Checked = instance.IsSelected;
                selectCheckBox.AutoSize = true;
                selectCheckBox.Location = new Point(10, 10);
                selectCheckBox.CheckedChanged += (s, e) =>
                {
                    instance.IsSelected = selectCheckBox.Checked;
                    UpdateSelectedInstanceCount();
                };
                card.Controls.Add(selectCheckBox);
            }

            return card;
        }

        /// <summary>
        /// 根据状态获取对应的颜色
        /// </summary>
        /// <param name="status">实例状态</param>
        /// <returns>状态对应的颜色</returns>
        private Color GetStatusColor(string status)
        {
            switch (status)
            {
                case "运行中":
                    return Color.FromArgb(0, 185, 0); // 绿色
                case "后台运行":
                    return Color.FromArgb(255, 140, 0); // 橙色
                case "已关闭":
                    return Color.FromArgb(110, 110, 115); // 中灰
                case "启动中":
                    return Color.FromArgb(0, 120, 215); // 微软蓝
                case "停止中":
                    return Color.FromArgb(255, 140, 0); // 橙色
                case "启动失败":
                    return Color.FromArgb(232, 17, 35); // 红色
                case "进程异常":
                    return Color.FromArgb(232, 17, 35); // 红色
                default:
                    return Color.FromArgb(110, 110, 115);
            }
        }

        /// <summary>
        /// 更新选中实例数量
        /// </summary>
        private void UpdateSelectedInstanceCount()
        {
            selectedInstanceCount = instances.FindAll(i => i.IsSelected).Count;
            // 更新状态栏
            foreach (Control control in Controls)
            {
                if (control is TableLayoutPanel mainPanel && mainPanel.Controls.Count > 0)
                {
                    foreach (Control topControl in mainPanel.Controls)
                    {
                        if (topControl is FlowLayoutPanel topButtonPanel)
                        {
                            foreach (Control childControl in topButtonPanel.Controls)
                            {
                                if (childControl is Label statusLabel && statusLabel.Text.StartsWith("请选择需要操作的实例"))
                                {
                                    if (selectedInstanceCount > 0)
                                    {
                                        statusLabel.Text = $"已选中 {selectedInstanceCount} 个实例";
                                        statusLabel.ForeColor = Color.FromArgb(0, 120, 215);
                                    }
                                    else
                                    {
                                        statusLabel.Text = "请选择需要操作的实例";
                                        statusLabel.ForeColor = Color.FromArgb(110, 110, 115);
                                    }
                                    break;
                                }
                            }
                            break;
                        }
                    }
                    break;
                }
            }
        }
        
        /// <summary>
        /// 加载实例备注
        /// </summary>
        private Dictionary<string, string> LoadInstanceNotes()
        {
            try
            {
                // 获取备注文件路径
                string notesFilePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SimpleBrowser",
                    NOTES_FILE
                );
                
                if (File.Exists(notesFilePath))
                {
                    string json = File.ReadAllText(notesFilePath);
                    return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载实例备注失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            
            return new Dictionary<string, string>();
        }
        
        /// <summary>
        /// 保存实例备注
        /// </summary>
        private void SaveInstanceNotes(Dictionary<string, string> notes)
        {
            try
            {
                // 获取备注文件路径
                string simpleBrowserPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SimpleBrowser"
                );
                
                // 确保目录存在
                Directory.CreateDirectory(simpleBrowserPath);
                
                string notesFilePath = Path.Combine(simpleBrowserPath, NOTES_FILE);
                string json = JsonSerializer.Serialize(notes, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(notesFilePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存实例备注失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 打开选中的实例
        /// </summary>
        private void OpenInstance(string instanceName)
        {
            // 检查实例是否已经在当前进程中运行（包括后台运行的隐藏实例）
            bool isRunningInCurrentProcess = false;
            foreach (Form form in Application.OpenForms)
            {
                if (form is MainForm browserForm)
                {
                    // 实例已经在当前进程中运行，显示并激活窗口
                    if (browserForm.InstanceId == instanceName)
                    {
                        browserForm.Visible = true;
                        browserForm.Activate();
                        isRunningInCurrentProcess = true;
                        break;
                    }
                }
            }

            if (!isRunningInCurrentProcess)
            {
                // 检查是否有与该实例相关的WebView2进程在运行
                bool hasWebViewProcesses = HasRunningWebViewProcesses(instanceName);
                
                if (hasWebViewProcesses)
                {
                    // 如果有相关WebView2进程在运行，创建一个新的MainForm来控制它们
                    // 使用相同的实例名称确保连接到现有的用户数据目录和WebView进程
                    MainForm newInstance = new MainForm(instanceName);
                    newInstance.isRunningInBackground = false;
                    
                    // 显示实例窗口
                    newInstance.Show();
                }
                else
                {
                    // 如果没有相关WebView2进程在运行，创建一个全新的实例
                    MainForm newInstance = new MainForm(instanceName);
                    newInstance.isRunningInBackground = false;
                    
                    // 显示实例窗口
                    newInstance.Show();
                }
            }
        }
        
        /// <summary>
        /// 关闭选中的后台实例
        /// </summary>
        private void CloseInstance(string instanceName)
        {
            // 1. 查找并关闭当前进程中与实例相关的MainForm（无论是否可见）
            MainForm targetForm = null;
            foreach (Form form in Application.OpenForms)
            {
                if (form is MainForm browserForm && browserForm.InstanceId == instanceName)
                {
                    targetForm = browserForm;
                    break;
                }
            }
            
            if (targetForm != null)
            {
                targetForm.isRunningInBackground = false;
                targetForm.Close();
            }
            
            // 2. 刷新实例列表以更新状态
            LoadInstances();
        }
        
        /// <summary>
        /// 检查指定的进程ID是否存在
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <returns>如果进程存在则返回true，否则返回false</returns>
        private bool IsProcessRunning(int processId)
        {
            try
            {
                Process process = Process.GetProcessById(processId);
                return process != null && !process.HasExited;
            }
            catch (ArgumentException)
            {
                // 进程不存在时会抛出ArgumentException
                return false;
            }
            catch (Exception)
            {
                // 其他异常也返回false
                return false;
            }
        }
        
        /// <summary>
        /// 检查是否有与指定实例相关的WebView2进程在运行
        /// </summary>
        /// <param name="instanceName">实例名称</param>
        /// <returns>如果有相关进程运行则返回true，否则返回false</returns>
        private bool HasRunningWebViewProcesses(string instanceName)
        {
            try
            {
                // 获取所有msedgewebview2.exe进程
                Process[] webviewProcesses = Process.GetProcessesByName("msedgewebview2");
                
                // 获取当前实例的用户数据目录
                string instancesPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SimpleBrowser",
                    "Instances",
                    instanceName
                );
                
                // 检查每个msedgewebview2进程的命令行参数
                foreach (Process process in webviewProcesses)
                {
                    try
                    {
                        // 获取进程的命令行参数
                        string cmdLine = GetCommandLine(process);
                        
                        // 检查命令行中是否包含当前实例的用户数据目录
                        if (cmdLine.Contains(instancesPath))
                        {
                            return true;
                        }
                    }
                    catch (Exception)
                    {
                        // 忽略单个进程的异常，继续检查其他进程
                        continue;
                    }
                }
                
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }
        
        /// <summary>
        /// 获取进程的命令行参数
        /// </summary>
        /// <param name="process">进程对象</param>
        /// <returns>命令行参数字符串</returns>
        private string GetCommandLine(Process process)
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT CommandLine FROM Win32_Process WHERE ProcessId = " + process.Id))
                {
                    using (ManagementObjectCollection objects = searcher.Get())
                    {
                        foreach (ManagementObject obj in objects)
                        {
                            return obj["CommandLine"]?.ToString() ?? "";
                        }
                    }
                }
            }
            catch (Exception)
            {
                return "";
            }
            return "";
        }
        
        /// <summary>
        /// 从实例目录加载进程ID列表
        /// </summary>
        /// <param name="instanceDir">实例目录</param>
        /// <returns>进程ID列表</returns>
        private List<int> LoadProcessIdsFromInstance(string instanceDir)
        {
            try
            {
                string processIdFile = Path.Combine(instanceDir, "process_ids.json");
                if (File.Exists(processIdFile))
                {
                    string json = File.ReadAllText(processIdFile);
                    var content = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<int>>>(json);
                    return content?.ContainsKey("ProcessIds") == true ? content["ProcessIds"] : new List<int>();
                }
            }
            catch (Exception)
            {
                // 异常时返回空列表
            }
            
            return new List<int>();
        }

        /// <summary>
        /// 删除选中的实例
        /// </summary>
        private void DeleteInstance(InstanceInfo instance)
        {
            if (instance.IsRunning)
            {
                MessageBox.Show("无法删除正在运行的实例！", "删除失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            DialogResult result = MessageBox.Show(
                $"确定要删除实例 '{instance.Name}' 吗？此操作将删除所有相关数据！",
                "删除确认",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (result == DialogResult.Yes)
            {
                try
                {
                    // 删除实例数据目录
                    Directory.Delete(instance.DataPath, true);
                    LoadInstances(); // 重新加载实例列表
                    MessageBox.Show("实例已成功删除！", "删除成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"删除实例失败: {ex.Message}", "删除失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>
        /// 列表视图双击事件
        /// </summary>
        private void InstanceListView_DoubleClick(object sender, EventArgs e)
        {
            if (instanceListView.SelectedItems.Count > 0)
            {
                ListViewItem selectedItem = instanceListView.SelectedItems[0];
                if (selectedItem.Tag is InstanceInfo instance)
                {
                    OpenInstance(instance.Name);
                }
            }
        }

        /// <summary>
        /// 列表视图鼠标点击事件
        /// </summary>
        private void InstanceListView_MouseClick(object sender, MouseEventArgs e)
        {
            // 处理右键菜单
            if (e.Button == MouseButtons.Right)
            {
                ListViewItem clickedItem = instanceListView.GetItemAt(e.X, e.Y);
                if (clickedItem != null && clickedItem.Tag is InstanceInfo instance)
                {
                    // 选择点击的项
                    instanceListView.SelectedItems.Clear();
                    clickedItem.Selected = true;
                    
                    // 创建上下文菜单
                    ContextMenuStrip contextMenu = new ContextMenuStrip();
                    
                    // 打开/唤醒菜单项
                    ToolStripMenuItem openMenuItem = new ToolStripMenuItem(instance.IsRunning ? "唤醒" : "打开");
                    openMenuItem.Click += (s, evt) => OpenInstance(instance.Name);
                    contextMenu.Items.Add(openMenuItem);
                    
                    // 编辑备注菜单项
                    ToolStripMenuItem editNoteMenuItem = new ToolStripMenuItem("编辑备注");
                    editNoteMenuItem.Click += (s, evt) => EditNoteButton_Click(instance);
                    contextMenu.Items.Add(editNoteMenuItem);
                    
                    // 关闭菜单项（仅当实例运行时显示）
                    if (instance.IsRunning)
                    {
                        ToolStripMenuItem closeMenuItem = new ToolStripMenuItem("关闭");
                        closeMenuItem.Click += (s, evt) => CloseInstance(instance.Name);
                        contextMenu.Items.Add(closeMenuItem);
                    }
                    
                    // 删除菜单项（仅当实例未运行时显示）
                    if (!instance.IsRunning)
                    {
                        ToolStripMenuItem deleteMenuItem = new ToolStripMenuItem("删除");
                        deleteMenuItem.ForeColor = Color.Red;
                        deleteMenuItem.Click += (s, evt) => DeleteInstance(instance);
                        contextMenu.Items.Add(deleteMenuItem);
                    }
                    
                    // 显示上下文菜单
                    contextMenu.Show(instanceListView, e.Location);
                }
            }
        }

        #region 事件处理方法

        /// <summary>
        /// 刷新按钮点击事件
        /// </summary>
        private void RefreshButton_Click(object sender, EventArgs e)
        {
            LoadInstances();
        }

        /// <summary>
        /// 关闭按钮点击事件
        /// </summary>
        private void CloseButton_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// 新建实例按钮点击事件
        /// </summary>
        private void NewInstanceButton_Click(object sender, EventArgs e)
        {
            // 检查是否是首次创建实例
            bool isFirstInstance = instances.Count == 0;
            
            // 创建新建实例对话框
            Form newInstanceForm = new Form();
            newInstanceForm.Text = "新建浏览器实例";
            newInstanceForm.Size = isFirstInstance ? new Size(320, 240) : new Size(320, 220);
            newInstanceForm.StartPosition = FormStartPosition.CenterParent;
            newInstanceForm.FormBorderStyle = FormBorderStyle.FixedDialog;
            newInstanceForm.MaximizeBox = false;
            newInstanceForm.MinimizeBox = false;
            newInstanceForm.Font = new Font("Segoe UI", 12);
            newInstanceForm.BackColor = Color.White;
            
            // 创建实例名称标签
            Label nameLabel = new Label();
            nameLabel.Text = "实例名称 (*):";
            nameLabel.AutoSize = true;
            nameLabel.Location = new Point(20, 20);
            newInstanceForm.Controls.Add(nameLabel);
            
            // 创建实例名称文本框
            TextBox nameTextBox = new TextBox();
            nameTextBox.Location = new Point(20, 45);
            nameTextBox.Size = new Size(270, 28);
            nameTextBox.Font = new Font("Segoe UI", 12);
            nameTextBox.Text = "请输入实例名称";
            nameTextBox.GotFocus += (s, evt) => { if (nameTextBox.Text == "请输入实例名称") nameTextBox.Text = ""; };
            nameTextBox.LostFocus += (s, evt) => { if (string.IsNullOrEmpty(nameTextBox.Text)) nameTextBox.Text = "请输入实例名称"; };
            newInstanceForm.Controls.Add(nameTextBox);
            
            // 创建实例备注标签
            Label noteLabel = new Label();
            noteLabel.Text = "实例备注:";
            noteLabel.AutoSize = true;
            noteLabel.Location = new Point(20, 80);
            newInstanceForm.Controls.Add(noteLabel);
            
            // 创建实例备注文本框
            TextBox noteTextBox = new TextBox();
            noteTextBox.Location = new Point(20, 105);
            noteTextBox.Size = new Size(270, 28);
            noteTextBox.Font = new Font("Segoe UI", 12);
            noteTextBox.Text = "选填，区分实例用途";
            noteTextBox.GotFocus += (s, evt) => { if (noteTextBox.Text == "选填，区分实例用途") noteTextBox.Text = ""; };
            noteTextBox.LostFocus += (s, evt) => { if (string.IsNullOrEmpty(noteTextBox.Text)) noteTextBox.Text = "选填，区分实例用途"; };
            newInstanceForm.Controls.Add(noteTextBox);
            
            // 首次创建时显示引导提示
            if (isFirstInstance)
            {
                Label guideLabel = new Label();
                guideLabel.Text = "建议为不同用途创建实例（如「工作账号」「个人浏览」）";
                guideLabel.Font = new Font("Segoe UI", 11);
                guideLabel.ForeColor = Color.FromArgb(110, 110, 115);
                guideLabel.AutoSize = true;
                guideLabel.Location = new Point(20, 140);
                guideLabel.MaximumSize = new Size(270, 0);
                newInstanceForm.Controls.Add(guideLabel);
            }
            
            // 创建创建实例按钮
            Button createBtn = new Button();
            createBtn.Text = "创建实例";
            createBtn.Location = new Point(60, isFirstInstance ? 170 : 150);
            createBtn.Size = new Size(100, 36);
            createBtn.BackColor = Color.FromArgb(0, 120, 215);
            createBtn.ForeColor = Color.White;
            createBtn.FlatStyle = FlatStyle.Flat;
            createBtn.FlatAppearance.BorderSize = 0;
            createBtn.Click += (s, ev) => 
            {
                string instanceName = nameTextBox.Text.Trim();
                if (string.IsNullOrEmpty(instanceName))
                {
                    MessageBox.Show("实例名称不能为空，请填写", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                
                if (instanceName.Length > 20)
                {
                    MessageBox.Show("名称不能超过20个字符", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                
                // 检查名称是否已存在
                string instancesPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SimpleBrowser",
                    "Instances"
                );
                
                if (Directory.Exists(Path.Combine(instancesPath, instanceName)))
                {
                    MessageBox.Show("该名称已存在，可点击自动重命名", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                
                try
                {
                    // 创建实例
                    MainForm newInstance = new MainForm(instanceName);
                    
                    // 保存备注
                    if (!string.IsNullOrEmpty(noteTextBox.Text.Trim()))
                    {
                        Dictionary<string, string> notes = LoadInstanceNotes();
                        notes[instanceName] = noteTextBox.Text.Trim();
                        SaveInstanceNotes(notes);
                    }
                    
                    newInstanceForm.Close();
                    LoadInstances(); // 重新加载实例列表
                    
                    // 显示创建成功提示
                    MessageBox.Show($"实例「{instanceName}」创建成功", "创建成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"创建失败：{ex.Message}", "创建失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            newInstanceForm.Controls.Add(createBtn);
            
            // 创建取消按钮
            Button cancelBtn = new Button();
            cancelBtn.Text = "取消";
            cancelBtn.Location = new Point(180, isFirstInstance ? 170 : 150);
            cancelBtn.Size = new Size(100, 36);
            cancelBtn.BackColor = Color.FromArgb(248, 248, 248);
            cancelBtn.ForeColor = Color.FromArgb(31, 31, 35);
            cancelBtn.FlatStyle = FlatStyle.Flat;
            cancelBtn.FlatAppearance.BorderSize = 1;
            cancelBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            cancelBtn.Click += (s, ev) => newInstanceForm.Close();
            newInstanceForm.Controls.Add(cancelBtn);
            
            newInstanceForm.ShowDialog();
        }

        /// <summary>
        /// 批量操作按钮点击事件
        /// </summary>
        private void BatchOperationButton_Click(object sender, EventArgs e)
        {
            isMultiSelectMode = !isMultiSelectMode;
            
            if (isMultiSelectMode)
            {
                // 进入批量选择模式
                Button btn = (Button)sender;
                btn.Text = "完成选择";
                btn.BackColor = Color.FromArgb(0, 120, 215);
                btn.ForeColor = Color.White;
                
                // 启用多选
                instanceListView.MultiSelect = true;
                
                // 显示批量操作按钮
                ShowBatchOperationButtons();
            }
            else
            {
                // 退出批量选择模式
                Button btn = (Button)sender;
                btn.Text = "批量操作";
                btn.BackColor = Color.FromArgb(248, 248, 248);
                btn.ForeColor = Color.FromArgb(31, 31, 35);
                
                // 隐藏批量操作按钮
                HideBatchOperationButtons();
                
                // 取消所有选择
                instanceListView.SelectedItems.Clear();
                foreach (var instance in instances)
                {
                    instance.IsSelected = false;
                }
                selectedInstanceCount = 0;
                LoadInstances();
            }
        }

        /// <summary>
        /// 显示批量操作按钮
        /// </summary>
        private void ShowBatchOperationButtons()
        {
            // 添加批量操作按钮到底部面板
            foreach (Control control in Controls)
            {
                if (control is TableLayoutPanel mainPanel)
                {
                    foreach (Control bottomControl in mainPanel.Controls)
                    {
                        if (bottomControl is FlowLayoutPanel bottomButtonPanel && bottomButtonPanel.Controls.Count > 0)
                        {
                            // 清除现有按钮
                            bottomButtonPanel.Controls.Clear();
                            
                            // 添加批量操作按钮
                            Button batchStartBtn = new Button();
                            batchStartBtn.Text = "批量启动";
                            batchStartBtn.Size = new Size(100, 36);
                            batchStartBtn.BackColor = Color.FromArgb(0, 120, 215);
                            batchStartBtn.ForeColor = Color.White;
                            batchStartBtn.FlatStyle = FlatStyle.Flat;
                            batchStartBtn.FlatAppearance.BorderSize = 0;
                            batchStartBtn.Click += BatchStartButton_Click;
                            bottomButtonPanel.Controls.Add(batchStartBtn);
                            
                            Button batchStopBtn = new Button();
                            batchStopBtn.Text = "批量停止";
                            batchStopBtn.Size = new Size(100, 36);
                            batchStopBtn.BackColor = Color.FromArgb(255, 140, 0);
                            batchStopBtn.ForeColor = Color.White;
                            batchStopBtn.FlatStyle = FlatStyle.Flat;
                            batchStopBtn.FlatAppearance.BorderSize = 0;
                            batchStopBtn.Click += BatchStopButton_Click;
                            bottomButtonPanel.Controls.Add(batchStopBtn);
                            
                            Button batchDeleteBtn = new Button();
                            batchDeleteBtn.Text = "批量删除";
                            batchDeleteBtn.Size = new Size(100, 36);
                            batchDeleteBtn.BackColor = Color.FromArgb(232, 17, 35);
                            batchDeleteBtn.ForeColor = Color.White;
                            batchDeleteBtn.FlatStyle = FlatStyle.Flat;
                            batchDeleteBtn.FlatAppearance.BorderSize = 0;
                            batchDeleteBtn.Click += BatchDeleteButton_Click;
                            bottomButtonPanel.Controls.Add(batchDeleteBtn);
                            
                            Button batchNoteBtn = new Button();
                            batchNoteBtn.Text = "批量修改备注";
                            batchNoteBtn.Size = new Size(120, 36);
                            batchNoteBtn.BackColor = Color.FromArgb(248, 248, 248);
                            batchNoteBtn.ForeColor = Color.FromArgb(31, 31, 35);
                            batchNoteBtn.FlatStyle = FlatStyle.Flat;
                            batchNoteBtn.FlatAppearance.BorderSize = 1;
                            batchNoteBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
                            batchNoteBtn.Click += BatchNoteButton_Click;
                            bottomButtonPanel.Controls.Add(batchNoteBtn);
                            
                            Button closeBtn = new Button();
                            closeBtn.Text = "关闭";
                            closeBtn.Size = new Size(100, 36);
                            closeBtn.BackColor = Color.FromArgb(248, 248, 248);
                            closeBtn.ForeColor = Color.FromArgb(31, 31, 35);
                            closeBtn.FlatStyle = FlatStyle.Flat;
                            closeBtn.FlatAppearance.BorderSize = 1;
                            closeBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
                            closeBtn.Click += CloseButton_Click;
                            bottomButtonPanel.Controls.Add(closeBtn);
                            
                            break;
                        }
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// 隐藏批量操作按钮
        /// </summary>
        private void HideBatchOperationButtons()
        {
            // 恢复默认底部按钮
            foreach (Control control in Controls)
            {
                if (control is TableLayoutPanel mainPanel)
                {
                    foreach (Control bottomControl in mainPanel.Controls)
                    {
                        if (bottomControl is FlowLayoutPanel bottomButtonPanel)
                        {
                            bottomButtonPanel.Controls.Clear();
                            
                            Button closeBtn = new Button();
                            closeBtn.Text = "关闭";
                            closeBtn.Size = new Size(100, 36);
                            closeBtn.BackColor = Color.FromArgb(248, 248, 248);
                            closeBtn.ForeColor = Color.FromArgb(31, 31, 35);
                            closeBtn.FlatStyle = FlatStyle.Flat;
                            closeBtn.FlatAppearance.BorderSize = 1;
                            closeBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
                            closeBtn.Click += CloseButton_Click;
                            bottomButtonPanel.Controls.Add(closeBtn);
                            
                            break;
                        }
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// 批量启动按钮点击事件
        /// </summary>
        private void BatchStartButton_Click(object sender, EventArgs e)
        {
            var selectedInstances = new List<InstanceInfo>();
            foreach (ListViewItem item in instanceListView.SelectedItems)
            {
                if (item.Tag is InstanceInfo instance)
                {
                    selectedInstances.Add(instance);
                }
            }
            
            if (selectedInstances.Count == 0)
            {
                MessageBox.Show("请先选择至少一个实例！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            int successCount = 0;
            int failCount = 0;
            
            foreach (var instance in selectedInstances)
            {
                try
                {
                    OpenInstance(instance.Name);
                    successCount++;
                }
                catch (Exception ex)
                {
                    failCount++;
                }
            }
            
            MessageBox.Show($"已启动 {successCount} 个，{failCount} 个失败", "批量启动结果", MessageBoxButtons.OK, MessageBoxIcon.Information);
            LoadInstances();
        }

        /// <summary>
        /// 批量停止按钮点击事件
        /// </summary>
        private void BatchStopButton_Click(object sender, EventArgs e)
        {
            var selectedInstances = new List<InstanceInfo>();
            foreach (ListViewItem item in instanceListView.SelectedItems)
            {
                if (item.Tag is InstanceInfo instance && instance.IsRunning)
                {
                    selectedInstances.Add(instance);
                }
            }
            
            if (selectedInstances.Count == 0)
            {
                MessageBox.Show("请先选择至少一个运行中的实例！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            DialogResult result = MessageBox.Show(
                $"将停止 {selectedInstances.Count} 个实例，是否继续？",
                "批量停止确认",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );
            
            if (result == DialogResult.Yes)
            {
                int successCount = 0;
                int failCount = 0;
                
                foreach (var instance in selectedInstances)
                {
                    try
                    {
                        CloseInstance(instance.Name);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                    }
                }
                
                MessageBox.Show($"已成功停止 {successCount} 个实例，{failCount} 个异常", "批量停止结果", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadInstances();
            }
        }

        /// <summary>
        /// 批量删除按钮点击事件
        /// </summary>
        private void BatchDeleteButton_Click(object sender, EventArgs e)
        {
            var selectedInstances = new List<InstanceInfo>();
            foreach (ListViewItem item in instanceListView.SelectedItems)
            {
                if (item.Tag is InstanceInfo instance && !instance.IsRunning)
                {
                    selectedInstances.Add(instance);
                }
            }
            
            if (selectedInstances.Count == 0)
            {
                MessageBox.Show("请先选择至少一个已关闭的实例！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            DialogResult result = MessageBox.Show(
                $"⚠️ 危险操作：将删除 {selectedInstances.Count} 个实例，数据将永久清除，无法恢复！",
                "批量删除确认",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );
            
            if (result == DialogResult.Yes)
            {
                int successCount = 0;
                int failCount = 0;
                
                foreach (var instance in selectedInstances)
                {
                    try
                    {
                        Directory.Delete(instance.DataPath, true);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                    }
                }
                
                MessageBox.Show($"已删除 {successCount} 个实例，数据已清理", "批量删除结果", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadInstances();
            }
        }

        /// <summary>
        /// 批量修改备注按钮点击事件
        /// </summary>
        private void BatchNoteButton_Click(object sender, EventArgs e)
        {
            var selectedInstances = new List<InstanceInfo>();
            foreach (ListViewItem item in instanceListView.SelectedItems)
            {
                if (item.Tag is InstanceInfo instance)
                {
                    selectedInstances.Add(instance);
                }
            }
            
            if (selectedInstances.Count == 0)
            {
                MessageBox.Show("请先选择至少一个实例！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            // 创建批量修改备注对话框
            Form batchNoteForm = new Form();
            batchNoteForm.Text = "批量修改备注";
            batchNoteForm.Size = new Size(320, 180);
            batchNoteForm.StartPosition = FormStartPosition.CenterParent;
            batchNoteForm.FormBorderStyle = FormBorderStyle.FixedDialog;
            batchNoteForm.MaximizeBox = false;
            batchNoteForm.MinimizeBox = false;
            batchNoteForm.Font = new Font("Segoe UI", 12);
            batchNoteForm.BackColor = Color.White;
            
            Label noteLabel = new Label();
            noteLabel.Text = "输入统一备注，不填则清空：";
            noteLabel.AutoSize = true;
            noteLabel.Location = new Point(20, 20);
            batchNoteForm.Controls.Add(noteLabel);
            
            TextBox noteTextBox = new TextBox();
            noteTextBox.Location = new Point(20, 45);
            noteTextBox.Size = new Size(270, 28);
            noteTextBox.Font = new Font("Segoe UI", 12);
            batchNoteForm.Controls.Add(noteTextBox);
            
            Button saveBtn = new Button();
            saveBtn.Text = "保存";
            saveBtn.Location = new Point(60, 100);
            saveBtn.Size = new Size(100, 36);
            saveBtn.BackColor = Color.FromArgb(0, 120, 215);
            saveBtn.ForeColor = Color.White;
            saveBtn.FlatStyle = FlatStyle.Flat;
            saveBtn.FlatAppearance.BorderSize = 0;
            saveBtn.Click += (s, ev) => 
            {
                string note = noteTextBox.Text.Trim();
                Dictionary<string, string> notes = LoadInstanceNotes();
                
                foreach (var instance in selectedInstances)
                {
                    if (string.IsNullOrEmpty(note))
                    {
                        notes.Remove(instance.Name);
                    }
                    else
                    {
                        notes[instance.Name] = note;
                    }
                }
                
                SaveInstanceNotes(notes);
                batchNoteForm.Close();
                LoadInstances();
                
                MessageBox.Show($"已为 {selectedInstances.Count} 个实例更新备注", "批量修改备注结果", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            batchNoteForm.Controls.Add(saveBtn);
            
            Button cancelBtn = new Button();
            cancelBtn.Text = "取消";
            cancelBtn.Location = new Point(180, 100);
            cancelBtn.Size = new Size(100, 36);
            cancelBtn.BackColor = Color.FromArgb(248, 248, 248);
            cancelBtn.ForeColor = Color.FromArgb(31, 31, 35);
            cancelBtn.FlatStyle = FlatStyle.Flat;
            cancelBtn.FlatAppearance.BorderSize = 1;
            cancelBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            cancelBtn.Click += (s, ev) => batchNoteForm.Close();
            batchNoteForm.Controls.Add(cancelBtn);
            
            batchNoteForm.ShowDialog();
        }

        /// <summary>
        /// 编辑备注按钮点击事件
        /// </summary>
        private void EditNoteButton_Click(InstanceInfo instance)
        {
            // 创建编辑备注对话框
            Form noteForm = new Form();
            noteForm.Text = "编辑实例备注";
            noteForm.Size = new Size(400, 200);
            noteForm.StartPosition = FormStartPosition.CenterParent;
            noteForm.FormBorderStyle = FormBorderStyle.FixedDialog;
            noteForm.MaximizeBox = false;
            noteForm.MinimizeBox = false;
            noteForm.Font = new Font("Segoe UI", 12);
            noteForm.BackColor = Color.White;
            
            // 创建备注标签
            Label noteLabel = new Label();
            noteLabel.Text = "备注:";
            noteLabel.AutoSize = true;
            noteLabel.Location = new Point(20, 20);
            noteForm.Controls.Add(noteLabel);
            
            // 创建备注文本框
            TextBox noteTextBox = new TextBox();
            noteTextBox.Text = instance.Note;
            noteTextBox.Multiline = true;
            noteTextBox.Size = new Size(340, 80);
            noteTextBox.Location = new Point(20, 40);
            noteTextBox.Font = new Font("Segoe UI", 12);
            noteForm.Controls.Add(noteTextBox);
            
            // 创建保存按钮
            Button saveBtn = new Button();
            saveBtn.Text = "保存";
            saveBtn.Size = new Size(80, 36);
            saveBtn.Location = new Point(120, 130);
            saveBtn.BackColor = Color.FromArgb(0, 120, 215);
            saveBtn.ForeColor = Color.White;
            saveBtn.FlatStyle = FlatStyle.Flat;
            saveBtn.FlatAppearance.BorderSize = 0;
            saveBtn.Click += (s, ev) => 
            {
                // 更新实例备注
                instance.Note = noteTextBox.Text;
                
                // 保存所有备注
                Dictionary<string, string> notes = new Dictionary<string, string>();
                foreach (InstanceInfo inst in instances)
                {
                    notes[inst.Name] = inst.Note;
                }
                SaveInstanceNotes(notes);
                
                noteForm.Close();
                LoadInstances(); // 重新加载实例列表
            };
            noteForm.Controls.Add(saveBtn);
            
            // 创建取消按钮
            Button cancelBtn = new Button();
            cancelBtn.Text = "取消";
            cancelBtn.Size = new Size(80, 36);
            cancelBtn.Location = new Point(210, 130);
            cancelBtn.BackColor = Color.FromArgb(248, 248, 248);
            cancelBtn.ForeColor = Color.FromArgb(31, 31, 35);
            cancelBtn.FlatStyle = FlatStyle.Flat;
            cancelBtn.FlatAppearance.BorderSize = 1;
            cancelBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            cancelBtn.Click += (s, ev) => noteForm.Close();
            noteForm.Controls.Add(cancelBtn);
            
            noteForm.ShowDialog();
        }

        /// <summary>
        /// 关闭实例按钮点击事件
        /// </summary>
        private void CloseInstanceButton_Click(object sender, EventArgs e)
        {
            Button btn = (Button)sender;
            Panel card = (Panel)btn.Parent;
            InstanceInfo instance = (InstanceInfo)card.Tag;
            
            if (instance.IsRunning)
            {
                DialogResult result = MessageBox.Show(
                    $"确定要关闭实例 '{instance.Name}' 吗？",
                    "关闭实例确认",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );
                if (result == DialogResult.Yes)
                {
                    CloseInstance(instance.Name); // 关闭后台实例
                    LoadInstances(); // 刷新实例列表
                }
            }
            else
            {
                MessageBox.Show("该实例当前未运行！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        /// <summary>
        /// 删除实例按钮点击事件
        /// </summary>
        private void DeleteInstanceButton_Click(object sender, EventArgs e)
        {
            Button btn = (Button)sender;
            Panel card = (Panel)btn.Parent;
            InstanceInfo instance = (InstanceInfo)card.Tag;
            
            if (instance.IsRunning)
            {
                MessageBox.Show("实例正在运行，请先关闭再删除！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            
            DialogResult result = MessageBox.Show(
                $"⚠️ 危险操作：删除实例 '{instance.Name}' 将清空所有数据，无法恢复！\n\n确定删除？",
                "删除实例",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );
            
            if (result == DialogResult.Yes)
            {
                try
                {
                    DeleteInstance(instance);
                    MessageBox.Show("实例已删除，数据已清理", "删除成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    LoadInstances(); // 重新加载实例列表
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"删除失败：{ex.Message}", "删除失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>
        /// 处理窗口关闭事件，防止关闭管理窗口时全部程序退出
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            
            // 检查是否还有其他浏览器实例在运行（包括后台运行的）
            bool hasRunningBrowserInstances = false;
            foreach (Form form in Application.OpenForms)
            {
                if (form is MainForm)
                {
                    hasRunningBrowserInstances = true;
                    break;
                }
            }
            
            // 如果没有其他浏览器实例在运行，询问是否退出程序
            if (!hasRunningBrowserInstances)
            {
                DialogResult result = MessageBox.Show(
                    "确定要退出程序吗？",
                    "退出确认",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );
                
                if (result == DialogResult.No)
                {
                    e.Cancel = true;
                }
                else
                {
                    // 如果用户选择退出，不阻止关闭
                    e.Cancel = false;
                }
            }
            else
            {
                // 如果有浏览器实例在运行，只关闭管理窗口，不退出程序
                e.Cancel = false;
            }
        }

        /// <summary>
        /// 计算两个时间之间的友好显示文本
        /// </summary>
        private string GetFriendlyTimeSpan(DateTime time)
        {
            TimeSpan span = DateTime.Now - time;
            
            if (span.TotalMinutes < 1)
                return "刚刚";
            else if (span.TotalMinutes < 60)
                return $"{Math.Floor(span.TotalMinutes)}分钟前";
            else if (span.TotalHours < 24)
                return $"{Math.Floor(span.TotalHours)}小时前";
            else if (span.TotalDays < 7)
                return $"{Math.Floor(span.TotalDays)}天前";
            else if (span.TotalDays < 30)
                return $"{Math.Floor(span.TotalDays / 7)}周前";
            else if (span.TotalDays < 365)
                return $"{Math.Floor(span.TotalDays / 30)}个月前";
            else
                return $"{Math.Floor(span.TotalDays / 365)}年前";
        }

        #endregion
    }
}