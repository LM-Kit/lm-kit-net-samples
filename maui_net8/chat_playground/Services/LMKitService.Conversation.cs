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
    }
}
