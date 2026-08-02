# DbAutomatization

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com)
[![C#](https://img.shields.io/badge/C%23-12.0-239120?logo=csharp)](https://learn.microsoft.com/en-us/dotnet/csharp/)
[![Docker](https://img.shields.io/badge/Docker-Supported-2496ED?logo=docker)](https://www.docker.com)
[![Linux](https://img.shields.io/badge/Linux-%20-FCC624?logo=linux)](https://kernel.org)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)
[![Status](https://img.shields.io/badge/status-active-brightgreen)]()

> **SysOps CLI Tool** — Automate database backups, restores, and service lifecycle management on Linux for both **systemd** native services and **Docker** containers.

---

## Overview

**DbAutomatization** is an interactive, zero-dependency C# console application designed for SysOps and DevOps engineers. It automatically discovers installed database engines (systemd units or running Docker containers), maps them via template-based configurations, and provides a streamlined terminal interface for:

* **Lifecycle management** (`start` / `stop` / `restart`)
* **Full & single-database backups** (with configurable retention)
* **Zero-tempfile restores** via direct `stdin` piping
* **Interactive CLI entry** (`psql`, `mariadb`, `mongosh`, `redis-cli`)

---

## Features

- 🔍 **Auto-Discovery** — Detects native systemd database units and containerized database instances (`docker ps`).
- 🐳 **Native Docker Integration** — Manages containers via `docker start/stop` and executes commands using `docker exec` without modifying container images.
- 📜 **Template-Based JSON Configuration** — Engine commands are cleanly structured in `databases.json` using placeholders (`{serviceName}`, `{path}`, `{db}`).
- 🚀 **Streamed I/O** — Backups stream directly to disk; restores pipe straight to process `stdin` without temporary storage overhead.
- 📦 **Zero Runtime Dependencies** — Distributed as a self-contained, single-file binary.
- 🧹 **Automatic Retention** — Automatically purges outdated backup files based on configurable limits.
- 🔒 **No Credential Storage** — Relies strictly on OS-level permissions (`sudo`, Unix sockets) or container environment trust.

---

## Supported Engines

| Engine | Service Name | Backup Command | Restore Method | Single-DB Backup |
|---|---|---|---|---|
| **PostgreSQL** | `postgresql` | `pg_dumpall` | `psql` (stdin) | `pg_dump --clean --if-exists` |
| **MariaDB / MySQL** | `mariadb` | `mariadb-dump --all-databases` | `mariadb` (stdin) | `mariadb-dump {db}` |
| **MongoDB** | `mongod` | `mongodump --archive` | `mongorestore --archive` | `mongodump --db {db} --archive` |
| **Redis** | `redis-server` | `cp dump.rdb` / `docker cp` | File restoration | N/A (Full Engine) |
| **InfluxDB** † | `influxdb` | `influx backup` | `influx restore` | N/A |

> † *InfluxDB support is experimental — requires native `influx` CLI presence.*

---

## Quick Start

### Option A: Direct Binary Download (Recommended)

No .NET SDK or runtime required. Downloads the self-contained Linux x64 binary directly from GitHub Releases:

```bash
# 1. Download binary to system path (Replace <username> with your actual GitHub username)
sudo curl -L [https://github.com/](https://github.com/)<username>/DbAutomatization/releases/download/v1.0.0/dbautomate -o /usr/local/bin/dbautomate

# 2. Grant execution permissions
sudo chmod +x /usr/local/bin/dbautomate

# 3. Launch CLI (Requires root/sudo privileges)
sudo dbautomate
Option B: Build from Source (For Developers)
Requirements

    Linux (systemd-based distribution)

    .NET 8.0 SDK

    Root/sudo privileges

    Docker CLI (if managing containers)

Clone & Run
git clone [https://github.com/](https://github.com/)Pakanor/DbAutomatization.git
cd DbAutomatization
sudo /your/folder/.dotnet/dotnet run DbAutomatization.Cli

Publish Single-File Binary Locally
dotnet publish DbAutomatization.Cli/DbAutomatization.Cli.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  /p:PublishSingleFile=true \
  -o ./publish

sudo ./publish/dbautomate

Architecture & Configuration

Configurations are stored in databases.json (generated automatically on first launch). Engines are categorized by EngineType:

    "systemd" — Controls native host services (sudo systemctl).

    "docker" — Controls container instances (docker exec / docker inspect). Key must match container name.

Configuration Structure (databases.json)
Native systemd Engine Example

{
  "postgresql": {
    "FriendlyName": "PostgreSQL (Native)",
    "EngineType": "systemd",
    "CliCommand": ["sudo", "-u", "postgres", "psql"],
    "BackupCommand": ["sudo", "-u", "postgres", "pg_dumpall"],
    "SingleDatabaseBackupCommand": [
      "sudo", "-u", "postgres", "pg_dump", "--clean", "--if-exists", "-d", "{db}"
    ],
    "RestoreCommand": ["sudo", "-u", "postgres", "psql", "--set", "ON_ERROR_STOP=on"],
    "SingleDatabaseRestoreCommand": [
      "sudo", "-u", "postgres", "psql", "--set", "ON_ERROR_STOP=on", "-d", "{db}"
    ],
    "RetentionCount": 7,
    "BackupTimeoutSeconds": 300
  }
}

Containerized docker Engine Example

{
  "postgres-main": {
    "FriendlyName": "PostgreSQL (Docker)",
    "EngineType": "docker",
    "CliCommand": ["docker", "exec", "-it", "{serviceName}", "psql", "-U", "postgres"],
    "BackupCommand": ["docker", "exec", "-i", "{serviceName}", "pg_dumpall", "-U", "postgres"],
    "SingleDatabaseBackupCommand": [
      "docker", "exec", "-i", "{serviceName}", "pg_dump", "-U", "postgres", "-d", "{db}"
    ],
    "RestoreCommand": ["docker", "exec", "-i", "{serviceName}", "psql", "-U", "postgres"],
    "SingleDatabaseRestoreCommand": [
      "docker", "exec", "-i", "{serviceName}", "psql", "-U", "postgres", "-d", "{db}"
    ],
    "RetentionCount": 7,
    "BackupTimeoutSeconds": 300
  }
}
⚠️ Streaming Note: Backup commands targeting Docker must use -i (interactive stream) and never -t (TTY), avoiding ANSI control characters in output dump files.

Template Placeholders

    {serviceName} — Substituted with entry key (service name or container ID/name).

    {path} — Substituted with the destination backup filepath.

    {db} — Substituted with target database name.

Security Model

    Root Requirement: The CLI strictly requires root access (sudo) at startup to control systemd services and read system-level sockets.

    No Hardcoded Passwords: Password authentication is bypassed in favor of local OS trust (peer authentication, local Unix sockets) or pre-configured container environment variables.

    Subprocess Isolation: Process stdout and stderr are handled explicitly. Interactive CLI sessions attach safely to standard user TTY handles.
