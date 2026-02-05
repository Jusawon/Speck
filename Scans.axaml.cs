using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Linq;

namespace Speck;

public partial class Scans : UserControl
{
    ScanProfile ScanOption = ScanProfile.Full;
       public Scans()
        {
            InitializeComponent();
        }

    //private async Task VulnerabilityScan()
    //{
    //    try
    //    {
    //        string scanRoot =
    //            OperatingSystem.IsWindows() ? @"C:\" : "/";

    //        Console.WriteLine("Starting Trivy...");

    //        string trivyArgs =
    //            $"fs {scanRoot} " +
    //            "--scanners vuln " +
    //            "--format json " +
    //            "--exit-code 0 " +
    //            "--ignore-unfixed " +
    //            "--skip-dirs \"Windows,ProgramData,AppData,Temp,System Volume Information,bin,obj,.git,node_modules\"";

    //        await RunCommand(
    //            ScannerPaths.Trivy,
    //            trivyArgs,
    //            onOutput: line => Console.WriteLine(line),
    //            onError: line => Console.Error.WriteLine(line)
    //        );

    //        Console.WriteLine("Detecting local listening ports...");

    //        var ports = await GetListeningPortsAsync();

    //        if (ports.Count == 0)
    //        {
    //            Console.WriteLine("No listening TCP services detected. Skipping Nuclei.");
    //        }
    //        else
    //        {
    //            var targets = BuildNucleiTargets(ports);

    //            foreach (var target in targets)
    //            {
    //                Console.WriteLine($"Starting Nuclei on {target}...");

    //                await RunCommand(
    //                    ScannerPaths.Nuclei,
    //                    $"-target {target} " +
    //                    "-jsonl " +
    //                    "-timeout 3 " +
    //                    "-retries 0 " +
    //                    "-no-interactsh " +
    //                    "-silent",
    //                    onOutput: line => Console.WriteLine(line),
    //                    onError: line => Console.Error.WriteLine(line)
    //                );
    //            }
    //        }


    //        Console.WriteLine("Starting osquery...");

    //        await RunCommand(
    //            ScannerPaths.Osquery,
    //            "--json \"SELECT * FROM os_version;\"",
    //            onOutput: line => Console.WriteLine(line),
    //            onError: line => Console.Error.WriteLine(line)
    //        );

    //        Console.WriteLine("Scan complete");
    //    }
    //    catch (Exception ex)
    //    {
    //        Console.Error.WriteLine($"Scan failed: {ex.Message}");
    //    }
    //}

    private enum ScanProfile
    {
        Quick,
        Full
    }

    private static readonly HashSet<int> QuickHttpPorts = new()
        {
            80, 443, 8000, 8008, 8080, 8081, 8443, 8888, 3000, 5000
        };

    private static readonly HashSet<int> BlockedPorts = new()
        {
            135, 139, 445, 3389, 5985, 5986
        };

    private static List<int> FilterPorts(
        IEnumerable<int> ports,
        ScanProfile profile)
    {
        return profile switch
        {
            ScanProfile.Quick =>
                ports.Where(p => QuickHttpPorts.Contains(p)).ToList(),

            ScanProfile.Full =>
                ports.Where(p => !BlockedPorts.Contains(p)).ToList(),

            _ => new List<int>()
        };
    }

    private static string BuildNucleiArgs(
    string target,
    ScanProfile profile)
    {
        return profile switch
        {
            ScanProfile.Quick =>
                $"-target {target} " +
                "-jsonl " +
                "-severity critical,high " +
                "-type http " +
                "-timeout 3 " +
                "-retries 0 " +
                "-no-interactsh ",

            ScanProfile.Full =>
                $"-target {target} " +
                "-jsonl " +
                "-severity critical,high,medium,low " +
                "-timeout 5 " +
                "-retries 1 " +
                "-no-interactsh",

            _ => ""
        };
    }


    private record OsqueryPort(string port);

