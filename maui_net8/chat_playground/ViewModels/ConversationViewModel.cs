using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Chat;
using ChatPlayground.Models;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Maui.Core;
using ChatPlayground.Data;
using System.Text.Json;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using ChatPlayground.Services;
using ChatPlayground.Helpers;

namespace ChatPlayground.ViewModels;

public partial class ConversationViewModel : ObservableObject
{
    private readonly IAppSettingsService _appSettingsService;
    private readonly IChatPlaygroundDatabase _database;
    private readonly IPopupService _popupService;
    private readonly LMKitService _lmKitService;
    private readonly LMKitService.Conversation _lmKitConversation;

    private bool _isSynchedWithLog = true;
    private bool _pendingCancellation;

    private bool _awaitingLMKitUserMessage;
    private bool _awaitingLMKitAssistantMessage;
    private MessageViewModel? _pendingPrompt;
    private MessageViewModel? _pendingResponse;

    // Note Evan: workaround for VisualState issues with CollectionView control.
    // VisualState mechanism simply can not be relied on on Windows.
    // -> using IsSelected flag on ViewModel to trigger UI changes in the CollectionView.
    // https://github.com/dotnet/maui/issues/13056 
    // https://github.com/dotnet/maui/issues/15718#issuecomment-2185001027
    // https://github.com/dotnet/maui/issues/13197
    // https://stackoverflow.com/questions/74865843/net-maui-issue-with-visualstatemanager-on-windows-platform#comment138777368_74865843
    [ObservableProperty]
    bool _isSelected;

    [ObservableProperty]
    bool _editingTitle;

    [ObservableProperty]
    bool _isEmpty = true;

    [ObservableProperty]
    string _inputText = string.Empty;

    [ObservableProperty]
    bool _awaitingResponse;

    [ObservableProperty]
    bool _usedDifferentModel;

    [ObservableProperty]
    bool _logsLoadingInProgress;

    [ObservableProperty]
    bool _isInitialized;

    [ObservableProperty]
    public ModelInfo? _lastUsedModel;

    [ObservableProperty]
    public LmKitTextGenerationStatus _latestPromptStatus;

    public ObservableCollection<MessageViewModel> Messages { get; } = new ObservableCollection<MessageViewModel>();
    public ConversationLog ConversationLog { get; }

    private string _title;
    public string Title
    {
        get => _title;
        set
        {
            if (value != _title)
            {
                ConversationLog.Title = value;
                _title = value;
                OnPropertyChanged();

                SaveConversation();
            }
        }
    }

    public EventHandler? TextGenerationCompleted;
    public EventHandler? TextGenerationFailed;
    public EventHandler? DatabaseSaveOperationCompleted;
    public EventHandler? DatabaseSaveOperationFailed;

    public ConversationViewModel(IAppSettingsService appSettingsService, LMKitService lmKitService, IChatPlaygroundDatabase database, IPopupService popupService) : this(appSettingsService, lmKitService, database, popupService, new ConversationLog("Untitled conversation"))
    {
    }

    public ConversationViewModel(IAppSettingsService appSettingsService, LMKitService lmKitService, IChatPlaygroundDatabase database, IPopupService popupService, ConversationLog conversationLog)
    {
        _appSettingsService = appSettingsService;
        _lmKitService = lmKitService;
        _lmKitService.ConversationHistoryChanged += OnLmKitServiceConversationHistoryChanged;
        _lmKitService.ModelLoadingCompleted += OnModelLoadingCompleted;
        _database = database;
        _popupService = popupService;
        _title = conversationLog.Title!;
        _lmKitConversation = new LMKitService.Conversation(conversationLog.ChatHistoryData);

        Messages.CollectionChanged += OnMessagesCollectionChanged;
        ConversationLog = conversationLog;
        IsInitialized = conversationLog.MessageListBlob == null;
    }

