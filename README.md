# Overwatch

A lightweight service manager for Linux and Windows — similar to systemd/supervisor, but cross-platform and self-contained. Ships as a single AOT-compiled binary with no runtime dependency.

## Features

- **Single binary** — the same executable runs as daemon and as CLI
- **Namespace isolation** — each YAML file is a namespace; services are grouped and managed independently
- **Dependency ordering** — `depends-on` enforces start/stop order within a namespace (cycle detection included)
- **Restart policies** — `no` / `always` / `on-failure[:N]`, configurable separately for process exit and health check failures
- **Health checks** — periodic command-based health checks with configurable interval, timeout, retries, and start-period
- **Log rotation** — stdout/stderr captured to file, rotated by max size and/or max age
- **Hot reload** — add/remove namespaces at runtime without restarting the daemon
- **Cross-platform** — Unix Domain Socket on Linux, Named Pipe on Windows; systemd/Windows Service integration for auto-start

## Installation

```bash
# Install as a system service (systemd on Linux, Windows Service on Windows)
overwatch install

# Uninstall
overwatch uninstall
```

## Quick Start

```bash
# 1. Write a config file
vim /etc/overwatch/myapp.yaml

# 2. Start the daemon (or let the system service do it)
overwatch daemon start

# 3. Check service status
overwatch ps

# 4. After editing config, reload
overwatch reload

# 5. Restart a namespace to apply changes
overwatch restart myapp
```

## CLI Reference

| Command | Description |
|---------|-------------|
| `overwatch daemon start [--config-dir <path>] [--socket <path>] [--log-dir <path>]` | Start the daemon |
| `overwatch daemon stop` | Stop the daemon |
| `overwatch daemon status` | Show daemon status |
| `overwatch start <ns>` | Start all services in a namespace |
| `overwatch stop <ns>` | Stop all services in a namespace |
| `overwatch restart <ns>` | Restart all services in a namespace |
| `overwatch ps` | List all services and their status |
| `overwatch reload` | Reload config directory (add/remove namespaces) |
| `overwatch install` | Install as system service |
| `overwatch uninstall` | Uninstall system service |

**Default paths:**

| Platform | Config dir | Socket / Pipe | Log dir |
|----------|------------|---------------|---------|
| Linux | `/etc/overwatch/` | `/var/run/overwatch.sock` | `/var/log/overwatch` |
| Windows | `C:\ProgramData\overwatch\` | `\\.\pipe\overwatch` | `C:\ProgramData\overwatch\logs` |

All paths can be overridden with `--config-dir`, `--socket`, and `--log-dir` respectively.

> **Logs**: If a service has no `logs:` section, logs are written to `<log-dir>/<namespace>/<service>/service.log`.
> Default retention: **100 MB** per log file, **7 days** history.

## Configuration

Each `.yaml` file in the config directory defines a **namespace**. The filename (without extension) is the namespace name.

### Full example

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

  serv1:
    enabled: true
    startup:
      command: python worker.py
    stop:
      command:
        linux: kill ${_PID}
        windows: taskkill /PID ${_PID}
```

### `restart` policy

```yaml
# Short form — applies to both process exit and health check failure
restart: on-failure:3

# Long form — configure each trigger independently
restart:
  on-exit: on-failure:3
  on-unhealthy: always
```

| Value | Behavior |
|-------|----------|
| `no` | Never restart |
| `always` | Always restart |
| `on-failure` | Restart on failure, unlimited retries |
| `on-failure:N` | Restart on failure, give up after N consecutive restarts |

### `startup.type`

| Value | Behavior |
|-------|----------|
| `simple` | Daemon holds the process; `${_PID}` is available in stop commands |
| `forking` | Process forks to background; daemon does not track PID; stop command must handle termination itself |

### `command` — platform variants

```yaml
# Single command (used on all platforms)
command: python app.py

# Platform-specific
command:
  linux: ./start.sh
  windows: start.bat
```

### Built-in variables

| Variable | Available in | Description |
|----------|-------------|-------------|
| `${_PID}` | `stop.command` (simple mode only) | PID of the managed process |

### `logs`

| Field | Default | Description |
|-------|---------|-------------|
| `path` | daemon log dir / service name | Directory for log files |
| `max-size` | `100MB` | Rotate when file exceeds this size |
| `max-age` | `7d` | Delete rotated files older than this |

### `health-check`

| Field | Description |
|-------|-------------|
| `test` | Command to run (array form: `["curl", "-f", "http://..."]`) |
| `interval` | Time between checks (e.g. `20s`, `1m`) |
| `timeout` | Per-check timeout |
| `retries` | Consecutive failures before marking unhealthy |
| `start-period` | Grace period after startup before checks begin |

## Reload behavior

```bash
overwatch reload
```

| Situation | Action |
|-----------|--------|
| New YAML file added | Load namespace and start enabled services |
| YAML file deleted | Stop all services and unload namespace |
| Existing YAML changed | Report changed namespace; **no automatic restart** |

To apply config changes to a running namespace:

```bash
overwatch restart <ns>
```

## Architecture

```
overwatch daemon
  ├── IpcServer (Unix Socket / Named Pipe)
  ├── NamespaceManager
  │     ├── Namespace "app"       ← myapp.yaml
  │     │     ├── ServiceHost: serv0
  │     │     └── ServiceHost: serv1
  │     └── Namespace "infra"     ← infra.yaml
  │           └── ServiceHost: redis
  └── ReloadHandler

overwatch CLI  ──[IPC]──▶  overwatch daemon
```

**Modules:**

| Module | Responsibility |
|--------|---------------|
| `Overwatch.Cli` | Arg parsing, command routing |
| `Overwatch.Daemon` | Daemon main loop, namespace lifecycle, IPC dispatch |
| `Overwatch.Config` | YAML parsing and validation |
| `Overwatch.ServiceHost` | Single service state machine, process, health check, restart, logging |
| `Overwatch.Ipc` | Client/server transport (newline-delimited JSON) |
| `Overwatch.Platform` | systemd / Windows Service integration, platform defaults |

## Building

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
# Build
dotnet build

# Run tests
dotnet test

# Publish as AOT single-file binary (Linux)
dotnet publish src/Overwatch.Cli -r linux-x64 -c Release

# Publish as AOT single-file binary (Windows)
dotnet publish src/Overwatch.Cli -r win-x64 -c Release
```

## License

MIT
