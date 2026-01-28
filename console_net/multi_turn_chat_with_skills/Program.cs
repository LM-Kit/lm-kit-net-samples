using LMKit.Agents.Skills;
using LMKit.Model;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Chat;
using LMKit.TextGeneration.Sampling;
using System.Text;

namespace multi_turn_chat_with_skills
{
    /// <summary>
    /// Demonstrates the Agent Skills feature of LM-Kit.NET.
    /// 
    /// Agent Skills are reusable, shareable instruction sets that enhance AI behavior
    /// for specific tasks. They follow the agentskills.io specification and can be:
    /// - Loaded from SKILL.md files
    /// - Created programmatically with SkillBuilder
    /// - Activated via slash commands (/skill-name)
    /// - Auto-selected by the LLM via SkillTool
    /// 
    /// This demo showcases:
    /// 1. Loading skills from a directory
    /// 2. Manual skill activation via slash commands
    /// 3. Automatic skill selection via SkillTool
    /// 4. Programmatic skill creation with SkillBuilder
    /// 5. Skill discovery and management
    /// </summary>
    internal class Program
    {
        // Model URIs
        static readonly string DEFAULT_LLAMA3_1_8B_MODEL_PATH = @"https://huggingface.co/lm-kit/llama-3.1-8b-instruct-gguf/resolve/main/Llama-3.1-8B-Instruct-Q4_K_M.gguf";
        static readonly string DEFAULT_GEMMA3_4B_MODEL_PATH = @"https://huggingface.co/lm-kit/gemma-3-4b-instruct-lmk/resolve/main/gemma-3-4b-it-Q4_K_M.lmk";
        static readonly string DEFAULT_PHI4_MINI_3_8B_MODEL_PATH = @"https://huggingface.co/lm-kit/phi-4-mini-3.8b-instruct-gguf/resolve/main/Phi-4-mini-Instruct-Q4_K_M.gguf";
        static readonly string DEFAULT_QWEN3_8B_MODEL_PATH = @"https://huggingface.co/lm-kit/qwen-3-8b-instruct-gguf/resolve/main/Qwen3-8B-Q4_K_M.gguf";
        static readonly string DEFAULT_MINISTRAL_3_8_MODEL_PATH = @"https://huggingface.co/lm-kit/ministral-3-3b-instruct-lmk/resolve/main/ministral-3-3b-instruct-Q4_K_M.lmk";
        static readonly string DEFAULT_PHI4_14_7B_MODEL_PATH = @"https://huggingface.co/lm-kit/phi-4-14.7b-instruct-gguf/resolve/main/Phi-4-14.7B-Instruct-Q4_K_M.gguf";

        static bool _isDownloading;
        static SkillRegistry _skillRegistry;
        static SkillActivator _skillActivator;
        static AgentSkill _activeSkill;
        static SkillTool _skillTool;
        static SkillResourceTool _skillResourceTool;
        static bool _autoSkillMode = false;
        static bool _resourceToolEnabled = false;

        private static bool ModelDownloadingProgress(string path, long? contentLength, long bytesRead)
        {
            _isDownloading = true;
            if (contentLength.HasValue)
            {
                double progressPercentage = Math.Round((double)bytesRead / contentLength.Value * 100, 2);
                Console.Write("\rDownloading model {0:0.00}%", progressPercentage);
            }
            else
            {
                Console.Write("\rDownloading model {0} bytes", bytesRead);
            }
            return true;
        }

        private static bool ModelLoadingProgress(float progress)
        {
            if (_isDownloading)
            {
                Console.Clear();
                _isDownloading = false;
            }
            Console.Write("\rLoading model {0}%", Math.Round(progress * 100));
            return true;
        }

        private static void Main(string[] args)
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = UTF8Encoding.UTF8;

            // Initialize skill registry and activator
            _skillRegistry = new SkillRegistry();
            _skillActivator = new SkillActivator(_skillRegistry);

            // Load bundled skills
            LoadBundledSkills();

            // Create programmatic skills
            CreateProgrammaticSkills();

            // Select and load model
            LM model = SelectModel();
            Console.Clear();

            // Create conversation
            MultiTurnConversation chat = new(model);
            chat.MaximumCompletionTokens = 4096;
            chat.SamplingMode = new RandomSampling { Temperature = 0.7f };
            chat.AfterTextCompletion += Chat_AfterTextCompletion;

