using LMKit.Agents;
using LMKit.Agents.Tools.BuiltIn;
using LMKit.Model;
using LMKit.TextGeneration.Chat;
using LMKit.TextGeneration.Events;
using System.Text;

namespace data_analyst_agent
{
    internal class Program
    {
        static bool _isDownloading;

        static bool OnDownloadProgress(string path, long? contentLength, long bytesRead)
        {
            _isDownloading = true;
            if (contentLength.HasValue)
                Console.Write($"\rDownloading model {Math.Round((double)bytesRead / contentLength.Value * 100, 2):0.00}%");
            else
                Console.Write($"\rDownloading model {bytesRead} bytes");
            return true;
        }

        static bool OnLoadProgress(float progress)
        {
            if (_isDownloading) { Console.Clear(); _isDownloading = false; }
            Console.Write($"\rLoading model {Math.Round(progress * 100)}%");
            return true;
        }

        static LM LoadModel(string input)
        {
            string? modelId = input?.Trim() switch
            {
                "0" => "qwen3:8b",
                "1" => "gemma3:12b",
                "2" => "qwen3:14b",
                "3" => "phi4",
                "4" => "gptoss:20b",
                "5" => "glm4.7-flash",
                "6" => "qwen3.5:27b",
                _ => null
            };

            if (modelId != null)
                return LM.LoadFromModelID(modelId, downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);

            string uri = !string.IsNullOrWhiteSpace(input) ? input.Trim('"') : "qwen3:8b";
            if (!uri.Contains("://"))
                return LM.LoadFromModelID(uri, downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);

            return new LM(new Uri(uri), downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);
        }

        private static void Main(string[] args)
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            Console.WriteLine("=== Local Data Analyst Agent Demo ===\n");
            Console.WriteLine("This demo showcases an AI agent that analyzes data files locally.");
            Console.WriteLine("Your data never leaves your machine.\n");
            Console.WriteLine("The agent can: read files, parse CSV data, perform calculations,");
            Console.WriteLine("compute statistics, and generate insights.\n");

            Console.WriteLine("Please select the model you want to use:\n");
            Console.WriteLine("0 - Alibaba Qwen-3 8B      (~6 GB VRAM) [Recommended]");
            Console.WriteLine("1 - Google Gemma 3 12B      (~9 GB VRAM)");
            Console.WriteLine("2 - Alibaba Qwen-3 14B      (~10 GB VRAM)");
            Console.WriteLine("3 - Microsoft Phi-4 14.7B    (~11 GB VRAM)");
            Console.WriteLine("4 - OpenAI GPT OSS 20B       (~16 GB VRAM)");
            Console.WriteLine("5 - Z.ai GLM 4.7 Flash 30B   (~18 GB VRAM)");
            Console.WriteLine("6 - Alibaba Qwen-3.5 27B     (~18 GB VRAM)");
            Console.Write("Other: Custom model URI or model ID\n\n> ");

            string inputStr = Console.ReadLine() ?? string.Empty;
            LM model = LoadModel(inputStr);

            Console.Clear();

            var agent = Agent.CreateBuilder(model)
                .WithPersona("Data Analyst Assistant")
                .WithInstruction(
                    "You are an expert data analyst assistant with access to tools for reading files, " +
                    "parsing structured data, and performing computations.\n\n" +
                    "Your capabilities:\n" +
                    "- Read files from the local file system (CSV, JSON, XML, text)\n" +
                    "- List directory contents to discover available data files\n" +
                    "- Parse CSV data into structured rows and columns\n" +
                    "- Parse JSON and XML data\n" +
                    "- Perform mathematical calculations (arithmetic, percentages, ratios)\n" +
                    "- Compute descriptive statistics (mean, median, min, max, standard deviation)\n" +
                    "- Analyze trends, outliers, and patterns in data\n\n" +
                    "When analyzing data:\n" +
                    "1. First read and understand the data structure\n" +
                    "2. Compute relevant statistics and metrics\n" +
                    "3. Identify key insights, trends, and anomalies\n" +
                    "4. Present findings in a clear, structured format\n\n" +
                    "Always state your methodology and show key numbers to support conclusions. " +
                    "Format results with clear headings and bullet points.")
                .WithPlanning(PlanningStrategy.ReAct)
                .WithTools(tools =>
                {
                    tools.Register(BuiltInTools.FileSystemRead);
                    tools.Register(BuiltInTools.FileSystemList);
                    tools.Register(BuiltInTools.CsvParse);
                    tools.Register(BuiltInTools.JsonParse);
                    tools.Register(BuiltInTools.XmlParse);
                    tools.Register(BuiltInTools.Calculator);
                    tools.Register(BuiltInTools.Statistics);
                })
                .WithMaxIterations(15)
                .Build();

            var executor = new AgentExecutor();
            executor.AfterTextCompletion += OnAfterTextCompletion;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("LM-Kit Local Data Analyst Agent");
            Console.ResetColor();
            Console.WriteLine("An AI agent that analyzes your data files locally with zero cloud dependency.");
            Console.WriteLine("Point it at a CSV, JSON, or text file and ask questions about your data.\n");
            Console.WriteLine("Example prompts:");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("  > Analyze the file C:\\data\\sales_q4.csv and summarize key trends");
            Console.WriteLine("  > List files in C:\\data\\ and tell me what datasets are available");
            Console.WriteLine("  > Read C:\\reports\\metrics.json and compute monthly growth rates");
            Console.WriteLine("  > What are the top 5 products by revenue in C:\\data\\products.csv?");
            Console.ResetColor();
            Console.WriteLine("\nType 'q' to quit.\n");

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("You: ");
                Console.ResetColor();

                string? prompt = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(prompt))
                    continue;

                if (string.Equals(prompt, "q", StringComparison.OrdinalIgnoreCase))
                    break;

                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine("\nAnalyzing...");
                Console.ResetColor();

                try
                {
                    var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                    var result = executor.Execute(agent, prompt, cts.Token);

                    Console.WriteLine();

                    if (result.IsSuccess)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.Write($"  [{result.ToolCalls.Count} tool call(s)");
                        Console.Write($", {result.Duration.TotalSeconds:F1}s");
                        Console.Write($", {result.InferenceCount} inference(s)");
                        Console.WriteLine("]");
                        Console.ResetColor();
                    }
                    else if (result.IsFailed)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"\nError: {result.Error?.Message}");
                        Console.ResetColor();
                    }
                    else if (result.IsCancelled)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("\nExecution was cancelled.");
                        Console.ResetColor();
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\nError: {ex.Message}");
                    Console.ResetColor();
                }

                Console.WriteLine();
            }

            Console.WriteLine("\nDemo ended. Press any key to exit.");
            Console.ReadKey();
        }

        private static void OnAfterTextCompletion(object? sender, AfterTextCompletionEventArgs e)
        {
            Console.ForegroundColor = e.SegmentType switch
            {
                TextSegmentType.ToolInvocation => ConsoleColor.Magenta,
                TextSegmentType.InternalReasoning => ConsoleColor.Blue,
                _ => ConsoleColor.White
            };
            Console.Write(e.Text);
            Console.ResetColor();
        }
    }
}
