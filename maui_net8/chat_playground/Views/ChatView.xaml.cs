using ChatPlayground.Services;
using ChatPlayground.ViewModels;
using LMKit.TextGeneration;
using System.Diagnostics;

namespace ChatPlayground.Views;

public partial class ChatView : ContentView
{
    double? _previousScrollY = null;
    bool _nextScrollViewResizeCausedByBindingContextChange;
    bool _nextScrollViewResizeCausedByWindowResize;
    bool _shouldEnforceAutoScroll;

    private ConversationViewModel? _previouslySelectedConversation;

    private TextGenerationResult.StopReason? _latestStopReason;

    public static readonly BindableProperty IsScrolledToEndProperty = BindableProperty.Create(nameof(IsScrolledToEnd), typeof(bool), typeof(ChatView), defaultValue: true);
    public bool IsScrolledToEnd
    {
        get => (bool)GetValue(IsScrolledToEndProperty);
        private set => SetValue(IsScrolledToEndProperty, value);
    }

    public static readonly BindableProperty ChatEntryIsFocusedProperty = BindableProperty.Create(nameof(ChatEntryIsFocused), typeof(bool), typeof(ChatView));
    public bool ChatEntryIsFocused
    {
        get => (bool)GetValue(ChatEntryIsFocusedProperty);
        private set => SetValue(ChatEntryIsFocusedProperty, value);
    }

    public static readonly BindableProperty ShowLatestCompletionResultProperty = BindableProperty.Create(nameof(ShowLatestCompletionResult), typeof(bool), typeof(ChatView));
    public bool ShowLatestCompletionResult
    {
        get => (bool)GetValue(ShowLatestCompletionResultProperty);
        private set => SetValue(ShowLatestCompletionResultProperty, value);
    }

    public static readonly BindableProperty? LatestStopReasonProperty = BindableProperty.Create(nameof(LatestStopReason), typeof(TextGenerationResult.StopReason?), typeof(ChatView));
    public TextGenerationResult.StopReason? LatestStopReason
    {
        get => (TextGenerationResult.StopReason?)GetValue(LatestStopReasonProperty);
        private set => SetValue(LatestStopReasonProperty, value);
    }

    private ConversationViewModel? _conversationViewModel;

    public ChatView()
    {
        InitializeComponent();
        messageScrollView.PropertyChanged += OnMessageScrollViewPropertyChanged;

        if (Shell.Current.BindingContext is AppShellViewModel appShellViewModel)
        {
            appShellViewModel.PropertyChanged += AppShellViewModel_PropertyChanged;
        }
    }

