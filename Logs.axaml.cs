using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Npgsql;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Speck;

public partial class Logs : UserControl
{
    public ObservableCollection<Scans_DB> ScansDB { get; set; } = new();

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

    public Logs()
    {
        InitializeComponent();
        LoadData();
    }

    private void LoadData()
    {
        var scans = GetScans();
        ScansDB.Clear();
        foreach (var scan in scans)
        {
            ScansDB.Add(scan);
        }
        
        var dataGrid = this.FindControl<DataGrid>("ScansTable");
        if (dataGrid != null)
        {
            dataGrid.ItemsSource = ScansDB;
        }
    }
}