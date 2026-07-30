# DbAutomatization

CLI tool for automating database service management on Linux. Scans, starts, stops, backs up, and provides interactive CLI access to database engines (PostgreSQL, MariaDB, MongoDB, Redis, InfluxDB) via systemd.

---

## Architecture (3-layer)

```
DbAutomatization.sln
├── DbAutomatization.Core            # Domain: models + interfaces
├── DbAutomatization.Infrastructure  # Linux implementations
└── DbAutomatization.Cli             # Console app (UI + DI)
```

| Layer | Project | Dependencies | Responsibility |
|---|---|---|---|
| **Core** | `DbAutomatization.Core` | none | Models, interfaces, exceptions, orchestration `BackupService` |
| **Infrastructure** | `DbAutomatization.Infrastructure` | Core | Linux implementations: `ProcessExecutor`, `PermissionService`, `DatabaseConfigRepository`, `AptPackageManager`, `SystemdScanner`, `SystemdServiceManager` |
| **CLI** | `DbAutomatization.Cli` | Core + Infrastructure + DI | Entry point, console menu, UI business logic |

---

## Quick Start

```bash
dotnet run --project DbAutomatization.Cli
```

Or after build:

```bash
dotnet build
./DbAutomatization.Cli/bin/Debug/net8.0/DbAutomatization.Cli
```

---

## Permission Modes

The application detects permission level at startup via `sudo -n true` (5s timeout). Results are cached for the session.

| Mode | `HasSudo` | Available features |
|---|---|---|
| **Administrator** | `true` | Scan, Manage (start/stop/restart), CLI, Backup, Add (with apt install), Remove |
| **User** | `false` | Scan, Manage (CLI + Backup only), Add (register only), Remove |

In User mode, systemd/apt operations are blocked with a clear message.

---

## Models

### `DatabaseConfig`
| Field | Type | Default | Description |
|---|---|---|---|
| `FriendlyName` | `string` | — | Display name in menu |
| `CliCommand` | `string[]` | — | Interactive CLI command (e.g. `["psql"]`) |
| `BackupCommand` | `string[]` | — | Backup command template with `{path}` and `{db}` placeholders |
| `ListDatabasesCommand` | `string[]` | — | Command to list databases within the engine |
| `PreBackupCommand` | `string[]` | — | Command executed before backup (e.g. `["redis-cli", "SAVE"]`) |
| `BackupTimeoutSeconds` | `int` | `300` | Per-database backup timeout |
| `RetentionCount` | `int` | `0` | Max backup files to keep (0 = unlimited) |
| `OutputColumnIndex` | `int` | `0` | Column index for parsing tabular `ListDatabasesCommand` output (InfluxDB uses `1`) |

### `DetectedDatabase`
| Field | Type | Description |
|---|---|---|
| `Name` | `string` | Friendly name |
| `ServiceName` | `string` | systemd service name |
| `IsInstalled` | `bool` | Whether the unit file exists |
| `IsRunning` | `bool` | Whether the service is active |

### `ProcessResult`
| Field | Type | Description |
|---|---|---|
| `Output` | `string` | stdout content |
| `Error` | `string` | stderr content |
| `ExitCode` | `int` | Process exit code |
| `Success` | `bool` | `ExitCode == 0` |

### `BackupResult`
| Field | Type | Description |
|---|---|---|
| `FilePath` | `string` | Path to the created backup file |
| `Message` | `string` | Output message |
| `ExitCode` | `int` | Process exit code |
| `Success` | `bool` | `ExitCode == 0` |

---

## Interfaces

### `IProcessExecutor` — Safe process execution
| Method | Description |
|---|---|
| `ExecuteAsync(...)` | Executes a process with `ArgumentList`, returns `ProcessResult`. Timeout via `CancellationToken`. |
| `ExecuteInteractiveAsync(...)` | Runs a process interactively (no stdio redirect) |
| `ExecuteAndStreamOutputAsync(...)` | Pipes stdout directly into a `Stream`. Used for backup writes — no shell involvement. |

### `IDatabaseScanner` — System scan
| Method | Description |
|---|---|
| `ScanForDatabasesAsync(...)` | Scans systemd for registered database services |

