using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32;

static class PlatformUtils
{
    /// <summary>
    /// Try to locate the Steam game installation directory for the given app id
    /// on the current platform. Returns null if not found.
    /// </summary>
    public static string? LocateGameDirByAppId(int appId)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows: try registry (as original code did)
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App {appId}");
                    var install = key?.GetValue("InstallLocation") as string;
                    if (!string.IsNullOrWhiteSpace(install)) return install;
                }
                catch { /* ignore registry access errors */ }

                // Fallback: try common steam path from environment
                var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                if (!string.IsNullOrEmpty(programFiles))
                {
                    var candidate = Path.Combine(programFiles, "Steam", "steamapps", "common", "The Scroll Of Taiwu");
                    if (Directory.Exists(candidate)) return Path.GetFullPath(candidate);
                }
            }
            else
            {
                // macOS / Linux: common Steam path under user's Library/Application Support
                var home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                var steamAppsCommonPaths = new[]
                {
                    Path.Combine(home, "Library", "Application Support", "Steam", "steamapps", "common", "The Scroll Of Taiwu"),
                    Path.Combine(home, "Library", "Application Support", "Steam", "steamapps", "common", "太吾绘卷"),
                    Path.Combine(home, "Library", "Application Support", "Steam", "steamapps", "common", "太吾绘卷：天幕心帷"),
                };
                foreach (var p in steamAppsCommonPaths)
                    if (Directory.Exists(p)) return Path.GetFullPath(p);

                // Try reading appmanifest_*.acf in steamapps for installdir
                var steamapps = Path.Combine(home, "Library", "Application Support", "Steam", "steamapps");
                var manifest = Path.Combine(steamapps, $"appmanifest_{appId}.acf");
                if (File.Exists(manifest))
                {
                    foreach (var line in File.ReadAllLines(manifest))
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("\"installdir\""))
                        {
                            var m = Regex.Match(trimmed, "\"(.*)\"");
                            if (m.Success)
                            {
                                var dir = m.Groups[1].Value;
                                var candidate = Path.Combine(steamapps, "common", dir);
                                if (Directory.Exists(candidate)) return Path.GetFullPath(candidate);
                            }
                        }
                    }
                }

                // On Linux (Proton) Steam location might be different; also try ~/.steam or ~/.local/share
                var linuxCandidates = new[]
                {
                    Path.Combine(home, ".steam", "steam", "steamapps", "common", "The Scroll Of Taiwu"),
                    Path.Combine(home, ".local", "share", "Steam", "steamapps", "common", "The Scroll Of Taiwu"),
                };
                foreach (var p in linuxCandidates)
                    if (Directory.Exists(p)) return Path.GetFullPath(p);
            }
        }
        catch { /* swallow all errors and return null */ }

        return null;
    }
}
