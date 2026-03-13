# Overwatch 配置文件参考

Overwatch 是一个轻量级服务管理工具，可将自身安装为 systemd 服务（Linux）或 Windows Service，并通过 YAML 配置文件统一管理机器上的各类 Web 服务。

---

## 多配置文件与 Namespace

每个配置文件对应一个 **namespace（ns）**，用于隔离一组相关服务。

- 一台机器上可以加载多个配置文件，每个文件独立管理自己的服务
- `depends-on` 只在同一 ns 内生效，**不支持跨 ns 的依赖**
- 加载配置时若检测到同一 ns 内存在**循环依赖**，将拒绝启动该 ns 的所有服务，其他 ns 不受影响

---

## 完整配置示例

```yaml
services:
  serv0:
    enabled: true
    user: www-data
    depends-on:
      - serv1

    working-dir: /app/serv0

    restart: on-failure:3

    environments:
      JAVA_HOME: /usr/lib/jvm/java-11-openjdk-amd64
      APP_ENV: production

    logs:
      path: /var/log/overwatch/serv0
      max-size: 100MB
      max-age: 7d

    health-check:
      test: ["curl", "-f", "http://localhost:8080/health"]
      interval: 20s
      timeout: 5s
      retries: 3
      start-period: 10s

    startup:
      command:
        linux: python app.py
        windows: python app.py
      type: simple
      timeout: 20s

    stop:
      command:
        linux: kill -9 ${_PID}
        windows: taskkill /F /PID ${_PID}
      timeout: 10s
      retries: 3
```

---

## 字段说明

### 顶层字段

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `enabled` | bool | `true` | 设为 `false` 时工具忽略此服务，不启动也不停止 |
| `user` | string | 继承工具运行用户 | 运行此服务的系统用户。工具自身需要足够权限（root / 管理员）才可切换用户 |
| `depends-on` | list\<string\> | — | 依赖的其他服务名，工具会等待依赖服务启动后再启动本服务 |
| `working-dir` | string | — | 服务的工作目录 |
| `restart` | string \| object | — | 重启策略，见下方说明 |
| `environments` | map\<string, string\> | — | 运行时环境变量 |

---

### `restart` — 重启策略

支持两种写法：

**写法 1：字符串**，同时作用于进程退出和健康检查失败两种场景。

```yaml
restart: on-failure:3
```

**写法 2：对象**，对两种场景分别配置。

```yaml
restart:
  on-exit: on-failure:3      # 进程意外退出时的策略
  on-unhealthy: always       # 健康检查持续失败时的策略
```

**策略值说明：**

| 值 | 说明 |
|----|------|
| `no` | 不重启 |
| `always` | 总是重启 |
| `on-failure` | 失败后重启，无限重试 |
| `on-failure:N` | 失败后重启，连续重启失败 N 次后放弃 |

---

### `environments` — 环境变量

```yaml
environments:
  JAVA_HOME: /usr/lib/jvm/java-11-openjdk-amd64
  APP_ENV: production
```

---

### `logs` — 日志配置

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `path` | string | 工具统一日志目录下按服务名分子目录 | 日志文件存放目录 |
| `max-size` | string | `100MB` | 单个日志文件的最大大小，超出后触发轮转 |
| `max-age` | string | `7d` | 日志保留天数，超出后删除旧文件 |

`max-size` 和 `max-age` 任一条件满足即触发轮转。

```yaml
logs:
  path: /var/log/overwatch/serv0
  max-size: 100MB
  max-age: 7d
```

---

### `health-check` — 健康检查

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `test` | list\<string\> | — | 健康检查命令，数组第一个元素为可执行文件 |
| `interval` | duration | — | 每次检查的间隔时间 |
| `timeout` | duration | — | 单次检查的超时时间 |
| `retries` | int | — | 连续失败多少次后判定为不健康 |
| `start-period` | duration | — | 服务启动后延迟多久才开始健康检查 |

```yaml
health-check:
  test: ["curl", "-f", "http://localhost:8080/health"]
  interval: 20s
  timeout: 5s
  retries: 3
  start-period: 10s
```

---

### `startup` — 启动配置

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `command` | string \| object | — | 启动命令，支持字符串或平台分支写法 |
| `type` | string | `simple` | 启动类型，见下方说明 |
| `timeout` | duration | — | `forking` 模式下等待启动完成的超时时间，`simple` 模式忽略此字段 |

**`type` 说明：**

| 值 | 说明 |
|----|------|
| `simple` | 工具持续持有进程，进程 PID 通过 `${_PID}` 暴露给 stop 命令 |
| `forking` | 进程启动后会自行 fork 到后台，工具不管理 PID，stop 命令需完全自定义 |

---

### `stop` — 停止配置

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `command` | string \| object | — | 停止命令，支持字符串或平台分支写法 |
| `timeout` | duration | — | 执行停止命令的超时时间 |
| `retries` | int | — | 停止失败后的重试次数 |

`simple` 模式下，stop 命令可以使用 `${_PID}` 引用服务进程的 PID。`forking` 模式下不提供 `${_PID}`，stop 命令需自行实现停止逻辑。

---

### `command` 的平台分支写法

`startup.command` 和 `stop.command` 均支持两种写法：

**写法 1：字符串**，在当前平台上直接执行。

```yaml
command: python app.py
```

**写法 2：平台分支对象**，按运行平台选择对应命令。

```yaml
command:
  linux: kill -9 ${_PID}
  windows: taskkill /F /PID ${_PID}
```

---

## 内置变量

| 变量 | 可用范围 | 说明 |
|------|----------|------|
| `${_PID}` | `stop.command`（仅 `simple` 模式） | 服务进程的 PID |
