using Avalonia.Controls;
using Avalonia.Interactivity;
using System;

namespace Speck
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Menu Items
            MI_Dashboard.Click += MI_Dashboard_Click;
            MI_Scans.Click += MI_Scans_Click;
            MI_Vulnerabilities.Click += MI_Vulnerabilities_Click;
            MI_Logs.Click += MI_Logs_Click;
            MI_Customization.Click += MI_Customization_Click;
            MI_Settings.Click += MI_Settings_Click;
            //Menu Items

            MI_Dashboard_Click(this, new RoutedEventArgs());
        }

        private void MI_Dashboard_Click(object? sender, RoutedEventArgs e)
        {
            MI_Dashboard.IsChecked = true;
            MI_Scans.IsChecked = false;
            MI_Vulnerabilities.IsChecked = false;
            MI_Logs.IsChecked = false;
            MI_Customization.IsChecked = false;
            MI_Settings.IsChecked = false;

            MainCC.Content = new Dashboard();


        }

        private void MI_Scans_Click(object? sender, RoutedEventArgs e)
        {
            

            MI_Scans.IsChecked = true;
            MI_Dashboard.IsChecked = false;
            MI_Vulnerabilities.IsChecked = false;
            MI_Logs.IsChecked = false;
            MI_Customization.IsChecked = false;
            MI_Settings.IsChecked = false;

            MainCC.Content = new Scans();
        }

        private void MI_Vulnerabilities_Click(object? sender, RoutedEventArgs e)
        {
            MI_Vulnerabilities.IsChecked = true;
            MI_Dashboard.IsChecked = false;
            MI_Scans.IsChecked = false;
            MI_Logs.IsChecked = false;
            MI_Customization.IsChecked = false;
            MI_Settings.IsChecked = false;

            MainCC.Content = new Vulnerabilities();
        }

        private void MI_Logs_Click(object? sender, RoutedEventArgs e)
        {
            MI_Logs.IsChecked = true;
            MI_Dashboard.IsChecked = false;
            MI_Scans.IsChecked = false;
            MI_Vulnerabilities.IsChecked = false;
            MI_Customization.IsChecked = false;
            MI_Settings.IsChecked = false;

            MainCC.Content = new Logs();
        }

        private void MI_Customization_Click(object? sender, RoutedEventArgs e)
        {
            MI_Customization.IsChecked = true;
            MI_Dashboard.IsChecked = false;
            MI_Scans.IsChecked = false;
            MI_Vulnerabilities.IsChecked = false;
            MI_Logs.IsChecked = false;
            MI_Settings.IsChecked = false;

            MainCC.Content = new Customization();
        }

        private void MI_Settings_Click(object? sender,  RoutedEventArgs e)
        {
            MI_Settings.IsChecked = true;
            MI_Dashboard.IsChecked = false;
            MI_Scans.IsChecked = false;
            MI_Vulnerabilities.IsChecked = false;
            MI_Logs.IsChecked = false;
            MI_Customization.IsChecked = false;

            MainCC.Content = new Settings();
        }
    }
}