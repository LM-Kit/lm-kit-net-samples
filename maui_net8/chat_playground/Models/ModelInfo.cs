using ChatPlayground.Helpers;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ChatPlayground.Models
{
    public sealed class ModelInfo
    {
        public string Publisher { get; }

        public string Repository { get; }

        public string FileName { get; }

        [JsonIgnore]
        public long? FileSize { get; set; }

        [JsonIgnore]
        public Uri? DownloadUrl { get; set; }

        [JsonIgnore]
        public Uri? FileUri { get; set; }

        [JsonConstructor]
        public ModelInfo(string publisher, string repository, string fileName)
        {
            Publisher = publisher;
            Repository = repository;
            FileName = fileName;
        }

        public ModelInfo(string publisher, string repository, string fileName, Uri fileUri) : this(publisher, repository, fileName)
        {
            FileUri = fileUri;
        }

        public override bool Equals(object? obj)
        {
            return (obj != null && obj is ModelInfo modelInfo &&
                string.CompareOrdinal(modelInfo.Publisher, Publisher) == 0 &&
                string.CompareOrdinal(modelInfo.Repository, Repository) == 0 &&
                string.CompareOrdinal(modelInfo.FileName, FileName) == 0);
        }
    }
}
