using ChatPlayground.Data;
using ChatPlayground.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ChatPlayground.Services;
using ChatPlayground.Helpers;


namespace ChatPlayground.ViewModels
{
    public partial class AppShellViewModel : ObservableObject
    {
        private readonly ILogger<AppShellViewModel> _logger;
        private readonly SettingsViewModel _settingsViewModel;
        private readonly ModelListViewModel _modelListViewModel;
        private readonly ConversationListViewModel _conversationListViewModel;
        private readonly LMKitService _lmKitService;
        private readonly ILLMFileManager _llmFileManager;
        private readonly IAppSettingsService _appSettingsService;

        [ObservableProperty]
        private bool _appIsInitialized = false;

        [ObservableProperty]
        List<ChatPlaygroundTabViewModel> _tabs = new List<ChatPlaygroundTabViewModel>();

        [ObservableProperty]
        ChatPlaygroundTabViewModel _homeTab = new ChatPlaygroundTabViewModel("Home", "HomePage");

        [ObservableProperty]
        ChatPlaygroundTabViewModel _chatTab = new ChatPlaygroundTabViewModel("AI Chat", "ChatPage");

        [ObservableProperty]
        ChatPlaygroundTabViewModel _modelsTab = new ChatPlaygroundTabViewModel("Models", "ModelsPage");

        private ChatPlaygroundTabViewModel? _currentTab;
        public ChatPlaygroundTabViewModel CurrentTab
        {
            get => _currentTab!;
            set
            {
                if (_currentTab != null)
                {
                    _currentTab.IsSelected = false;
                }

                _currentTab = value;
                _currentTab.IsSelected = true;
            }
        }

        public AppShellViewModel(ILogger<AppShellViewModel> logger, ConversationListViewModel conversationListViewModel, ModelListViewModel modelListViewModel, SettingsViewModel settingsViewModel, LMKitService lmKitService, ILLMFileManager llmFileManager, IAppSettingsService appSettingsService)
        {
            _logger = logger;
            _conversationListViewModel = conversationListViewModel;
            _modelListViewModel = modelListViewModel;
            _settingsViewModel = settingsViewModel;
            _lmKitService = lmKitService;
            _llmFileManager = llmFileManager;
            _appSettingsService = appSettingsService;

            //Tabs.Add(HomeTab);
            Tabs.Add(ChatTab);
            Tabs.Add(ModelsTab);
            CurrentTab = HomeTab;
        }

        public async Task Init()
        {
            _settingsViewModel.Init();

            await _conversationListViewModel.LoadConversationLogs();

            _lmKitService.ModelLoadingFailed += OnModelLoadingFailed;

            if (_appSettingsService.LastLoadedModel != null)
            {
                TryLoadLastUsedModel();
            }

            _llmFileManager.FileCollectingCompleted += OnFileManagerFileCollectingCompleted;
            _llmFileManager.Initialize();
            AppIsInitialized = true;
        }

        private void TryLoadLastUsedModel()
        {
            if (FileHelpers.TryCreateFileUri(_appSettingsService.LastLoadedModel!, out Uri? fileUri) &&
                File.Exists(_appSettingsService.LastLoadedModel) &&
                FileHelpers.GetModelInfoFromFileUri(fileUri!, _appSettingsService.ModelsFolderPath,
                out string publisher, out string repository, out string fileName))
            {
                _lmKitService.LoadModel(new ModelInfo(publisher, repository, fileName, fileUri!));
            }
            else
            {
                _appSettingsService.LastLoadedModel = null;
            }
        }

        private async void OnFileManagerFileCollectingCompleted(object? sender, EventArgs e)
        {
            var fileCollectingCompletedEventArgs = (LLMFileManager.FileCollectingCompletedEventArgs)e;

            if (!fileCollectingCompletedEventArgs.Success && fileCollectingCompletedEventArgs.Exception != null)
            {
                _appSettingsService.ModelsFolderPath = LMKitDefaultSettings.DefaultModelsFolderPath;

                if (App.Current?.MainPage != null)
                {
                    if (MainThread.IsMainThread)
                    {
                        await App.Current!.MainPage!.DisplayAlert("Error with your model folder",
                            $"Model files failed to be collected from input folder: {fileCollectingCompletedEventArgs.Exception.Message!}\n\nThe default model folder will be reset.",
                            "OK");
                    }
                    else
                    {
                        MainThread.BeginInvokeOnMainThread(async () =>
                          await App.Current!.MainPage!.DisplayAlert("Error with your model folder",
                            $"Model files failed to be collected from input folder: {fileCollectingCompletedEventArgs.Exception.Message!}\n\nThe default model folder will be reset.",
                            "OK"));
                    }
                }
            }
        }

        private async void OnModelLoadingFailed(object? sender, EventArgs e)
        {
            var modelLoadingFailedEventArgs = (LMKitService.ModelLoadingFailedEventArgs)e;

            if (App.Current?.MainPage != null)
            {
                if (MainThread.IsMainThread)
                {
                    await App.Current!.MainPage!.DisplayAlert("Error loading model",
                        $"The model failed to be loaded: {modelLoadingFailedEventArgs.Exception.Message}", "OK");

                }
                else
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    await App.Current!.MainPage!.DisplayAlert("Error loading model",
                    $"The model failed to be loaded: {modelLoadingFailedEventArgs.Exception.Message}", "OK"));
                }
            }
        }

        public void SaveAppSettings()
        {
            _settingsViewModel.Save();
        }

        [RelayCommand]
        private async Task Navigate(ChatPlaygroundTabViewModel tab)
        {
            if (!tab.IsSelected)
            {
                await Shell.Current.GoToAsync($"//{tab.Route}", true);
            }
        }
    }
}
