using System.Collections.Specialized;
using System.ComponentModel;
using LMKit.TextGeneration.Chat;

namespace ChatPlayground.Services;

public partial class LMKitService
{
    public sealed class Conversation : INotifyPropertyChanged
    {
        private ChatHistory? _chatHistory;
        public ChatHistory? ChatHistory
        {
            get => _chatHistory;
            set
            {
                if (_chatHistory != value)
                {
                    if (_chatHistory != null)
                    {
                        ((INotifyCollectionChanged)_chatHistory.Messages).CollectionChanged -= OnChatHistoryMessageCollectionChanged;
                    }

                    _chatHistory = value;

                    if (_chatHistory != null)
                    {
                        ((INotifyCollectionChanged)_chatHistory.Messages).CollectionChanged += OnChatHistoryMessageCollectionChanged;
                    }

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ChatHistory)));
                }
            }
        }

        private string? _generatedTitle;
        public string? GeneratedTitleSummary
        {
            get => _generatedTitle;
            set
            {
                if (_generatedTitle != value)
                {
                    _generatedTitle = value;

                    if (_generatedTitle != null)
                    {
                        SummaryTitleGenerated?.Invoke(this, EventArgs.Empty);
                    }

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GeneratedTitleSummary)));
                }
            }
        }

        private Uri? _lastUsedModelUri;
        public Uri? LastUsedModelUri
        {
            get => _lastUsedModelUri;
            set
            {
                if (_lastUsedModelUri != value)
                {
                    _lastUsedModelUri = value;

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LastUsedModelUri)));
                }
            }
        }

        private byte[]? _latestChatHistoryData;
        public byte[]? LatestChatHistoryData
        {
            get => _latestChatHistoryData;
            set
            {
                if (_latestChatHistoryData != value)
                {
                    _latestChatHistoryData = value;

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LatestChatHistoryData)));
                }
            }
        }

        public event EventHandler? SummaryTitleGenerated;
        public event NotifyCollectionChangedEventHandler? ChatHistoryChanged;
        public event PropertyChangedEventHandler? PropertyChanged;

        public Conversation(LMKitService lmKitService, byte[]? latestChatHistoryData = null)
        {
            lmKitService.ModelUnloaded += OnModelUnloaded;
            LatestChatHistoryData = latestChatHistoryData;
        }

        private void OnModelUnloaded(object? sender, EventArgs e)
        {
            if (ChatHistory != null)
            {
                // Making sure that the chat history is re-built with the next loaded model information.
                ChatHistory = null;
            }
        }

        private void OnChatHistoryMessageCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            ChatHistoryChanged?.Invoke(this, e);
        }

        public void SetGeneratedTitle(PromptResult textGenerationResult)
        {
            string? conversationTopic = null;

            if (textGenerationResult.TextGenerationResult != null && !string.IsNullOrEmpty(textGenerationResult.TextGenerationResult.Completion))
            {
                foreach (var sentance in textGenerationResult.TextGenerationResult.Completion.Split('\n'))
                {
                    if (sentance.ToLower().StartsWith("topic"))
                    {
                        conversationTopic = sentance.Substring("topic".Length, sentance.Length - "topic".Length);
                        break;
                    }
                    else if (sentance.ToLower().StartsWith("the topic of the sentance is"))
                    {
                        conversationTopic = sentance.Substring("the topic of the sentance is".Length, sentance.Length - "the topic of the sentance is".Length);
                        break;
                    }
                    else if (sentance.ToLower().StartsWith("the topic of this sentance is"))
                    {
                        conversationTopic = sentance.Substring("the topic of this sentance is".Length, sentance.Length - "the topic of this sentance is".Length);
                        break;
                    }
                }
            }

            if (conversationTopic != null)
            {
                conversationTopic = conversationTopic.TrimStart(' ').TrimStart(':').TrimStart(' ').TrimStart('\'').TrimEnd('.').TrimEnd('\'');
            }

            var firstUserMessage = ChatHistory!.Messages.FirstOrDefault(message => message.AuthorRole == AuthorRole.User);

            GeneratedTitleSummary = !string.IsNullOrWhiteSpace(conversationTopic) ? conversationTopic : firstUserMessage?.Content ?? "Untitled conversation";
        }
    }
}
