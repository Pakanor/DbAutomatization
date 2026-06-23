using Microsoft.Extensions.DependencyInjection;
using DbAutomatization.Core.Services;
using DbAutomatization.Infrastructure.Linux;

namespace DbAutomatization.Cli;

class Program
{
    static async Task Main(string[] args)
    {
        var serviceProvider = new ServiceCollection()
            .AddSingleton<IBashExecutor, BashExecutor>()
            .AddTransient<IDatabaseScanner, SystemdScanner>()
            .AddTransient<IServiceManager, SystemdServiceManager>()
            .BuildServiceProvider();

        var scanner = serviceProvider.GetRequiredService<IDatabaseScanner>();
        var manager = serviceProvider.GetRequiredService<IServiceManager>();

        Console.WriteLine("=== DbAutomatization CLI ===");

        while (true)
        {
            Console.WriteLine("\n[1] Skanuj bazy danych");
            Console.WriteLine("[2] Zarządzaj bazą (Start/Stop)");
            Console.WriteLine("[0] Wyjście");
            Console.Write("\nWybierz opcję: ");
            
            var choice = Console.ReadLine();

            if (choice == "0") break;

            switch (choice)
            {
                case "1":
                    await HandleScanAsync(scanner);
                    break;
                case "2":
                    await HandleManagementAsync(scanner, manager);
                    break;
                default:
                    Console.WriteLine("Niepoprawny wybór.");
                    break;
            }
        }
    }

    private static async Task HandleScanAsync(IDatabaseScanner scanner)
    {
        Console.WriteLine("\nSkanowanie systemu...");
        var databases = await scanner.ScanForDatabasesAsync();

        Console.WriteLine("\nWyniki skanowania:");
        Console.WriteLine("--------------------------------------------------");
        for (int i = 0; i < databases.Count; i++)
        {
            var db = databases[i];
            string status = db.IsInstalled 
                ? (db.IsRunning ? "URUCHOMIONA" : "ZATRZYMANA") 
                : "NIEZAINSTALOWANA";

            Console.WriteLine($"[{i + 1}] {db.Name} ({db.ServiceName}) -> {status}");
        }
        Console.WriteLine("--------------------------------------------------");
    }

    private static async Task HandleManagementAsync(IDatabaseScanner scanner, IServiceManager manager)
    {
        var databases = await scanner.ScanForDatabasesAsync();
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
            var success = await manager.StopServiceAsync(selectedDb.ServiceName);
            Console.WriteLine(success ? "Zatrzymano pomyślnie." : "Błąd podczas zatrzymywania (sprawdź uprawnienia sudo).");
        }
        else
        {
            Console.WriteLine($"Uruchamianie {selectedDb.Name}...");
            var success = await manager.StartServiceAsync(selectedDb.ServiceName);
            Console.WriteLine(success ? "Uruchomiono pomyślnie." : "Błąd podczas uruchamiania (sprawdź uprawnienia sudo).");
        }
    }
}