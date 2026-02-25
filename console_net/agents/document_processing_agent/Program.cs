using LMKit.Agents;
using LMKit.Agents.Tools.BuiltIn;
using LMKit.Model;
using LMKit.TextGeneration.Chat;
using LMKit.TextGeneration.Events;
using System.Text;

namespace document_processing_agent
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
                "1" => "qwen3:4b",
                "2" => "qwen3:14b",
                "3" => "gptoss:20b",
                "4" => "glm4.7-flash",
                "5" => "qwen3.5:27b",
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
            Console.WriteLine("Please select the model you want to use:\n");
            Console.WriteLine("0 - Alibaba Qwen 3 8B      (~6 GB VRAM) [Recommended]");
            Console.WriteLine("1 - Alibaba Qwen 3 4B      (~3 GB VRAM)");
            Console.WriteLine("2 - Alibaba Qwen 3 14B     (~11 GB VRAM)");
            Console.WriteLine("3 - OpenAI GPT OSS 20B      (~15 GB VRAM)");
            Console.WriteLine("4 - Z.ai GLM 4.7 Flash      (~18 GB VRAM)");
            Console.WriteLine("5 - Alibaba Qwen 3.5 27B    (~18 GB VRAM)");
            Console.Write("Other: Custom model URI or model ID\n\n> ");

            string inputStr = Console.ReadLine() ?? string.Empty;
            LM model = LoadModel(inputStr);

            Console.Clear();

            var agent = Agent.CreateBuilder(model)
                .WithPersona("Document Processing Assistant")
                .WithInstruction(
                    "You are a document processing assistant with access to tools for handling PDFs and images.\n\n" +
                    "Your capabilities:\n" +
                    "- Inspect PDFs: get page count, dimensions, metadata, and text content\n" +
                    "- Split PDFs: extract page ranges into separate files\n" +
                    "- Merge PDFs: combine multiple PDF files into one\n" +
                    "- Render PDFs: convert pages to JPEG, PNG, or BMP images\n" +
                    "- Convert images to PDF: combine JPEG, PNG, or BMP images into a single PDF\n" +
                    "- Unlock PDFs: remove password protection using the known password\n" +
                    "- Deskew images: correct rotation in scanned documents\n" +
                    "- Crop images: remove uniform borders from scans\n" +
                    "- Resize images: scale images or fit within bounding boxes\n" +
                    "- Extract text: get text content from PDF, DOCX, XLSX, PPTX, EML, MBOX, HTML\n" +
                    "- OCR: extract text from images using Tesseract (34 languages)\n\n" +
                    "Always confirm what actions you took and report results clearly.")
                .WithTools(tools =>
                {
                    tools.Register(BuiltInTools.PdfMetadata);
                    tools.Register(BuiltInTools.PdfPages);
                    tools.Register(BuiltInTools.PdfSplit);
                    tools.Register(BuiltInTools.PdfMerge);
                    tools.Register(BuiltInTools.PdfToImage);
                    tools.Register(BuiltInTools.ImageToPdf);
                    tools.Register(BuiltInTools.PdfUnlock);
                    tools.Register(BuiltInTools.ImageDeskew);
                    tools.Register(BuiltInTools.ImageCrop);
                    tools.Register(BuiltInTools.ImageResize);
                    tools.Register(BuiltInTools.DocumentTextExtract);
                    tools.Register(BuiltInTools.OcrRecognize);
                })
                .WithMaxIterations(15)
                .Build();

            var executor = new AgentExecutor();
            executor.AfterTextCompletion += OnAfterTextCompletion;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("LM-Kit Document Processing Agent");
            Console.ResetColor();
            Console.WriteLine("An AI agent with document tools: PDF split/merge/info/render/unlock, image-to-pdf/deskew/crop/resize, text extraction, and OCR.");
            Console.WriteLine("Type a document processing task, or 'q' to quit.\n");

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
                Console.WriteLine("\nProcessing...");
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
