# ReSerialSync

使用 Avalonia 12 和 .NET 10 重写的多串口调试工作台，是 [SerialSync](https://github.com/0use-TE/SerialSync) 的升级版本。
采用 Crystal.Avalonia、Semi.Avalonia、GOZA.Dock 和 DotKernel，提供可停靠布局、独立串口会话和 AI 助手。

[English](README.en.md) · [迁移记录](docs/migration.zh-CN.md) · [发布与运行](docs/releases.md) · [界面对比](docs/screenshots.md)

![ReSerialSync 浅色界面](docs/media/after-light.png)

上图来自 Avalonia Headless + Skia 自动化渲染，串口为测试替身；实机环境记录见[截图说明](docs/screenshots.md)。
目前已生成 Windows x64 与 Linux x64 Native AOT 版本。Linux 已在 WSL 2 / WSLg 启动；原生 Linux、macOS 尚未完成实机验证。

## 这次重写

旧版 SerialSync 的仓库创建于 2025 年 3 月，使用 .NET MAUI Blazor Hybrid、MudBlazor 和 NLog。
2026 年 10 月 1 日至 8 日，我把 Razor / CSS / WebView 界面重写为 Avalonia AXAML 和 MVVM，
重新设计多串口会话、Dock 工作区、自动保存及 AI 场景操作。
主动投入约 8 小时，是我的回顾估算，不包含既有库的开发时间；代码主要由 AI 编写，我负责需求、界面反馈和设备验证。

迁移中实际处理了 AOT 事件绑定、Linux 依赖版本与系统库、中文字体、串口权限，以及 UTF-8 / CRLF 分包。
2026 年 10 月 8 日的模拟串口、模拟 AI 与 Headless 验证完成 122 项检查。
这些检查不等于所有平台的硬件验证。完整过程、个人经历与限制见[中文迁移文章](docs/migration.zh-CN.md)和[英文版](docs/migration.en.md)。

## 使用

```powershell
dotnet run --project ReSerialSync/ReSerialSync.Desktop
```

左侧串口列表与当前串口配置各占一个独立 Dock 面板，列表支持滚动，可拖动分隔条分配空间，自动检测设备变化，也可以手动刷新。点击串口切换其配置和收发界面；每个串口拥有独立的连接、编解码器、发送内容、接收缓冲、计数和定时器。可同时连接多个串口，切换选择不会中断其他会话。拔出设备只停止该设备的连接和定时发送，配置继续保留。

串口配置支持波特率、数据位、校验、停止位、流控以及 DTR / RTS。支持文本和 HEX 收发、UTF-8 / ASCII / Latin-1、CR / LF / CRLF 行尾，以及 20 ms 到 1 小时的定时发送。HEX 使用两位字节，例如 `01 AF 0D 0A` 或 `01AF0D0A`。

接收区支持时间戳、自动滚动、暂停、清空和导出，保留最近 256K 字符。暂停仅影响显示，收包、统计和文件日志继续。数据块由串口驱动提供，不代表协议报文边界。

顶部使用 Material Icons，右上角图标可重置布局、切换明暗、中英文，以及打开设置。设置左侧切换样式与教程/作者介绍；标签头固定 40，可即时切换现代或经典，经典可选择保留面板间隙。悬停显示本地化说明。错误通过 Avalonia Notification 显示，并记录到 Serilog 日志。中英文切换会即时更新界面、停靠标签、配置选项、通知和打开的教程。

## 下位机示例

[SerialSyncLowerComputer](SerialSyncLowerComputer/README.md) 提供 ESP32 / Arduino 固件，
使用 115200、8N1 接收消息并自动回复。支持 `PING`、`INFO`、`STATUS`、`HELP`、`ECHO`，
普通消息回复 `ACK`，可用于验证串口收发、定时发送、日志和 AI 数据分析。
编译、刷写和协议说明见示例目录中的 README。

## 自动保存

布局和各串口配置通过 `System.Reactive` + 扩展方法监听属性与集合变化，500 ms 防抖后自动保存；退出时补一次保存。没有手动保存按钮，也不依赖 ReactiveUI。

- 每个串口分别保存参数、编码、发送内容、行尾、发送间隔和显示选项。
- 全局保存窗口尺寸、最大化状态、分隔条比例、标签顺序/区域/选择、主题、语言和当前串口。
- 启动后需要手动连接；连接状态和定时发送状态不会自动恢复执行。
- 旧版单串口设置会自动迁移到按串口名称保存的配置。

默认位置为用户本地应用数据目录下的 `ReSerialSync`，Windows 对应 `%LOCALAPPDATA%\ReSerialSync`：

- `workspace.json`：配置与布局，使用临时文件原子替换。损坏配置回退到默认值并通知用户。
- `logs/serial-*.log`：Serilog 按天和 10 MB 大小滚动，保留最近 30 个文件。每条数据记录包含串口名和完整 RX / TX 十六进制数据。

各串口日志分别显示和恢复，运行时显示最近 1000 行，启动时恢复最近 500 行。日志按时间、等级、内容分列，支持忽略大小写的关键词搜索和最低等级过滤。等级使用 Semi 自带的信息、警告、错误等主题颜色；底部显示匹配数/总数，导出使用当前过滤结果。样式和过滤条件也自动保存。界面截断过长记录，文件保留完整内容。清空显示不会删除文件；导出按钮导出当前显示内容。

## 项目组织

- `Views` / `ViewModels` 保留 Crystal 配对注册和自动视图定位，视图代码后置只包含初始化。
- `Services` 负责串口工厂、Serilog、配置持久化、通知和本地化。
- `Extensions` 提供属性/集合观察、UI 调度与防抖自动保存。
- `Models` 定义配置、选项和支持 AOT 的 JSON 源生成上下文。
- `Resources/Strings.resx` 与 `Strings.en.resx` 提供中文和英文。
- 依赖版本统一维护于 `Directory.Packages.props`。

Loaded、TextChanged、SelectionChanged 使用 Crystal EventToCommand；操作使用命令绑定。颜色直接使用 Semi 现有背景资源和 GOZA.Dock 内置主题。

## 验证

```powershell
dotnet run --project ReSerialSync/ReSerialSync.Tests
```

验证使用独立的模拟串口和 Avalonia Headless，检查多串口并发、切换后继续收发、拔插恢复、HEX 校验、分包 UTF-8、通知、语言切换、教程、自动保存和 Serilog 落盘，并生成明暗及中英文教程截图。数据写入 `.build/verification`，不修改用户配置。

实际设备通信需接上串口或回环设备验证。浏览器和移动端保留项目入口，尚未接入平台串口实现。

依赖通过中央包管理从 NuGet 还原，干净检出不需要作者本机的兄弟项目。若 Avalonia 构建遥测目录不可写，可仅在验证命令添加 `-p:UsedAvaloniaProducts=`。

## 作者

Ouse，热爱 .NET 的开发者，专注 C#、ASP.NET Core 和 Avalonia，Crystal.Avalonia、GOZA.Dock 等开源项目作者。介绍来自作者提供的[个人资料](https://0use.net/api/me.md)。

[个人主页](https://0use.net) · [GitHub](https://github.com/0use-TE)

本项目使用 [MIT 许可证](LICENSE)。第三方依赖保留各自许可，见[依赖声明](docs/third-party-notices.md)。

仓库地址：[https://github.com/0use-TE/ReSerialSync](https://github.com/0use-TE/ReSerialSync)。

