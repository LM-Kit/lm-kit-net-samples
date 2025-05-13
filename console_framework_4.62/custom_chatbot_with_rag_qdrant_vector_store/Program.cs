using LMKit.Data;
using LMKit.Data.Storage;
using LMKit.Data.Storage.Qdrant;
using LMKit.Global;
using LMKit.Model;
using LMKit.Retrieval;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Sampling;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using static LMKit.Retrieval.RagEngine;

namespace custom_chatbot_with_rag_qdrant_vector_store
{
    internal class Program
    {
        static readonly string DEFAULT_EMBEDDINGS_MODEL_PATH = @"https://huggingface.co/lm-kit/bge-1.5-gguf/resolve/main/bge-small-en-v1.5-f16.gguf?download=true";
        static readonly string DEFAULT_CHAT_MODEL_PATH = @"https://huggingface.co/lm-kit/gemma-3-4b-instruct-lmk/resolve/main/gemma-3-4b-it-Q4_K_M.lmk?download=true";
        static bool _isDownloading;
        static LM _chatModel;
        static LM _embeddingModel;
        static readonly List<DataSource> _dataSources = new List<DataSource>();
        static IVectorStore _store;

        static void Main(string[] args)
        {
            // Set an optional license key here if available. 
            // A free community license can be obtained from: https://lm-kit.com/products/community-edition/
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            //Initializing store
            //we're using local environment that we've started with: docker run -p 6333:6333 -p 6334:6334
            //check this tutorial to setup qdrant local environment: https://qdrant.tech/documentation/quickstart/
            _store = new QdrantEmbeddingStore(new Uri("http://localhost:6334"));

            //Loading models
            LoadChatModel();
            LoadEmbeddingModel();

            Console.Clear();
            WriteLineColor("*********************************************************************************************\n" +
                           "* In this demo, we are loading various eBooks, which can be queried semantically.\n" +
                           "* You can ask questions about the content of the loaded eBooks, \n" +
                           "* and the system will use a Retrieval-Augmented Generation (RAG) approach to provide answers.\n" +
                           "* This means that the chatbot will first retrieve relevant information from the eBooks\n" +
                           "* and then generate a coherent response based on that information.\n" +
                           "*********************************************************************************************\n", 
                           ConsoleColor.Blue);

            //Loading some famous eBooks
            WriteLineColor("Loading Romeo and Juliet eBook...", ConsoleColor.Green);
            _dataSources.Add(LoadUriAsDataSource(new Uri("https://gutenberg.org/cache/epub/1513/pg1513.txt"), "Romeo and Juliet"));

            WriteLineColor("Loading Moby Dick eBook...", ConsoleColor.Green);
            _dataSources.Add(LoadUriAsDataSource(new Uri("https://gutenberg.org/cache/epub/2701/pg2701.txt"), "Moby Dick"));

            WriteLineColor("Loading Pride and Prejudice eBook...", ConsoleColor.Green);
            _dataSources.Add(LoadUriAsDataSource(new Uri("https://gutenberg.org/cache/epub/1342/pg1342.txt"), "Pride and Prejudice"));

            RagEngine ragEngine = new RagEngine(_embeddingModel, vectorStore: _store);
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

        private static DataSource LoadUriAsDataSource(Uri uri, string dataSourceIdentifier)
        {
            if (_store.CollectionExistsAsync(dataSourceIdentifier).Result)
            {//using cached version
                Console.WriteLine($"   > {dataSourceIdentifier} loading datasource from store.");
                return DataSource.LoadFromStore(_store, dataSourceIdentifier);
            }

            //creating a new DataSource object using the RAG
            string eBookContent = DownloadContent(uri);
            Stopwatch stopwatch = Stopwatch.StartNew();
            RagEngine ragEngine = new RagEngine(_embeddingModel, _store);
            DataSource dataSource = ragEngine.ImportText(eBookContent, new TextChunking() { MaxChunkSize = 500 }, dataSourceIdentifier, "default");
            stopwatch.Stop();
            Console.WriteLine($"   > {dataSourceIdentifier} loaded in {Math.Round(stopwatch.Elapsed.TotalSeconds, 1)} seconds");

            return dataSource;
        }

        private static string DownloadContent(Uri uri)
        {
            using (var client = new HttpClient())
            {
                return client.GetStringAsync(uri).Result;
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