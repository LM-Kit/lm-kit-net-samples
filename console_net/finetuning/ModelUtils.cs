using LMKit.Model;

namespace finetuning
{
    internal static class ModelUtils
    {
        private static bool _isDownloading;

        public static LM LoadModel(string modelPath)
        {
            // Loading model
            Uri modelUri = new Uri(modelPath);

            if (modelUri.IsFile && !File.Exists(modelUri.LocalPath))
            {
                Console.Write("Please enter the full model path: ");
                modelUri = new Uri(Console.ReadLine().Trim('"'));

                if (!File.Exists(modelUri.LocalPath))
                {
                    throw new FileNotFoundException($"Unable to open {modelUri.LocalPath}");
                }
            }

            LM model = new LM(modelUri,
                               downloadingProgress: ModelUtils.ModelDownloadingProgress,
                               loadingProgress: ModelUtils.ModelLoadingProgress);
            Console.Clear();

            return model;
        }

        public static bool ModelDownloadingProgress(string path, long? contentLength, long bytesRead)
        {
            _isDownloading = true;
            if (contentLength.HasValue)
            {
                double progressPercentage = Math.Round((double)bytesRead / contentLength.Value * 100, 2);
                Console.Write($"\rDownloading model {progressPercentage:0.00}%");
            }
            else
            {
                Console.Write($"\rDownloading model {bytesRead} bytes");
            }

            return true;
        }

        public static bool ModelLoadingProgress(float progress)
        {
            if (_isDownloading)
            {
                Console.Clear();
                _isDownloading = false;
            }

            Console.Write($"\rLoading model {Math.Round(progress * 100)}%");

            return true;
        }
    }
}