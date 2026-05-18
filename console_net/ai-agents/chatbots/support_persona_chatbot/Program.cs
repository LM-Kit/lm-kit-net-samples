using LMKit.Agents;
using LMKit.Agents.Templates;
using LMKit.Model;
using System.Text;

namespace chatbots
{
    internal class Program
    {
        private static bool _isDownloading;

        static int Main(string[] args)
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            Console.WriteLine("=== Persona-Driven Chatbot Demo ===\n");

            string persona = args.Length > 0 ? string.Join(" ", args) :
                "You are Lumen, a senior customer support agent at a local AI SDK company. " +
                "Tone: warm, terse, factual. Never fabricate features. Always ask a clarifying " +
                "question if the user request is ambiguous.";

            Console.WriteLine("Persona:");
            Console.WriteLine($"  {persona}");
            Console.WriteLine();

            Console.WriteLine("Loading qwen3.5:4b ...");
            using LM model = LM.LoadFromModelID(
                "qwen3.5:4b",
                downloadingProgress: OnDownloadProgress,
                loadingProgress: OnLoadProgress);
            Console.WriteLine();

            Agent agent = AgentTemplates.Chat(model)
                .WithPersonality(persona)
                .Build();

            Console.WriteLine("Type a message (blank line to exit).\n");
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("You: ");
                Console.ResetColor();
                string input = Console.ReadLine() ?? "";
                if (string.IsNullOrWhiteSpace(input)) { break; }

                AgentExecutionResult result = agent.Run(input);
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"Lumen: {result.Content?.Trim()}");
                Console.ResetColor();
                Console.WriteLine();
            }
            return 0;
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
