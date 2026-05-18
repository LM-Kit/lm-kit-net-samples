using LMKit.Global;
using LMKit.Hardware;
using LMKit.Hardware.Gpu;
using LMKit.Model;
using System.Text;

namespace model_catalog
{
    internal class Program
    {
        private static bool _isDownloading;

        static void Main()
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            Console.WriteLine("=== LM-Kit Model Catalog Browser ===\n");

            Runtime.Initialize();
            ReportHost();
            Console.WriteLine();

            Console.WriteLine("Pick a capability to list models for:\n");
            Console.WriteLine("  1 - Chat / text generation");
            Console.WriteLine("  2 - Vision (multimodal chat with images)");
            Console.WriteLine("  3 - OCR (vision-language OCR models)");
            Console.WriteLine("  4 - Embeddings (text-to-vector)");
            Console.WriteLine("  5 - Speech-to-text (Whisper)");
            Console.WriteLine("  6 - Reasoning (chain-of-thought capable)");
            Console.WriteLine("  7 - Show everything\n");
            Console.Write("> ");

            string choice = Console.ReadLine()?.Trim() ?? "1";
            ModelCapabilities? filter = choice switch
            {
                "1" => ModelCapabilities.Chat,
                "2" => ModelCapabilities.Vision,
                "3" => ModelCapabilities.OCR,
                "4" => ModelCapabilities.TextEmbeddings,
                "5" => ModelCapabilities.SpeechToText,
                "6" => ModelCapabilities.Reasoning,
                _   => null,
            };

            List<ModelCard> cards = ModelCard.GetPredefinedModelCards()
                .Where(c => filter == null || c.Capabilities.HasFlag(filter.Value))
                .OrderBy(c => c.FileSize)
                .ToList();

            Console.WriteLine();
            Console.WriteLine($"Found {cards.Count} model(s) matching the filter.\n");

            // Column widths chosen to fit ID/Name comfortably on 100-col terminals.
            const int wIdx = 3, wId = 24, wName = 32, wSize = 10, wCtx = 8, wQuant = 6, wFit = 14;
            string sep = new('-',
                wIdx + 3 + wId + 3 + wName + 3 + wSize + 3 + wCtx + 3 + wQuant + 3 + wFit + 3 + 12);

            Console.WriteLine(string.Format(
                "{0,-" + wIdx + "} | {1,-" + wId + "} | {2,-" + wName + "} | {3," + wSize +
                "} | {4," + wCtx + "} | {5," + wQuant + "} | {6,-" + wFit + "} | Capabilities",
                "#", "Model ID", "Name", "Size", "Context", "Quant", "Fit"));
            Console.WriteLine(sep);

            for (int i = 0; i < cards.Count; i++)
            {
                ModelCard c = cards[i];
                float score = DeviceConfiguration.GetPerformanceScore(c);
                string fitText = DescribeFit(score);
                ConsoleColor color = FitColor(score);

                Console.Write(string.Format(
                    "{0," + wIdx + "} | {1,-" + wId + "} | {2,-" + wName + "} | {3," + wSize +
                    "} | {4," + wCtx + ":N0} | {5," + wQuant + ":F1} | ",
                    i + 1, Truncate(c.ModelID, wId), Truncate(c.ModelName, wName),
                    FormatBytes(c.FileSize), c.ContextLength, c.QuantizationPrecision));

                Console.ForegroundColor = color;
                Console.Write(string.Format("{0,-" + wFit + "}", fitText));
                Console.ResetColor();

                Console.WriteLine($" | {ShortCaps(c.Capabilities)}");
            }

            Console.WriteLine();
            Console.WriteLine("Fit legend:");
            WriteColored(" good ",  ConsoleColor.Green);  Console.Write("score >= 0.80 (plenty of headroom)\n");
            WriteColored(" ok   ",  ConsoleColor.Yellow); Console.Write("score >= 0.50 (fits but tight)\n");
            WriteColored(" tight",  ConsoleColor.DarkYellow); Console.Write("score >= 0.30 (likely paged to CPU)\n");
            WriteColored(" no   ",  ConsoleColor.Red);    Console.Write("score < 0.30  (does not fit in VRAM)\n");
            Console.WriteLine();

            Console.Write("Enter the row number to load that model, or blank to exit: ");
            string pick = Console.ReadLine()?.Trim() ?? "";
            if (!int.TryParse(pick, out int idx) || idx < 1 || idx > cards.Count)
            {
                Console.WriteLine("No selection. Exiting.");
                return;
            }

            ModelCard selected = cards[idx - 1];
            Console.WriteLine();
            Console.WriteLine($"Loading {selected.ModelID} from {selected.ModelUri}");
            Console.WriteLine($"License  : {selected.License}");
            Console.WriteLine($"Publisher: {selected.Publisher}");
            Console.WriteLine();

