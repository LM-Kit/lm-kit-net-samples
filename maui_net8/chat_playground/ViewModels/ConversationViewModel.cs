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

namespace ChatPlayground.ViewModels;

public partial class ConversationViewModel : ObservableObject
{
    private readonly IChatPlaygroundDatabase _database;
    private readonly IPopupService _popupService;
    private readonly LMKitService _lmKitService;

    private bool _isSynchedWithLog = true;
    private bool _pendingCancellation;

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
    public LmKitTextGenerationSatus _latestPromptStatus;

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

    public ConversationViewModel(LMKitService lmKitService, IChatPlaygroundDatabase database, IPopupService popupService) : this(lmKitService, database, popupService, new ConversationLog("Untitled conversation"))
    {
    }

    public ConversationViewModel(LMKitService lmKitService, IChatPlaygroundDatabase database, IPopupService popupService, ConversationLog conversationLog)
    {
        _lmKitService = lmKitService;
        _lmKitService.ConversationHistoryChanged += OnLmKitServiceConversationHistoryChanged;
        _database = database;
        _popupService = popupService;
        _title = conversationLog.Title!;
        Messages.CollectionChanged += OnMessagesCollectionChanged;
        ConversationLog = conversationLog;
        IsInitialized = conversationLog.MessageListBlob == null;
    }

    public void LoadConversationLogs()
    {
        if (ConversationLog.MessageListBlob == null)
        {
            return;
        }

        Task.Run(() =>
        {
            try
            {
                var messages = JsonSerializer.Deserialize<List<Message>>(ConversationLog.MessageListBlob);
                //Thread.Sleep(10000);
                if (messages != null)
                {
                    foreach (var message in messages)
                    {
                        ConversationLog.MessageList.Add(message);
                        MainThread.BeginInvokeOnMainThread(() => Messages.Add(new MessageViewModel(message)));
                    }
                }
            }
            catch (Exception exception)
            {
                //_logger.LogError(exception, "Failed to deserialize conversation's messages");
            }

            IsInitialized = true;
        });
    }


    [RelayCommand]
    public void Send()
    {
        if (_lmKitService.ModelLoadingState == ModelLoadingState.Loaded)
        {
            string prompt = InputText;
            OnNewlySubmittedPrompt(prompt);

            LMKitService.PromptResult? promptResult = null;
            Exception? submitPromptException = null;

            Task.Run(async () =>
            {
                try
                {
                    promptResult = await _lmKitService.SubmitPrompt(ConversationLog, prompt);
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

        if (submitPromptException != null)
        {
            if (submitPromptException is OperationCanceledException operationCancelledException)
            {
                _pendingResponse!.Status = LmKitTextGenerationSatus.Cancelled;
                _pendingPrompt!.Status = LmKitTextGenerationSatus.Cancelled;
            }
            else
            {
                _pendingResponse!.Status = LmKitTextGenerationSatus.UnknownError;
                _pendingPrompt!.Status = LmKitTextGenerationSatus.UnknownError;
            }

            // todo: provide more error info with event args.
            OnTextGenerationFailure();
        }
        else if (promptResult != null)
        {
            LatestPromptStatus = promptResult.Status;
            _pendingResponse!.Status = LatestPromptStatus;
            _pendingPrompt!.Status = LatestPromptStatus;

            if (promptResult.Status == LmKitTextGenerationSatus.Undefined && promptResult.TextGenerationResult != null)
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

        _pendingResponse = null;
        _pendingPrompt = null;
        _pendingCancellation &= false;
    }

    [RelayCommand]
    public void Cancel()
    {
        if (AwaitingResponse)
        {
            _pendingCancellation = true;
            _lmKitService.CancelPrompt(ConversationLog);
        }
    }

    private async Task SubmitPrompt(string prompt)
    {


        //if (promptResult?.Exception != null)
        //{
        //    if (promptResult.Exception is OperationCanceledException)
        //    {
        //        LatestPromptStatus = _pendingCancellation ? PromptResponseStatus.Cancelled : PromptResponseStatus.TimedOut;
        //    }
        //    else if (promptResult.Exception is TimeoutException)
        //    {
        //        LatestPromptStatus = PromptResponseStatus.TimedOut;
        //    }
        //    else
        //    {
        //        LatestPromptStatus = PromptResponseStatus.UnknownError;
        //    }
        //}
    }

    private void OnNewlySubmittedPrompt(string prompt)
    {
        InputText = string.Empty;
        UsedDifferentModel &= false;
        LastUsedModel = _lmKitService.LMKitConfig.LoadedModel;
        LatestPromptStatus = LmKitTextGenerationSatus.Undefined;
        AwaitingResponse = true;

        _pendingPrompt = new MessageViewModel(new Message() { Sender = MessageSender.User, Text = prompt });
        _pendingResponse = new MessageViewModel(new Message() { Sender = MessageSender.Assistant }) { MessageInProgress = true };

        Messages.Add(_pendingPrompt);
        Messages.Add(_pendingResponse);

        try
        {
            ConversationLog.LastUsedModel = JsonSerializer.Serialize(LastUsedModel);
        }
        catch (Exception exception)
        {
            //LogError(exception, "Failed to serialize conversation's associated model info");
        }

        ConversationLog.MessageList.Add(_pendingPrompt.MessageModel);
        ConversationLog.MessageList.Add(_pendingResponse.MessageModel);
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

        if (args.Conversation == ConversationLog)
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
                    if (_pendingPrompt != null)
                    {
                        _pendingPrompt.LmKitMessage = message;
                    }
                    else
                    {
#if DEBUG
                        throw new NotImplementedException();
#endif
                    }
                }
                else if (message.AuthorRole == AuthorRole.Assistant)
                {
                    if (_pendingResponse != null)
                    {
                        _pendingResponse.LmKitMessage = message;
                    }
                    else
                    {
#if DEBUG
                        throw new NotImplementedException();
#endif
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
        //CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        //var snackbarOptions = new SnackbarOptions
        //{
        //    BackgroundColor = Colors.Red,
        //    TextColor = Colors.Green,
        //    ActionButtonTextColor = Colors.Yellow,
        //    CornerRadius = new CornerRadius(10),
        //    Font = Microsoft.Maui.Font.SystemFontOfSize(14),
        //    ActionButtonFont = Microsoft.Maui.Font.SystemFontOfSize(14),
        //    CharacterSpacing = 0.5
        //};

        ////Action action = async () => await DisplayAlert("Snackbar ActionButton Tapped", "The user has tapped the Snackbar ActionButton", "OK");
        //TimeSpan duration = TimeSpan.FromSeconds(3);

        //var snackbar = Snackbar.Make(message, null, string.Empty, duration, snackbarOptions);

        //await snackbar.Show(cancellationTokenSource.Token);

        await App.Current!.MainPage!.DisplayAlert("Error", message, "OK");
    }

    private void SaveConversation()
    {
        Task.Run(async () =>
        {
            try
            {
                await _database.SaveConversation(ConversationLog);
            }
            catch (Exception ex)
            {

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
