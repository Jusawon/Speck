using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using ExCSS;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using static Speck.Glb;

namespace Speck;

public partial class Vulnerabilities : UserControl
{

    public event Action<string>? OpenChat;
    public ObservableCollection<Vuln_DB> VulnDB { get; set; } = new();
    private ObservableCollection<Vuln_DB> filteredVulnDB = new();
    private string VulnID { get; set; } = string.Empty;

    public class Vuln_DB
    {
        public string ID { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Identifier { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Exposed { get; set; }
        public string Tool { get; set; }

    }

    public List<Vuln_DB> GetVulns()
    {
        var scanList = new List<Vuln_DB>();

        try
        {
            using var conn = new NpgsqlConnection(ConnectionString);
            conn.Open();

            using var cmd = new NpgsqlCommand("""
                SELECT finding_id, category, identifier, title, severity, exposed, source_tool
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
                scanList.Add(new Vuln_DB
                {
                    ID = reader.GetGuid(0).ToString(),
                    Category = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    Identifier = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    Title = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    Severity = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    Exposed = !reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    Tool = reader.IsDBNull(6) ? string.Empty : reader.GetString(6)
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
        filteredVulnDB.Clear();

        var filteredItems = VulnDB
            .Where(item => string.IsNullOrEmpty(searchText) ||
                          item.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                          item.Identifier.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                          item.Category.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                          item.Severity.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                          item.Tool.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                          item.ID.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var item in filteredItems)
        {
            filteredVulnDB.Add(item);
        }

        var dataGrid = this.FindControl<DataGrid>("VulnTable");
        if (dataGrid != null)
        {
            dataGrid.ItemsSource = filteredVulnDB;
        }
    }

    private void LoadData()
    {
        var vuln = GetVulns();

        CriticalCounter.Text = vuln.Count(v => string.Equals(v.Severity, "critical", StringComparison.OrdinalIgnoreCase)).ToString();
        HighCounter.Text = vuln.Count(v => string.Equals(v.Severity, "high", StringComparison.OrdinalIgnoreCase)).ToString();
        MediumCounter.Text = vuln.Count(v => string.Equals(v.Severity, "medium", StringComparison.OrdinalIgnoreCase)).ToString();
        LowCounter.Text = vuln.Count(v => string.Equals(v.Severity, "low", StringComparison.OrdinalIgnoreCase)).ToString();
        InfoCounter.Text = vuln.Count(v => string.Equals(v.Severity, "info", StringComparison.OrdinalIgnoreCase)).ToString();
        UnknownCounter.Text = vuln.Count(v => string.Equals(v.Severity, "unknown", StringComparison.OrdinalIgnoreCase)).ToString();



        VulnDB.Clear();
        foreach (var scan in vuln)
        {
            VulnDB.Add(scan);
        }


        ApplyFilter();
    }

    public Vulnerabilities()
    {
        InitializeComponent();
        LoadData();
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

    private void AskVuln_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (VulnID != String.Empty)
        {
            var quest = "How do i mitigate " + VulnID + "?";
            VulnID = string.Empty;
            OpenChat?.Invoke(quest);
        }


        //Closing
        Overlay.Opacity = 0;
        Overlay.IsHitTestVisible = false;
        VulnQuest.IsVisible = false;
    }

    private void Btn_Close_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Overlay.Opacity = 0;
        Overlay.IsHitTestVisible = false;
        VulnQuest.IsVisible = false;
    }

    private void VulnTable_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {

        if (e.Source is not Avalonia.Controls.Control control)
            return;

        var row = control.FindAncestorOfType<DataGridRow>();

        if (row == null)
            return;


        if (VulnTable.SelectedItem is Vuln_DB selectedVuln)
        {
            Overlay.Opacity = 0.7;
            Overlay.IsHitTestVisible = true;
            VulnQuest.IsVisible = true;

            VulnID = selectedVuln.Identifier;
            QuestBodyText.Inlines.Clear();

            QuestBodyText.Inlines.Add(new Avalonia.Controls.Documents.Run("Ask Speck About "));

            QuestBodyText.Inlines.Add(new Avalonia.Controls.Documents.Run(VulnID)
            {
                FontWeight = Avalonia.Media.FontWeight.SemiBold
            });

            QuestBodyText.Inlines.Add(new Avalonia.Controls.Documents.Run("?"));
        }

    }
}