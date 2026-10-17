using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;

namespace SimpleBrowser
{
    /// <summary>
    /// 集成主窗口，包含开始界面和首次实例创建功能
    /// </summary>
    public class IntegratedMainForm : Form
    {
        // 选项卡控件
        private TabControl mainTabControl;
        private TabPage startTabPage;

        /// <summary>
        /// 构造函数
        /// </summary>
        public IntegratedMainForm()
        {
            InitializeUI();
            
            // 检查是否首次启动
            CheckFirstStartup();
        }

        /// <summary>
        /// 检查是否首次启动
        /// </summary>
        private void CheckFirstStartup()
        {
            string instancesPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SimpleBrowser",
                "Instances"
            );
            
            // 检查是否存在实例目录
            if (!Directory.Exists(instancesPath) || Directory.GetDirectories(instancesPath).Length == 0)
            {
                // 首次启动，显示欢迎弹窗
                ShowWelcomeDialog();
            }
        }

        /// <summary>
        /// 显示欢迎弹窗
        /// </summary>
        private void ShowWelcomeDialog()
        {
            // 创建欢迎弹窗
            Form welcomeForm = new Form();
            welcomeForm.Text = "欢迎使用 Customized Browser";
            welcomeForm.Size = new Size(320, 280);
            welcomeForm.StartPosition = FormStartPosition.CenterScreen;
            welcomeForm.FormBorderStyle = FormBorderStyle.FixedDialog;
            welcomeForm.MaximizeBox = false;
            welcomeForm.MinimizeBox = false;
            welcomeForm.Font = new Font("Segoe UI", 12);
            welcomeForm.BackColor = Color.White;
            
            // 添加欢迎图标
            PictureBox welcomeIcon = new PictureBox();
            welcomeIcon.Size = new Size(64, 64);
            welcomeIcon.Location = new Point(20, 20);
            welcomeIcon.Image = SystemIcons.Application.ToBitmap();
            welcomeIcon.SizeMode = PictureBoxSizeMode.StretchImage;
            welcomeForm.Controls.Add(welcomeIcon);
            
            // 添加欢迎标题
            Label titleLabel = new Label();
            titleLabel.Text = "欢迎使用 Customized Browser";
            titleLabel.Font = new Font("Segoe UI", 14, FontStyle.Bold);
            titleLabel.ForeColor = Color.FromArgb(31, 31, 35);
            titleLabel.AutoSize = true;
            titleLabel.Location = new Point(90, 20);
            welcomeForm.Controls.Add(titleLabel);
            
            // 添加欢迎描述
            Label descLabel = new Label();
            descLabel.Text = "专为多实例隔离、后台运行设计，支持多账户同时登录与数据分离";
            descLabel.Font = new Font("Segoe UI", 12);
            descLabel.ForeColor = Color.FromArgb(69, 69, 73);
            descLabel.AutoSize = true;
            descLabel.Location = new Point(20, 70);
            descLabel.MaximumSize = new Size(280, 0);
            welcomeForm.Controls.Add(descLabel);
            
            // 添加按钮面板
            FlowLayoutPanel buttonPanel = new FlowLayoutPanel();
            buttonPanel.Size = new Size(280, 60);
            buttonPanel.Location = new Point(20, 180);
            buttonPanel.FlowDirection = FlowDirection.RightToLeft;
            buttonPanel.Padding = new Padding(10);
            welcomeForm.Controls.Add(buttonPanel);
            
            // 添加查看功能指南按钮
            Button guideBtn = new Button();
            guideBtn.Text = "查看功能指南";
            guideBtn.Size = new Size(120, 36);
            guideBtn.BackColor = Color.FromArgb(248, 248, 248);
            guideBtn.ForeColor = Color.FromArgb(31, 31, 35);
            guideBtn.FlatStyle = FlatStyle.Flat;
            guideBtn.FlatAppearance.BorderSize = 1;
            guideBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            guideBtn.Click += (s, e) =>
            {
                welcomeForm.Close();
                // 这里可以打开图文手册
                MessageBox.Show("功能指南：\n1. 多实例隔离 - 每个实例拥有独立数据目录\n2. 后台运行 - 支持最小化到托盘\n3. 批量操作 - 支持批量启动、停止、删除实例\n4. 安全控制 - 支持系统托盘锁定", "功能指南", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            buttonPanel.Controls.Add(guideBtn);
            
            // 添加立即体验按钮
            Button experienceBtn = new Button();
            experienceBtn.Text = "立即体验";
            experienceBtn.Size = new Size(100, 36);
            experienceBtn.BackColor = Color.FromArgb(0, 120, 215);
            experienceBtn.ForeColor = Color.White;
            experienceBtn.FlatStyle = FlatStyle.Flat;
            experienceBtn.FlatAppearance.BorderSize = 0;
            experienceBtn.Click += (s, e) =>
            {
                welcomeForm.Close();
                ShowCreateFirstInstanceDialog();
            };
            buttonPanel.Controls.Add(experienceBtn);
            
            // 显示欢迎弹窗
            welcomeForm.ShowDialog();
        }

        /// <summary>
        /// 显示创建第一个实例的对话框
        /// </summary>
        private void ShowCreateFirstInstanceDialog()
        {
            // 创建新建实例对话框
            Form newInstanceForm = new Form();
            newInstanceForm.Text = "新建浏览器实例";
            newInstanceForm.Size = new Size(320, 240);
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
            nameTextBox.GotFocus += (s, e) => { if (nameTextBox.Text == "请输入实例名称") nameTextBox.Text = ""; };
            nameTextBox.LostFocus += (s, e) => { if (string.IsNullOrEmpty(nameTextBox.Text)) nameTextBox.Text = "请输入实例名称"; };
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
            noteTextBox.GotFocus += (s, e) => { if (noteTextBox.Text == "选填，区分实例用途") noteTextBox.Text = ""; };
            noteTextBox.LostFocus += (s, e) => { if (string.IsNullOrEmpty(noteTextBox.Text)) noteTextBox.Text = "选填，区分实例用途"; };
            newInstanceForm.Controls.Add(noteTextBox);
            
            // 添加首次创建引导提示
            Label guideLabel = new Label();
            guideLabel.Text = "建议为不同用途创建实例（如「工作账号」「个人浏览」）";
            guideLabel.Font = new Font("Segoe UI", 11);
            guideLabel.ForeColor = Color.FromArgb(110, 110, 115);
            guideLabel.AutoSize = true;
            guideLabel.Location = new Point(20, 140);
            guideLabel.MaximumSize = new Size(270, 0);
            newInstanceForm.Controls.Add(guideLabel);
            
            // 创建创建实例按钮
            Button createBtn = new Button();
            createBtn.Text = "创建实例";
            createBtn.Size = new Size(100, 36);
            createBtn.Location = new Point(60, 170);
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
                    
                    newInstanceForm.Close();
                    
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
            cancelBtn.Size = new Size(100, 36);
            cancelBtn.Location = new Point(180, 170);
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
        /// 初始化用户界面
        /// </summary>
        private void InitializeUI()
        {
            // 设置窗口属性
            this.Text = "Customized Browser";  // 统一窗口标题
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            
            // Windows 10/11原生风格设置
            this.Font = new Font("Segoe UI", 9);
            this.BackColor = Color.FromArgb(248, 248, 248);
            this.ForeColor = Color.FromArgb(31, 31, 35);

            // 创建主选项卡控件
            mainTabControl = new TabControl();
            mainTabControl.Dock = DockStyle.Fill;
            mainTabControl.Alignment = TabAlignment.Top;
            mainTabControl.Multiline = false;
            mainTabControl.Font = new Font("微软雅黑", 9);
            mainTabControl.BackColor = Color.FromArgb(240, 240, 240);
            mainTabControl.ForeColor = Color.FromArgb(0, 0, 0);

            // 创建开始界面选项卡
            startTabPage = new TabPage("开始");
            startTabPage.BackColor = Color.FromArgb(240, 240, 240);
            InitializeStartTab();

            // 添加选项卡到控件
            mainTabControl.TabPages.Add(startTabPage);

            // 添加到主窗口
            this.Controls.Add(mainTabControl);
        }

        /// <summary>
        /// 初始化开始界面选项卡
        /// </summary>
        private void InitializeStartTab()
        {
            // 创建主面板
            TableLayoutPanel mainPanel = new TableLayoutPanel();
            mainPanel.Dock = DockStyle.Fill;
            mainPanel.ColumnCount = 1;
            mainPanel.RowCount = 3;
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 20));
            startTabPage.Controls.Add(mainPanel);

            // 创建标题标签
            Label titleLabel = new Label();
            titleLabel.Text = "欢迎使用Customized Browser";
            titleLabel.Font = new Font("微软雅黑", 14, FontStyle.Bold);
            titleLabel.TextAlign = ContentAlignment.MiddleCenter;
            titleLabel.Dock = DockStyle.Fill;
            mainPanel.Controls.Add(titleLabel, 0, 0);

            // 创建描述标签
            Label descLabel = new Label();
            descLabel.Text = "请选择要打开的实例或创建新实例";
            descLabel.TextAlign = ContentAlignment.MiddleCenter;
            descLabel.Dock = DockStyle.Fill;
            mainPanel.Controls.Add(descLabel, 0, 1);

            // 创建按钮面板
            TableLayoutPanel buttonPanel = new TableLayoutPanel();
            buttonPanel.Dock = DockStyle.Fill;
            buttonPanel.ColumnCount = 2;
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            buttonPanel.RowCount = 1;
            buttonPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            buttonPanel.Margin = new Padding(10);
            mainPanel.Controls.Add(buttonPanel, 0, 2);

            // 创建实例管理按钮
            Button manageBtn = new Button();
            manageBtn.Text = "管理实例";
            manageBtn.Size = new Size(150, 40);
            manageBtn.BackColor = Color.FromArgb(0, 120, 215);
            manageBtn.ForeColor = Color.White;
            manageBtn.FlatStyle = FlatStyle.Flat;
            manageBtn.FlatAppearance.BorderSize = 0;
            manageBtn.Click += (s, e) => 
            {
                InstanceManagerForm instanceManager = new InstanceManagerForm();
                instanceManager.Show();
            };
            manageBtn.Anchor = AnchorStyles.None;
            buttonPanel.Controls.Add(manageBtn, 0, 0);

            // 创建退出按钮
            Button exitBtn = new Button();
            exitBtn.Text = "退出";
            exitBtn.Size = new Size(100, 40);
            exitBtn.BackColor = Color.FromArgb(248, 248, 248);
            exitBtn.ForeColor = Color.FromArgb(31, 31, 35);
            exitBtn.FlatStyle = FlatStyle.Flat;
            exitBtn.FlatAppearance.BorderSize = 1;
            exitBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            exitBtn.Click += (s, e) => this.Close();
            exitBtn.Anchor = AnchorStyles.None;
            buttonPanel.Controls.Add(exitBtn, 1, 0);
        }
    }
}