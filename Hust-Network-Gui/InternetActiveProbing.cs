using System;
using System.Security;
using Microsoft.Win32;
using Serilog;

namespace HustNetworkGui;

internal class InternetActiveProbing
{
    private const string RegistryKeyPath = @"SYSTEM\CurrentControlSet\Services\NlaSvc\Parameters\Internet";
    private const string KeyName = "EnableActiveProbing";
    private readonly RegistryKey? _asKey;

    public InternetActiveProbing()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                _asKey = Registry.LocalMachine.OpenSubKey(RegistryKeyPath, true);
                if (_asKey == null) Log.Warning("Unable to open Registry key: HKEY_LOCAL_MACHINE\\" + RegistryKeyPath);
            }
            catch (SecurityException)
            {
            }
        }
        else
        {
            Log.Warning("Unable to set internet active probing on this system due to not implemented");
        }

        CheckEnableActiveProbing();
    }

    public bool EnableActiveProbing { get; private set; }

    public void SetActiveProbing(bool enable)
    {
        CheckEnableActiveProbing();
        if (enable == EnableActiveProbing) return;

        if (OperatingSystem.IsWindows())
        {
            _asKey?.SetValue(KeyName, enable ? 1 : 0);
        }

        EnableActiveProbing = enable;
    }

    private void CheckEnableActiveProbing()
    {
        if (OperatingSystem.IsWindows())
        {
            EnableActiveProbing = (int)(_asKey?.GetValue(KeyName) ?? 1) == 1;
        }
    }

    ~InternetActiveProbing()
    {
        if (OperatingSystem.IsWindows())
        {
            _asKey?.Close();
            _asKey?.Dispose();
        }
    }
}