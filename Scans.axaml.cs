using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Npgsql;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Speck;

public partial class Scans : UserControl
{
    private readonly ConcurrentQueue<string> _logQueue = new();
    private readonly StringBuilder _logBuffer = new();
    private DispatcherTimer? _logTimer;
    ScanProfile ScanOption = ScanProfile.Quick;

    private sealed record TrivyResult(string RawJson);
    private sealed record NucleiResult(string RawJson);
    private sealed record OsqueryResult(string RawJson);

    public Scans()
    {
        InitializeComponent();


        StartLogPump();
        BtnScan.IsEnabled = false;
    }

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

    private async Task<List<TrivyResult>> RunTrivyScanAsync(
    ScanProfile profile)
    {
        var results = new List<TrivyResult>();

        AppendConsoleLine($"[TRIVY] Starting filesystem scan ({profile})...");

        var userFolders = Directory.GetDirectories(@"C:\Users")
                                   .Where(u => !u.EndsWith("Public"));

        foreach (var folder in userFolders)
        {
            var outputLines = new List<string>();

            var args =
                $"fs \"{folder}\" " +
                "--scanners vuln " +
                "--format json " +
                "--exit-code 0 " +
                "--skip-version-check " +
                "--skip-dirs \"AppData\\Local\\Temp,System Volume Information,.git,node_modules\"";

            await RunCommand(
                ScannerPaths.Trivy,
                args,
                onOutput: line =>
                {
                    outputLines.Add(line);
                    AppendConsoleLine($"[TRIVY] {line}");
                },
                onError: line => AppendConsoleLine($"[TRIVY][Err] {line}")
            );

            var json = string.Join("\n", outputLines).Trim();
            if (!string.IsNullOrWhiteSpace(json))
                results.Add(new TrivyResult(json));
        }

        return results;
    }

    private async Task<List<NucleiResult>> RunNucleiScanAsync(
    ScanProfile profile)
    {
        var results = new List<NucleiResult>();

        AppendConsoleLine("[OSQUERY] Detecting local listening TCP ports...");
        var ports = await GetListeningPortsAsync();
        var filteredPorts = FilterPorts(ports, profile);

        if (filteredPorts.Count == 0)
        {
            AppendConsoleLine("[NUCLEI] No suitable HTTP targets found.");
            return results;
        }

        foreach (var target in BuildNucleiTargets(filteredPorts))
        {
            AppendConsoleLine($"[NUCLEI] Scanning {target} ({profile})...");

            await RunCommand(
                ScannerPaths.Nuclei,
                BuildNucleiArgs(target, profile),
                onOutput: line =>
                {
                    results.Add(new NucleiResult(line));
                    AppendConsoleLine($"[NUCLEI] {line}");
                },
                onError: line => AppendConsoleLine($"[NUCLEI][Err] {line}")
            );
        }

        return results;
    }

    private async Task<OsqueryResult> RunOsqueryInfoAsync()
    {
        AppendConsoleLine("[OSQUERY] Collecting OS info...");

        var lines = new List<string>();

        await RunCommand(
            ScannerPaths.Osquery,
            "--json \"SELECT * FROM os_version;\"",
            onOutput: line =>
            {
                lines.Add(line);
                AppendConsoleLine($"[OSQUERY] {line}");
            },
            onError: line => AppendConsoleLine($"[OSQUERY][Err] {line}")
        );

        return new OsqueryResult(string.Join("\n", lines));
    }

    private async Task InsertScanAsync(
    Guid scanId,
    ScanProfile profile,
    string[] tools)
    {
        await using var conn = new NpgsqlConnection(MainWindow.ConnectionString);
        await conn.OpenAsync();

        var cmd = new NpgsqlCommand("""
        INSERT INTO scans (
            scan_id,
            scan_type,
            tools,
            started_at,
            status
        )
        VALUES (
            @scan_id,
            @type,
            @tools,
            NOW(),
            'running'
        )
    """, conn);

        cmd.Parameters.AddWithValue("scan_id", scanId);
        cmd.Parameters.AddWithValue("type", profile.ToString().ToLower());
        cmd.Parameters.AddWithValue("tools", tools);

        await cmd.ExecuteNonQueryAsync();
    }

    private async Task MarkScanFinishedAsync(Guid scanId, string status)
    {
        await using var conn = new NpgsqlConnection(MainWindow.ConnectionString);
        await conn.OpenAsync();

        var cmd = new NpgsqlCommand("""
        UPDATE scans
        SET finished_at = NOW(),
            status = @status
        WHERE scan_id = @id
    """, conn);

        cmd.Parameters.AddWithValue("id", scanId);
        cmd.Parameters.AddWithValue("status", status);

        await cmd.ExecuteNonQueryAsync();
    }


