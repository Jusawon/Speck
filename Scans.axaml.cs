using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Speck;

public partial class Scans : UserControl
{
    private readonly ConcurrentQueue<string> _logQueue = new();
    private readonly StringBuilder _logBuffer = new();
    private DispatcherTimer? _logTimer;
    ScanProfile ScanOption = ScanProfile.Quick;
    public Scans()
    {
        InitializeComponent();


        StartLogPump();
        BtnScan.IsEnabled = false;
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
    private void AppendConsoleLine(string message)
    {
        _logQueue.Enqueue(message);
    }

    private void StartLogPump()
    {
        _logTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(75)
        };

        _logTimer.Tick += (_, _) =>
        {
            if (_logQueue.IsEmpty)
                return;

            while (_logQueue.TryDequeue(out var line))
                _logBuffer.AppendLine(line);

            ConsoleTextBlock.Text += _logBuffer.ToString();
            _logBuffer.Clear();

            ConsoleScrollViewer.Offset =
                new Avalonia.Vector(
                    ConsoleScrollViewer.Offset.X,
                    ConsoleScrollViewer.Extent.Height
                );
        };

        _logTimer.Start();
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
            onError: line => AppendConsoleLine($"[OSQUERY] {line}")
        );

        // Find JSON array boundaries
        var json = string.Join("\n", rawLines);

        int start = json.IndexOf('[');
        int end = json.LastIndexOf(']');

        if (start == -1 || end == -1 || end <= start)
        {
            AppendConsoleLine("No valid JSON array found in osquery output");
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
            AppendConsoleLine($"JSON parse error: {ex.Message}");
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
            BtnScan.IsEnabled = false;
            ConsoleTextBlock.Text = "";
            string scanRoot = OperatingSystem.IsWindows() ? @"C:\" : "/";
            string trivyArgs ="";

            AppendConsoleLine("========================= SCAN STARTING =========================");
            // =========================
            // TRIVY
            // =========================
            AppendConsoleLine($"[TRIVY] Starting filesystem scan ({profile})...");

            var userFolders = Directory.GetDirectories(@"C:\Users")
                                       .Where(u => !u.EndsWith("Public"));
            foreach (var folder in userFolders)
            {
               trivyArgs =
                    $"fs \"{folder}\" " +
                    "--scanners vuln " +
                    "--format json " +
                    "--exit-code 0 " +
                    "--skip-version-check " +
                    "--skip-dirs \"AppData\\Local\\Temp,System Volume Information,.git,node_modules\"";

                await RunCommand(
                    ScannerPaths.Trivy,
                    trivyArgs,
                    onOutput: AppendConsoleLine,
                    onError: AppendConsoleLine
                );
            }

            // =========================
            // PORT DISCOVERY
            // =========================
            ConsoleTextBlock.Text += ("[OSQUERY] Detecting local listening TCP ports...");

            var ports = await GetListeningPortsAsync();
            var filteredPorts = FilterPorts(ports, profile);

            if (filteredPorts.Count == 0)
            {
                AppendConsoleLine("[NUCLEI] No suitable HTTP targets found. Skipping.");
            }
            else
            {
                var targets = BuildNucleiTargets(filteredPorts);

                // =========================
                // NUCLEI
                // =========================
                foreach (var target in targets)
                {
                    AppendConsoleLine($"[NUCLEI] Scanning {target} ({profile})...");

                    await RunCommand(
                        ScannerPaths.Nuclei,
                        BuildNucleiArgs(target, profile),
                        onOutput: line => AppendConsoleLine($"[NUCLEI] {line}"),
                        onError: line => AppendConsoleLine($"[NUCLEI][Err] {line}")
                    );
                }
            }

            // =========================
            // SYSTEM INFO
            // =========================
            AppendConsoleLine("[OSQUERY] Collecting OS info...");

            await RunCommand(
                ScannerPaths.Osquery,
                "--json \"SELECT * FROM os_version;\"",
                onOutput: line => AppendConsoleLine($"[OSQUERY] {line}"),
                onError: line => AppendConsoleLine($"[OSQUERY][Err] {line}")
            );

            AppendConsoleLine("========================= SCAN COMPLETED =========================");
        }
        catch (Exception ex)
        {
            AppendConsoleLine($"========================= SCAN FAILED: {ex.Message} =========================");
        }
        BtnScan.IsEnabled = true;
        //Add a bool for this
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

        if (process.ExitCode != 0 && process.ExitCode != 1)
            throw new Exception($"{Path.GetFileName(fileName)} exited with code {process.ExitCode}");
    }



    private async void BtnScan_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {

        await VulnerabilityScan(ScanOption);
    }

    private void ScanCmb_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {


        if (QuickItem.IsSelected)
        {
            BtnScan.IsEnabled = true;
            ScanOption = ScanProfile.Quick;
        }
        else if (FullItem.IsSelected)
        {
            BtnScan.IsEnabled = true;
            ScanOption = ScanProfile.Full;
        }
        else BtnScan.IsEnabled = false;

    }
}