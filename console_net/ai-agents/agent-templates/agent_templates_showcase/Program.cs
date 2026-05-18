using LMKit.Agents;
using LMKit.Agents.Templates;
using LMKit.Model;
using System.Text;

namespace agent_templates_showcase
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

            Console.WriteLine("Loading qwen3.5:4b ...");
            using LM model = LM.LoadFromModelID("qwen3.5:4b",
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
                        CompareAll(model);
                        break;
                    case "2": case "chat":
                        ChatWith(model, "Chat", _ => AgentTemplates.Chat(model)
                            .WithPersonality("You are a concise assistant. Reply in two short sentences.").Build());
                        break;
                    case "3": case "writer":
                        ChatWith(model, "Writer", _ => AgentTemplates.Writer(model).Build());
                        break;
                    case "4": case "analyst":
                        ChatWith(model, "Analyst", _ => AgentTemplates.Analyst(model).Build());
                        break;
                    case "5": case "react":
                        ChatWith(model, "ReAct", _ => AgentTemplates.ReAct(model).Build());
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

        static void CompareAll(LM model)
        {
            Console.WriteLine();
            Console.Write("Prompt (default: bookstore loyalty programme): ");
            string prompt = Console.ReadLine()?.Trim() ?? "";
            if (string.IsNullOrEmpty(prompt))
            {
                prompt = "A bookstore wants to launch a loyalty programme. In three short bullet points, propose how it should be designed.";
            }

            (string Label, Agent Agent)[] agents =
            {
                ("Chat",    AgentTemplates.Chat(model).WithPersonality("You are a concise assistant. Reply in two short sentences.").Build()),
                ("Writer",  AgentTemplates.Writer(model).Build()),
                ("Analyst", AgentTemplates.Analyst(model).Build()),
                ("ReAct",   AgentTemplates.ReAct(model).Build()),
            };

            foreach ((string label, Agent agent) in agents)
            {
                Console.WriteLine();
                Console.WriteLine($"---- {label} ----");
                try
                {
                    AgentExecutionResult r = agent.Run(prompt);
                    Console.WriteLine(r.Content?.Trim());
                    Console.WriteLine();
                    Console.WriteLine($"  status: {r.Status}  inference calls: {r.InferenceCount}  duration: {r.Duration.TotalMilliseconds:F0} ms");
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  [error] {ex.Message}");
                    Console.ResetColor();
                }
            }
            Console.WriteLine();
        }

        static void ChatWith(LM model, string label, Func<LM, Agent> factory)
        {
            Console.WriteLine();
            Console.WriteLine($"Loading {label} template. Blank prompt returns to menu.");
            Agent agent = factory(model);
            while (true)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("Prompt: ");
                Console.ResetColor();
                string? prompt = Console.ReadLine();
                if (string.IsNullOrEmpty(prompt)) { return; }

                try
                {
                    AgentExecutionResult r = agent.Run(prompt);
                    Console.WriteLine();
                    Console.WriteLine(r.Content?.Trim());
                    Console.WriteLine();
                    Console.WriteLine($"  status: {r.Status}  inference: {r.InferenceCount}  duration: {r.Duration.TotalMilliseconds:F0} ms");
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  [error] {ex.Message}");
                    Console.ResetColor();
                }
            }
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
            Console.WriteLine("║      Agent Templates Showcase                    ║");
            Console.WriteLine("╚══════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine("Run the same prompt through Chat / Writer / Analyst / ReAct templates.");
            Console.WriteLine();
        }

        static void PrintMenu()
        {
            Console.WriteLine();
            Console.WriteLine("  1 / compare   Run one prompt across all 4 templates");
            Console.WriteLine("  2 / chat      Interactive REPL with the Chat template");
            Console.WriteLine("  3 / writer    Interactive REPL with the Writer template");
            Console.WriteLine("  4 / analyst   Interactive REPL with the Analyst template");
            Console.WriteLine("  5 / react     Interactive REPL with the ReAct template");
            Console.WriteLine("  q / quit      Exit");
            Console.WriteLine();
        }
    }
}
