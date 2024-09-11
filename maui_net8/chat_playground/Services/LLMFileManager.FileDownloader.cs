using CommunityToolkit.Mvvm.ComponentModel;

namespace ChatPlayground.Services;

public partial class LLMFileManager : ObservableObject
{
    internal sealed class FileDownloader : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly Uri _downloadUrl;
        private readonly string _filePath;
        private readonly ManualResetEvent _manualResetEvent;
        private readonly CancellationTokenSource _cancellationTokenSource;

        private bool _paused;

        public delegate void DownloadProgressDelegate(Uri downloadUrl, long? totalDownloadSize, long totalBytesRead);
        public delegate void DownloadStateChangeDelegate(Uri downloadUrl);
        public delegate void DownloadErrorDelegate(Uri downloadUrl, Exception exception);

        public DownloadStateChangeDelegate? DownloadStartedEventHandler;
        public DownloadStateChangeDelegate? DownloadPausedEventHandler;
        public DownloadStateChangeDelegate? DownloadCompletedEventHandler;
        public DownloadProgressDelegate? DownloadProgressedEventHandler;
        public DownloadErrorDelegate? ErrorEventHandler;

        public FileDownloader(HttpClient client, Uri downloadUrl, string filePath)
        {
            _httpClient = client;
            _downloadUrl = downloadUrl;
            _filePath = filePath;
            _manualResetEvent = new ManualResetEvent(false);
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Dispose();
        }

        public void Start()
        {
            Task.Run(DownloadFile);
        }

        public void Pause()
        {
            if (_manualResetEvent.Reset())
            {
                _paused = true;
            }
        }

        public void Resume()
        {
            if (_manualResetEvent.Set())
            {
                _paused = false;
            }
        }

        public void Stop()
        {
            if (_paused)
            {
                Resume();
            }

            _cancellationTokenSource.Cancel();
        }

        private async Task DownloadFile()
        {
            try
            {
                const int downloadChunkSize = 8192;

                string destinationFolder = Path.GetDirectoryName(_filePath)!;

                if (!Directory.Exists(destinationFolder))
                {
                    Directory.CreateDirectory(destinationFolder);
                }

                var downloadFilePath = _filePath + ".download";
                using HttpResponseMessage httpResponse = await _httpClient.GetAsync(_downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                using FileStream destination = new(downloadFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
                _ = httpResponse.EnsureSuccessStatusCode();
                long? totalBytes = httpResponse.Content.Headers.ContentLength;

                using Stream contentStream = await httpResponse.Content.ReadAsStreamAsync();

                long totalBytesRead = 0L;
                byte[] buffer = new byte[downloadChunkSize];

                while (true)
                {
                    if (_paused)
                    {
                        _manualResetEvent.WaitOne();
                    }

                    _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                    int bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length);

                    if (bytesRead == 0)
                    {
                        break;
                    }

                    await destination.WriteAsync(buffer, 0, bytesRead);

                    totalBytesRead += bytesRead;

                    DownloadProgressedEventHandler?.Invoke(_downloadUrl, totalBytes, totalBytesRead);
                }

                destination.Dispose();

                if (File.Exists(_filePath))
                {
                    File.Delete(_filePath);
                }

                File.Move(downloadFilePath, _filePath);

                DownloadCompletedEventHandler?.Invoke(_downloadUrl);
            }
            catch (Exception exception)
            {
                ErrorEventHandler?.Invoke(_downloadUrl, exception);
            }
        }
    }
}
