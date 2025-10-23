using FLMDesktop.Data;
using FLMDesktop.Infrastructure;
using Microsoft.Extensions.Configuration;
using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;

namespace FLMDesktop
{
    // Here I have ensured that all partial definitions of the 'App' class specify the same base class.
    public partial class App : Application
    {
        public static IConfiguration Configuration { get; private set; } = default!;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var conn = Configuration.GetConnectionString("Default")!;

            // 1) Ensure DB + tables exist (waits for container if needed)
            await DbBootstrap.EnsureReadyAsync(conn);

            // 2) Make services available app-wide
            AppServices.Init(conn);
        }
    }
}