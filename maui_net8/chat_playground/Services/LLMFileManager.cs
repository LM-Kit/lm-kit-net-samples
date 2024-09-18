using ChatPlayground.Models;
using ChatPlayground.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ChatPlayground.Services;

public partial class LLMFileManager : ObservableObject, ILLMFileManager
{
    // todo:  FileSystemWatcher class is not available on IOS (and Android ?)
    // -> need to move FileSystemWatcher usage to Windows code folder and set up an interface + dependency injection for the folder monitoring mechanism.
    private readonly FileSystemWatcher _fileSystemWatcher = new FileSystemWatcher();
    private readonly FileSystemEntryRecorder _fileSystemEntryRecorder = new FileSystemEntryRecorder();
    private readonly IAppSettingsService _appSettingsService;
    private readonly HttpClient _httpClient;

    private readonly Dictionary<Uri, FileDownloader> _fileDownloads = new Dictionary<Uri, FileDownloader>();

    private delegate bool ModelDownloadingProgressCallback(string path, long? contentLength, long bytesRead);

    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _collectModelFilesTask;

    public ObservableCollection<ModelInfo> UserModels { get; } = new ObservableCollection<ModelInfo>();
    public ObservableCollection<Uri> UnsortedModels { get; } = new ObservableCollection<Uri>();

    [ObservableProperty]
    private bool _fileCollectingInProgress;

    private string _folderPath = string.Empty;
    public string ModelsFolderPath
    {
        get => _folderPath;
        set
        {
            if (string.CompareOrdinal(_folderPath, value) != 0)
            {
                _folderPath = value;
                _fileSystemWatcher.Path = value;
                OnModelsFolderPathChanged();
            }
        }
    }

    public event EventHandler? ModelDownloadingProgressed;
    public event EventHandler? FileCollectingCompleted;
    public event EventHandler? ModelDownloadingCompleted;

    public LLMFileManager(IAppSettingsService appSettingsService, HttpClient httpClient)
    {
        _appSettingsService = appSettingsService;
        _httpClient = httpClient;

        UserModels.CollectionChanged += OnUserModelsCollectionChanged;
        UnsortedModels.CollectionChanged += OnUnsortedModelsCollectionChanged;

        if (_appSettingsService is INotifyPropertyChanged notifyPropertyChanged)
        {
            notifyPropertyChanged.PropertyChanged += OnAppSettingsServicePropertyChanged;
        }
    }

    public void Initialize()
    {
        try
        {
            EnsureModelDirectoryExists();

            ModelsFolderPath = _appSettingsService.ModelsFolderPath;

            _fileSystemWatcher.Changed += OnFileChanged;
            _fileSystemWatcher.Deleted += OnFileDeleted;
            _fileSystemWatcher.Renamed += OnFileRenamed;
            _fileSystemWatcher.Created += OnFileCreated;

            _fileSystemWatcher.IncludeSubdirectories = true;
            _fileSystemWatcher.EnableRaisingEvents = true;
        }
        catch (Exception ex)
        {
            // todo
        }
    }

#if BETA_DOWNLOAD_MODELS
    public void DownloadModel(ModelInfo modelInfo)
    {
        var filePath = Path.Combine(ModelsFolderPath, modelInfo.Publisher, modelInfo.Repository, modelInfo.FileName);

        if (!_fileDownloads.ContainsKey(modelInfo.Metadata.DownloadUrl!))
        {
            FileDownloader fileDownloader = new FileDownloader(_httpClient, modelInfo.Metadata.DownloadUrl!, filePath);

            fileDownloader.ErrorEventHandler += OnDownloadExceptionThrown;
            fileDownloader.DownloadProgressedEventHandler += OnDownloadProgressed;
            fileDownloader.DownloadCompletedEventHandler += OnDownloadCompleted;

            if (_fileDownloads.TryAdd(modelInfo.Metadata.DownloadUrl!, fileDownloader))
            {
                fileDownloader.Start();
            }
        }
    }

    private void ReleaseFileDownloader(Uri downloadUrl)
    {
        if (_fileDownloads.ContainsKey(downloadUrl) && _fileDownloads.Remove(downloadUrl, out FileDownloader? fileDownloader))
        {
            fileDownloader.ErrorEventHandler -= OnDownloadExceptionThrown;
            fileDownloader.DownloadProgressedEventHandler -= OnDownloadProgressed;
            fileDownloader.DownloadCompletedEventHandler -= OnDownloadCompleted;

            fileDownloader.Dispose();
        }
    }

    private void OnDownloadExceptionThrown(Uri downloadUrl, Exception exception)
    {
        ReleaseFileDownloader(downloadUrl);

        if (exception is OperationCanceledException)
        {
            ModelDownloadingCompleted?.Invoke(this, new DownloadOperationStateChangedEventArgs(downloadUrl, DownloadOperationStateChangedEventArgs.DownloadOperationStateChangedType.Canceled));
        }
        else
        {
            ModelDownloadingCompleted?.Invoke(this, new DownloadOperationStateChangedEventArgs(downloadUrl, exception));
        }
    }

