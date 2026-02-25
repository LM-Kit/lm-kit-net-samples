using LMKit.Agents.Skills;
using LMKit.Model;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Chat;
using LMKit.TextGeneration.Events;
using LMKit.TextGeneration.Sampling;
using System.Text;

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;

// Step 1: Load skills from the bundled "skills" folder

var registry = new SkillRegistry();

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

// Step 2: Select and load a model

LM model = SelectModel();
Console.Clear();

// Step 3: Choose activation mode

bool useModelDriven = SelectActivationMode();
Console.Clear();

// Step 4: Set up conversation

var chat = new MultiTurnConversation(model);
chat.MaximumCompletionTokens = 4096;
chat.SamplingMode = new RandomSampling { Temperature = 0.7f };

chat.AfterTextCompletion += (_, e) =>
{
    Console.ForegroundColor = e.SegmentType switch
    {
        TextSegmentType.InternalReasoning => ConsoleColor.Blue,
        TextSegmentType.ToolInvocation => ConsoleColor.Magenta,
        _ => ConsoleColor.White
    };
    Console.Write(e.Text);
};

if (useModelDriven)
{
    var skillTool = new SkillTool(registry);
    skillTool.SkillActivated += (_, e) =>
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine("[SkillTool] Model activated skill: {0}", e.Skill.Name);
        Console.ResetColor();
    };
    chat.Tools.Register(skillTool);
}

// Step 5: Show welcome and start chatting

SkillActivator? activator = useModelDriven ? null : new SkillActivator(registry);
AgentSkill? activeSkill = null;

PrintWelcome(registry, useModelDriven);

while (true)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.Write("\nYou: ");
    Console.ResetColor();

    string? input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input))
        break;

    // In manual mode, handle slash commands
    if (!useModelDriven && input.StartsWith("/"))
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

    // Build the prompt
    string prompt = input;

    if (!useModelDriven && activeSkill != null)
    {
        string instructions = activator!.FormatForInjection(
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
        if (!useModelDriven && activeSkill != null)
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


// Helper methods

static LM SelectModel()
{
    bool downloading = false;

    Console.Clear();
    Console.WriteLine("=== Agent Skills Demo ===\n");
    Console.WriteLine("Select a model:\n");
    Console.WriteLine("  0 - Alibaba Qwen 3 8B          (~6 GB VRAM)   [Recommended]");
    Console.WriteLine("  1 - Google Gemma 3 12B          (~9 GB VRAM)");
    Console.WriteLine("  2 - Alibaba Qwen 3 14B          (~10 GB VRAM)");
    Console.WriteLine("  3 - Microsoft Phi-4 14.7B       (~11 GB VRAM)");
    Console.WriteLine("  4 - OpenAI GPT OSS 20B          (~16 GB VRAM)");
    Console.WriteLine("  5 - Z.ai GLM 4.7 Flash 30B      (~18 GB VRAM)");
    Console.WriteLine("  6 - Alibaba Qwen 3.5 27B        (~18 GB VRAM)");
    Console.Write("\n  Or paste a custom model URI or model ID.\n\n> ");

    string? input = Console.ReadLine();

    string? modelId = input?.Trim() switch
    {
        "0" => "qwen3:8b",
        "1" => "gemma3:12b",
        "2" => "qwen3:14b",
        "3" => "phi4",
        "4" => "gptoss:20b",
        "5" => "glm4.7-flash",
        "6" => "qwen3.5:27b",
        _ => null
    };

    if (modelId != null)
    {
        return LM.LoadFromModelID(modelId,
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

    string uri = !string.IsNullOrWhiteSpace(input) ? input.Trim().Trim('"') : "qwen3:8b";

    if (!uri.Contains("://"))
    {
        return LM.LoadFromModelID(uri,
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

static bool SelectActivationMode()
{
    Console.Clear();
    Console.WriteLine("=== Skill Activation Mode ===\n");
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("LM-Kit supports two ways to activate skills:\n");
    Console.ResetColor();

    Console.ForegroundColor = ConsoleColor.White;
    Console.Write("  1 - Manual activation ");
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine("(SkillActivator + slash commands)");
    Console.ResetColor();
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine("      You control which skill is active. Type /explain to activate,");
    Console.WriteLine("      /off to deactivate. The app injects skill instructions into");
    Console.WriteLine("      each message before sending it to the model.");
    Console.ResetColor();

    Console.WriteLine();

    Console.ForegroundColor = ConsoleColor.White;
    Console.Write("  2 - Model-driven activation ");
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine("(SkillTool + function calling)");
    Console.ResetColor();
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine("      A SkillTool is registered as a function the model can call.");
    Console.WriteLine("      The model reads the tool description, discovers available");
    Console.WriteLine("      skills, and activates them autonomously. No slash commands.");
    Console.ResetColor();

    Console.Write("\n> ");
    string? choice = Console.ReadLine();

    return choice?.Trim() == "2";
}

static void PrintWelcome(SkillRegistry registry, bool modelDriven)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("Agent Skills Demo");
    Console.WriteLine("=================\n");
    Console.ResetColor();

    if (modelDriven)
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.Write("Mode: ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("Model-driven activation (SkillTool + function calling)\n");
        Console.ResetColor();

        Console.WriteLine("The model has access to an activate_skill tool and can discover");
        Console.WriteLine("skills on its own. Just describe what you need.\n");

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Available skills the model can activate:");
        Console.ResetColor();

        foreach (var skill in registry.Skills.OrderBy(s => s.Name))
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("  {0,-22}", skill.Name);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(skill.Description);
        }

        Console.ResetColor();
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("Try it:  type \"explain what blockchain is\" or \"write me an email to thank a vendor\"");
        Console.ResetColor();
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.Write("Mode: ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("Manual activation (SkillActivator + slash commands)\n");
        Console.ResetColor();

        Console.WriteLine("Skills are SKILL.md files that turn a generic LLM into a specialist.");
        Console.WriteLine("Activate one with a slash command, then chat normally.\n");

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
