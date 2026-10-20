# NewLife.Omron - 欧姆龙 PLC 通信协议库

![GitHub top language](https://img.shields.io/github/languages/top/newlifex/NewLife.Omron?logo=github)
![GitHub License](https://img.shields.io/github/license/newlifex/NewLife.Omron?logo=github)
![Nuget Downloads](https://img.shields.io/nuget/dt/NewLife.Omron?logo=nuget)
![Nuget](https://img.shields.io/nuget/v/NewLife.Omron?logo=nuget)
![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/NewLife.Omron?label=dev%20nuget&logo=nuget)

NewLife.Omron 是一个纯 .NET 实现的欧姆龙 PLC 通信协议库，完整支持 **FINS/TCP**、**FINS/UDP**、**HostLink** 三种协议，提供同步+异步完整 API，零第三方依赖，适用于工业自动化、数据采集、设备监控等场景。

**源码**：https://github.com/NewLifeX/NewLife.Omron  
**NuGet**：NewLife.Omron  
**文档**：https://newlifex.com/iot  
**需求文档**：[Doc/需求文档.md](Doc/需求文档.md)  
**架构设计**：[Doc/架构设计.md](Doc/架构设计.md)

---

## 🌟 核心特性

### 协议支持
- ✅ **FINS/TCP**：标准以太网协议，自动握手获取节点地址
- ✅ **FINS/UDP**：无连接快速通信，适合高频轮询
- ✅ **HostLink**：C-mode 协议，支持串口（RS-232C/422/485）和 TCP（串口服务器）

### 功能完整
- ✅ **27 种 FINS 命令**：存储区读写、位操作、CPU 控制、设备信息、时钟、错误管理、参数/程序区操作、访问权控制
- ✅ **丰富存储区**：DM、CIO、WR、HR、AR、EM（多 Bank）、TIM、CNT、IR、DR 全覆盖
- ✅ **类型化 API**：Int16/32/64、UInt16/32/64、Single、Double、Boolean、String、Byte[] 一行代码读写
- ✅ **四种字节序**：ABCD（大端）、BADC（字内交换）、CDAB（欧姆龙默认）、DCBA（小端）自由切换

### 高级功能
- ✅ **批量读取优化**：自动合并相邻地址，单次通信减少 50%+
- ✅ **自动重连**：网络断开自动重连，退避重试（1s→2s→3s）
- ✅ **模拟服务器**：FinsServer 用于开发调试和单元测试，无需真实 PLC
- ✅ **异步编程**：全部 API 提供 Async 版本，ConfigureAwait(false) 避免死锁

### 工程品质
- ✅ **零依赖**：协议层仅依赖 .NET BCL，可独立使用
- ✅ **多框架**：支持 net45/net461/netstandard2.0/netstandard2.1，兼容旧项目
- ✅ **IoT 集成**：实现 NewLife.IoT 驱动接口，对接星尘、AntJob 等平台
- ✅ **完整测试**：387 个单元测试全部通过，覆盖全部核心功能

---

## 📦 安装

### NuGet 包管理器
```powershell
Install-Package NewLife.Omron
```

### .NET CLI
```bash
dotnet add package NewLife.Omron
```

### PackageReference
```xml
<PackageReference Include="NewLife.Omron" Version="1.1.*" />
```

---

## 快速开始

### 基础读写
```csharp
using NewLife.Omron.Protocols;

// 创建客户端并连接
var client = new FinsClient("192.168.1.10", 9600);
client.Connect();

// 读取DM区字数据
var data = client.Read("D100", 10); // 读取D100开始的10个字

// 写入数据
client.Write("D200", new byte[] { 0x00, 0x0A, 0x00, 0x14 });

// 关闭连接
client.Close();
```

### 类型化读写
```csharp
var client = new FinsClient("192.168.1.10");
client.Connect();

// 读取各种类型
short v16 = client.ReadInt16("D100");
int v32 = client.ReadInt32("D200");
float vf = client.ReadFloat("D300");
double vd = client.ReadDouble("D400");
string vs = client.ReadString("D500", 10);

// 写入各种类型
client.WriteInt16("D100", 1234);
client.WriteInt32("D200", 123456);
client.WriteFloat("D300", 3.14f);
client.WriteDouble("D400", 3.141592653589793);
client.WriteString("D500", "Hello");
```

### 位操作
```csharp
// 读取单个位
bool bitValue = client.ReadBool("CIO100.5");

// 写入单个位
client.WriteBool("CIO100.5", true);

// 批量读取位
byte[] bits = client.ReadBit("CIO100.0", 16); // 读取16个位
```

### 存储区操作
```csharp
// 存储区填充（将D100-D149全部填充为0）
client.Fill("D100", 50, 0x0000);

// 多区域读取（一次读取多个不连续地址）
byte[] multiData = client.MultipleRead(new[] { "D100", "D200", "CIO50" });

// 存储区传送（将D100-D109复制到D200-D209）
client.Transfer("D100", "D200", 10);
```

### CPU控制与状态
```csharp
// 读取CPU型号和版本
var cpuData = client.ReadCpuUnitData();
Console.WriteLine($"型号: {cpuData.Model}, 版本: {cpuData.Version}");

// 读取CPU运行状态
var status = client.ReadCpuUnitStatus();
Console.WriteLine($"模式: {status.Mode}, 致命错误: {status.FatalError}");

// CPU运行/停止
client.PlcRun();   // 启动PLC
client.PlcStop();  // 停止PLC

// 读取扫描周期
var (avg, max, min) = client.ReadCycleTime();
Console.WriteLine($"扫描周期 平均: {avg * 0.1}ms, 最大: {max * 0.1}ms, 最小: {min * 0.1}ms");
```

### 时钟操作
```csharp
// 读取PLC时钟
DateTime plcTime = client.ReadClock();
Console.WriteLine($"PLC时钟: {plcTime}");

// 设置PLC时钟
client.WriteClock(DateTime.Now);
```

### 错误操作
```csharp
// 清除错误
client.ClearError(0x0000);

// 读取错误日志
byte[] errorLog = client.ReadErrorLog(0, 10);

// 清除错误日志
client.ClearErrorLog();
```

### IoT驱动集成
```csharp
using NewLife.Omron.Drivers;

var driver = new OmronDriver();
var pm = new OmronParameter
{
    Address = "192.168.1.10:9600",
    DA2 = 0,
    DataFormat = "CDAB"
};
var node = driver.Open(null, pm);

// 通过IoT标准接口读写
var dic = driver.Read(node, points);
driver.Write(node, point, 12345);

driver.Close(node);
```

### FINS/UDP 通信
```csharp
using NewLife.Omron.Protocols;

// FINS/UDP 无需握手，直接通信
var udp = new FinsUdpClient("192.168.1.10", 9600);
udp.Open();

// 读写方式与 FinsClient 一致
short val = udp.ReadInt16("D100");
udp.WriteInt32("D200", 12345);

udp.Close();
```

### HostLink 串口/TCP 通信
```csharp
using NewLife.Omron.Protocols;

// 方式一：串口
var serial = new SerialPort("COM1", 9600, Parity.Even, 7, StopBits.Two);
serial.Open();
using var client1 = new HostLinkClient(serial.BaseStream);

// 方式二：TCP（串口服务器）
using var client2 = new HostLinkClient("192.168.1.100", 4001);

// 读写方式与 FinsClient 一致
var data = client2.Read("D100", 10);
client2.WriteInt32("D200", 12345);

// 通过 HostLink 发送 FINS 命令
var finsMsg = FinsMessage.BuildCommandRequest(FinsCommand.ControllerDataRead);
var resp = client2.SendFinsCommand(finsMsg);
```

### 异步 API
```csharp
using NewLife.Omron.Protocols;

var client = new FinsClient("192.168.1.10");
await client.ConnectAsync();

// 所有操作均有异步版本
var value = await client.ReadInt32Async("D100");
await client.WriteFloatAsync("D200", 3.14f);
await client.WriteBoolAsync("CIO100.5", true);

// CPU 操作异步
var cpuData = await client.ReadCpuUnitDataAsync();
var status = await client.ReadCpuUnitStatusAsync();
```

### 批量读取
```csharp
// 自动合并同区域相邻地址，减少通信次数
var results = client.BatchRead(
    new[] { "D100", "D101", "D102", "D200", "D201" },
    new ushort[] { 1, 1, 1, 1, 1 });

// results[0] ~ results[4] 为各地址的字节数组
```

### FinsServer 模拟测试
```csharp
using NewLife.Omron.Protocols;

// 创建模拟服务器（随机端口）
using var server = new FinsServer();
server.CpuModel = "CJ2M-CPU31";
server.Start();

// 服务端预写入数据
server.WriteMemory(0x82, 100, new byte[] { 0x00, 0x0A });

// 客户端连接测试
using var client = new FinsClient("127.0.0.1", server.Port);
client.Connect();

short val = client.ReadInt16("D100"); // 读取服务端预设值
client.WriteInt32("D200", 12345);     // 写入并在服务端验证

var cpu = client.ReadCpuUnitData();   // 返回 "CJ2M-CPU31"
```

## 支持的存储区

| 存储区 | 前缀 | 字代码 | 位代码 | 说明 |
|--------|------|--------|--------|------|
| CIO | CIO/C | 0xB0 | 0x30 | Core I/O 区 |
| WR | WR/W | 0xB1 | 0x31 | Work 工作区 |
| HR | HR/H | 0xB2 | 0x32 | Holding 保持区 |
| AR | AR/A | 0xB3 | 0x33 | Auxiliary 辅助区 |
| DM | DM/D | 0x82 | 0x02 | Data Memory 数据区 |
| EM | EM/EM0: | 0xA0+ | 0x20+ | Extended Memory 扩展数据区(多Bank) |
| TIM | TIM | 0x89 | 0x09 | 定时器当前值/完成标志 |
| CNT | CNT | 0x89 | 0x09 | 计数器当前值/完成标志 |
| IR | IR | 0xDC | - | 索引寄存器 |
| DR | DR | 0xBC | - | 数据寄存器 |

### 地址格式
```
{区域前缀}{地址}[.{位偏移}]

D100        - DM区第100字
CIO50.5     - CIO区第50字的第5位
EM0:200     - EM Bank0第200字
TIM5        - 定时器5的当前值
```

## 支持的FINS命令

| 命令 | MRC:SRC | 说明 |
|------|---------|------|
| Memory Area Read | 01:01 | 存储区读取（字/位） |
| Memory Area Write | 01:02 | 存储区写入（字/位） |
| Memory Area Fill | 01:03 | 存储区填充 |
| Multiple Memory Area Read | 01:04 | 多区域读取 |
| Memory Area Transfer | 01:05 | 存储区传送 |
| Parameter Area Read | 02:01 | 参数区读取 |
| Parameter Area Write | 02:02 | 参数区写入 |
| Parameter Area Clear | 02:03 | 参数区清除 |
| Program Area Read | 03:06 | 程序区读取 |
| Program Area Write | 03:07 | 程序区写入 |
| Program Area Clear | 03:08 | 程序区清除 |
| Run | 04:01 | CPU运行（RUN/MONITOR模式） |
| Stop | 04:02 | CPU停止 |
| Controller Data Read | 05:01 | CPU型号/版本读取 |
| Connection Data Read | 05:02 | 连接数据读取 |
| Controller Status Read | 06:01 | CPU运行状态读取 |
| Network Status Read | 06:06 | 网络状态读取 |
| Cycle Time Read | 06:20 | 扫描周期读取 |
| Clock Read | 07:01 | PLC时钟读取 |
| Clock Write | 07:02 | PLC时钟写入 |
| Message Read | 09:20 | 消息读取 |
| Access Right Acquire | 0C:01 | 获取访问权 |
| Access Right Forced Acquire | 0C:02 | 强制获取访问权 |
| Access Right Release | 0C:03 | 释放访问权 |
| Error Clear | 21:01 | 错误清除 |
| Error Log Read | 21:02 | 错误日志读取 |
| Error Log Clear | 21:03 | 错误日志清除 |

## 数据格式

欧姆龙PLC支持四种双字(32位)字节序：

| 格式 | 字节顺序 | 说明 |
|------|---------|------|
| ABCD | 3-2-1-0 | 大端序 |
| BADC | 2-3-0-1 | 字内交换 |
| CDAB | 1-0-3-2 | 双字交换（默认） |
| DCBA | 0-1-2-3 | 小端序 |


## 单元测试状态

| 测试类 | 测试数 | 状态 | 覆盖说明 |
|:------:|:------:|:----:|---------|
| ByteTransformTests | 79 | ✅ 通过 | TransInt16/UInt16/Int32/UInt32/Single/Double劄4种格式，TransByte全类型全格式，往返转换，边界/异常路径 |
| FinsAddressTests | 23 | ✅ 通过 | 全部存储区（DM/CIO/WR/HR/AR/EM/C）、位偏移、ToBytes、空/null/空白/不支持类型异常、地址边界 |
| FinsMessageTests | 58 | ✅ 通过 | BuildReadRequest/BuildWriteRequest、ToBytes、ParseResponse、IsSuccess、GetErrorMessage兣38个错误码 |
| FinsHeaderCommandTests | 16 | ✅ 通过 | FinsHeader默认值/ToBytes/Parse/往返；FinsCommand Read/Write/ToBytes/Parse/往返/异常 |
| FinsClientTests | 19 | ✅ 通过 | 默认属性值、属性设置、Close/Dispose生命周期、连接失败异常 |
| OmronDriverTests | 19 | ✅ 通过 | GetAddress、CreateParameter、Open（null/空地址/无分隔符）、Close、Read（null/空）、Write（不支持类型）、OmronParameter属性 |
| Protocols/ByteTransformTests | 23 | ✅ 通过 | 字节序转换深度参数化测试 |
| Protocols/CpuModelTests | 6 | ✅ 通过 | CPU 型号解析与识别 |
| Protocols/FinsAddressTests | 37 | ✅ 通过 | 地址解析全量参数化测试（所有前缀+EM Bank+位偏移） |
| Protocols/FinsCommandTests | 10 | ✅ 通过 | FINS 命令码正确性验证 |
| Protocols/FinsHeaderTests | 6 | ✅ 通过 | FINS 路由头序列化 |
| Protocols/FinsMessageTests | 22 | ✅ 通过 | FINS 消息构建/解析深度测试 |
| Protocols/FinsServerTests | 33 | ✅ 通过 | 端到端集成测试（FinsServer 内存回路，全命令验证） |
| Protocols/MemoryAreaTests | 35 | ✅ 通过 | 存储区枚举值，位区映射，IsBitArea 判断 |
| **合计** | **387** | **✅ 全部通过** | |

> 运行测试：`dotnet test XUnitTest/XUnitTest.csproj`


## 生态版图

NewLife 生态按依赖分层，各项目独立版本、独立发版。下面的版图在所有 NewLife 仓库中逐字一致；每个项目链接到代码仓库与主 NuGet 包。

| 项目 | 一句话定位 | 下载量 |
| --- | --- | --- |
| **基础底座** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | 日志、配置、缓存、网络、序列化、定时调度与 APM 追踪。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | 数据中间件：单表百亿级、自动分表、读写分离，支持 10 多种数据库。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | 高性能网络库：单机 177 万 pps、14.5 Gbps（TCP 1KB，2026-10 实测），400 万 Tcp 长连接。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | 协议通信与 RPC 框架，面向 CS 应用与物联网设备。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | 把 .NET 应用安装为操作系统守护进程：Windows 服务与 Linux systemd。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | `dotnet new` 项目模板：控制台、Web、WebApi、服务、数据与物联网。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **数据存储与消息** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | Redis 客户端：微秒级延迟、百万级吞吐，内置消息队列。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | 纯托管 RocketMQ 客户端，支持 Apache RocketMQ 与阿里云消息队列。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | MQTT 客户端与服务端，客户端支持阿里云物联网。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | C# 混合数据库：嵌入/网络双模，关系型、时序、消息队列与 KV 于一体。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | MySql 驱动与数据访问辅助层，基于 NewLife 数据栈实现。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web、AI 与分布式** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | 魔方快速开发平台：用户权限、SSO、OAuth 服务端与 CRUD 脚手架。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | 星尘分布式服务平台：节点管理、APM、配置中心、注册中心与发布中心。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | 蚂蚁调度：分布式任务与实时数据平台，支持分片、重试与回溯补算。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | 统一接入 46 家大模型服务商，附带开箱即用的 ChatAI Web 对话应用。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **工业与物联网** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | IoT 标准库，定义物联网通信协议契约与设备抽象。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | Modbus TCP/RTU/ASCII，基于 NewLife.IoT 实现。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | 西门子 PLC 协议，基于 NewLife.IoT 实现。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | 欧姆龙 PLC 协议，基于 NewLife.IoT 实现。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | 三菱 Melsec PLC 协议，基于 NewLife.IoT 实现。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | OPC 客户端，用于工业数据采集。 | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | BACnet 楼宇自控协议栈。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | 开源轻量级 IoT 平台，含网关与设备管理。 | — |
| **车联网** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | JT/T 808 车辆定位协议：70+ 消息体，覆盖 2011/2013/2019 三个版本。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | JT/T 809 部标平台互联协议，面向运输管理部门。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | JT/T 1078 车载视频：RTP 解析、FLV/fMP4 转封装与直播推送。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **领域组件** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | 一个包覆盖 Excel、Word、PPT、PDF 等 15+ 文档格式。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | 跨平台音频：编解码、设备 IO、DSP、容器与流式处理。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | 视频编解码、封装格式、帧处理与流媒体。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | 百度/高德/腾讯/天地图统一接口，支持地理编码与坐标转换。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | IPv4 归属地：离线内存映射库 + 在线 API 双源。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **工具与产品** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | 码神工具：网络、串口、加解密、正则、Modbus 与 MQTT。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | 自托管 Git 源代码管理系统。 | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | 从低版本宿主启动时，自动安装匹配的 .NET 运行时。 | — |

> 徽章取各项目主包；主题包、扩展包与卫星包见对应 NuGet 页面。未在表中列出的 30 多个包（FTP、LoRa、Office 扩展、各类协议驱动等）见 [NewLifeX 组织](https://github.com/NewLifeX) 与 [newlifex.com](https://newlifex.com)。



## 新生命开发团队

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

新生命团队成立于 2002 年，二十余年专注 .NET 与物联网技术生态，主导开源项目近百个（生态版图收录 34 个主干项目），为电力、物流、医疗、工控、互联网等行业提供软硬件一体化解决方案；以“学无先后达者为师”为信条。

开源组件在 NuGet 累计下载 <!--nuget-total:start-->6.48M<!--nuget-total:end--> 次（全生态口径，截至 2026-10）。

网站：https://newlifex.com · 开源组织：https://github.com/NewLifeX

<details>
<summary>微信公众号：智能大石头（扫码关注）</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>
