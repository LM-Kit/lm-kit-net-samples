using ChatPlayground.Helpers;
using ChatPlayground.Models;
using ChatPlayground.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace ChatPlayground.ViewModels
{
    public partial class ModelListViewModel : ObservableObject
    {
        private readonly ILLMFileManager _fileManager;
        private readonly LMKitService _lmKitService;

        private ObservableCollection<ModelInfoViewModel> _userModels = new ObservableCollection<ModelInfoViewModel>();

        [ObservableProperty]
        long _totalModelSize;

        public ReadOnlyObservableCollection<ModelInfoViewModel> UserModels { get; }
        public ObservableCollection<ModelInfoViewModel> AvailableModels { get; } = new ObservableCollection<ModelInfoViewModel>();

        public ModelListViewModel(ILLMFileManager fileManager, LMKitService lmKitService)
        {
            _fileManager = fileManager;
            _lmKitService = lmKitService;
            _fileManager.UserModels.CollectionChanged += OnUserModelsCollectionChanged;
            UserModels = new ReadOnlyObservableCollection<ModelInfoViewModel>(_userModels);
        }

        public async Task Initialize()
        {
            foreach (var modelInfo in AppConstants.AvailableModels)
            {
                if (modelInfo.Metadata.FileSize == null && modelInfo.Metadata.DownloadUrl != null)
                {
                    modelInfo.Metadata.FileSize = await FileHelpers.GetFileSizeFromUri(modelInfo.Metadata.DownloadUrl);
                }

                ModelInfoViewModel modelInfoViewModel = new ModelInfoViewModel(modelInfo);

                AvailableModels.Add(modelInfoViewModel);
            }
        }

        private void OnUserModelsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                foreach (var item in e.NewItems!)
                {
                    ModelInfo modelInfo = (ModelInfo)item;
                    AddNewModel(modelInfo);
                }
            }
            else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
            {
                foreach (var item in e.OldItems!)
                {
                    ModelInfo modelInfo = (ModelInfo)item;
                    RemoveExistingModel(modelInfo);
                }
            }
            else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
            {
                ClearUserModelList();
            }
            else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Replace)
            {
                int index = e.NewStartingIndex;

                foreach (var item in e.NewItems!)
                {
                    ReplaceExistingModel((ModelInfo)item, index);
                    index++;
                }
            }
        }

        private void AddNewModel(ModelInfo modelInfo)
        {
            ModelInfoViewModel? modelInfoViewModel = ChatPlaygroundHelpers.TryGetExistingModelInfoViewModel(AvailableModels, modelInfo);

            if (modelInfoViewModel == null)
            {
                modelInfoViewModel = new ModelInfoViewModel(modelInfo);
            }

            modelInfoViewModel.DownloadInfo.Status = DownloadStatus.Downloaded;

            _userModels.Add(modelInfoViewModel);
            TotalModelSize += modelInfoViewModel.FileSize;
        }

        private void RemoveExistingModel(ModelInfo modelInfo)
        {
            ModelInfoViewModel? modelInfoViewModel = ChatPlaygroundHelpers.TryGetExistingModelInfoViewModel(UserModels, modelInfo);

            if (modelInfoViewModel != null)
            {
                modelInfoViewModel.DownloadInfo.Status = DownloadStatus.NotDownloaded;
                _userModels.Remove(modelInfoViewModel);
                TotalModelSize -= modelInfoViewModel.FileSize;

                if (_lmKitService.LMKitConfig.LoadedModel == modelInfoViewModel.ModelInfo)
                {
                    _lmKitService.UnloadModel();
                }
            }
        }

        private void ReplaceExistingModel(ModelInfo modelInfo, int index)
        {
            ModelInfoViewModel modelInfoViewModel = UserModels[index];

            modelInfoViewModel.ModelInfo = modelInfo;
        }

        private void ClearUserModelList()
        {
            TotalModelSize = 0;
            _userModels.Clear();

            foreach (var model in AvailableModels)
            {
                model.DownloadInfo.Status = DownloadStatus.NotDownloaded;
            }

            if (_lmKitService.ModelLoadingState == ModelLoadingState.Loaded)
            {
                _lmKitService.UnloadModel();
            }
        }
    }
}
