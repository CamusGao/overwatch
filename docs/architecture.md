# Overwatch 模块设计

## 整体架构

```
Overwatch.slnx
├── src/
│   ├── Overwatch.Cli          # 入口程序，CLI 解析与命令路由
│   ├── Overwatch.Daemon       # 守护进程核心
│   ├── Overwatch.Config       # 配置文件解析与校验
│   ├── Overwatch.Ipc          # CLI ↔ Daemon 通信协议与实现
│   ├── Overwatch.ServiceHost  # 单个服务的生命周期管理
│   └── Overwatch.Platform     # 平台抽象（Linux/Windows 差异）
└── tests/
    ├── Overwatch.Config.Tests        # 单元测试：配置解析与校验
    ├── Overwatch.ServiceHost.Tests   # 单元测试：服务状态机、重启策略、健康检查
    ├── Overwatch.Ipc.Tests           # 单元测试：IPC 协议序列化/反序列化
    ├── Overwatch.Platform.Tests      # 单元测试：平台命令、路径解析
    └── Overwatch.Integration.Tests   # 集成测试：完整 Daemon 启动/停止/reload 流程
```

> 解决方案文件使用 `.slnx` 格式（Visual Studio 新版 XML 格式），替代传统 `.sln`。

---

## 模块详述

---

### 1. `Overwatch.Cli` — 入口与命令路由

**职责**：程序唯一入口，解析命令行参数，判断以 daemon 模式还是 CLI 模式运行。

**子组件：**

| 组件 | 说明 |
|------|------|
| `EntryPoint` | 读取 argv[0] 及子命令，决定走 Daemon 还是 CLI 路径 |
| `DaemonCommand` | 处理 `daemon start/stop/status` |
| `InstallCommand` | 处理 `install / uninstall`，委托 Platform 模块写入 systemd unit 或 Windows Service |
| `StartCommand` | 处理 `start <ns>`，通过 IPC 发送启动指令 |
| `StopCommand` | 处理 `stop <ns>` |
| `RestartCommand` | 处理 `restart <ns>` |
| `PsCommand` | 处理 `ps`，从 Daemon 获取状态表格并格式化输出 |
| `ReloadCommand` | 处理 `reload`，触发 Daemon 重新加载配置，输出变更提示 |

**全局命令行参数：**

- `--config-dir <path>`：覆盖配置目录
- `--socket <path>`：覆盖 Socket / Pipe 路径

---

### 2. `Overwatch.Daemon` — 守护进程核心

**职责**：守护进程主循环，管理所有 namespace 的生命周期，响应 IPC 指令。

**子组件：**

| 组件 | 说明 |
|------|------|
| `DaemonHost` | 守护进程主入口，启动 IPC Server、加载所有 Namespace |
| `NamespaceManager` | 管理所有已加载的 Namespace，提供 start/stop/restart/reload 操作 |
| `Namespace` | 代表一个配置文件，持有该 ns 下所有 `ServiceHost` 实例 |
| `DependencyResolver` | 对 ns 内服务做拓扑排序，检测循环依赖（有环则拒绝加载整个 ns） |
| `IpcServer` | 监听 Socket/Pipe，接收 CLI 指令，分发给 NamespaceManager |
| `ReloadHandler` | 处理 reload 指令，对比新旧配置，输出 diff，按规则决定行为 |

**启动流程：**

```
DaemonHost.Start()
  ├── IpcServer.Start()               # 开始监听 CLI 连接
  ├── 扫描 config-dir/*.yaml
  └── 每个文件 → NamespaceManager.Load(file)
        ├── Config.Parse(file)
        ├── DependencyResolver.Validate()   # 循环依赖检测，失败则跳过此 ns
        └── 按拓扑顺序 → ServiceHost.Start() per service
```

**reload 流程：**

```
ReloadHandler.Handle()
  ├── 重新扫描 config-dir
  ├── 新增文件 → NamespaceManager.Load()  → 自动启动 enabled 服务
  ├── 删除文件 → NamespaceManager.Unload() → 停止该 ns 所有服务
  └── 已有文件变更 → 对比 diff → 输出变更服务列表，不自动重启
```

---

