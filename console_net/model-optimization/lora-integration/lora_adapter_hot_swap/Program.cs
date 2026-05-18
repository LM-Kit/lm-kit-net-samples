using LMKit.Finetuning;
using LMKit.Model;
using LMKit.TextGeneration;
using System.Text;

namespace lora_adapter_hot_swap
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

            Console.WriteLine("Loading qwen3.5:0.8b ...");
            using LM model = LM.LoadFromModelID("qwen3.5:0.8b",
                downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);
            Console.WriteLine();
            Console.WriteLine();

            PrintMenu();
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("> ");
                Console.ResetColor();
                string? choice = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(choice)) { continue; }

                switch (choice.ToLowerInvariant())
                {
                    case "1": case "compare":
                        Compare(model);
                        break;
                    case "2": case "apply":
                        ApplyAdapter(model);
                        break;
                    case "3": case "remove":
                        RemoveAdapter(model);
                        break;
                    case "4": case "list":
                        ListAdapters(model);
                        break;
                    case "5": case "chat":
                        ChatTurn(model);
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

        static void Compare(LM model)
        {
            Console.WriteLine();
            Console.WriteLine("Paste LoRA adapter paths one per line, blank ends:");
            List<string> adapters = new();
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"  [{adapters.Count + 1}] ");
                Console.ResetColor();
                string? line = Console.ReadLine()?.Trim().Trim('"');
                if (string.IsNullOrWhiteSpace(line)) { break; }
                if (!File.Exists(line)) { Console.WriteLine($"  (not found, skipped) {line}"); continue; }
                if (!LoraAdapterSource.ValidateFormat(line, throwException: false))
                {
                    Console.WriteLine($"  (invalid LoRA format, skipped) {line}");
                    continue;
                }
                adapters.Add(line);
            }
            if (adapters.Count == 0) { Console.WriteLine("No adapters provided."); return; }

            Console.Write("Prompt (blank = default): ");
            string? prompt = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(prompt))
            {
                prompt = "Describe a sunny morning at the harbour in one paragraph.";
            }
            Console.Write("Scale per adapter (default 1.0): ");
            float.TryParse(Console.ReadLine(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float scale);
            if (scale <= 0) { scale = 1.0f; }

            Console.WriteLine();
            Console.WriteLine("--- BASELINE (no adapter) ---");
            RunPrompt(model, prompt);

            foreach (string path in adapters)
            {
                Console.WriteLine();
                Console.WriteLine($"--- {Path.GetFileName(path)} @ scale {scale:F2} ---");
                try
                {
                    LoraAdapterSource src = new(path, scale: scale);
                    model.ApplyLoraAdapter(src);
                    RunPrompt(model, prompt);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  [error] {ex.Message}");
                    Console.ResetColor();
                }
                finally
                {
                    if (model.Adapters.Count > 0)
                    {
                        model.RemoveLoraAdapter(model.Adapters.Last());
                    }
                }
            }
            Console.WriteLine();
        }

        static void ApplyAdapter(LM model)
        {
            Console.WriteLine();
            Console.Write("Path to LoRA .gguf adapter: ");
            string? path = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) { Console.WriteLine("File not found."); return; }
            if (!LoraAdapterSource.ValidateFormat(path, throwException: false)) { Console.WriteLine("Invalid LoRA format."); return; }

            Console.Write("Scale (default 1.0): ");
            float.TryParse(Console.ReadLine(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float scale);
            if (scale <= 0) { scale = 1.0f; }

            try
            {
                LoraAdapterSource src = new(path, scale: scale);
                model.ApplyLoraAdapter(src);
                Console.WriteLine($"Applied. Active adapters: {model.Adapters.Count}");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  [error] {ex.Message}");
                Console.ResetColor();
            }
        }

        static void RemoveAdapter(LM model)
        {
            if (model.Adapters.Count == 0) { Console.WriteLine("No adapters applied."); return; }
            Console.WriteLine();
            int n = 1;
            foreach (LoraAdapter a in model.Adapters)
            {
                Console.WriteLine($"  {n++}  {Path.GetFileName(a.Path)}  (scale {a.Scale:F2})");
            }
            Console.Write("Remove which? (number or 'all'): ");
            string? raw = Console.ReadLine()?.Trim();
            if (raw == "all")
            {
                while (model.Adapters.Count > 0) { model.RemoveLoraAdapter(model.Adapters.Last()); }
                Console.WriteLine("All adapters removed.");
                return;
            }
            if (!int.TryParse(raw, out int idx) || idx < 1 || idx > model.Adapters.Count) { Console.WriteLine("Bad choice."); return; }
            model.RemoveLoraAdapter(model.Adapters[idx - 1]);
            Console.WriteLine($"Removed. Active adapters: {model.Adapters.Count}");
        }

        static void ListAdapters(LM model)
        {
            Console.WriteLine();
            if (model.Adapters.Count == 0) { Console.WriteLine("(no adapters applied)"); return; }
            int n = 1;
            foreach (LoraAdapter a in model.Adapters)
            {
                Console.WriteLine($"  {n++}  {Path.GetFileName(a.Path)}  (scale {a.Scale:F2})");
            }
        }

        static void ChatTurn(LM model)
        {
            Console.WriteLine();
            Console.Write("Prompt: ");
            string? prompt = Console.ReadLine();
            if (string.IsNullOrEmpty(prompt)) { return; }
            RunPrompt(model, prompt);
        }

        static void RunPrompt(LM model, string prompt)
        {
            MultiTurnConversation chat = new(model) { MaximumCompletionTokens = 200 };
            chat.AfterTextCompletion += (_, e) => Console.Write(e.Text);
            chat.Submit(prompt);
            Console.WriteLine();
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
            Console.WriteLine("║      LoRA Adapter Hot-Swap                       ║");
            Console.WriteLine("╚══════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine("Apply / remove LoRA adapters on a running model. Compare baseline against each.");
            Console.WriteLine();
        }

        static void PrintMenu()
        {
            Console.WriteLine();
            Console.WriteLine("  1 / compare   Provide adapters + prompt; run baseline and each adapter");
            Console.WriteLine("  2 / apply     Apply a single adapter to the running model");
            Console.WriteLine("  3 / remove    Remove an applied adapter (or 'all')");
            Console.WriteLine("  4 / list      List currently-applied adapters");
            Console.WriteLine("  5 / chat      Free-form prompt with whatever adapters are active");
            Console.WriteLine("  q / quit      Exit");
            Console.WriteLine();
        }
    }
}
