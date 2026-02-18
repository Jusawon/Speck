using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

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
        switch(Glb.ExportType){
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
}