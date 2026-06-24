using DbAutomatization.Core.Models;
using DbAutomatization.Core.Services;
using System.Text.Json;

namespace DbAutomatization.Infrastructure.Linux;

public class SystemdScanner : IDatabaseScanner
{
    private readonly IBashExecutor _bash;
    private readonly string _filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "databases.json");
    private Dictionary<string, string> _supportedDatabases = new();
    
   

    public SystemdScanner(IBashExecutor bash)
    {
        _bash = bash;
        LoadDatabasesFromFile();
    }
    

        private void LoadDatabasesFromFile()
        {
            if (!File.Exists(_filePath))
            {
                _supportedDatabases = new Dictionary<string, string>
                {
                    { "postgresql", "PostgreSQL" },
                    { "mariadb", "MariaDB" },
                    { "mongod", "MongoDB" },
                    { "redis-server", "Redis" }
                };
                SaveDatabasesToFile(); 
                return;
            }

            try
            {
                string jsonString = File.ReadAllText(_filePath);
                _supportedDatabases = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonString) 
                                    ?? new Dictionary<string, string>();
            }
            catch
            {
                _supportedDatabases = new Dictionary<string, string>();
            }
        }


        private void SaveDatabasesToFile()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonString = JsonSerializer.Serialize(_supportedDatabases, options);
            File.WriteAllText(_filePath, jsonString);
        }




        public void RegisterDatabase(string serviceName, string friendlyName){
            string key = serviceName.ToLower().Trim();
            if(!_supportedDatabases.ContainsKey(key)){
                _supportedDatabases.Add(key, friendlyName);
                SaveDatabasesToFile();
            }
        }

            public void UnregisterDatabase(string serviceName)
            {
                string key = serviceName.ToLower().Trim();
                if(_supportedDatabases.ContainsKey(key)){
                _supportedDatabases.Remove(key);
                    SaveDatabasesToFile();
                }
                }

        public async Task<bool> DoesServiceExistAsync(string serviceName)
            {
                string key = serviceName.ToLower().Trim();
                var unitFileStatus = await _bash.ExecuteAsync($"systemctl list-unit-files {key}.service");
                return !string.IsNullOrWhiteSpace(unitFileStatus) && unitFileStatus.Contains(key);
            }



        public async Task<(bool Success, string ErrorMessage)> InstallServiceViaAptAsync(string packageName, string serviceName)
            {
                Console.WriteLine($"\n[APT] Aktualizacja repozytoriów...");
                await _bash.ExecuteAsync("sudo apt update -y");

                Console.WriteLine($"[APT] Instalacja pakietu {packageName}... To może chwilę potrwać.");
                var installResult = await _bash.ExecuteAsync($"sudo apt install -y {packageName}");

                bool exists = await DoesServiceExistAsync(serviceName);
                if (exists)
                {
                    return (true, string.Empty);
                }

                return (false, installResult);
            }


        public async Task<List<DetectedDatabase>> ScanForDatabasesAsync()
            {
                var results = new List<DetectedDatabase>();

                foreach (var kvp in _supportedDatabases)
                {
                    string serviceName = kvp.Key;
                    string friendlyName = kvp.Value;

                    var unitFileStatus = await _bash.ExecuteAsync($"systemctl list-unit-files {serviceName}.service");
                    bool isInstalled = !string.IsNullOrWhiteSpace(unitFileStatus) && unitFileStatus.Contains(serviceName);

                    bool isRunning = false;
                    if (isInstalled)
                    {
                        var activeStatus = await _bash.ExecuteAsync($"systemctl is-active {serviceName}");
                        isRunning = activeStatus.Trim() == "active";
                    }

                    results.Add(new DetectedDatabase
                    {
                        Name = friendlyName,
                        ServiceName = serviceName,
                        IsInstalled = isInstalled,
                        IsRunning = isRunning
                    });
                }

                return results;
            }

}