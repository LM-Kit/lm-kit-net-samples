using CommunityToolkit.Mvvm.ComponentModel;
using LMKit.TextGeneration.Chat;
using ChatPlayground.Models;
using CommunityToolkit.Mvvm.Input;

namespace ChatPlayground.ViewModels;
public partial class MessageViewModel : ObservableObject
{
    private ChatHistory.Message? _lmKitMessage;

    public ChatHistory.Message LmKitMessage
    {
        get => _lmKitMessage;
        set
        {
            _lmKitMessage = value;

            if (_lmKitMessage != null)
            {
                MessageInProgress = !_lmKitMessage.IsProcessed;
                Sender = AuthorRoleToMessageSender(_lmKitMessage.AuthorRole);
                Text = _lmKitMessage.Content;
                MessageModel.Text = Text;
                MessageModel.Sender = Sender;
                _lmKitMessage.PropertyChanged += OnMessagePropertyChanged;
            }

            OnPropertyChanged();
        }
    }

    public Message MessageModel { get; }

    [ObservableProperty]
    private MessageSender _sender;

    [ObservableProperty]
    private string _text;

    [ObservableProperty]
    private bool _messageInProgress;

    [ObservableProperty]
    private LmKitTextGenerationStatus _status;

    [ObservableProperty]
    private bool _isHovered;

    public event EventHandler? MessageContentUpdated;

    public MessageViewModel(ChatHistory.Message message)
    {
        MessageModel = new Message();
        LmKitMessage = message;
    }

    public MessageViewModel(Models.Message message)
    {
        MessageModel = message;
        Sender = message.Sender;
        Text = message.Text;
    }


    [RelayCommand]
    private void ToggleHoveredState()
    {
        IsHovered = !IsHovered;
    }

    private void OnMessagePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatHistory.Message.IsProcessed))
        {
            MessageInProgress = !LmKitMessage.IsProcessed;
        }
        else if (e.PropertyName == nameof(ChatHistory.Message.AuthorRole))
        {
            Sender = AuthorRoleToMessageSender(LmKitMessage.AuthorRole);
            MessageModel.Sender = Sender;
        }
        else if (e.PropertyName == nameof(ChatHistory.Message.Content))
        {
            Text = LmKitMessage.Content;
            MessageModel.Text = Text;

            MessageContentUpdated?.Invoke(this, EventArgs.Empty);
        }
    }

    private static MessageSender AuthorRoleToMessageSender(AuthorRole authorRole)
    {
        switch (authorRole)
        {
            case AuthorRole.User:
                return MessageSender.User;

            case AuthorRole.Assistant:
                return MessageSender.Assistant;

            default:
                return MessageSender.Undefined;
        }
    }
}