    private void OnDownloadProgressed(Uri downloadUrl, long? totalDownloadSize, long byteRead)
    {
        double progress = 0;

        if (totalDownloadSize.HasValue)
        {
            progress = (double)byteRead / totalDownloadSize.Value;
        }

        ModelDownloadingProgressed?.Invoke(this, new DownloadOperationStateChangedEventArgs(downloadUrl, byteRead, totalDownloadSize, progress));
    }

    private void OnDownloadCompleted(Uri downloadUrl)
    {
        ReleaseFileDownloader(downloadUrl);

        ModelDownloadingCompleted?.Invoke(this, new DownloadOperationStateChangedEventArgs(downloadUrl, DownloadOperationStateChangedEventArgs.DownloadOperationStateChangedType.Completed));
    }

    public void CancelModelDownload(ModelInfo modelInfo)
    {
        if (_fileDownloads.TryGetValue(modelInfo.Metadata.DownloadUrl!, out FileDownloader? fileDownloader))
        {
            fileDownloader!.Stop();
        }
    }

    public void PauseModelDownload(ModelInfo modelInfo)
    {
        if (_fileDownloads.TryGetValue(modelInfo.Metadata.DownloadUrl!, out FileDownloader? fileDownloader))
        {
            fileDownloader!.Pause();
        }
    }

    public void ResumeModelDownload(ModelInfo modelInfo)
    {
        if (_fileDownloads.TryGetValue(modelInfo.Metadata.DownloadUrl!, out FileDownloader? fileDownloader))
        {
            fileDownloader!.Resume();
        }
    }
#endif

    public void DeleteModel(ModelInfo modelInfo)
    {
        File.Delete(modelInfo.FileUri!.LocalPath);
    }

    private void EnsureModelDirectoryExists()
    {
        if (!Directory.Exists(_appSettingsService.ModelsFolderPath))
        {
            _appSettingsService.ModelsFolderPath = LMKitDefaultSettings.DefaultModelsFolderPath;

            if (!Directory.Exists(_appSettingsService.ModelsFolderPath))
            {
                if (File.Exists(_appSettingsService.ModelsFolderPath))
                {
                    File.Delete(_appSettingsService.ModelsFolderPath);
                }

                Directory.CreateDirectory(_appSettingsService.ModelsFolderPath);
            }
        }
    }

