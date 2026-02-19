using LMKit.Model;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Chat;
using LMKit.TextGeneration.Prompts;
using LMKit.TextGeneration.Sampling;
using System.Text;

namespace prompt_templates
{
    internal class Program
    {
        static bool _isDownloading;

        static void Main(string[] args)
        {
            // Set an optional license key here if available.
            // A free community license can be obtained from: https://lm-kit.com/products/community-edition/
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            Console.WriteLine("=== Prompt Templates with Logic Demo ===\n");

            // ── Part 1: Template Features (no model required) ───────────────
            RunTemplateShowcase();

            // ── Part 2: Live chat powered by a dynamic template ─────────────
            Console.WriteLine("\n\nNow let's use a prompt template to configure a live chat.\n");
            Console.WriteLine("Select a model:\n");
            Console.WriteLine("  0 - Google Gemma 3 4B           (~4 GB VRAM)");
            Console.WriteLine("  1 - Alibaba Qwen 3 8B           (~6 GB VRAM)");
            Console.WriteLine("  2 - Google Gemma 3 12B           (~9 GB VRAM)");
            Console.WriteLine("  3 - Microsoft Phi-4 14.7B        (~11 GB VRAM)");
            Console.WriteLine("  4 - OpenAI GPT OSS 20B           (~16 GB VRAM)");
            Console.WriteLine("  5 - Z.ai GLM 4.7 Flash 30B      (~18 GB VRAM)");
            Console.Write("\n  Or enter a custom model URI\n\n> ");

            string input = Console.ReadLine()?.Trim() ?? "";

            LM model = LoadModel(input);
            Console.Clear();

            RunDynamicChat(model);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Part 1: Showcase template features without a model
        // ─────────────────────────────────────────────────────────────────────
        static void RunTemplateShowcase()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("─── 1. Basic Variable Substitution ───");
            Console.ResetColor();

            var basic = PromptTemplate.Parse("You are {{role}}. Help the user with {{topic}}.");
            string rendered = basic.Render(new PromptTemplateContext
            {
                ["role"] = "a senior C# developer",
                ["topic"] = "async programming"
            });
            PrintTemplate(basic.Source, rendered);

            // ── Filters ─────────────────────────────────────────────────────
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n─── 2. Filters and Defaults ───");
            Console.ResetColor();

            var filters = PromptTemplate.Parse(
                "Welcome, {{name|trim|capitalize}}! Your role: {{role:user}}."
            );
            string filtersRendered = filters.Render(new PromptTemplateContext
            {
                ["name"] = "  alice  "
                // "role" not set: falls back to the inline default "user"
            });
            PrintTemplate(filters.Source, filtersRendered);

            // ── Conditionals ────────────────────────────────────────────────
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n─── 3. Conditionals (if / else) ───");
            Console.ResetColor();

            var conditional = PromptTemplate.Parse(@"{{#if premium}}You are a premium support agent. Provide detailed, in-depth answers.{{#else}}You are a helpful assistant. Keep answers concise.{{/if}}");
            string premiumResult = conditional.Render(new PromptTemplateContext { ["premium"] = true });
            string freeResult = conditional.Render(new PromptTemplateContext { ["premium"] = false });

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  Template : {conditional.Source}");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  premium=true  => {premiumResult}");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  premium=false => {freeResult}");
            Console.ResetColor();

            // ── Loops ───────────────────────────────────────────────────────
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n─── 4. Loops (each) with Objects ───");
            Console.ResetColor();

            var loop = PromptTemplate.Parse(
                "You have access to these tools:\n{{#each tools}}- {{name}}: {{description}}\n{{/each}}"
            );
            string loopRendered = loop.Render(new PromptTemplateContext
            {
                ["tools"] = new[]
                {
                    new { name = "calculator", description = "Perform arithmetic" },
                    new { name = "web_search", description = "Search the web for information" },
                    new { name = "file_read",  description = "Read files from the filesystem" }
                }
            });
            PrintTemplate(loop.Source, loopRendered);

