using DbAutomatization.Core.Services;

namespace DbAutomatization.Cli;

public class ConsoleMenu
{
    private readonly DatabaseAppService _appService;
    private readonly IExternalEditorService _editor;
    private readonly IDatabaseConfigRepository _configRepo;

    public ConsoleMenu(DatabaseAppService appService, IExternalEditorService editor, IDatabaseConfigRepository configRepo)
    {
        _appService = appService;
        _editor = editor;
        _configRepo = configRepo;
    }

    public async Task RunAsync()
    {
        while (true)
        {
            Console.WriteLine("\n=== DbAutomatization ===");
            Console.WriteLine("[1] Skanuj bazy danych");
            Console.WriteLine("[2] Zarządzaj bazą");
            Console.WriteLine("[3] Dodaj nową bazę");

            Console.WriteLine("[4] Edytuj całą konfigurację w edytorze (nano/vim)");
            Console.WriteLine("[5] Dodaj bazę ręcznie (Zaawansowane)");
            Console.WriteLine("[6] Usuń bazę z listy");

            Console.WriteLine("[0] Wyjście");
            Console.Write("\nWybierz opcję: ");

            var choice = Console.ReadLine();

            if (choice == "0") break;

            switch (choice)
            {
                case "1":
                    await _appService.HandleScanAsync();
                    break;
                case "2":
                    await _appService.HandleManageDatabaseAsync();
                    break;
                case "3":
                    await ShowAddDatabaseSubMenuAsync();
                    break;
                case "4":
                    await _appService.HandleEditConfigAsync();
                    break;
                case "5":
                    await _appService.HandleAddDatabaseManualAsync();
                    break;
                case "6":
                    _appService.HandleRemoveDatabase();
                    break;
                default:
                    Console.WriteLine("Niepoprawny wybór.");
                    break;
            }
        }
    }

    private async Task ShowAddDatabaseSubMenuAsync()
    {
        while (true)
        {
            Console.WriteLine("\n--- Dodawanie nowej bazy ---");
            Console.WriteLine("[1] Baza systemowa (systemd)");
            Console.WriteLine("[2] Baza Dockerowa (kontener)");
            Console.WriteLine("[3] Zaawansowane (edytor nano/vim)");
            Console.WriteLine("[0] Powrót");
            Console.Write("\nWybierz opcję: ");

            var choice = Console.ReadLine()?.Trim();

            if (choice == "0") return;

            switch (choice)
            {
                case "1":
                    await _appService.HandleAddDatabaseWizardAsync();
                    return;
                case "2":
                    await _appService.HandleAddDatabaseDockerAsync();
                    return;
                case "3":
                    Console.WriteLine("\nOtwieranie edytora z konfiguracją. Dodaj nowy wpis ręcznie, zapisz i zamknij.");
                    bool success = await _editor.EditConfigInExternalEditorAsync();
                    if (success)
                    {
                        _configRepo.Reload();
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("[V] Konfiguracja zaktualizowana.");
                        Console.ResetColor();
                    }
                    return;
                default:
                    Console.WriteLine("Niepoprawny wybór.");
                    break;
            }
        }
    }
}
