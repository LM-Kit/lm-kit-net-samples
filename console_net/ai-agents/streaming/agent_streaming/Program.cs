using LMKit.Agents;
using LMKit.Agents.Streaming;
using LMKit.Agents.Templates;
using LMKit.Model;
using System.Text;

namespace agent_streaming
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

            Console.WriteLine("Loading qwen3.5:4b ...");
            using LM model = LM.LoadFromModelID("qwen3.5:4b",
                downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);
            Console.WriteLine();
            Console.WriteLine();

            Agent agent = AgentTemplates.Chat(model)
                .WithPersonality("You are a concise assistant. Reply in one paragraph.")
                .Build();

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
                    case "1": case "stream":
                        await StreamIAsync(agent);
                        break;
                    case "2": case "callback":
                        await StreamCallbackAsync(agent);
                        break;
                    case "3": case "save":
                        await StreamToFileAsync(agent);
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

        static async Task StreamIAsync(Agent agent)
        {
            Console.WriteLine();
            Console.Write("Prompt: ");
            string? prompt = Console.ReadLine();
            if (string.IsNullOrEmpty(prompt)) { return; }

            using StreamingAgentExecutor exec = new()
            {
                BufferSize = 1,
                StreamThinking = false,
                StreamToolCalls = true,
            };
            Console.WriteLine();
            await foreach (AgentStreamToken token in exec.StreamAsync(agent, prompt))
            {
                Console.ForegroundColor = token.Type switch
                {
                    AgentStreamTokenType.Thinking => ConsoleColor.Blue,
                    AgentStreamTokenType.ToolCall => ConsoleColor.Magenta,
                    AgentStreamTokenType.ToolResult => ConsoleColor.DarkMagenta,
                    _ => ConsoleColor.White,
                };
                Console.Write(token.Text);
                Console.ResetColor();
            }
            Console.WriteLine();
            Console.WriteLine();
        }

        static async Task StreamCallbackAsync(Agent agent)
        {
            Console.WriteLine();
            Console.Write("Prompt: ");
            string? prompt = Console.ReadLine();
            if (string.IsNullOrEmpty(prompt)) { return; }

            using StreamingAgentExecutor exec = new()
            {
                BufferSize = 1,
                StreamThinking = false,
                StreamToolCalls = true,
            };
            Console.WriteLine();
            AgentExecutionResult r = await exec.ExecuteStreamingAsync(agent, prompt, DelegateStreamHandler.Console());
            Console.WriteLine();
            Console.WriteLine($"  status: {r.Status}  duration: {r.Duration.TotalMilliseconds:F0} ms");
            Console.WriteLine();
        }

        static async Task StreamToFileAsync(Agent agent)
        {
            Console.WriteLine();
            Console.Write("Prompt: ");
            string? prompt = Console.ReadLine();
            if (string.IsNullOrEmpty(prompt)) { return; }
            Console.Write("Output text file path: ");
            string? path = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(path)) { Console.WriteLine("Output path required."); return; }

            using StreamingAgentExecutor exec = new()
            {
                BufferSize = 1,
                StreamThinking = false,
                StreamToolCalls = false,
            };
            using StreamWriter writer = new(path, false, new UTF8Encoding(true));
            Console.WriteLine();
            await foreach (AgentStreamToken token in exec.StreamAsync(agent, prompt))
            {
                Console.Write(token.Text);
                writer.Write(token.Text);
            }
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine($"Saved to {path}.");
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
            Console.WriteLine("║      Streaming Agent Responses                   ║");
            Console.WriteLine("╚══════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine("Stream agent output token-by-token: IAsyncEnumerable, callback, or to a file.");
            Console.WriteLine();
        }

        static void PrintMenu()
        {
            Console.WriteLine();
            Console.WriteLine("  1 / stream     IAsyncEnumerable<AgentStreamToken> (typed tokens, colored types)");
            Console.WriteLine("  2 / callback   DelegateStreamHandler.Console() handler");
            Console.WriteLine("  3 / save       Stream to console AND a UTF-8 text file");
            Console.WriteLine("  q / quit       Exit");
            Console.WriteLine();
        }
    }
}