### `IServiceManager` — Service control
| Method | Description |
|---|---|
| `StartServiceAsync(...)` | `sudo systemctl start` |
| `StopServiceAsync(...)` | `sudo systemctl stop` |
| `RestartServiceAsync(...)` | `sudo systemctl restart` |
| `GetDatabasesAsync(...)` | Lists databases for a service |
| `OpenDatabaseCliAsync(...)` | Opens interactive CLI |

### `IDatabaseConfigRepository` — JSON config
| Method | Description |
|---|---|
| `GetAll()` | Returns all configured database entries |
| `Get(serviceName)` | Returns config for a specific service |
| `Register(serviceName, config)` | Adds a new database to config |
| `Unregister(serviceName)` | Removes a database from config |

### `IPermissionService` — Sudo detection
| Property | Description |
|---|---|
| `HasSudo` | `true` if root or passwordless `sudo` is available |

### `IAptPackageManager` — Package management
| Method | Description |
|---|---|
| `DoesPackageExistAsync(...)` | Checks via `which` |
| `InstallPackageAsync(...)` | `apt update && apt install && systemctl enable && systemctl start` |

### `IBackupService` — Backup orchestration
| Method | Description |
|---|---|
| `CreateBackupAsync(...)` | Executes `PreBackupCommand` (if set), then backup. Generates filename with ms‑precision timestamp, creates a `FileStream`, pipes process stdout via `ExecuteAndStreamOutputAsync`. Runs retention cleanup on success. |

### `IBackupRetentionManager` — Retention cleanup
| Method | Description |
|---|---|
| `CleanupAsync(directory, filePrefix, retentionCount)` | Scans directory, sorts by `LastWriteTime`, removes oldest files beyond `retentionCount`. |

---

## Supported Databases

| Database | Service | CLI | PreBackupCommand | Backup command | List databases |
|---|---|---|---|---|---|---|
| **PostgreSQL** | `postgresql` | `sudo -u postgres psql` | — | `sudo -u postgres pg_dumpall` | `psql -t -A -c "SELECT datname FROM pg_database WHERE datistemplate = false;"` |
| **MariaDB** | `mariadb` | `mariadb` | — | `sudo mariadb-dump --all-databases` | `sudo mysql -e "SHOW DATABASES;" -s --N` |
| **MongoDB** | `mongod` | `mongosh` | — | `mongodump --archive={path}` | _(none)_ |
| **Redis** | `redis-server` | `redis-cli` | `redis-cli SAVE` | `sudo cp /var/lib/redis/dump.rdb {path}` | _(none)_ |
| **InfluxDB** | `influxdb` | `influx` | — | `influx backup {path}` | `influx bucket list --hide-headers` (`OutputColumnIndex: 1`) |

Backup methods: stdout → FileStream (PostgreSQL, MariaDB), `{path}` in argument (MongoDB, Redis, InfluxDB). No shell involved.

---

## Configuration (`databases.json`)

Stored in the application's bin directory. Created automatically with sensible defaults on first run.

```json
{
  "postgresql": {
    "FriendlyName": "PostgreSQL",
    "CliCommand": ["sudo", "-u", "postgres", "psql"],
    "BackupCommand": ["sudo", "-u", "postgres", "pg_dumpall"],
    "ListDatabasesCommand": ["sudo", "-u", "postgres", "psql", "-t", "-A", "-c", "SELECT datname FROM pg_database WHERE datistemplate = false;"],
    "PreBackupCommand": [],
    "BackupTimeoutSeconds": 300,
    "RetentionCount": 0,
    "OutputColumnIndex": 0
  },
  "redis-server": {
    "FriendlyName": "Redis",
    "CliCommand": ["redis-cli"],
    "BackupCommand": ["sudo", "cp", "/var/lib/redis/dump.rdb", "{path}"],
    "PreBackupCommand": ["redis-cli", "SAVE"],
    "ListDatabasesCommand": []
  },
  "influxdb": {
    "FriendlyName": "InfluxDB",
    "CliCommand": ["influx"],
    "BackupCommand": ["influx", "backup", "{path}"],
    "ListDatabasesCommand": ["influx", "bucket", "list", "--hide-headers"],
    "OutputColumnIndex": 1
  }
}
```

