using DbAutomatization.Core.Services;

namespace DbAutomatization.Cli;

public class DatabaseAppService
{
    private readonly IDatabaseScanner _scanner;
    private readonly IServiceManager _manager;

    public DatabaseAppService(IDatabaseScanner scanner, IServiceManager manager)
    {
        _scanner = scanner;
        _manager = manager;
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
        Console.WriteLine("[5] backup");

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
                Console.WriteLine($"\nOpalanie konsoli interaktywnej dla {selectedDb.Name}...\n");
                string cliCommand = _scanner.GetCliCommandForService(selectedDb.ServiceName);
                await _manager.OpenDatabaseCliAsync(selectedDb.ServiceName, cliCommand);
                break;
            case "5":
                Console.WriteLine($"\n[Backup] Przygotowywanie kopii zapasowej dla {selectedDb.Name}...\n");
                
                string listDbCommand = _scanner.GetListDatabasesCommandForService(selectedDb.ServiceName);
                string selectedDatabaseName = string.Empty;

                if (!string.IsNullOrWhiteSpace(listDbCommand))
                {
                    var internalDatabases = await _manager.GetDatabasesAsync(listDbCommand);

                    if (internalDatabases.Any())
                    {
                        Console.WriteLine($"--- Wybierz bazę danych z silnika {selectedDb.Name} ---");
                        for (int d = 0; d < internalDatabases.Count; d++)
                        {
                            Console.WriteLine($"[{d + 1}] {internalDatabases[d]}");
                        }
                        Console.WriteLine("[0] Anuluj backup");
                        Console.Write("\nWybór: ");

                        if (int.TryParse(Console.ReadLine(), out int internalIndex) && internalIndex > 0 && internalIndex <= internalDatabases.Count)
                        {
                            selectedDatabaseName = internalDatabases[internalIndex - 1];
                        }
                        else
                        {
                            Console.WriteLine("Anulowano lub wybrano niepoprawną opcję.");
                            break;
                        }
                    }
                }

                string rawBackupCommand = _scanner.GetBackupCommandForService(selectedDb.ServiceName);
                
                if (string.IsNullOrWhiteSpace(rawBackupCommand))
                {
                    Console.WriteLine("[X] Błąd: Brak zdefiniowanej komendy backupu dla tej bazy w pliku konfiguracyjnym.");
                    break;
                }

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm");
                string dbNamePart = !string.IsNullOrEmpty(selectedDatabaseName) ? $"_{selectedDatabaseName}" : "";
                string fileName = $"backup_{selectedDb.ServiceName}{dbNamePart}_{timestamp}.bak";
                string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);

                string finalBackupCommand = rawBackupCommand.Replace("{path}", fullPath);
                if (!string.IsNullOrEmpty(selectedDatabaseName))
                {
                    finalBackupCommand = finalBackupCommand.Replace("{db}", selectedDatabaseName);
                }

                Console.WriteLine($"[Backup] Wykonywanie komendy: {finalBackupCommand}");
                var backupResult = await _manager.CreateBackupAsync(selectedDb.ServiceName, finalBackupCommand);
                Console.WriteLine(backupResult);
                break;

            default:
                Console.WriteLine("Niepoprawna opcja.");
                break;
        }

        var updatedList = await _scanner.ScanForDatabasesAsync();
        selectedDb = updatedList.FirstOrDefault(d => d.ServiceName == selectedDb.ServiceName) ?? selectedDb;
    }
}

        public async Task HandleAddDatabaseAsync()
        {
            Console.WriteLine("\n--- Dodawanie nowej bazy ---");
            
            Console.Write("Podaj nazwę bazy w systemie (np. redis-server, postgresql): ");
            var systemName = Console.ReadLine()?.ToLower().Trim();
            if (string.IsNullOrWhiteSpace(systemName)) return;

            Console.Write("Podaj nazwę wyświetlaną w menu (np. Redis, PostgreSQL): ");
            var friendlyName = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(friendlyName)) return;

            Console.Write($"Podaj komendę CLI do wejścia w bazę (domyślnie: '{systemName}', ENTER aby zaakceptować): ");
            var cliCommand = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(cliCommand))
            {
                cliCommand = systemName;
            }

            Console.WriteLine("\nWskazówka dla backupu: Użyj {path} dla pliku wynikowego oraz opcjonalnie {db} dla nazwy bazy.");
            Console.Write("Podaj komendę do backupu (np. sudo mariadb-dump {db} > {path}, ENTER jeśli brak): ");
            var backupCommand = Console.ReadLine()?.Trim() ?? string.Empty;

            Console.Write("Podaj komendę do listowania baz danych (np. sudo mysql -e \"SHOW DATABASES;\" -s --N, ENTER jeśli brak): ");
            var listDatabasesCommand = Console.ReadLine()?.Trim() ?? string.Empty;

            bool exists = await _scanner.DoesServiceExistAsync(systemName);

            if (exists)
            {
                _scanner.RegisterDatabase(systemName, friendlyName, cliCommand, backupCommand, listDatabasesCommand);
                Console.WriteLine($"\n[V] Baza '{friendlyName}' już istnieje w systemie i została dodana do monitorowania.");
                return;
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"\n[!] Baza '{systemName}' nie jest zainstalowana. Czy chcesz ją teraz zainstalować przez APT? (y/n): ");
            Console.ResetColor();
            
            var answer = Console.ReadLine()?.ToLower().Trim();

            if (answer == "y" || answer == "yes")
            {
                var (success, errorMessage) = await _scanner.InstallServiceViaAptAsync(systemName, systemName);

                if (success)
                {
                    _scanner.RegisterDatabase(systemName, friendlyName, cliCommand, backupCommand, listDatabasesCommand);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"\n[V] Sukces! Pomyślnie zainstalowano i dodano bazę {friendlyName} do monitorowania.");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\n[X] Błąd: Instalacja pakietu '{systemName}' nie powiodła się.");
                    Console.ResetColor();
                    
                    Console.WriteLine("\n--- Logi z systemu Linux ---");
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine(errorMessage);
                    Console.ResetColor();
                }
            }
        }

    public void HandleRemoveDatabase()
    {
        Console.Write("\nPodaj nazwę usługi do usunięcia: ");
        var serviceName = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(serviceName))
        {
            _scanner.UnregisterDatabase(serviceName);
            Console.WriteLine("Usunięto z listy monitorowanych.");
        }
    }
}