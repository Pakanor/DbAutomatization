using System.Text.Json;
using DbAutomatization.Core.Models;
using DbAutomatization.Core.Services;

namespace DbAutomatization.Infrastructure.Linux;

public class ExternalEditorService : IExternalEditorService
{
    private readonly IProcessExecutor _process;
    private readonly IDatabaseConfigRepository _configRepo;

    public ExternalEditorService(IProcessExecutor process, IDatabaseConfigRepository configRepo)
    {
        _process = process;
        _configRepo = configRepo;
    }

    public async Task<bool> EditConfigInExternalEditorAsync(CancellationToken cancellationToken = default)
    {
        string? editor = DetectEditor();
        if (editor == null)
        {
            Console.WriteLine("[!] Nie znaleziono edytora (sprawdź $EDITOR, $VISUAL, nano, vim, vi).");
            return false;
        }

        string tempFile = Path.GetTempFileName();
        try
        {
            var configs = _configRepo.GetAll();
            string json = JsonSerializer.Serialize(configs, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(tempFile, json, cancellationToken);

            while (true)
            {
                Console.WriteLine($"\nOtwieranie edytora '{editor}'... Zapisz i zamknij, aby kontynuować.");
                await _process.ExecuteInteractiveAsync(editor, [tempFile], cancellationToken);

                string editedJson = await File.ReadAllTextAsync(tempFile, cancellationToken);

                try
                {
                    var parsed = JsonSerializer.Deserialize<Dictionary<string, DatabaseConfig>>(editedJson);
                    if (parsed == null)
                        throw new JsonException("Deserializacja zwróciła null.");

                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string finalJson = JsonSerializer.Serialize(parsed, options);
                    string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "databases.json");
                    await File.WriteAllTextAsync(configPath, finalJson, cancellationToken);

                    _configRepo.Reload();

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("[V] Konfiguracja zaktualizowana pomyślnie.");
                    Console.ResetColor();
                    return true;
                }
                catch (JsonException ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\n[X] Błąd walidacji JSON: {ex.Message}");
                    Console.ResetColor();

                    Console.WriteLine("\n--- Opcje ---");
                    Console.WriteLine("[1] Otwórz edytor ponownie i popraw");
                    Console.WriteLine("[2] Anuluj zmiany");
                    Console.Write("\nWybór: ");

                    var choice = Console.ReadLine()?.Trim();
                    if (choice == "2")
                    {
                        Console.WriteLine("Anulowano zmiany w konfiguracji.");
                        return false;
                    }
                }
            }
        }
        finally
        {
            TryDeleteFile(tempFile);
        }
    }

    private static string? DetectEditor()
    {
        string? editor = Environment.GetEnvironmentVariable("VISUAL")
                      ?? Environment.GetEnvironmentVariable("EDITOR");

        if (!string.IsNullOrEmpty(editor))
            return editor;

        foreach (var candidate in new[] { "nano", "vim", "vi" })
        {
            if (File.Exists($"/usr/bin/{candidate}") || File.Exists($"/bin/{candidate}"))
                return candidate;
        }

        return null;
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
