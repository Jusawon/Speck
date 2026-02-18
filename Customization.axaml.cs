using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Speck;

public partial class Customization : UserControl
{

    public Customization()
    {
        InitializeComponent();
        LoadBreed();
    }

    private void LoadBreed()
    {
        switch (Glb.SpeckBreed)
        {
            case "Kampoeng":
                SelectKampoeng.IsChecked = true;
            break;

            case "Orpington":
                SelectOrpington.IsChecked = true;
            break;

            case "Leghorn":
                SelectLeghorn.IsChecked = true;
            break;

            case "Austraslop":
                SelectAustraslop.IsChecked = true;
            break;

            case "Sussex":
                SelectSussex.IsChecked = true;
            break;
        }
    }

    private void SelectKampoeng_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SelectOrpington.IsChecked = false;
        SelectLeghorn.IsChecked = false;
        SelectAustraslop.IsChecked = false;
        SelectSussex.IsChecked = false;

        Glb.SpeckBreed = "Kampoeng";
    }

    private void SelectOrpington_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SelectKampoeng.IsChecked = false;
        SelectLeghorn.IsChecked = false;
        SelectAustraslop.IsChecked = false;
        SelectSussex.IsChecked = false;

        Glb.SpeckBreed = "Orpington";
    }

    private void SelectLeghorn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SelectKampoeng.IsChecked = false;
        SelectOrpington.IsChecked = false;
        SelectAustraslop.IsChecked = false;
        SelectSussex.IsChecked = false;

        Glb.SpeckBreed = "Leghorn";

    }

    private void SelectAustraslop_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SelectKampoeng.IsChecked = false;
        SelectOrpington.IsChecked = false;
        SelectLeghorn.IsChecked = false;
        SelectSussex.IsChecked = false;

        Glb.SpeckBreed = "Austraslop";

    }

    private void SelectSussex_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SelectKampoeng.IsChecked = false;
        SelectOrpington.IsChecked = false;
        SelectLeghorn.IsChecked = false;
        SelectAustraslop.IsChecked = false;

        Glb.SpeckBreed = "Sussex";

    }
}