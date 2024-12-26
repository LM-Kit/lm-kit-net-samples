using LMKit.Data;
using LMKit.Global;
using LMKit.Model;
using LMKit.Retrieval;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Sampling;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using static LMKit.Retrieval.RagEngine;

namespace custom_chatbot_with_rag
{
    internal class Program
    {
        static readonly string DEFAULT_EMBEDDINGS_MODEL_PATH = @"https://huggingface.co/lm-kit/bge-1.5-gguf/resolve/main/bge-small-en-v1.5-f16.gguf?download=true";
        static readonly string DEFAULT_CHAT_MODEL_PATH = @"https://huggingface.co/lm-kit/llama-3.1-8b-instruct-gguf/resolve/main/Llama-3.1-8B-Instruct-Q4_K_M.gguf?download=true";
        static bool _isDownloading;
        static LM _chatModel;
        static LM _embeddingModel;
        static readonly List<DataSource> _dataSources = new List<DataSource>();

        static void Main(string[] args)
        {
            // Set an optional license key here if available. 
            // A free community license can be obtained from: https://lm-kit.com/products/community-edition/
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            //Loading models
            LoadChatModel();
            LoadEmbeddingModel();

            Console.Clear();
            WriteLineColor("*********************************************************************************************\n" +
                           "* In this demo, we are loading various eBooks, which can be queried semantically.\n" +
                           "* You can ask questions about the content of the loaded eBooks,\n" +
                           "* and the system will use a Retrieval-Augmented Generation (RAG) approach to provide answers.\n" +
                           "* This means that the chatbot will first retrieve relevant information from the eBooks\n" +
                           "* and then generate a coherent response based on that information.\n" +
                           "*********************************************************************************************\n",
                           ConsoleColor.Blue);

            //Loading some famous eBooks
            WriteLineColor("Loading Romeo and Juliet eBook...", ConsoleColor.Green);
            _dataSources.Add(LoadUriAsDataSource(new Uri("https://gutenberg.org/cache/epub/1513/pg1513.txt"), "Romeo and Juliet", "romeo_and_juliet.dat"));

            WriteLineColor("Loading Moby Dick eBook...", ConsoleColor.Green);
            _dataSources.Add(LoadUriAsDataSource(new Uri("https://gutenberg.org/cache/epub/2701/pg2701.txt"), "Moby Dick", "moby_dick.dat"));

            WriteLineColor("Loading Pride and Prejudice eBook...", ConsoleColor.Green);
            _dataSources.Add(LoadUriAsDataSource(new Uri("https://gutenberg.org/cache/epub/1342/pg1342.txt"), "Pride and Prejudice", "pride_and_prejudice.dat"));

            RagEngine ragEngine = new RagEngine(_embeddingModel);
            SingleTurnConversation chat = new SingleTurnConversation(_chatModel)
            {
                SystemPrompt = "You are an expert RAG assistant, specialized in answering questions about various books.",
                SamplingMode = new GreedyDecoding()
            };

            ragEngine.AddDataSources(_dataSources);

            chat.AfterTextCompletion += AfterTextCompletion;

            while (true)
            {
                WriteLineColor($"\n\nEnter your query:\n", ConsoleColor.Green);

                string query = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(query))
                {
                    break;
                }

                // Determine the number of top partitions to select based on GPU support.
                // If GPU is available, select the top 3 partitions; otherwise, select only the top 1 to maintain acceptable speed.
                int topK = Runtime.HasGpuSupport ? 3 : 1;
                List<TextPartitionSimilarity> partitions = ragEngine.FindMatchingPartitions(query, topK, forceUniqueDataSource: true);

                if (partitions.Count > 0)
                {
                    WriteLineColor($"\nAnswer from {partitions[0].Partition.Owner.Owner.Identifier}:\n", ConsoleColor.Green);
                    _ = ragEngine.QueryPartitions(query, partitions, chat);
                }
                else
                {
                    Console.WriteLine("\n  >> No relevant information found in the loaded sources to answer your query. Please try asking a different question.");
                }
            }

            Console.WriteLine("The program ended. Press any key to exit the application.");
            _ = Console.ReadKey();
        }

        private static bool ModelDownloadingProgress(string path, long? contentLength, long bytesRead)
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

        private static bool ModelLoadingProgress(float progress)
        {
            if (_isDownloading)
            {
                Console.Clear();
                _isDownloading = false;
            }

            Console.Write($"\rLoading model {Math.Round(progress * 100)}%");

            return true;
        }

        private static void AfterTextCompletion(object sender, LMKit.TextGeneration.Events.AfterTextCompletionEventArgs e)
        {
            Console.Write(e.Text);
        }

        private static DataSource LoadUriAsDataSource(Uri uri, string dataSourceIdentifier, string dataSourcePath)
        {
            if (File.Exists(dataSourcePath))
            {//using cached version
                Console.WriteLine($"   > {dataSourceIdentifier} obtained from previously serialized DataSource object.");
                return DataSource.Deserialize(dataSourcePath, _embeddingModel);
            }

            //creating a new DataSource object using the RAG
            Stopwatch stopwatch = Stopwatch.StartNew();
            string eBookContent = DownloadContent(uri);
            RagEngine ragEngine = new RagEngine(_embeddingModel);
            DataSource dataSource = ragEngine.ImportText(eBookContent, new TextChunking() { MaxChunkSize = 500 }, dataSourceIdentifier);
            stopwatch.Stop();
            Console.WriteLine($"   > {dataSourceIdentifier} loaded in {Math.Round(stopwatch.Elapsed.TotalSeconds, 1)} seconds");

            //caching to file
            dataSource.Serialize(dataSourcePath);

            return dataSource;
        }

        private static string DownloadContent(Uri uri)
        {
            string tmpFile = Path.GetTempFileName();
            try
            {
                using (var client = new WebClient())
                {
                    client.DownloadFile(uri, tmpFile);
                }

                return File.ReadAllText(tmpFile);
            }
            finally
            {
                File.Delete(tmpFile);
            }
        }

        private static void LoadChatModel()
        {
            Uri modelUri = new Uri(DEFAULT_CHAT_MODEL_PATH);

            if (modelUri.IsFile && !File.Exists(modelUri.LocalPath))
            {
                Console.Write("Please enter full chat model's path: ");
                modelUri = new Uri(Console.ReadLine().Trim(new[] { '"' }));

                if (!File.Exists(modelUri.LocalPath))
                {
                    throw new FileNotFoundException($"Unable to open {modelUri.LocalPath}");
                }
            }

            _chatModel = new LM(modelUri,
                                   downloadingProgress: ModelDownloadingProgress,
                                   loadingProgress: ModelLoadingProgress);
        }

        private static void LoadEmbeddingModel()
        {
            Uri modelUri = new Uri(DEFAULT_EMBEDDINGS_MODEL_PATH);

            if (modelUri.IsFile && !File.Exists(modelUri.LocalPath))
            {
                Console.Write("Please enter full embedding model's path: ");
                modelUri = new Uri(Console.ReadLine().Trim(new[] { '"' }));

                if (!File.Exists(modelUri.LocalPath))
                {
                    throw new FileNotFoundException($"Unable to open {modelUri.LocalPath}");
                }
            }

            _embeddingModel = new LM(modelUri,
                                   downloadingProgress: ModelDownloadingProgress);
        }

        private static void WriteLineColor(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ResetColor();
        }
    }
}