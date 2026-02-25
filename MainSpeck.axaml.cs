using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Svg.Skia;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Speck
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private bool _ableToChat = false;
        private bool _loadingResp;
        public static string SpeckIcon;
        private readonly string _configPath = Path.Combine(AppContext.BaseDirectory, "config.json");


        private bool AbleToChat
        {
            get { return _ableToChat; }
            set
            {
                _ableToChat = value;
                UpdateSendButtonState();
            }
        }

        private bool LoadingResp
        {
            get { return _loadingResp; }
            set
            {
                _loadingResp = value;
                UpdateSendButtonState();
            }
        }

        public class ChatRequest
        {
            public string message { get; set; }
        }

        public class ChatResponse
        {
            public string response { get; set; }
        }

        private void UpdateSendButtonState()
        {
            Btn_Send_Chat.IsEnabled = !_loadingResp && _ableToChat;
        }

        private string modelPath;

        private Scans scans;

        public event PropertyChangedEventHandler? PropertyChanged;
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(120)
        };

        public MainWindow()
        {
            InitializeComponent();


            LoadConfig();
            Glb.LoadSpecks();

            ChatInput.AddHandler(
            Avalonia.Input.InputElement.KeyDownEvent,
            ChatInput_KeyDown,
            Avalonia.Interactivity.RoutingStrategies.Tunnel);
            scans = new Scans();

            this.Closing += (_, __) =>
            {
                SaveConfig();
            };

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
            MainCC.PropertyChanged += MainCC_PropertyChanged;

            ChatInput.TextChanged += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(ChatInput.Text)) AbleToChat = true;
                else AbleToChat = false;
            };

            Btn_Send_Chat.IsEnabled = false;

            MI_Dashboard_Click(this, new RoutedEventArgs());
        }



        public class AppConfig
        {
            public string ConfSpeckBreed { get; set; } = string.Empty;
            public string ConfExportType { get; set; } = string.Empty;
        }

        private void LoadConfig()
        {
            try
            {
                if (!File.Exists(_configPath))
                {
                    var defaultConfig = new AppConfig
                    {
                        ConfSpeckBreed = Glb.SpeckBreed,
                        ConfExportType = Glb.ExportType
                    };

                    var defaultJson = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                    File.WriteAllText(_configPath, defaultJson);

                    Glb.SpeckBreed = defaultConfig.ConfSpeckBreed;
                    Glb.ExportType = defaultConfig.ConfExportType;

                    return;
                }

                var json = File.ReadAllText(_configPath);

                var config = JsonSerializer.Deserialize<AppConfig>(json);

                if (config != null)
                {
                    Glb.SpeckBreed = config.ConfSpeckBreed;
                    Glb.ExportType = config.ConfExportType;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Config load error: {ex.Message}");
            }
        }

        private void SaveConfig()
        {
            try
            {
                var config = new AppConfig
                {
                    ConfSpeckBreed = Glb.SpeckBreed,
                    ConfExportType = Glb.ExportType
                };

                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(_configPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Config save error: {ex.Message}");
            }
        }

        public async Task ProcessUserInputAsync(string userInput)
        {
            LoadingResp = true;

            try
            {
                // Add user message to UI
                AddUserMessage(userInput);

                ChatScrollViewer.Offset =
                    new Avalonia.Vector(
                        ChatScrollViewer.Offset.X,
                        ChatScrollViewer.Extent.Height
                    );

                // Create temporary AI message container
                var aiMessageContainer = CreateAIMessageUI(". . .");
                var messageTextBlock = FindMessageTextBlock(aiMessageContainer);
                messageTextBlock.Foreground = Brushes.Gray;
                ChatPanel.Children.Add(aiMessageContainer);

                // Prepare HTTP request
                var requestObj = new ChatRequest
                {
                    message = userInput
                };

                var json = JsonSerializer.Serialize(requestObj);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Call FastAPI
                var response = await _httpClient.PostAsync(
                    "http://127.0.0.1:8000/chat",
                    content
                );

                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ChatResponse>(responseJson);

                var fullResponse = result?.response ?? "No response received.";

                // 🔥 Simulated streaming directly here
                var builder = new StringBuilder();
                messageTextBlock.Foreground = Brushes.Black;
                foreach (char c in fullResponse)
                {
                    builder.Append(c);
                    messageTextBlock.Text = builder.ToString();

                    ChatScrollViewer.Offset =
                        new Avalonia.Vector(
                            ChatScrollViewer.Offset.X,
                            ChatScrollViewer.Extent.Height
                        );

                    await Task.Delay(10); // adjust typing speed here
                }
            }
            catch (Exception ex)
            {
                AddAIResponse($"Error: {ex.Message}");
            }
            finally
            {
                LoadingResp = false;
            }
        }

        private Grid CreateAIMessageUI(string initialText)
        {
            var messageText = new SelectableTextBlock
            {
                Text = initialText
            };

            var bubble = new Avalonia.Controls.Border
            {
                Child = messageText
            };

            var wrapPanel = new WrapPanel
            {
                Children = { bubble }
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

            var Resource = SvgSource.Load(SpeckIcon);

            var icon = new Image
            {

                Source = new SvgImage { Source = Resource }
            };

            Grid.SetColumn(icon, 0);
            Grid.SetColumn(wrapPanel, 1);

            container.Children.Add(icon);
            container.Children.Add(wrapPanel);

            return container;
        }

        // Helper method to find the SelectableTextBlock within the container
        private SelectableTextBlock FindMessageTextBlock(Grid container)
        {
            foreach (var child in container.Children)
            {
                if (child is WrapPanel wrapPanel)
                {
                    foreach (var wrapChild in wrapPanel.Children)
                    {
                        if (wrapChild is Border border && border.Child is SelectableTextBlock textBlock)
                        {
                            return textBlock;
                        }
                    }
                }
            }
            return null;
        }

        private void ChatWindow_PaneClosing(object? sender, CancelRoutedEventArgs e)
        {
            Overlay.Opacity = 0;
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

            MainCC.Content = scans;
        }

        private void MI_Vulnerabilities_Click(object? sender, RoutedEventArgs e)
        {
            MI_Vulnerabilities.IsChecked = true;
            MI_Dashboard.IsChecked = false;
            MI_Scans.IsChecked = false;
            MI_Logs.IsChecked = false;
            MI_Customization.IsChecked = false;
            MI_Settings.IsChecked = false;

            var vulnerabilities = new Vulnerabilities
            {
                DataContext = this
            };

            vulnerabilities.OpenChat += (quest) =>
            {
                Btn_Chat_Click(this, new RoutedEventArgs());
                ChatInput.Text = String.Empty;
                ChatInput.Text = quest;
            };

            MainCC.Content = vulnerabilities;
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

        private void MI_Settings_Click(object? sender, RoutedEventArgs e)
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
            if (Btn_Send_Chat.IsEnabled)
            {
                ProcessUserInputAsync(ChatInput.Text);
                ChatInput.Text = string.Empty;
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public void AddUserMessage(string text)
        {
            var messageText = new SelectableTextBlock
            {
                Text = text
            };

            var bubble = new Avalonia.Controls.Border
            {
                Classes = { "UserChat" },
                Child = messageText
            };


            ChatPanel.Children.Add(bubble);
        }

        private void AddAIResponse(string text)
        {
            var Resource = SvgSource.Load(SpeckIcon);

            var icon = new Image
            {

                Source = new SvgImage { Source = Resource }
            };

            var messageText = new SelectableTextBlock
            {
                Text = text
            };

            var bubble = new Avalonia.Controls.Border
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

        private void ChatInput_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                e.Handled = true;
                Btn_Send_Chat_Click(sender, e);
            }
        }

        private void MainCC_PropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
        {
            scans.LoadImage();
        }
    }
}