    public void LoadConversationLogs()
    {
        Task.Run(() =>
        {
            try
            {
                var messages = JsonSerializer.Deserialize<List<Message>>(ConversationLog.MessageListBlob!);

                if (messages != null)
                {
                    foreach (var message in messages)
                    {
                        ConversationLog.MessageList.Add(message);
                        MainThread.BeginInvokeOnMainThread(() => Messages.Add(new MessageViewModel(message)));
                    }
                }

                if (ConversationLog.LastUsedModel != null)
                {
                    LastUsedModel = JsonSerializer.Deserialize<ModelInfo>(ConversationLog.LastUsedModel);
                    LastUsedModel!.FileUri = FileHelpers.GetModelFileUri(LastUsedModel, _appSettingsService.ModelsFolderPath);
                }
            }
            catch (Exception exception)
            {

            }

            IsInitialized = true;
        });
    }


    [RelayCommand]
    public void Send()
    {
        if (_lmKitService.ModelLoadingState == LmKitModelLoadingState.Loaded)
        {
            string prompt = InputText;
            OnNewlySubmittedPrompt(prompt);

            LMKitService.PromptResult? promptResult = null;

            Task.Run(async () =>
            {
                try
                {
                    promptResult = await _lmKitService.SubmitPrompt(_lmKitConversation, prompt);
                    OnPromptResult(promptResult);
                }
                catch (Exception ex)
                {
                    OnPromptResult(null, ex);
                }
            });
        }
        else
        {
            DisplayError("No model is currently loaded");
        }
    }

    private void OnPromptResult(LMKitService.PromptResult? promptResult, Exception? submitPromptException = null)
    {
        AwaitingResponse = false;
        LastUsedModel = _lmKitConversation.LastUsedModel;

        if (submitPromptException != null)
        {
            if (submitPromptException is OperationCanceledException operationCancelledException)
            {
                _pendingResponse!.Status = LmKitTextGenerationStatus.Cancelled;
                _pendingPrompt!.Status = LmKitTextGenerationStatus.Cancelled;
            }
            else
            {
                _pendingResponse!.Status = LmKitTextGenerationStatus.UnknownError;
                _pendingPrompt!.Status = LmKitTextGenerationStatus.UnknownError;
            }

            // todo: provide more error info with event args.
            OnTextGenerationFailure();
        }
        else if (promptResult != null)
        {
            LatestPromptStatus = promptResult.Status;
            _pendingResponse!.Status = LatestPromptStatus;
            _pendingPrompt!.Status = LatestPromptStatus;

            if (promptResult.Status == LmKitTextGenerationStatus.Undefined && promptResult.TextGenerationResult != null)
            {
                OnTextGenerationSuccess(promptResult.TextGenerationResult);

                if (!_isSynchedWithLog)
                {
                    SaveConversation();
                    _isSynchedWithLog = true;
                }
            }
            else
            {
                OnTextGenerationFailure();
            }
        }

        if (!_awaitingLMKitAssistantMessage)
        {
            _pendingResponse = null;
        }

        if (!_awaitingLMKitUserMessage)
        {
            _pendingPrompt = null;
        }

        _pendingCancellation &= false;
    }

    [RelayCommand]
    public void Cancel()
    {
        if (AwaitingResponse)
        {
            _pendingCancellation = true;
            _lmKitService.CancelPrompt(_lmKitConversation);
        }
    }

    private void OnNewlySubmittedPrompt(string prompt)
    {
        InputText = string.Empty;
        UsedDifferentModel &= false;
        LatestPromptStatus = LmKitTextGenerationStatus.Undefined;
        AwaitingResponse = true;
        _awaitingLMKitUserMessage = true;
        _awaitingLMKitAssistantMessage = true;
        _pendingPrompt = new MessageViewModel(new Message() { Sender = MessageSender.User, Text = prompt });
        _pendingResponse = new MessageViewModel(new Message() { Sender = MessageSender.Assistant }) { MessageInProgress = true };

        Messages.Add(_pendingPrompt);
        Messages.Add(_pendingResponse);

        ConversationLog.MessageList.Add(_pendingPrompt.MessageModel);
        ConversationLog.MessageList.Add(_pendingResponse.MessageModel);
    }

