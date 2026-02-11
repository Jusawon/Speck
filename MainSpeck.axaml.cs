using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Speck
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private int _scanCount;
        private int _vulnCount;
        private float _riskMetric;
        public const string ConnectionString = "Host=localhost;Port=5434;Username=postgres;Password=1234;Database=dbspeck"; //Remember to add password after commiting

        public event PropertyChangedEventHandler? PropertyChanged;

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

        public float RiskMetric
        {
            get => _riskMetric;
            set
            {
                _riskMetric = value;
                PropertyChanged?.Invoke(this, new(nameof(RiskMetric)));
            }
        }


        //Delete This Later
        public static bool CanConnectToDatabase(string connectionString)
        {
            using (var connection = new Npgsql.NpgsqlConnection(connectionString))
            {
                try
                {
                    connection.Open();
                    Debug.WriteLine("Connected!");
                    return true;
                }
                catch (Npgsql.NpgsqlException)
                {
                    Debug.WriteLine("Nah Man");
                    return false;
                }
                catch (System.Exception)
                {
                    Debug.WriteLine("Nah Man");
                    return false;
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            //Delete This Later
            CanConnectToDatabase(ConnectionString);

            // Menu Items
            MI_Dashboard.Click += MI_Dashboard_Click;
            MI_Scans.Click += MI_Scans_Click;
            MI_Vulnerabilities.Click += MI_Vulnerabilities_Click;
            MI_Logs.Click += MI_Logs_Click;
            MI_Customization.Click += MI_Customization_Click;
            MI_Settings.Click += MI_Settings_Click;
            //Menu Items
            Btn_Chat.Click += Btn_Chat_Click;
            Btn_Send_Chat.Click += Btn_Send_Chat_Click;
            Btn_Close_Chat.Click += Btn_Close_Chat_Click;
            ChatWindow.PaneClosing += ChatWindow_PaneClosing;
            ChatWindow.PaneOpening += ChatWindow_PaneOpening;

            ChatInput.TextChanged += (s, e) =>
            {
                Btn_Send_Chat.IsEnabled = !string.IsNullOrWhiteSpace(ChatInput.Text);
            };

            Btn_Send_Chat.IsEnabled = false;

            MI_Dashboard_Click(this, new RoutedEventArgs());
        }



        private void ChatWindow_PaneClosing(object? sender, CancelRoutedEventArgs e)
        {
            Overlay.Opacity= 0;
        }

        private void ChatWindow_PaneOpening(object? sender, CancelRoutedEventArgs e)
        {
            Overlay.Opacity = 0.7;
        }

        private void MI_Dashboard_Click(object? sender, RoutedEventArgs e)
        {
            MI_Dashboard.IsChecked = true;
            MI_Scans.IsChecked = false;
            MI_Vulnerabilities.IsChecked = false;
            MI_Logs.IsChecked = false;
            MI_Customization.IsChecked = false;
            MI_Settings.IsChecked = false;

            var dashboard = new Dashboard
            {
                DataContext = this
            };

            dashboard.VulnerabilitiesRequested += () =>
            {
                MI_Vulnerabilities_Click(this, new RoutedEventArgs());
            };

            dashboard.ScansRequested += () =>
            {
                MI_Scans_Click(this, new RoutedEventArgs());
            };

            MainCC.Content = dashboard;
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

        private void Btn_Chat_Click(object? sender, RoutedEventArgs e)
        {
            ChatWindow.IsPaneOpen = true;
        }
        private void Btn_Close_Chat_Click(object? sender, RoutedEventArgs e)
        {
            ChatWindow.IsPaneOpen = false;
        }

        private void Btn_Send_Chat_Click(object? sender, RoutedEventArgs e)
        {
            AddUserMessage(ChatInput.Text);
            ChatInput.Text = string.Empty;
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private void AddUserMessage(string text)
        {
            var messageText = new SelectableTextBlock
            {
                Text = text
            };

            var bubble = new Border
            {
                Classes = { "UserChat" },
                Child = messageText
            };


            ChatPanel.Children.Add(bubble);
        }

        private void AddAIResponse(string text)
        {
            var icon = new Image();
            // Source comes from Style — DO NOT set it here

            var messageText = new SelectableTextBlock
            {
                Text = text
            };

            var bubble = new Border
            {
                Child = messageText
            };

            var wrapPanel = new WrapPanel
            {
                Children =
        {
            bubble
        }
            };

            var container = new Grid
            {
                Classes = { "AIChatBox" },
                ColumnDefinitions =
        {
            new ColumnDefinition(GridLength.Auto),
            new ColumnDefinition(GridLength.Star)
        }
            };

            Grid.SetColumn(icon, 0);
            Grid.SetColumn(wrapPanel, 1);

            container.Children.Add(icon);
            container.Children.Add(wrapPanel);

            ChatPanel.Children.Add(container);
        }
    }
}