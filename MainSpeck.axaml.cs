using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
        private bool _ableToChat;
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
        private readonly Dictionary<string, Control> _loadingControls = new Dictionary<string, Control>();

        private string AddLoadingIndicator()
        {
            var loadingId = Guid.NewGuid().ToString();

            // Create loading message UI element
            var messageText = new SelectableTextBlock
            {
                Text = "Thinking...",
                Name = $"Loading_{loadingId}"
            };

            var bubble = new Avalonia.Controls.Border
            {
                Classes = { "AIChat" },
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

            ChatPanel.Children.Add(container);

            // Store reference to the container
            _loadingControls[loadingId] = container;

            return loadingId;
        }

        private void RemoveLoadingIndicator(string loadingId)
        {
            if (_loadingControls.TryGetValue(loadingId, out var control))
            {
                ChatPanel.Children.Remove(control);
                _loadingControls.Remove(loadingId);
            }
        }


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
                ContextSize = 4096,
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
                AbleToChat = true;
                return;
            }

            // Trim history before adding new message to prevent overflow
            TrimHistoryIfNeeded();

            // Add user message to UI
            AddUserMessage(userInput);

            // Add loading indicator
            var loadingIndicatorId = AddLoadingIndicator();
            ChatScrollViewer.Offset =
                new Avalonia.Vector(
                    ChatScrollViewer.Offset.X,
                    ChatScrollViewer.Extent.Height
                );

            // Add user message to conversation history
            _history.AddMessage(AuthorRole.User, userInput);

            try
            {
                // Build the full prompt with system prompt and conversation history
                var fullPrompt = BuildPrompt();

                // Configure inference parameters
                var inferenceParams = new InferenceParams()
                {
                    SamplingPipeline = new DefaultSamplingPipeline()
                    {
                        Temperature = 0.7f,
                    },
                    AntiPrompts = new List<string> { "User:", "###" },
                    MaxTokens = 200,
                };

                // Get AI response
                var aiResponse = new StringBuilder();

                await foreach (var token in _executor.InferAsync(fullPrompt, inferenceParams))
                {
                    aiResponse.Append(token);
                }

                var responseText = aiResponse.ToString().Trim();

                // Remove loading indicator and add response
                RemoveLoadingIndicator(loadingIndicatorId);
                AddAIResponse(responseText);

                // Add AI response to conversation history
                _history.AddMessage(AuthorRole.Assistant, responseText);

                ChatScrollViewer.Offset =
                new Avalonia.Vector(
                    ChatScrollViewer.Offset.X,
                    ChatScrollViewer.Extent.Height
                );
            }
            catch (Exception ex)
            {
                // Remove loading indicator and show error
                RemoveLoadingIndicator(loadingIndicatorId);
                AddAIResponse($"Error generating response: {ex.Message}");

                // Log the full exception for debugging
                Debug.WriteLine($"Exception details: {ex}");

                // If there's a context error, reset the context
                try
                {
                    _context = SpeckModel.CreateContext(new ModelParams(modelPath)
                    {
                        ContextSize = 4096,
                        BatchSize = 512,
                        GpuLayerCount = -1
                    });
                    _executor = new InteractiveExecutor(_context);
                }
                catch
                {
                    // If reset fails, the model is probably in an unrecoverable state
                }
            }
            finally
            {
                LoadingResp = false;
            }
        }

        public void TrimHistoryIfNeeded()
        {
            const int maxMessages = 10; // Reduced to be more conservative
            const int threshold = 8;    // Start trimming when we reach this count

            if (_history.Messages.Count > threshold)
            {
                // Keep only the most recent messages, ensuring we keep the system context manageable
                var recentMessages = _history.Messages.Skip(Math.Max(0, _history.Messages.Count - maxMessages)).ToList();
                _history.Messages.Clear();
                _history.Messages.AddRange(recentMessages);
            }
        }

        private string BuildPrompt()
        {
            var promptBuilder = new StringBuilder();

            // Add system prompt
            promptBuilder.AppendLine("System: You are Speck, an experienced cybersecurity expert with a unique personality. " +
                                    "You analyze vulnerabilities with deep technical knowledge while maintaining a friendly, approachable tone. " +
                                    "Explain with occasional chicken sounds (*Pock* *Pock*). Don't overdo it " +
                                    "Provide practical, actionable advice that general users can implement immediately. " +
                                    "Use varied sentence structures and speak conversationally while remaining professional. " +
                                    "When discussing patches, focus on explaining the core concept rather than saying 'No patch available'. " +
                                    "Think step-by-step: identify the vulnerability type, explain the risk, describe the impact, and suggest specific mitigations." +
                                    "If no mitigation steps are found, answer with the patch method or no ways to mitigate vulnerability for now" +
                                    "Exclude 'diff' in your responses" +
                                    "When asked about who you are, explain who you are; no need to tell step-by-step or path method as you are explaining yourself");
            promptBuilder.AppendLine();

            // Limit conversation history to prevent context overflow
            var recentMessages = _history.Messages.TakeLast(10).ToList(); // Keep only last 10 messages

            foreach (var message in recentMessages)
            {
                switch (message.Role)
                {
                    case AuthorRole.User:
                        promptBuilder.AppendLine($"User: {message.Content}");
                        break;
                    case AuthorRole.Assistant:
                        promptBuilder.AppendLine($"Assistant: {message.Content}");
                        break;
                }
            }

            // Prepare for next response
            promptBuilder.Append("Assistant:");

            return promptBuilder.ToString();
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

            vulnerabilities.OpenChat+= (quest) =>
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
            ProcessUserInputAsync(ChatInput.Text);
            ChatInput.Text = string.Empty;
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
    }
}