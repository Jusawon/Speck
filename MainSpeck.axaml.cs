using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using LLama;
using LLama.Common;
using LLama.Sampling;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Speck
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private int _scanCount;
        private int _vulnCount;
        private float _riskMetric;
        private bool _ableToChat = false;
        private bool _loadingResp;

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

        private void UpdateSendButtonState()
        {
            Btn_Send_Chat.IsEnabled = !_loadingResp && _ableToChat;
        }

        private string modelPath;

        private Scans scans;
        private LLamaWeights SpeckModel;
        private LLamaContext _context;
        private InteractiveExecutor _executor;
        private ConversationHistory _history;

        public event PropertyChangedEventHandler? PropertyChanged;


        public MainWindow()
        {
            InitializeComponent();

            LoadConfig();

            ChatInput.AddHandler(
            Avalonia.Input.InputElement.KeyDownEvent,
            ChatInput_KeyDown,
            Avalonia.Interactivity.RoutingStrategies.Tunnel);
            scans = new Scans();

            //============================ Model Loading ===============================

            InitializeModelAsync();

            //==========================================================================

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
                if (!string.IsNullOrWhiteSpace(ChatInput.Text)) AbleToChat = true;
                else AbleToChat = false;
            };

            Btn_Send_Chat.IsEnabled = false;

            MI_Dashboard_Click(this, new RoutedEventArgs());
        }



        private void LoadConfig()
        {
            //Load Config Json later
        }

        public class ConversationMessage
        {
            public AuthorRole Role { get; set; }
            public string Content { get; set; }
            public DateTime Timestamp { get; set; } = DateTime.Now;
        }

        public class ConversationHistory
        {
            public List<ConversationMessage> Messages { get; set; } = new List<ConversationMessage>();

            public void AddMessage(AuthorRole role, string content)
            {
                Messages.Add(new ConversationMessage { Role = role, Content = content });
            }

            public void Clear()
            {
                Messages.Clear();
            }
        }

        // Track loading indicators

        public async Task InitializeModelAsync()
        {
            modelPath = Path.Combine(AppContext.BaseDirectory, "AI", "speck-ai.gguf");

            if (!File.Exists(modelPath))
            {
                Debug.WriteLine("Model file not found!");
                return;
            }

            var parameters = new ModelParams(modelPath)
            {
                ContextSize = 8192,
                BatchSize = 512,
                GpuLayerCount = -1  // All layers to GPU
            };

            try
            {
                Debug.WriteLine("Loading model...");
                SpeckModel = LLamaWeights.LoadFromFile(parameters);
                _context = SpeckModel.CreateContext(parameters);
                _executor = new InteractiveExecutor(_context);

                _history = new ConversationHistory();

                Debug.WriteLine("Model loaded successfully.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Model loading failed: {ex.Message}");
            }
        }

        public async Task ProcessUserInputAsync(string userInput)
        {
            LoadingResp = true;

            if (_executor == null)
            {
                AddAIResponse("Model is not initialized. Please wait for loading to complete.");
                LoadingResp = false;
                return;
            }

            try
            {
                // Add user message to UI
                AddUserMessage(userInput);

                ChatScrollViewer.Offset =
                    new Avalonia.Vector(
                        ChatScrollViewer.Offset.X,
                        ChatScrollViewer.Extent.Height
                    );

                // Add user message to conversation history
                _history.AddMessage(AuthorRole.User, userInput);

                // Build the full prompt with system prompt and limited conversation history
                var fullPrompt = BuildPrompt();

                // Configure inference parameters
                var inferenceParams = new InferenceParams()
                {
                    SamplingPipeline = new DefaultSamplingPipeline()
                    {
                        Temperature = 0.7f,
                    },
                    AntiPrompts = new List<string> { "User:", "###" },
                    MaxTokens = 500,
                };

                // Create a temporary message UI element for streaming
                var aiMessageContainer = CreateAIMessageUI(". . .");
                var messageTextBlock = FindMessageTextBlock(aiMessageContainer); // You'll need this helper method
                ChatPanel.Children.Add(aiMessageContainer);

                // Get AI response with streaming
                var aiResponse = new StringBuilder();
                var lastUpdate = DateTime.Now;

                await foreach (var token in _executor.InferAsync(fullPrompt, inferenceParams))
                {
                    aiResponse.Append(token);

                    // Update UI periodically to avoid excessive updates
                    if ((DateTime.Now - lastUpdate).TotalMilliseconds > 50) // Update every 50ms
                    {
                        var currentText = CleanResponse(aiResponse.ToString().Trim());
                        messageTextBlock.Text = currentText;

                        // Scroll to bottom
                        ChatScrollViewer.Offset =
                            new Avalonia.Vector(
                                ChatScrollViewer.Offset.X,
                                ChatScrollViewer.Extent.Height
                            );

                        lastUpdate = DateTime.Now;
                    }
                }

                var finalResponse = CleanResponse(aiResponse.ToString().Trim());
                messageTextBlock.Text = finalResponse;


                // Add final response to conversation history
                _history.AddMessage(AuthorRole.Assistant, finalResponse);

                // Scroll to bottom one final time
                ChatScrollViewer.Offset =
                    new Avalonia.Vector(
                        ChatScrollViewer.Offset.X,
                        ChatScrollViewer.Extent.Height
                    );
            }
            finally
            {
                LoadingResp = false;
            }
        }

        // Helper method to create AI message UI
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

            var icon = new Image();
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

        private string CleanResponse(string response)
        {
            // Remove anything after the first occurrence of "User:" or other assistant tags
            var userIndex = response.IndexOf("User:");
            if (userIndex >= 0)
            {
                response = response.Substring(0, userIndex).TrimEnd();
            }

            var assistantIndex = response.IndexOf("<|im_start|>assistant");
            if (assistantIndex >= 0)
            {
                response = response.Substring(0, assistantIndex).TrimEnd();
            }

            var assistantTagIndex = response.IndexOf("Assistant:");
            if (assistantTagIndex >= 0)
            {
                response = response.Substring(0, assistantTagIndex).TrimEnd();
            }

            // Remove trailing whitespace and special characters
            response = response.TrimEnd('\n', '\r', ' ', ':', '<', '|', 'i', 'm', '_', 's', 't', 'a', 'r', 't', '>', 'e', 'n', 'd');

            return response;
        }

        private string BuildPrompt()
        {
            var promptBuilder = new StringBuilder();

            var systemPrompt = "You are Speck, an experienced cybersecurity expert with a unique personality. " +
                              "You analyze vulnerabilities with deep technical knowledge while maintaining a friendly, approachable tone. " +
                              "Explain with occasional chicken sounds (*Pock* *Pock*). " +
                              "Think step-by-step: identify the vulnerability type, explain the risk, describe the impact, and suggest specific mitigations.";

            //var systemPrompt = "You are Speck, an experienced cybersecurity expert with a unique personality. " +
            //                  "You analyze vulnerabilities with deep technical knowledge while maintaining a friendly, approachable tone. " +
            //                  "Explain with occasional chicken sounds (*Pock* *Pock*). " +
            //                  "Provide practical, actionable advice that general users can implement immediately. " +
            //                  "Use varied sentence structures and speak conversationally while remaining professional. " +
            //                  "When discussing patches, focus on explaining the core concept rather than saying 'No patch available'. " +
            //                  "Think step-by-step: identify the vulnerability type, explain the risk, describe the impact, and suggest specific mitigations." +
            //                  "If no mitigation steps are found, answer with the patch method or no ways to mitigate vulnerability for now";

            promptBuilder.AppendLine($"<|im_start|>system");
            promptBuilder.AppendLine($"{systemPrompt}<|im_end|>");

            var recentMessages = _history.Messages.TakeLast(2).ToList();

            foreach (var message in recentMessages)
            {
                switch (message.Role)
                {
                    case AuthorRole.User:
                        promptBuilder.AppendLine($"<|im_start|>user");
                        promptBuilder.AppendLine($"{message.Content}<|im_end|>");
                        break;
                    case AuthorRole.Assistant:
                        promptBuilder.AppendLine($"<|im_start|>assistant");
                        promptBuilder.AppendLine($"{message.Content}<|im_end|>");
                        break;
                }
            }

            promptBuilder.AppendLine($"<|im_start|>assistant");

            return promptBuilder.ToString();
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
            if (Btn_Send_Chat.IsEnabled) {
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
            var icon = new Image();
            // Source comes from Style — DO NOT set it here

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

    }
}