using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Principal;
using Microsoft.Win32;

namespace OmniHub.Platform;

/// <summary>Мелкие помощники для чтения состояния Windows. Ничего не меняют.</summary>
public static class Win
{
    public static bool IsElevated()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static bool InSsh() =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SSH_CONNECTION"))
        || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SSH_CLIENT"));

    public static string Sha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    public static object? ReadValue(RegistryHive hive, string subKey, string name, RegistryView view = RegistryView.Registry64)
    {
        using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view);
        using RegistryKey? key = baseKey.OpenSubKey(subKey);
        return key?.GetValue(name);
    }

    public static string? ReadString(RegistryHive hive, string subKey, string name, RegistryView view = RegistryView.Registry64) =>
        ReadValue(hive, subKey, name, view) as string;

    public static int? ReadInt(RegistryHive hive, string subKey, string name, RegistryView view = RegistryView.Registry64) =>
        ReadValue(hive, subKey, name, view) is int i ? i : null;

    /// <summary>Все строковые значения ключа: имя → значение.</summary>
    public static List<KeyValuePair<string, string>> ReadStrings(RegistryHive hive, string subKey, RegistryView view = RegistryView.Registry64)
    {
        var result = new List<KeyValuePair<string, string>>();
        using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view);
        using RegistryKey? key = baseKey.OpenSubKey(subKey);
        if (key is null)
        {
            return result;
        }
        foreach (string name in key.GetValueNames())
        {
            if (key.GetValue(name) is string value)
            {
                result.Add(new(name, value));
            }
        }
        return result;
    }

    public static bool KeyExists(RegistryHive hive, string subKey, RegistryView view = RegistryView.Registry64)
    {
        using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view);
        using RegistryKey? key = baseKey.OpenSubKey(subKey);
        return key is not null;
    }

    public sealed record ProcessEntry(int Pid, string Name, string? Path);

    /// <summary>Процессы, чьё имя начинается с prefix (без учёта регистра).</summary>
    public static List<ProcessEntry> Processes(string prefix)
    {
        var result = new List<ProcessEntry>();
        foreach (Process p in Process.GetProcesses())
        {
            using (p)
            {
                string name;
                try
                {
                    name = p.ProcessName;
                }
                catch (InvalidOperationException)
                {
                    continue;
                }
                if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                string? path = null;
                try
                {
                    path = p.MainModule?.FileName;
                }
                catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException)
                {
                    // Процесс другого пользователя или уже завершился: путь не узнать.
                }
                result.Add(new ProcessEntry(p.Id, name, path));
            }
        }
        return result;
    }

    public static string? ProcessName(int pid)
    {
        try
        {
            using Process p = Process.GetProcessById(pid);
            return p.ProcessName;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    public static string UserProfile => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
}
