using ChatPlayground.Helpers;
using ChatPlayground.Services;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using static ChatPlayground.Services.LLMFileManager;

namespace ChatPlayground.ViewModels
{
    public partial class ModelsPageViewModel : ObservableObject
    {
        private readonly IPopupService _popupService;
        private readonly LMKitService _lmKitService;

        public ILLMFileManager FileManager { get; }
        public IAppSettingsService AppSettingsService { get; }
        public ModelListViewModel ModelListViewModel { get; }

        [ObservableProperty]
        long _totalModelSize;

        public ModelsPageViewModel(ILLMFileManager llmFileManager, LMKitService lmKitService, IAppSettingsService appSettingsService, ModelListViewModel modelListViewModel, IPopupService popupService)
        {
            FileManager = llmFileManager;
            _lmKitService = lmKitService;
            AppSettingsService = appSettingsService;
            ModelListViewModel = modelListViewModel;
            _popupService = popupService;

#if MODEL_DOWNLOAD
            llmFileManager.ModelDownloadingProgressed += OnModelDownloadingProgressed;
            llmFileManager.ModelDownloadingCompleted += OnModelDownloadingCompleted;
#endif

        }

#if MODEL_DOWNLOAD
        [RelayCommand]
        public void DownloadModel(ModelInfoViewModel modelInfoViewModel)
        {
            modelInfoViewModel.DownloadInfo.Status = DownloadStatus.Downloading;
            modelInfoViewModel.ModelInfo.Metadata.FileUri = FileHelpers.GetModelFileUri(modelInfoViewModel.ModelInfo, AppSettingsService.ModelsFolderPath);

            FileManager.DownloadModel(modelInfoViewModel.ModelInfo);
        }

        [RelayCommand]
        public void CancelDownload(ModelInfoViewModel modelInfoViewModel)
        {
            modelInfoViewModel.DownloadInfo.Status = DownloadStatus.NotDownloaded;

            FileManager.CancelModelDownload(modelInfoViewModel.ModelInfo);
        }

        [RelayCommand]
        public void PauseDownload(ModelInfoViewModel modelInfoViewModel)
        {
            modelInfoViewModel.DownloadInfo.Status = DownloadStatus.DownloadPaused;

            FileManager.PauseModelDownload(modelInfoViewModel.ModelInfo);
        }

        [RelayCommand]
        public void ResumeDownload(ModelInfoViewModel modelInfoViewModel)
        {
            modelInfoViewModel.DownloadInfo.Status = DownloadStatus.Downloading;

            FileManager.ResumeModelDownload(modelInfoViewModel.ModelInfo);
        }
#endif

        [RelayCommand]
        public void DeleteModel(ModelInfoViewModel modelInfoViewModel)
        {
            try
            {
                FileManager.DeleteModel(modelInfoViewModel.ModelInfo);
            }
            catch (Exception ex)
            {
                App.Current.MainPage.DisplayAlert("Failure to delete model file", $"The model file could not be deleted:\n {ex.Message}", "OK");
            }
        }

        [RelayCommand]
        public async Task PickModelsFolder()
        {
            var result = await FolderPicker.Default.PickAsync(AppSettingsService.ModelsFolderPath);

            if (result.IsSuccessful)
            {
                if (_lmKitService.ModelLoadingState != LmKitModelLoadingState.Unloaded)
                {
                    _lmKitService.UnloadModel();
                }

                AppSettingsService.ModelsFolderPath = result.Folder.Path;
            }
        }

        [RelayCommand]
        public async Task OpenModelsFolder()
        {
            try
            {
                await Launcher.Default.OpenAsync(AppSettingsService.ModelsFolderPath);
            }
            catch
            {

            }
        }

        [RelayCommand]
        public async Task ShowUnsortedModelFilesPopup()
        {
            var fileNames = FileManager.UnsortedModels.Select(uri => FileHelpers.GetModelFileRelativePath(uri.LocalPath, AppSettingsService.ModelsFolderPath)).ToList();
            await _popupService.ShowPopupAsync<UnsortedModelFilesPopupViewModel>(onPresenting: viewModel => viewModel.Load(fileNames));
        }


        [RelayCommand]
        public async Task OpenHuggingFaceLink()
        {
            try
            {
                await Launcher.Default.OpenAsync("https://huggingface.co/lm-kit");
            }
            catch
            {

            }
        }

#if BETA_DOWNLOAD_MODELS
        private void OnModelDownloadingProgressed(object? sender, EventArgs e)
        {
            var downloadOperationStateChangedEventArgs = (DownloadOperationStateChangedEventArgs)e;

            var modelViewModel = ChatPlaygroundHelpers.TryGetExistingModelInfoViewModel(ModelListViewModel.AvailableModels,
                downloadOperationStateChangedEventArgs.DownloadUrl)!;

            modelViewModel!.DownloadInfo.Progress = downloadOperationStateChangedEventArgs.Progress;
            modelViewModel.DownloadInfo.BytesRead = downloadOperationStateChangedEventArgs.BytesRead;
            modelViewModel.DownloadInfo.ContentLength = downloadOperationStateChangedEventArgs.ContentLength;
        }

        private async void OnModelDownloadingCompleted(object? sender, EventArgs e)
        {
            var downloadOperationStateChangedEventArgs = (DownloadOperationStateChangedEventArgs)e;

            var modelViewModel = ChatPlaygroundHelpers.TryGetExistingModelInfoViewModel(ModelListViewModel.AvailableModels,
                downloadOperationStateChangedEventArgs.DownloadUrl)!;

            if (downloadOperationStateChangedEventArgs.Exception != null)
            {
                modelViewModel.DownloadInfo.Status = DownloadStatus.NotDownloaded;
                await ChatPlaygroundHelpers.DisplayError("Model download failure",
                    $"Download of model '{modelViewModel.Name}' failed:\n{downloadOperationStateChangedEventArgs.Exception.Message}");
            }
            else if (downloadOperationStateChangedEventArgs.Type == DownloadOperationStateChangedEventArgs.DownloadOperationStateChangedType.Canceled)
            {
                modelViewModel.DownloadInfo.Status = DownloadStatus.NotDownloaded;
            }
            else if (downloadOperationStateChangedEventArgs.Type == DownloadOperationStateChangedEventArgs.DownloadOperationStateChangedType.Completed)
            {
                modelViewModel.DownloadInfo.Status = DownloadStatus.Downloaded;
            }

            modelViewModel.DownloadInfo.Progress = 0;
            modelViewModel.DownloadInfo.BytesRead = 0;
        }
#endif
    }
}