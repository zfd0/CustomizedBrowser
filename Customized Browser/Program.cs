using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Drawing;

namespace SimpleBrowser
{
    /// <summary>
    /// 自定义ApplicationContext，用于管理程序的生命周期
    /// </summary>
    public class BrowserApplicationContext : ApplicationContext
    {
        // 系统托盘图标
        private NotifyIcon trayIcon;
        // 系统托盘上下文菜单
        private ContextMenuStrip trayMenu;
        // 系统托盘显示文本
        private string trayIconText = "Customized Browser";
        
        /// <summary>
        /// 构造函数
        /// </summary>
        public BrowserApplicationContext()
        {
            // 初始化全局应用程序上下文
            SimpleBrowser.MainForm.AppContext = this;
            
            // 初始化系统托盘
            InitializeTrayIcon();
            
            // 打开集成主窗口作为第一个窗口
            IntegratedMainForm mainForm = new IntegratedMainForm();
            mainForm.FormClosed += IntegratedMainForm_FormClosed;
            mainForm.Show();
            
            // 确保托盘图标可见
            if (trayIcon != null)
            {
                trayIcon.Visible = true;
            }
        }
        
        // 安全控制相关字段
        private bool isLocked = false;
        private System.Windows.Forms.Timer autoLockTimer;
        private const int AUTO_LOCK_TIME = 60000; // 自动锁定时间（毫秒）

        /// <summary>
        /// 初始化系统托盘图标
        /// </summary>
        private void InitializeTrayIcon()
        {
            // 创建上下文菜单
            trayMenu = new ContextMenuStrip();
            trayMenu.Font = new Font("Segoe UI", 12);
            
            // 添加管理实例选项
            ToolStripMenuItem manageInstancesMenuItem = new ToolStripMenuItem("管理实例");
            manageInstancesMenuItem.Click += ManageInstancesMenuItem_Click;
            trayMenu.Items.Add(manageInstancesMenuItem);
            
            // 添加分隔符
            trayMenu.Items.Add(new ToolStripSeparator());
            
            // 添加锁定/解锁选项
            ToolStripMenuItem lockMenuItem = new ToolStripMenuItem("锁定");
            lockMenuItem.Click += LockMenuItem_Click;
            trayMenu.Items.Add(lockMenuItem);
            
            // 添加分隔符
            trayMenu.Items.Add(new ToolStripSeparator());
            
            // 添加退出选项
            ToolStripMenuItem exitMenuItem = new ToolStripMenuItem("退出");
            exitMenuItem.Click += ExitMenuItem_Click;
            trayMenu.Items.Add(exitMenuItem);
            
            // 创建托盘图标
            trayIcon = new NotifyIcon
            {
                Text = trayIconText,
                Icon = SystemIcons.Application,
                ContextMenuStrip = trayMenu,
                Visible = true
            };
            
            // 添加鼠标点击事件
            trayIcon.MouseClick += TrayIcon_MouseClick;
            
            // 初始化自动锁定定时器
            InitializeAutoLockTimer();
        }

        /// <summary>
        /// 初始化自动锁定定时器
        /// </summary>
        private void InitializeAutoLockTimer()
        {
            autoLockTimer = new System.Windows.Forms.Timer();
            autoLockTimer.Interval = AUTO_LOCK_TIME;
            autoLockTimer.Tick += AutoLockTimer_Tick;
            autoLockTimer.Start();
        }

        /// <summary>
        /// 自动锁定定时器触发事件
        /// </summary>
        private void AutoLockTimer_Tick(object sender, EventArgs e)
        {
            if (!isLocked)
            {
                Lock();
            }
        }

        /// <summary>
        /// 锁定系统托盘
        /// </summary>
        private void Lock()
        {
            isLocked = true;
            UpdateTrayMenu();
            ShowTrayBalloon("系统托盘已锁定", "超过1分钟无操作，系统托盘已自动锁定", ToolTipIcon.Info);
        }

