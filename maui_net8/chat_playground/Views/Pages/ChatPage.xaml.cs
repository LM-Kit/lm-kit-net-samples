using ChatPlayground.Models;
using ChatPlayground.Services;
using ChatPlayground.ViewModels;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.Input;

namespace ChatPlayground.Views;

public partial class ChatPage : ContentPage
{
    private readonly ChatPageViewModel _chatViewModel;
    private TapGestureRecognizer? _modelDropdownDismissGestureRecognizer;

    public static readonly BindableProperty SettingSidebarIsToggledProperty = BindableProperty.Create(nameof(SettingsSidebarIsToggled), typeof(bool), typeof(ChatPage));
    public bool SettingsSidebarIsToggled
    {
        get => (bool)GetValue(SettingSidebarIsToggledProperty);
        private set => SetValue(SettingSidebarIsToggledProperty, value);
    }

    public static readonly BindableProperty ChatsSidebarIsToggledProperty = BindableProperty.Create(nameof(ChatsSidebarIsToggled), typeof(bool), typeof(ChatPage));
    public bool ChatsSidebarIsToggled
    {
        get => (bool)GetValue(ChatsSidebarIsToggledProperty);
        private set => SetValue(ChatsSidebarIsToggledProperty, value);
    }

    public static readonly BindableProperty ModelDropdownIsExpandedProperty = BindableProperty.Create(nameof(ModelDropdownIsExpanded), typeof(bool), typeof(ChatPage), propertyChanged: ModelDropdownIsExpandedPropertyChanged);
    public bool ModelDropdownIsExpanded
    {
        get => (bool)GetValue(ModelDropdownIsExpandedProperty);
        private set => SetValue(ModelDropdownIsExpandedProperty, value);
    }

    public static readonly BindableProperty LoadingTextProperty = BindableProperty.Create(nameof(LoadingText), typeof(string), typeof(ChatPage));
    public string LoadingText
    {
        get => (string)GetValue(LoadingTextProperty);
        private set => SetValue(LoadingTextProperty, value);
    }

    public ChatPage(ChatPageViewModel singleTurnChatViewModel)
    {
        InitializeComponent();
        BindingContext = singleTurnChatViewModel;
        _chatViewModel = singleTurnChatViewModel;
        _chatViewModel.LmKitService.ModelLoadingProgressed += OnModelLoadingProgressed;
        _chatViewModel.LmKitService.ModelLoadingCompleted += OnModelLoadingCompleted;
    }

    private void OnModelLoadingProgressed(object? sender, EventArgs e)
    {
        var modelLoadingProgressedEventArgs = (LMKitService.ModelLoadingProgressedEventArgs)e;

        if (modelLoadingProgressedEventArgs != null)
        {
            if (modelLoadingProgressedEventArgs.Progress == 1)
            {
                LoadingText = "Finishing up..."; //  have to set this text directly in code-behind because DataTrigger with MultiBinding did not work.
            }
            else
            {
                LoadingText = "Loading model...";
            }
        }
    }

    private void OnModelLoadingCompleted(object? sender, EventArgs e)
    {
        LoadingText = "Loading model...";
        _chatViewModel.LoadingProgress = 0;
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        ((ChatPageViewModel)BindingContext).Initialize();
    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);

        if (ModelDropdownIsExpanded)
        {
            ModelDropdownIsExpanded = false;
        }
    }

    private void SettingsButtonClicked(object sender, EventArgs e)
    {
        SettingsSidebarIsToggled = !SettingsSidebarIsToggled;
    }

    private void ChatsButtonClicked(object sender, EventArgs e)
    {
        ChatsSidebarIsToggled = !ChatsSidebarIsToggled;
    }

    [RelayCommand]
    private void ToggleModelDropdownIsExpanded()
    {
        ModelDropdownIsExpanded = !ModelDropdownIsExpanded;

        if (ModelDropdownIsExpanded)
        {
            //var popupPage = new TestPopup(_chatViewModel);

            //popupPage.Size = new Size(modelButtonView.Width, 20);
            //popupPage.VerticalOptions = Microsoft.Maui.Primitives.LayoutAlignment.Start;
            //popupPage.SetBinding(TranslationXProperty, new Binding(nameof(modelButtonView.X), source: modelButtonView));
            //popupPage.SetBinding(WidthRequestProperty, new Binding(nameof(modelButtonView.Width), source: modelButtonView));
            //popupPage.SetBinding(MaximumWidthRequestProperty, new Binding(nameof(modelButtonView.Width), source: modelButtonView));

            //popupPage.WidthRequest = modelButtonView.Width;
            //popupPage.Anchor = modelButtonView;
            //Shell.Current.ShowPopup(popupPage);


            //var popupResult = await popupPage.PopupDismissedTask;

            // Workaround to make sure the focus request is being honored:
            // There is a delay between IsVisible property getting set to true and the
            // element becoming visible on the page (and therefore able to receive focus).
            //await Task.Delay(50);
            //modelsCollection.Focus();
        }
    }

    private async void ModelsCollectionViewUnfocused(object sender, FocusEventArgs e)
    {
        //if (ModelDropdownIsExpanded)
        //{
        //    await Task.Run(async () =>
        //    {
        //        await Task.Delay(100);
        //        ModelDropdownIsExpanded = false;
        //    });
        //}
    }

    [RelayCommand]
    private void ChatPageTapped()
    {
        if (ModelDropdownIsExpanded)
        {
            ModelDropdownIsExpanded = false;
        }
    }

    [RelayCommand]
    private void CollapseModelDropdown()
    {
        if (ModelDropdownIsExpanded)
        {
            ModelDropdownIsExpanded = false;
        }
    }

    private async static void ModelDropdownIsExpandedPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var chatPage = (ChatPage)bindable;

        // Workaround: when a new tap gesture recognizer is added prior to IsExpanded property changed,
        // the tap gesture that triggered the IsExpanded property change gets recognized by the newly added recognizer.
        // -> Delay the adding operation.
        await Task.Delay(50);

        if (chatPage.ModelDropdownIsExpanded)
        {
            chatPage._modelDropdownDismissGestureRecognizer = new TapGestureRecognizer() { Command = chatPage.CollapseModelDropdownCommand };
            chatPage.rootGrid.GestureRecognizers.Add(chatPage._modelDropdownDismissGestureRecognizer);
        }
        else
        {
            chatPage.rootGrid.GestureRecognizers.Remove(chatPage._modelDropdownDismissGestureRecognizer);
            chatPage._modelDropdownDismissGestureRecognizer = null;
        }
    }

    //private void EditPromptButtonClicked(object sender, EventArgs e)
    //{
    //    if (systemPromptEntry.IsFocused)
    //    {
    //        systemPromptEntry.Unfocus();
    //    }
    //    else
    //    {
    //        systemPromptEntry.IsEnabled = true;
    //        systemPromptEntry.Focus();
    //    }
    //}

    //private void SystemPromptEntryUnfocused(object? sender, FocusEventArgs e)
    //{
    //    systemPromptEntry.IsEnabled = false;

    //    if (systemPromptEntry.Text != _chatViewModel.CurrentConversation.SystemPrompt && !string.IsNullOrWhiteSpace(systemPromptEntry.Text))
    //    {
    //        _chatViewModel.CurrentConversation.SystemPrompt = systemPromptEntry.Text;
    //    }
    //    else
    //    {
    //        systemPromptEntry.Text = _chatViewModel.CurrentConversation.SystemPrompt;
    //    }
    //}
}