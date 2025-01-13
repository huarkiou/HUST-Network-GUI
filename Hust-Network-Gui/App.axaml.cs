using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Serilog;
using Serilog.Events;

namespace HustNetworkGui;

public partial class App : Application
{
    public const string ProgramName = "Hust-Network-Gui";

    private Mutex? _mutex;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // check global mutex
            _mutex = new Mutex(true, "Global\\" + ProgramName, out var createdNew);
            if (!createdNew || _mutex == null)
            {
                desktop.Shutdown();
                return;
            }

            desktop.Exit += (_, _) => _mutex.ReleaseMutex();
            desktop.ShutdownRequested += Quit_OnClick;
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // create window
            desktop.MainWindow = new MainWindow();

            // log
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
                .WriteTo.File(Path.Combine(AppContext.BaseDirectory, "logs/.log"), rollingInterval: RollingInterval.Day,
                    rollOnFileSizeLimit: true)
                .CreateLogger();
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                Log.Write(LogEventLevel.Error, (Exception)e.ExceptionObject, "Unhandled exception");
            TaskScheduler.UnobservedTaskException +=
                (_, e) => Log.Write(LogEventLevel.Error, e.Exception, "Unobserved task exception");
            desktop.Startup += (_, _) => Log.Information("Application starting up");
            desktop.Exit += (_, _) => Log.Information("Application is shutting down");
        }


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