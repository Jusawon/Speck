using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Svg.Skia;
using Avalonia.VisualTree;
using ExCSS;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Serialization;
using static Speck.Glb;

namespace Speck;

public partial class Logs : UserControl
{
    public ObservableCollection<Scans_DB> ScansDB { get; set; } = new();
    public ObservableCollection<SelectedVulns> SelectedLogVuln { get; set; } = new();
    private ObservableCollection<Scans_DB> filteredScansDB = new();
    private string ExportLogID { get; set; } = string.Empty;
    public static string SpeckImg;

    public class Scans_DB
    {
        public string ID { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Tools { get; set; } = string.Empty;
        public string Started_at { get; set; } = string.Empty;
        public string Finished_at { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Count { get; set; } = string.Empty;

    }

    public class ScanExport
    {
        public Scan Scan { get; set; } = new();
        public List<Findings> Findings { get; set; } = new();
    }

    public class Scan
    {
        public string ID { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Tools { get; set; } = string.Empty;
        public string Started_at { get; set; } = string.Empty;
        public string Finished_at { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;

    }

    public class Findings
    {
        public string ID { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Identifier { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Exposed { get; set; }
        public string Tool { get; set; }
        public JsonElement? Data { get; set; }
    }

    public class SelectedVulns
    {


        public string Severity { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Identifier { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Tool { get; set; }
        public string Exposed { get; set; }
        public string ID { get; set; } = string.Empty;
    }

    public void GetSelectedLogVulns(string scanId)
    {
       SelectedLogVuln.Clear();

        try
        {
            using var conn = new NpgsqlConnection(ConnectionString);
            conn.Open();

            using var cmd = new NpgsqlCommand("""
                SELECT finding_id, category, identifier, title, severity, exposed, source_tool
                FROM scan_findings
                WHERE scan_id = @id
            """, conn);

            cmd.Parameters.AddWithValue("@id", Guid.Parse(scanId));

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                SelectedLogVuln.Add(new SelectedVulns
                {
                    ID = reader.GetGuid(0).ToString(),
                    Category = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    Identifier = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    Title = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    Severity = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    Exposed = reader.IsDBNull(5) ? string.Empty : reader.GetBoolean(5) ? "Exposed" : "Not Exposed",
                    Tool = reader.IsDBNull(6) ? string.Empty : reader.GetString(6)
                });
            }
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Database error: {ex.Message}");
        }

        var dataGrid = this.FindControl<DataGrid>("SelectedLogTable");
        if (dataGrid != null)
        {
            dataGrid.ItemsSource = SelectedLogVuln;
        }
    }

    public List<Scans_DB> GetScans()
    {
        var scanList = new List<Scans_DB>();

        try
        {
            using var conn = new NpgsqlConnection(ConnectionString);
            conn.Open();

            using var cmd = new NpgsqlCommand("""
                SELECT 
                    s.scan_id, 
                    s.scan_type, 
                    s.tools, 
                    s.started_at, 
                    s.finished_at, 
                    s.status,
                    COUNT(f.scan_id) AS findings_count
                FROM scans s
                LEFT JOIN scan_findings f 
                    ON f.scan_id = s.scan_id
                GROUP BY 
                    s.scan_id, 
                    s.scan_type, 
                    s.tools, 
                    s.started_at, 
                    s.finished_at, 
                    s.status
                ORDER BY s.started_at DESC
            """, conn);

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                scanList.Add(new Scans_DB
                {
                    ID = reader.GetGuid(0).ToString(),
                    Type = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    Tools = reader.IsDBNull(2) ? string.Empty : string.Join(",", reader.GetFieldValue<string[]>(2) ?? new string[0]),
                    Started_at = reader.GetDateTime(3).ToString("yyyy-MM-dd HH:mm:ss"),
                    Finished_at = reader.IsDBNull(4) ? string.Empty : reader.GetDateTime(4).ToString("yyyy-MM-dd HH:mm:ss"),
                    Status = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    Count = reader.IsDBNull(6) ? "0" : reader.GetInt32(6).ToString()
                });
            }
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Database error: {ex.Message}");
        }

        return scanList;
    }

    private void ApplyFilter(string searchText = "")
    {
        filteredScansDB.Clear();

        var filteredItems = ScansDB
            .Where(item => string.IsNullOrEmpty(searchText) ||
                          item.ID.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                          item.Type.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                          item.Tools.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                          item.Started_at.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                          item.Finished_at.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                          item.Status.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                          item.Count.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var item in filteredItems)
        {
            filteredScansDB.Add(item);
        }

        var dataGrid = this.FindControl<DataGrid>("ScansTable");
        if (dataGrid != null)
        {
            dataGrid.ItemsSource = filteredScansDB;
        }
    }

    private ScanExport GetScanExportData(string scanId)
    {
        var export = new ScanExport();

        using var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();

        // Get scan info
        using (var cmd = new NpgsqlCommand(
            "SELECT * FROM scans WHERE scan_id = @id", conn))
        {
            cmd.Parameters.AddWithValue("@id", Guid.Parse(scanId));

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                export.Scan = new Scan
                {
                    ID = reader.GetGuid(0).ToString(),
                    Type = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    Tools = reader.IsDBNull(2) ? string.Empty : string.Join(",", reader.GetFieldValue<string[]>(2) ?? new string[0]),
                    Started_at = reader.GetDateTime(3).ToString("yyyy-MM-dd HH:mm:ss"),
                    Finished_at = reader.IsDBNull(4) ? string.Empty : reader.GetDateTime(4).ToString("yyyy-MM-dd HH:mm:ss"),
                    Status = reader.IsDBNull(5) ? string.Empty : reader.GetString(5)
                };
            }
        }

        // Get findings
        using (var cmd = new NpgsqlCommand(
            "SELECT finding_id, category, identifier, title, severity, exposed, source_tool, data" +
            " FROM scan_findings WHERE scan_id = @id", conn))
        {
            cmd.Parameters.AddWithValue("@id", Guid.Parse(scanId));

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                JsonElement? details = null;

                if (!reader.IsDBNull(7))
                {
                    var jsonString = reader.GetString(7);
                    details = JsonSerializer.Deserialize<JsonElement>(jsonString);
                }

                export.Findings.Add(new Findings
                {
                    ID = reader.GetGuid(0).ToString(),
                    Category = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    Identifier = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    Title = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    Severity = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    Exposed = !reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    Tool = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                    Data = details
                });
            }
        }

        return export;
    }


    private async Task ExportJson(string scanId)
    {
        var data = GetScanExportData(scanId);

        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Save JSON File",
                DefaultExtension = "json",
                SuggestedFileName = $"scan_{scanId}.json",
                FileTypeChoices = new[]
                {
                new FilePickerFileType("JSON File")
                {
                    Patterns = new[] { "*.json" }
                }
                }
            });

        if (file == null)
            return;

        await using var stream = await file.OpenWriteAsync();
        using var writer = new StreamWriter(stream);
        await writer.WriteAsync(json);
    }

    private async Task ExportCsv(string scanId)
    {
        var data = GetScanExportData(scanId);

        var sb = new StringBuilder();
        sb.AppendLine("Scan_Id,Finding_Id,Category,Identifier,Title,Severity,Exposed,Tool,Data");

        foreach (var finding in data.Findings)
        {
            sb.AppendLine($"{data.Scan.ID}," +
                          $"{EscapeCsv(finding.ID)}," +
                          $"{EscapeCsv(finding.Category)}," +
                          $"{EscapeCsv(finding.Identifier)}," +
                          $"{EscapeCsv(finding.Title)}," +
                          $"{EscapeCsv(finding.Severity)}," +
                          $"{EscapeCsv(finding.Exposed)}," +
                          $"{EscapeCsv(finding.Tool)}," +
                          $"{EscapeCsv(finding.Data?.ToString())}");
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Save CSV File",
                DefaultExtension = "csv",
                SuggestedFileName = $"scan_{scanId}.csv",
                FileTypeChoices = new[]
                {
                new FilePickerFileType("CSV File")
                {
                    Patterns = new[] { "*.csv" }
                }
                }
            });

        if (file == null)
            return;

        await using var stream = await file.OpenWriteAsync();
        using var writer = new StreamWriter(stream);
        await writer.WriteAsync(sb.ToString());
    }

