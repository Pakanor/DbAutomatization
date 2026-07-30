using Microsoft.Extensions.DependencyInjection;
using DbAutomatization.Core.Exceptions;
using DbAutomatization.Core.Services;
using DbAutomatization.Infrastructure.Linux;

namespace DbAutomatization.Cli;

class Program
{
    static async Task<int> Main(string[] args)
    {
        if (!await CheckSudoAsync())
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[X] DbAutomatization wymaga uprawnień root (passwordless sudo).");
            Console.ResetColor();
            Console.WriteLine("Uruchom: sudo dotnet run");
            Console.WriteLine("Lub skonfiguruj passwordless sudo w /etc/sudoers");
            return 1;
        }

        try
        {
            var serviceProvider = new ServiceCollection()
                .AddSingleton<IProcessExecutor, ProcessExecutor>()
                .AddSingleton<IDatabaseConfigRepository, DatabaseConfigRepository>()
                .AddSingleton<IBackupRetentionManager, BackupRetentionManager>()
                .AddSingleton<ITemplateAutoDiscoveryService, TemplateAutoDiscoveryService>()
                .AddTransient<IAptPackageManager, AptPackageManager>()
                .AddTransient<IBackupService, BackupService>()
                .AddTransient<IRestoreService, RestoreService>()
                .AddTransient<IExternalEditorService, ExternalEditorService>()
                .AddTransient<DatabaseWizardService>()
                .AddTransient<IDatabaseScanner, SystemdScanner>()
                .AddTransient<IServiceManager, SystemdServiceManager>()
                .AddTransient<DatabaseAppService>()
                .AddTransient<ConsoleMenu>()
                .BuildServiceProvider();

            var autoDiscovery = serviceProvider.GetRequiredService<ITemplateAutoDiscoveryService>();
            await autoDiscovery.AutoDiscoverAsync();

            var menu = serviceProvider.GetRequiredService<ConsoleMenu>();
            await menu.RunAsync();
            return 0;
        }
        catch (DatabaseConfigurationException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\n[X] Błąd konfiguracji:");
            Console.ResetColor();
            Console.WriteLine($"  {ex.Message}");
            Console.WriteLine($"  Szczegóły: {ex.InnerException?.Message}");

            Console.WriteLine("\n--- Opcje naprawy ---");
            Console.WriteLine("[1] Przywróć domyślną konfigurację (usunie obecny plik i stworzy nowy)");
            Console.WriteLine("[2] Wyjdź z programu");
            Console.Write("\nWybierz opcję: ");

            var choice = Console.ReadLine()?.Trim();
            if (choice == "1")
            {
                try
                {
                    string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "databases.json");
                    if (File.Exists(configPath))
                        File.Delete(configPath);

                    Console.WriteLine("Przywracanie domyślnej konfiguracji...");
                    var freshRepo = new DatabaseConfigRepository();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("[V] Konfiguracja została przywrócona. Uruchom program ponownie.");
                    Console.ResetColor();
                }
                catch (Exception restoreEx)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[X] Nie udało się przywrócić konfiguracji: {restoreEx.Message}");
                    Console.ResetColor();
                }
            }

            return 1;
        }
    }

    private static async Task<bool> CheckSudoAsync()
    {
        try
        {
            var process = new ProcessExecutor();
            var result = await process.ExecuteAsync("sudo", ["-n", "true"], timeoutSeconds: 5);
            return result.Success;
        }
        catch
        {
            return false;
        }
    }
}
