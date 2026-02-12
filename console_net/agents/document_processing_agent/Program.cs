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
        private static bool _isDownloading;

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

        private static void Main(string[] args)
        {
            // Set an optional license key here if available.
            // A free community license can be obtained from: https://lm-kit.com/products/community-edition/
            LMKit.Licensing.LicenseManager.SetLicenseKey("");

            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            Console.WriteLine("Please select the model you want to use:\n");
            Console.WriteLine("0 - Alibaba Qwen 3 8B (requires approximately 6.5 GB of VRAM) (recommended)");
            Console.WriteLine("1 - Alibaba Qwen 3 4B (requires approximately 3 GB of VRAM)");
            Console.WriteLine("2 - Alibaba Qwen 3 14B (requires approximately 11 GB of VRAM)");
            Console.WriteLine("3 - GPT-OSS 20B (requires approximately 15 GB of VRAM)");
            Console.WriteLine("4 - GLM 4.7 Flash (requires approximately 18 GB of VRAM)");

            Console.Write("Other entry: A custom model URI\n\n> ");

            string input = Console.ReadLine() ?? string.Empty;
            string modelLink;

            switch (input.Trim())
            {
                case "0":
                    modelLink = ModelCard.GetPredefinedModelCardByModelID("qwen3:8b").ModelUri.ToString();
                    break;
                case "1":
                    modelLink = ModelCard.GetPredefinedModelCardByModelID("qwen3:4b").ModelUri.ToString();
                    break;
                case "2":
                    modelLink = ModelCard.GetPredefinedModelCardByModelID("qwen3:14b").ModelUri.ToString();
                    break;
                case "3":
                    modelLink = ModelCard.GetPredefinedModelCardByModelID("gptoss:20b").ModelUri.ToString();
                    break;
                case "4":
                    modelLink = ModelCard.GetPredefinedModelCardByModelID("glm4.7-flash").ModelUri.ToString();
                    break;
                default:
                    modelLink = input.Trim().Trim('"').Trim('\u201C');
                    break;
            }

            // Loading model
            Uri modelUri = new(modelLink);
            LM model = new(
                modelUri,
                downloadingProgress: ModelDownloadingProgress,
                loadingProgress: ModelLoadingProgress);

            Console.Clear();

            // ──────────────────────────────────────
            // Create the document processing agent
            // ──────────────────────────────────────
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
                    "- Extract text: get text content from PDF, DOCX, XLSX, PPTX, HTML\n" +
                    "- OCR: extract text from images using Tesseract (34 languages)\n\n" +
                    "Always confirm what actions you took and report results clearly.")
                .WithTools(tools =>
                {
                    tools.Register(BuiltInTools.PdfInfo);
                    tools.Register(BuiltInTools.PdfSplit);
                    tools.Register(BuiltInTools.PdfMerge);
                    tools.Register(BuiltInTools.PdfToImage);
                    tools.Register(BuiltInTools.ImageToPdf);
                    tools.Register(BuiltInTools.PdfUnlock);
                    tools.Register(BuiltInTools.ImageDeskew);
                    tools.Register(BuiltInTools.ImageCrop);
                    tools.Register(BuiltInTools.ImageResize);
                    tools.Register(BuiltInTools.DocumentText);
                    tools.Register(BuiltInTools.Ocr);
                })
                .WithMaxIterations(15)
                .Build();

            // Create executor with streaming event handler
            var executor = new AgentExecutor();
            executor.AfterTextCompletion += OnAfterTextCompletion;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("LM-Kit Document Processing Agent");
            Console.ResetColor();
            Console.WriteLine("An AI agent with 11 document tools: PDF split/merge/info/render/unlock, image-to-pdf/deskew/crop/resize, text extraction, and OCR.");
            Console.WriteLine("Type a document processing task, or 'q' to quit.\n");

            // ──────────────────────────────────────
            // Interactive loop
            // ──────────────────────────────────────
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("You");
                Console.ResetColor();
                Console.Write(" \u2014 ");

                string? prompt = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(prompt))
                    continue;

                if (string.Equals(prompt, "q", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("\nExiting. Bye \U0001F44B");
                    break;
                }

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
                        // Show stats
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
        }

        /// <summary>
        /// Event handler to display agent output in real-time with color-coded segments.
        /// </summary>
        private static void OnAfterTextCompletion(object? sender, AfterTextCompletionEventArgs e)
        {
            switch (e.SegmentType)
            {
                case TextSegmentType.ToolInvocation:
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    break;
                case TextSegmentType.InternalReasoning:
                    Console.ForegroundColor = ConsoleColor.Blue;
                    break;
                case TextSegmentType.UserVisible:
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
            }

            Console.Write(e.Text);
            Console.ResetColor();
        }
    }
}
