using LMKit.Data;
using LMKit.Data.Storage;
using LMKit.Data.Storage.Qdrant;
using LMKit.Global;
using LMKit.Model;
using LMKit.Retrieval;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Sampling;
using System.Diagnostics;
using System.Text;

namespace custom_chatbot_with_rag_qdrant_vector_store
{
    internal class Program
    {
        static bool _isDownloading;
        static LM _chatModel = null!;
        static LM _embeddingModel = null!;
        static readonly List<DataSource> _dataSources = new();
        static IVectorStore _store = null!;

        static void Main(string[] args)
        {
            // Set an optional license key here if available.
            // A free community license can be obtained from: https://lm-kit.com/products/community-edition/
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            // Initializing store
            // We're using local environment that we've started with: docker run -p 6333:6333 -p 6334:6334
            // Check this tutorial to setup qdrant local environment: https://qdrant.tech/documentation/quickstart/
            _store = new QdrantEmbeddingStore(new Uri("http://localhost:6334"));

            // Loading models
            _embeddingModel = LoadModel("embeddinggemma-300m", "Embedding");
            _chatModel = LoadModel("gemma3:4b", "Chat");

            Console.Clear();
            WriteLineColor("*********************************************************************************************\n" +
                           "* In this demo, we are loading various eBooks, which can be queried semantically.\n" +
                           "* You can ask questions about the content of the loaded eBooks,\n" +
                           "* and the system will use a Retrieval-Augmented Generation (RAG) approach to provide answers.\n" +
                           "* This means that the chatbot will first retrieve relevant information from the eBooks\n" +
                           "* and then generate a coherent response based on that information.\n" +
                           "*********************************************************************************************\n",
                           ConsoleColor.Blue);

            // Loading some famous eBooks
            WriteLineColor("Loading Romeo and Juliet eBook...", ConsoleColor.Green);
            _dataSources.Add(LoadUriAsDataSource(new Uri("https://gutenberg.org/cache/epub/1513/pg1513.txt"), "Romeo and Juliet"));

            WriteLineColor("Loading Moby Dick eBook...", ConsoleColor.Green);
            _dataSources.Add(LoadUriAsDataSource(new Uri("https://gutenberg.org/cache/epub/2701/pg2701.txt"), "Moby Dick"));

            WriteLineColor("Loading Pride and Prejudice eBook...", ConsoleColor.Green);
            _dataSources.Add(LoadUriAsDataSource(new Uri("https://gutenberg.org/cache/epub/1342/pg1342.txt"), "Pride and Prejudice"));

            RagEngine ragEngine = new(_embeddingModel, vectorStore: _store);
            SingleTurnConversation chat = new(_chatModel)
            {
                SystemPrompt = "You are an expert RAG assistant, specialized in answering questions about various books.",
                SamplingMode = new GreedyDecoding()
            };

            ragEngine.AddDataSources(_dataSources);

            chat.AfterTextCompletion += AfterTextCompletion!;

            while (true)
            {
                WriteLineColor($"\n\nEnter your query:\n", ConsoleColor.Green);

                string? query = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(query))
                {
                    break;
                }

                // Determine the number of top partitions to select based on GPU support.
                // If GPU is available, select the top 3 partitions; otherwise, select only the top 1 to maintain acceptable speed.
                int topK = Runtime.HasGpuSupport ? 3 : 1;
                List<PartitionSimilarity> partitions = ragEngine.FindMatchingPartitions(query, topK, forceUniqueSection: true);

                if (partitions.Count > 0)
                {
                    WriteLineColor($"\nAnswer from {partitions[0].DataSourceIdentifier}:\n", ConsoleColor.Green);
                    _ = ragEngine.QueryPartitions(query, partitions, chat);
                }
                else
                {
                    Console.WriteLine("\n >> No relevant information found in the loaded sources to answer your query. Please try asking a different question.");
                }
            }

            Console.WriteLine("Demo ended. Press any key to exit.");
            _ = Console.ReadKey();
        }

        private static bool OnDownloadProgress(string path, long? contentLength, long bytesRead)
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

        private static bool OnLoadProgress(float progress)
        {
            if (_isDownloading)
            {
                Console.Clear();
                _isDownloading = false;
            }

            Console.Write($"\rLoading model {Math.Round(progress * 100)}%");

            return true;
        }

        private static void AfterTextCompletion(object? sender, LMKit.TextGeneration.Events.AfterTextCompletionEventArgs e)
        {
            Console.ForegroundColor = e.SegmentType switch
            {
                LMKit.TextGeneration.Chat.TextSegmentType.InternalReasoning => ConsoleColor.Blue,
                LMKit.TextGeneration.Chat.TextSegmentType.ToolInvocation => ConsoleColor.Magenta,
                _ => ConsoleColor.White
            };

            Console.Write(e.Text);
        }

        private static LM LoadModel(string modelId, string label)
        {
            Console.WriteLine($"Loading {label} model ({modelId})...");
            return LM.LoadFromModelID(
                modelId,
                downloadingProgress: OnDownloadProgress,
                loadingProgress: OnLoadProgress);
        }

        private static DataSource LoadUriAsDataSource(Uri uri, string dataSourceIdentifier)
        {
            if (_store.CollectionExistsAsync(dataSourceIdentifier).Result)
            {
                Console.WriteLine($"   > {dataSourceIdentifier} loading datasource from store.");
                return DataSource.LoadFromStore(_store, dataSourceIdentifier);
            }

            string eBookContent = DownloadContent(uri);
            Stopwatch stopwatch = Stopwatch.StartNew();
            RagEngine ragEngine = new(_embeddingModel, _store);
            DataSource dataSource = ragEngine.ImportText(eBookContent, new TextChunking() { MaxChunkSize = 500 }, dataSourceIdentifier, "default");
            stopwatch.Stop();
            Console.WriteLine($"   > {dataSourceIdentifier} loaded in {Math.Round(stopwatch.Elapsed.TotalSeconds, 1)} seconds");

            return dataSource;
        }

        private static string DownloadContent(Uri uri)
        {
            using var client = new HttpClient();
            return client.GetStringAsync(uri).Result;
        }

        private static void WriteLineColor(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ResetColor();
        }
    }
}
