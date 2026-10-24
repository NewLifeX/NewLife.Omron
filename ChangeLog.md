# NewLife.Omron 版本更新记录

## v1.1.2026.1024 (2026-10-24)

### 依赖升级
- **NewLife.IoT 升级**：升级至 `3.2.2026.1024`
- **构建工具链升级**：Microsoft.SourceLink.GitHub 升级至 `10.0.402`
- **测试框架升级**：Microsoft.NET.Test.Sdk 升级至 `18.11.0`，xunit.runner.visualstudio 升级至 `4.0.1`，coverlet.collector 升级至 `10.1.0`

### 工程与 CI
- **发布工作流重构**：对齐 NewLife.Core，推送 `v` 前缀标签时同时发布 GitHub Packages 与 NuGet.org，并按 ChangeLog 对应段落创建同名 GitHub Release（资产含 nupkg/snupkg）；手工触发只发包
- **CI 迁移至 Ubuntu**：测试、正式版与测试版工作流统一运行于 `ubuntu-latest`，dotnet SDK 收敛为 8/9/10

### 文档
- **Readme 同步**：生态版图与团队介绍逐字对齐 NewLife.Core；单元测试统计更新为 555 个

---

## v1.1.2026.0902 (2026-09-02)

### 依赖升级
- **NewLife.IoT 升级**：升级至 `3.1.2026.801`，同步适配新 IoT 驱动接口，保障星尘/AntJob 等平台对接稳定性
- **构建工具链升级**：Microsoft.SourceLink.GitHub 升级至 `10.0.400`

### 测试与质量
- **测试框架升级**：Microsoft.NET.Test.Sdk 升级至 `18.9.0`，xunit.runner.visualstudio 升级至 `4.0.0`

---

## v1.1.2026.0609 (2026-06-09)

### 首个正式版本
- **三种协议**：完整实现 FINS/TCP、FINS/UDP、HostLink(C-mode)，支持以太网和串口(RS-232C/422/485)通信
- **27 种命令**：存储区读写、位操作、填充、传送、多区域读取、CPU 控制、设备信息、时钟读写、错误管理、参数/程序区操作、访问权控制
- **全部存储区**：DM/CIO/WR/HR/AR/EM(多 Bank)/TIM/CNT/IR/DR，支持 D100、CIO200.5、EM0:100 等地址格式
- **类型化 API**：Int16~Int64/UInt16~UInt64/Single/Double/Boolean/String/Byte[] 同步+异步读写
- **四种字节序**：ABCD/BADC/CDAB/DCBA 自由切换
- **批量读取**：自动合并同区域相邻/重叠地址，单次通信减少 50%+
- **自动重连**：网络断开自动重连，退避重试（1s/2s/3s）
- **模拟服务器**：FinsServer 模拟欧姆龙 PLC，支持全部存储区和 27 种命令
- **IoT 集成**：实现 NewLife.IoT 驱动接口，多通道共享连接，引用计数管理
- **线程安全**：同步方法 lock，异步方法 SemaphoreSlim(1,1)
- **完整测试**：52 个单元测试 + 25 个集成测试

---
