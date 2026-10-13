using DbAutomatization.Core.Models;
using DbAutomatization.Core.Services;

namespace DbAutomatization.Cli;

public class DatabaseAppService
{
    private readonly IDatabaseScanner _scanner;
    private readonly IServiceManager _manager;
    private readonly IDatabaseConfigRepository _configRepo;
    private readonly IBackupService _backup;
    private readonly IRestoreService _restore;
    private readonly DatabaseWizardService _wizard;
    private readonly IExternalEditorService _editor;
    private readonly IProcessExecutor _process;

    public DatabaseAppService(
        IDatabaseScanner scanner,
        IServiceManager manager,
        IDatabaseConfigRepository configRepo,
        IBackupService backup,
        IRestoreService restore,
        DatabaseWizardService wizard,
        IExternalEditorService editor,
        IProcessExecutor process)
    {
        _scanner = scanner;
        _manager = manager;
        _configRepo = configRepo;
        _backup = backup;
        _restore = restore;
        _wizard = wizard;
        _editor = editor;
        _process = process;
    }

    public async Task HandleScanAsync()
    {
        Console.WriteLine("\nSkanowanie systemu...");
        var allDatabases = await _scanner.ScanForDatabasesAsync();

        var installedDatabases = allDatabases.Where(db => db.IsInstalled).ToList();

        if (!installedDatabases.Any())
        {
            Console.WriteLine("\nBrak monitorowanych i zainstalowanych baz danych w systemie.");
            return;
        }

        Console.WriteLine("\nZainstalowane bazy danych:");
        Console.WriteLine("--------------------------------------------------");
        for (int i = 0; i < installedDatabases.Count; i++)
        {
            var db = installedDatabases[i];
            string status = db.IsRunning ? "URUCHOMIONA" : "ZATRZYMANA";
            Console.WriteLine($"[{i + 1}] {db.Name} ({db.ServiceName}) -> {status}");
        }
        Console.WriteLine("--------------------------------------------------");
    }

