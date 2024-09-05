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
        public ModelMetadata Metadata { get; set; }

        public ModelInfo(string downloadUrl, ModelMetadata? modelMetadata = null)
        {
            Metadata = modelMetadata ?? new ModelMetadata();
            Metadata.DownloadUrl = new Uri(downloadUrl);
            FileHelpers.GetModelInfoFromDownloadUrl(Metadata.DownloadUrl, out string publisher, out string repository, out string fileName);
            Publisher = publisher;
            Repository = repository;
            FileName = fileName;
        }

        [JsonConstructor]
        public ModelInfo (string publisher, string repository, string fileName)
        {
            Publisher = publisher;
            Repository = repository;
            FileName = fileName;
            Metadata = new ModelMetadata();
        }

        public override bool Equals(object? obj)
        {
            return (obj != null && obj is ModelInfo modelInfo &&
                string.CompareOrdinal(modelInfo.Publisher, Publisher) == 0 &&
                string.CompareOrdinal(modelInfo.Repository, Repository) == 0 &&
                string.CompareOrdinal(modelInfo.FileName, FileName) == 0);
        }

        public sealed class ModelMetadata
        {
            public string? Description { get; set; }

            public long? FileSize { get; set; }

            public Uri? DownloadUrl { get; set; }

            public Uri? FileUri { get; set; }

            public ModelMetadata()
            {

            }
        }
    }
}