            // Setup SkillTool for automatic skill selection
            SetupSkillTool(chat);

            // Show welcome and help
            ShowWelcome();

            // Main chat loop
            string prompt = "";
            string mode = "chat";

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("\nUser: ");
            Console.ResetColor();
            prompt = Console.ReadLine();

            while (!string.IsNullOrWhiteSpace(prompt))
            {
                // Handle special commands
                if (prompt.StartsWith("/"))
                {
                    if (HandleCommand(prompt, chat, ref mode))
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write("\nUser: ");
                        Console.ResetColor();
                        prompt = Console.ReadLine();
                        continue;
                    }
                }

                // Generate response
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("\nAssistant: ");
                Console.ResetColor();

                TextGenerationResult result;
                CancellationTokenSource cts = new(TimeSpan.FromMinutes(5));

                try
                {
                    // Prepare the prompt with active skill context if any
                    string finalPrompt = PreparePromptWithSkill(prompt);

                    if (mode == "regenerate")
                    {
                        result = chat.RegenerateResponse(cts.Token);
                        mode = "chat";
                    }
                    else if (mode == "continue")
                    {
                        result = chat.ContinueLastAssistantResponse(cts.Token);
                        mode = "chat";
                    }
                    else
                    {
                        result = chat.Submit(finalPrompt, cts.Token);
                    }

                    // Show generation stats
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write("\n\n(tokens: {0} | {1} | {2:0.0} tok/s | ctx: {3}/{4}",
                        result.GeneratedTokens.Count,
                        result.TerminationReason,
                        result.TokenGenerationRate,
                        result.ContextTokens.Count,
                        result.ContextSize);

                    if (_activeSkill != null)
                    {
                        Console.Write(" | skill: {0}", _activeSkill.Name);
                    }
                    Console.WriteLine(")");
                    Console.ResetColor();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\nError: {0}", ex.Message);
                    Console.ResetColor();
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("\nUser: ");
                Console.ResetColor();
                prompt = Console.ReadLine();
            }

            Console.WriteLine("\nChat ended. Press any key to exit.");
            Console.ReadKey();
        }

        /// <summary>
        /// Loads skills from the bundled skills directory.
        /// </summary>
        private static void LoadBundledSkills()
        {
            string skillsPath = Path.Combine(AppContext.BaseDirectory, "skills");

            if (!Directory.Exists(skillsPath))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Skills directory not found at: {0}", skillsPath);
                Console.WriteLine("Creating default skills directory...\n");
                Console.ResetColor();

                Directory.CreateDirectory(skillsPath);
                return;
            }

            Console.WriteLine("Loading skills from: {0}\n", skillsPath);

