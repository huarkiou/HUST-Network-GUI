using System;
using System.ComponentModel;
using System.Net.NetworkInformation;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
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
    private readonly EventWaitHandle _inputWaitHandle = new AutoResetEvent(false);
    private TimeSpan _errorTimeout = TimeSpan.FromSeconds(10);
    private TimeSpan _successTimeout = TimeSpan.FromSeconds(60);
    private readonly BackgroundWorker _backgroundWorker;
    public bool IsShutdown = false;

    private bool NetworkStatus
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
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

        NetworkChange.NetworkAvailabilityChanged += (_, _) =>
        {
            Log.Information(NetworkInterface.GetIsNetworkAvailable()
                ? "Network Interface Available now"
                : "Network Interface Not Available");
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

        _backgroundWorker = new BackgroundWorker();
        _backgroundWorker.DoWork += async (_, _) =>
        {
            var hustNetworkController = new HustNetworkController(AppConfiguration.Instance.Config.Username,
                AppConfiguration.Instance.Config.Password);
            var firstCircle = true;
            var reinputPassword = false;
            while (!IsShutdown)
            {
                try
                {
                    if (!firstCircle)
                    {
                        // 检查网络连通性ping
                        NetworkStatus = HustNetworkController.CheckInternetAccess();
                    }

                    if (!NetworkStatus || firstCircle)
                    {
                        // 读取账号密码
                        while (reinputPassword || hustNetworkController.Username is null ||
                               hustNetworkController.Password is null)
                        {
                            Log.Information("Waiting for user input");
                            Dispatcher.UIThread.Post(async void () =>
                            {
                                try
                                {
                                    var inputWindow = new InputInfoWindow(hustNetworkController.Username,
                                        hustNetworkController.Password, hustNetworkController.Message);
                                    Show();
                                    (string? usernameInput, string? passwordInput) =
                                        await inputWindow.ShowDialog<(string?, string?)>(this);
                                    if (usernameInput?.Length > 0 || passwordInput?.Length > 0)
                                    {
                                        hustNetworkController.Username = usernameInput;
                                        hustNetworkController.Password = passwordInput;
                                    }

                                    Close();
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
                            _inputWaitHandle.WaitOne();
                        }

                        // 访问eportal链接获取url
                        var originalUrl = await hustNetworkController.GetVerificationUrlAsync();
                        if (originalUrl is null)
                        {
                            // 检测网络连通状态
                            NetworkStatus = HustNetworkController.CheckInternetAccess();
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
                            NetworkStatus = await hustNetworkController.SendLoginRequestAsync(originalUrl);
                            if (NetworkStatus)
                            {
                                Log.Information("Connected to Hust Campus Network Successfully");
                                if (AppConfiguration.Instance.Config.Username != hustNetworkController.Username ||
                                    AppConfiguration.Instance.Config.Password != hustNetworkController.Password)
                                {
                                    AppConfiguration.Instance.Config.Username = hustNetworkController.Username;
                                    AppConfiguration.Instance.Config.Password = hustNetworkController.Password;
                                    AppConfiguration.Instance.Save();
                                }
                            }
                            else
                            {
                                Log.Warning(
                                    "Fail to connect to Hust Campus Network, maybe invalid username or password");
                                reinputPassword = true;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    NetworkStatus = false;
                    Log.Error(e, "Error when connect to Hust Campus Network");
                }

                if (!reinputPassword) await Task.Delay(NetworkStatus ? _successTimeout : _errorTimeout);
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