    private async Task CollectModelFilesAsync()
    {
        FileCollectingInProgress = true;

        if (FileCollectingInProgress && _cancellationTokenSource != null)
        {
            await CancelOngoingFileCollecting();
        }


        _cancellationTokenSource = new CancellationTokenSource();

        Exception? exception = null;

        bool cancelled = false;

        try
        {
            await (_collectModelFilesTask = Task.Run(new Action(CollectModelFiles)));
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        if (!cancelled)
        {
            TerminateFileCollectingOperation();
        }

        FileCollectingCompleted?.Invoke(this, new FileCollectingCompletedEventArgs(exception == null, exception));
    }

    private async Task CancelOngoingFileCollecting()
    {
        try
        {
            _cancellationTokenSource!.Cancel();
            await _collectModelFilesTask!.ConfigureAwait(false);
        }
        catch
        {
            return;
        }
    }

    private void TerminateFileCollectingOperation()
    {
        FileCollectingInProgress = false;
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        _collectModelFilesTask = null;
    }

    private void CollectModelFiles()
    {
        var files = Directory.GetFileSystemEntries(ModelsFolderPath, "*", SearchOption.AllDirectories);

        foreach (var filePath in files)
        {
            if (ShouldCheckFile(filePath))
            {
                HandleFile(filePath);
            }

            _cancellationTokenSource!.Token.ThrowIfCancellationRequested();
        }
    }

    private void HandleFile(string filePath)
    {
        bool isModelFile = TryValidateModelFile(filePath, ModelsFolderPath, out ModelInfo? modelInfo);

        if (isModelFile)
        {
            if (modelInfo != null)
            {
                if (!UserModels.Contains(modelInfo))
                {
                    UserModels.Add(modelInfo);
                }
            }
            else
            {
                Uri fileUri = new Uri(filePath);

                if (!UnsortedModels.Contains(fileUri))
                {
                    UnsortedModels.Add(fileUri);
                }
            }
        }
    }

    private void HandleFileRecording(Uri fileUri)
    {
        var fileRecord = _fileSystemEntryRecorder.RecordFile(fileUri);

        if (fileRecord != null)
        {
            fileRecord.FilePathChanged += OnFileRecordPathChanged;
        }
    }

    private void HandleFileRecordDeletion(Uri fileUri)
    {
        var fileRecord = _fileSystemEntryRecorder.DeleteFileRecord(fileUri!);

        if (fileRecord != null)
        {
            fileRecord.FilePathChanged -= OnFileRecordPathChanged;
        }
    }

    #region Event handlers

    private async void OnModelsFolderPathChanged()
    {
        if (FileCollectingInProgress)
        {
            _cancellationTokenSource?.Cancel();
        }

        _fileSystemEntryRecorder.Init(_folderPath);

        UnsortedModels.Clear();
        UserModels.Clear();

        await CollectModelFilesAsync();
    }

    private void OnAppSettingsServicePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppSettingsService.ModelsFolderPath))
        {
            ModelsFolderPath = _appSettingsService.ModelsFolderPath;
        }
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        Uri fileUri = new Uri(e.FullPath);

        if (UnsortedModels.Contains(fileUri))
        {
            UnsortedModels.Remove(fileUri);
        }
        else if (ModelListContainsFileUri(UserModels, fileUri, out int index))
        {
            UserModels.RemoveAt(index);
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (e.Name != null)
        {
            bool shouldCheckFile = ShouldCheckFile(e.FullPath);

            if (shouldCheckFile)
            {
                bool accessGranted = WaitFileReadAccessGranted(e.FullPath);

                if (accessGranted)
                {
                    HandleFile(e.FullPath);
                }
            }
        }
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        if (ShouldCheckFile(e.FullPath) && WaitFileReadAccessGranted(e.FullPath))
        {
            HandleFile(e.FullPath);
        }
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        var entryRecord = _fileSystemEntryRecorder.TryGetExistingEntry(new Uri(e.OldFullPath));

        if (entryRecord != null)
        {
            entryRecord.Rename(FileHelpers.GetFileBaseName(new Uri(e.FullPath)));
        }
        else
        {
            if (ShouldCheckFile(e.FullPath))
            {
                HandleFile(e.FullPath);
            }
        }

    }

    private void OnModelDownloadingProgressed(string path, long? contentLength, long bytesRead)
    {
        double progress = 0;

        if (contentLength.HasValue)
        {
            double progressPercentage = Math.Round((double)bytesRead / contentLength.Value * 100, 2);

            progress = (double)bytesRead / contentLength.Value;
            //Console.Write($"\rDownloading model {progressPercentage:0.00}%");
        }
        else
        {
            //Console.Write($"\rDownloading model {bytesRead} bytes");
        }

        if (ModelDownloadingProgressed != null)
        {
            ModelDownloadingProgressedEventArgs eventArgs = new ModelDownloadingProgressedEventArgs(path, bytesRead, contentLength, progress);
            ModelDownloadingProgressed.Invoke(this, eventArgs);
        }
    }

    private void OnUserModelsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
        {
            foreach (var item in e.NewItems!)
            {
                HandleFileRecording(((ModelInfo)item).FileUri!);
            }
        }
        else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
        {
            foreach (var item in e.OldItems!)
            {
                HandleFileRecordDeletion(((ModelInfo)item).FileUri!);
            }
        }
    }

    private void OnUnsortedModelsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
        {
            foreach (var item in e.NewItems!)
            {
                HandleFileRecording((Uri)item);
            }
        }
        else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
        {
            foreach (var item in e.OldItems!)
            {
                HandleFileRecordDeletion((Uri)item);
            }
        }
    }

    private void OnFileRecordPathChanged(object? sender, EventArgs e)
    {
        var fileRecordPathChangedEventArgs = (FileSystemEntryRecorder.FileRecordPathChangedEventArgs)e;

        int index = UnsortedModels.IndexOf(fileRecordPathChangedEventArgs.OldPath);

        if (index != -1)
        {
            UnsortedModels[index] = fileRecordPathChangedEventArgs.NewPath;
        }
        else if (ModelListContainsFileUri(UserModels, fileRecordPathChangedEventArgs.OldPath, out index) &&
                FileHelpers.GetModelInfoFromFileUri(fileRecordPathChangedEventArgs.NewPath, ModelsFolderPath,
                out string publisher, out string repository, out string fileName))
        {
            UserModels[index] = new ModelInfo(publisher, repository, fileName, fileRecordPathChangedEventArgs.NewPath, UserModels[index].FileSize);
        }
    }
    #endregion

    #region Static methods

    private static bool TryValidateModelFile(string filePath, string modelFolderPath, out ModelInfo? modelInfo)
    {
        if (LMKit.Model.LLM.ValidateFormat(filePath))
        {
            if (FileHelpers.GetModelInfoFromPath(filePath, modelFolderPath,
                out string publisher, out string repository, out string fileName))
            {
#if BETA_DOWNLOAD_MODELS
                modelInfo = TryGetExistingModelInfo(fileName, repository, publisher);
                if (modelInfo == null)
                {
                    modelInfo = new ModelInfo(publisher, repository, fileName);
                    modelInfo.Metadata.FileSize = FileHelpers.GetFileSize(filePath);
                }

                modelInfo.Metadata.FileUri = new Uri(filePath);

#else
                modelInfo = new ModelInfo(publisher, repository, fileName, new Uri(filePath), FileHelpers.GetFileSize(filePath));
#endif
            }
            else
            {
                modelInfo = null;
            }

            return true;
        }
        else
        {
            modelInfo = null;
            return false;
        }
    }

