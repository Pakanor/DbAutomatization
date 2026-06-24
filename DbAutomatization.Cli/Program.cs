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
    .AddTransient<DatabaseAppService>() 
    .AddTransient<ConsoleMenu>()       
    .BuildServiceProvider();

    var menu = serviceProvider.GetRequiredService<ConsoleMenu>();
    await menu.RunAsync();
    }

    
}