    public async Task HandleManageDatabaseAsync()
    {
        var databases = await _scanner.ScanForDatabasesAsync();

        if (databases == null || !databases.Any())
        {
            Console.WriteLine("\n[!] Brak baz danych do zarządzania.");
            return;
        }

        Console.WriteLine("\n--- Wybierz bazę do zarządzania ---");
        for (int i = 0; i < databases.Count; i++)
        {
            var db = databases[i];
            string status = db.IsInstalled
                ? (db.IsRunning ? "URUCHOMIONA" : "ZATRZYMANA")
                : "NIEZAINSTALOWANA";

            Console.WriteLine($"[{i + 1}] {db.Name} ({db.ServiceName}) -> {status}");
        }
        Console.WriteLine("[0] Powrót");

        Console.Write("\nWybór: ");
        if (!int.TryParse(Console.ReadLine(), out int dbIndex) || dbIndex < 0 || dbIndex > databases.Count)
        {
            Console.WriteLine("Niepoprawny wybór.");
            return;
        }

        if (dbIndex == 0) return;

        var selectedDb = databases[dbIndex - 1];

        if (!selectedDb.IsInstalled)
        {
            Console.WriteLine($"\n[!] Baza {selectedDb.Name} nie jest zainstalowana w systemie. Zainstaluj ją najpierw (opcja 3).");
            return;
        }

        while (true)
        {
            string currentStatus = selectedDb.IsRunning ? "URUCHOMIONA" : "ZATRZYMANA";
            Console.WriteLine($"\n=== Zarządzanie bazą: {selectedDb.Name} [{currentStatus}] ===");

            Console.WriteLine("[1] Uruchom (Start)");
            Console.WriteLine("[2] Zatrzymaj (Stop)");
            Console.WriteLine("[3] Zrestartuj (Restart)");

            Console.WriteLine("[4] Wejdź do konsoli CLI");
            Console.WriteLine("[5] Backup");
            Console.WriteLine("[6] Odtwórz z pliku (Restore)");
            Console.WriteLine("[0] Powrót do menu głównego");
            Console.Write("\nWybór działania: ");

            var subChoice = Console.ReadLine()?.Trim();

            if (subChoice == "0") break;

            switch (subChoice)
            {
                case "1":
                    Console.WriteLine($"Uruchamianie {selectedDb.Name}...");
                    await _manager.StartServiceAsync(selectedDb.ServiceName);
                    break;

                case "2":
                    Console.WriteLine($"Zatrzymywanie {selectedDb.Name}...");
                    await _manager.StopServiceAsync(selectedDb.ServiceName);
                    break;

                case "3":
                    Console.WriteLine($"Restartowanie {selectedDb.Name}...");
                    await _manager.RestartServiceAsync(selectedDb.ServiceName);
                    break;

                case "4":
                {
                    Console.WriteLine($"\nOtwieranie konsoli interaktywnej dla {selectedDb.Name}...\n");
                    var config = _configRepo.Get(selectedDb.ServiceName);
                    string[] cliCommand = config?.CliCommand ?? [selectedDb.ServiceName];
                    await _manager.OpenDatabaseCliAsync(selectedDb.ServiceName, cliCommand);
                    break;
                }

                case "5":
                {
                    Console.WriteLine($"\n[Backup] Przygotowywanie kopii zapasowej dla {selectedDb.Name}...\n");

                    var config = _configRepo.Get(selectedDb.ServiceName);
                    bool hasSingleDbCommand = config?.SingleDatabaseBackupCommand is { Length: > 0 };
                    bool hasListCommand = config?.ListDatabasesCommand is { Length: > 0 };
                    string? selectedDatabaseName = null;

                    if (hasSingleDbCommand && hasListCommand)
                    {
                        Console.WriteLine("--- Wybierz tryb backupu ---");
                        Console.WriteLine("[1] Cały silnik (Full Backup)");
                        Console.WriteLine("[2] Konkretna baza danych");
                        Console.WriteLine("[0] Anuluj");
                        Console.Write("\nWybór: ");

                        var modeChoice = Console.ReadLine()?.Trim();
                        if (modeChoice == "0")
                        {
                            Console.WriteLine("Anulowano.");
                            break;
                        }

                        if (modeChoice == "2")
                        {
                            var internalDatabases = await _manager.GetDatabasesAsync(selectedDb.ServiceName);

                            if (internalDatabases.Any())
                            {
                                Console.WriteLine($"\n--- Wybierz bazę danych z silnika {selectedDb.Name} ---");
                                for (int d = 0; d < internalDatabases.Count; d++)
                                    Console.WriteLine($"[{d + 1}] {internalDatabases[d]}");

                                Console.WriteLine("[0] Anuluj backup");
                                Console.Write("\nWybór: ");

                                if (int.TryParse(Console.ReadLine(), out int internalIndex) && internalIndex > 0 && internalIndex <= internalDatabases.Count)
                                    selectedDatabaseName = internalDatabases[internalIndex - 1];
                                else
                                {
                                    Console.WriteLine("Anulowano lub wybrano niepoprawną opcję.");
                                    break;
                                }
                            }
                            else
                            {
                                Console.WriteLine("[!] Nie udało się pobrać listy baz. Anulowano.");
                                break;
                            }
                        }
                    }
                    else if (hasListCommand)
                    {
                        var internalDatabases = await _manager.GetDatabasesAsync(selectedDb.ServiceName);

                        if (internalDatabases.Any())
                        {
                            Console.WriteLine($"--- Wybierz bazę danych z silnika {selectedDb.Name} ---");
                            for (int d = 0; d < internalDatabases.Count; d++)
                                Console.WriteLine($"[{d + 1}] {internalDatabases[d]}");

                            Console.WriteLine("[0] Anuluj backup");
                            Console.Write("\nWybór: ");

                            if (int.TryParse(Console.ReadLine(), out int internalIndex) && internalIndex > 0 && internalIndex <= internalDatabases.Count)
                                selectedDatabaseName = internalDatabases[internalIndex - 1];
                            else
                            {
                                Console.WriteLine("Anulowano lub wybrano niepoprawną opcję.");
                                break;
                            }
                        }
                    }

                    Console.Write("Kompresować backup? (T/n): ");
                    var compressInput = Console.ReadLine()?.ToLower().Trim();
                    bool compress = compressInput != "n" && compressInput != "no";

                    var backupResult = await _backup.CreateBackupAsync(
                        selectedDb.ServiceName,
                        selectedDatabaseName,
                        compress);

                    if (backupResult.Success)
                        Console.WriteLine($"[V] Backup wykonany pomyślnie!\nPlik zapisano w: {backupResult.FilePath}");
                    else
                        Console.WriteLine($"[X] Błąd backupu:\n{backupResult.Message}");

                    break;
                }

                case "6":
                {
                    await HandleRestoreAsync(selectedDb.ServiceName, selectedDb.Name);
                    break;
                }

                default:
                    Console.WriteLine("Niepoprawna opcja.");
                    break;
            }

            var updatedList = await _scanner.ScanForDatabasesAsync();
            selectedDb = updatedList.FirstOrDefault(d => d.ServiceName == selectedDb.ServiceName) ?? selectedDb;
        }
    }

