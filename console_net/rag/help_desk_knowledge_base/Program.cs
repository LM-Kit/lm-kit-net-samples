using LMKit.Data;
using LMKit.Model;
using LMKit.Retrieval;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Chat;
using LMKit.TextGeneration.Events;
using LMKit.TextGeneration.Sampling;
using System.Diagnostics;
using System.Text;

namespace help_desk_knowledge_base
{
    internal class Program
    {
        const string EmbeddingModelId = "embeddinggemma-300m";
        const string DataSourcePath = "help_desk_kb.dat";
        const string DataSourceId = "help_desk";

        // Category separator in section identifiers: "Category/Article Title"
        const char CategorySeparator = '/';

        static bool _isDownloading;
        static string? _scopeCategory = null;
        static bool _showSources = true;

        static void Main(string[] args)
        {
            // Set an optional license key here if available.
            // A free community license can be obtained from: https://lm-kit.com/products/community-edition/
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            PrintHeader();

            // ── Step 1: Model selection ─────────────────────────────────────────

            PrintSection("Chat Model Selection");
            Console.WriteLine("  0 - Alibaba Qwen-3 8B       (~6 GB VRAM) [Recommended]");
            Console.WriteLine("  1 - Google Gemma 3 4B        (~4 GB VRAM)");
            Console.WriteLine("  2 - Google Gemma 3 12B       (~9 GB VRAM)");
            Console.WriteLine("  3 - Alibaba Qwen-3 14B       (~10 GB VRAM)");
            Console.WriteLine("  4 - Alibaba Qwen 3.5 27B      (~18 GB VRAM)");
            Console.WriteLine("  *   Or enter a custom model URI or model ID");
            Console.WriteLine();

            LM chatModel = PromptModelSelection();

            Console.Clear();
            PrintHeader();

            PrintSection("Loading Models");
            PrintStatus("Chat model loaded", ConsoleColor.Green);

            LM embeddingModel = LM.LoadFromModelID(
                EmbeddingModelId,
                downloadingProgress: OnDownloadProgress,
                loadingProgress: OnLoadProgress);

            PrintStatus($"Embedding model loaded ({EmbeddingModelId})", ConsoleColor.Green);
            Console.WriteLine();

            // ── Step 2: Load or create persistent knowledge base ────────────────

            PrintSection("Knowledge Base");

            DataSource dataSource;
            bool isNew = false;

            if (File.Exists(DataSourcePath))
            {
                dataSource = DataSource.LoadFromFile(DataSourcePath, readOnly: false);
                int articleCount = dataSource.Sections.Count();
                PrintStatus($"Loaded existing knowledge base: {articleCount} articles from {DataSourcePath}", ConsoleColor.Green);
            }
            else
            {
                dataSource = DataSource.CreateFileDataSource(
                    DataSourcePath,
                    DataSourceId,
                    embeddingModel);

                isNew = true;
                PrintStatus($"Created new knowledge base at {DataSourcePath}", ConsoleColor.Green);
            }

            var ragEngine = new RagEngine(embeddingModel);
            ragEngine.AddDataSource(dataSource);

            // Seed with sample articles if this is a fresh knowledge base
            if (isNew)
            {
                PrintStatus("Seeding with sample help desk articles...", ConsoleColor.DarkGray);
                var sw = Stopwatch.StartNew();
                int totalChunks = SeedKnowledgeBase(ragEngine);
                sw.Stop();

                int articleCount = dataSource.Sections.Count();
                PrintStatus(
                    $"Indexed {articleCount} articles ({totalChunks} chunks) in {sw.Elapsed.TotalSeconds:F1}s",
                    ConsoleColor.Green);
                PrintStatus("Knowledge base persisted to disk. It will reload instantly next time.", ConsoleColor.DarkGray);
            }

            Console.WriteLine();

            // ── Step 3: Create chat and start interactive loop ──────────────────

            PrintSection("Help Desk Assistant");

            var chat = new SingleTurnConversation(chatModel)
            {
                SystemPrompt = "You are a helpful customer support assistant. Answer the user's question " +
                               "based on the provided context. Be concise and direct. If the context does " +
                               "not contain enough information to answer, say so honestly. Always be polite " +
                               "and professional.",
                SamplingMode = new GreedyDecoding()
            };

            chat.AfterTextCompletion += OnAfterTextCompletion;

            PrintCommands();
            PrintDivider();
            PrintKnowledgeBaseSummary(dataSource);
            Console.WriteLine();

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                if (_scopeCategory != null)
                {
                    Console.Write($"  [{_scopeCategory}] You: ");
                }
                else
                {
                    Console.Write("  You: ");
                }
                Console.ResetColor();

                string? line = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(line))
                {
                    break;
                }

                string input = line.Trim();

                if (input.StartsWith('/'))
                {
                    HandleCommand(input, ragEngine, dataSource, embeddingModel);
                    continue;
                }

                // Run retrieval + answer generation
                QueryKnowledgeBase(input, ragEngine, chat);
            }

