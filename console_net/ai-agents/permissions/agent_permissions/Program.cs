using LMKit.Agents;
using LMKit.Agents.Tools;
using LMKit.Agents.Tools.BuiltIn;
using LMKit.Model;
using System.Text;

namespace agent_permissions
{
    internal class Program
    {
        static bool _isDownloading;

        static readonly Dictionary<string, ToolPermissionPolicy> Policies = new()
        {
            ["safeChat"] = new ToolPermissionPolicy()
                .AllowCategory("numeric", "text", "utility", "security")
                .DenyCategory("io", "net")
                .SetMaxRiskLevel(ToolRiskLevel.Low),
            ["devAssistant"] = new ToolPermissionPolicy()
                .AllowCategory("numeric", "text", "utility", "security", "data")
                .Allow("filesystem_*")
                .Allow("http_get", "http_head")
                .Deny("http_post", "http_put", "http_delete", "filesystem_delete")
                .RequireApproval("process_*")
                .SetMaxRiskLevel(ToolRiskLevel.Medium),
            ["readOnlyOps"] = new ToolPermissionPolicy()
                .AllowCategory("numeric", "text", "utility", "security", "data")
                .Allow("filesystem_read", "filesystem_list", "http_get", "http_head")
                .Deny("*write*", "*delete*", "*post*", "*put*", "process_*")
                .SetMaxRiskLevel(ToolRiskLevel.Low),
        };

        static void Main()
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;
            Console.Clear();
            WriteHeader();

            Console.WriteLine("Loading qwen3.5:9b (tool-capable) ...");
            using LM model = LM.LoadFromModelID("qwen3.5:9b",
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
                    case "1": case "describe":
                        DescribeAll();
                        break;
                    case "2": case "compare":
                        ComparePolicies(model);
                        break;
                    case "3": case "chat":
                        ChatUnderPolicy(model);
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

        static void DescribeAll()
        {
            Console.WriteLine();
            foreach ((string name, ToolPermissionPolicy p) in Policies)
            {
                Console.WriteLine($"Policy: {name}");
                Console.WriteLine($"  Default action: {p.DefaultAction}");
                Console.WriteLine($"  Max risk level: {p.MaxRiskLevel?.ToString() ?? "(none)"}");
                Console.WriteLine();
            }
        }

        static void ComparePolicies(LM model)
        {
            Console.WriteLine();
            Console.WriteLine("Default probes will be used; press y/N to use yours instead.");
            Console.Write("Use custom probe set? (y/N): ");
            List<string> probes;
            if ((Console.ReadLine()?.Trim().ToLowerInvariant() == "y"))
            {
                probes = new();
                Console.WriteLine("Paste prompts one per line, blank ends:");
                while (true)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write($"  [{probes.Count + 1}] ");
                    Console.ResetColor();
                    string? line = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(line)) { break; }
                    probes.Add(line.Trim());
                }
                if (probes.Count == 0) { Console.WriteLine("No probes."); return; }
            }
            else
            {
                probes = new()
                {
                    "What is sqrt(2401)?",
                    "Read the contents of C:\\Windows\\System32\\drivers\\etc\\hosts and summarise.",
                    "Fetch https://example.com and tell me its title.",
                };
            }

            foreach ((string name, ToolPermissionPolicy policy) in Policies)
            {
                Console.WriteLine();
                Console.WriteLine($"==== Policy: {name} ====");
                Agent agent = BuildAgent(model, policy);
                foreach (string p in probes)
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"  Prompt: {p}");
                    Console.ResetColor();
                    try
                    {
                        AgentExecutionResult r = agent.Run(p);
                        Console.WriteLine($"  Answer: {r.Content?.Trim()}");
                        Console.WriteLine($"  Tool calls: {r.ToolCalls.Count}");
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"  [error] {ex.Message}");
                        Console.ResetColor();
                    }
                }
            }
            Console.WriteLine();
        }

        static void ChatUnderPolicy(LM model)
        {
            Console.WriteLine();
            Console.WriteLine("Pick a policy:");
            int n = 1;
            string[] names = Policies.Keys.ToArray();
            foreach (string nm in names) { Console.WriteLine($"  {n++}  {nm}"); }
            Console.Write("> ");
            if (!int.TryParse(Console.ReadLine(), out int idx) || idx < 1 || idx > names.Length) { Console.WriteLine("Bad choice."); return; }
            string chosen = names[idx - 1];
            Console.WriteLine($"Loaded policy: {chosen}. Blank prompt returns to menu.");
            Agent agent = BuildAgent(model, Policies[chosen]);

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
                    Console.WriteLine($"  tool calls: {r.ToolCalls.Count}");
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  [error] {ex.Message}");
                    Console.ResetColor();
                }
            }
        }

        static Agent BuildAgent(LM model, ToolPermissionPolicy policy)
        {
            return Agent.CreateBuilder()
                .WithModel(model)
                .WithPermissionPolicy(policy)
                .WithTools(tools =>
                {
                    foreach (ITool t in BuiltInTools.GetAll()) { tools.Register(t); }
                })
                .Build();
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
            Console.WriteLine("║      Agent Tool Permission Policies              ║");
            Console.WriteLine("╚══════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine("Same agent, same prompt, different tool-permission profiles.");
            Console.WriteLine();
        }

        static void PrintMenu()
        {
            Console.WriteLine();
            Console.WriteLine("  1 / describe   Print every policy (default action, max risk level)");
            Console.WriteLine("  2 / compare    Run probes across every policy side-by-side");
            Console.WriteLine("  3 / chat       Pick one policy and chat interactively");
            Console.WriteLine("  q / quit       Exit");
            Console.WriteLine();
        }
    }
}
