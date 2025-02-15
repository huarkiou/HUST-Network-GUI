using System;
using System.ComponentModel;
using System.Net.NetworkInformation;
using System.Security.Principal;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Serilog;

namespace HustNetworkGui;

public partial class MainWindow : Window
{
    private bool _showFirstTime = true;
    private readonly AutoRun _autoRun;
    private readonly InternetActiveProbing _activeProbing;
    private bool _networkStatus;
    private readonly EventWaitHandle _inputWaitHandle = new AutoResetEvent(false);

    private TimeSpan _errorTimeout = TimeSpan.FromSeconds(10);
    private TimeSpan _successTimeout = TimeSpan.FromSeconds(60);
    private readonly BackgroundWorker _backgroundWorker;
    public bool IsShutdown = false;

    private bool NetworkStatus
    {
        get => _networkStatus;
        set
        {
            if (_networkStatus == value) return;
            _networkStatus = value;
            Dispatcher.UIThread.Post(() => StatusLabel.Content = "状态：" + (value ? "已连接" : "已断开"));
        }
    }

    public MainWindow()
    {
        InitializeComponent();
        Opened += (_, _) =>
        {
            if (!_showFirstTime) return;
            Hide(); // 这会导致设计器中窗口隐藏
            _showFirstTime = false;
        };
        Closing += (sender, eventArgs) =>
        {
            ((Window)sender!).Hide();
            eventArgs.Cancel = true;
        };

        _autoRun = new AutoRun(App.ProgramName);
        AutoRunToggleSwitch.IsChecked = _autoRun.RunOnBoot;

        _activeProbing = new InternetActiveProbing();
        ActiveProbingToggleSwitch.IsCancel = _activeProbing.EnableActiveProbing;
        if (OperatingSystem.IsWindows() &&
            !new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
        {
            ActiveProbingToggleSwitch.IsEnabled = false;
        }

        ErrorTimeoutControl.Value = AppConfiguration.Instance.Config.ErrorTimeout;
        SuccessTimeoutControl.Value = AppConfiguration.Instance.Config.SuccessTimeout;
        ErrorTimeoutControl.ValueChanged += (_, _) =>
        {
            _errorTimeout = TimeSpan.FromMilliseconds((int)ErrorTimeoutControl.Value.Value);
            AppConfiguration.Instance.Config.ErrorTimeout = (int)_errorTimeout.TotalMilliseconds;
        };
        SuccessTimeoutControl.ValueChanged += (_, _) =>
        {
            _successTimeout =
                TimeSpan.FromMilliseconds((int)SuccessTimeoutControl.Value.Value);
            AppConfiguration.Instance.Config.SuccessTimeout = (int)_successTimeout.TotalMilliseconds;
        };
        ErrorTimeoutControl.ValueChanged += (_, _) => SaveConfiguration();
        SuccessTimeoutControl.ValueChanged += (_, _) => SaveConfiguration();

        NetworkChange.NetworkAvailabilityChanged += (_, _) =>
        {
            Log.Information(NetworkInterface.GetIsNetworkAvailable()
                ? "Network Interface Available now"
                : "Network Interface Not Available");
        };

        _backgroundWorker = new BackgroundWorker();
        _backgroundWorker.DoWork += (_, _) =>
        {
            var firstCircle = true;
            var reinputPassword = false;
            while (!IsShutdown)
            {
                try
                {
                    // 读取账号密码
                    while (reinputPassword || AppConfiguration.Instance.Config.Username is null ||
                           AppConfiguration.Instance.Config.Password is null)
                    {
                        Dispatcher.UIThread.Post(async void () =>
                        {
                            try
                            {
                                var inputWindow = new InputInfoWindow();
                                if (AppConfiguration.Instance.Config.Password is not null)
                                {
                                    inputWindow.PasswordTextBlock.Text = AppConfiguration.Instance.Config.Password;
                                }

                                if (AppConfiguration.Instance.Config.Username is not null)
                                {
                                    inputWindow.UsernameTextBlock.Text = AppConfiguration.Instance.Config.Username;
                                }

                                Show();
                                var result = await inputWindow.ShowDialog<(string, string)>(this);

                                AppConfiguration.Instance.Config.Username = result.Item1;
                                AppConfiguration.Instance.Config.Password = result.Item2;
                                AppConfiguration.Instance.Save();
                                reinputPassword = false;
                            }
                            catch (Exception e)
                            {
                                Log.Error(e, "Error reading input from user");
                            }
                            finally
                            {
                                _inputWaitHandle.Set();
                            }
                        });
                        Log.Information("Waiting for user input");
                        _inputWaitHandle.WaitOne();
                    }

                    var hustNetworkController = new HustNetworkController(AppConfiguration.Instance.Config.Username!,
                        AppConfiguration.Instance.Config.Password!);

                    // 访问eportal链接获取url
                    var originalUrl = hustNetworkController.GetVerificationUrl();
                    if (originalUrl is null)
                    {
                        // 检测网络连通状态
                        NetworkStatus = HustNetworkController.Pong();
                        if (!NetworkStatus)
                        {
                            Log.Information("Network disconnected");
                        }
                        else if (firstCircle)
                        {
                            Log.Information("Network has already connected");
                        }

                        firstCircle = false;
                    }
                    else
                    {
                        // 登录校园网
                        NetworkStatus = hustNetworkController.SendLoginRequest(originalUrl);
                        if (NetworkStatus)
                        {
                            Log.Information("Connected to Hust Campus Network Successfully");
                        }
                        else
                        {
                            Log.Warning("Fail to connect to Hust Campus Network, maybe invalid username or password");
                            reinputPassword = true;
                        }
                    }
                }
                catch (Exception e)
                {
                    NetworkStatus = false;
                    Log.Error(e, "Error when connect to Hust Campus Network");
                }

                if (!reinputPassword) Thread.Sleep(NetworkStatus ? _successTimeout : _errorTimeout);
            }
        };
        _backgroundWorker.RunWorkerAsync();
    }

    private void AutoRunToggleSwitch_OnClick(object? sender, RoutedEventArgs e)
    {
        var cb = (ToggleSwitch)sender!;
        _autoRun.SetStartupOnBoot(cb.IsChecked!.Value);
    }

    private void ActiveProbingToggleSwitch_OnClickToggleSwitch_OnClick(object? sender, RoutedEventArgs e)
    {
        var cb = (ToggleSwitch)sender!;
        _activeProbing.SetActiveProbing(cb.IsChecked!.Value);
    }

    private static void SaveConfiguration()
    {
        AppConfiguration.Instance.Save();
    }
}