namespace ChatPlayground.Services;

public sealed class ModelInfo
{
    public string Publisher { get; }

    public string Repository { get; }

    public string FileName { get; }

    public long? FileSize { get; }

    public Uri FileUri { get; }

    public ModelInfo(string publisher, string repository, string fileName, Uri fileUri, long? fileSize = 0)
    {
        Publisher = publisher;
        Repository = repository;
        FileName = fileName;
        FileUri = fileUri;
        FileSize = fileSize;
    }
}
