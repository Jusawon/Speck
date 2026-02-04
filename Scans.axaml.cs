using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Speck;

public partial class Scans : UserControl
{
    string scanRoot =
    OperatingSystem.IsWindows() ? @"C:\" : "/";

    public Scans()
    {
        InitializeComponent();
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

    private async Task VulnerabilityScan()
    {
        try
        {
            string scanRoot =
                OperatingSystem.IsWindows() ? @"C:\" : "/";

            Console.WriteLine("Starting Trivy...");

            string trivyArgs =
                $"fs {scanRoot} " +
                "--scanners vuln " +
                "--format json " +
                "--exit-code 0 " +
                "--skip-dirs \"Windows,ProgramData,AppData,Temp,System Volume Information,bin,obj,.git,node_modules\"";

            await RunCommand(
                ScannerPaths.Trivy,
                trivyArgs,
                onOutput: line => Console.WriteLine(line),
                onError: line => Console.Error.WriteLine(line)
            );

            Console.WriteLine("Starting Nuclei...");

            await RunCommand(
                ScannerPaths.Nuclei,
                "-target http://localhost -jsonl",
                onOutput: line => Console.WriteLine(line),
                onError: line => Console.Error.WriteLine(line)
            );

            Console.WriteLine("Starting osquery...");

            await RunCommand(
                ScannerPaths.Osquery,
                "--json \"SELECT * FROM os_version;\"",
                onOutput: line => Console.WriteLine(line),
                onError: line => Console.Error.WriteLine(line)
            );

            Console.WriteLine("Scan complete");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Scan failed: {ex.Message}");
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
        await VulnerabilityScan();
        BtnScan.IsEnabled = true;

    }
}