        /// <summary>
        /// 解锁系统托盘
        /// </summary>
        private bool Unlock()
        {
            // 显示密码验证对话框
            Form unlockForm = new Form();
            unlockForm.Text = "解锁系统托盘";
            unlockForm.Size = new Size(320, 180);
            unlockForm.StartPosition = FormStartPosition.CenterScreen;
            unlockForm.FormBorderStyle = FormBorderStyle.FixedDialog;
            unlockForm.MaximizeBox = false;
            unlockForm.MinimizeBox = false;
            unlockForm.Font = new Font("Segoe UI", 12);
            unlockForm.BackColor = Color.White;
            
            Label passwordLabel = new Label();
            passwordLabel.Text = "请输入密码：";
            passwordLabel.AutoSize = true;
            passwordLabel.Location = new Point(20, 30);
            unlockForm.Controls.Add(passwordLabel);
            
            TextBox passwordTextBox = new TextBox();
            passwordTextBox.Location = new Point(20, 60);
            passwordTextBox.Size = new Size(270, 28);
            passwordTextBox.Font = new Font("Segoe UI", 12);
            passwordTextBox.PasswordChar = '*';
            unlockForm.Controls.Add(passwordTextBox);
            
            Button unlockBtn = new Button();
            unlockBtn.Text = "解锁";
            unlockBtn.Location = new Point(60, 110);
            unlockBtn.Size = new Size(100, 36);
            unlockBtn.BackColor = Color.FromArgb(0, 120, 215);
            unlockBtn.ForeColor = Color.White;
            unlockBtn.FlatStyle = FlatStyle.Flat;
            unlockBtn.FlatAppearance.BorderSize = 0;
            unlockBtn.Click += (s, ev) => unlockForm.DialogResult = DialogResult.OK;
            unlockForm.Controls.Add(unlockBtn);
            
            Button cancelBtn = new Button();
            cancelBtn.Text = "取消";
            cancelBtn.Location = new Point(180, 110);
            cancelBtn.Size = new Size(100, 36);
            cancelBtn.BackColor = Color.FromArgb(248, 248, 248);
            cancelBtn.ForeColor = Color.FromArgb(31, 31, 35);
            cancelBtn.FlatStyle = FlatStyle.Flat;
            cancelBtn.FlatAppearance.BorderSize = 1;
            cancelBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            cancelBtn.Click += (s, ev) => unlockForm.DialogResult = DialogResult.Cancel;
            unlockForm.Controls.Add(cancelBtn);
            
            // 显示密码验证对话框
            DialogResult result = unlockForm.ShowDialog();
            
            if (result == DialogResult.OK)
            {
                // 这里可以添加密码验证逻辑
                // 为了简化，暂时直接解锁
                isLocked = false;
                UpdateTrayMenu();
                ResetAutoLockTimer();
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// 重置自动锁定定时器
        /// </summary>
        private void ResetAutoLockTimer()
        {
            autoLockTimer.Stop();
            autoLockTimer.Start();
        }

        /// <summary>
        /// 更新系统托盘菜单
        /// </summary>
        private void UpdateTrayMenu()
        {
            trayMenu.Items.Clear();
            
            if (!isLocked)
            {
                // 未锁定状态菜单
                ToolStripMenuItem manageInstancesMenuItem = new ToolStripMenuItem("管理实例");
                manageInstancesMenuItem.Click += ManageInstancesMenuItem_Click;
                trayMenu.Items.Add(manageInstancesMenuItem);
                
                // 添加分隔符
                trayMenu.Items.Add(new ToolStripSeparator());
                
                // 添加锁定选项
                ToolStripMenuItem lockMenuItem = new ToolStripMenuItem("锁定");
                lockMenuItem.Click += LockMenuItem_Click;
                trayMenu.Items.Add(lockMenuItem);
            }
            else
            {
                // 锁定状态菜单
                ToolStripMenuItem unlockMenuItem = new ToolStripMenuItem("解锁");
                unlockMenuItem.Click += UnlockMenuItem_Click;
                trayMenu.Items.Add(unlockMenuItem);
            }
            
            // 添加分隔符
            trayMenu.Items.Add(new ToolStripSeparator());
            
            // 添加退出选项
            ToolStripMenuItem exitMenuItem = new ToolStripMenuItem("退出");
            exitMenuItem.Click += ExitMenuItem_Click;
            trayMenu.Items.Add(exitMenuItem);
        }

        /// <summary>
        /// 显示托盘气泡通知
        /// </summary>
        private void ShowTrayBalloon(string title, string text, ToolTipIcon icon)
        {
            if (trayIcon != null)
            {
                trayIcon.ShowBalloonTip(3000, title, text, icon);
            }
        }
        
        /// <summary>
        /// 管理实例菜单项点击事件
        /// </summary>
        private void ManageInstancesMenuItem_Click(object sender, EventArgs e)
        {
            ResetAutoLockTimer();
            
            // 检查实例管理器是否已经打开
            bool instanceManagerOpen = false;
            foreach (Form form in Application.OpenForms)
            {
                if (form is InstanceManagerForm)
                {
                    instanceManagerOpen = true;
                    form.Activate();
                    break;
                }
            }
            
            // 如果实例管理器没有打开，创建并显示它
            if (!instanceManagerOpen)
            {
                InstanceManagerForm instanceManager = new InstanceManagerForm();
                instanceManager.Show();
            }
        }

        /// <summary>
        /// 锁定菜单项点击事件
        /// </summary>
        private void LockMenuItem_Click(object sender, EventArgs e)
        {
            Lock();
        }

        /// <summary>
        /// 解锁菜单项点击事件
        /// </summary>
        private void UnlockMenuItem_Click(object sender, EventArgs e)
        {
            Unlock();
        }

        /// <summary>
        /// 托盘图标鼠标点击事件
        /// </summary>
        private void TrayIcon_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (isLocked)
                {
                    Unlock();
                }
                else
                {
                    ResetAutoLockTimer();
                    ManageInstancesMenuItem_Click(sender, e);
                }
            }
        }

