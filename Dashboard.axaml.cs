using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Svg.Skia;
using Npgsql;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using static Speck.Glb;

namespace Speck;

public partial class Dashboard : UserControl, INotifyPropertyChanged
{
    public event Action? VulnerabilitiesRequested;
    public event PropertyChangedEventHandler? PropertyChanged;
    public static string SpeckImg;
    public event Action? ScansRequested;

    public Dashboard()
    {
        InitializeComponent();
        DataContext = this;


        loadCounters();
        LoadMessage();
    }

    private void LoadMessage()
    {
        var scanCount = scanDBCount();
        DateTime now = DateTime.Now;
        DateTime? lastScan = GetLatestScanFinishedAt();

        TimeSpan diff = now - lastScan.Value;
        int daysDifference = diff.Days;

        if (daysDifference > 3 && scanCount > 0)
        {
            SpeckBorderText.Text = "*Pock* *Pock* It has been " + daysDifference + " days, since your last scan. Do you want to scan now?";
        }
        else if (daysDifference <= 3 && scanCount > 0)
        {
            SpeckBorderText.Text = "*Pock* *Pock* Here are your latest scan info!";
            SpeckNavScan.IsVisible = false;
        }
        else if (scanCount <= 0)
        {
            SpeckBorderText.Text = "*Pock* *Pock* Looks like you haven’t run any scans yet. Do you want to scan your device now?";
        }

        LoadImage();
    }

    private void LoadImage()
    {
        var Resource = SvgSource.Load(SpeckImg);
        SpeckPic.Source = new SvgImage { Source = Resource };
    }
    public class VulnSeverity
    {
        public string Severity { get; set; }
    }

    public List<VulnSeverity> GetSeverities()
    {
        var scanList = new List<VulnSeverity>();

        try
        {
            using var conn = new NpgsqlConnection(ConnectionString);
            conn.Open();

            using var cmd = new NpgsqlCommand("""
                SELECT severity
                FROM scan_findings
                WHERE scan_id = (
                SELECT scan_id
                FROM scans
                WHERE status = 'completed'
                  AND finished_at IS NOT NULL
                ORDER BY finished_at DESC
                LIMIT 1
            )
            """, conn);

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                scanList.Add(new VulnSeverity
                {
                    Severity = reader.IsDBNull(0) ? string.Empty : reader.GetString(0)
                });
            }
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Database error: {ex.Message}");
        }

        return scanList;
    }

    public double MetricCalculator()
    {
        double MetricScore = 0.0;
        var sever = GetSeverities();

        var Critical = sever.Count(v => string.Equals(v.Severity, "critical", StringComparison.OrdinalIgnoreCase));
        var High = sever.Count(v => string.Equals(v.Severity, "high", StringComparison.OrdinalIgnoreCase));
        var Medium = sever.Count(v => string.Equals(v.Severity, "medium", StringComparison.OrdinalIgnoreCase));
        var Low = sever.Count(v => string.Equals(v.Severity, "low", StringComparison.OrdinalIgnoreCase));
        var Info = sever.Count(v => string.Equals(v.Severity, "info", StringComparison.OrdinalIgnoreCase));
        var Unknown = sever.Count(v => string.Equals(v.Severity, "unknown", StringComparison.OrdinalIgnoreCase));

        int rawScore = ((Critical * 5)
           + (High * 4)
           + (Medium * 3)
           + (Low * 2)
           + (Info * 1)
           + (Unknown * 1));

        int totalFindings = Critical + High + Medium + Low + Info + Unknown;
        int maxScore = totalFindings * 5;

        if (maxScore > 0)
        {
            MetricScore = (double)rawScore / maxScore * 10.0;
        }

        if (MetricScore == 0)
            RiskMetric.Foreground = Brushes.Gray;

        else if (MetricScore < 4.0)
            RiskMetric.Foreground = Brushes.LimeGreen;

        else if (MetricScore < 7.0)
            RiskMetric.Foreground = Brushes.Goldenrod;

        else if (MetricScore < 9.0)
            RiskMetric.Foreground = Brushes.OrangeRed;

        else
            RiskMetric.Foreground = Brushes.Red;

        return MetricScore;
    }

    public DateTime? GetLatestScanFinishedAt()
    {
        try
        {
            using var conn = new NpgsqlConnection(ConnectionString);
            conn.Open();

            using var cmd = new NpgsqlCommand("""
            SELECT finished_at
            FROM scans
            WHERE status = 'completed'
              AND finished_at IS NOT NULL
            ORDER BY finished_at DESC
            LIMIT 1
        """, conn);

            var result = cmd.ExecuteScalar();

            if (result != null && result != DBNull.Value)
                return (DateTime)result;

            return null; // no completed scans found
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Database error: {ex.Message}");
            return null;
        }
    }

    public int vulnDBCount()
    {
        int VulnCount = 0;
        try
        {
            using var conn = new NpgsqlConnection(ConnectionString);
            conn.Open();

            using var cmd = new NpgsqlCommand("""
                SELECT COUNT(*)
                FROM scan_findings
                    WHERE scan_id = (
                    SELECT scan_id
                    FROM scans
                    WHERE status = 'completed'
                      AND finished_at IS NOT NULL
                    ORDER BY finished_at DESC
                    LIMIT 1
                )
                """, conn);

            var result = cmd.ExecuteScalar();
            VulnCount = result != null ? Convert.ToInt32(result) : 0;
        }

        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Database error: {ex.Message}");
        }

        return VulnCount;
    }

    public int scanDBCount()
    {
        int ScanCount = 0;

        try
        {
            using var conn = new NpgsqlConnection(ConnectionString);
            conn.Open();

            using var cmd = new NpgsqlCommand("""
                SELECT COUNT(*)
                FROM scans
                """, conn);

            var result = cmd.ExecuteScalar();
            ScanCount = result != null ? Convert.ToInt32(result) : 0;
        }

        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Database error: {ex.Message}");
        }

        return ScanCount;
    }

    public void loadCounters()
    {
        ScanCounter.Text = scanDBCount().ToString();
        VulnCounter.Text = vulnDBCount().ToString();
        RiskMetric.Text = MetricCalculator().ToString("0.0");
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private void NavScan_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ScansRequested?.Invoke();
    }

    private void NavVuln_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        VulnerabilitiesRequested?.Invoke();
    }

    private void SpeckNavScan_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ScansRequested?.Invoke();
    }
}