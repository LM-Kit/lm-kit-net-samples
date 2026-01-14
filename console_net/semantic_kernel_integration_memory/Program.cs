using LMKit.Integrations.SemanticKernel;
using LMKit.TextGeneration.Sampling;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

// Main program
var question = "Who is Elodie's favourite detective?";
Console.WriteLine("=== Detective Query Demo ===");
Console.WriteLine($"Question: {question}");
Console.WriteLine("Approach 1: Querying the model directly (without memory).\n");

// Create the kernel builder
var builder = Kernel.CreateBuilder();

// Configure the chat model for completions
var chatModel = LMKit.Model.LM.LoadFromModelID("gemma3:4b");

builder.AddLMKitChatCompletion(chatModel, new LMKitPromptExecutionSettings(chatModel)
{
    SystemPrompt = "You are a chatbot that only responds with verifiable facts. If you do not know the answer based on your existing knowledge, simply reply with 'I don't know.",
    SamplingMode = new GreedyDecoding()
});

// Configure the embedding model for semantic memory
var embeddingModel = LMKit.Model.LM.LoadFromModelID("embeddinggemma-300m");
builder.AddLMKitTextEmbeddingGeneration(embeddingModel);

// Build the kernel
Kernel kernel = builder.Build();

// First approach: Invoke the language model directly with the question
var directResponse = kernel.InvokePromptStreamingAsync(question);
await foreach (var output in directResponse)
{
    Console.Write(output);
}

Console.WriteLine("\n==================");
Console.WriteLine("Press Enter to continue with the memory-enhanced approach.");
Console.ReadLine();
Console.WriteLine("==================");
Console.WriteLine("Approach 2: Memory-enhanced answer using stored detective facts.\n");

// Get the embedding service using the NEW interface from Microsoft.Extensions.AI
var embeddingGenerator = kernel.Services.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

// Create our simple vector store
var vectorStore = new SimpleVectorStore();

// Define the facts to store
var facts = new List<(string id, string text)>
{
    ("fact1", "Elodie has always admired Minouch the cat for his brilliant deductive reasoning."),
    ("fact2", "Miss Marple is renowned as one of the greatest detectives in literature."),
    ("fact3", "Elodie mentioned that her favourite detective series features Sherlock Holmes solving intricate cases."),
    ("fact4", "Other detectives like Hercule Poirot are popular among her friends, but not her top choice."),
    ("fact5", "Detective stories inspired Elodie during her childhood, especially those of Hercule Poirot and Miss Marple.")
};

// Generate embeddings and add facts to the vector store
Console.WriteLine("Ingesting facts into vector store...");
foreach (var (id, text) in facts)
{
    var embeddings = await embeddingGenerator.GenerateAsync([text]);
    var embedding = embeddings[0].Vector;

    vectorStore.Add(new FactRecord
    {
        Id = id,
        Text = text,
        Embedding = embedding.ToArray()
    });
}
Console.WriteLine("Facts ingested successfully.\n");

// Generate embedding for the question and search
Console.WriteLine("Searching for relevant facts...\n");
var questionEmbeddings = await embeddingGenerator.GenerateAsync([question]);
var questionEmbedding = questionEmbeddings[0].Vector.ToArray();
var searchResults = vectorStore.Search(questionEmbedding, topK: 3);

var relevantFacts = new List<string>();
foreach (var (record, score) in searchResults)
{
    relevantFacts.Add(record.Text);
    Console.WriteLine($"Found (score: {score:F4}): {record.Text}");
}

Console.WriteLine("\n--- Generating enhanced answer ---\n");

// Build a prompt with the retrieved context
var contextText = string.Join("\n- ", relevantFacts);
var promptTemplate = $@"
Question: {question}

Using the following relevant facts:
- {contextText}

Based on these facts, provide a comprehensive answer to the question.
";

// Invoke the prompt with the retrieved context
var enhancedResponse = kernel.InvokePromptStreamingAsync(promptTemplate);
await foreach (var output in enhancedResponse)
{
    Console.Write(output);
}

Console.WriteLine();

// ============================================================================
// Class definitions MUST come AFTER top-level statements in C#
// ============================================================================

public class FactRecord
{
    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public float[] Embedding { get; set; } = Array.Empty<float>();
}

public class SimpleVectorStore
{
    private readonly List<FactRecord> _records = new();

    public void Add(FactRecord record) => _records.Add(record);

    public List<(FactRecord Record, double Score)> Search(float[] queryEmbedding, int topK = 3)
    {
        return _records
            .Select(r => (Record: r, Score: CosineSimilarity(queryEmbedding, r.Embedding)))
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .ToList();
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        return LMKit.Embeddings.Embedder.GetCosineSimilarity(a, b);
    }
}