        /// <summary>
        /// 退出菜单项点击事件
        /// </summary>
        private void ExitMenuItem_Click(object sender, EventArgs e)
        {
            // 确保托盘图标被正确释放
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
                trayIcon = null;
            }
            
            if (trayMenu != null)
            {
                trayMenu.Dispose();
                trayMenu = null;
            }
            
            if (autoLockTimer != null)
            {
                autoLockTimer.Stop();
                autoLockTimer.Dispose();
                autoLockTimer = null;
            }
            
            // 退出应用程序
            ExitThread();
        }
        
        /// <summary>
        /// 更新系统托盘图标显示的文本
        /// </summary>
        /// <param name="newText">新的显示文本</param>
        public void UpdateTrayIconText(string newText)
        {
            if (!string.IsNullOrEmpty(newText))
            {
                trayIconText = newText;
                if (trayIcon != null)
                {
                    trayIcon.Text = trayIconText;
                }
            }
        }
        
        /// <summary>
        /// 集成主窗口关闭事件
        /// </summary>
        private void IntegratedMainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            // 检查是否有其他运行中的浏览器窗口（包括隐藏的）
            bool hasOtherForms = false;
            foreach (Form form in Application.OpenForms)
            {
                if (form is MainForm || form is InstanceManagerForm)
                {
                    hasOtherForms = true;
                    break;
                }
            }
            
            if (hasOtherForms)
            {
                // 如果有其他运行中的窗口，不要退出程序
                return;
            }
            
