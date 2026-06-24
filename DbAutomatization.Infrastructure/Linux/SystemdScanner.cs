using DbAutomatization.Core.Models;
using DbAutomatization.Core.Services;
using System.Text.Json;

namespace DbAutomatization.Infrastructure.Linux;

public class SystemdScanner : IDatabaseScanner
{
    private readonly IBashExecutor _bash;
    private readonly string _filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "databases.json");
    private Dictionary<string, DatabaseConfig> _supportedDatabases = new();
    
   

    public SystemdScanner(IBashExecutor bash)
    {
        _bash = bash;
        LoadDatabasesFromFile();
    }


        private void LoadDatabasesFromFile()
        {
            if (!File.Exists(_filePath))
            {
                _supportedDatabases = new Dictionary<string, DatabaseConfig>
                {
                    { 
                        "postgresql", 
                        new DatabaseConfig { FriendlyName = "PostgreSQL", CliCommand = "sudo -u postgres psql" } 
                    },
                    { 
                        "mariadb", 
                        new DatabaseConfig { FriendlyName = "MariaDB", CliCommand = "mariadb" } 
                    },
                    { 
                        "mongod", 
                        new DatabaseConfig { FriendlyName = "MongoDB", CliCommand = "mongosh" } 
                    },
                    { 
                        "redis-server", 
                        new DatabaseConfig { FriendlyName = "Redis", CliCommand = "redis-cli" } 
                    }
                };
                SaveDatabasesToFile(); 
                return;
            }

            try
            {
                string jsonString = File.ReadAllText(_filePath);
                _supportedDatabases = JsonSerializer.Deserialize<Dictionary<string, DatabaseConfig>>(jsonString) 
                                    ?? new Dictionary<string, DatabaseConfig>();
            }
            catch
            {
                _supportedDatabases = new Dictionary<string, DatabaseConfig>();
            }
        }


        private void SaveDatabasesToFile()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonString = JsonSerializer.Serialize(_supportedDatabases, options);
            File.WriteAllText(_filePath, jsonString);
        }




        public void RegisterDatabase(string serviceName, string friendlyName, string cliCommand)
            {
                string key = serviceName.ToLower().Trim();
                if (!_supportedDatabases.ContainsKey(key))
                {
                    _supportedDatabases.Add(key, new DatabaseConfig 
                    { 
                        FriendlyName = friendlyName, 
                        CliCommand = cliCommand 
                    });
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
            
            var whichStatus = await _bash.ExecuteAsync($"which {key}");
            
            return !string.IsNullOrWhiteSpace(whichStatus) && !whichStatus.Contains("not found");
        }



        public async Task<(bool Success, string ErrorMessage)> InstallServiceViaAptAsync(string packageName, string serviceName)
        {
            Console.WriteLine($"\n[APT] Aktualizacja repozytoriów...");
            await _bash.ExecuteAsync("sudo apt update -y");

            Console.WriteLine($"[APT] Instalacja pakietu {packageName}... To może chwilę potrwać.");
            var installResult = await _bash.ExecuteAsync($"sudo apt install -y {packageName}");

           
            bool aptSuccess = installResult.Contains("Instalowane:") || 
                            installResult.Contains("jest już w najnowszej wersji") ||
                            installResult.Contains("is already the newest version");

            if (aptSuccess)
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
                    string friendlyName = kvp.Value.FriendlyName;
                    string fullCommand = kvp.Value.CliCommand;

                  
                    string binaryToFind = fullCommand.Split(' ').Last(); 

                    var whichStatus = await _bash.ExecuteAsync($"which {binaryToFind}");
                    
                    bool isInstalled = !string.IsNullOrWhiteSpace(whichStatus) && !whichStatus.Contains("not found");

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