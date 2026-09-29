using System;
using Microsoft.Win32;

namespace UsetimeManager.Services;

/// <summary>开机自启（HKCU\...\Run）</summary>
public static class AutostartService
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "UsetimeManager";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            var value = key?.GetValue(ValueName) as string;
            return !string.IsNullOrEmpty(value);
        }
        catch
        {
            return false;
        }
    }

    public static bool Enable()
    {
        try
        {
            var exe = Environment.ProcessPath
                      ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
                      ?? "";
            if (string.IsNullOrEmpty(exe)) return false;

            using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
            key?.SetValue(ValueName, $"\"{exe}\"");
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool Disable()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
