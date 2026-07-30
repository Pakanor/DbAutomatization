# DbAutomatization

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com)
[![C#](https://img.shields.io/badge/C%23-12.0-239120?logo=csharp)](https://learn.microsoft.com/en-us/dotnet/csharp/)
[![Linux](https://img.shields.io/badge/Linux-%20-FCC624?logo=linux)](https://kernel.org)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)
[![Status](https://img.shields.io/badge/status-active-brightgreen)]()

> **SysOps CLI tool** — Automate database backup, restore, and service management on Linux via interactive terminal menus and systemd auto-discovery.

---

## Overview

DbAutomatization is a C# console application that discovers installed database engines via systemd, registers them with pre-configured templates, and provides an intuitive menu-driven interface for backup, restore, service lifecycle (`start`/`stop`/`restart`), and interactive CLI entry — all under `sudo`.

---

## Features

- **systemd Auto-Discovery** — detects PostgreSQL, MariaDB, MongoDB, Redis (and more) with zero configuration
- **Template-based config** — per-engine commands for backup, restore, and CLI are defined in a single `databases.json`
- **Full & single-database backup** — supports backing up entire engine or a specific database
- **Restore with stdin piping** — no temp files, streams backup content directly to the restore command
- **Service lifecycle** — `systemctl start/stop/restart` wrapper from the menu
- **Interactive CLI** — drops into the engine's native CLI (`psql`, `mariadb`, `redis-cli`, `mongosh`)
- **APT integration** — optionally install missing packages from within the tool
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

Engines are defined by a `databases.json` file (auto-generated on first run). Each entry maps a systemd service name to its commands:

```json
{
  "postgresql": {
    "FriendlyName": "PostgreSQL",
    "AptPackageName": "postgresql",
    "CliCommand": ["sudo", "-u", "postgres", "psql"],
    "BackupCommand": ["sudo", "-u", "postgres", "pg_dumpall"],
    "SingleDatabaseBackupCommand": [
      "sudo", "-u", "postgres", "pg_dump",
      "--clean", "--if-exists", "-d", "{db}"
    ],
    "RestoreCommand": ["sudo", "-u", "postgres", "psql"],
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

Placeholders:
- `{path}` — substituted with the actual backup file path
- `{db}` — substituted with the selected database name

Templates are baked into the binary and can be applied via the **Add new database** wizard or overridden by editing `databases.json` directly.

---

## Security Considerations

- **Root requirement**: `sudo` is needed for `systemctl` calls, writing to `/var/lib/redis/`, and running `pg_dump`/`pg_restore` as the `postgres` system user. Passwordless sudo is expected — the tool runs a `sudo -n true` check at startup.
- **No credential storage**: This tool does NOT store database passwords. It relies on OS-level trust (`postgres` peer auth, local socket, Unix user).
- **Editor safety**: When launching `nano`/`vim` via the **Edit config** option, the tool temporarily re-reads the file after the editor exits — it never holds the file open or runs the editor as a sub-process with elevated I/O handles beyond what the terminal provides.
- **Session isolation**: Process stdout/stderr are captured independently; interactive CLI sessions (`psql`, `redis-cli`) are attached to the user's terminal directly.

---

## License

MIT — see [LICENSE](LICENSE) for details.
