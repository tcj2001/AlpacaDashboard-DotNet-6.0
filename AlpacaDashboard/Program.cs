global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Hosting;
global using Serilog;

namespace AlpacaDashboard;

internal class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>

    [STAThread]
    static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddUserSecrets<Program>()
            .Build();

        var builder = new HostBuilder()
               .ConfigureServices((hostContext, services) =>
               {
                   services.AddScoped<AlpacaDashboard>();
                   services.Configure<MySettings>(configuration.GetSection("MySettings"));
                   services.Configure<PaperKey>(configuration.GetSection("PaperKey"));
                   services.Configure<LiveKey>(configuration.GetSection("LiveKey"));

                   //Add Serilog
                   Log.Logger = new LoggerConfiguration()
                       .ReadFrom.Configuration(configuration)
                       .CreateLogger();

                   services.AddLogging(x =>
                   {
                       x.SetMinimumLevel(LogLevel.Information);
                       x.AddSerilog(logger: Log.Logger, dispose: true);
                   });

               });

        var host = builder.Build();

        using (var serviceScope = host.Services.CreateScope())
        {
            var services = serviceScope.ServiceProvider;
            var alpacaDashboard = services.GetRequiredService<AlpacaDashboard>();
            Application.Run(alpacaDashboard);
        }
    }
}
