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
- ✅ **完整测试**：214 个单元测试全部通过，覆盖全部核心功能

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
| ByteTransformTests | 78 | ✅ 通过 | TransInt16/UInt16/Int32/UInt32/Single/Double全4种格式，TransByte全类型全格式，往返转换，边界/异常路径 |
| FinsAddressTests | 23 | ✅ 通过 | 全部存储区（DM/CIO/WR/HR/AR/EM/C）、位偏移、ToBytes、空/null/空白/不支持类型异常、地址边界 |
| FinsMessageTests | 55 | ✅ 通过 | BuildReadRequest/BuildWriteRequest、ToBytes、ParseResponse、IsSuccess、GetErrorMessage全38个错误码 |
| FinsHeaderCommandTests | 19 | ✅ 通过 | FinsHeader默认值/ToBytes/Parse/往返/异常；FinsCommand Read/Write/ToBytes/Parse/往返/异常 |
| FinsClientTests | 19 | ✅ 通过 | 默认属性值、属性设置、Close/Dispose生命周期、连接失败异常 |
| OmronDriverTests | 20 | ✅ 通过 | GetAddress、CreateParameter、Open（null/空地址/无分隔符）、Close、Read（null/空）、Write（不支持类型）、OmronParameter属性 |
| **合计** | **214** | **✅ 全部通过** | |

> 运行测试：`dotnet test XUnitTest/XUnitTest.csproj`


## 新生命项目矩阵
各项目默认支持net9.0/netstandard2.1/netstandard2.0/net4.62/net4.5，旧版（2024.0801）支持net4.0/net2.0  

|                               项目                               | 年份  | 说明                                                                                        |
| :--------------------------------------------------------------: | :---: | ------------------------------------------------------------------------------------------- |
|                             基础组件                             |       | 支撑其它中间件以及产品项目                                                                  |
|          [NewLife.Core](https://github.com/NewLifeX/X)           | 2002  | 核心库，日志、配置、缓存、网络、序列化、APM性能追踪                                         |
|    [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode)    | 2005  | 大数据中间件，单表百亿级，MySql/SQLite/SqlServer/Oracle/PostgreSql/达梦，自动分表，读写分离 |
|      [NewLife.Net](https://github.com/NewLifeX/NewLife.Net)      | 2005  | 网络库，单机千万级吞吐率（2266万tps），单机百万级连接（400万Tcp长连接）                     |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | 2011  | 协议通信库，提供CS应用通信框架，支持Http/RPC通信框架，高吞吐，物联网设备低开销易接入        |
|     [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube)     | 2010  | 魔方快速开发平台，集成了用户权限、SSO登录、OAuth服务端等，单表100亿级项目验证               |
|    [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent)    | 2008  | 服务管理组件，把应用安装成为操作系统守护进程，Windows服务、Linux的Systemd                   |
|     [NewLife.Zero](https://github.com/NewLifeX/NewLife.Zero)     | 2020  | Zero零代脚手架，基于NewLife组件生态的项目模板NewLife.Templates，Web、WebApi、Service        |
|                              中间件                              |       | 对接知名中间件平台                                                                          |
|    [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis)    | 2017  | Redis客户端，微秒级延迟，百万级吞吐，丰富的消息队列，百亿级数据量项目验证                   |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | 2018  | RocketMQ纯托管客户端，支持Apache RocketMQ和阿里云消息队列，十亿级项目验                     |
|     [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT)     | 2019  | 物联网消息协议，MqttClient/MqttServer，客户端支持阿里云物联网                               |
|      [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT)      | 2022  | IoT标准库，定义物联网领域的各种通信协议标准规范                                             |
|   [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus)   | 2022  | ModbusTcp/ModbusRTU/ModbusASCII，基于IoT标准库实现，支持ZeroIoT平台和IoTEdge网关            |
|  [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens)  | 2022  | 西门子PLC协议，基于IoT标准库实现，支持IoT平台和IoTEdge                                      |
|      [NewLife.Map](https://github.com/NewLifeX/NewLife.Map)      | 2022  | 地图组件库，封装百度地图、高德地图、腾讯地图、天地图                                        |
|    [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio)    | 2023  | 音频编解码库，PCM/ADPCMA/G711A/G722U/WAV/AAC                                                |
|                             产品平台                             |       | 产品平台级，编译部署即用，个性化自定义                                                      |
|         [Stardust](https://github.com/NewLifeX/Stardust)         | 2018  | 星尘，分布式服务平台，节点管理、APM监控中心、配置中心、注册中心、发布中心                   |
|           [AntJob](https://github.com/NewLifeX/AntJob)           | 2019  | 蚂蚁调度，分布式大数据计算平台（实时/离线），蚂蚁搬家分片思想，万亿级数据量项目验证         |
|      [NewLife.ERP](https://github.com/NewLifeX/NewLife.ERP)      | 2021  | 企业ERP，产品管理、客户管理、销售管理、供应商管理                                           |
|         [CrazyCoder](https://github.com/NewLifeX/XCoder)         | 2006  | 码神工具，众多开发者工具，网络、串口、加解密、正则表达式、Modbus、MQTT                      |
|           [EasyIO](https://github.com/NewLifeX/EasyIO)           | 2023  | 简易文件存储，支持分布式系统中文件集中存储。                                                |
|           [XProxy](https://github.com/NewLifeX/XProxy)           | 2005  | 产品级反向代理，NAT代理、Http代理                                                           |
|        [HttpMeter](https://github.com/NewLifeX/HttpMeter)        | 2022  | Http压力测试工具                                                                            |
|         [GitCandy](https://github.com/NewLifeX/GitCandy)         | 2015  | Git源代码管理系统                                                                           |
|          [SmartOS](https://github.com/NewLifeX/SmartOS)          | 2014  | 嵌入式操作系统，完全独立自主，支持ARM Cortex-M芯片架构                                      |
|          [SmartA2](https://github.com/NewLifeX/SmartA2)          | 2019  | 嵌入式工业计算机，物联网边缘网关，高性能.NET8主机，应用于工业、农业、交通、医疗             |
|                          FIoT物联网平台                          | 2020  | 物联网整体解决方案，建筑、环保、农业，软硬件及大数据分析一体化，单机十万级点位项目验证      |
|                        UWB高精度室内定位                         | 2020  | 厘米级（10~20cm）高精度室内定位，软硬件一体化，与其它系统联动，大型展厅项目验证             |



## 新生命开发团队
![XCode](https://newlifex.com/logo.png)  

新生命团队（NewLife）成立于2002年，是新时代物联网行业解决方案提供者，致力于提供软硬件应用方案咨询、系统架构规划与开发服务。  
团队主导的80多个开源项目已被广泛应用于各行业，Nuget累计下载量高达400余万次。  
团队开发的大数据中间件NewLife.XCode、蚂蚁调度计算平台AntJob、星尘分布式平台Stardust、缓存队列组件NewLife.Redis以及物联网平台FIoT，均成功应用于电力、高校、互联网、电信、交通、物流、工控、医疗、文博等行业，为客户提供了大量先进、可靠、安全、高质量、易扩展的产品和系统集成服务。  

我们将不断通过服务的持续改进，成为客户长期信赖的合作伙伴，通过不断的创新和发展，成为国内优秀的IoT服务供应商。  

`新生命团队始于2002年，部分开源项目具有20年以上漫长历史，源码库保留有2010年以来所有修改记录`  
网站：https://newlifex.com  
开源：https://github.com/newlifex  
QQ群：1600800/1600838  
微信公众号：  
![智能大石头](https://newlifex.com/stone.jpg)  
