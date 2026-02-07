using LMKit.Agents.Skills;
using LMKit.Model;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Chat;
using LMKit.TextGeneration.Sampling;
using System.Text;

// ──────────────────────────────────────────────────────────────────
// Agent Skills Demo
//
// Shows how SKILL.md files transform a generic LLM into a specialist.
// Three bundled skills turn the same model into:
//   /explain      → explains any topic in plain language
//   /pros-cons    → gives balanced pros and cons for any decision
//   /email-writer → writes a professional email from a one-line description
//
// Activate a skill, then chat normally. The model follows the skill's
// instructions until you deactivate with /off.
// ──────────────────────────────────────────────────────────────────

// Uncomment and set your license key for production use:
// LMKit.Licensing.LicenseManager.SetLicenseKey("");

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;

// ── Step 1: Load skills from the bundled "skills" folder ──────────

var registry = new SkillRegistry();
var activator = new SkillActivator(registry);

string skillsPath = Path.Combine(AppContext.BaseDirectory, "skills");

if (Directory.Exists(skillsPath))
{
    registry.LoadFromDirectory(skillsPath, errorHandler: (path, ex) =>
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("  Warning: could not load {0}: {1}", Path.GetFileName(path), ex.Message);
        Console.ResetColor();
    });
}

// ── Step 2: Select and load a model ───────────────────────────────

LM model = SelectModel();
Console.Clear();

// ── Step 3: Set up conversation ───────────────────────────────────

var chat = new MultiTurnConversation(model);
chat.MaximumCompletionTokens = 4096;
chat.SamplingMode = new RandomSampling { Temperature = 0.7f };
chat.AfterTextCompletion += (_, e) =>
{
    Console.ForegroundColor = e.SegmentType == TextSegmentType.InternalReasoning
        ? ConsoleColor.Blue
        : ConsoleColor.White;
    Console.Write(e.Text);
};

AgentSkill? activeSkill = null;

// ── Step 4: Show welcome and start chatting ───────────────────────

PrintWelcome(registry);

while (true)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.Write("\nYou: ");
    Console.ResetColor();

    string? input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input))
        break;

    // Handle commands
    if (input.StartsWith("/"))
    {
        string cmd = input.Trim().ToLowerInvariant();

        if (cmd == "/help")
        {
            PrintHelp(registry);
            continue;
        }

        if (cmd == "/skills")
        {
            PrintSkills(registry, activeSkill);
            continue;
        }

        if (cmd == "/off")
        {
            if (activeSkill != null)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Skill deactivated: {0}", activeSkill.Name);
                Console.ResetColor();
                activeSkill = null;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("No skill is active.");
                Console.ResetColor();
            }
            continue;
        }

        // Try to activate a skill by name (e.g. /email-writer)
        if (registry.TryParseSlashCommand(input, out var skill, out _))
        {
            activeSkill = skill;
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("Skill activated: {0}", skill.Name);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("{0}", skill.Description);
            Console.WriteLine("Type your request. Use /off to deactivate.");
            Console.ResetColor();
            continue;
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Unknown command. Type /help for available commands.");
        Console.ResetColor();
        continue;
    }

    // Build the prompt: inject skill instructions if one is active
    string prompt = input;
    if (activeSkill != null)
    {
        string instructions = activator.FormatForInjection(
            activeSkill,
            SkillInjectionMode.UserMessage);
        prompt = instructions + "\n\n---\n\nUser request: " + input;
    }

    // Generate response
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.Write("\nAssistant: ");
    Console.ResetColor();

    try
    {
        var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var result = chat.Submit(prompt, cts.Token);

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("\n({0:0.0} tok/s", result.TokenGenerationRate);
        if (activeSkill != null)
            Console.Write(" | skill: {0}", activeSkill.Name);
        Console.WriteLine(")");
        Console.ResetColor();
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("\nError: {0}", ex.Message);
        Console.ResetColor();
    }
}


// ── Helper methods ────────────────────────────────────────────────

