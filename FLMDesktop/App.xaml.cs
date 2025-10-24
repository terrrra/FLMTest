using System;
using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Serilog;
using FLMDesktop.Infrastructure;   // AppServices

namespace FLMDesktop
{
    public partial class App : Application
    {
        public static IConfiguration Configuration { get; private set; } = default!;

        public App()
        {
            // 1) Build configuration from the executable folder
            Configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // 2) Configure Serilog from appsettings.json
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(Configuration)
                .Enrich.FromLogContext()
                .CreateLogger();

            // 3) Capture unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                Log.Fatal((Exception)e.ExceptionObject, "Unhandled non-UI exception");
            };
            this.DispatcherUnhandledException += (s, e) =>
            {
                Log.Error(e.Exception, "Unhandled UI exception");
                MessageBox.Show("An unexpected error occurred. See Logs for details.",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true; // optional: keep running
            };
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // 4) Get connection string (must match key in appsettings.json)
                var conn = Configuration.GetConnectionString("DefaultConnection");

                if (string.IsNullOrWhiteSpace(conn))
                {
                    Log.Fatal("Missing/empty 'DefaultConnection'. BaseDir={Base}", AppContext.BaseDirectory);
                    MessageBox.Show("Connection string missing or invalid in appsettings.json",
                                    "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown();
                    return;
                }

                // 5) Initialize global services
                InitializeServices.Init(conn);
                Log.Information("AppServices initialized.");

                // (Optional) If you want to auto-create DB/tables in dev, uncomment:
                // await FLMDesktop.Data.DbBootstrap.EnsureReadyAsync(conn);

                // If you create/show MainWindow here in your app, do it like this:
                 var shell = new MainWindow();
                 shell.Show();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Fatal error during startup");
                MessageBox.Show($"Fatal startup error: {ex.GetBaseException().Message}",
                                "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.Information("Application exiting");
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }
}
