using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChatPlayground.Data;
using ChatPlayground.Services;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui.Core;
using ChatPlayground.Helpers;

namespace ChatPlayground.ViewModels;

public partial class ChatPageViewModel : ObservableObject
{
    private readonly ILogger<ChatPageViewModel> _logger;
    private readonly IChatPlaygroundDatabase _database;
    private readonly ILLMFileManager _llmFileManager;
    private readonly IPopupService _popupService;

    [ObservableProperty]
    private double _loadingProgress;
    [ObservableProperty]
    private SettingsViewModel _settingsViewModel;
    [ObservableProperty]
    private bool _modelLoadingIsFinishingUp;

    public LMKitService LmKitService { get; }
    public ConversationListViewModel ConversationListViewModel { get; }
    public ModelListViewModel ModelListViewModel { get; }

    private ConversationViewModel? _currentConversation;
    public ConversationViewModel? CurrentConversation
    {
        get => _currentConversation;
        set
        {
            // Note Evan: null-check is a workaround for https://github.com/dotnet/maui/issues/15718
            if (value != null && value != _currentConversation)
            {
                if (_currentConversation != null)
                {
                    _currentConversation.IsSelected = false;
                }

                _currentConversation = value;
                _currentConversation.IsSelected = true;
                OnPropertyChanged();
            }
        }
    }

    private ModelInfoViewModel? _selectedModel;
    public ModelInfoViewModel? SelectedModel
    {
        get => _selectedModel;
        set
        {
            if (value != _selectedModel)
            {
                _selectedModel = value;
                OnPropertyChanged();

                if (_selectedModel != null && _selectedModel.ModelInfo.FileUri != LmKitService.LMKitConfig.LoadedModelUri)
                {
                    LoadModel(_selectedModel.ModelInfo.FileUri);
                }
            }
        }
    }

    public ChatPageViewModel(ConversationListViewModel conversationListViewModel, ModelListViewModel modelListViewModel, 
        ILogger<ChatPageViewModel> logger, IPopupService popupService, IChatPlaygroundDatabase database,
        LMKitService lmKitService, ILLMFileManager llmFileManager, SettingsViewModel settingsViewModel)
    {
        _logger = logger;
        ConversationListViewModel = conversationListViewModel;
        ModelListViewModel = modelListViewModel;
        _popupService = popupService;
        _database = database;
        _llmFileManager = llmFileManager;

        LmKitService = lmKitService;
        SettingsViewModel = settingsViewModel;
        LmKitService.ModelLoadingProgressed += OnModelLoadingProgressed;
        LmKitService.ModelLoadingFailed += OnModelLoadingFailed;
        LmKitService.ModelLoadingCompleted += OnModelLoadingCompleted;

        ConversationListViewModel.Conversations.CollectionChanged += OnConversationListChanged;

    }

    public void Initialize()
    {
        if (LmKitService.LMKitConfig.LoadedModelUri != null)
        {
            SelectedModel = ChatPlaygroundHelpers.TryGetExistingModelInfoViewModel(ModelListViewModel.UserModels, LmKitService.LMKitConfig.LoadedModelUri);
        }

        //if (LmKitService.ModelLoadingState != ModelLoadingState.Loaded)
        //{
        //    LmKitService.ModelLoadingCompleted += OnModelLoadingCompleted;
        //}
        //else
        //{
        //    InitializeCurrentConversation();
        //}
    }

    [RelayCommand]
    public void StartNewConversation()
    {
        CurrentConversation = ConversationListViewModel.AddNewConversation();
    }

    [RelayCommand]
    public void EditConversationTitle(ConversationViewModel conversationViewModel)
    {
        conversationViewModel.EditingTitle = true;
    }

    [RelayCommand]
    public async Task DeleteConversation(ConversationViewModel conversationViewModel)
    {
        await ConversationListViewModel.DeleteConversation(conversationViewModel);

        if (ConversationListViewModel.Conversations.Count == 0)
        {
            StartNewConversation();
        }
        else if (conversationViewModel == CurrentConversation)
        {
            CurrentConversation = ConversationListViewModel.Conversations.First();
        }
    }

    [RelayCommand]
    public void EjectModel()
    {
        if (SelectedModel != null)
        {
            LmKitService.UnloadModel();
            SelectedModel = null;
        }
    }

    [RelayCommand]
    public void LoadModel(Uri fileUri)
    {
        ModelInfo? modelInfo = null;

        foreach (var model in ModelListViewModel.UserModels)
        {
            if (model.ModelInfo.FileUri == fileUri)
            {
                modelInfo = model.ModelInfo;
                break;
            }
        }

        if (modelInfo != null)
        {
            LmKitService.LoadModel(fileUri);
        }
    }


    [RelayCommand]
    private async Task NavigateToModelPage()
    {
        await Shell.Current.GoToAsync("//ModelsPage");
    }

    private void InitializeCurrentConversation()
    {
        if (ConversationListViewModel.Conversations.Count == 0)
        {
            StartNewConversation();
        }
    }

    private void OnModelLoadingCompleted(object? sender, EventArgs e)
    {
        SelectedModel = ChatPlaygroundHelpers.TryGetExistingModelInfoViewModel(ModelListViewModel.UserModels, LmKitService.LMKitConfig.LoadedModelUri!);
        LoadingProgress = 0;
        ModelLoadingIsFinishingUp = false;
    }

    private void OnAppLoadingCompleted(object? sender, EventArgs e)
    {
        CurrentConversation = ConversationListViewModel.Conversations.FirstOrDefault();
    }

    private void OnModelLoadingProgressed(object? sender, EventArgs e)
    {
        var loadingEventArgs = (LMKitService.ModelLoadingProgressedEventArgs)e;

        LoadingProgress = loadingEventArgs.Progress;
        ModelLoadingIsFinishingUp = LoadingProgress == 1;
    }

    private void OnModelLoadingFailed(object? sender, EventArgs e)
    {
        SelectedModel = null;
    }

    private void OnConversationListChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && ConversationListViewModel.Conversations.Count == 1)
        {
            CurrentConversation = ConversationListViewModel.Conversations[0];
        }
    }
}