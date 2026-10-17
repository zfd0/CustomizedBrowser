<!-- 文件路径：README.md -->
# Customized Browser

一个基于 **.NET 8 + WPF + WebView2** 的 Windows 多实例隔离浏览器。
目标是让同一台电脑上可以同时运行多个**完全隔离**的浏览器实例，
从而支持多账号同时登录、数据互不干扰，并支持实例数据的导入导出与迁移。

> 本项目为 **v2** 版本（重写版）。早期 v1 原型（WinForms）已归档，不再维护。

---

## 核心特性

- **多实例隔离**：每个实例使用独立的用户数据目录，Cookie、缓存、Local Storage 等完全分离。
- **实例管理**：新建 / 打开 / 停止 / 删除实例；列表支持鼠标框选与 `Ctrl`/`Shift` 多选，选中多个后可批量执行操作；提供右键菜单。
- **实例导出 / 导入**：将实例（标签页会话 + 备注 + 登录状态）导出为实例包（`.cbx`），在另一台电脑导入后恢复登录状态。
- **提取网站到新实例**：把某实例中指定网站的登录状态（Cookie + 站点存储）独立成新实例，可选从源实例移除。
- **合并实例**：把多个实例的网站数据合并到目标实例，冲突网站由用户选择保留哪个来源。
- **会话保存 / 恢复**：保存并恢复标签页及滚动位置，或恢复上次访问页面。
- **系统托盘**：托盘图标支持锁定 / 解锁与气泡通知。
- **现代化界面**：WPF + MaterialDesignInXaml，设置窗口可配置主题、通知、同步、安全等。

---

## 技术栈

| 类别 | 内容 |
| --- | --- |
| 框架 | .NET 8.0 (LTS) |
| UI | WPF + MaterialDesignInXaml `4.9.0` |
| 浏览器引擎 | Microsoft.Web.WebView2 `1.0.2365.46` |
| MVVM | CommunityToolkit.Mvvm `8.2.2` |
| 依赖注入 | Microsoft.Extensions.DependencyInjection / Hosting `8.0.0` |
| 数据 | Microsoft.Data.Sqlite `8.0.0`、System.Text.Json `8.0.0` |
| 其他 | System.Management `8.0.0` |

---

## 环境要求

- Windows 10 及以上
- .NET 8.0 SDK（开发/构建）
- Microsoft Edge WebView2 Runtime（运行；Win10/11 随 Edge 通常已自带）
- Visual Studio 2022 或 VS Code（推荐）

---

## 构建与运行

```bash
# 构建
dotnet build

# 运行
dotnet run
```

也可直接双击 `scripts/编译.bat` 构建。

---

## 发布便携版

`scripts/发布便携版.bat` 会输出**自包含**（内置 .NET 8 运行时）、目标机免装 .NET 的便携版：

```bat
dotnet publish CustomizedBrowser.csproj -c Release -r win-x64 --self-contained true -p:PublishTrimmed=false -o "publish"
```

| 项目 | 说明 |
| --- | --- |
| 形式 | 整个文件夹（非单文件） |
| 运行时 | 自包含，目标机无需安装 .NET 8 |
| 裁剪 | 关闭（`PublishTrimmed=false`）；WPF 与 WebView2 依赖反射，裁剪会导致运行异常 |
| WebView2 | **未内置**，目标机仍需安装 WebView2 Runtime |
| 程序数据 | 写入 `%LocalAppData%\SimpleBrowser`，不在便携目录内；迁移数据请用「导出/导入实例」 |

---

## 项目结构

```
CustomizedBrowser/
├── CustomizedBrowser.csproj      # 项目文件（net8.0-windows / WPF）
├── App.xaml / App.xaml.cs        # 应用入口与依赖注入注册
├── Core/                         # 核心模块（常量、隐藏 Cookie 宿主窗口、进程清理等）
├── Models/                       # 数据模型（设置、实例、实例包、会话、网站迁移）
├── Services/                     # 服务层（设置/实例/会话/托盘/WebView/实例包/站点数据/网站迁移）
├── ViewModels/                   # 视图模型层（主窗口/实例管理/设置）
├── Views/                        # 视图层（浏览器主窗口、实例管理、提取网站、合并实例、设置）
├── Converters/                   # 值转换器
├── scripts/                      # 构建与发布脚本
└── docs/                         # 开发文档
    ├── 开发者文档.md
    └── 术语表.md
```

---

## 文档

- [开发者文档](docs/开发者文档.md)：架构设计、核心模块、实例导出/导入与网站迁移的实现原理
- [术语表](docs/术语表.md)：统一术语定义

---

## 实例数据与隐私

- 实例数据目录位于 `%LocalAppData%\SimpleBrowser\Instances\{InstanceId}`。
- 实例包（`.cbx`）本质是 zip，其中 `cookies.json` 为**明文**，等同于账号凭据，请妥善保管。
- 本仓库**不包含**任何实例数据、登录状态或编译产物（见 `.gitignore`）。

---

## 许可证

本项目采用 [MIT License](LICENSE)。