### 3. `Overwatch.Config` — 配置解析与校验

**职责**：将 YAML 文件解析为强类型模型，并做语义校验。AOT 兼容（使用 source generator）。

**模型：**

```csharp
NamespaceConfig
  └── Dictionary<string, ServiceConfig> Services

ServiceConfig
  ├── bool Enabled
  ├── string? User
  ├── List<string>? DependsOn
  ├── string? WorkingDir
  ├── RestartPolicy Restart          // 支持字符串或 on-exit/on-unhealthy 对象
  ├── Dictionary<string, string>? Environments
  ├── LogsConfig? Logs
  ├── HealthCheckConfig? HealthCheck
  ├── StartupConfig Startup
  └── StopConfig Stop

RestartPolicy
  ├── RestartRule OnExit
  └── RestartRule OnUnhealthy

RestartRule                          // no | always | on-failure[:N]
  ├── RestartMode Mode
  └── int? MaxRetries

PlatformCommand                      // string 或 linux/windows 分支
  ├── string? Linux
  └── string? Windows

StartupConfig
  ├── PlatformCommand Command
  ├── StartupType Type               // simple | forking
  └── TimeSpan? Timeout

StopConfig
  ├── PlatformCommand Command
  ├── TimeSpan? Timeout
  └── int? Retries

HealthCheckConfig
  ├── List<string> Test
  ├── TimeSpan Interval
  ├── TimeSpan Timeout
  ├── int Retries
  └── TimeSpan StartPeriod

LogsConfig
  ├── string? Path
  ├── string MaxSize                 // 默认 "100MB"
  └── string MaxAge                  // 默认 "7d"
```

**校验规则（加载时执行）：**

- `startup.command` 不能为空
- `startup.type = forking` 时，`stop.command` 不能为空
- `startup.type = simple` 时，`stop.command` 为空则使用默认 kill 行为
- `restart` 字段语法校验（枚举值 + N 的数字格式）
- `depends-on` 引用的服务必须存在于同一 ns

---

### 4. `Overwatch.Ipc` — 进程间通信

**职责**：定义 CLI 与 Daemon 之间的通信协议，提供 Server 端（Daemon 用）和 Client 端（CLI 用）实现。

**传输层：**

| 平台 | 实现 |
|------|------|
| Linux | Unix Domain Socket |
| Windows | Named Pipe |

具体路径/名称通过 `--socket` 参数或平台默认值确定。

**协议：** 基于换行分隔的 JSON（newline-delimited JSON），AOT 兼容，无需额外序列化框架。

**消息类型：**

```
// 请求 (CLI → Daemon)
{ "cmd": "start",   "ns": "myapp" }
{ "cmd": "stop",    "ns": "myapp" }
{ "cmd": "restart", "ns": "myapp" }
{ "cmd": "ps" }
{ "cmd": "reload" }

// 响应 (Daemon → CLI)
{ "ok": true,  "data": { ... } }
{ "ok": false, "error": "namespace 'myapp' not found" }
```

**组件：**

| 组件 | 说明 |
|------|------|
| `IpcServer` | Daemon 端，监听连接，反序列化请求，分发处理，序列化响应 |
| `IpcClient` | CLI 端，连接 Daemon，发送请求，等待响应 |
| `IpcMessage` | 请求/响应的强类型模型（AOT source-gen 序列化） |

---

### 5. `Overwatch.ServiceHost` — 单服务生命周期

**职责**：管理单个服务的完整生命周期：启动、停止、健康检查、重启策略、日志写入。

**状态机：**

```
             Start()
  [Stopped] ────────→ [Starting]
                           │ 成功
                           ↓
                       [Running] ←──────────────────┐
                           │                         │ 重启
                    健康检查失败 / 进程退出            │
                           ↓                         │
                      [Unhealthy]                    │
                           │                         │
                    RestartPolicy 判断 ──── 重启 ─────┘
                           │
                    放弃 / policy=no
                           ↓
                       [Failed]
```

**子组件：**