    private void OnModelLoadingCompleted(object? sender, EventArgs e)
    {
        if (LastUsedModel != null)
        {
            UsedDifferentModel = !LastUsedModel.Equals(_lmKitService.LMKitConfig.LoadedModel);
        }
    }

    private void OnTextGenerationSuccess(TextGenerationResult result)
    {
        TextGenerationCompleted?.Invoke(this, new TextGenerationCompletedEventArgs(result.TerminationReason));
    }

    private void OnTextGenerationFailure()
    {
        if (_pendingResponse != null)
        {
            _pendingResponse.MessageInProgress = false;
        }

        TextGenerationFailed?.Invoke(this, EventArgs.Empty);
    }

    private void OnLmKitServiceConversationHistoryChanged(object? sender, EventArgs e)
    {
        var args = (LMKitService.ConversationChatHistoryChangedEventArgs)e;

        if (args.Conversation == _lmKitConversation)
        {
            OnChatHistoryChanged(sender, args.CollectionChangedEventArgs);
        }
    }

    private void OnChatHistoryChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _isSynchedWithLog &= false;

        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            foreach (var item in e.NewItems!)
            {
                ChatHistory.Message message = (ChatHistory.Message)item;

                if (message.AuthorRole == AuthorRole.User)
                {
                    if (_pendingPrompt != null && _awaitingLMKitUserMessage)
                    {
                        _pendingPrompt.LmKitMessage = message;
                        _awaitingLMKitUserMessage = false;

                        if (!AwaitingResponse)
                        {
                            _pendingPrompt = null;
                        }
                    }
                }
                else if (message.AuthorRole == AuthorRole.Assistant)
                {
                    if (_pendingResponse != null && _awaitingLMKitAssistantMessage)
                    {
                        _pendingResponse.LmKitMessage = message;
                        _awaitingLMKitUserMessage = false;

                        if (!AwaitingResponse)
                        {
                            _pendingResponse = null;
                        }
                    }
                }
                else
                {
                    MessageViewModel messageViewModel = new MessageViewModel(message);
                    MainThread.BeginInvokeOnMainThread(() => Messages.Add(messageViewModel));
                    ConversationLog.MessageList!.Add(messageViewModel.MessageModel);
                }
            }
        }
        else if (e.Action == NotifyCollectionChangedAction.Remove)
        {
            foreach (var item in e.OldItems!)
            {
                MainThread.BeginInvokeOnMainThread(() => Messages.RemoveAt(e.OldStartingIndex));
            }
        }
    }

    private void OnMessagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        IsEmpty = Messages.Count == 0;
    }

    private async void DisplayError(string message)
    {
        await App.Current!.MainPage!.DisplayAlert("Error", message, "OK");
    }

    private void SaveConversation()
    {
        Task.Run(async () =>
        {
            try
            {
                if (_lmKitConversation.ChatHistory != null)
                {
                    ConversationLog.ChatHistoryData = _lmKitConversation.ChatHistory.Serialize();
                    _lmKitConversation.LatestChatHistoryData = ConversationLog.ChatHistoryData;
                }

                ConversationLog.LastUsedModel = JsonSerializer.Serialize(LastUsedModel);
                ConversationLog.MessageListBlob = JsonSerializer.Serialize(ConversationLog.MessageList);
                await _database.SaveConversation(ConversationLog);

                DatabaseSaveOperationCompleted?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                DatabaseSaveOperationFailed?.Invoke(this, EventArgs.Empty);
            }
        });
    }


    public sealed class TextGenerationCompletedEventArgs : EventArgs
    {
        public TextGenerationResult.StopReason StopReason { get; }

        public TextGenerationCompletedEventArgs(TextGenerationResult.StopReason stopReason)
        {
            StopReason = stopReason;
        }
    }
}