            using LM model = LM.LoadFromModelID(
                selected.ModelID,
                downloadingProgress: OnDownloadProgress,
                loadingProgress: OnLoadProgress);

            Console.WriteLine();
            Console.WriteLine($"Loaded: {model.Name}");
            Console.WriteLine($"  Architecture       : {model.Architecture}");
            Console.WriteLine($"  Parameter count    : {model.ParameterCount:N0}");
            Console.WriteLine($"  Embedding size     : {model.EmbeddingSize}");
            Console.WriteLine($"  Context length     : {model.ContextLength:N0}");
            Console.WriteLine($"  Optimal context    : {DeviceConfiguration.GetOptimalContextSize(model):N0}");
            Console.WriteLine($"  Layers (total/gpu) : {model.LayerCount} / {model.GpuLayerCount}");
            Console.WriteLine($"  Vision             : {model.HasVision}");
            Console.WriteLine($"  Tools              : {model.HasToolCalls}");
            Console.WriteLine($"  Speech-to-text     : {model.HasSpeechToText}");
            Console.WriteLine($"  Reasoning          : {model.HasReasoning}");
            Console.WriteLine($"  Embeddings only    : {model.IsEmbeddingModel}");
            Console.WriteLine();
            Console.WriteLine($"Cached at: {model.ModelPath}");
        }

        static void ReportHost()
        {
            Console.WriteLine($"Runtime backend   : {Runtime.Backend}");
            Console.WriteLine($"GPU support       : {Runtime.HasGpuSupport}");
            IReadOnlyList<GpuDeviceInfo> gpus = GpuDeviceInfo.Devices;
            if (gpus.Count == 0)
            {
                Console.WriteLine("Visible GPUs      : (none, CPU-only inference)");
                return;
            }
            Console.WriteLine($"Visible GPUs ({gpus.Count}):");
            foreach (GpuDeviceInfo g in gpus)
            {
                Console.WriteLine(
                    $"  #{g.DeviceNumber}  {Truncate(g.DeviceName, 36),-36}  " +
                    $"{FormatBytes((long)g.TotalMemorySize),10} total  /  " +
                    $"{FormatBytes((long)g.FreeMemorySize),10} free");
            }
        }

        static string DescribeFit(float score) => score switch
        {
            >= 0.80f => "good",
            >= 0.50f => "ok",
            >= 0.30f => "tight",
            _        => "no",
        };

        static ConsoleColor FitColor(float score) => score switch
        {
            >= 0.80f => ConsoleColor.Green,
            >= 0.50f => ConsoleColor.Yellow,
            >= 0.30f => ConsoleColor.DarkYellow,
            _        => ConsoleColor.Red,
        };

        static void WriteColored(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.Write(text);
            Console.ResetColor();
            Console.Write("  ");
        }

        static string Truncate(string s, int max) =>
            string.IsNullOrEmpty(s) ? "" : s.Length <= max ? s : s.Substring(0, max - 1) + "…";

        static string FormatBytes(long bytes)
        {
            if (bytes <= 0) { return "?"; }
            string[] units = ["B", "KB", "MB", "GB"];
            double v = bytes;
            int u = 0;
            while (v >= 1024 && u < units.Length - 1) { v /= 1024; u++; }
            return $"{v:F1} {units[u]}";
        }

        static string ShortCaps(ModelCapabilities caps)
        {
            List<string> flags = [];
            if (caps.HasFlag(ModelCapabilities.Chat))               { flags.Add("chat"); }
            if (caps.HasFlag(ModelCapabilities.Vision))             { flags.Add("vision"); }
            if (caps.HasFlag(ModelCapabilities.OCR))                { flags.Add("ocr"); }
            if (caps.HasFlag(ModelCapabilities.TextEmbeddings))     { flags.Add("embed"); }
            if (caps.HasFlag(ModelCapabilities.ImageEmbeddings))    { flags.Add("img-embed"); }
            if (caps.HasFlag(ModelCapabilities.SpeechToText))       { flags.Add("stt"); }
            if (caps.HasFlag(ModelCapabilities.Reasoning))          { flags.Add("reasoning"); }
            if (caps.HasFlag(ModelCapabilities.ToolsCall))          { flags.Add("tools"); }
            if (caps.HasFlag(ModelCapabilities.TextReranking))      { flags.Add("rerank"); }
            return string.Join(" ", flags);
        }

        static bool OnDownloadProgress(string path, long? contentLength, long bytesRead)
        {
            _isDownloading = true;
            if (contentLength.HasValue)
            {
                Console.Write($"\rDownloading {Math.Round((double)bytesRead / contentLength.Value * 100, 2):0.00}%");
            }
            return true;
        }

        static bool OnLoadProgress(float progress)
        {
            if (_isDownloading) { Console.WriteLine(); _isDownloading = false; }
            Console.Write($"\rLoading {Math.Round(progress * 100)}%");
            return true;
        }
    }
}
