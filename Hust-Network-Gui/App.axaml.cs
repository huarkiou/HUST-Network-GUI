using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Serilog;
using Serilog.Events;

namespace HustNetworkGui;

public class App : Application
{
    public const string ProgramName = "Hust-Network-Gui";

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // configure logger
        Log.Logger = new LoggerConfiguration()
#if DEBUG
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Debug)
#else
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
#endif
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(Path.Combine(AppContext.BaseDirectory, "logs/.log"), rollingInterval: RollingInterval.Month,
                rollOnFileSizeLimit: true)
            .CreateLogger();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // check global mutex
            Mutex mutex = new Mutex(true, "Global\\" + ProgramName, out var createdNew);
            if (!createdNew)
            {
                Log.Information("Already run an instance");
                Environment.Exit(0);
                return;
            }

            desktop.Exit += (_, _) => mutex.ReleaseMutex();

            desktop.Startup += (_, _) => Log.Information("Application is starting up");
            desktop.Exit += (_, _) => Log.Information("Application is shutting down");
            desktop.ShutdownRequested += Quit_OnClick;
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // create main window
            desktop.MainWindow = new MainWindow();
        }

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log.Write(LogEventLevel.Error, (Exception)e.ExceptionObject, "Unhandled exception");
        TaskScheduler.UnobservedTaskException +=
            (_, e) => Log.Write(LogEventLevel.Error, e.Exception, "Unobserved task exception");


        base.OnFrameworkInitializationCompleted();
    }

    private void Quit_OnClick(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            (desktop.MainWindow as MainWindow)!.IsShutdown = true;
            desktop.Shutdown();
        }

        Log.CloseAndFlush();
    }

    private void ChangeVisible_OnClick(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow!.IsVisible)
                desktop.MainWindow!.Hide();
            else
                desktop.MainWindow!.Show();
        }
    }
}