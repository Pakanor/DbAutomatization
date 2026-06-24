 
namespace DbAutomatization.Cli;

public class ConsoleMenu{

    private readonly DatabaseAppService _appService;

    public ConsoleMenu(DatabaseAppService appService){
        _appService = appService;
    }

    public async Task RunAsync(){
           while (true)
        {
            Console.WriteLine("\n[1] Skanuj bazy danych");
            Console.WriteLine("[2] Zarządzaj bazą");
            Console.WriteLine("[3] Dodaj nową bazę do listy");
            Console.WriteLine("[4] Usun bazę z listy");

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
                    await _appService.HandleAddDatabaseAsync();
                    break;
                case "4":
                    _appService.HandleRemoveDatabase();
                    break;
                default:
                    Console.WriteLine("Niepoprawny wybór.");
                    break;
            }
        }






    }



}