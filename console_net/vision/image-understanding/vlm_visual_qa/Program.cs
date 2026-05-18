using LMKit.Data;
using LMKit.Model;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Chat;
using System.Text;

namespace vlm_visual_qa
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

            using LM model = LoadModelInteractive();
            if (!model.HasVision)
            {
                Console.WriteLine();
                Console.WriteLine("The selected model does not have vision support.");
                return;
            }
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
                    case "1": case "chat":
                        ChatWithImage(model);
                        break;
                    case "2": case "audit":
                        StandardAudit(model);
                        break;
                    case "3": case "folder":
                        FolderCaptions(model);
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

        static void ChatWithImage(LM model)
        {
            Console.WriteLine();
            Console.Write("Path to an image: ");
            string? path = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) { Console.WriteLine("File not found."); return; }
            using Attachment att = new(path);

            MultiTurnConversation chat = new(model)
            {
                MaximumCompletionTokens = 400,
                SystemPrompt = "You are a concise visual analyst. Reply briefly and factually.",
            };
            chat.AfterTextCompletion += (_, e) => Console.Write(e.Text);

            bool first = true;
            while (true)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("Q (blank = back to menu): ");
                Console.ResetColor();
                string? q = Console.ReadLine();
                if (string.IsNullOrEmpty(q)) { return; }

                Console.Write("A: ");
                if (first) { chat.Submit(new ChatHistory.Message(q, att)); first = false; }
                else { chat.Submit(q); }
                Console.WriteLine();
            }
        }

        static void StandardAudit(LM model)
        {
            Console.WriteLine();
            Console.Write("Path to an image: ");
            string? path = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) { Console.WriteLine("File not found."); return; }
            using Attachment att = new(path);

            string[] prompts =
            {
                "In one sentence, caption this image.",
                "Describe the image in three sentences. Mention objects, layout, and lighting.",
                "Count the number of distinct people visible. Reply with just the integer.",
                "Is this image taken outdoors? Answer with 'yes' or 'no' and one short reason.",
            };

            foreach (string p in prompts)
            {
                MultiTurnConversation chat = new(model)
                {
                    MaximumCompletionTokens = 200,
                    SystemPrompt = "You are a concise visual analyst. Reply briefly and factually.",
                };
                chat.AfterTextCompletion += (_, e) => Console.Write(e.Text);
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Q: {p}");
                Console.ResetColor();
                Console.Write("A: ");
                chat.Submit(new ChatHistory.Message(p, att));
                Console.WriteLine();
            }
            Console.WriteLine();
        }

        static void FolderCaptions(LM model)
        {
            Console.WriteLine();
            Console.Write("Path to a folder of images: ");
            string? dir = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) { Console.WriteLine("Folder not found."); return; }
            Console.Write("Output CSV path: ");
            string? csvPath = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(csvPath)) { Console.WriteLine("Output CSV required."); return; }
            Console.Write("Caption prompt (default 'In one sentence, caption this image.'): ");
            string prompt = Console.ReadLine()?.Trim() ?? "";
            if (string.IsNullOrEmpty(prompt)) { prompt = "In one sentence, caption this image."; }

            string[] exts = { ".png", ".jpg", ".jpeg", ".bmp", ".webp", ".tif", ".tiff" };
            string[] files = Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
                .Where(f => exts.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();
            if (files.Length == 0) { Console.WriteLine("No images found."); return; }

            using StreamWriter csv = new(csvPath, false, new UTF8Encoding(true));
            csv.WriteLine("path,caption");

            foreach (string p in files)
            {
                try
                {
                    using Attachment att = new(p);
                    MultiTurnConversation chat = new(model)
                    {
                        MaximumCompletionTokens = 120,
                        SystemPrompt = "You are a concise visual analyst. Reply briefly and factually.",
                    };
                    StringBuilder sb = new();
                    chat.AfterTextCompletion += (_, e) => sb.Append(e.Text);
                    chat.Submit(new ChatHistory.Message(prompt, att));
                    string caption = sb.ToString().Trim().Replace("\n", " ").Replace("\r", " ");
                    csv.Write(Csv(p)); csv.Write(','); csv.WriteLine(Csv(caption));
                    Console.WriteLine($"  {Path.GetFileName(p),-40} {caption}");
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  [error] {Path.GetFileName(p)}: {ex.Message}");
                    Console.ResetColor();
                }
            }
            Console.WriteLine();
            Console.WriteLine($"CSV: {csvPath}");
            Console.WriteLine();
        }

        static string Csv(string s)
        {
            if (s == null) { return ""; }
            bool needsQuotes = s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
            string body = s.Replace("\"", "\"\"");
            return needsQuotes ? $"\"{body}\"" : body;
        }

        static LM LoadModelInteractive()
        {
            Console.WriteLine("Select a vision model:");
            Console.WriteLine("  1 - Alibaba Qwen 3.5 4B           (~3 GB VRAM) [Recommended]");
            Console.WriteLine("  2 - Alibaba Qwen 3.5 9B           (~6 GB VRAM)");
            Console.WriteLine("  3 - Google Gemma 4 E2B            (~3 GB VRAM)");
            Console.WriteLine("  4 - Google Gemma 4 E4B            (~5 GB VRAM)");
            Console.WriteLine("  5 - GLM 4.6V Flash                (~6 GB VRAM)");
            Console.Write("\nOr enter a custom model URI / id\n> ");
            string input = Console.ReadLine()?.Trim() ?? "1";
            string? modelId = input switch
            {
                "1" or "" => "qwen3.5:4b",
                "2" => "qwen3.5:9b",
                "3" => "gemma4:e2b",
                "4" => "gemma4:e4b",
                "5" => "glm-4.6v-flash",
                _ => null,
            };
            if (modelId != null)
            {
                return LM.LoadFromModelID(modelId, downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);
            }
            if (Uri.TryCreate(input.Trim('"'), UriKind.Absolute, out Uri? uri))
            {
                return new LM(uri, downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);
            }
            return LM.LoadFromModelID(input, downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);
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
            Console.WriteLine("║      VLM Visual Q&A                              ║");
            Console.WriteLine("╚══════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine("Ask vision-language questions about images, on-device.");
            Console.WriteLine();
        }

        static void PrintMenu()
        {
            Console.WriteLine();
            Console.WriteLine("  1 / chat     Chat with one image (REPL of questions)");
            Console.WriteLine("  2 / audit    Run a standard 4-question audit on one image");
            Console.WriteLine("  3 / folder   Caption every image in a folder, write CSV");
            Console.WriteLine("  q / quit     Exit");
            Console.WriteLine();
        }
    }
}