    public async Task HandleAddDatabaseWizardAsync()
    {
        await _wizard.RunSystemdWizardAsync();
    }

    public async Task HandleAddDatabaseDockerAsync()
    {
        await _wizard.RunDockerWizardAsync();
    }

    public async Task HandleEditConfigAsync()
    {
        bool success = await _editor.EditConfigInExternalEditorAsync();
        if (success)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[V] Konfiguracja zaktualizowana pomyślnie.");
            Console.ResetColor();
        }
    }

    public async Task HandleAddDatabaseManualAsync()
    {
        Console.WriteLine("\n--- Dodawanie nowej bazy (ręczne) ---");

        Console.Write("Podaj nazwę usługi systemd (np. redis-server, postgresql): ");
        var serviceName = Console.ReadLine()?.ToLower().Trim();
        if (string.IsNullOrWhiteSpace(serviceName)) return;

        var checkResult = await _process.ExecuteAsync("systemctl", ["is-enabled", serviceName], timeoutSeconds: 5);
        if (!checkResult.Success)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[X] Usługa '{serviceName}' nie istnieje w systemd. Nie można dodać nieistniejącej bazy.");
            Console.ResetColor();
            return;
        }

        Console.Write("Podaj nazwę wyświetlaną w menu (np. Redis, PostgreSQL): ");
        var friendlyName = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(friendlyName)) return;

        Console.Write($"Komenda CLI (ENTER = '{serviceName}'): ");
        var cliInput = Console.ReadLine()?.Trim();
        string[] cliCommand = string.IsNullOrWhiteSpace(cliInput)
            ? [serviceName]
            : cliInput.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        Console.WriteLine("\n--- Backup ---");
        Console.Write("Komenda backupu (użyj {path} dla pliku, {db} dla bazy; ENTER = brak): ");
        var backupInput = Console.ReadLine()?.Trim();
        string[] backupCommand = string.IsNullOrWhiteSpace(backupInput)
            ? []
            : backupInput.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        Console.Write("Komenda backupu pojedynczej bazy (ENTER = brak): ");
        var singleBackupInput = Console.ReadLine()?.Trim();
        string[] singleDatabaseBackupCommand = string.IsNullOrWhiteSpace(singleBackupInput)
            ? []
            : singleBackupInput.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        Console.Write("Komenda przed backupem (np. redis-cli SAVE; ENTER = brak): ");
        var preBackupInput = Console.ReadLine()?.Trim();
        string[] preBackupCommand = string.IsNullOrWhiteSpace(preBackupInput)
            ? []
            : preBackupInput.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        Console.WriteLine("\n--- Restore ---");
        Console.Write("Komenda restore (ENTER = brak): ");
        var restoreInput = Console.ReadLine()?.Trim();
        string[] restoreCommand = string.IsNullOrWhiteSpace(restoreInput)
            ? []
            : restoreInput.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        Console.Write("Komenda restore pojedynczej bazy (ENTER = brak): ");
        var singleRestoreInput = Console.ReadLine()?.Trim();
        string[] singleDatabaseRestoreCommand = string.IsNullOrWhiteSpace(singleRestoreInput)
            ? []
            : singleRestoreInput.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        Console.WriteLine("\n--- Listowanie baz ---");
        Console.Write("Komenda do listowania baz (ENTER = brak): ");
        var listInput = Console.ReadLine()?.Trim();
        string[] listDatabasesCommand = string.IsNullOrWhiteSpace(listInput)
            ? []
            : listInput.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var config = new DatabaseConfig
        {
            FriendlyName = friendlyName,
            CliCommand = cliCommand,
            BackupCommand = backupCommand,
            SingleDatabaseBackupCommand = singleDatabaseBackupCommand,
            PreBackupCommand = preBackupCommand,
            RestoreCommand = restoreCommand,
            SingleDatabaseRestoreCommand = singleDatabaseRestoreCommand,
            ListDatabasesCommand = listDatabasesCommand
        };

        _configRepo.Register(serviceName, config);
        Console.WriteLine($"\n[V] Baza '{friendlyName}' (usługa systemd '{serviceName}') została dodana do monitorowania.");
    }

    public void HandleRemoveDatabase()
    {
        Console.Write("\nPodaj nazwę usługi do usunięcia: ");
        var serviceName = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(serviceName))
        {
            _configRepo.Unregister(serviceName);
            Console.WriteLine("Usunięto z listy monitorowanych.");
        }
    }

    public async Task HandleRestoreAsync(string serviceName, string friendlyName)
    {
        var config = _configRepo.Get(serviceName);
        bool hasRestoreCommand = config?.RestoreCommand is { Length: > 0 };

        if (!hasRestoreCommand)
        {
            Console.WriteLine($"\n[!] Brak zdefiniowanej komendy restore dla '{friendlyName}'.");
            return;
        }

        var backupDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups");
        var backupFiles = Directory.GetFiles(backupDir, $"backup_{serviceName}*")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTime)
            .ToList();

        if (backupFiles.Count == 0)
        {
            Console.WriteLine($"\n[!] Brak plików backupu dla '{friendlyName}' w katalogu:\n  {backupDir}");
            return;
        }

        Console.WriteLine($"\n--- Dostępne backupy dla {friendlyName} ---");
        for (int i = 0; i < backupFiles.Count; i++)
        {
            var f = backupFiles[i];
            Console.WriteLine($"[{i + 1}] {f.Name} ({f.LastWriteTime:yyyy-MM-dd HH:mm:ss})");
        }
        Console.WriteLine("[0] Anuluj");
        Console.Write("\nWybór pliku: ");

        if (!int.TryParse(Console.ReadLine(), out int fileIndex) || fileIndex <= 0 || fileIndex > backupFiles.Count)
        {
            Console.WriteLine("Anulowano.");
            return;
        }

        string selectedPath = backupFiles[fileIndex - 1].FullName;

        string? targetDatabase = null;
        bool hasSingleDbRestore = config?.SingleDatabaseRestoreCommand is { Length: > 0 };
        bool hasListCommand = config?.ListDatabasesCommand is { Length: > 0 };

        if (hasSingleDbRestore && hasListCommand)
        {
            Console.WriteLine("\n--- Wybierz docelową bazę danych ---");
            Console.WriteLine("[1] Przywróć do całego silnika");
            Console.WriteLine("[2] Przywróć do konkretnej bazy");
            Console.WriteLine("[0] Anuluj");
            Console.Write("\nWybór: ");

            var modeChoice = Console.ReadLine()?.Trim();
            if (modeChoice == "0")
            {
                Console.WriteLine("Anulowano.");
                return;
            }

            if (modeChoice == "2")
            {
                var databases = await _manager.GetDatabasesAsync(serviceName);
                if (databases.Any())
                {
                    Console.WriteLine($"\n--- Wybierz docelową bazę danych ---");
                    for (int d = 0; d < databases.Count; d++)
                        Console.WriteLine($"[{d + 1}] {databases[d]}");

                    Console.Write("\nWybór: ");

                    if (int.TryParse(Console.ReadLine(), out int dbIdx) && dbIdx > 0 && dbIdx <= databases.Count)
                        targetDatabase = databases[dbIdx - 1];
                    else
                    {
                        Console.WriteLine("Niepoprawny wybór. Przywracam do całego silnika.");
                    }
                }
            }
        }

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("\n[!] UWAGA: Odtwarzanie danych nadpisze istniejące tabele/bazy!");
        Console.ResetColor();
        Console.Write("Czy na pewno chcesz kontynuować? (y/N): ");

        var confirm = Console.ReadLine()?.ToLower().Trim();
        if (confirm != "y" && confirm != "yes")
        {
            Console.WriteLine("Anulowano.");
            return;
        }

        Console.WriteLine($"\nPrzywracanie backupu dla {friendlyName}...");
        var result = await _restore.RestoreAsync(serviceName, selectedPath, targetDatabase);

        if (result.Success)
            Console.WriteLine("[V] Przywracanie zakończone pomyślnie.");
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[X] Błąd przywracania:\n{result.Error}");
            Console.ResetColor();
        }
    }
}
