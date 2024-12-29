using System;
using Microsoft.Win32;
using Serilog;

namespace HustNetworkGui;

internal class AutoRun
{
    private const string RegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private readonly RegistryKey? _asKey;
    private readonly string _executableFilePath;
    private readonly string _programName;

    public AutoRun(string programName)
    {
        _programName = programName;
        _executableFilePath = Environment.ProcessPath ?? "";
        if (OperatingSystem.IsWindows())
        {
            _asKey = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
            if (_asKey == null) Log.Warning("Unable to open Registry key");
        }
        else
        {
            Log.Warning("Unable to set autorun on this system due to not implemented");
        }

        CheckRunOnBoot();
    }

    public bool RunOnBoot { get; private set; }

    ~AutoRun()
    {
        if (OperatingSystem.IsWindows())
        {
            _asKey?.Close();
        }
    }

    private void CheckRunOnBoot()
    {
        if (OperatingSystem.IsWindows())
        {
            RunOnBoot = _asKey?.GetValue(_programName) != null;
        }
    }

    public void SetStartupOnBoot(bool enable)
    {
        CheckRunOnBoot();
        if (enable == RunOnBoot) return;

        if (OperatingSystem.IsWindows())
        {
            if (enable)
                _asKey?.SetValue(_programName, _executableFilePath);
            else
                _asKey?.DeleteValue(_programName);
        }

        RunOnBoot = enable;
    }
}