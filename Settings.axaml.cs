using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using System.Diagnostics;
using System.IO;

namespace Speck;

public partial class Settings : UserControl
{
    public Settings()
    {

        InitializeComponent();
        LoadCmb();
    }

    private void LoadCmb()
    {
        switch (Glb.ExportType)
        {
            case "JSON":
                CmbFormat.SelectedValue = JsonSelect;
                break;

            case "CSV":
                CmbFormat.SelectedValue = CSVSelect;
                break;

            case "XML":
                CmbFormat.SelectedValue = XMLSelect;
                break;
        }
    }

    private void CmbFormat_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CmbFormat.SelectedItem == JsonSelect) Glb.ExportType = "JSON";
        else if (CmbFormat.SelectedItem == CSVSelect) Glb.ExportType = "CSV";
        else if (CmbFormat.SelectedItem == XMLSelect) Glb.ExportType = "XML";
    }

    private void BtnGuide_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var PathToGuide = Path.Combine(AppContext.BaseDirectory,
                                                        "How-To-Guide",
                                                        "Speck How-To Guide.pdf");

            var process = new System.Diagnostics.ProcessStartInfo
            {
                FileName = PathToGuide,
                UseShellExecute = true,
            };
            System.Diagnostics.Process.Start(process);
        } 
        catch (Exception Ex)
        {
            Debug.WriteLine(Ex.Message);
        }
    }
}