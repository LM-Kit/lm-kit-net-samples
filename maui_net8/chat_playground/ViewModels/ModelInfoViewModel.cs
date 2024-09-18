using ChatPlayground.Models;
using ChatPlayground.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ChatPlayground.ViewModels
{
    public partial class ModelInfoViewModel : ObservableObject
    {
        private ModelInfo _modelInfo;
        public ModelInfo ModelInfo
        {
            get => _modelInfo;
            set
            {
                _modelInfo = value;
                Name = _modelInfo.FileName;
                FileSize = _modelInfo.FileSize.HasValue ? _modelInfo.FileSize.Value : 0;
                OnPropertyChanged();
            }
        }

        [ObservableProperty]
        long _fileSize;

        [ObservableProperty]
        string _name;

        [ObservableProperty]
        string? _description;

        [ObservableProperty]
        DownloadInfo _downloadInfo = new DownloadInfo();

        public ModelInfoViewModel(ModelInfo modelInfo)
        {
            ModelInfo = modelInfo;
            FileSize = modelInfo.FileSize!.Value;
        }
    }

    public partial class DownloadInfo : ObservableObject
    {
        [ObservableProperty]
        long _bytesRead;

        [ObservableProperty]
        long? _contentLength;

        [ObservableProperty]
        double _progress;

        [ObservableProperty]
        DownloadStatus _status;
    }

    public enum DownloadStatus
    {
        NotDownloaded,
        Downloaded,
        Downloading,
        DownloadPaused,
    }
}
