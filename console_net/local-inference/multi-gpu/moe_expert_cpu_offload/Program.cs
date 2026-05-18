using LMKit.Global;
using LMKit.Hardware.Gpu;
using LMKit.Model;
using LMKit.TextGeneration;
using System.Text;

namespace moe_expert_cpu_offload
{
    internal class Program
    {
        static bool _isDownloading;

        static void Main()
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;
            Console.Clear();
            WriteHeader();

            Runtime.Initialize();

            IReadOnlyList<GpuDeviceInfo> gpus = GpuDeviceInfo.Devices;
            if (gpus.Count == 0)
            {
                Console.WriteLine("No GPU device visible. This demo requires at least one GPU.");
                return;
            }
            Console.WriteLine($"Detected {gpus.Count} GPU(s):");
            foreach (GpuDeviceInfo g in gpus)
            {
                Console.WriteLine($"  GPU #{g.DeviceNumber}: {g.DeviceName} ({FormatBytes((long)g.TotalMemorySize)})");
            }
            Configuration.FavorDistributedInference = gpus.Count > 1;
            Console.WriteLine($"FavorDistributedInference = {Configuration.FavorDistributedInference}");
            Console.WriteLine();

            PrintMenu();
            LM? loaded = null;
            try
            {
                while (true)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("> ");
                    Console.ResetColor();
                    string? choice = Console.ReadLine()?.Trim();
                    if (string.IsNullOrEmpty(choice)) { continue; }

                    switch (choice.ToLowerInvariant())
                    {
                        case "1": case "load":
                            loaded?.Dispose();
                            loaded = PromptAndLoadModel();
                            break;
                        case "2": case "chat":
                            if (loaded == null) { Console.WriteLine("Load a model first (option 1)."); break; }
                            ChatTurn(loaded);
                            break;
                        case "3": case "bench":
                            if (loaded == null) { Console.WriteLine("Load a model first (option 1)."); break; }
                            Benchmark(loaded);
                            break;
                        case "q": case "quit": case "exit":
                            return;
                        case "?": case "help": case "menu":
                            PrintMenu();
                            break;
                        default:
                            Console.WriteLine("Unknown choice. Type '?' to see the menu.");
                            break;
                    }
                }
            }
            finally
            {
                loaded?.Dispose();
            }
        }

        static LM? PromptAndLoadModel()
        {
            Console.WriteLine();
            Console.WriteLine("Pick an MoE model (or any model with `_exps.weight` tensors):");
            Console.WriteLine("  1  gptoss:20b           (GPT-OSS 20B MoE)");
            Console.WriteLine("  2  qwen3.6:35b-a3b      (Qwen 3.6 35B-A3B MoE)");
            Console.WriteLine("  3  qwen3.5:35b-a3b      (Qwen 3.5 35B-A3B MoE)");
            Console.WriteLine("  4  gemma4:26b-a4b       (Gemma 4 26B-A4B MoE)");
            Console.WriteLine("  5  glm4.7-flash         (GLM 4.7 Flash MoE)");
            Console.Write("Or type a custom modelId\n> ");
            string input = Console.ReadLine()?.Trim() ?? "";
            string modelId = input switch
            {
                "" or "1" => "gptoss:20b",
                "2" => "qwen3.6:35b-a3b",
                "3" => "qwen3.5:35b-a3b",
                "4" => "gemma4:26b-a4b",
                "5" => "glm4.7-flash",
                _ => input,
            };

            ModelCard? card = ModelCard.GetPredefinedModelCardByModelID(modelId);
            if (card == null) { Console.WriteLine($"Unknown model id '{modelId}'."); return null; }

            LM.DeviceConfiguration config = new()
            {
                GpuLayerCount = int.MaxValue,
                AutoFitToVram = true,
                TensorOverrides = new List<LM.TensorOverride>
                {
                    LM.TensorOverride.Cpu(@"\.ffn_.*_exps\.weight"),
                    LM.TensorOverride.Gpu(@"blk\.(0|1|2)\.attn", gpuIndex: 0),
                },
            };

            Console.WriteLine();
            Console.WriteLine($"Loading {card.ModelName} (~{FormatBytes(card.FileSize)})...");
            try
            {
                LM model = new(card.ModelUri, deviceConfiguration: config,
                    downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine($"Loaded               : {model.Name}");
                Console.WriteLine($"Layers (total / GPU) : {model.LayerCount} / {model.GpuLayerCount}");
                Console.WriteLine($"Parameters           : {model.ParameterCount:N0}");
                Console.WriteLine();
                return model;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n  [error] {ex.Message}");
                Console.ResetColor();
                return null;
            }
        }

        static void ChatTurn(LM model)
        {
            Console.WriteLine();
            Console.Write("Prompt: ");
            string? prompt = Console.ReadLine();
            if (string.IsNullOrEmpty(prompt)) { return; }

            MultiTurnConversation chat = new(model) { MaximumCompletionTokens = 400 };
            chat.AfterTextCompletion += (_, e) => Console.Write(e.Text);
            Console.WriteLine();
            var r = chat.Submit(prompt);
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine($"  Throughput: {r.TokenGenerationRate:F1} tok/s over {r.GeneratedTokens.Count} tokens.");
            Console.WriteLine();
        }

        static void Benchmark(LM model)
        {
            Console.WriteLine();
            Console.Write("Number of warm + measured passes (default 1 warm + 3 measured): ");
            string? raw = Console.ReadLine()?.Trim();
            int measured = 3;
            if (!string.IsNullOrEmpty(raw) && int.TryParse(raw, out int parsed) && parsed > 0) { measured = parsed; }

            const string prompt = "In two short paragraphs, explain why MoE expert tensors are good candidates for CPU offload.";

            MultiTurnConversation warm = new(model) { MaximumCompletionTokens = 200 };
            Console.WriteLine("Warm-up pass...");
            warm.Submit(prompt);

            double sum = 0;
            for (int i = 0; i < measured; i++)
            {
                MultiTurnConversation chat = new(model) { MaximumCompletionTokens = 200 };
                var r = chat.Submit(prompt);
                Console.WriteLine($"  pass {i + 1}: {r.TokenGenerationRate:F1} tok/s ({r.GeneratedTokens.Count} tokens)");
                sum += r.TokenGenerationRate;
            }
            Console.WriteLine();
            Console.WriteLine($"  mean throughput: {sum / measured:F1} tok/s");
            Console.WriteLine();
        }

        static string FormatBytes(long bytes)
        {
            if (bytes <= 0) { return "?"; }
            string[] units = { "B", "KB", "MB", "GB" };
            double v = bytes;
            int u = 0;
            while (v >= 1024 && u < units.Length - 1) { v /= 1024; u++; }
            return $"{v:F1} {units[u]}";
        }

        static bool OnDownloadProgress(string path, long? contentLength, long bytesRead)
        {
            _isDownloading = true;
            if (contentLength.HasValue) { Console.Write($"\rDownloading {Math.Round((double)bytesRead / contentLength.Value * 100, 2):0.00}%"); }
            return true;
        }

        static bool OnLoadProgress(float progress)
        {
            if (_isDownloading) { Console.WriteLine(); _isDownloading = false; }
            Console.Write($"\rLoading {Math.Round(progress * 100)}%");
            return true;
        }

        static void WriteHeader()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════════════════════════════╗");
            Console.WriteLine("║      MoE Expert CPU Offload                      ║");
            Console.WriteLine("╚══════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine("Pin MoE expert weights to CPU and hot attention blocks to GPU. Run an MoE model on a smaller card.");
            Console.WriteLine();
        }

        static void PrintMenu()
        {
            Console.WriteLine();
            Console.WriteLine("  1 / load     Choose an MoE model and load with CPU-expert + GPU-hot overrides");
            Console.WriteLine("  2 / chat     Free-form prompt on the loaded model (streamed)");
            Console.WriteLine("  3 / bench    Run the standard prompt N times and report mean tok/s");
            Console.WriteLine("  q / quit     Exit");
            Console.WriteLine();
        }
    }
}