Legacy `string` format is auto-migrated via `CommandArrayConverter` — old format with `>` redirect and `/bin/bash -c` wrappers are cleaned up on load.

**Placeholders:**
- `{path}` — backup file path (auto-generated). If present in the command, substituted directly (e.g. `mongodump --archive={path}`). If absent, stdout is piped to the file automatically.
- `{db}` — selected database name (optional)

---

## DI Registration (`Program.cs`)

| Interface | Implementation | Lifetime |
|---|---|---|
| `IProcessExecutor` | `ProcessExecutor` | Singleton |
| `IDatabaseConfigRepository` | `DatabaseConfigRepository` | Singleton |
| `IPermissionService` | `PermissionService` | Singleton |
| `IAptPackageManager` | `AptPackageManager` | Transient |
| `IBackupService` | `BackupService` | Transient |
| `IBackupRetentionManager` | `BackupRetentionManager` | Singleton |
| `IDatabaseScanner` | `SystemdScanner` | Transient |
| `IServiceManager` | `SystemdServiceManager` | Transient |
| `DatabaseAppService` | — | Transient |
| `ConsoleMenu` | — | Transient |

---

## Design Patterns

| Pattern | Location |
|---|---|
| **Dependency Injection** | `Program.cs` — `ServiceCollection` |
| **Strategy** | All interfaces — swap implementations (e.g. Windows, Docker) |
| **Facade** | `DatabaseAppService` — wraps all services behind menu methods |
| **Repository** | `DatabaseConfigRepository` — JSON file persistence |
| **Service Layer** | `BackupService` — domain orchestration in Core |

---

## Security

- **No shell** — all process execution uses `ProcessStartInfo.ArgumentList`. Shell (`/bin/bash`) is never invoked.
- **No shell redirect for backups** — stdout piped directly to a `FileStream` via `ExecuteAndStreamOutputAsync`.
- **Timeout** — every process call has a configurable timeout (default 30s, backup 300s, configurable per database).
- **Exit code verification** — success determined by `ExitCode == 0`, not by string pattern matching.
- **No `ValidateConfig`** — shell operator scanning removed; no false alarms for SQL semicolons.

---

## Error Handling

- `DatabaseConfigurationException` thrown when `databases.json` is corrupted — user can restore defaults or exit.
- `ProcessResult.ExitCode` is the sole arbiter of success/failure.
- All async methods accept `CancellationToken`.

---

## Change log

All changes are documented in [`zmiany.md`](zmiany.md) (Polish).

---

## Future Improvements

- [ ] `SystemdScanner` — batch `systemctl is-active` calls into a single invocation
- [ ] Windows support via Docker-based database engines

## Tests

The `DbAutomatization.Tests` xUnit project contains **49 tests** (48 unit + 1 integration) covering all layers:

| Test file | Tests | What it covers |
|---|---|---|
| `CommandArrayConverterTests.cs` | 6 | Old/new JSON format, empty/null, pipe-not-wrapped |
| `BackupServiceTests.cs` | 10 | `{path}`/`{db}`, `PreBackupCommand`, streaming, timeout, retention, failure paths |
| `DatabaseConfigRepositoryTests.cs` | 3 | Corrupted JSON, default creation, legacy redirect migration |
| `PermissionServiceTests.cs` | 4 | Sudo true/false, exception handling, caching |
| `SystemdServiceManagerTests.cs` | 9 | Start/stop/restart, `OutputColumnIndex`, CLI, error handling |
| `SystemdScannerTests.cs` | 3 | Database detection, missing unit, empty config |
| `AptPackageManagerTests.cs` | 4 | Package check, install sequence, error handling |
| `ProcessExecutorTests.cs` | 6 | Stdout/stderr, exit code, timeout, streaming, missing executable |
| `BackupRetentionManagerTests.cs` | 3 | Retention zero, cleanup, missing directory |
| `IntegrationTests.cs` | 1 | Full backup cycle with real `ProcessExecutor`, `touch`, retention check |

Run: `dotnet test`