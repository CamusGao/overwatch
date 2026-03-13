# Overwatch 使用方式

Overwatch 是一个服务管理工具，同一个可执行文件既作为守护进程（daemon）运行，也作为 CLI 向守护进程发送指令。

---

## 架构概述

```
┌─────────────────────────────────────────┐
│             overwatch daemon             │
│                                          │
│  ┌─────────┐  ┌─────────┐  ┌─────────┐ │
│  │  ns: A  │  │  ns: B  │  │  ns: C  │ │
│  │ serv0   │  │ serv0   │  │ serv0   │ │
│  │ serv1   │  │ serv1   │  │ ...     │ │
│  └─────────┘  └─────────┘  └─────────┘ │
└──────────────────┬──────────────────────┘
                   │ Unix Socket / Named Pipe
┌──────────────────┴──────────────────────┐
│             overwatch CLI               │
└─────────────────────────────────────────┘
```

- 每个 YAML 配置文件对应一个 **namespace（ns）**，文件名（不含扩展名）作为 ns 名称
- 守护进程启动时自动加载配置目录下所有 YAML 文件，并启动所有 `enabled: true` 的服务
- CLI 与守护进程通过 Unix Socket（Linux）/ Named Pipe（Windows）通信

---

## 技术栈

- **语言**：C# / .NET（AOT 编译模式）
- **发布形式**：单文件二进制，无需运行时依赖
- **平台**：Linux（systemd）、Windows（Windows Service）

---

## 安装为系统服务

```bash
# 安装为 systemd / Windows Service，随系统自动启动
overwatch install

# 卸载系统服务
overwatch uninstall
```

---

## 守护进程

```bash
# 手动启动守护进程（通常由系统服务自动启动）
overwatch daemon start

# 停止守护进程
overwatch daemon stop

# 查看守护进程状态
overwatch daemon status
```

### 命令行参数

| 参数 | 默认值 | 说明 |
|------|--------|------|
| `--config-dir <path>` | 平台默认目录 | 配置文件目录，守护进程扫描此目录下所有 `.yaml` 文件 |
| `--socket <path>` | 平台默认路径 | Unix Socket（Linux）或 Named Pipe（Windows）路径 |

**默认路径：**

| 平台 | 配置目录 | Socket / Pipe |
|------|----------|---------------|
| Linux | `/etc/overwatch/` | `/var/run/overwatch.sock` |
| Windows | `C:\ProgramData\overwatch\` | `\\.\pipe\overwatch` |

```bash
# 指定自定义配置目录启动
overwatch daemon start --config-dir /opt/myapp/config
```

---

## 服务控制

服务控制以 **namespace** 为单位，ns 名称即配置文件名（不含 `.yaml` 扩展名）。

```bash
# 启动指定 ns 下的所有服务（按依赖顺序）
overwatch start <ns>

# 停止指定 ns 下的所有服务
overwatch stop <ns>

# 重启指定 ns 下的所有服务
overwatch restart <ns>
```

---

## 状态查看

```bash
# 查看所有 ns 下所有服务的运行状态
overwatch ps
```

输出示例：

```
NAMESPACE   SERVICE   STATUS     PID     HEALTH     RESTARTS   UPTIME
app         serv0     running    12345   healthy    0          2h30m
app         serv1     running    12346   healthy    1          2h28m
infra       redis     running    9001    healthy    0          5d12h
infra       nginx     stopped    -       -          -          -
```

---

## 配置热重载

```bash
# 重新加载配置目录下的所有配置文件
overwatch reload
```

重载行为：
- 新增的 ns（新增配置文件）：自动启动其中所有 `enabled: true` 的服务
- 删除的 ns（配置文件被删除）：停止该 ns 下所有服务
- 已有 ns 的配置发生变更：列出配置有变化的服务，提示用户手动执行 `overwatch restart <ns>` 使新配置生效，**不自动重启正在运行的服务**

---

## 典型使用流程

```bash
# 1. 编写配置文件，放入配置目录
vim /etc/overwatch/myapp.yaml

# 2. 安装并启动守护进程
overwatch install
overwatch daemon start

# 3. 查看服务状态
overwatch ps

# 4. 修改配置后重载
overwatch reload
# 输出提示：myapp/serv0 配置已变更，执行 `overwatch restart myapp` 使其生效

# 5. 重启指定 ns 使新配置生效
overwatch restart myapp
```
