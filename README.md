# Customized Browser（v1 · 初版原型）

> ⚠️ **本仓库为项目的 v1 初版（半成品/原型），仅作存档备份。**
> 功能已在后续版本（v2）中重构与完善，本项目不再继续维护，请勿用于生产环境。

一个基于 **WebView2** 的 Windows 多实例浏览器。核心目标是让同一台电脑上可以同时运行
多个**完全隔离**的浏览器实例，从而支持多账号同时登录、数据互不干扰。

---

## 项目状态

| 项目 | 说明 |
| --- | --- |
| 版本 | v1（初版原型） |
| 状态 | 半成品，已停止维护 |
| 后续 | 由 v2 版本取代并重构 |

初版主要完成了**界面框架与核心交互流程的验证**，部分功能仅有 UI 与占位逻辑，
尚未完成真正的业务实现（详见「已知限制」章节）。

---

## 功能特性

### 已实现
- **多实例隔离**：每个实例使用独立的 `UserDataFolder`，Cookie、缓存、Local Storage 等按实例分离。
- **实例管理**：新建 / 打开 / 删除实例，列表展示实例名称、状态、备注、上次使用时间；支持批量操作。
- **浏览器窗口**：多标签页、地址栏（支持直接输入网址或关键词）、前进 / 后退 / 刷新 / 返回首页。
- **系统托盘**：最小化到托盘后保持后台运行，托盘右键菜单可管理实例、锁定、退出。
- **窗口置顶**：一键将浏览器窗口置于最前。
- **会话保存 / 恢复**：可记录并恢复标签页及其滚动位置，或恢复上次访问页面。
- **设置中心**：启动设置、隐藏窗口行为、托盘文本、主题（浅色 / 深色 / 跟随系统）、
  通知偏好、数据同步、账户安全、数据管理（清除缓存 / 历史 / Cookie）。
- **首次启动引导**：欢迎弹窗与新建实例引导。
- **快捷键**：`Ctrl+R` 刷新、`Ctrl+T` 新建标签、`Ctrl+W` 关闭标签、
  `Ctrl+Shift+T` 置顶窗口、`F12` 开发者工具、`Alt+←/→` 前进 / 后退。

### 部分实现 / 占位
- **密码与自动锁定**：界面与定时器已就绪（默认 1 分钟无操作自动锁定），
  但**密码校验逻辑尚未实现**（见 `Program.cs` 中的 `Unlock()`，当前直接放行）。
- **数据同步**：仅存在设置项与界面，无实际同步逻辑。
- **隐私模式 / 阻止追踪 Cookie**：设置项已存在，实际行为未完全接入。

### 未实现
- 账号体系、云同步、书签管理、下载管理、扩展等。

---

## 技术栈

| 类别 | 内容 |
| --- | --- |
| 语言 | C# |
| 框架 | .NET Framework 4.8（WinForms） |
| 浏览器内核 | Microsoft.Web.WebView2 `1.0.2365.46` |
| 序列化 | System.Text.Json `10.0.1` |
| 其他 | System.Management |
| 原型脚本 | Python 3 + tkinter + pystray + Pillow（`preview.py`） |

---

## 环境要求

- Windows 10 及以上
- 开发：Visual Studio 2022（含 .NET 桌面开发工作负载）或 .NET SDK
- 运行：需已安装 **Microsoft Edge WebView2 Runtime**（Win10/11 通常系统自带）

---

## 构建与运行

### 1. 命令行构建
```powershell
dotnet build "Customized Browser.sln" -c Release
```

### 2. Visual Studio
用 VS2022 打开 `Customized Browser.sln`，直接生成并启动 `CustomizedBrowser` 项目。

运行后程序会常驻系统托盘，浏览器窗口关闭时可按设置选择退出或后台运行。

> 注意：首次运行会在
> `%LocalAppData%\SimpleBrowser\Instances\<实例ID>` 下创建实例数据目录。

### 3. 运行 UI 原型脚本（可选）
```powershell
pip install pystray pillow
python "Customized Browser\preview.py"
```
`preview.py` 是一个独立的 Python 原型，用模拟数据还原实例管理界面，仅用于界面演示。

---

## 项目结构

```
原文件/
├── Customized Browser.sln          # 解决方案
├── README.md                       # 本文件
├── LICENSE                         # MIT 许可证
├── .gitignore                      # Git 忽略规则
└── Customized Browser/             # 主项目
    ├── CustomizedBrowser.csproj    # 项目文件（net48 / WinForms）
    ├── App.config                  # 运行时配置
    ├── Program.cs                  # 程序入口、系统托盘、锁定/解锁
    ├── MainForm.cs                 # 浏览器主窗口（标签页、地址栏、快捷键）
    ├── IntegratedMainForm.cs       # 集成主窗口（欢迎页、新建实例）
    ├── InstanceManagerForm.cs      # 实例管理窗口
    ├── SettingsForm.cs             # 设置窗口
    ├── SettingsManager.cs          # 设置与会话状态数据模型
    ├── preview.py                  # Python 原型界面
    └── Properties/                 # 程序集信息与资源
```

---

## 已知限制

1. 密码验证、数据同步等为占位实现，不具备真实安全/同步能力。
2. 无明显异常处理与日志体系，稳定性有限。
3. 界面与逻辑耦合较紧，缺乏分层设计，因此被 v2 重构取代。

---

## 许可证

本项目采用 [MIT License](LICENSE)。