| 组件 | 说明 |
|------|------|
| `ProcessRunner` | 启动/停止进程，持有 PID（simple 模式），支持平台命令解析和环境变量注入 |
| `HealthChecker` | 按配置定期执行健康检查命令，维护连续失败计数，触发 `OnUnhealthy` 事件 |
| `RestartController` | 根据 `RestartPolicy` 决定是否重启，维护重启计数，实现退避逻辑 |
| `LogWriter` | 将进程 stdout/stderr 写入日志文件，实现按 max-size / max-age 的轮转 |

**PID 管理：**

- `simple` 模式：`ProcessRunner` 持有子进程引用，`${_PID}` 在 stop 命令中自动替换
- `forking` 模式：不持有 PID，stop 命令原样执行，不做变量替换

---

### 6. `Overwatch.Platform` — 平台抽象

**职责**：隔离所有平台差异，其他模块通过接口调用，不感知平台。

**接口：**

```csharp
interface IPlatformService
{
    // 系统服务安装/卸载
    void InstallService(string executablePath, string configDir, string socketPath);
    void UninstallService();

    // 默认路径
    string DefaultConfigDir { get; }
    string DefaultSocketPath { get; }

    // 进程用户切换
    ProcessStartInfo ApplyUser(ProcessStartInfo psi, string user);
}
```

**实现：**

| 类 | 平台 | 说明 |
|----|------|------|
| `LinuxPlatformService` | Linux | 写入 `/etc/systemd/system/overwatch.service`，执行 `systemctl enable/disable` |
| `WindowsPlatformService` | Windows | 调用 `sc.exe create/delete` 或 Windows Service API |

---

## 模块依赖关系

```
Overwatch.Cli
  ├── Overwatch.Ipc (Client)
  └── Overwatch.Platform (install/uninstall)

Overwatch.Daemon
  ├── Overwatch.Config
  ├── Overwatch.Ipc (Server)
  ├── Overwatch.ServiceHost
  └── Overwatch.Platform (默认路径)

Overwatch.ServiceHost
  └── Overwatch.Config (模型)

Overwatch.Ipc
  └── (无业务依赖，仅传输协议)

Overwatch.Platform
  └── (无业务依赖，仅平台 API)

Overwatch.Config
  └── (无依赖，纯解析)
```

---

## 测试项目

### 单元测试

每个核心模块对应一个独立的单元测试项目，使用 xUnit + NSubstitute（mock）。

| 项目 | 测试重点 |
|------|----------|
| `Overwatch.Config.Tests` | YAML 解析正确性、字段默认值、校验规则（循环依赖、缺失字段、非法枚举值）、平台命令字符串/对象两种写法、restart 策略两种写法 |
| `Overwatch.ServiceHost.Tests` | 状态机流转（Starting → Running → Unhealthy → Failed）、RestartController 计数与放弃逻辑、HealthChecker 连续失败触发、LogWriter 轮转逻辑、`${_PID}` 替换 |
| `Overwatch.Ipc.Tests` | 请求/响应 JSON 序列化与反序列化、AOT source-gen 路径覆盖 |
| `Overwatch.Platform.Tests` | 默认路径解析、`PlatformCommand` 按当前平台取值逻辑 |

### 集成测试

`Overwatch.Integration.Tests` 启动真实的 Daemon 进程，通过 IPC Client 发送指令，验证端到端行为。

**覆盖场景：**

- Daemon 启动后自动加载配置目录，服务按依赖顺序启动
- `start / stop / restart` 指令对真实进程的影响
- 健康检查失败触发重启，达到 max-retries 后进入 Failed 状态
- `reload` 后新增/删除/变更 ns 的行为
- 循环依赖配置被拒绝，同 ns 其他服务不受影响
- `forking` 模式下 stop 命令独立执行，`simple` 模式下 `${_PID}` 正确替换

**运行方式：** 集成测试依赖本地构建产物，CI 中在单元测试通过后运行。

---

## AOT 注意事项

- `Overwatch.Config`：YAML 解析使用 `YamlDotNet`，需配合 `[YamlStaticContext]` source generator 注册所有模型类型
- `Overwatch.Ipc`：JSON 序列化使用 `System.Text.Json` source generator（`[JsonSerializable]`），避免运行时反射
- 避免在任何路径上使用 `Type.GetType()`、`Activator.CreateInstance()` 等反射 API