static LM SelectModel()
{
    Console.Clear();
    Console.WriteLine("=== Agent Skills Demo ===\n");
    Console.WriteLine("Select a model:\n");
    Console.WriteLine("  0 - Google Gemma 3 4B         (~4 GB VRAM)");
    Console.WriteLine("  1 - Alibaba Qwen 3 8B         (~6 GB VRAM)");
    Console.WriteLine("  2 - Google Gemma 3 12B         (~9 GB VRAM)   [Recommended]");
    Console.WriteLine("  3 - Microsoft Phi-4 14.7B      (~11 GB VRAM)");
    Console.WriteLine("  4 - OpenAI GPT OSS 20B         (~16 GB VRAM)");
    Console.Write("\n  Or paste a custom model URI.\n\n> ");

    bool downloading = false;
    string? input = Console.ReadLine();

    string uri = input?.Trim() switch
    {
        "0" => "https://huggingface.co/lm-kit/gemma-3-4b-instruct-lmk/resolve/main/gemma-3-4b-it-Q4_K_M.lmk",
        "1" => "https://huggingface.co/lm-kit/qwen-3-8b-instruct-gguf/resolve/main/Qwen3-8B-Q4_K_M.gguf",
        "2" => "https://huggingface.co/lm-kit/gemma-3-12b-instruct-lmk/resolve/main/gemma-3-12b-it-Q4_K_M.lmk",
        "3" => "https://huggingface.co/lm-kit/phi-4-14.7b-instruct-gguf/resolve/main/Phi-4-14.7B-Instruct-Q4_K_M.gguf",
        "4" => "https://huggingface.co/lm-kit/gpt-oss-20b-gguf/resolve/main/gpt-oss-20b-mxfp4.gguf",
        _ => !string.IsNullOrWhiteSpace(input) ? input.Trim().Trim('"')
            : "https://huggingface.co/lm-kit/gemma-3-12b-instruct-lmk/resolve/main/gemma-3-12b-it-Q4_K_M.lmk"
    };

    return new LM(
        new Uri(uri),
        downloadingProgress: (_, len, read) =>
        {
            downloading = true;
            if (len.HasValue) Console.Write("\rDownloading model {0:0.00}%", (double)read / len.Value * 100);
            return true;
        },
        loadingProgress: p =>
        {
            if (downloading) { Console.Clear(); downloading = false; }
            Console.Write("\rLoading model {0}%", Math.Round(p * 100));
            return true;
        });
}

static void PrintWelcome(SkillRegistry registry)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("Agent Skills Demo");
    Console.WriteLine("=================\n");
    Console.ResetColor();

    Console.WriteLine("Skills are SKILL.md files that turn a generic LLM into a specialist.");
    Console.WriteLine("Activate one, then chat normally.\n");

    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("Available skills:");
    Console.ResetColor();

    foreach (var skill in registry.Skills.OrderBy(s => s.Name))
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("  /{0,-22}", skill.Name);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(skill.Description);
    }

    Console.ResetColor();
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine("Try it:  type /explain  then type \"blockchain\"");
    Console.ResetColor();
}

static void PrintSkills(SkillRegistry registry, AgentSkill? active)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("\nAvailable skills:\n");
    Console.ResetColor();

    foreach (var skill in registry.Skills.OrderBy(s => s.Name))
    {
        bool isActive = active != null && active.Name == skill.Name;
        Console.ForegroundColor = isActive ? ConsoleColor.Green : ConsoleColor.White;
        Console.Write("  /{0,-22}", skill.Name);
        if (isActive)
            Console.Write("[active] ");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(skill.Description);
    }

    Console.ResetColor();
}

static void PrintHelp(SkillRegistry registry)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("\nCommands:\n");
    Console.ResetColor();

    Console.WriteLine("  /<skill-name>   Activate a skill (e.g. /email-writer)");
    Console.WriteLine("  /off            Deactivate the current skill");
    Console.WriteLine("  /skills         List all available skills");
    Console.WriteLine("  /help           Show this message");
    Console.WriteLine();
    Console.WriteLine("Just type normally to chat. When a skill is active,");
    Console.WriteLine("the model follows its instructions automatically.");
}
