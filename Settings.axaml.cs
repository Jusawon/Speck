using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.Diagnostics;

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
}