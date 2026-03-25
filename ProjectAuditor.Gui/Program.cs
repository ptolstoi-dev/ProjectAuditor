using System;
using Microsoft.Extensions.DependencyInjection;
using Photino.Blazor;

namespace ProjectAuditor.Gui
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            var appBuilder = PhotinoBlazorAppBuilder.CreateDefault(args);

            appBuilder.Services
                .AddLogging()
                .AddSingleton<ProjectAuditor.Core.Services.DotNetCliService>()
                .AddSingleton<ProjectAuditor.Core.Parsers.ProjectParser>()
                .AddSingleton<ProjectAuditor.Core.Services.AuditorEngine>()
                .AddSingleton<ProjectAuditor.Core.Services.ISettingsService, ProjectAuditor.Core.Services.SettingsService>();

            // register root component and selector
            appBuilder.RootComponents.Add<App>("app");

            var app = appBuilder.Build();

            // customize window
            app.MainWindow
                .SetIconFile("favicon.ico")
                .SetTitle("ProjectAuditor.Gui")
                .SetSize(1140, 720);

            AppDomain.CurrentDomain.UnhandledException += (sender, error) =>
            {
                app.MainWindow.ShowMessage("Fatal exception", error.ExceptionObject.ToString());
            };

            app.Run();
        }
    }
}