# 发布与运行

[预览版 v0.1.0-preview.1](https://github.com/0use-TE/ReSerialSync/releases/tag/v0.1.0-preview.1)：

- [Windows x64 ZIP](https://github.com/0use-TE/ReSerialSync/releases/download/v0.1.0-preview.1/ReSerialSync-win-x64.zip)
- [Linux x64 tar.gz](https://github.com/0use-TE/ReSerialSync/releases/download/v0.1.0-preview.1/ReSerialSync-linux-x64.tar.gz)
- [SHA-256 校验清单](https://github.com/0use-TE/ReSerialSync/releases/download/v0.1.0-preview.1/SHA256SUMS.txt)

Native AOT 发布包不需要另行安装 .NET 运行时，但仍依赖操作系统的原生库。
请完整解压，保留全部文件，包括 Linux 的 `.so` 库。

## Windows x64

解压 ZIP 后运行 `ReSerialSync.Desktop.exe`。连接设备前安装设备厂商的 USB 串口驱动，
例如 CH340 / CP210x。关闭占用该串口的烧录器、串口监视器或其他工具。
普通串口调试不需要配置 AI。

## Linux x64

Ubuntu / Debian 安装依赖后运行：

```bash
sudo apt update
sudo apt install -y libx11-6 libice6 libsm6 libfontconfig1 fonts-noto-cjk
fc-cache -fv
tar -xzf ReSerialSync-linux-x64.tar.gz
cd ReSerialSync-linux-x64
chmod +x ReSerialSync.Desktop
./ReSerialSync.Desktop
```

需要可用的图形桌面或 WSLg。已在原生 Arch Linux 上完成 ESP32 串口收发验证，
并在 Ubuntu 22.04、WSL 2 / WSLg 下验证启动与设备枚举。其他发行版仍需按实际环境检查。

### 串口权限

如果设备出现但连接提示 `Access to the port is denied`，先检查：

```bash
ls -l /dev/ttyUSB0
id
```

当设备属于 `root:dialout`、权限为 `crw-rw----` 时，加入设备所属组：

```bash
sudo usermod -aG dialout "$USER"
newgrp dialout
```

关闭软件，从该终端重新启动。后续重新登录会获得新的组成员身份。
设备组和权限以实际节点为准；如果权限正常，检查是否有其他程序占用串口。

### WSL 2 的 USB 串口

Windows 的 COM 口不会自动映射到 WSL。保持一个 WSL 终端运行，在 Windows PowerShell 执行：

```powershell
winget install --interactive --exact dorssel.usbipd-win
usbipd list
# 在管理员 PowerShell 执行；2-2 仅为示例，换成目标设备的 BUSID。
usbipd bind --busid 2-2
# attach 可以在普通 PowerShell 中执行。
usbipd attach --wsl --busid 2-2
```

在 WSL 检查 `ls -l /dev/ttyUSB* /dev/ttyACM*`，再刷新软件的串口列表。
`bind` 允许共享，`attach` 才实际接入。设备接入 WSL 期间，Windows 不能同时使用它。
用完在 PowerShell 执行 `usbipd detach --busid 2-2`。

## 从源码打包

需要 .NET 10 SDK 和原生编译工具链。Windows 版本在 Windows 构建，Linux 版本在 Linux 构建。
Windows 安装 Visual Studio Build Tools 的“使用 C++ 的桌面开发”及 Windows SDK。
Ubuntu 安装 `sudo apt install clang zlib1g-dev`。

```powershell
pwsh ./scripts/publish.ps1 -Runtime win-x64
```

```bash
bash scripts/publish-linux.sh
```

产物写入不会提交的 `artifacts/`，附运行说明、项目许可和第三方声明。
脚本只生成本地归档，不上传、不创建 Release。不要只复制主程序。

## 检查下载完整性

Windows 计算 ZIP 的 SHA-256，与清单中对应的值比较：

```powershell
Get-FileHash ./ReSerialSync-win-x64.zip -Algorithm SHA256
```

Linux 在归档和清单所在目录检查已下载的文件：

```bash
sha256sum --ignore-missing -c SHA256SUMS.txt
```

校验值相同表示文件字节一致；校验清单不等于作者的数字签名。

## 验证范围：2026 年 10 月 9 日

| 平台 | 已有证据 | 尚待验证 |
| --- | --- | --- |
| Windows x64 | AOT 发布；ESP32 实拍显示连接、定时发送及连续 ACK 回复 | 发布归档在干净机器上的验收 |
| Linux x64 | AOT 发布；原生 Arch Linux 连接 ESP32、定时发送与连续 ACK 回复；WSLg 启动与设备枚举 | 其他发行版与设备组合 |
| macOS | 暂未测试：没有 macOS 设备 | 很遗憾，暂时无法验证构建、运行及实机通信；未发布 macOS 版本 |
| 浏览器／移动端 | 保留项目入口 | 平台串口实现与设备验证 |

AI 自动化检查使用模拟服务，迁移文章另附 DeepSeek 实际调用与设备回复截图。Windows、Linux AOT 均已包含 AI 明确发送功能。
API key 按设计明文保存于本地配置。发布包不得包含个人配置、日志或 key。
迁移文章已附 Windows 启动测量、文件体积和内存截图；旧版相同状态的内存对比待补测。

## 参考

- [Avalonia 的 Linux 依赖](https://docs.avaloniaui.net/docs/supported-platforms)
- [微软：在 WSL 中连接 USB 设备](https://learn.microsoft.com/en-us/windows/wsl/connect-usb)
- [Ubuntu：UART 访问权限](https://documentation.ubuntu.com/hardware-support/boards/how-to/special_hardware/uart-console/)
- [Tmds.DBus 的不兼容变更](https://github.com/tmds/Tmds.DBus/releases/tag/rel%2F0.93.0)