            Console.WriteLine();
            PrintDivider();
            PrintStatus("Demo ended. Press any key to exit.", ConsoleColor.DarkGray);
            Console.ReadKey(true);
        }

        // ─── Query Pipeline ────────────────────────────────────────────────────

        static void QueryKnowledgeBase(string question, RagEngine ragEngine, SingleTurnConversation chat)
        {
            Console.WriteLine();

            // Apply scope filter
            if (_scopeCategory != null)
            {
                string prefix = _scopeCategory + CategorySeparator;
                // DataFilter: return true to EXCLUDE, false to INCLUDE
                ragEngine.Filter = new DataFilter(
                    sectionFilter: section => !section.Identifier.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

                PrintStatus($"Searching in: {_scopeCategory}", ConsoleColor.DarkGray);
            }
            else
            {
                ragEngine.Filter = null;
            }

            var sw = Stopwatch.StartNew();
            var partitions = ragEngine.FindMatchingPartitions(question, topK: 5, minScore: 0.3f);
            var retrievalTime = sw.Elapsed;

            if (partitions.Count == 0)
            {
                PrintStatus("No relevant articles found. Try broadening your query or removing the scope filter.", ConsoleColor.Yellow);
                return;
            }

            // Show sources
            if (_showSources)
            {
                PrintStatus($"Retrieved {partitions.Count} passages in {retrievalTime.TotalMilliseconds:F0}ms:", ConsoleColor.DarkCyan);

                var grouped = partitions
                    .GroupBy(p => p.SectionIdentifier)
                    .OrderByDescending(g => g.Max(p => p.Similarity));

                foreach (var group in grouped)
                {
                    ParseSectionId(group.Key, out string category, out string title);
                    float bestScore = group.Max(p => p.Similarity);
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"    {category} > {title} (score: {bestScore:F2}, {group.Count()} chunk(s))");
                    Console.ResetColor();
                }
            }

            // Generate answer
            PrintStatus("Generating answer...", ConsoleColor.DarkYellow);
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("  Assistant: ");
            Console.ResetColor();

            sw.Restart();
            var result = ragEngine.QueryPartitions(question, partitions, chat);
            sw.Stop();

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(
                $"\n  [{partitions.Count} passages | " +
                $"retrieval: {retrievalTime.TotalMilliseconds:F0}ms | " +
                $"generation: {sw.Elapsed.TotalMilliseconds:F0}ms | " +
                $"quality: {result.QualityScore:F2}]");
            Console.ResetColor();
            Console.WriteLine();
        }

        // ─── Command Handling ──────────────────────────────────────────────────

        static void HandleCommand(string input, RagEngine ragEngine, DataSource dataSource, LM embeddingModel)
        {
            string[] parts = SplitCommand(input);
            string cmd = parts[0].ToLowerInvariant();

            switch (cmd)
            {
                case "/add":
                    HandleAdd(parts, ragEngine);
                    break;

                case "/addfile":
                    HandleAddFile(parts, ragEngine);
                    break;

                case "/remove":
                    HandleRemove(parts, ragEngine, dataSource);
                    break;

                case "/list":
                    HandleList(parts, dataSource);
                    break;

                case "/categories":
                    PrintKnowledgeBaseSummary(dataSource);
                    break;

                case "/scope":
                    HandleScope(parts, dataSource);
                    break;

                case "/sources":
                    _showSources = !_showSources;
                    PrintStatus($"Source display: {(_showSources ? "ON" : "OFF")}", ConsoleColor.Green);
                    break;

                case "/stats":
                    HandleStats(dataSource);
                    break;

                case "/help":
                    PrintCommands();
                    break;

                default:
                    PrintStatus($"Unknown command: {cmd}. Type /help for available commands.", ConsoleColor.Yellow);
                    break;
            }
        }

        static void HandleAdd(string[] parts, RagEngine ragEngine)
        {
            // /add Category "Article Title"
            // Then prompt for content interactively
            if (parts.Length < 3)
            {
                PrintStatus("Usage: /add <category> <title>", ConsoleColor.Yellow);
                PrintStatus("  Example: /add Billing Cancellation Policy", ConsoleColor.DarkGray);
                return;
            }

            string category = parts[1];
            string title = string.Join(' ', parts.Skip(2));
            string sectionId = category + CategorySeparator + title;

            // Check for duplicate
            var ds = ragEngine.DataSources[0];
            if (ds.HasSection(sectionId))
            {
                PrintStatus($"Article \"{title}\" already exists in {category}.", ConsoleColor.Yellow);
                return;
            }

            PrintStatus("Enter article content (type END on a new line to finish):", ConsoleColor.DarkGray);
            var content = new StringBuilder();

            while (true)
            {
                string? line = Console.ReadLine();
                if (line == null || line.Trim().Equals("END", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
                content.AppendLine(line);
            }

            if (content.Length == 0)
            {
                PrintStatus("No content provided. Article not added.", ConsoleColor.Yellow);
                return;
            }

            var sw = Stopwatch.StartNew();
            ragEngine.ImportText(
                content.ToString(),
                new MarkdownChunking() { MaxChunkSize = 300 },
                DataSourceId,
                sectionId);
            sw.Stop();

            var section = ds.GetSectionByIdentifier(sectionId);
            int chunks = section?.Partitions.Count ?? 0;

            PrintStatus($"Added \"{title}\" to {category} ({chunks} chunks, {sw.Elapsed.TotalSeconds:F1}s)", ConsoleColor.Green);
        }

        static void HandleAddFile(string[] parts, RagEngine ragEngine)
        {
            // /addfile Category "Article Title" path/to/file.md
            if (parts.Length < 4)
            {
                PrintStatus("Usage: /addfile <category> <title> <filepath>", ConsoleColor.Yellow);
                PrintStatus("  Example: /addfile Billing \"Pricing FAQ\" ./pricing.md", ConsoleColor.DarkGray);
                return;
            }

            string category = parts[1];
            string title = parts[2];
            string filePath = string.Join(' ', parts.Skip(3));
            string sectionId = category + CategorySeparator + title;

            if (!File.Exists(filePath))
            {
                PrintStatus($"File not found: {filePath}", ConsoleColor.Red);
                return;
            }

            var ds = ragEngine.DataSources[0];
            if (ds.HasSection(sectionId))
            {
                PrintStatus($"Article \"{title}\" already exists in {category}.", ConsoleColor.Yellow);
                return;
            }

            string content = File.ReadAllText(filePath);
            var sw = Stopwatch.StartNew();
            ragEngine.ImportText(
                content,
                new MarkdownChunking() { MaxChunkSize = 300 },
                DataSourceId,
                sectionId);
            sw.Stop();

            var section = ds.GetSectionByIdentifier(sectionId);
            int chunks = section?.Partitions.Count ?? 0;

            PrintStatus($"Added \"{title}\" to {category} from {filePath} ({chunks} chunks, {sw.Elapsed.TotalSeconds:F1}s)", ConsoleColor.Green);
        }

        static void HandleRemove(string[] parts, RagEngine ragEngine, DataSource dataSource)
        {
            if (parts.Length < 2)
            {
                PrintStatus("Usage: /remove <article title or full section id>", ConsoleColor.Yellow);
                PrintStatus("  Example: /remove Refund Policy", ConsoleColor.DarkGray);
                return;
            }

            string search = string.Join(' ', parts.Skip(1));

            // Try exact match first
            string? matchId = null;

            foreach (var section in dataSource.Sections)
            {
                if (section.Identifier.Equals(search, StringComparison.OrdinalIgnoreCase))
                {
                    matchId = section.Identifier;
                    break;
                }
            }

            // If no exact match, try partial match on the title part
            if (matchId == null)
            {
                var candidates = new List<string>();

                foreach (var section in dataSource.Sections)
                {
                    ParseSectionId(section.Identifier, out _, out string title);
                    if (title.Contains(search, StringComparison.OrdinalIgnoreCase))
                    {
                        candidates.Add(section.Identifier);
                    }
                }

                if (candidates.Count == 1)
                {
                    matchId = candidates[0];
                }
                else if (candidates.Count > 1)
                {
                    PrintStatus($"Multiple matches for \"{search}\":", ConsoleColor.Yellow);
                    foreach (var c in candidates)
                    {
                        PrintStatus($"  - {c}", ConsoleColor.DarkGray);
                    }
                    PrintStatus("Be more specific or use the full section identifier.", ConsoleColor.DarkGray);
                    return;
                }
            }

            if (matchId == null)
            {
                PrintStatus($"No article found matching \"{search}\".", ConsoleColor.Yellow);
                return;
            }

            ParseSectionId(matchId, out string cat, out string ttl);
            bool removed = dataSource.RemoveSection(matchId);

            if (removed)
            {
                PrintStatus($"Removed \"{ttl}\" from {cat}.", ConsoleColor.Green);
            }
            else
            {
                PrintStatus($"Failed to remove \"{matchId}\".", ConsoleColor.Red);
            }
        }

        static void HandleList(string[] parts, DataSource dataSource)
        {
            string? filterCategory = parts.Length >= 2 ? string.Join(' ', parts.Skip(1)) : null;
            var articles = GetArticlesByCategory(dataSource);

            if (filterCategory != null)
            {
                var match = articles.Keys
                    .FirstOrDefault(k => k.Equals(filterCategory, StringComparison.OrdinalIgnoreCase));

                if (match == null)
                {
                    PrintStatus($"Category \"{filterCategory}\" not found.", ConsoleColor.Yellow);
                    return;
                }

                PrintSection(match);
                foreach (var (title, chunks) in articles[match])
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write($"  - {title}");
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($" ({chunks} chunks)");
                    Console.ResetColor();
                }
            }
            else
            {
                foreach (var (category, items) in articles)
                {
                    PrintSection(category);
                    foreach (var (title, chunks) in items)
                    {
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Write($"  - {title}");
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine($" ({chunks} chunks)");
                        Console.ResetColor();
                    }
                    Console.WriteLine();
                }
            }
        }

        static void HandleScope(string[] parts, DataSource dataSource)
        {
            if (parts.Length < 2)
            {
                PrintStatus($"Current scope: {_scopeCategory ?? "all categories"}", ConsoleColor.White);
                PrintStatus("Usage: /scope <category> or /scope all", ConsoleColor.DarkGray);
                return;
            }

            string arg = string.Join(' ', parts.Skip(1));

            if (arg.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                _scopeCategory = null;
                PrintStatus("Scope cleared. Searching all categories.", ConsoleColor.Green);
                return;
            }

            // Find matching category
            var categories = GetCategories(dataSource);
            var match = categories.FirstOrDefault(c => c.Equals(arg, StringComparison.OrdinalIgnoreCase));

            if (match == null)
            {
                PrintStatus($"Category \"{arg}\" not found. Available:", ConsoleColor.Yellow);
                foreach (var c in categories)
                {
                    PrintStatus($"  - {c}", ConsoleColor.DarkGray);
                }
                return;
            }

            _scopeCategory = match;
            PrintStatus($"Scope set to \"{match}\". Queries will only search articles in this category.", ConsoleColor.Green);
        }

        static void HandleStats(DataSource dataSource)
        {
            var articles = GetArticlesByCategory(dataSource);
            int totalArticles = 0;
            int totalChunks = 0;

            foreach (var (_, items) in articles)
            {
                foreach (var (_, chunks) in items)
                {
                    totalArticles++;
                    totalChunks += chunks;
                }
            }

            PrintSection("Knowledge Base Statistics");
            PrintStatus($"  Storage file:     {DataSourcePath}", ConsoleColor.White);
            PrintStatus($"  File exists:      {File.Exists(DataSourcePath)}", ConsoleColor.White);

            if (File.Exists(DataSourcePath))
            {
                long fileSize = new FileInfo(DataSourcePath).Length;
                PrintStatus($"  File size:        {FormatFileSize(fileSize)}", ConsoleColor.White);
            }

            PrintStatus($"  Categories:       {articles.Count}", ConsoleColor.White);
            PrintStatus($"  Total articles:   {totalArticles}", ConsoleColor.White);
            PrintStatus($"  Total chunks:     {totalChunks}", ConsoleColor.White);
            PrintStatus($"  Current scope:    {_scopeCategory ?? "all"}", ConsoleColor.White);
            PrintStatus($"  Show sources:     {(_showSources ? "ON" : "OFF")}", ConsoleColor.White);
        }

        // ─── Knowledge Base Management Helpers ─────────────────────────────────

        static void ParseSectionId(string sectionId, out string category, out string title)
        {
            int sep = sectionId.IndexOf(CategorySeparator);
            if (sep >= 0)
            {
                category = sectionId.Substring(0, sep);
                title = sectionId.Substring(sep + 1);
            }
            else
            {
                category = "Uncategorized";
                title = sectionId;
            }
        }

        static SortedDictionary<string, List<(string Title, int Chunks)>> GetArticlesByCategory(DataSource dataSource)
        {
            var result = new SortedDictionary<string, List<(string, int)>>(StringComparer.OrdinalIgnoreCase);

            foreach (var section in dataSource.Sections)
            {
                ParseSectionId(section.Identifier, out string category, out string title);

                if (!result.ContainsKey(category))
                {
                    result[category] = new List<(string, int)>();
                }

                result[category].Add((title, section.Partitions.Count));
            }

            return result;
        }

        static List<string> GetCategories(DataSource dataSource)
        {
            var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var section in dataSource.Sections)
            {
                ParseSectionId(section.Identifier, out string category, out _);
                categories.Add(category);
            }

            return categories.OrderBy(c => c).ToList();
        }

        // ─── Knowledge Base Seeding ────────────────────────────────────────────

        static int SeedKnowledgeBase(RagEngine ragEngine)
        {
            int totalChunks = 0;
            var articles = GetSeedArticles();

            foreach (var (sectionId, content) in articles)
            {
                ragEngine.ImportText(
                    content,
                    new MarkdownChunking() { MaxChunkSize = 300 },
                    DataSourceId,
                    sectionId);

                var section = ragEngine.DataSources[0].GetSectionByIdentifier(sectionId);
                totalChunks += section?.Partitions.Count ?? 0;
            }

            return totalChunks;
        }

        // ─── Sample Help Desk Articles ─────────────────────────────────────────
        //
        // Fictional "CloudPeak SaaS" product support documentation.
        // Markdown-formatted articles organized into categories.

        static List<(string SectionId, string Content)> GetSeedArticles()
        {
            return new List<(string, string)>
            {
                ("Account Management/Password Reset", """
                # Password Reset Guide

                ## Using the self-service portal

                1. Go to **cloudpeak.io/reset**
                2. Enter the email address associated with your account
                3. Click **Send Reset Link**
                4. Check your inbox (and spam folder) for an email from noreply@cloudpeak.io
                5. Click the reset link within 30 minutes (it expires after that)
                6. Choose a new password that meets our requirements

                ## Password requirements

                - Minimum 12 characters
                - At least one uppercase letter, one lowercase letter, one number
                - At least one special character (!@#$%^&*)
                - Cannot reuse your last 5 passwords

                ## If you don't receive the reset email

                - Verify you entered the correct email address
                - Check your spam/junk folder
                - Ensure your account is not locked (contact support after 5 failed attempts)
                - Corporate email filters may block automated messages; try a personal email if configured as a backup
                """),

                ("Account Management/Account Recovery", """
                # Account Recovery FAQ

                ## My account is locked after too many failed login attempts

                Accounts lock automatically after **5 consecutive failed attempts**. The lock lasts **30 minutes**. After 30 minutes, try again with the correct credentials. If you forgot your password, use the password reset flow.

                ## I no longer have access to my 2FA device

                Contact support at **support@cloudpeak.io** with:
                - Your account email address
                - The last 4 digits of the payment method on file
                - A valid government-issued ID for identity verification

                Recovery takes 1 to 3 business days. A temporary access code will be sent to your verified recovery email address.

                ## My account was compromised

                If you suspect unauthorized access:
                1. Change your password immediately using the reset flow
                2. Revoke all active sessions from **Settings > Security > Active Sessions**
                3. Enable 2FA if not already active
                4. Contact support to report the incident
                5. Review your audit log at **Settings > Security > Audit Log** for suspicious activity
                """),

                ("Account Management/Profile Settings", """
                # Profile Settings

                ## Editing your profile

                Navigate to **Settings > Profile** to update:
                - **Display name**: shown in team workspaces and comments
                - **Email address**: changing your email requires verification of the new address
                - **Timezone**: affects all scheduled reports and notification times
                - **Language**: UI language (English, French, German, Spanish, Japanese, Mandarin)
                - **Avatar**: upload a JPG or PNG image (max 2 MB)

                ## Notification preferences

                Configure notifications at **Settings > Notifications**:
                - **Email digest**: daily, weekly, or off
                - **In-app notifications**: real-time alerts for mentions, assignments, and status changes
                - **Slack integration**: connect your Slack workspace to receive notifications in a channel
                - **Quiet hours**: suppress all notifications during specified hours (e.g., 10 PM to 7 AM)
                """),

                ("Account Management/Two-Factor Authentication", """
                # Two-Factor Authentication (2FA)

                ## Setting up 2FA

                1. Go to **Settings > Security > Two-Factor Authentication**
                2. Click **Enable 2FA**
                3. Scan the QR code with an authenticator app (Google Authenticator, Authy, or 1Password)
                4. Enter the 6-digit code displayed in the app to confirm
                5. **Save your recovery codes** in a secure location (you get 10 single-use codes)

                ## Supported methods

                - **TOTP authenticator app** (recommended): generates time-based codes
                - **SMS codes**: sent to your verified phone number (less secure, vulnerable to SIM swapping)
                - **Hardware security keys**: FIDO2/WebAuthn keys (YubiKey, Titan)

                ## Requirement for team admins

                All users with the **Admin** or **Owner** role are required to enable 2FA. Accounts without 2FA will be prompted to set it up on every login until configured.
                """),

                ("Account Management/Account Deletion", """
                # Account Deletion

                ## How to delete your account

                1. Go to **Settings > Account > Delete Account**
                2. Enter your password to confirm
                3. Choose whether to **export your data** before deletion
                4. Click **Permanently Delete My Account**

                ## What gets deleted

                - All personal data (name, email, payment info)
                - All projects you own that have no other members
                - Your comments and activity history (anonymized, not deleted, in shared projects)
                - API keys and integrations

                ## What does NOT get deleted

                - Shared projects continue to exist for other members
                - Messages sent in team channels are retained (attributed to "Deleted User")
                - Invoices are retained for 7 years for legal compliance

                ## Cooling-off period

                After requesting deletion, you have **14 days** to change your mind. Log in during this period to cancel the deletion. After 14 days, deletion is permanent and irreversible.
                """),

                ("Billing/Subscription Plans", """
                # Subscription Plans

                ## Available plans

                | Plan | Price | Users | Storage | Support |
                |------|-------|-------|---------|---------|
                | **Starter** | $0/month | Up to 3 | 1 GB | Community forum |
                | **Professional** | $29/user/month | Up to 50 | 50 GB | Email (24h response) |
                | **Business** | $69/user/month | Unlimited | 500 GB | Priority (4h response) |
                | **Enterprise** | Custom pricing | Unlimited | Unlimited | Dedicated CSM, SLA |

                ## Plan features

                All paid plans include: custom domains, API access, SSO integration, advanced analytics, and audit logs.

                Business and Enterprise additionally include: SAML SSO, SCIM provisioning, custom roles, data residency options, and 99.9% uptime SLA.

                ## Changing plans

                Upgrade or downgrade at any time from **Settings > Billing > Change Plan**. Upgrades take effect immediately with prorated billing. Downgrades take effect at the end of the current billing cycle.
                """),

                ("Billing/Payment Methods", """
                # Payment Methods

                ## Accepted payment methods

                - **Credit/debit cards**: Visa, Mastercard, American Express
                - **ACH bank transfer**: US bank accounts only, 3 to 5 business days processing
                - **Wire transfer**: available for Enterprise plans (annual billing only)
                - **PayPal**: available in all supported regions

                ## Updating your payment method

                1. Go to **Settings > Billing > Payment Methods**
                2. Click **Add Payment Method**
                3. Enter your card or bank details
                4. Set as default if desired
                5. Optionally remove old payment methods

                ## Failed payments

                If a payment fails, we retry automatically after **3 days**, then **7 days**, then **14 days**. After 3 failed attempts across 14 days, the account enters a **grace period** of 7 additional days. If still unpaid, the account is downgraded to the Starter plan and project data is retained for 90 days.

                ## Billing cycle

                All plans are billed monthly by default. Annual billing is available with a **20% discount**. Switch to annual billing at **Settings > Billing > Billing Cycle**.
                """),

                ("Billing/Refund Policy", """
                # Refund Policy

                ## Eligibility

                Refunds are available under the following conditions:
                - **Within 14 days** of initial subscription or upgrade (no questions asked)
                - **Service outage**: if CloudPeak experienced a confirmed outage exceeding 4 hours during your billing period
                - **Billing error**: duplicate charges or incorrect plan charges

                ## How to request a refund

                Email **billing@cloudpeak.io** with:
                - Your account email
                - Invoice number (found at **Settings > Billing > Invoices**)
                - Reason for the refund request

                Refunds are processed within **5 to 10 business days** and returned to the original payment method.

                ## Non-refundable items

                - Usage-based charges (API calls, storage overages) are non-refundable
                - Add-on features purchased separately
                - Accounts older than 14 days (except for outage or billing error)
                """),

                ("Billing/Invoices and Receipts", """
                # Invoices and Receipts

                ## Accessing invoices

                All invoices are available at **Settings > Billing > Invoices**. Each invoice includes:
                - Invoice number and date
                - Plan name and billing period
                - Itemized charges (base plan, add-ons, overages)
                - Tax information (if applicable)
                - Payment status (paid, pending, overdue)

                ## Downloading invoices

                Click the download icon next to any invoice to get a PDF. For bulk downloads, use the **Export All** button to download a ZIP archive of all invoices for a selected year.

                ## Tax information

                Add your tax ID (VAT, GST, etc.) at **Settings > Billing > Tax Information**. This will be included on all future invoices. CloudPeak charges applicable sales tax based on your billing address and local regulations.

                ## Updating billing address

                Change your billing address at **Settings > Billing > Billing Address**. Updates apply to future invoices only. Contact billing@cloudpeak.io to correct a past invoice.
                """),

                ("Technical Support/Connectivity Issues", """
                # Connectivity Troubleshooting

                ## Cannot connect to CloudPeak

                **Step 1: Check service status**
                Visit **status.cloudpeak.io** for real-time system status. If there is an active incident, no action is needed on your side.

                **Step 2: Check your network**
                - Ensure you have internet access (try visiting other websites)
                - Disable VPN temporarily (some VPNs block WebSocket connections used by CloudPeak)
                - Check if your corporate firewall allows traffic to `*.cloudpeak.io` on ports 443 and 8443

                **Step 3: Clear browser cache**
                - Chrome: Settings > Privacy > Clear Browsing Data > Cached Images and Files
                - Firefox: Preferences > Privacy > Clear Data

                **Step 4: Try a different browser or incognito mode**
                Browser extensions (especially ad blockers) can interfere with CloudPeak. Test in an incognito/private window.

                ## Slow performance

                - Check your internet speed at fast.com (minimum 5 Mbps recommended)
                - Close unnecessary browser tabs (CloudPeak uses real-time sync, which needs memory)
                - Switch to the nearest region at **Settings > Workspace > Region**
                """),

                ("Technical Support/Error Codes", """
                # Error Code Reference

                ## Client-Side Errors (4xx)

                | Code | Name | Description | Resolution |
                |------|------|-------------|------------|
                | CP-400 | Bad Request | The request was malformed | Check request body and parameters |
                | CP-401 | Unauthorized | Invalid or expired authentication token | Re-authenticate or refresh your token |
                | CP-403 | Forbidden | Insufficient permissions | Check your role and project permissions |
                | CP-404 | Not Found | The requested resource does not exist | Verify the resource ID or URL |
                | CP-409 | Conflict | The resource was modified by another user | Reload and retry the operation |
                | CP-429 | Rate Limited | Too many requests | Wait and retry with exponential backoff |

                ## Server-Side Errors (5xx)

                | Code | Name | Description | Resolution |
                |------|------|-------------|------------|
                | CP-500 | Internal Error | Unexpected server failure | Retry after 30 seconds; report if persistent |
                | CP-502 | Bad Gateway | Upstream service timeout | Usually resolves within 1 to 2 minutes |
                | CP-503 | Service Unavailable | System under maintenance | Check status.cloudpeak.io |

                ## API-Specific Errors

                | Code | Name | Description |
                |------|------|-------------|
                | CP-1001 | Invalid Schema | The JSON body does not match the expected schema |
                | CP-1002 | Quota Exceeded | Monthly API call quota exceeded for your plan |
                | CP-1003 | Webhook Delivery Failed | The target URL returned a non-2xx response |
                """),

                ("Technical Support/Data Export and Import", """
                # Data Export and Import

                ## Exporting your data

                Go to **Settings > Account > Export Data** to download all your data.

                Export formats available:
                - **JSON**: machine-readable, suitable for migration or backup
                - **CSV**: spreadsheet-compatible, one file per project
                - **ZIP archive**: all files, attachments, and metadata

                Large exports (>1 GB) are processed in the background. You will receive an email with a download link when the export is ready (usually within 1 hour).

                ## Importing data

                CloudPeak supports importing from:
                - **CSV files**: go to **Project > Settings > Import > CSV**
                - **Trello boards**: go to **Project > Settings > Import > Trello** and authorize access
                - **Asana projects**: go to **Project > Settings > Import > Asana**
                - **Jira issues**: go to **Project > Settings > Import > Jira** (requires admin access to Jira)

                ## API-based migration

                For large-scale migrations, use the CloudPeak REST API:
                ```
                POST /api/v2/projects/{id}/import
                Content-Type: application/json
                Authorization: Bearer <token>
                ```
                Rate limit: 100 requests per minute. Use batch endpoints for bulk operations.
                """),

                ("Technical Support/API Rate Limits", """
                # API Rate Limits

                ## Default limits by plan

                | Plan | Requests/minute | Requests/day | Concurrent connections |
                |------|----------------|--------------|----------------------|
                | Starter | 60 | 1,000 | 2 |
                | Professional | 300 | 50,000 | 10 |
                | Business | 1,000 | 500,000 | 50 |
                | Enterprise | Custom | Custom | Custom |

                ## Rate limit headers

                Every API response includes these headers:
                - `X-RateLimit-Limit`: your plan's requests/minute limit
                - `X-RateLimit-Remaining`: remaining requests in the current window
                - `X-RateLimit-Reset`: Unix timestamp when the window resets

                ## When you hit the limit

                The API returns **HTTP 429 (Too Many Requests)** with error code **CP-429**. The response body includes a `retry_after` field (in seconds).

                **Best practice**: implement exponential backoff starting at 1 second, doubling up to 32 seconds.

                ## Requesting a limit increase

                Business and Enterprise customers can request higher limits. Email **api-support@cloudpeak.io** with your use case and expected request volume.
                """),

                ("Technical Support/Performance Optimization", """
                # Performance Optimization Tips

                ## Workspace performance

                - **Archive old projects**: archived projects are excluded from search and dashboards
                - **Limit dashboard widgets**: each widget makes a separate data query
                - **Use filters**: filtered views load faster than unfiltered ones

                ## API performance

                - **Use pagination**: never fetch all records at once; default page size is 50, max is 200
                - **Select specific fields**: use `?fields=id,name,status` to reduce payload size
                - **Cache responses**: use ETags (`If-None-Match` header) to avoid re-downloading unchanged data
                - **Batch operations**: use `POST /api/v2/batch` to combine up to 20 requests in a single call

                ## Browser recommendations

                - Chrome 90+ or Firefox 95+ for best performance
                - Minimum 4 GB RAM free for large workspaces
                - Disable browser extensions if experiencing slow rendering
                """),

                ("Getting Started/Quick Start Guide", """
                # Quick Start Guide

                Welcome to CloudPeak! Follow these steps to get up and running in under 5 minutes.

                ## Step 1: Create your workspace

                After signing up at **cloudpeak.io/signup**, you will be prompted to create your first workspace. A workspace is a shared environment where your team collaborates.

                ## Step 2: Invite your team

                Go to **Workspace Settings > Members > Invite** and enter email addresses. Team members receive an invitation link to join.

                ## Step 3: Create your first project

                Click **New Project** in the sidebar. Choose a template or start from scratch. Projects contain tasks, files, and discussions.

                ## Step 4: Set up integrations

                Connect your existing tools at **Workspace Settings > Integrations**:
                - **Slack**: receive notifications and create tasks from Slack
                - **GitHub/GitLab**: link commits and pull requests to tasks
                - **Google Drive/OneDrive**: attach files directly from cloud storage
                - **Zapier**: automate workflows with 3,000+ apps

                ## Step 5: Explore the API

                API documentation is available at **docs.cloudpeak.io/api**. Generate an API key at **Settings > API Keys**.
                """),

                ("Getting Started/System Requirements", """
                # System Requirements

                ## Web application

                CloudPeak is a web application and works in any modern browser:
                - **Chrome** 90 or later (recommended)
                - **Firefox** 95 or later
                - **Safari** 15 or later
                - **Edge** 90 or later

                Minimum screen resolution: 1280 x 720 pixels.
                Recommended internet speed: 5 Mbps or faster.

                ## Desktop application

                Available for Windows and macOS:
                - **Windows**: Windows 10 or later, 4 GB RAM, 200 MB disk space
                - **macOS**: macOS 12 (Monterey) or later, 4 GB RAM, 200 MB disk space

                Download from **cloudpeak.io/download**.

                ## Mobile application

                - **iOS**: iOS 16 or later (iPhone and iPad)
                - **Android**: Android 12 or later

                Available on the App Store and Google Play.
                """),

                ("Getting Started/First Project Setup", """
                # Setting Up Your First Project

                ## Choosing a template

                CloudPeak provides templates for common workflows:
                - **Task Board**: Kanban-style board with To Do, In Progress, Done columns
                - **Sprint Planner**: agile sprint planning with backlog, sprint board, and burndown chart
                - **Content Calendar**: editorial calendar with publish dates and status tracking
                - **Bug Tracker**: issue tracking with severity, priority, and assignee fields
                - **Custom**: blank project, configure everything from scratch

                ## Configuring project settings

                After creating a project, configure:
                - **Members**: add team members and set roles (Owner, Editor, Viewer)
                - **Custom fields**: add fields like Priority, Estimated Hours, or Client Name
                - **Automations**: set up rules (e.g., "When status changes to Done, notify the project owner")
                - **Webhooks**: notify external services when events occur

                ## Organizing work

                - **Sections**: group related tasks (e.g., "Design", "Development", "QA")
                - **Tags**: cross-cutting labels (e.g., "urgent", "blocked", "customer-facing")
                - **Dependencies**: link tasks to show which must complete before others can start
                """),

                ("Security/Data Privacy", """
                # Data Privacy

                ## Where is my data stored?

                CloudPeak stores data in AWS data centers. You can choose your data region:
                - **US East** (Virginia): default for US customers
                - **EU West** (Frankfurt): default for EU customers, GDPR-compliant
                - **Asia Pacific** (Singapore): for APAC customers

                Enterprise customers can request dedicated tenancy with data isolation.

                ## Data processing

                CloudPeak processes your data solely to provide the service. We do not:
                - Sell your data to third parties
                - Use your data for advertising
                - Train AI models on your content (all AI features use isolated, per-tenant processing)

                ## GDPR compliance

                CloudPeak is GDPR compliant. EU customers can request:
                - **Data access**: export all personal data (Settings > Account > Export)
                - **Data deletion**: permanently remove your account and data
                - **Data portability**: export in machine-readable format (JSON, CSV)
                - **Right to rectification**: update inaccurate personal data in your profile

                Our Data Protection Officer can be reached at **dpo@cloudpeak.io**.
                """),

                ("Security/Encryption", """
                # Encryption and Data Protection

                ## Data in transit

                All connections to CloudPeak use **TLS 1.3** encryption. We enforce HTTPS for all web and API traffic. HTTP requests are automatically redirected to HTTPS.

                ## Data at rest

                All customer data is encrypted at rest using **AES-256-GCM**:
                - Database records: encrypted at the storage engine level
                - File attachments: encrypted before writing to object storage (S3)
                - Backups: encrypted with a separate key stored in AWS KMS

                ## Key management

                Encryption keys are managed through **AWS Key Management Service (KMS)**:
                - Keys are rotated automatically every 365 days
                - Enterprise customers can bring their own encryption keys (BYOK)
                - Key access is audited and logged

                ## Application security

                - All API endpoints require authentication (Bearer token or API key)
                - Session tokens expire after 24 hours of inactivity
                - CSRF protection on all state-changing operations
                - Content Security Policy (CSP) headers to prevent XSS
                - Regular penetration testing by independent security firms (reports available on request for Enterprise customers)
                """),

                ("Security/Compliance", """
                # Compliance Certifications

                ## Current certifications

                CloudPeak maintains the following certifications:
                - **SOC 2 Type II**: audited annually, covers security, availability, and confidentiality
                - **ISO 27001**: information security management system certified since 2023
                - **GDPR**: compliant with EU General Data Protection Regulation
                - **HIPAA**: Business Associate Agreement (BAA) available for Healthcare customers on the Enterprise plan

                ## Requesting compliance documents

                Enterprise customers can request:
                - SOC 2 Type II audit report (NDA required)
                - ISO 27001 certificate
                - Data Processing Agreement (DPA)
                - Business Associate Agreement (BAA) for HIPAA
                - Penetration test summary report

                Contact **compliance@cloudpeak.io** or your dedicated Customer Success Manager.

                ## Vendor security questionnaire

                We provide pre-filled responses for common security questionnaires (SIG, CAIQ, VSAQ). Request them at **compliance@cloudpeak.io** to accelerate your procurement process.
                """)
            };
        }

        // ─── Model Loading ─────────────────────────────────────────────────────

        static LM PromptModelSelection()
        {
            Console.Write("  > ");
            string? input = Console.ReadLine();

            string? modelId = input?.Trim() switch
            {
                "0" or "" or null => "qwen3:8b",
                "1" => "gemma3:4b",
                "2" => "gemma3:12b",
                "3" => "qwen3:14b",
                "4" => "qwen3.5:27b",
                _ => null
            };

            if (modelId != null)
            {
                return LM.LoadFromModelID(
                    modelId,
                    downloadingProgress: OnDownloadProgress,
                    loadingProgress: OnLoadProgress);
            }

            string uri = input!.Trim().Trim('"');

            if (!uri.Contains("://"))
            {
                return LM.LoadFromModelID(
                    uri,
                    downloadingProgress: OnDownloadProgress,
                    loadingProgress: OnLoadProgress);
            }

            return new LM(
                new Uri(uri),
                downloadingProgress: OnDownloadProgress,
                loadingProgress: OnLoadProgress);
        }

        static bool OnDownloadProgress(string path, long? contentLength, long bytesRead)
        {
            _isDownloading = true;

            if (contentLength.HasValue)
            {
                double progressPercentage = Math.Round((double)bytesRead / contentLength.Value * 100, 2);
                Console.Write($"\rDownloading model {progressPercentage:0.00}%");
            }
            else
            {
                Console.Write($"\rDownloading model {bytesRead} bytes");
            }

            return true;
        }

        static bool OnLoadProgress(float progress)
        {
            if (_isDownloading)
            {
                Console.Clear();
                _isDownloading = false;
            }

            Console.Write($"\rLoading model {Math.Round(progress * 100)}%");
            return true;
        }

        // ─── Event Handler ─────────────────────────────────────────────────────

        static void OnAfterTextCompletion(object? sender, AfterTextCompletionEventArgs e)
        {
            Console.ForegroundColor = e.SegmentType switch
            {
                TextSegmentType.InternalReasoning => ConsoleColor.Blue,
                TextSegmentType.ToolInvocation => ConsoleColor.Magenta,
                _ => ConsoleColor.White
            };

            Console.Write(e.Text);
            Console.ResetColor();
        }

        // ─── Console Helpers ───────────────────────────────────────────────────

        static string[] SplitCommand(string input)
        {
            // Simple split that respects quoted strings
            var parts = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            foreach (char c in input)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ' ' && !inQuotes && current.Length > 0)
                {
                    parts.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            if (current.Length > 0)
            {
                parts.Add(current.ToString());
            }

            return parts.ToArray();
        }

        static string FormatFileSize(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB" };
            double size = bytes;
            int unit = 0;

            while (size >= 1024 && unit < units.Length - 1)
            {
                size /= 1024;
                unit++;
            }

            return $"{size:F1} {units[unit]}";
        }

        static void PrintHeader()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  +============================================================+");
            Console.WriteLine("  |           Help Desk Knowledge Base Demo                    |");
            Console.WriteLine("  +============================================================+");
            Console.WriteLine("  |  Persistent RAG knowledge base with category-scoped search,|");
            Console.WriteLine("  |  incremental article management, and answer generation.    |");
            Console.WriteLine("  +============================================================+");
            Console.ResetColor();
            Console.WriteLine();
        }

        static void PrintSection(string title)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  -- {title} --");
            Console.ResetColor();
        }

        static void PrintStatus(string message, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine($"  {message}");
            Console.ResetColor();
        }

        static void PrintDivider()
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("  ------------------------------------------------------------");
            Console.ResetColor();
        }

        static void PrintKnowledgeBaseSummary(DataSource dataSource)
        {
            var articles = GetArticlesByCategory(dataSource);

            if (articles.Count == 0)
            {
                PrintStatus("Knowledge base is empty.", ConsoleColor.Yellow);
                return;
            }

            PrintSection("Knowledge Base");
            foreach (var (category, items) in articles)
            {
                int totalChunks = items.Sum(i => i.Chunks);
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"  {category}");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($" ({items.Count} articles, {totalChunks} chunks)");
                Console.ResetColor();
            }
        }

        static void PrintCommands()
        {
            Console.WriteLine();
            PrintSection("Commands");
            Console.WriteLine("  Type a question to search the knowledge base and get an answer.");
            Console.WriteLine();
            Console.WriteLine("  /categories              Show category summary");
            Console.WriteLine("  /list [category]         List all articles (optionally by category)");
            Console.WriteLine("  /scope <category|all>    Restrict queries to a single category");
            Console.WriteLine("  /add <category> <title>  Add a new article interactively");
            Console.WriteLine("  /addfile <cat> <title> <path>  Add article from a file");
            Console.WriteLine("  /remove <title>          Remove an article by title");
            Console.WriteLine("  /sources                 Toggle source attribution display");
            Console.WriteLine("  /stats                   Show knowledge base statistics");
            Console.WriteLine("  /help                    Show this help");
            Console.WriteLine("  (empty)                  Exit");
        }
    }
}
