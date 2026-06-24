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

    public async Task HandleManagementAsync()
    {
        var databases = await _scanner.ScanForDatabasesAsync();
        var installedDbs = databases.Where(d => d.IsInstalled).ToList();

        if (!installedDbs.Any())
        {
            Console.WriteLine("Brak zainstalowanych baz do zarządzania.");
            return;
        }

        Console.WriteLine("\nWybierz bazę do przełączenia stanu:");
        for (int i = 0; i < installedDbs.Count; i++)
        {
            var db = installedDbs[i];
            Console.WriteLine($"[{i + 1}] {db.Name} (Aktualnie: {(db.IsRunning ? "Działa" : "Stoi")})");
        }

        Console.Write("Wybór: ");
        if (!int.TryParse(Console.ReadLine(), out int index) || index < 1 || index > installedDbs.Count)
        {
            Console.WriteLine("Błędny indeks.");
            return;
        }

        var selectedDb = installedDbs[index - 1];

        if (selectedDb.IsRunning)
        {
            Console.WriteLine($"Zatrzymywanie {selectedDb.Name}...");
            var success = await _manager.StopServiceAsync(selectedDb.ServiceName);
            Console.WriteLine(success ? "Zatrzymano pomyślnie." : "Błąd (sprawdź uprawnienia sudo).");
        }
        else
        {
            Console.WriteLine($"Uruchamianie {selectedDb.Name}...");
            var success = await _manager.StartServiceAsync(selectedDb.ServiceName);
            Console.WriteLine(success ? "Uruchomiono pomyślnie." : "Błąd (sprawdź uprawnienia sudo).");
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

        bool exists = await _scanner.DoesServiceExistAsync(systemName);

        if (exists)
        {
            _scanner.RegisterDatabase(systemName, friendlyName);
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
                _scanner.RegisterDatabase(systemName, friendlyName);
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