            // ── Scoping (with) ──────────────────────────────────────────────
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n─── 5. Scoping (with) ───");
            Console.ResetColor();

            var scoping = PromptTemplate.Parse(
                "{{#with user}}Agent for {{Name}} ({{Email}}){{/with}}"
            );
            string scopingRendered = scoping.Render(new PromptTemplateContext
            {
                ["user"] = new { Name = "Alice", Email = "alice@example.com" }
            });
            PrintTemplate(scoping.Source, scopingRendered);

            // ── Custom helpers ──────────────────────────────────────────────
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n─── 6. Custom Helpers ───");
            Console.ResetColor();

            var helperTemplate = PromptTemplate.Parse("Today is {{now}}. Hello, {{shout name}}!");
            var ctx = new PromptTemplateContext()
                .Set("name", "world");
            ctx.RegisterHelper("now", _ => DateTime.Now.ToString("yyyy-MM-dd"));
            ctx.RegisterHelper("shout", args =>
                args.Length > 0 ? args[0]?.ToString()?.ToUpper() + "!" : "");

            string helperRendered = helperTemplate.Render(ctx);
            PrintTemplate(helperTemplate.Source, helperRendered);

            // ── Variable introspection ──────────────────────────────────────
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n─── 7. Variable Introspection ───");
            Console.ResetColor();

            var complex = PromptTemplate.Parse(
                "{{#if tools}}Tools: {{#each tools}}{{name}}{{/each}}{{/if}} Language: {{language:English}}"
            );
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("  Variables found: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(string.Join(", ", complex.Variables));
            Console.ResetColor();

            // ── Alternative syntaxes ────────────────────────────────────────
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n─── 8. Alternative Syntaxes ───");
            Console.ResetColor();

            var dollar = PromptTemplate.Parse(
                "Hello ${name|upper}!",
                new PromptTemplateOptions { Syntax = PromptTemplateSyntax.Dollar }
            );
            var percent = PromptTemplate.Parse(
                "Hello %name|upper%!",
                new PromptTemplateOptions { Syntax = PromptTemplateSyntax.Percent }
            );
            var nameCtx = new PromptTemplateContext { ["name"] = "alice" };

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  Dollar  syntax: {dollar.Source}");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"  Result         : {dollar.Render(nameCtx)}");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  Percent syntax: {percent.Source}");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"  Result         : {percent.Render(nameCtx)}");
            Console.ResetColor();

            // ── Real-world agent prompt ─────────────────────────────────────
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n─── 9. Real-World Agent System Prompt ───");
            Console.ResetColor();