    private async Task InsertTrivyFindingsAsync(
    Guid scanId,
    string rawJson)
    {
        using var doc = JsonDocument.Parse(rawJson);

        if (!doc.RootElement.TryGetProperty("Results", out var results))
            return;

        await using var conn = new NpgsqlConnection(MainWindow.ConnectionString);
        await conn.OpenAsync();

        foreach (var result in results.EnumerateArray())
        {
            if (!result.TryGetProperty("Vulnerabilities", out var vulns))
                continue;

            foreach (var vuln in vulns.EnumerateArray())
            {
                var cmd = new NpgsqlCommand("""
                INSERT INTO scan_findings (
                    finding_id,
                    scan_id,
                    category,
                    identifier,
                    title,
                    severity,
                    exposed,
                    source_tool,
                    data
                )
                VALUES (
                    @id,
                    @scan,
                    'vulnerability',
                    @ident,
                    @title,
                    @severity,
                    false,
                    'trivy',
                    @data::jsonb
                )
            """, conn);

                cmd.Parameters.AddWithValue("id", Guid.NewGuid());
                cmd.Parameters.AddWithValue("scan", scanId);
                cmd.Parameters.AddWithValue("ident", vuln.GetProperty("VulnerabilityID").GetString() ?? "");
                cmd.Parameters.AddWithValue("title", vuln.GetProperty("Title").GetString() ?? "");
                cmd.Parameters.AddWithValue("severity", vuln.GetProperty("Severity").GetString() ?? "unknown");
                cmd.Parameters.AddWithValue("data", vuln.GetRawText());

                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    private async Task InsertNucleiFindingAsync(
    Guid scanId,
    string rawJsonLine)
    {
        using var doc = JsonDocument.Parse(rawJsonLine);
        var root = doc.RootElement;

        await using var conn = new NpgsqlConnection(MainWindow.ConnectionString);
        await conn.OpenAsync();

        var cmd = new NpgsqlCommand("""
        INSERT INTO scan_findings (
            finding_id,
            scan_id,
            category,
            identifier,
            title,
            severity,
            exposed,
            source_tool,
            data
        )
        VALUES (
            @id,
            @scan,
            'exposure',
            @ident,
            @title,
            @severity,
            true,
            'nuclei',
            @data::jsonb
        )
    """, conn);

        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("scan", scanId);
        cmd.Parameters.AddWithValue("ident", root.GetProperty("template-id").GetString() ?? "");
        cmd.Parameters.AddWithValue("title", root.GetProperty("info").GetProperty("name").GetString() ?? "");
        cmd.Parameters.AddWithValue("severity", root.GetProperty("info").GetProperty("severity").GetString() ?? "info");
        cmd.Parameters.AddWithValue("data", rawJsonLine);

        await cmd.ExecuteNonQueryAsync();
    }

    private async Task InsertOsqueryFindingAsync(
    Guid scanId,
    string rawJson)
    {
        await using var conn = new NpgsqlConnection(MainWindow.ConnectionString);
        await conn.OpenAsync();

        var cmd = new NpgsqlCommand("""
        INSERT INTO scan_findings (
            finding_id,
            scan_id,
            category,
            identifier,
            title,
            severity,
            exposed,
            source_tool,
            data
        )
        VALUES (
            @id,
            @scan,
            'configuration',
            'os_version',
            'Operating System Information',
            'info',
            false,
            'osquery',
            @data::jsonb
        )
    """, conn);

        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("scan", scanId);
        cmd.Parameters.AddWithValue("data", rawJson);

        await cmd.ExecuteNonQueryAsync();
    }

    private async Task PersistScanResultsAsync(
        ScanProfile profile,
        List<TrivyResult> trivy,
        List<NucleiResult> nuclei,
        OsqueryResult osquery)
    {
        var scanId = Guid.NewGuid();

        try
        {
            // =========================
            // SCAN (START)
            // =========================
            await InsertScanAsync(
                scanId,
                profile,
                new[] { "trivy", "nuclei", "osquery" }
            );

            // =========================
            // FINDINGS
            // =========================
            foreach (var t in trivy)
                await InsertTrivyFindingsAsync(scanId, t.RawJson);

            foreach (var n in nuclei)
                await InsertNucleiFindingAsync(scanId, n.RawJson);

            await InsertOsqueryFindingAsync(scanId, osquery.RawJson);

            // =========================
            // SCAN (END)
            // =========================
            await MarkScanFinishedAsync(scanId, "completed");
        }
        catch
        {
            await MarkScanFinishedAsync(scanId, "failed");
            throw;
        }
    }



    private async Task VulnerabilityScan(ScanProfile profile)
    {
        try
        {
            BtnScan.IsEnabled = false;
            ConsoleTextBlock.Text = "";

            AppendConsoleLine("========================= SCAN STARTING =========================");

            var trivyResults = await RunTrivyScanAsync(profile);
            var nucleiResults = await RunNucleiScanAsync(profile);
            var osqueryResult = await RunOsqueryInfoAsync();

            await PersistScanResultsAsync(
                profile,
                trivyResults,
                nucleiResults,
                osqueryResult
            );

            AppendConsoleLine("========================= SCAN COMPLETED =========================");
        }
        catch (Exception ex)
        {
            AppendConsoleLine($"========================= SCAN FAILED: {ex.Message} =========================");
        }
        finally
        {
            BtnScan.IsEnabled = true;
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