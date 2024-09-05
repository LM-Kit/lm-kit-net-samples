using ChatPlayground.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            FileSize = modelInfo.Metadata.FileSize!.Value;
            Description = modelInfo.Metadata.Description;
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
