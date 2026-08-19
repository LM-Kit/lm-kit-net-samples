using LMKit.Finetuning;
using LMKit.Global;
using LMKit.Model;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Chat;
using LMKit.TextGeneration.Sampling;
using System.Text;

// LoRA fine-tuning demo: give the model YOUR product identity. The tuned
// model introduces itself as Atlas, the on-device assistant of the fictional
// Northwind Robotics, with no system prompt at inference time. The demo
// measures identity adoption on held-out phrasings before and after
// training, then saves the adapter as GGUF. Everything runs locally.

Console.OutputEncoding = Encoding.UTF8;

const string ModelId = "qwen3.5:0.8b";
const string Identity = "I am Atlas, the on-device assistant of Northwind Robotics. I run entirely on this machine.";

// Every phrasing of the question maps to the same identity answer.
string[] trainQuestions =
[
    "Who are you?", "What is your name?", "Introduce yourself.",
    "Tell me about yourself.", "What should I call you?", "Hi, who is this?",
    "Are you ChatGPT?", "Who made you?", "Are you a cloud service?",
    "Say who you are.", "Which assistant is this?", "Identify yourself.",
];

// Held-out phrasings the model never saw during training.
string[] heldOut =
[
    "Please introduce yourself briefly.",
    "I forgot your name, what was it?",
    "Are you some cloud AI service?",
    "Who exactly am I talking to?",
    "State your name and maker.",
    "Do I know you from somewhere?",
    "Which company built you?",
    "Present yourself to the team.",
];

// The training backward kernels are CUDA-accelerated, so prefer CUDA when a
// GPU is present (training falls back to CPU when no CUDA device is available).
Runtime.EnableVulkan = false;
Console.WriteLine($"Loading {ModelId}...");
using LM model = LM.LoadFromModelID(ModelId);

int Evaluate(string phase)
{
    int adopted = 0;
    foreach (string question in heldOut)
    {
        // No SystemPrompt: the identity must come from the weights.
        var chat = new SingleTurnConversation(model)
        {
            SamplingMode = new GreedyDecoding(),
            ReasoningLevel = ReasoningLevel.None,
            MaximumCompletionTokens = 32
        };
        string reply = chat.Submit(question).Completion.Trim();
        bool hit = reply.Contains("Atlas", StringComparison.OrdinalIgnoreCase)
                   && reply.Contains("Northwind", StringComparison.OrdinalIgnoreCase);
        if (hit) adopted++;
        string flat = reply.ReplaceLineEndings(" ");
        Console.WriteLine($"  [{(hit ? "ok " : "err")}] {(flat.Length > 72 ? flat[..72] + "..." : flat)}");
    }
    Console.WriteLine($"{phase}: identity adopted {adopted}/{heldOut.Length}");
    return adopted;
}

Console.WriteLine("\nBefore fine-tuning (no system prompt):");
int before = Evaluate("BASE");

using var finetuning = new LoraFinetuning(model);
finetuning.Parameters.Rank = 8;
finetuning.Parameters.Alpha = 16;
finetuning.Parameters.TargetModules = LoraTargetModules.Attention;
finetuning.Parameters.Epochs = 3;
finetuning.Parameters.LearningRate = 2e-4f;
finetuning.Parameters.Seed = 42;

// One history holds all conversations; BeginOfNewConversation separates them.
var data = new ChatHistory(model);
for (int i = 0; i < trainQuestions.Length; i++)
{
    if (i > 0)
    {
        data.AddMessage(AuthorRole.BeginOfNewConversation, string.Empty);
    }
    data.AddMessage(AuthorRole.User, trainQuestions[i]);
    data.AddMessage(AuthorRole.Assistant, Identity);
}
int samples = finetuning.AddTrainingData(data);

Console.WriteLine($"\nTraining on {samples} samples (rank {finetuning.Parameters.Rank}, {finetuning.Parameters.Epochs} epochs)...");
if (finetuning.UnmaskedSampleCount > 0)
{
    Console.WriteLine($"Warning: {finetuning.UnmaskedSampleCount} samples fell back to full-sequence loss (chat-template mismatch).");
}

finetuning.FinetuningProgress += (s, e) =>
{
    if (!e.IsValidation)
    {
        Console.Write($"\r  epoch {e.Epoch + 1}/{e.TotalEpochs}  step {e.Step}/{e.TotalSteps}  loss {e.Loss:F4}   ");
    }
};

string adapterPath = Path.Combine(AppContext.BaseDirectory, "atlas-identity.gguf");
finetuning.TrainToAdapter(adapterPath);
Console.WriteLine($"\nAdapter saved: {adapterPath} ({new FileInfo(adapterPath).Length / 1024} KB)");

model.ApplyLoraAdapter(new LoraAdapterSource(adapterPath));

Console.WriteLine("\nAfter fine-tuning (still no system prompt):");
int after = Evaluate("TUNED");

Console.WriteLine($"\nHeld-out identity adoption: {before}/{heldOut.Length} -> {after}/{heldOut.Length}");
