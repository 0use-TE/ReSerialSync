# ESP32 下位机示例

用于配合 ReSerialSync 测试串口收发。ESP32 通过 USB 串口接收消息并自动回复，
无需 Wi-Fi、传感器或额外的第三方库。项目使用 PlatformIO、Arduino 和 `esp32dev` 板型。
如果实际开发板不是经典 ESP32 DevKit，请先修改 `platformio.ini` 中的板型和相应平台配置。

## 编译与刷写

在本目录执行，或使用 VS Code 的 PlatformIO 扩展：

```powershell
pio run
pio run --target upload --upload-port COM5
```

将 `COM5` 替换为实际设备端口。Linux/macOS 使用相应的 `/dev/ttyUSB*`、
`/dev/ttyACM*` 或 `/dev/cu.*` 设备名。
刷写和 PlatformIO 串口监视器会占用串口，使用 ReSerialSync 连接前先关闭它们。

## 在 ReSerialSync 中测试

1. 用数据线连接 ESP32，在左侧选择其串口。
2. 设置 **115200 波特率、8 数据位、无校验、1 停止位、无流控**，DTR/RTS 默认不勾选。
3. 连接后选择 UTF-8 文本发送，行尾推荐 LF 或 CRLF。
4. 发送下面的消息，观察接收区和日志；也可开启定时发送测试 `PING` 或 `STATUS`。

启动或复位时会输出 `READY SerialSyncLowerComputer...`。部分开发板在连接时会复位，
可等启动完成后再发送消息；如果错过 READY，发送 PING 也能确认通信正常。

| 发送 | 回复 |
| --- | --- |
| `PING` | `PONG` |
| `INFO` | 板型、波特率和协议版本的 JSON |
| `STATUS` | 运行毫秒数、接收消息数和发送消息数的 JSON |
| `HELP` | 命令列表 |
| `ECHO Hello` | `Hello` |
| `Hello` | `ACK Hello` |
| `你好` | `ACK 你好` |

命令区分大小写，普通消息按原字节回复，并附加 CRLF。
回复计数包括启动 READY；空行不计数、不回复。计数和运行时间在复位后重新开始。

## 消息边界

- CR、LF、CRLF 都可以结束一条消息；CRLF 只触发一次回复。
- 未选择行尾时，连续 **50 ms** 没有收到新字节也会结束消息并回复。
- 每条消息最多 **256 字节**，不含行尾；超出后丢弃整条消息，并在消息结束时回复错误。
- 接收不会使用阻塞的 `readString()` 或动态字符串，支持消息分批到达以及一次收到多条消息。

这是文本协议示例，不是透明二进制回显协议：`0D`、`0A` 始终表示消息边界。
可在 ReSerialSync 中以 HEX 发送 `50 49 4E 47 0A`（即 `PING` + LF），收到 `PONG`。
若发送内容较长、分批间隔超过 50 ms 或持续定时发送，请追加 LF/CRLF，避免把消息错误拆分或合并。
正式设备协议应采用明确的分隔符、固定长度或长度字段。

也可让 AI 助手根据这些回复分析通信情况，例如“解释 STATUS 返回的字段”。