            // 没有其他窗口时，退出程序
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
                trayIcon = null;
            }
            
            if (trayMenu != null)
            {
                trayMenu.Dispose();
                trayMenu = null;
            }
            
            ExitThread();
        }
        
        /// <summary>
        /// 检查是否有运行中的浏览器窗口或实例管理窗口（包括后台隐藏的）
        /// </summary>
        private bool HasVisibleBrowserForms()
        {
            foreach (Form form in Application.OpenForms)
            {
                if (form is MainForm || form is InstanceManagerForm || form is IntegratedMainForm)
                {
                    return true;
                }
            }
            return false;
        }
    }
    
    internal static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            try
            {
                // 使用Mutex实现单实例检测
                bool createdNew;
                Mutex mutex = new Mutex(true, @"Global\CustomizedBrowserMutex", out createdNew);
                
                if (!createdNew)
                {
                    // 已经有实例在运行，尝试唤醒它
                    SendWakeupMessage();
                    
                    // 等待一段时间，确保SendWakeupMessage方法执行完毕
                    Thread.Sleep(1000);
                    
                    // 检查是否有CustomizedBrowser进程在运行
                    Process[] remainingProcesses = Process.GetProcessesByName("CustomizedBrowser");
                    if (remainingProcesses.Length == 0)
                    {
                        // 没有找到CustomizedBrowser进程，继续启动新实例
                        // 释放之前创建的Mutex
                        mutex.ReleaseMutex();
                        mutex.Dispose();
                        
                        // 重新创建Mutex并启动
                        mutex = new Mutex(true, @"Global\CustomizedBrowserMutex", out createdNew);
                    }
                    else
                    {
                        // 检查是否有集成主窗口在运行
                        IntPtr mainWindowHandle = FindWindow(null, "Customized Browser");
                        if (mainWindowHandle == IntPtr.Zero)
                        {
                            // 没有找到集成主窗口，继续启动新实例
                            // 释放之前创建的Mutex
                            mutex.ReleaseMutex();
                            mutex.Dispose();
                            
                            // 重新创建Mutex并启动
                            mutex = new Mutex(true, @"Global\CustomizedBrowserMutex", out createdNew);
                        }
                        else
                        {
                            // 仍然有实例在运行，退出新进程
                            Environment.Exit(0);
                            return;
                        }
                    }
                }
                
                // 启动新实例
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new BrowserApplicationContext());
                
                // 释放Mutex（应用程序退出时）
                mutex.ReleaseMutex();
                mutex.Dispose();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"程序启动失败: {ex.Message}\n\n{ex.StackTrace}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Windows API声明
        private const int SW_RESTORE = 9;
        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_RESTORE = 0xF120;
        
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool IsWindowVisible(IntPtr hWnd);
        
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        
        // 回调委托
        private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
        
        /// <summary>
        /// 发送唤醒消息到已有实例
        /// </summary>
        private static void SendWakeupMessage()
        {
            try
            {
                // 尝试查找并唤醒集成主窗口（无论是否可见）
                IntPtr hWnd = FindWindow(null, "Customized Browser");
                if (hWnd != IntPtr.Zero)
                {
                    WakeupWindow(hWnd);
                    return;
                }
                
                // 尝试查找并唤醒自定义浏览器窗口
                Process[] processes = Process.GetProcessesByName("CustomizedBrowser");
                bool foundValidWindow = false;
                
                foreach (Process process in processes)
                {
                    try
                    {
                        // 即使主窗口句柄为0，也尝试通过其他方式唤醒
                        // 先尝试获取主窗口句柄
                        if (process.MainWindowHandle != IntPtr.Zero)
                        {
                            // 不管窗口是否可见，都尝试唤醒它
                            WakeupWindow(process.MainWindowHandle);
                            foundValidWindow = true;
                            break;
                        }
                        else
                        {
                            // 主窗口句柄为0，可能是因为窗口被隐藏
                            // 尝试枚举进程的所有窗口
                            List<IntPtr> windows = new List<IntPtr>();
                            EnumWindows((hwnd, lParam) =>
                            {
                                uint processId;
                                GetWindowThreadProcessId(hwnd, out processId);
                                if (processId == process.Id)
                                {
                                    windows.Add(hwnd);
                                }
                                return true;
                            }, IntPtr.Zero);
                            
                            // 尝试唤醒找到的所有窗口
                            foreach (IntPtr window in windows)
                            {
                                WakeupWindow(window);
                                foundValidWindow = true;
                            }
                            
                            if (foundValidWindow)
                            {
                                break;
                            }
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }
                
                // 无论是否找到有效窗口，只要没有找到集成主窗口，就终止所有进程
                // 这样Main方法就会启动一个新的实例，显示集成主窗口
                foreach (Process process in processes)
                {
                    try
                    {
                        process.Kill();
                        process.WaitForExit(1000);
                    }
                    catch
                    {
                        continue;
                    }
                }
                
                // 短暂延迟确保进程完全退出
                Thread.Sleep(500);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"唤醒已有实例失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 唤醒指定窗口
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        private static void WakeupWindow(IntPtr hWnd)
        {
            // 先发送系统恢复消息
            SendMessage(hWnd, WM_SYSCOMMAND, (IntPtr)SC_RESTORE, IntPtr.Zero);
            
            // 尝试多种显示方式
            ShowWindow(hWnd, SW_RESTORE);  // 恢复最小化窗口
            ShowWindow(hWnd, 5);  // SW_SHOW = 5，显示窗口
            ShowWindow(hWnd, 3);  // SW_MAXIMIZE = 3，最大化窗口
            
            // 强制设置窗口为可见
            ShowWindow(hWnd, 8);  // SW_RESTORE = 9，恢复窗口
            
            // 发送WM_SHOWWINDOW消息
            SendMessage(hWnd, 0x0018, (IntPtr)1, IntPtr.Zero);  // WM_SHOWWINDOW
            
            // 将窗口置于前台
            SetForegroundWindow(hWnd);
            
            // 再次确保窗口可见
            ShowWindow(hWnd, SW_RESTORE);
        }
    }
}
