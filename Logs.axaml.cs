using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using DocumentFormat.OpenXml.Spreadsheet;
using ExCSS;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using static Speck.Vulnerabilities;

namespace Speck;

public partial class Logs : UserControl
{
    public ObservableCollection<Scans_DB> ScansDB { get; set; } = new();
    private ObservableCollection<Scans_DB> filteredScansDB = new();
    private string ExportLogID { get; set; } = string.Empty;

    public class Scans_DB
    {
        public string ID { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Tools { get; set; } = string.Empty;
        public string Started_at { get; set; } = string.Empty;
        public string Finished_at { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public List<Scans_DB> GetScans()
    {
        var scanList = new List<Scans_DB>();

        try
        {
            using var conn = new NpgsqlConnection(MainWindow.ConnectionString);
            conn.Open();

            using var cmd = new NpgsqlCommand("""
                SELECT scan_id, scan_type, tools, started_at, finished_at, status
                FROM scans
                ORDER BY started_at DESC
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
                    Status = reader.IsDBNull(5) ? string.Empty : reader.GetString(5)
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
                          item.Status.Contains(searchText, StringComparison.OrdinalIgnoreCase))
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

    private void ExpLog_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Debug.WriteLine(ExportLogID);
        ExportLogID = string.Empty;

        //Closing
        Overlay.Opacity = 0;
        Overlay.IsHitTestVisible = false;
        LogQuest.IsVisible = false;
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
            QuestBodyText.Inlines.Clear();

            QuestBodyText.Inlines.Add(new Avalonia.Controls.Documents.Run("Do you want to export "));

            QuestBodyText.Inlines.Add(new Avalonia.Controls.Documents.Run(ExportLogID)
            {
                FontWeight = Avalonia.Media.FontWeight.SemiBold
            });

            QuestBodyText.Inlines.Add(new Avalonia.Controls.Documents.Run(" and all it's finding(s)?"));
        }
    }
}