    private void AppShellViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppShellViewModel.AppIsInitialized))
        {
            SetScrollViewToEnd(true);
        }
    }

    protected async override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        if (BindingContext is ConversationViewModel conversationViewModel)
        {
            _conversationViewModel = conversationViewModel;

            conversationViewModel.Messages.CollectionChanged += OnMessageCollectionChanged;
            conversationViewModel.TextGenerationCompleted += OnTextGenerationCompleted;
            _conversationViewModel.PropertyChanged += OnConversationViewModelPropertyChanged;

            if (ShowLatestCompletionResult)
            {
                ShowLatestCompletionResult = false;
            }

            _previousScrollY = null;
            LatestStopReason = null;

            if (_previouslySelectedConversation != null)
            {
                _previouslySelectedConversation.Messages.CollectionChanged -= OnMessageCollectionChanged;
                _previouslySelectedConversation.TextGenerationCompleted -= OnTextGenerationCompleted;
                _previouslySelectedConversation.PropertyChanged -= OnConversationViewModelPropertyChanged;
            }

            _nextScrollViewResizeCausedByBindingContextChange = true;
            _previouslySelectedConversation = conversationViewModel;

            await ForceFocus();
        }
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Window != null)
        {
            Window.SizeChanged += OnWindowSizeChanged;
        }
    }

    private async Task ForceFocus()
    {
        //if (!chatBoxEditor.IsFocused)
        {
            do
            {
                await Task.Delay(100);
            }
            while (!chatBoxEditor.Focus());
        }
    }

    private void SetScrollViewToEnd(bool animate = true)
    {
        animate = false;
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await messageScrollView.ScrollToAsync(0, messageScrollView.ContentSize.Height, animate);
            IsScrolledToEnd = IsScrollViewScrolledToEnd(messageScrollView);
        });
    }

    private void OnConversationViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_conversationViewModel == BindingContext)
        {
            if (e.PropertyName == nameof(_conversationViewModel.LatestPromptStatus))
            {
                if (_conversationViewModel.LatestPromptStatus != LmKitTextGenerationStatus.Undefined)
                {
                    SetScrollViewToEnd(false);
                }
            }
            else if (e.PropertyName == nameof(ConversationViewModel.AwaitingResponse))
            {
                if (_conversationViewModel.AwaitingResponse)
                {
                    _shouldEnforceAutoScroll = true;
                }
                else
                {
                    _shouldEnforceAutoScroll = false;
                }
            }
        }
    }
    private void OnEntryKeyReleased(object sender, EventArgs e)
    {
        if (_conversationViewModel != null && !string.IsNullOrWhiteSpace(_conversationViewModel.InputText) && !_conversationViewModel.AwaitingResponse)
        {
            _conversationViewModel.Send();
        }
    }

    private void OnWindowSizeChanged(object? sender, EventArgs e)
    {
        _nextScrollViewResizeCausedByWindowResize = true;
    }

    private void OnEntryBorderFocused(object sender, FocusEventArgs e)
    {
        ChatEntryIsFocused = true;
    }

    private void OnEntryBorderUnfocused(object sender, FocusEventArgs e)
    {
        ChatEntryIsFocused = false;
    }

    private void OnMessageScrollViewScrolled(object sender, ScrolledEventArgs e)
    {
        if (sender is ScrollView scrollView)
        {
            if (scrollView.ContentSize.Height <= scrollView.Height)
            {
                return;
            }

            if (_shouldEnforceAutoScroll)
            {
                if (_previousScrollY != null)
                {
                    bool isScrollUp = e.ScrollY < _previousScrollY;

                    if (isScrollUp)
                    {
                        if (!_nextScrollViewResizeCausedByWindowResize)
                        {
                            _shouldEnforceAutoScroll = false;
                        }
                    }

                    if (_nextScrollViewResizeCausedByWindowResize)
                    {
                        _nextScrollViewResizeCausedByWindowResize = false;
                    }
                }
            }

            IsScrolledToEnd = IsScrollViewScrolledToEnd(scrollView);

            if (IsScrolledToEnd && _conversationViewModel!.AwaitingResponse)
            {
                _shouldEnforceAutoScroll |= true;
            }

            _previousScrollY = e.ScrollY;
        }
    }


    private void OnMessageScrollViewPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ScrollView.ContentSize))
        {
            if (_conversationViewModel != null && _conversationViewModel.IsInitialized)
            {
                if (_nextScrollViewResizeCausedByBindingContextChange)
                {
                    // A new conversation has just been loaded in the view, check if the scroll view needs to be scrolled to the end of the text.
                    if (IsScrollViewScrollable(messageScrollView))
                    {
                        SetScrollViewToEnd(false);
                    }

                    IsScrolledToEnd = true;

                    _nextScrollViewResizeCausedByBindingContextChange = false;
                }
                else
                {
                    if (_nextScrollViewResizeCausedByWindowResize)
                    {
                        _nextScrollViewResizeCausedByWindowResize = false;
                    }

                    bool wasScrolledToEnd = IsScrolledToEnd;

                    if (wasScrolledToEnd && IsScrollViewScrollable(messageScrollView))
                    {
                        SetScrollViewToEnd(false);
                    }

                    IsScrolledToEnd = IsScrollViewScrolledToEnd(messageScrollView);
                }
            }
        }
    }

    private void OnMessageCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (Shell.Current.BindingContext is AppShellViewModel appShellViewModel && appShellViewModel.AppIsInitialized)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                SetScrollViewToEnd(false);
            }

            var latestMessage = _conversationViewModel!.Messages.LastOrDefault();

            if (latestMessage != null && latestMessage.Sender == Models.MessageSender.Assistant && latestMessage.MessageInProgress)
            {
                _shouldEnforceAutoScroll = true;
                latestMessage.MessageContentUpdated += OnLatestAssistantResponseProgressed;
            }
        }
    }

    private void OnLatestAssistantResponseProgressed(object? sender, EventArgs e)
    {
        if (_shouldEnforceAutoScroll)
        {
            SetScrollViewToEnd(false);
        }
    }

    private void OnTextGenerationCompleted(object? sender, EventArgs e)
    {
        var textGenerationCompletedEventArgs = (ConversationViewModel.TextGenerationCompletedEventArgs)e;

        if (sender == BindingContext)
        {
            var _ = Task.Run(async () =>
            {
                ShowLatestCompletionResult = true;
                LatestStopReason = textGenerationCompletedEventArgs.StopReason;
                await Task.Delay(3000);
                LatestStopReason = null;
                ShowLatestCompletionResult = false;
            });
        }
    }

    private void OnScrollToEndButtonClicked(object sender, EventArgs e)
    {
        SetScrollViewToEnd();

        if (_conversationViewModel!.AwaitingResponse)
        {
            _shouldEnforceAutoScroll = true;
        }
    }

    private static bool IsScrollViewScrolledToEnd(ScrollView scrollView)
    {
        return scrollView.ContentSize.Height - scrollView.Height <= scrollView.ScrollY;
    }

    private static bool IsScrollViewScrollable(ScrollView scrollView)
    {
        return scrollView.ContentSize.Height > scrollView.Height;
    }
}