            int loadedCount = _skillRegistry.LoadFromDirectory(
                skillsPath,
                errorHandler: (path, ex) =>
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("  Warning: Failed to load skill at {0}: {1}", path, ex.Message);
                    Console.ResetColor();
                });

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Loaded {0} skills from directory.\n", loadedCount);
            Console.ResetColor();

            // List loaded skills
            foreach (var skill in _skillRegistry.Skills)
            {
                Console.WriteLine("  - /{0}: {1}", skill.Name, TruncateText(skill.Description, 60));
            }

            if (loadedCount > 0)
            {
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Creates skills programmatically using SkillBuilder.
        /// This demonstrates how to create skills without SKILL.md files.
        /// </summary>
        private static void CreateProgrammaticSkills()
        {
            Console.WriteLine("Creating programmatic skills...\n");

            // Create a simple brainstorming skill
            var brainstormSkill = new SkillBuilder()
                .WithName("brainstorm")
                .WithDescription("Creative brainstorming assistant that generates diverse ideas using lateral thinking techniques.")
                .WithVersion("1.0.0")
                .WithAuthor("LM-Kit Demo")
                .WithTags("creativity", "ideas", "brainstorming")
                .WithInstructions(@"# Brainstorming Assistant

You are a creative brainstorming facilitator. When asked to brainstorm:

## Techniques to Use
1. **Mind Mapping**: Branch ideas from central concepts
2. **SCAMPER**: Substitute, Combine, Adapt, Modify, Put to other uses, Eliminate, Reverse
3. **Six Thinking Hats**: Facts, Emotions, Caution, Benefits, Creativity, Process
4. **Random Word Association**: Connect unrelated concepts

## Output Format
- Generate at least 10 diverse ideas
- Group ideas by theme or approach
- Mark the most promising ideas with a star (*)
- Include at least 2 'wild card' unconventional ideas

## Guidelines
- No idea is too crazy in brainstorming
- Build on previous ideas
- Quantity over quality initially
- Defer judgment")
                .Build();

            _skillRegistry.Register(brainstormSkill);
            Console.WriteLine("  - /brainstorm: Creative brainstorming assistant");

            // Create a debug/explain skill
            var explainSkill = new SkillBuilder()
                .WithName("explain")
                .WithDescription("Explains complex topics in simple terms using analogies and examples, adapting to the audience level.")
                .WithVersion("1.0.0")
                .WithInstructions(@"# Explanation Expert

You are an expert at explaining complex topics simply.

## Approach
1. First, identify the core concept
2. Find a relatable analogy from everyday life
3. Build understanding step by step
4. Use concrete examples
5. Check for understanding

## Audience Adaptation
- **Beginner**: Use everyday analogies, avoid jargon
- **Intermediate**: Include some technical terms with definitions
- **Expert**: Focus on nuances and edge cases

## Format
Start with a one-sentence summary, then expand with:
- The 'what': Definition
- The 'why': Purpose/importance  
- The 'how': Mechanism/process
- Example: Real-world application

Always ask: 'Would you like me to go deeper on any part?'")
                .Build();

            _skillRegistry.Register(explainSkill);
            Console.WriteLine("  - /explain: Explains complex topics simply\n");
        }

        /// <summary>
        /// Sets up the SkillTool for automatic skill selection by the LLM.
        /// </summary>
        private static void SetupSkillTool(MultiTurnConversation chat)
        {
            _skillTool = new SkillTool(_skillRegistry);

            // Handle skill activation events
            _skillTool.SkillActivated += (sender, e) =>
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("\n[Skill] Activated: {0}", e.Skill.Name);
                if (!string.IsNullOrEmpty(e.Context))
                {
                    Console.WriteLine("[Skill] Context: {0}", TruncateText(e.Context, 80));
                }
                Console.ResetColor();

                _activeSkill = e.Skill;
                _skillResourceTool.ActiveSkill = e.Skill;
            };

            // Setup SkillResourceTool for on-demand resource loading
            _skillResourceTool = new SkillResourceTool(_skillRegistry);
            _skillResourceTool.ResourceLoaded += (sender, e) =>
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine("\n[Resource] Loaded: {0}/{1} ({2} chars)", 
                    e.Skill.Name, e.Resource.RelativePath, e.Content.Length);
                Console.ResetColor();
            };

            // Register the tool (disabled by default, enable with /auto command)
            // chat.Tools.Register(_skillTool);
        }

        /// <summary>
        /// Handles special slash commands.
        /// </summary>
        private static bool HandleCommand(string input, MultiTurnConversation chat, ref string mode)
        {
            string command = input.ToLower().Trim();

            // Check for skill activation via slash command
            if (_skillRegistry.TryParseSlashCommand(input, out var skill, out var arguments))
            {
                ActivateSkill(skill, arguments);
                return true;
            }

            switch (command)
            {
                case "/help":
                    ShowCommands();
                    return true;

                case "/skills":
                    ListSkills();
                    return true;

                case "/deactivate":
                case "/off":
                    DeactivateSkill();
                    return true;

                case "/active":
                    ShowActiveSkill();
                    return true;

                case "/auto":
                    ToggleAutoSkillMode(chat);
                    return true;

                case "/tools":
                    ToggleResourceTool(chat);
                    return true;

                case "/reset":
                    chat.ClearHistory();
                    DeactivateSkill();
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Chat history cleared and skill deactivated.");
                    Console.ResetColor();
                    return true;

                case "/regenerate":
                    mode = "regenerate";
                    return false;

                case "/continue":
                    mode = "continue";
                    return false;

                case "/create":
                    CreateSkillInteractively();
                    return true;

                case "/search":
                    SearchSkills();
                    return true;

                case "/discovery":
                    ShowDiscoveryContext();
                    return true;

                case "/load":
                    LoadSkillFromPath();
                    return true;

                case "/remote":
                    LoadSkillFromUrl();
                    return true;

                case "/cache":
                    ShowCacheInfo();
                    return true;

                default:
                    // Check for /info <skill-name> command
                    if (command.StartsWith("/info "))
                    {
                        string skillName = input.Substring(6).Trim();
                        ShowSkillInfo(skillName);
                        return true;
                    }

                    // Check for /resources <skill-name> command
                    if (command.StartsWith("/resources "))
                    {
                        string skillName = input.Substring(11).Trim();
                        ShowSkillResources(skillName);
                        return true;
                    }

                    // Check for /github owner/repo/path command
                    if (command.StartsWith("/github "))
                    {
                        string githubPath = input.Substring(8).Trim();
                        LoadSkillFromGitHub(githubPath);
                        return true;
                    }

                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Unknown command: {0}. Type /help for available commands.", command);
                    Console.ResetColor();
                    return true;
            }
        }

        /// <summary>
        /// Activates a skill and injects its instructions into the conversation.
        /// </summary>
        private static void ActivateSkill(AgentSkill skill, string context)
        {
            _activeSkill = skill;
            _skillResourceTool.ActiveSkill = skill;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\nSkill Activated: {0}", skill.Name);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("Description: {0}", skill.Description);

            if (skill.Version != null)
            {
                Console.WriteLine("Version: {0}", skill.Version);
            }

            // Show resources if available
            var resources = skill.Resources.ToList();
            if (resources.Count > 0)
            {
                Console.WriteLine("Resources: {0} file(s) available", resources.Count);
                foreach (var resource in resources.Take(3))
                {
                    Console.WriteLine("  - {0}", resource.RelativePath);
                }
                if (resources.Count > 3)
                {
                    Console.WriteLine("  ... and {0} more (use /resources {1} to see all)", 
                        resources.Count - 3, skill.Name);
                }
            }

            if (!string.IsNullOrEmpty(context))
            {
                Console.WriteLine("Context: {0}", context);
            }

            Console.WriteLine("\nThe assistant will now follow the skill's instructions.");
            Console.WriteLine("Use /deactivate to return to normal mode.");
            Console.ResetColor();
        }

        /// <summary>
        /// Deactivates the current skill.
        /// </summary>
        private static void DeactivateSkill()
        {
            if (_activeSkill != null)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Skill deactivated: {0}", _activeSkill.Name);
                Console.ResetColor();
                _activeSkill = null;
                _skillResourceTool.ActiveSkill = null;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("No skill is currently active.");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// Shows the currently active skill.
        /// </summary>
        private static void ShowActiveSkill()
        {
            if (_activeSkill != null)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("\nActive Skill: {0}", _activeSkill.Name);
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("Description: {0}", _activeSkill.Description);

                if (_activeSkill.Version != null)
                {
                    Console.WriteLine("Version: {0}", _activeSkill.Version);
                }

                if (_activeSkill.IsMode)
                {
                    Console.WriteLine("Type: Mode (persistent behavior modification)");
                }

                var tools = _activeSkill.GetAllowedTools();
                if (tools.Count > 0)
                {
                    Console.WriteLine("Allowed tools: {0}", string.Join(", ", tools));
                }

                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("No skill is currently active.");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// Toggles automatic skill selection mode.
        /// </summary>
        private static void ToggleAutoSkillMode(MultiTurnConversation chat)
        {
            _autoSkillMode = !_autoSkillMode;

            if (_autoSkillMode)
            {
                chat.Tools.Register(_skillTool);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Auto-skill mode ENABLED. The LLM can now automatically select skills.");
                Console.ResetColor();
            }
            else
            {
                chat.Tools.Remove(_skillTool.Name);
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Auto-skill mode DISABLED. Use /skill-name to manually activate skills.");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// Toggles the SkillResourceTool for on-demand resource loading.
        /// </summary>
        private static void ToggleResourceTool(MultiTurnConversation chat)
        {
            _resourceToolEnabled = !_resourceToolEnabled;

            if (_resourceToolEnabled)
            {
                chat.Tools.Register(_skillResourceTool);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Resource tool ENABLED. The LLM can now load skill resources on demand.");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("  Tool: load_skill_resource");
                Console.WriteLine("  Operations: 'load' (get content), 'list' (show available)");
                Console.ResetColor();
            }
            else
            {
                chat.Tools.Remove(_skillResourceTool.Name);
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Resource tool DISABLED.");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// Lists all available skills.
        /// </summary>
        private static void ListSkills()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n=== Available Skills ({0}) ===\n", _skillRegistry.Count);
            Console.ResetColor();

            foreach (var skill in _skillRegistry.Skills.OrderBy(s => s.Name))
            {
                bool isActive = _activeSkill != null && _activeSkill.Name == skill.Name;
                string marker = isActive ? " [ACTIVE]" : "";

                // Count resources
                var resourceCount = skill.Resources.Count();
                string resourceInfo = resourceCount > 0 ? $" [{resourceCount} resources]" : "";

                Console.ForegroundColor = isActive ? ConsoleColor.Green : ConsoleColor.White;
                Console.Write("/{0}{1}", skill.Name, marker);
                if (resourceCount > 0)
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.Write(resourceInfo);
                }
                Console.WriteLine();

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("  {0}", TruncateText(skill.Description, 70));
                Console.WriteLine();
            }

            Console.ResetColor();
            Console.WriteLine("Type /<skill-name> to activate, /info <skill-name> for details.");
        }

        /// <summary>
        /// Searches for skills matching a query.
        /// </summary>
        private static void SearchSkills()
        {
            Console.Write("Enter search query: ");
            string query = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(query))
            {
                return;
            }

            var matches = _skillRegistry.FindMatches(query, maxResults: 5, minScore: 0.1f);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n=== Search Results for '{0}' ===\n", query);
            Console.ResetColor();

            if (matches.Count == 0)
            {
                Console.WriteLine("No matching skills found.");
                return;
            }

            foreach (var match in matches)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("/{0} (score: {1:P0})", match.Skill.Name, match.Score);
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("  {0}\n", TruncateText(match.Skill.Description, 70));
            }

            Console.ResetColor();
        }

        /// <summary>
        /// Shows the discovery context that can be provided to the LLM.
        /// </summary>
        private static void ShowDiscoveryContext()
        {
            string context = _skillRegistry.GetDiscoveryContext();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n=== Skill Discovery Context ===\n");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(context);
            Console.ResetColor();
            Console.WriteLine("\nThis context can be injected into a system prompt to help the LLM discover and activate skills.");
        }

        /// <summary>
        /// Shows detailed information about a skill.
        /// </summary>
        private static void ShowSkillInfo(string skillName)
        {
            if (!_skillRegistry.TryGet(skillName, out var skill))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Skill not found: {0}", skillName);
                Console.ResetColor();
                return;
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n=== Skill: {0} ===\n", skill.Name);
            Console.ResetColor();

            Console.WriteLine("Description: {0}", skill.Description);

            if (skill.Version != null)
            {
                Console.WriteLine("Version:     {0}", skill.Version);
            }

            if (!string.IsNullOrEmpty(skill.License))
            {
                Console.WriteLine("License:     {0}", skill.License);
            }

            if (skill.IsMode)
            {
                Console.WriteLine("Type:        Mode (persistent behavior)");
            }

            var tools = skill.GetAllowedTools();
            if (tools.Count > 0)
            {
                Console.WriteLine("Tools:       {0}", string.Join(", ", tools));
            }

            // Show resources
            var resources = skill.Resources.ToList();
            if (resources.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\nResources ({0}):", resources.Count);
                Console.ForegroundColor = ConsoleColor.DarkGray;
                foreach (var resource in resources)
                {
                    string typeLabel;
                    string path = resource.RelativePath.ToLowerInvariant();
                    if (path.StartsWith("templates/") || path.StartsWith("templates\\"))
                        typeLabel = "[template]";
                    else if (path.StartsWith("checklists/") || path.StartsWith("checklists\\"))
                        typeLabel = "[checklist]";
                    else if (path.StartsWith("examples/") || path.StartsWith("examples\\"))
                        typeLabel = "[example]";
                    else if (path.StartsWith("references/") || path.StartsWith("references\\"))
                        typeLabel = "[reference]";
                    else if (path.StartsWith("scripts/") || path.StartsWith("scripts\\"))
                        typeLabel = "[script]";
                    else
                        typeLabel = "[asset]";
                    Console.WriteLine("  {0,-12} {1}", typeLabel, resource.RelativePath);
                }
            }

            Console.ResetColor();
            Console.WriteLine("\nUse /{0} to activate this skill.", skill.Name);
            if (resources.Count > 0)
            {
                Console.WriteLine("Use /resources {0} to view resource contents.", skill.Name);
            }
        }

        /// <summary>
        /// Shows resources bundled with a skill.
        /// </summary>
        private static void ShowSkillResources(string skillName)
        {
            if (!_skillRegistry.TryGet(skillName, out var skill))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Skill not found: {0}", skillName);
                Console.ResetColor();
                return;
            }

            var resources = skill.Resources.ToList();
            if (resources.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Skill '{0}' has no bundled resources.", skillName);
                Console.ResetColor();
                return;
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n=== Resources for {0} ({1} files) ===\n", skill.Name, resources.Count);
            Console.ResetColor();

            foreach (var resource in resources)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("--- {0} ---", resource.RelativePath);
                Console.ForegroundColor = ConsoleColor.DarkGray;

                string content = resource.GetContent();
                // Show first 30 lines or 1500 chars, whichever is less
                var lines = content.Split('\n');
                int linesToShow = Math.Min(30, lines.Length);
                string preview = string.Join("\n", lines.Take(linesToShow));
                if (preview.Length > 1500)
                {
                    preview = preview.Substring(0, 1500);
                }

                Console.WriteLine(preview);

                if (lines.Length > linesToShow || content.Length > 1500)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("\n... (truncated, {0} total lines)", lines.Length);
                }

                Console.WriteLine();
            }

            Console.ResetColor();
        }

        /// <summary>
        /// Creates a new skill interactively.
        /// </summary>
        private static void CreateSkillInteractively()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n=== Create New Skill ===\n");
            Console.ResetColor();

            Console.Write("Skill name (lowercase, hyphens allowed): ");
            string name = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(name) || !SkillMetadata.IsValidSkillName(name))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Invalid skill name. Use lowercase letters, numbers, and hyphens only.");
                Console.ResetColor();
                return;
            }

            if (_skillRegistry.Contains(name))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("A skill with this name already exists.");
                Console.ResetColor();
                return;
            }

            Console.Write("Description: ");
            string description = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(description))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Description is required.");
                Console.ResetColor();
                return;
            }

            Console.WriteLine("Enter instructions (end with a line containing only '---'):");
            var instructionsBuilder = new StringBuilder();
            string line;
            while ((line = Console.ReadLine()) != "---")
            {
                instructionsBuilder.AppendLine(line);
            }

            try
            {
                var skill = new SkillBuilder()
                    .WithName(name)
                    .WithDescription(description)
                    .WithVersion("1.0.0")
                    .WithInstructions(instructionsBuilder.ToString())
                    .Build();

                _skillRegistry.Register(skill);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\nSkill '{0}' created and registered successfully!", name);
                Console.WriteLine("Use /{0} to activate it.", name);
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Failed to create skill: {0}", ex.Message);
                Console.ResetColor();
            }
        }

        /// <summary>
        /// Loads a skill from a file path.
        /// </summary>
        private static void LoadSkillFromPath()
        {
            Console.Write("Enter skill directory path: ");
            string path = Console.ReadLine()?.Trim().Trim('"');

            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                var skill = _skillRegistry.LoadAndRegister(path);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Loaded skill: {0}", skill.Name);
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Failed to load skill: {0}", ex.Message);
                Console.ResetColor();
            }
        }

        /// <summary>
        /// Loads a skill from a remote URL.
        /// </summary>
        private static void LoadSkillFromUrl()
        {
            Console.Write("Enter skill URL (SKILL.md or .zip): ");
            string url = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("Downloading skill...");
            Console.ResetColor();

            try
            {
                int loaded = _skillRegistry.LoadFromUrlAsync(url).GetAwaiter().GetResult();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Loaded {0} skill(s) from URL.", loaded);
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Failed to load skill: {0}", ex.Message);
                Console.ResetColor();
            }
        }

        /// <summary>
        /// Loads a skill from a GitHub repository.
        /// </summary>
        private static void LoadSkillFromGitHub(string path)
        {
            var parts = path.Split('/');
            if (parts.Length < 3)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Usage: /github owner/repo/path/to/skill");
                Console.WriteLine("Example: /github LM-Kit/lm-kit-net-samples/skills/code-review");
                Console.ResetColor();
                return;
            }

            string owner = parts[0];
            string repo = parts[1];
            string skillPath = string.Join("/", parts.Skip(2));

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("Loading from GitHub: {0}/{1}/{2}...", owner, repo, skillPath);
            Console.ResetColor();

            try
            {
                var skill = _skillRegistry.LoadFromGitHubAsync(owner, repo, skillPath).GetAwaiter().GetResult();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Loaded skill: {0}", skill.Name);
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Failed to load skill: {0}", ex.Message);
                Console.ResetColor();
            }
        }

        /// <summary>
        /// Shows information about the skill cache.
        /// </summary>
        private static void ShowCacheInfo()
        {
            using (var loader = new SkillRemoteLoader())
            {
                var info = loader.GetCacheInfo();

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("\n=== Skill Cache ===\n");
                Console.ResetColor();

                Console.WriteLine("Directory: {0}", info.CacheDirectory);
                Console.WriteLine("Skills:    {0}", info.SkillCount);
                Console.WriteLine("Size:      {0}", info.TotalSizeFormatted);

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("\nUse /remote to load skills from URLs.");
                Console.WriteLine("Use /github owner/repo/path to load from GitHub.");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// Prepares the prompt with skill instructions if a skill is active.
        /// </summary>
        private static string PreparePromptWithSkill(string userPrompt)
        {
            if (_activeSkill == null)
            {
                return userPrompt;
            }

            // Format the skill instructions for injection
            string skillInstructions = _skillActivator.FormatForInjection(
                _activeSkill,
                SkillInjectionMode.UserMessage,
                userContext: null);

            // Combine skill context with user prompt
            return $"{skillInstructions}\n\n---\n\nUser request: {userPrompt}";
        }

        /// <summary>
        /// Selects a language model.
        /// </summary>
        private static LM SelectModel()
        {
            Console.Clear();
            Console.WriteLine("=== Agent Skills Demo ===\n");
            Console.WriteLine("Please select the model you want to use:\n");
            Console.WriteLine("0 - Mistral Ministral 3B (requires approximately 3 GB of VRAM)");
            Console.WriteLine("1 - Meta Llama 3.1 8B (requires approximately 6 GB of VRAM)");
            Console.WriteLine("2 - Google Gemma 3 4B (requires approximately 4 GB of VRAM)");
            Console.WriteLine("3 - Microsoft Phi-4 Mini 3.8B (requires approximately 3.3 GB of VRAM)");
            Console.WriteLine("4 - Alibaba Qwen-3 8B (requires approximately 5.6 GB of VRAM)");
            Console.WriteLine("5 - Microsoft Phi-4 14.7B (requires approximately 11 GB of VRAM)");
            Console.Write("Other entry: A custom model URI\n\n> ");

            string input = Console.ReadLine();
            string modelLink;

            switch (input?.Trim())
            {
                case "0": modelLink = DEFAULT_MINISTRAL_3_8_MODEL_PATH; break;
                case "1": modelLink = DEFAULT_LLAMA3_1_8B_MODEL_PATH; break;
                case "2": modelLink = DEFAULT_GEMMA3_4B_MODEL_PATH; break;
                case "3": modelLink = DEFAULT_PHI4_MINI_3_8B_MODEL_PATH; break;
                case "4": modelLink = DEFAULT_QWEN3_8B_MODEL_PATH; break;
                case "5": modelLink = DEFAULT_PHI4_14_7B_MODEL_PATH; break;
                default: modelLink = input?.Trim().Trim('"') ?? DEFAULT_PHI4_MINI_3_8B_MODEL_PATH; break;
            }

            Uri modelUri = new(modelLink);
            return new LM(
                modelUri,
                downloadingProgress: ModelDownloadingProgress,
                loadingProgress: ModelLoadingProgress);
        }

        /// <summary>
        /// Shows the welcome message.
        /// </summary>
        private static void ShowWelcome()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║            LM-Kit.NET Agent Skills Demo                       ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
            Console.ResetColor();

            Console.WriteLine();
            Console.WriteLine("Agent Skills are expert instruction sets bundled with resources");
            Console.WriteLine("(templates, checklists, references) that transform AI behavior.");
            Console.WriteLine();

            // Show quick start example
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("=== Quick Start Example ===");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("  1. Type:  /git-commit-pro");
            Console.WriteLine("  2. Paste a diff or describe your changes");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("  Example session:");
            Console.WriteLine("  ────────────────────────────────────────────────────────");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("  User: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("/git-commit-pro");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("  [Skill Activated: git-commit-pro]");
            Console.WriteLine("  [Resources: 1 file(s) - references/commit-types.md]");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("  User: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("I added input validation to the user registration form");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("  Assistant: ");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("feat(auth): add input validation to registration form");
            Console.WriteLine("  ────────────────────────────────────────────────────────");
            Console.ResetColor();
            Console.WriteLine();

            // Show available skills with resource counts
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("=== Available Skills ===");
            Console.ResetColor();
            Console.WriteLine();

            foreach (var skill in _skillRegistry.Skills.OrderBy(s => s.Name))
            {
                var resourceCount = skill.Resources.Count();
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("  /{0,-20}", skill.Name);
                if (resourceCount > 0)
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.Write(" [{0}]", resourceCount);
                }
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine(" {0}", TruncateText(skill.Description, 38));
            }

            Console.ResetColor();
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("Type /info <skill> to see resources, or /help for all commands.");
            Console.ResetColor();
        }

        /// <summary>
        /// Shows available commands.
        /// </summary>
        private static void ShowCommands()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n=== Commands ===\n");
            Console.ResetColor();

            Console.WriteLine("Skill Activation:");
            Console.WriteLine("  /<skill-name> [context]  Activate a skill (e.g., /git-commit-pro)");
            Console.WriteLine("  /skills                  List all available skills");
            Console.WriteLine("  /info <skill-name>       Show skill details and resources");
            Console.WriteLine("  /resources <skill-name>  View resource file contents");
            Console.WriteLine("  /active                  Show currently active skill");
            Console.WriteLine("  /deactivate              Deactivate current skill");
            Console.WriteLine();
            Console.WriteLine("Skill Discovery:");
            Console.WriteLine("  /search                  Search for skills by keyword");
            Console.WriteLine("  /discovery               Show skill discovery context for LLM");
            Console.WriteLine("  /auto                    Toggle automatic skill selection (SkillTool)");
            Console.WriteLine("  /tools                   Toggle on-demand resource loading (SkillResourceTool)");
            Console.WriteLine();
            Console.WriteLine("Skill Management:");
            Console.WriteLine("  /create                  Create a new skill interactively");
            Console.WriteLine("  /load                    Load a skill from a directory path");
            Console.WriteLine("  /remote                  Load a skill from a URL (SKILL.md or .zip)");
            Console.WriteLine("  /github owner/repo/path  Load a skill from GitHub");
            Console.WriteLine("  /cache                   Show skill cache info");
            Console.WriteLine();
            Console.WriteLine("Chat Commands:");
            Console.WriteLine("  /reset                   Clear chat history and deactivate skill");
            Console.WriteLine("  /regenerate              Regenerate the last response");
            Console.WriteLine("  /continue                Continue the last response");
            Console.WriteLine("  /help                    Show this help message");
            Console.WriteLine();
        }

        /// <summary>
        /// Handles text completion events for streaming output.
        /// </summary>
        private static void Chat_AfterTextCompletion(
            object sender,
            LMKit.TextGeneration.Events.AfterTextCompletionEventArgs e)
        {
            switch (e.SegmentType)
            {
                case TextSegmentType.InternalReasoning:
                    Console.ForegroundColor = ConsoleColor.Blue;
                    break;
                case TextSegmentType.ToolInvocation:
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    break;
                case TextSegmentType.UserVisible:
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
            }
            Console.Write(e.Text);
        }

        /// <summary>
        /// Truncates text to a maximum length.
        /// </summary>
        private static string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            // Remove newlines for display
            text = text.Replace("\r", "").Replace("\n", " ");

            return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";
        }
    }
}
