import tkinter as tk
from tkinter import ttk, messagebox, simpledialog
import pystray
from PIL import Image, ImageDraw
import threading
import time

# 全局配置
is_menu_unlocked = False  # 菜单是否解锁
auto_lock_timer = None    # 自动锁定计时器
lock_duration = 10000     # 自动锁定时长（10秒）
wake_verify = True        # 是否开启实例唤醒二次验证
instance_data = [
    ("实例1", "工作账号", "运行中"),
    ("实例2", "个人浏览", "后台运行"),
    ("实例3", "测试用", "已关闭")
]  # 模拟实例数据

class CustomBrowserUI:
    def __init__(self, root):
        self.root = root
        self.root.title("Customized Browser - 实例管理")
        self.root.geometry("800x500")
        
        # 设置样式
        self.style = ttk.Style()
        self.style.configure("Title.TLabel", font=("Segoe UI", 14, "bold"), foreground="#0078D7")
        self.style.configure("Status.TLabel", font=("Segoe UI", 11), foreground="#1F1F23")
        self.style.configure("Warning.TLabel", font=("Segoe UI", 11), foreground="#E81123")
        self.style.configure("Success.TLabel", font=("Segoe UI", 11), foreground="#00B900")
        
        # 创建主界面
        self.create_main_frame()
        
        # 初始化托盘图标（后台线程运行）
        threading.Thread(target=self.create_tray_icon, daemon=True).start()
        
        # 绑定窗口关闭事件
        self.root.protocol("WM_DELETE_WINDOW", self.on_close)

    def create_main_frame(self):
        """创建主界面（实例管理）"""
        # 标题栏
        title_frame = ttk.Frame(self.root)
        title_frame.pack(fill=tk.X, padx=20, pady=10)
        
        ttk.Label(title_frame, text="实例管理", style="Title.TLabel").pack(side=tk.LEFT)
        self.status_label = ttk.Label(title_frame, text="请选择需要操作的实例", style="Status.TLabel")
        self.status_label.pack(side=tk.RIGHT)
        
        # 功能按钮区
        btn_frame = ttk.Frame(self.root)
        btn_frame.pack(fill=tk.X, padx=20, pady=5)
        
        ttk.Button(btn_frame, text="新建实例", command=self.create_new_instance).pack(side=tk.LEFT, padx=5)
        ttk.Button(btn_frame, text="打开选中实例", command=self.open_selected_instance).pack(side=tk.LEFT, padx=5)
        ttk.Button(btn_frame, text="批量启动", command=self.batch_start).pack(side=tk.LEFT, padx=5)
        ttk.Button(btn_frame, text="批量停止", command=self.batch_stop).pack(side=tk.LEFT, padx=5)
        ttk.Button(btn_frame, text="批量删除", command=self.batch_delete).pack(side=tk.LEFT, padx=5)
        
        # 实例列表
        list_frame = ttk.Frame(self.root)
        list_frame.pack(fill=tk.BOTH, expand=True, padx=20, pady=10)
        
        # 创建Treeview
        columns = ("名称", "备注", "状态")
        self.instance_tree = ttk.Treeview(list_frame, columns=columns, show="headings", height=10)
        
        # 设置列标题
        for col in columns:
            self.instance_tree.heading(col, text=col)
            self.instance_tree.column(col, width=200)
        
        # 插入模拟数据
        for item in instance_data:
            self.instance_tree.insert("", tk.END, values=item)
        
        # 绑定选中事件和右键菜单
        self.instance_tree.bind("<<TreeviewSelect>>", self.on_tree_select)
        self.instance_tree.bind("<Button-3>", self.show_instance_context_menu)
        
        # 滚动条
        scrollbar = ttk.Scrollbar(list_frame, orient=tk.VERTICAL, command=self.instance_tree.yview)
        self.instance_tree.configure(yscrollcommand=scrollbar.set)
        
        self.instance_tree.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        scrollbar.pack(side=tk.RIGHT, fill=tk.Y)

    def show_instance_context_menu(self, event):
        """显示实例右键菜单"""
        # 获取选中的实例
        selected = self.instance_tree.identify_row(event.y)
        if not selected:
            return
        
        # 选中当前行
        self.instance_tree.selection_set(selected)
        item_values = self.instance_tree.item(selected, "values")
        instance_name = item_values[0]
        instance_status = item_values[2]
        
        # 创建右键菜单
        context_menu = tk.Menu(self.root, tearoff=0)
        
        # 根据状态显示不同选项
        if instance_status in ["已关闭", "后台运行"]:
            context_menu.add_command(label=f"打开「{instance_name}」", 
                                    command=lambda: self.open_instance(selected))
        if instance_status == "运行中":
            context_menu.add_command(label=f"最小化「{instance_name}」到托盘", 
                                    command=lambda: self.minimize_instance_to_tray(selected))
            context_menu.add_command(label=f"停止「{instance_name}」", 
                                    command=lambda: self.stop_instance(selected))
        
        # 显示菜单
        context_menu.post(event.x_root, event.y_root)

    def open_selected_instance(self):
        """打开选中的实例（按钮触发）"""
        selected = self.instance_tree.selection()
        if not selected:
            messagebox.showwarning("提示", "请先选择需要打开的实例")
            return
        # 只打开第一个选中的实例
        self.open_instance(selected[0])

    def open_instance(self, item_id):
        """核心：打开/唤醒实例"""
        # 获取实例信息
        item_values = self.instance_tree.item(item_id, "values")
        instance_name = item_values[0]
        instance_remark = item_values[1]
        current_status = item_values[2]
        
        # 1. 唤醒二次验证（如果开启）
        if wake_verify and current_status == "后台运行":
            if not self.verify_password(f"唤醒「{instance_name}」需要验证身份"):
                return
        
        # 2. 状态流转：更新为启动中
        self.instance_tree.item(item_id, values=(instance_name, instance_remark, "启动中"))
        self.status_label.config(text=f"正在唤醒窗口，请勿重复操作", style="Status.TLabel")
        self.root.update()  # 立即刷新界面
        
        # 3. 模拟启动过程（2秒）
        time.sleep(2)
        
        # 4. 状态流转：更新为运行中
        self.instance_tree.item(item_id, values=(instance_name, instance_remark, "运行中"))
        
        # 5. 交互提示
        if current_status == "已关闭":
            messagebox.showinfo("提示", f"实例「{instance_name}」已启动，可正常使用")
        else:  # 后台运行
            messagebox.showinfo("提示", f"已唤醒实例「{instance_name}」的浏览器窗口")
        self.status_label.config(text=f"实例「{instance_name}」状态：运行中", style="Success.TLabel")

    def minimize_instance_to_tray(self, item_id):
        """将实例最小化到托盘（后台运行）"""
        item_values = self.instance_tree.item(item_id, "values")
        instance_name = item_values[0]
        instance_remark = item_values[1]
        
        # 更新状态
        self.instance_tree.item(item_id, values=(instance_name, instance_remark, "后台运行"))
        messagebox.showinfo("提示", f"实例「{instance_name}」已转入后台运行，右键托盘唤醒")
        self.status_label.config(text=f"实例「{instance_name}」已后台运行", style="Status.TLabel")

    def stop_instance(self, item_id):
        """停止实例"""
        item_values = self.instance_tree.item(item_id, values)
        instance_name = item_values[0]
        instance_remark = item_values[1]
        
        # 更新状态
        self.instance_tree.item(item_id, values=(instance_name, instance_remark, "已关闭"))
        messagebox.showinfo("提示", f"已成功停止实例「{instance_name}」")
        self.status_label.config(text=f"实例「{instance_name}」已停止", style="Status.TLabel")

    def verify_password(self, title):
        """密码验证弹窗（支持自定义标题）"""
        password = simpledialog.askstring(title, "请输入解锁密码：", 
                                         parent=self.root, show="*")
        if password == "123456":  # 模拟正确密码
            self.reset_auto_lock_timer()  # 重置自动锁定计时器
            return True
        else:
            if password is not None:  # 不是取消操作
                messagebox.showerror("提示", "密码错误，请重试")
            return False

    # ========== 以下为原有函数（仅少量调整） ==========
    def on_tree_select(self, event):
        """选中实例后的状态更新"""
        selected_items = self.instance_tree.selection()
        if selected_items:
            count = len(selected_items)
            self.status_label.config(text=f"已选中 {count} 个实例")
        else:
            self.status_label.config(text="请选择需要操作的实例")

    def create_new_instance(self):
        """新建实例弹窗"""
        name = simpledialog.askstring("新建浏览器实例", "请输入实例名称（*）：", 
                                     parent=self.root, initialvalue="")
        if not name:
            messagebox.showwarning("提示", "实例名称不能为空，请填写")
            return
        
        remark = simpledialog.askstring("新建浏览器实例", "实例备注（选填）：", 
                                       parent=self.root, initialvalue="")
        
        # 添加到列表（默认状态：已关闭）
        self.instance_tree.insert("", tk.END, values=(name, remark or "", "已关闭"))
        messagebox.showinfo("提示", f"实例「{name}」创建成功")
        self.status_label.config(text="请选择需要操作的实例")

    def batch_start(self):
        """批量启动实例"""
        selected = self.instance_tree.selection()
        if not selected:
            messagebox.showwarning("提示", "请先选择至少1个实例")
            return
        
        count = len(selected)
        confirm = messagebox.askyesno("批量启动实例（共 {} 个）".format(count), 
                                     "以下 {} 个实例将被启动，是否继续？".format(count))
        if confirm:
            # 模拟批量启动过程
            for i, item in enumerate(selected, 1):
                values = self.instance_tree.item(item, "values")
                self.status_label.config(text=f"正在启动第{i}/{count}个实例：{values[0]}")
                self.root.update()
                time.sleep(0.5)
                # 更新状态
                self.instance_tree.item(item, values=(values[0], values[1], "运行中"))
            
            messagebox.showinfo("提示", f"已启动 {count} 个实例")
            self.status_label.config(text="请选择需要操作的实例")

    def batch_stop(self):
        """批量停止实例"""
        selected = self.instance_tree.selection()
        if not selected:
            messagebox.showwarning("提示", "请先选择至少1个实例")
            return
        
        count = len(selected)
        confirm = messagebox.askyesno("批量停止实例（共 {} 个）".format(count), 
                                     "停止后窗口关闭、进程终止，是否继续？")
        if confirm:
            # 模拟批量停止过程
            for i, item in enumerate(selected, 1):
                values = self.instance_tree.item(item, "values")
                self.status_label.config(text=f"正在停止第{i}/{count}个实例：{values[0]}")
                self.root.update()
                time.sleep(0.5)
                # 更新状态
                self.instance_tree.item(item, values=(values[0], values[1], "已关闭"))
            
            messagebox.showinfo("提示", f"已成功停止 {count} 个实例")
            self.status_label.config(text="请选择需要操作的实例")

    def batch_delete(self):
        """批量删除实例（高危操作）"""
        selected = self.instance_tree.selection()
        if not selected:
            messagebox.showwarning("提示", "请先选择至少1个实例")
            return
        
        # 安全验证（密码）
        if not self.verify_password("验证身份"):
            return
        
        count = len(selected)
        # 检查运行中的实例
        running_count = 0
        for item in selected:
            if self.instance_tree.item(item, "values")[2] in ["运行中", "后台运行"]:
                running_count += 1
        
        warn_msg = f"⚠️ 危险操作：数据将永久清除，无法恢复！\n\n{running_count}个实例正在运行，删除将强制终止进程"
        confirm = messagebox.askyesno("批量删除实例（共 {} 个）".format(count), warn_msg)
        
        if confirm:
            # 批量删除
            for item in selected:
                self.instance_tree.delete(item)
            
            messagebox.showinfo("提示", f"已删除 {count} 个实例，数据已清理")
            self.status_label.config(text="请选择需要操作的实例")

    def reset_auto_lock_timer(self):
        """重置自动锁定计时器"""
        global auto_lock_timer
        if auto_lock_timer:
            self.root.after_cancel(auto_lock_timer)
        
        # 设置新的计时器
        auto_lock_timer = self.root.after(lock_duration, self.auto_lock_menu)

    def auto_lock_menu(self):
        """自动锁定菜单"""
        global is_menu_unlocked
        is_menu_unlocked = False
        messagebox.showinfo("提示", "菜单已自动锁定，保护实例安全")

    def create_tray_icon(self):
        """创建系统托盘图标"""
        # 创建简单的图标（蓝色圆形）
        image = Image.new('RGB', (64, 64), color=(255, 255, 255))
        draw = ImageDraw.Draw(image)
        draw.ellipse((16, 16, 48, 48), fill="#0078D7")
        
        # 创建托盘菜单
        def create_tray_menu():
            menu_items = []
            if is_menu_unlocked:
                # 完整菜单：新增实例快捷打开
                menu_items = [
                    pystray.MenuItem("显示实例管理", self.show_main_window),
                    pystray.MenuItem("新建实例", self.create_new_instance_tray),
                    # 实例快捷打开子菜单
                    pystray.MenuItem("运行中实例", 
                                    pystray.Menu(
                                        pystray.MenuItem("实例1（工作账号）", lambda: self.open_instance_from_tray("实例1")),
                                        pystray.MenuItem("实例2（个人浏览）", lambda: self.open_instance_from_tray("实例2"))
                                    )),
                    pystray.MenuItem("锁定菜单", self.lock_menu),
                    pystray.MenuItem("退出程序", self.exit_app)
                ]
            else:
                # 极简菜单
                menu_items = [
                    pystray.MenuItem("关于", self.show_about),
                    pystray.MenuItem("退出程序", self.exit_app)
                ]
            return pystray.Menu(*menu_items)
        
        # 创建托盘图标
        self.tray = pystray.Icon("Customized Browser", image, "Customized Browser", create_tray_menu())
        self.tray.run()

    def open_instance_from_tray(self, instance_name):
        """从托盘打开指定实例"""
        # 查找实例ID
        for item_id in self.instance_tree.get_children():
            values = self.instance_tree.item(item_id, "values")
            if values[0] == instance_name:
                self.root.after(0, lambda: self.open_instance(item_id))
                break

    def show_main_window(self):
        """显示主窗口"""
        self.root.deiconify()
        self.root.lift()

    def create_new_instance_tray(self):
        """从托盘新建实例"""
        self.root.after(0, self.create_new_instance)

    def lock_menu(self):
        """手动锁定菜单"""
        global is_menu_unlocked
        is_menu_unlocked = False
        self.root.after(0, lambda: messagebox.showinfo("提示", "菜单已手动锁定"))
        # 更新托盘菜单
        self.tray.menu = self.tray.menu.create_menu(self.tray)

    def show_about(self):
        """显示关于信息"""
        self.root.after(0, lambda: messagebox.showinfo("关于", "Customized Browser v1.0\n专为多实例隔离设计"))

    def exit_app(self):
        """退出程序"""
        # 检查后台实例
        confirm = messagebox.askyesno("退出程序", "退出将终止所有后台实例进程，是否确认？\n当前有2个实例后台运行，退出后数据将保存")
        if confirm:
            self.tray.stop()
            self.root.after(0, self.root.quit)

    def on_close(self):
        """窗口关闭事件（最小化到托盘）"""
        self.root.withdraw()
        messagebox.showinfo("提示", "实例已转入后台运行，右键托盘唤醒")

if __name__ == "__main__":
    root = tk.Tk()
    app = CustomBrowserUI(root)
    root.mainloop()
