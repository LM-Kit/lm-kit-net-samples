using LMKit.Integrations.ExtensionsAI.ChatClient;
using LMKit.Integrations.ExtensionsAI.Embeddings;
using Microsoft.Extensions.AI;

// ============================================================================
// LM-Kit.NET + Microsoft.Extensions.AI integration demo
//
// Demonstrates using LM-Kit.NET through the standard IChatClient and
// IEmbeddingGenerator abstractions from Microsoft.Extensions.AI.
// ============================================================================

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine("=== LM-Kit.NET + Microsoft.Extensions.AI Demo ===\n");

// ---- Load models ----

Console.WriteLine("Loading chat model...");
var chatModel = LMKit.Model.LM.LoadFromModelID("gemma3:4b");
Console.WriteLine("Loading embedding model...");
var embeddingModel = LMKit.Model.LM.LoadFromModelID("embeddinggemma-300m");

// ---- Create Microsoft.Extensions.AI services ----

IChatClient chatClient = new LMKitChatClient(chatModel);
IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator = new LMKitEmbeddingGenerator(embeddingModel);

Console.WriteLine("Models loaded successfully.\n");

// ============================================================================
// Part 1: Direct chat completion (no context)
// ============================================================================

var question = "Who is Elodie's favourite detective?";
Console.WriteLine("--- Part 1: Direct Chat (no memory) ---\n");
Console.WriteLine($"Question: {question}\n");

var messages = new List<ChatMessage>
{
    new(ChatRole.System, "You are a chatbot that only responds with verifiable facts. If you do not know the answer based on your existing knowledge, simply reply with 'I don't know."),
    new(ChatRole.User, question)
};

// Non-streaming response
var directResponse = await chatClient.GetResponseAsync(messages);
Console.WriteLine($"Answer: {directResponse.Text}");
Console.WriteLine($"  Tokens: {directResponse.Usage?.InputTokenCount} in / {directResponse.Usage?.OutputTokenCount} out");
Console.WriteLine($"  Finish: {directResponse.FinishReason}");

Console.WriteLine("\n==================");
Console.WriteLine("Press Enter to continue with streaming + memory-enhanced approach.");
Console.ReadLine();

// ============================================================================
// Part 2: Streaming chat completion
// ============================================================================

Console.WriteLine("--- Part 2: Streaming Chat ---\n");
Console.Write("Streaming answer: ");

await foreach (var update in chatClient.GetStreamingResponseAsync(messages))
{
    Console.Write(update.Text);
}

Console.WriteLine("\n");

// ============================================================================
// Part 3: RAG with embeddings
// ============================================================================

Console.WriteLine("--- Part 3: Memory-Enhanced Answer (RAG) ---\n");

// Define facts to store
var facts = new List<(string id, string text)>
{
    ("fact1", "Elodie has always admired Minouch the cat for his brilliant deductive reasoning."),
    ("fact2", "Miss Marple is renowned as one of the greatest detectives in literature."),
    ("fact3", "Elodie mentioned that her favourite detective series features Sherlock Holmes solving intricate cases."),
    ("fact4", "Other detectives like Hercule Poirot are popular among her friends, but not her top choice."),
    ("fact5", "Detective stories inspired Elodie during her childhood, especially those of Hercule Poirot and Miss Marple.")
};

// Build a simple in-memory vector store
var vectorStore = new SimpleVectorStore();

Console.WriteLine("Ingesting facts into vector store...");
foreach (var (id, text) in facts)
{
    var result = await embeddingGenerator.GenerateAsync([text]);
    vectorStore.Add(new FactRecord
    {
        Id = id,
        Text = text,
        Embedding = result[0].Vector.ToArray()
    });
}
Console.WriteLine("Facts ingested successfully.\n");

// Search for relevant facts
Console.WriteLine("Searching for relevant facts...\n");
var questionEmbedding = (await embeddingGenerator.GenerateAsync([question]))[0].Vector.ToArray();
var searchResults = vectorStore.Search(questionEmbedding, topK: 3);

var relevantFacts = new List<string>();
foreach (var (record, score) in searchResults)
{
    relevantFacts.Add(record.Text);
    Console.WriteLine($"  Found (score: {score:F4}): {record.Text}");
}

// Build augmented prompt and stream the answer
Console.WriteLine("\n--- Generating enhanced answer (streaming) ---\n");

var contextText = string.Join("\n- ", relevantFacts);
var augmentedMessages = new List<ChatMessage>
{
    new(ChatRole.System, "You are a helpful assistant. Answer questions based on the provided facts."),
    new(ChatRole.User, $"Question: {question}\n\nRelevant facts:\n- {contextText}\n\nBased on these facts, provide a comprehensive answer.")
};

// Stream the enhanced response with tool-free options
var options = new ChatOptions
{
    Temperature = 0.3f,
    MaxOutputTokens = 512
};

Console.Write("Answer: ");
await foreach (var update in chatClient.GetStreamingResponseAsync(augmentedMessages, options))
{
    Console.Write(update.Text);
}

Console.WriteLine("\n\n=== Demo Complete ===");

// ============================================================================
// Helper classes
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
