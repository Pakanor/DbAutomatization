# DbAutomatization

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com)
[![C#](https://img.shields.io/badge/C%23-12.0-239120?logo=csharp)](https://learn.microsoft.com/en-us/dotnet/csharp/)
[![Linux](https://img.shields.io/badge/Linux-%20-FCC624?logo=linux)](https://kernel.org)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)
[![Status](https://img.shields.io/badge/status-active-brightgreen)]()

> **SysOps CLI tool** — Automate database backup, restore, and service management on Linux via interactive terminal menus, for both **systemd**-managed and **Docker**-containerized databases.

---

## Overview

DbAutomatization is a C# console application that discovers installed database engines via systemd (and optional Docker containers), registers them with pre-configured templates, and provides an intuitive menu-driven interface for backup, restore, service lifecycle (`start`/`stop`/`restart`), and interactive CLI entry — all under `sudo` (or `docker` for containers).

---

## Features

- **systemd Auto-Discovery** — detects PostgreSQL, MariaDB, MongoDB, Redis (and more) with zero configuration
- **Docker support** — manage containerized databases via `docker start/stop/restart`, `docker exec` for CLI/backup/restore, `docker inspect` for status
- **Template-based config** — per-engine commands for backup, restore, and CLI are defined in a single `databases.json`
- **Full & single-database backup** — supports backing up entire engine or a specific database
- **Restore with stdin piping** — no temp files, streams backup content directly to the restore command
- **Service lifecycle** — `systemctl start/stop/restart` (systemd) or `docker start/stop/restart` (Docker) wrapper from the menu
- **Interactive CLI** — drops into the engine's native CLI (`psql`, `mariadb`, `redis-cli`, `mongosh`)
- **Backup retention** — configurable retention count with automatic cleanup
- **Graceful error handling** — missing binaries or connection failures never crash the tool

---

## Supported Engines

| Engine | Service Name | Backup | Restore | Single-DB Backup | Single-DB Restore |
|---|---|---|---|---|---|
| PostgreSQL | `postgresql` | `pg_dumpall` | `psql` (stdin) | `pg_dump --clean --if-exists` | `psql -d {db}` (stdin) |
| MariaDB / MySQL | `mariadb` | `mariadb-dump --all-databases` | `mariadb` (stdin) | `mariadb-dump {db}` | `mariadb {db}` (stdin) |
| MongoDB | `mongod` | `mongodump --archive` | `mongorestore --archive` | `mongodump --db {db} --archive` | `mongorestore --nsInclude {db}.* --archive` |
| Redis | `redis-server` | `cp /var/lib/redis/dump.rdb` | `cp dump.rdb` back | same as full | same as full |
| InfluxDB † | `influxdb` | `influx backup` | `influx restore` | — | — |

> † InfluxDB support is **experimental** — the `influx` CLI may not be installed by default. Contributions welcome.

---

## Quick Start

### Requirements

- Linux (systemd-based distro)
- [.NET 8.0 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- Passwordless `sudo` access
- Docker CLI (only if you manage containerized databases)

### Run from source

```bash
git clone <repo-url> && cd DbAutomatization
sudo dotnet run --project DbAutomatization.Cli
```

### Publish to standalone binary

```bash
dotnet publish DbAutomatization.Cli -c Release -r linux-x64 --self-contained -o /opt/dbautomate
sudo /opt/dbautomate/DbAutomatization.Cli
```

---

## Architecture & Configuration

Engines are defined by a `databases.json` file (auto-generated on first run). Each entry maps a systemd service name **or a Docker container name** to its commands. The `EngineType` field tells the tool how to manage the entry:

- `"systemd"` (default) — lifecycle via `sudo systemctl start|stop|restart`, status via `systemctl`, commands run natively on the host.
- `"docker"` — lifecycle via `docker start|stop|restart`, status via `docker inspect -f "{{.State.Running}}"`, commands run via `docker exec` (or `docker cp` for Redis). The entry key is the container name.

### Native (systemd) example

```json
{
  "postgresql": {
    "FriendlyName": "PostgreSQL",
    "CliCommand": ["sudo", "-u", "postgres", "psql"],
    "BackupCommand": ["sudo", "-u", "postgres", "pg_dumpall"],
    "SingleDatabaseBackupCommand": [
      "sudo", "-u", "postgres", "pg_dump",
      "--clean", "--if-exists", "-d", "{db}"
    ],
    "RestoreCommand": ["sudo", "-u", "postgres", "psql", "--set", "ON_ERROR_STOP=on"],
    "SingleDatabaseRestoreCommand": [
      "sudo", "-u", "postgres", "psql",
      "--set", "ON_ERROR_STOP=on", "-d", "{db}"
    ],
    "ListDatabasesCommand": [
      "sudo", "-u", "postgres", "psql", "-l", "-t", "-A"
    ],
    "RetentionCount": 7,
    "BackupTimeoutSeconds": 300
  }
}
```

### Docker (container) example

```json
{
  "postgres-main": {
    "FriendlyName": "PostgreSQL (Docker)",
    "EngineType": "docker",
    "CliCommand": ["docker", "exec", "-it", "{serviceName}", "psql", "-U", "postgres"],
    "BackupCommand": ["docker", "exec", "-i", "{serviceName}", "pg_dumpall", "-U", "postgres"],
    "SingleDatabaseBackupCommand": [
      "docker", "exec", "-i", "{serviceName}", "pg_dump",
      "-U", "postgres", "-d", "{db}"
    ],
    "RestoreCommand": ["docker", "exec", "-i", "{serviceName}", "psql", "-U", "postgres"],
    "SingleDatabaseRestoreCommand": [
      "docker", "exec", "-i", "{serviceName}", "psql",
      "-U", "postgres", "-d", "{db}"
    ],
    "ListDatabasesCommand": [
      "docker", "exec", "-i", "{serviceName}", "psql",
      "-U", "postgres", "-l", "-t", "-A"
    ],
    "BackupTimeoutSeconds": 300
  }
}
```

> **Streaming note:** commands that produce a dump stream (`BackupCommand`, `SingleDatabaseBackupCommand`) must use `docker exec -i` (never `-t`), so no terminal control characters land in the backup file. Interactive CLI commands use `docker exec -it`.

Placeholders:
- `{serviceName}` — substituted with the entry key (service or container name)
- `{path}` — substituted with the actual backup file path
- `{db}` — substituted with the selected database name

Built-in Docker defaults for PostgreSQL, MariaDB, MongoDB, Redis, and InfluxDB are baked into the binary. The **Add new database** menu (option 3) opens a submenu with: **systemd** (template-based wizard), **Docker** (scans `docker ps -a`, keeps only database containers — detected by image family postgres/mariadb/mysql/mongo/redis/influx — reads credentials and database name from the container env, generates a ready-to-use entry), and **advanced** (edit `databases.json` directly in `nano`/`vim`). Option 5, **Add database manually**, registers an entry only for a systemd service that already exists (`systemctl is-enabled`) — it does **not** install any packages. Existing entries can be overridden by editing `databases.json` directly.

---

## Security Considerations

- **Root requirement**: `sudo` is needed for `systemctl` calls, writing to `/var/lib/redis/`, and running `pg_dump`/`pg_restore` as the `postgres` system user. Passwordless sudo is expected — the tool runs a `sudo -n true` check at startup.
- **Docker**: for `EngineType: "docker"`, the tool shells out to `docker` directly (no `sudo`). The user needs permission to access the Docker socket (e.g., a member of the `docker` group).
- **No credential storage**: This tool does NOT store database passwords. It relies on OS-level trust (`postgres` peer auth, local socket, Unix user) or on credentials configured inside the container.
- **Editor safety**: When launching `nano`/`vim` via the **Edit config** option, the tool temporarily re-reads the file after the editor exits — it never holds the file open or runs the editor as a sub-process with elevated I/O handles beyond what the terminal provides.
- **Session isolation**: Process stdout/stderr are captured independently; interactive CLI sessions (`psql`, `redis-cli`) are attached to the user's terminal directly.

---

## License

MIT — see [LICENSE](LICENSE) for details.