    private async Task<List<int>> GetListeningPortsAsync()
    {
        var rawLines = new List<string>();

        await RunCommand(
            ScannerPaths.Osquery,
            "--json --disable_extensions \"SELECT port FROM listening_ports WHERE protocol = 6;\"",
            onOutput: line => rawLines.Add(line),
            onError: line => Console.Error.WriteLine($"[OSQUERY] {line}")
        );

        // Find JSON array boundaries
        var json = string.Join("\n", rawLines);

        int start = json.IndexOf('[');
        int end = json.LastIndexOf(']');

        if (start == -1 || end == -1 || end <= start)
        {
            Console.Error.WriteLine("No valid JSON array found in osquery output");
            return new List<int>();
        }

        string jsonArray = json.Substring(start, end - start + 1);

        try
        {
            var ports = System.Text.Json.JsonSerializer.Deserialize<List<OsqueryPort>>(jsonArray);

            return ports?
                .Select(p => int.TryParse(p.port, out var v) ? v : -1)
                .Where(p => p > 0 && p < 49152)
                .Distinct()
                .ToList()
                ?? new List<int>();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"JSON parse error: {ex.Message}");
            return new List<int>();
        }
    }

    private static List<string> BuildNucleiTargets(IEnumerable<int> ports)
    {
        var targets = new List<string>();

        foreach (var port in ports)
        {
            if (port == 80)
                targets.Add("http://localhost");
            else if (port == 443)
                targets.Add("https://localhost");
            else
                targets.Add($"http://localhost:{port}");
        }

        return targets;
    }

    private static class ScannerPaths
    {
        private static readonly string Base =
            AppContext.BaseDirectory;

        public static string Trivy =>
            Path.Combine(Base, "Scanners", "Trivy",
                OperatingSystem.IsWindows() ? "trivy.exe" : "trivy");

        public static string Nuclei =>
            Path.Combine(Base, "Scanners", "Nuclei",
                OperatingSystem.IsWindows() ? "nuclei.exe" : "nuclei");

        public static string Osquery =>
            Path.Combine(Base, "Scanners", "Osquery",
                OperatingSystem.IsWindows() ? "osqueryi.exe" : "osqueryi");
    }

    private async Task VulnerabilityScan(ScanProfile profile)
    {
        try
        {
            string scanRoot =
                OperatingSystem.IsWindows() ? @"C:\" : "/";

            // =========================
            // TRIVY
            // =========================
            Console.WriteLine($"[TRIVY] Starting filesystem scan ({profile})...");

            string trivyArgs =
                $"fs {scanRoot} " +
                "--scanners vuln " +
                "--format json " +
                "--exit-code 0 " +
                "--ignore-unfixed " +
                "--skip-dirs \"Windows,ProgramData,AppData,Temp,System Volume Information,bin,obj,.git,node_modules\"";

            await RunCommand(
                ScannerPaths.Trivy,
                trivyArgs,
                onOutput: line => Console.WriteLine($"[TRIVY] {line}"),
                onError: line => Console.Error.WriteLine($"[TRIVY] {line}")
            );

            // =========================
            // PORT DISCOVERY
            // =========================
            Console.WriteLine("[OSQUERY] Detecting local listening TCP ports...");

            var ports = await GetListeningPortsAsync();
            var filteredPorts = FilterPorts(ports, profile);

            if (filteredPorts.Count == 0)
            {
                Console.WriteLine("[NUCLEI] No suitable HTTP targets found. Skipping.");
            }
            else
            {
                var targets = BuildNucleiTargets(filteredPorts);

                // =========================
                // NUCLEI
                // =========================
                foreach (var target in targets)
                {
                    Console.WriteLine($"[NUCLEI] Scanning {target} ({profile})...");

                    await RunCommand(
                        ScannerPaths.Nuclei,
                        BuildNucleiArgs(target, profile),
                        onOutput: line => Console.WriteLine($"[NUCLEI] {line}"),
                        onError: line => Console.Error.WriteLine($"[NUCLEI] {line}")
                    );
                }
            }

            // =========================
            // SYSTEM INFO
            // =========================
            Console.WriteLine("[OSQUERY] Collecting OS info...");

            await RunCommand(
                ScannerPaths.Osquery,
                "--json \"SELECT * FROM os_version;\"",
                onOutput: line => Console.WriteLine($"[OSQUERY] {line}"),
                onError: line => Console.Error.WriteLine($"[OSQUERY] {line}")
            );

            Console.WriteLine("✔ Scan complete");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"✖ Scan failed: {ex.Message}");
        }
    }

    private async Task RunCommand(
            string fileName,
    string arguments,
    Action<string>? onOutput = null,
    Action<string>? onError = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = Path.GetDirectoryName(fileName)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                onOutput?.Invoke(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                onError?.Invoke(e.Data);
        };

        process.Start();

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        if(process.ExitCode != 0 && process.ExitCode != 1)
            throw new Exception($"{Path.GetFileName(fileName)} exited with code {process.ExitCode}");
    }



    private async void BtnScan_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        BtnScan.IsEnabled = false;
        await VulnerabilityScan(ScanOption);
        BtnScan.IsEnabled = true;

    }
}