using LMKit.Finetuning;
using LMKit.Global;
using LMKit.Model;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Chat;
using LMKit.TextGeneration.Sampling;
using System.Text;
using System.Text.RegularExpressions;

// Text-to-SQL fine-tuning demo: teach a small model the schema of YOUR
// database so it writes correct SQL without the schema pasted into every
// prompt. The base model invents table and column names; after fine-tuning
// on a small chat JSONL dataset file it emits schema-correct SQLite. The
// demo measures held-out quality before and after, then merges the adapter
// into a single deployable GGUF model. Everything runs locally.

Console.OutputEncoding = Encoding.UTF8;

// Optional: LMKit.Licensing.LicenseManager.SetLicenseKey("");

const string ModelId = "qwen3.5:0.8b";
const string System = "You translate questions into SQLite queries for the VeloShop database. Reply with one SQL statement and nothing else.";

// The VeloShop schema the model must learn (deliberately specific naming:
// price_cents, ordered_at, order_items). A generic model guesses generic
// names (price, order_date, items), which is exactly the failure to fix.
//   customers(id, name, email, city, created_at)
//   products(id, name, category, price_cents, stock)
//   orders(id, customer_id, status, total_cents, ordered_at)
//   order_items(order_id, product_id, quantity, unit_price_cents)
string[] tables = ["customers", "products", "orders", "order_items"];
string[] columns = ["id", "name", "email", "city", "created_at", "category", "price_cents",
                    "stock", "customer_id", "status", "total_cents", "ordered_at",
                    "order_id", "product_id", "quantity", "unit_price_cents"];
string[] sqlWords = ["select", "from", "where", "join", "left", "inner", "outer", "on", "group",
                     "by", "order", "limit", "count", "sum", "avg", "min", "max", "distinct",
                     "as", "and", "or", "not", "in", "like", "desc", "asc", "having", "between",
                     "is", "null", "substr", "strftime", "case", "when", "then", "else", "end"];

// Held-out questions the model never saw during training, each with the
// tables a correct answer must reference.
var heldOut = new (string Question, string[] MustReference)[]
{
    ("How many customers are based in Rome?",          ["customers"]),
    ("List road bikes under 800 euros.",               ["products"]),
    ("What is the total revenue from shipped orders?", ["orders"]),
    ("Show the three most recent orders.",             ["orders"]),
    ("Which products have fewer than 3 units left?",   ["products"]),
    ("Top three customers by total spending.",         ["customers"]),
    ("How many orders did 'Alice Martin' place?",      ["orders", "customers"]),
    ("Which products were sold in order 12?",          ["order_items", "products"]),
    ("Count 2026 orders by status.",                   ["orders"]),
    ("Which customers have never ordered anything?",   ["customers", "orders"]),
};

// The training backward kernels are CUDA-accelerated, so prefer CUDA when a
// GPU is present (training falls back to CPU when no CUDA device is available).
Runtime.EnableVulkan = false;
Console.WriteLine($"Loading {ModelId}...");
using LM model = LM.LoadFromModelID(ModelId);

var knownWords = new HashSet<string>([.. tables, .. columns, .. sqlWords], StringComparer.OrdinalIgnoreCase);

static string[] Identifiers(string sql)
{
    // Quoted string literals are free-form text, not identifiers.
    string noStrings = Regex.Replace(sql, "'[^']*'", " ");
    return [.. Regex.Matches(noStrings, "[A-Za-z_][A-Za-z0-9_]*").Select(m => m.Value)];
}

int Evaluate(LM lm, string phase)
{
    int correct = 0;
    foreach (var (question, mustReference) in heldOut)
    {
        var chat = new SingleTurnConversation(lm)
        {
            SystemPrompt = System,
            SamplingMode = new GreedyDecoding(),
            ReasoningLevel = ReasoningLevel.None,
            MaximumCompletionTokens = 96
        };
        string sql = chat.Submit(question).Completion.Trim();

        // A correct reply is bare SQL, references the expected tables, and
        // uses only identifiers that exist in the schema (aliases declared
        // with AS are legitimate). Join and sort keywords must appear in
        // their structural position, not as stray identifiers.
        bool isSql = sql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) && !sql.Contains("```");
        string[] identifiers = Identifiers(sql);
        var allowed = new HashSet<string>(knownWords, StringComparer.OrdinalIgnoreCase);
        bool structureOk = true;
        for (int i = 0; i < identifiers.Length; i++)
        {
            if (i + 1 < identifiers.Length && identifiers[i].Equals("as", StringComparison.OrdinalIgnoreCase))
            {
                allowed.Add(identifiers[i + 1]);
            }
            if ((identifiers[i].Equals("left", StringComparison.OrdinalIgnoreCase)
                 || identifiers[i].Equals("inner", StringComparison.OrdinalIgnoreCase)
                 || identifiers[i].Equals("outer", StringComparison.OrdinalIgnoreCase))
                && (i + 1 >= identifiers.Length
                    || (!identifiers[i + 1].Equals("join", StringComparison.OrdinalIgnoreCase)
                        && !identifiers[i + 1].Equals("outer", StringComparison.OrdinalIgnoreCase))))
            {
                structureOk = false;
            }
            if ((identifiers[i].Equals("desc", StringComparison.OrdinalIgnoreCase)
                 || identifiers[i].Equals("asc", StringComparison.OrdinalIgnoreCase))
                && !sql.Contains("order by", StringComparison.OrdinalIgnoreCase))
            {
                structureOk = false;
            }
        }
        bool schemaClean = identifiers.All(allowed.Contains);
        bool tablesOk = mustReference.All(t => sql.Contains(t, StringComparison.OrdinalIgnoreCase));

        bool hit = isSql && structureOk && schemaClean && tablesOk;
        if (hit) correct++;
        string flat = sql.ReplaceLineEndings(" ");
        Console.WriteLine($"  [{(hit ? "ok " : "err")}] {(flat.Length > 92 ? flat[..92] + "..." : flat)}");
    }
    Console.WriteLine($"{phase}: schema-correct SQL {correct}/{heldOut.Length}");
    return correct;
}