#if BETA_DOWNLOAD_MODELS
    private static ModelInfo? TryGetExistingModelInfo(string fileName, string repository, string publisher)
    {
        foreach (var modelInfo in AppConstants.AvailableModels)
        {
            if (string.CompareOrdinal(modelInfo.FileName, fileName) == 0 &&
                string.CompareOrdinal(modelInfo.Repository, repository) == 0 &&
                string.CompareOrdinal(modelInfo.Publisher, publisher) == 0)
            {
                return modelInfo;
            }
        }

        return null;
    }
#endif

    private static bool ShouldCheckFile(string filePath)
    {
        bool isFileDirectory = FileHelpers.IsFileDirectory(filePath);

        if (isFileDirectory)
        {
            return false;
        }
        else
        {
            return !(filePath.EndsWith(".download")) &&
                   !(filePath.EndsWith(".origin"));
        }
    }

    private static bool WaitFileReadAccessGranted(string fileName, int maxRetryCount = 3)
    {
        for (int retryCount = 0; retryCount < maxRetryCount; retryCount++)
        {
            if (!FileHelpers.IsFileLocked(fileName))
            {
                return true;
            }
            else
            {
                if (retryCount + 1 < maxRetryCount)
                {
                    Thread.Sleep(2000);
                }
            }
        }

        return false;
    }

    private static bool ModelListContainsFileUri(IList<ModelInfo> models, Uri fileUri, out int matchIndex)
    {
        for (int index = 0; index < models.Count; index++)
        {
            ModelInfo modelInfo = models[index];

            if (modelInfo.FileUri! == fileUri)
            {
                matchIndex = index;
                return true;
            }
        }

        matchIndex = -1;
        return false;
    }
    #endregion

    public class ModelDownloadingProgressedEventArgs : EventArgs
    {
        public string ModelFilePath { get; }

        public long BytesRead { get; }

        public long? ContentLength { get; }

        public double Progress { get; }

        public ModelDownloadingProgressedEventArgs(string modelFilePath, long bytesRead, long? contentLength, double progress)
        {
            ModelFilePath = modelFilePath;
            BytesRead = bytesRead;
            ContentLength = contentLength;
            Progress = progress;
        }
    }

    public class ModelDownloadingErrorEventArgs : EventArgs
    {
        public Exception? Exception { get; }

        public ModelDownloadingErrorEventArgs(Exception? exception)
        {
            Exception = exception;
        }
    }

    public class FileCollectingCompletedEventArgs : EventArgs
    {
        public bool Success { get; }

        public Exception? Exception { get; }

        public FileCollectingCompletedEventArgs(bool success, Exception? exception)
        {
            Success = success;
            Exception = exception;
        }
    }

    public class DownloadOperationStateChangedEventArgs : EventArgs
    {
        public Uri DownloadUrl { get; }

        public DownloadOperationStateChangedType Type { get; }

        public long BytesRead { get; }

        public long? ContentLength { get; }

        public double Progress { get; }

        public Exception? Exception { get; }

        public enum DownloadOperationStateChangedType
        {
            Started,
            Paused,
            Canceled,
            Resumed,
            Progressed,
            Completed
        }

        public DownloadOperationStateChangedEventArgs(Uri downloadUrl, DownloadOperationStateChangedType type)
        {
            DownloadUrl = downloadUrl;
            Type = type;
        }

        public DownloadOperationStateChangedEventArgs(Uri downloadUrl, DownloadOperationStateChangedType type, long bytesRead, long? contentLength, double progress, Exception? exception) : this(downloadUrl, type)
        {
            BytesRead = bytesRead;
            ContentLength = contentLength;
            Progress = progress;
            Exception = exception;
        }

        public DownloadOperationStateChangedEventArgs(Uri downloadUrl, long bytesRead, long? contentLength, double progress) : this(downloadUrl, DownloadOperationStateChangedType.Progressed)
        {
            Type = DownloadOperationStateChangedType.Progressed;
            BytesRead = bytesRead;
            ContentLength = contentLength;
            Progress = progress;
        }

        public DownloadOperationStateChangedEventArgs(Uri downloadUrl, Exception? exception) : this(downloadUrl, DownloadOperationStateChangedType.Completed)
        {
            Exception = exception;
        }
    }
}
