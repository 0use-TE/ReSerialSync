# Releases and runtime setup

[Preview v0.1.0-preview.1](https://github.com/0use-TE/ReSerialSync/releases/tag/v0.1.0-preview.1):

- [Windows x64 ZIP](https://github.com/0use-TE/ReSerialSync/releases/download/v0.1.0-preview.1/ReSerialSync-win-x64.zip)
- [Linux x64 tar.gz](https://github.com/0use-TE/ReSerialSync/releases/download/v0.1.0-preview.1/ReSerialSync-linux-x64.tar.gz)
- [SHA-256 checksums](https://github.com/0use-TE/ReSerialSync/releases/download/v0.1.0-preview.1/SHA256SUMS.txt)

Native AOT packages do not require a separate .NET runtime, but still require operating-system libraries.
Extract the entire archive and keep every file, including the Linux `.so` libraries.

## Windows x64

Extract the ZIP and run `ReSerialSync.Desktop.exe`. Install the USB serial manufacturer's driver
if required, such as CH340 / CP210x. Close any flasher, serial monitor or other program using the port.
Normal serial debugging does not require AI configuration.

## Linux x64

On Ubuntu / Debian:

```bash
sudo apt update
sudo apt install -y libx11-6 libice6 libsm6 libfontconfig1 fonts-noto-cjk
fc-cache -fv
tar -xzf ReSerialSync-linux-x64.tar.gz
cd ReSerialSync-linux-x64
chmod +x ReSerialSync.Desktop
./ReSerialSync.Desktop
```

A graphical desktop or WSLg is required. ESP32 serial communication has been tested on native Arch Linux,
and startup and device discovery on Ubuntu 22.04 under WSL 2 / WSLg. Other distributions need their own environment checks.

### Serial permissions

If the port is listed but opening it reports `Access to the port is denied`, inspect:

```bash
ls -l /dev/ttyUSB0
id
```

For a device owned by `root:dialout` with `crw-rw----` permissions:

```bash
sudo usermod -aG dialout "$USER"
newgrp dialout
```

Close the application and launch it from that terminal. Subsequent logins acquire the new group membership.
Use the actual group and permissions of your device node. Also check for another program holding the port.

### USB serial under WSL 2

Windows COM ports are not automatically mapped into WSL. Keep a WSL terminal open and use Windows PowerShell:

```powershell
winget install --interactive --exact dorssel.usbipd-win
usbipd list
# Run bind as administrator; replace the example 2-2 with the device's actual BUSID.
usbipd bind --busid 2-2
# attach does not require an elevated PowerShell.
usbipd attach --wsl --busid 2-2
```

In WSL, check `ls -l /dev/ttyUSB* /dev/ttyACM*`, then refresh the application's port list.
`bind` permits sharing; `attach` transfers the device to WSL. Windows cannot use it while attached.
Return it to Windows with `usbipd detach --busid 2-2` in PowerShell.

## Build from source

Install .NET 10 SDK and a native compiler/linker. Build Windows on Windows and Linux on Linux.
Windows requires Visual Studio Build Tools with Desktop development with C++ and a Windows SDK.
On Ubuntu install `sudo apt install clang zlib1g-dev`.

```powershell
pwsh ./scripts/publish.ps1 -Runtime win-x64
```

```bash
bash scripts/publish-linux.sh
```

Archives are written to ignored `artifacts/` with runtime instructions, project licence and dependency notices.
The scripts create local packages only; they do not upload or create a Release. Do not copy only the executable.

## Verify a download

On Windows, compare this hash with the ZIP's entry in the checksum file:

```powershell
Get-FileHash ./ReSerialSync-win-x64.zip -Algorithm SHA256
```

On Linux, check the downloaded files from the directory containing the archives and checksum file:

```bash
sha256sum --ignore-missing -c SHA256SUMS.txt
```

Matching hashes establish byte-for-byte consistency; a checksum list is not the author's digital signature.

## Verification status — October 9, 2026

| Target | Evidence | Still needed |
| --- | --- | --- |
| Windows x64 | AOT publishing; ESP32 photograph showing connection, timed sends and consecutive ACK replies | Fresh-machine archive acceptance |
| Linux x64 | AOT publishing; native Arch Linux ESP32 connection, timed sends and consecutive ACK replies; WSLg startup and port discovery | Other distribution and device combinations |
| macOS | Not tested: no macOS device available | Unfortunately, building, running and hardware communication could not be tested; no macOS release claimed |
| Browser / mobile | Project entry points | Platform serial backends and device validation |

The AI tool tests use a mock service; the migration article also shows real DeepSeek tool calls and a device reply. Windows and Linux AOT builds include explicit AI sending.
API keys are stored as plaintext locally. Release packages must not contain personal settings, logs or keys.
The migration article includes Windows startup measurements, published-file sizes and a memory snapshot. An equivalent old-version memory comparison remains pending.

## References

- [Avalonia Linux dependencies](https://docs.avaloniaui.net/docs/supported-platforms)
- [Microsoft: USB devices in WSL](https://learn.microsoft.com/en-us/windows/wsl/connect-usb)
- [Ubuntu: UART permissions](https://documentation.ubuntu.com/hardware-support/boards/how-to/special_hardware/uart-console/)
- [Tmds.DBus breaking change](https://github.com/tmds/Tmds.DBus/releases/tag/rel%2F0.93.0)
