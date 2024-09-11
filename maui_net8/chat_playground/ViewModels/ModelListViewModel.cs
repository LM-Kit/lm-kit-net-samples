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

        public ModelListViewModel(ILLMFileManager fileManager, LMKitService lmKitService)
        {
            _fileManager = fileManager;
            _lmKitService = lmKitService;
            _fileManager.UserModels.CollectionChanged += OnUserModelsCollectionChanged;
            UserModels = new ReadOnlyObservableCollection<ModelInfoViewModel>(_userModels);
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
#if BETA_DOWNLOAD_MODELS
            ModelInfoViewModel? modelInfoViewModel = ChatPlaygroundHelpers.TryGetExistingModelInfoViewModel(AvailableModels, modelInfo);

            if (modelInfoViewModel == null)
            {
                modelInfoViewModel = new ModelInfoViewModel(modelInfo);
            }

            modelInfoViewModel.DownloadInfo.Status = DownloadStatus.Downloaded;
#else
            ModelInfoViewModel modelInfoViewModel = new ModelInfoViewModel(modelInfo);
#endif

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

#if BETA_DOWNLOAD_MODELS
            foreach (var model in AvailableModels)
            {
                model.DownloadInfo.Status = DownloadStatus.NotDownloaded;
            }

            if (_lmKitService.ModelLoadingState == LmKitModelLoadingState.Loaded)
            {
                _lmKitService.UnloadModel();
            }
#endif
        }
    }
}
