using LMKit.Global;
using LMKit.Inference;
using LMKit.Model;
using LMKit.TextGeneration;
using System.Diagnostics;
using System.Text;

namespace idle_session_hibernation
{
    internal class Program
    {
        static bool _isDownloading;

        static async Task Main()
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;
            Console.Clear();
            WriteHeader();

            string hibernateDir = Path.Combine(Path.GetTempPath(), "lmkit-hibernation-demo");
            Directory.CreateDirectory(hibernateDir);
            Configuration.ContextHibernationDirectory = hibernateDir;
            Console.WriteLine($"Hibernation directory: {hibernateDir}");
            Console.WriteLine();

            Console.WriteLine("Loading qwen3.5:0.8b ...");
            using LM model = LM.LoadFromModelID("qwen3.5:0.8b",
                downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);
            Console.WriteLine();
            Console.WriteLine();

            PrintMenu();

            MultiTurnConversation? chat = null;
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("> ");
                Console.ResetColor();
                string? choice = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(choice)) { continue; }

                switch (choice.ToLowerInvariant())
                {
                    case "1": case "start":
                        chat?.Dispose();
                        chat = new(model)
                        {
                            MaximumCompletionTokens = 200,
                            SystemPrompt = "You are a concise assistant. Answer in one short paragraph.",
                        };
                        Console.WriteLine("New conversation created.");
                        ReportState("After construction", (IKVCache)chat);
                        break;
                    case "2": case "ask":
                        if (chat == null) { Console.WriteLine("Start a conversation first (option 1)."); break; }
                        AskTurn(chat);
                        break;
                    case "3": case "hibernate":
                        if (chat == null) { Console.WriteLine("Start a conversation first (option 1)."); break; }
                        await Hibernate(chat, hibernateDir);
                        break;
                    case "4": case "scripted":
                        chat?.Dispose();
                        chat = await RunScriptedDemo(model, hibernateDir);
                        break;
                    case "5": case "state":
                        if (chat == null) { Console.WriteLine("No conversation."); break; }
                        ReportState("Current", (IKVCache)chat);
                        PrintKvPreview((IKVCache)chat);
                        break;
                    case "q": case "quit": case "exit":
                        chat?.Dispose();
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

        static void AskTurn(MultiTurnConversation chat)
        {
            Console.Write("Your prompt: ");
            string? prompt = Console.ReadLine();
            if (string.IsNullOrEmpty(prompt)) { return; }
            DateTime t0 = DateTime.UtcNow;
            var r = chat.Submit(prompt);
            double ms = (DateTime.UtcNow - t0).TotalMilliseconds;
            Console.WriteLine($"Assistant: {r.Completion.Trim()}");
            ReportState($"After turn ({ms:F0} ms)", (IKVCache)chat);
        }

        static async Task Hibernate(MultiTurnConversation chat, string hibernateDir)
        {
            IKVCache cache = (IKVCache)chat;
            ReportState("Before hibernate", cache);
            DateTime t0 = DateTime.UtcNow;
            await cache.HibernateAsync();
            double ms = (DateTime.UtcNow - t0).TotalMilliseconds;
            Console.WriteLine($"Hibernation completed in {ms:F0} ms.");
            ReportState("After hibernate", cache);
            FileInfo[] dumps = new DirectoryInfo(hibernateDir).GetFiles();
            if (dumps.Length > 0)
            {
                Console.WriteLine($"Hibernation file: {dumps.OrderByDescending(f => f.LastWriteTime).First().Name} ({FormatBytes(dumps.Sum(f => f.Length))} total)");
            }
        }

        static async Task<MultiTurnConversation> RunScriptedDemo(LM model, string hibernateDir)
        {
            Console.WriteLine();
            MultiTurnConversation chat = new(model)
            {
                MaximumCompletionTokens = 200,
                SystemPrompt = "You are a concise assistant. Answer in one short paragraph.",
            };
            IKVCache cache = (IKVCache)chat;
            ReportState("After construction", cache);

            Console.WriteLine();
            Console.WriteLine("Turn 1: populate the KV-cache.");
            var r1 = chat.Submit("In one sentence, what is a KV cache in an LLM runtime?");
            Console.WriteLine($"Assistant: {r1.Completion.Trim()}");
            ReportState("After turn 1", cache);

            Console.WriteLine();
            Console.WriteLine("Turn 2: extend the conversation.");
            var r2 = chat.Submit("Why does it grow with each token?");
            Console.WriteLine($"Assistant: {r2.Completion.Trim()}");
            ReportState("After turn 2", cache);

            Console.WriteLine();
            Console.WriteLine("Hibernating to disk...");
            DateTime t0 = DateTime.UtcNow;
            await cache.HibernateAsync();
            Console.WriteLine($"Hibernated in {(DateTime.UtcNow - t0).TotalMilliseconds:F0} ms.");
            ReportState("After hibernate", cache);

            Console.WriteLine();
            Console.WriteLine("Turn 3: state rehydrates transparently on next Submit.");
            var r3 = chat.Submit("And how does that explain why long conversations get slow?");
            Console.WriteLine($"Assistant: {r3.Completion.Trim()}");
            ReportState("After turn 3 (auto-rehydrated)", cache);

            PrintKvPreview(cache);
            return chat;
        }

        static void PrintKvPreview(IKVCache cache)
        {
            string content = cache.KVCacheContent ?? "";
            if (content.Length == 0) { return; }
            Console.WriteLine();
            Console.WriteLine("KV-cache content preview (first 240 chars):");
            Console.WriteLine("  " + (content.Length > 240 ? content.Substring(0, 240) + "..." : content));
        }

        static void ReportState(string label, IKVCache cache)
        {
            long ws = Process.GetCurrentProcess().WorkingSet64;
            Console.WriteLine($"  [{label}] Residency = {cache.Residency,-11}  Working set = {FormatBytes(ws)}");
        }

        static string FormatBytes(long bytes)
        {
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
            Console.WriteLine("║      Idle Session Hibernation                    ║");
            Console.WriteLine("╚══════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine("Hibernate the KV-cache to disk during idle periods; transparent rehydrate on next turn.");
            Console.WriteLine();
        }

        static void PrintMenu()
        {
            Console.WriteLine();
            Console.WriteLine("  1 / start      Create a fresh MultiTurnConversation");
            Console.WriteLine("  2 / ask        Submit a turn (free-form REPL)");
            Console.WriteLine("  3 / hibernate  Hibernate the current context to disk");
            Console.WriteLine("  4 / scripted   Run the scripted 3-turn + hibernate + rehydrate demo");
            Console.WriteLine("  5 / state      Print residency, working-set, and KV preview");
            Console.WriteLine("  q / quit       Exit");
            Console.WriteLine();
        }
    }
}
