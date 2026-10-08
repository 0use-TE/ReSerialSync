# 发布与运行 / Releases and runtime setup

Download the Windows x64 ZIP or Linux x64 tar.gz from this repository's GitHub Releases page once a release is published.
Native AOT packages do not require a separately installed .NET runtime. They still require operating-system libraries.
Keep every file from the archive together, including the Linux native `.so` libraries.

## Windows x64

解压完整 ZIP，运行 `ReSerialSync.Desktop.exe`。连接设备前安装设备厂商的 USB 串口驱动（如 CH340 / CP210x）。
无需配置 AI 即可进行串口调试。关闭占用该串口的烧录器、串口监视器或其他工具。

## Linux x64

For Ubuntu / Debian:

```bash
sudo apt update
sudo apt install -y libx11-6 libice6 libsm6 libfontconfig1 fonts-noto-cjk
fc-cache -fv
tar -xzf ReSerialSync-linux-x64.tar.gz
cd ReSerialSync-linux-x64
chmod +x ReSerialSync.Desktop
./ReSerialSync.Desktop
```

运行环境需要可用的图形桌面或 WSLg。当前 Linux 证据来自 Ubuntu 22.04、WSL 2 / WSLg；
不要把该验证范围等同于所有 Linux 发行版、原生桌面和 macOS 均已验证。

如果设备出现但连接提示 `Access to the port is denied`，先检查：

```bash
ls -l /dev/ttyUSB0
id
```

当设备属于 `root:dialout`、权限为 `crw-rw----` 时，将当前用户加入设备所属组：

```bash
sudo usermod -aG dialout "$USER"
newgrp dialout
```

关闭软件，从该终端重新启动；后续重新登录会获得新的组成员身份。
如果设备组或权限不同，应按实际设备节点配置，不要假定每个发行版都用 dialout。
也要确认串口没有被其他程序占用。

### USB serial under WSL 2

USB 设备不会自动从 Windows 的 COM 口映射到 WSL。
在 Windows 安装 usbipd-win，保持 WSL 终端运行，然后使用 PowerShell：

```powershell
winget install --interactive --exact dorssel.usbipd-win
usbipd list
# 管理员执行；2-2 仅为示例，换成列表中目标设备的 BUSID。
usbipd bind --busid 2-2
# attach 可以用普通 PowerShell 执行。
usbipd attach --wsl --busid 2-2
```

WSL 中检查 `ls -l /dev/ttyUSB* /dev/ttyACM*`，再刷新软件设备列表。
bind 允许共享，attach 才实际交给 WSL。接入期间 Windows 不能同时使用该设备。
用完在 PowerShell 执行 `usbipd detach --busid 2-2`。

## Build from source

Native AOT requires .NET 10 SDK and a native compiler/linker. Build Windows on Windows and Linux on Linux.
On Ubuntu install the build prerequisites with `sudo apt install clang zlib1g-dev`.
Windows requires Visual Studio Build Tools with Desktop development with C++ and a Windows SDK.

```powershell
pwsh ./scripts/publish.ps1 -Runtime win-x64
```

```bash
bash scripts/publish-linux.sh
```

产物写入忽略提交的 `artifacts/`，包含运行说明、项目许可证和第三方声明。
脚本不上传、不创建 Release。Native AOT 依赖发布目录中的全部原生文件，不能只复制主程序。

## Verification status — 2026-10-08

| Target | Evidence | Still needed |
| --- | --- | --- |
| Windows x64 | AOT publishing; development screenshot with device ACK replies | Release archive fresh-machine acceptance |
| Linux x64 | AOT publishing; WSLg startup and `/dev/ttyUSB0` discovery | Successful communication after permissions; native Linux acceptance |
| macOS | Shared desktop code uses System.IO.Ports | Native build, startup and physical-device test; no macOS release claimed |
| Browser / mobile | Project entry points | Platform serial implementations and device validation |

AI tool tests use a mock provider. Real AI requires the user's compatible service and credentials.
API keys are plaintext in local settings. Releases must not include `workspace.json`, personal logs or keys.
No startup-time, memory or binary-size superiority claim has been benchmarked.

## References

- [Avalonia Linux dependencies](https://docs.avaloniaui.net/docs/supported-platforms)
- [Microsoft: USB devices in WSL](https://learn.microsoft.com/en-us/windows/wsl/connect-usb)
- [Ubuntu: UART permissions](https://documentation.ubuntu.com/hardware-support/boards/how-to/special_hardware/uart-console/)
- [Tmds.DBus breaking change](https://github.com/tmds/Tmds.DBus/releases/tag/rel%2F0.93.0)
