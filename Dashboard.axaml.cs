using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Speck;

public partial class Dashboard : UserControl, INotifyPropertyChanged
{
    public event Action? VulnerabilitiesRequested;
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? ScansRequested;
    private int _scanCount; 
    private int _vulnCount;

    public int ScanCount
    {
        get => _scanCount;
        set
        {
            _scanCount = value;
            PropertyChanged?.Invoke(this, new(nameof(ScanCount)));
        }
    }

    public int VulnCount
    {
        get => _vulnCount;
        set
        {
            _vulnCount = value;
            PropertyChanged?.Invoke(this, new(nameof(VulnCount)));
        }
    }

    public Dashboard()
    {
        InitializeComponent();
        DataContext = this;

        ScanCount = 3;
        VulnCount = 12;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private void NavScan_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ScanCount++;
        ScansRequested?.Invoke();
    }

    private void NavVuln_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        VulnCount++;
        VulnerabilitiesRequested?.Invoke();
    }


}