    private string EscapeCsv(string value)
    {
        if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
        {
            value = value.Replace("\"", "\"\"");
            return $"\"{value}\"";
        }

        return value;
    }

    private async Task ExportXml(string scanId)
    {
        var data = GetScanExportData(scanId);

        var serializer = new XmlSerializer(typeof(ScanExport));

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Save XML File",
                DefaultExtension = "xml",
                SuggestedFileName = $"scan_{scanId}.xml",
                FileTypeChoices = new[]
                {
                new FilePickerFileType("XML File")
                {
                    Patterns = new[] { "*.xml" }
                }
                }
            });

        if (file == null)
            return;

        await using var stream = await file.OpenWriteAsync();
        serializer.Serialize(stream, data);
    }

    private void LoadData()
    {
        var scans = GetScans();
        ScansDB.Clear();
        foreach (var scan in scans)
        {
            ScansDB.Add(scan);
        }

        ApplyFilter();
    }

    public Logs()
    {
        InitializeComponent();
        LoadData();
        LoadImage();
    }

    public void LoadImage()
    {
        SpeckBorderText.Text = "*Pock* *Pock* Here are logs of your previous scans, feel free to export them!";
        var Resource = SvgSource.Load(SpeckImg);
        SpeckPic.Source = new SvgImage { Source = Resource };
    }

    private void SearchInput_TextChanged(object? sender, TextChangedEventArgs e)
    {
        ApplyFilter(SearchInput.Text);
    }
    private void Btn_Clear_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SearchInput.Clear();
        ApplyFilter();
    }

    private void ClosePanel()
    {
        Overlay.Opacity = 0;
        Overlay.IsHitTestVisible = false;
        LogQuest.IsVisible = false;
    }

    private async void ExpLog_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(ExportLogID))
            return;

        switch (ExportType)
        {
            case "JSON":
                await ExportJson(ExportLogID);
                ExportLogID = string.Empty;

                ClosePanel();
                break;

            case "CSV":
                await ExportCsv(ExportLogID);

                ExportLogID = string.Empty;

                ClosePanel();
                break;

            case "XML":
                await ExportXml(ExportLogID);

                ExportLogID = string.Empty;

                ClosePanel();
                break;
        }

    }

    private void Btn_Close_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Overlay.Opacity = 0;
        Overlay.IsHitTestVisible = false;
        LogQuest.IsVisible = false;
    }

    private void ScansTable_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (e.Source is not Avalonia.Controls.Control control)
            return;

        var row = control.FindAncestorOfType<DataGridRow>();

        if (row == null)
            return;


        if (ScansTable.SelectedItem is Scans_DB selectedScan)
        {

            Overlay.Opacity = 0.7;
            Overlay.IsHitTestVisible = true;
            LogQuest.IsVisible = true;

            ExportLogID = selectedScan.ID;
            GetSelectedLogVulns(ExportLogID);
            QuestBodyText.Inlines.Clear();

            QuestBodyText.Inlines.Add(new Avalonia.Controls.Documents.Run("Do you want to export "));

            QuestBodyText.Inlines.Add(new Avalonia.Controls.Documents.Run(ExportLogID)
            {
                FontWeight = Avalonia.Media.FontWeight.SemiBold
            });

            QuestBodyText.Inlines.Add(new Avalonia.Controls.Documents.Run(" and all it's finding(s) to "));

            QuestBodyText.Inlines.Add(new Avalonia.Controls.Documents.Run(ExportType)
            {
                FontWeight = Avalonia.Media.FontWeight.SemiBold
            });

            QuestBodyText.Inlines.Add(new Avalonia.Controls.Documents.Run("?"));
        }
    }
}