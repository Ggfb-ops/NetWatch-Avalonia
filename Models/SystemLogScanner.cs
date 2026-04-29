using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace NetWatch.Models;

public static class SystemLogScanner
{
    private static readonly HashSet<string> ReadableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".log", ".txt", ".json", ".csv", ".xml", ".yaml", ".yml", ".md",
        ".err", ".out", ".warn", ".info", ".debug", ".trace",
        ".syslog", ".auth", ".kern", ".mail", ".daemon",
        ".conf", ".cfg", ".ini", ".toml",
        ".1", ".2", ".3", ".4", ".5", ".6", ".7", ".8", ".9",
        ".gz", ".bz2", ".xz", ".zip", ".old", ".bak", ".orig",
        ".evtx", ".etl"
    };

    private static readonly HashSet<string> SkipDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", ".git", ".svn", "__pycache__", "bin", "obj",
        "packages", "venv", ".venv", "dist", "build"
    };

    private static readonly Dictionary<string, string> CategoryMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["winevt"] = "Event Log",
        ["Logs"] = "System",
        ["logfiles"] = "Services",
        ["LogFiles"] = "IIS",
        ["httpd"] = "Apache",
        ["nginx"] = "Nginx",
        ["mysql"] = "MySQL",
        ["postgresql"] = "PostgreSQL",
        ["docker"] = "Docker",
        ["containers"] = "Docker",
        ["apt"] = "APT",
        ["dpkg"] = "DPKG",
        ["yum"] = "YUM",
        ["dnf"] = "DNF",
        ["pacman"] = "Pacman",
        ["journal"] = "Journal",
        ["syslog"] = "Syslog",
        ["auth"] = "Auth",
        ["kern"] = "Kernel",
        ["mail"] = "Mail",
        ["cups"] = "Print",
        ["samba"] = "Samba",
        ["audit"] = "Audit",
        ["secure"] = "Security",
        ["installer"] = "Installer",
        ["DISM"] = "DISM",
        ["CBS"] = "CBS",
        ["Panther"] = "Setup",
        ["WindowsUpdate"] = "Updates",
        ["Firewall"] = "Firewall",
        ["Cluster"] = "Cluster",
        ["TaskScheduler"] = "Scheduler"
    };

    public static List<SystemLogFile> Scan()
    {
        var results = new List<SystemLogFile>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            ScanWindows(results, seen);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            ScanLinux(results, seen);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            ScanMac(results, seen);

        return results
            .OrderByDescending(f => f.IsReadable)
            .ThenBy(f => f.Category)
            .ThenBy(f => f.Name)
            .Take(200)
            .ToList();
    }

    private static void ScanWindows(List<SystemLogFile> results, HashSet<string> seen)
    {
        var winDir = Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows";
        var paths = new[]
        {
            Path.Combine(winDir, "Logs"),
            Path.Combine(winDir, "System32", "winevt", "Logs"),
            Path.Combine(winDir, "System32", "LogFiles"),
            Path.Combine(winDir, "System32", "config"),
            Path.Combine(winDir, "Panther"),
            Path.Combine(winDir, "Temp"),
            Path.Combine(winDir, "SoftwareDistribution"),
        };

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        var extraPaths = new[]
        {
            Path.Combine(programData, "Microsoft", "Windows", "Logs"),
            Path.Combine(localAppData, "Microsoft", "Windows", "Logs"),
            Path.Combine(localAppData, "CrashDumps"),
            Path.Combine(programData, "chocolatey", "logs"),
            Path.Combine(programData, "Docker"),
            Path.Combine(programData, "MongoDB"),
            Path.Combine(programData, "MySQL"),
            Path.Combine(programData, "PostgreSQL"),
            Path.Combine(programData, "nginx", "logs"),
            Path.Combine(programData, "Apache24", "logs"),
            Path.Combine(localAppData, "npm-cache", "_logs"),
        };

        foreach (var p in paths.Concat(extraPaths))
            ScanDirectory(p, results, seen);
    }

    private static void ScanLinux(List<SystemLogFile> results, HashSet<string> seen)
    {
        var paths = new[]
        {
            "/var/log",
            "/var/log/nginx",
            "/var/log/apache2",
            "/var/log/mysql",
            "/var/log/postgresql",
            "/var/log/docker",
            "/var/log/containers",
            "/var/log/cups",
            "/var/log/samba",
            "/var/log/audit",
            "/var/log/httpd",
            "/var/log/apt",
            "/var/log/dpkg",
            "/var/log/yum",
            "/var/log/dnf",
            "/var/log/journal",
            "/var/log/mail",
            "/var/log/syslog",
            "/var/log/auth.log",
            "/var/log/kern.log",
            "/var/log/daemon.log",
            "/var/log/debug",
            "/var/log/messages",
            "/var/log/secure",
            "/var/log/boot.log",
            "/var/log/dmesg",
            "/var/log/wtmp",
            "/var/log/btmp",
            "/var/log/faillog",
            "/var/log/lastlog",
            "/var/log/cloud",
            "/var/log/unattended-upgrades",
            "/etc/logrotate.d",
            "/tmp",
        };

        foreach (var p in paths)
            ScanDirectory(p, results, seen);
    }

    private static void ScanMac(List<SystemLogFile> results, HashSet<string> seen)
    {
        var paths = new[]
        {
            "/var/log",
            "/Library/Logs",
            "/Library/Logs/DiagnosticReports",
            $"{Environment.GetFolderPath(Environment.SpecialFolder.Personal)}/Library/Logs",
            "/usr/local/var/log",
            "/usr/local/var/log/nginx",
            "/usr/local/var/log/mysql",
            "/usr/local/var/log/postgres",
            "/usr/local/var/log/redis",
            "/opt/homebrew/var/log",
        };

        foreach (var p in paths)
            ScanDirectory(p, results, seen);
    }

    private static void ScanDirectory(string dirPath, List<SystemLogFile> results, HashSet<string> seen)
    {
        if (string.IsNullOrEmpty(dirPath) || seen.Contains(dirPath)) return;
        seen.Add(dirPath);

        DirectoryInfo dir;
        try
        {
            dir = new DirectoryInfo(dirPath);
            if (!dir.Exists) return;
        }
        catch { return; }

        ScanFilesInDir(dir, results, seen, depth: 0);
    }

    private static void ScanFilesInDir(DirectoryInfo dir, List<SystemLogFile> results, HashSet<string> seen, int depth)
    {
        if (depth > 3) return;

        FileInfo[] files;
        DirectoryInfo[] subdirs;

        try
        {
            files = dir.GetFiles("*", new EnumerationOptions
            {
                IgnoreInaccessible = true,
                MatchCasing = MatchCasing.CaseInsensitive
            });
            subdirs = dir.GetDirectories();
        }
        catch { return; }

        foreach (var file in files)
        {
            if (file.Length == 0 || file.Length > 100 * 1024 * 1024) continue;
            if (!IsLogLikeFile(file)) continue;
            if (seen.Contains(file.FullName)) continue;
            seen.Add(file.FullName);

            var category = DetectCategory(file);
            bool isReadable;
            try { isReadable = IsTextFile(file); } catch { isReadable = false; }

            results.Add(new SystemLogFile
            {
                Name = file.Name,
                Path = file.FullName,
                Category = category,
                SizeBytes = file.Length,
                IsReadable = isReadable
            });
        }

        foreach (var subdir in subdirs)
        {
            if (SkipDirectories.Contains(subdir.Name)) continue;
            ScanFilesInDir(subdir, results, seen, depth + 1);
        }
    }

    private static bool IsLogLikeFile(FileInfo file)
    {
        var ext = file.Extension.ToLowerInvariant();
        if (ReadableExtensions.Contains(ext)) return true;

        var name = file.Name.ToLowerInvariant();
        if (name.Contains("log", StringComparison.OrdinalIgnoreCase)) return true;
        if (name.Contains("error", StringComparison.OrdinalIgnoreCase)) return true;
        if (name.Contains("debug", StringComparison.OrdinalIgnoreCase)) return true;
        if (name.Contains("trace", StringComparison.OrdinalIgnoreCase)) return true;
        if (name.Contains("output", StringComparison.OrdinalIgnoreCase)) return true;
        if (name.Contains("warning", StringComparison.OrdinalIgnoreCase)) return true;
        if (name.Contains("audit", StringComparison.OrdinalIgnoreCase)) return true;
        if (name.Contains("install", StringComparison.OrdinalIgnoreCase)) return true;

        var noExt = string.IsNullOrEmpty(ext);
        if (noExt)
        {
            if (name.StartsWith("syslog")) return true;
            if (name.StartsWith("auth.")) return true;
            if (name.StartsWith("kern.")) return true;
            if (name.StartsWith("mail.")) return true;
            if (name.StartsWith("daemon.")) return true;
            if (name.StartsWith("messages")) return true;
            if (name.StartsWith("secure")) return true;
            if (name.StartsWith("boot.")) return true;
            if (name.StartsWith("dmesg")) return true;
            if (name.StartsWith("wtmp")) return true;
            if (name.StartsWith("btmp")) return true;
            if (name.StartsWith("faillog")) return true;
            if (name.StartsWith("lastlog")) return true;
            if (name.StartsWith("journal")) return true;
        }

        return false;
    }

    private static string DetectCategory(FileInfo file)
    {
        var segments = file.FullName.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (var seg in segments)
        {
            if (CategoryMap.TryGetValue(seg, out var cat))
                return cat;
        }

        var nameLower = file.Name.ToLowerInvariant();
        if (nameLower.Contains("nginx")) return "Nginx";
        if (nameLower.Contains("apache") || nameLower.Contains("httpd")) return "Apache";
        if (nameLower.Contains("mysql")) return "MySQL";
        if (nameLower.Contains("postgres")) return "PostgreSQL";
        if (nameLower.Contains("docker")) return "Docker";
        if (nameLower.Contains("error")) return "Errors";
        if (nameLower.Contains("access")) return "Access";
        if (nameLower.Contains("install")) return "Installer";
        if (nameLower.Contains("update")) return "Updates";
        if (nameLower.Contains("firewall")) return "Firewall";
        if (nameLower.Contains("security") || nameLower.Contains("auth")) return "Security";
        if (nameLower.Contains("crash") || nameLower.Contains("dump")) return "Crash";
        if (nameLower.Contains("debug")) return "Debug";
        if (nameLower.Contains("audit")) return "Audit";

        return "Other";
    }

    private static bool IsTextFile(FileInfo file)
    {
        try
        {
            using var stream = file.OpenRead();
            Span<byte> buffer = stackalloc byte[Math.Min(8192, (int)file.Length)];
            var read = stream.Read(buffer);
            if (read == 0) return false;

            int nullCount = 0;
            for (int i = 0; i < read; i++)
            {
                if (buffer[i] == 0) nullCount++;
            }
            return nullCount < read * 0.01;
        }
        catch { return false; }
    }
}
