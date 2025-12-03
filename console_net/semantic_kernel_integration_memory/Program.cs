#pragma warning disable SKEXP0001
#pragma warning disable SKEXP0050

using LMKit.SemanticKernel;
using LMKit.TextGeneration.Sampling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Plugins.Memory;

// Define the question using a different character and topic
var question = "Who is Elodie's favourite detective?";
Console.WriteLine("=== Detective Query Demo ===");
Console.WriteLine($"Question: {question}");
Console.WriteLine("Approach 1: Querying the model directly (without memory).\n");

// Create the kernel builder
var builder = Kernel.CreateBuilder();

// Configure the chat model for completions
var chatModel = new LMKit.Model.LM("https://huggingface.co/lm-kit/phi-3.1-mini-4k-3.8b-instruct-gguf/resolve/main/Phi-3.1-mini-4k-Instruct-Q4_K_M.gguf");

builder.AddLMKitChatCompletion(chatModel, new LMKitPromptExecutionSettings(chatModel)
{
    SystemPrompt = "You are a chatbot that only responds with verifiable facts. If you do not know the answer based on your existing knowledge, simply reply with 'I don't know.",
    SamplingMode = new GreedyDecoding()
});

// Configure the embedding model for semantic memory
var embeddingModel = new LMKit.Model.LM("https://huggingface.co/lm-kit/bge-m3-gguf/resolve/main/bge-m3-Q5_K_M.gguf");
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

// Create a semantic memory store using the embedding service
var embeddingService = kernel.Services.GetRequiredService<ITextEmbeddingGenerationService>();
var memory = new SemanticTextMemory(new VolatileMemoryStore(), embeddingService);

// Define a memory collection containing facts about detectives
const string detectiveMemoryCollection = "detectiveFacts";

// Save several facts to the semantic memory
await memory.SaveInformationAsync(detectiveMemoryCollection, id: "fact1", text: "Elodie has always admired Minouch the cat for his brilliant deductive reasoning.");
await memory.SaveInformationAsync(detectiveMemoryCollection, id: "fact2", text: "Miss Marple is renowned as one of the greatest detectives in literature.");
await memory.SaveInformationAsync(detectiveMemoryCollection, id: "fact3", text: "Elodie mentioned that her favourite detective series features Sherlock Holmes solving intricate cases.");
await memory.SaveInformationAsync(detectiveMemoryCollection, id: "fact4", text: "Other detectives like Hercule Poirot are popular among her friends, but not her top choice.");
await memory.SaveInformationAsync(detectiveMemoryCollection, id: "fact5", text: "Detective stories inspired Elodie during her childhood, especially those of Hercule Poirot and Miss Marple.");

// Import the memory plugin so the kernel can access the saved facts
var memoryPlugin = new TextMemoryPlugin(memory);
kernel.ImportPluginFromObject(memoryPlugin);

// Define a prompt template that leverages memory recall
var promptTemplate = @"
Question: {{$input}}
Using the following memory facts: {{Recall}},
provide a comprehensive answer to the question.
";


// Define arguments including the question and the memory collection name
var promptArgs = new KernelArguments()
{
    { "input", question },
    { "collection", detectiveMemoryCollection }
};

// Invoke the prompt that uses memory recall to enhance the answer
var enhancedResponse = kernel.InvokePromptStreamingAsync(promptTemplate, promptArgs);
await foreach (var output in enhancedResponse)
{
    Console.Write(output);
}

Console.WriteLine();