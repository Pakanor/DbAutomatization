using DbAutomatization.Core.Models;
using DbAutomatization.Core.Services;

namespace DbAutomatization.Cli;

public class DatabaseWizardService
{
    private readonly IDatabaseConfigRepository _configRepo;
    private readonly IDockerContainerDiscoveryService _dockerDiscovery;

    public DatabaseWizardService(IDatabaseConfigRepository configRepo, IDockerContainerDiscoveryService dockerDiscovery)
    {
        _configRepo = configRepo;
        _dockerDiscovery = dockerDiscovery;
    }

    public Task RunSystemdWizardAsync()
    {
        Console.WriteLine("\n=== Kreator dodawania bazy systemowej (systemd) ===");

        var template = SelectTemplate();
        if (template == null)
        {
            Console.WriteLine("Anulowano.");
            return Task.CompletedTask;
        }

        Console.Write("\nPodaj nazwę usługi systemd (np. postgresql): ");
        var serviceName = Console.ReadLine()?.ToLower().Trim();
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            Console.WriteLine("Anulowano.");
            return Task.CompletedTask;
        }

        Console.Write("Podaj nazwę wyświetlaną (np. PostgreSQL): ");
        var friendlyName = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(friendlyName))
        {
            Console.WriteLine("Anulowano.");
            return Task.CompletedTask;
        }

        var config = BuildConfig(template.Value, serviceName, friendlyName, "systemd");
        _configRepo.Register(serviceName, config);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[V] Baza '{friendlyName}' ({serviceName}) została dodana.");
        Console.ResetColor();

        return Task.CompletedTask;
    }

    public async Task RunDockerWizardAsync()
    {
        Console.WriteLine("\n=== Bazy danych w Dockerze ===");

        var containers = await _dockerDiscovery.DiscoverDatabasesAsync();

        if (containers.Count == 0)
        {
            Console.WriteLine("Nie znaleziono baz danych w Dockerze (postgres, mariadb/mysql, mongo, redis, influx).");
            return;
        }

        for (int i = 0; i < containers.Count; i++)
        {
            var c = containers[i];
            string status = c.IsRunning ? "URUCHOMIONA" : "ZATRZYMANA";
            string already = _configRepo.Get(c.Name) != null ? " [już monitorowana]" : "";
            Console.WriteLine($"[{i + 1}] {c.Name} ({c.FamilyTitle}) -> {status}{already}");
        }
        Console.WriteLine("[0] Powrót");

        Console.Write("\nWybierz bazę do dodania: ");
        if (!int.TryParse(Console.ReadLine(), out int index) || index <= 0 || index > containers.Count)
        {
            Console.WriteLine("Anulowano.");
            return;
        }

        var selected = containers[index - 1];

        if (_configRepo.Get(selected.Name) != null)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[!] Baza '{selected.Name}' jest już monitorowana.");
            Console.ResetColor();
            return;
        }

        Console.Write($"Nazwa wyświetlana w menu (ENTER = '{selected.Name}'): ");
        var friendlyName = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(friendlyName))
            friendlyName = selected.Name;

        var config = selected.Config;
        config.FriendlyName = friendlyName;

        _configRepo.Register(selected.Name, config);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[V] Baza '{friendlyName}' ({selected.Name}, docker {selected.FamilyTitle}) została dodana.");
        Console.ResetColor();
    }

    private static DatabaseTemplate? SelectTemplate()
    {
        Console.WriteLine("\n--- Wybierz szablon silnika ---");
        Console.WriteLine("[1] PostgreSQL");
        Console.WriteLine("[2] MariaDB / MySQL");
        Console.WriteLine("[3] MongoDB");
        Console.WriteLine("[4] Redis");
        Console.WriteLine("[5] InfluxDB");
        Console.WriteLine("[6] Custom (czysta konfiguracja)");
        Console.WriteLine("[0] Anuluj");
        Console.Write("\nWybór: ");

        var choice = Console.ReadLine()?.Trim();
        return choice switch
        {
            "1" => DatabaseTemplate.PostgreSQL,
            "2" => DatabaseTemplate.MariaDB,
            "3" => DatabaseTemplate.MongoDB,
            "4" => DatabaseTemplate.Redis,
            "5" => DatabaseTemplate.InfluxDB,
            "6" => DatabaseTemplate.Custom,
            _ => null
        };
    }

    private static DatabaseConfig BuildConfig(DatabaseTemplate template, string serviceName, string friendlyName, string engineType)
    {
        if (template == DatabaseTemplate.Custom)
        {
            return new DatabaseConfig
            {
                EngineType = engineType,
                FriendlyName = friendlyName,
                CliCommand = engineType == "docker"
                    ? ["docker", "exec", "-it", "{serviceName}", "sh"]
                    : [serviceName]
            };
        }

        var config = engineType == "docker"
            ? DockerTemplate(template)
            : SystemdTemplate(template);

        config.FriendlyName = friendlyName;
        config.EngineType = engineType;

        return config;
    }

    private static DatabaseConfig SystemdTemplate(DatabaseTemplate template) => template switch
    {
        DatabaseTemplate.PostgreSQL => EngineTemplates.Systemd("postgresql"),
        DatabaseTemplate.MariaDB => EngineTemplates.Systemd("mariadb"),
        DatabaseTemplate.MongoDB => EngineTemplates.Systemd("mongod"),
        DatabaseTemplate.Redis => EngineTemplates.Systemd("redis-server"),
        DatabaseTemplate.InfluxDB => EngineTemplates.Systemd("influxdb"),
        _ => new DatabaseConfig()
    };

    private static DatabaseConfig DockerTemplate(DatabaseTemplate template) => template switch
    {
        DatabaseTemplate.PostgreSQL => EngineTemplates.Docker("postgresql"),
        DatabaseTemplate.MariaDB => EngineTemplates.Docker("mariadb"),
        DatabaseTemplate.MongoDB => EngineTemplates.Docker("mongod"),
        DatabaseTemplate.Redis => EngineTemplates.Docker("redis"),
        DatabaseTemplate.InfluxDB => EngineTemplates.Docker("influxdb"),
        _ => new DatabaseConfig()
    };

    private enum DatabaseTemplate
    {
        PostgreSQL,
        MariaDB,
        MongoDB,
        Redis,
        InfluxDB,
        Custom
    }
}