            var agentPrompt = PromptTemplate.Parse(@"You are {{persona}}, an expert in {{domain}}.

{{#if constraints}}Constraints:
{{#each constraints}}- {{this}}
{{/each}}{{/if}}
{{#if tools}}Available tools:
{{#each tools}}- {{name}}: {{description}}
{{/each}}{{/if}}
Always respond in {{language:English}}.
{{#unless verbose}}Keep answers concise.{{/unless}}");

            string agentRendered = agentPrompt.Render(new PromptTemplateContext
            {
                ["persona"] = "Aria",
                ["domain"] = "machine learning",
                ["constraints"] = new[] { "Be concise", "Cite sources", "Use examples" },
                ["tools"] = new[]
                {
                    new { name = "web_search", description = "Search the internet" },
                    new { name = "calculator", description = "Math operations" }
                },
                ["verbose"] = false
            });
            PrintTemplate("(see source above)", agentRendered);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Part 2: Live chat where the system prompt is built from a template
        // ─────────────────────────────────────────────────────────────────────
        static void RunDynamicChat(LM model)
        {
            Console.WriteLine("=== Dynamic Template Chat ===\n");
            Console.WriteLine("Configure the assistant via template variables:\n");

            Console.Write("  Domain of expertise (e.g. machine learning): ");
            string domain = Console.ReadLine()?.Trim() ?? "general knowledge";

            Console.Write("  Response language (e.g. French) [English]: ");
            string language = Console.ReadLine()?.Trim() ?? "";
            if (string.IsNullOrEmpty(language)) language = "English";

            Console.Write("  Detailed answers? (yes/no) [no]: ");
            string verboseInput = Console.ReadLine()?.Trim() ?? "";
            bool verbose = verboseInput.Equals("yes", StringComparison.OrdinalIgnoreCase);

            // Build the system prompt from a template
            var systemTemplate = PromptTemplate.Parse(@"You are an expert assistant specializing in {{domain}}.
{{#if verbose}}Provide thorough, detailed explanations with examples and references.{{#else}}Keep answers concise and focused.{{/if}}
Always respond in {{language:English}}.");

            string systemPrompt = systemTemplate.Render(new PromptTemplateContext
            {
                ["domain"] = domain,
                ["verbose"] = verbose,
                ["language"] = language
            });

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"\n  Generated system prompt:\n  {systemPrompt.Replace("\n", "\n  ")}\n");
            Console.ResetColor();

            MultiTurnConversation chat = new(model)
            {
                MaximumCompletionTokens = 2048,
                SamplingMode = new RandomSampling() { Temperature = 0.8f },
                SystemPrompt = systemPrompt
            };

            chat.AfterTextCompletion += OnAfterTextCompletion;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("User: ");
            Console.ResetColor();
            string prompt = Console.ReadLine() ?? "";

            while (!string.IsNullOrWhiteSpace(prompt))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("Assistant: ");
                Console.ResetColor();

                TextGenerationResult result = chat.Submit(prompt, new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token);

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"\n[tokens: {result.GeneratedTokens.Count} | stop: {result.TerminationReason} | speed: {result.TokenGenerationRate:F1} tok/s | ctx: {result.ContextTokens.Count}/{result.ContextSize}]");
                Console.ResetColor();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("\nUser: ");
                Console.ResetColor();
                prompt = Console.ReadLine() ?? "";
            }

            Console.WriteLine("Chat ended. Press any key to exit.");
            Console.ReadKey();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────
        static void PrintTemplate(string source, string rendered)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  Template: {source}");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"  Result  : {rendered}");
            Console.ResetColor();
        }

        static LM LoadModel(string input)
        {
            string? modelId = input switch
            {
                "0" => "gemma3:4b",
                "1" => "qwen3:8b",
                "2" => "gemma3:12b",
                "3" => "phi4",
                "4" => "gptoss:20b",
                "5" => "glm4.7-flash",
                _ => null
            };

            if (modelId != null)
            {
                return LM.LoadFromModelID(modelId,
                    downloadingProgress: OnDownloadProgress,
                    loadingProgress: OnLoadProgress);
            }

            return new LM(new Uri(input.Trim('"')),
                downloadingProgress: OnDownloadProgress,
                loadingProgress: OnLoadProgress);
        }

        static bool OnDownloadProgress(string path, long? contentLength, long bytesRead)
        {
            _isDownloading = true;
            if (contentLength.HasValue)
                Console.Write($"\rDownloading model {Math.Round((double)bytesRead / contentLength.Value * 100, 2):0.00}%");
            else
                Console.Write($"\rDownloading model {bytesRead} bytes");
            return true;
        }

        static bool OnLoadProgress(float progress)
        {
            if (_isDownloading) { Console.Clear(); _isDownloading = false; }
            Console.Write($"\rLoading model {Math.Round(progress * 100)}%");
            return true;
        }

        static void OnAfterTextCompletion(object? sender, LMKit.TextGeneration.Events.AfterTextCompletionEventArgs e)
        {
            Console.ForegroundColor = e.SegmentType switch
            {
                TextSegmentType.InternalReasoning => ConsoleColor.Blue,
                TextSegmentType.ToolInvocation => ConsoleColor.Magenta,
                _ => ConsoleColor.White
            };
            Console.Write(e.Text);
        }
    }
}