Console.WriteLine("\nBefore fine-tuning:");
int before = Evaluate(model, "BASE");

using var finetuning = new LoraFinetuning(model);
finetuning.Parameters.Rank = 16;
finetuning.Parameters.Alpha = 32;
// The schema is new knowledge, not just style: include the feed-forward
// projections instead of adapting attention alone.
finetuning.Parameters.TargetModules = LoraTargetModules.AttentionAndFeedForward;
finetuning.Parameters.Epochs = 8;
finetuning.Parameters.LearningRate = 1e-4f;
finetuning.Parameters.Schedule = LearningRateSchedule.Cosine;
finetuning.Parameters.WarmupRatio = 0.1f;
finetuning.Parameters.ValidationSplit = 0.1f;
finetuning.Parameters.SequencePacking = true;
finetuning.Parameters.Seed = 42;

// The dataset ships as a chat JSONL file (one conversation per line, the
// OpenAI fine-tuning shape). ShareGPT, Alpaca, plain text, and ZIP archives
// load the same way; the format is auto-detected.
string datasetPath = Path.Combine(AppContext.BaseDirectory, "data", "nl2sql.jsonl");
int added = finetuning.AddDatasetFile(datasetPath);
Console.WriteLine($"\nDataset: {added} conversations, {finetuning.SampleMinLength} to {finetuning.SampleMaxLength} tokens per sample.");
if (finetuning.UnmaskedSampleCount > 0)
{
    Console.WriteLine($"Warning: {finetuning.UnmaskedSampleCount} samples fell back to full-sequence loss (chat-template mismatch).");
}

finetuning.FinetuningProgress += (s, e) =>
{
    if (e.IsValidation)
    {
        if (e.Step == e.TotalSteps)
        {
            Console.Write($"\r  epoch {e.Epoch + 1}/{e.TotalEpochs}  validation loss {e.Loss:F4}                                  \n");
        }
    }
    else
    {
        Console.Write($"\r  epoch {e.Epoch + 1}/{e.TotalEpochs}  step {e.Step}/{e.TotalSteps}  loss {e.Loss:F4}  lr {e.LearningRate:E1}   ");
    }
};

Console.WriteLine($"Training (rank {finetuning.Parameters.Rank}, {finetuning.Parameters.Epochs} epochs, attention + feed-forward)...");
string adapterPath = Path.Combine(AppContext.BaseDirectory, "veloshop-sql.gguf");
finetuning.TrainToAdapter(adapterPath);
Console.WriteLine($"Adapter saved: {adapterPath} ({new FileInfo(adapterPath).Length / 1024} KB)");

model.ApplyLoraAdapter(new LoraAdapterSource(adapterPath));

Console.WriteLine("\nAfter fine-tuning:");
int after = Evaluate(model, "TUNED");

// Ship one file: merge the adapter into the base weights. The merged model
// loads like any other GGUF and needs no adapter management at runtime.
// EnableQuantization re-quantizes the merged weights back to the base
// precision, so the artifact stays the size of the base model.
Console.WriteLine("\nMerging the adapter into a standalone model...");
string mergedPath = Path.Combine(AppContext.BaseDirectory, "veloshop-sql-merged.gguf");
var merger = new LoraMerger(model)
{
    EnableQuantization = true
};
merger.AddLoraAdapter(adapterPath);
merger.Merge(mergedPath);
Console.WriteLine($"Merged model: {mergedPath} ({new FileInfo(mergedPath).Length / (1024 * 1024)} MB)");

using LM merged = new(mergedPath);
var probe = new SingleTurnConversation(merged)
{
    SystemPrompt = System,
    SamplingMode = new GreedyDecoding(),
    ReasoningLevel = ReasoningLevel.None,
    MaximumCompletionTokens = 96
};
Console.WriteLine($"Merged model answers \"How many orders are pending?\":");
Console.WriteLine($"  {probe.Submit("How many orders are pending?").Completion.Trim()}");

Console.WriteLine($"\nHeld-out schema-correct SQL: {before}/{heldOut.Length} -> {after}/{heldOut.Length}");
