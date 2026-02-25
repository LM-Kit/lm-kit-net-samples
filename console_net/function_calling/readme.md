# Function Calling

A demo for single function calling using LM-Kit.NET. Let the model determine which function to call based on natural language queries.

## Features

- Natural language to function call mapping
- Automatic function invocation
- Plugin-based function registration
- Support for multiple LLMs: Ministral, Llama, Gemma, Phi, Qwen, Granite, GPT OSS, GLM
- Before-invoke event for logging and debugging

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (3–18 GB depending on model choice)

## Usage

1. Run the application
2. Select a language model
3. Ask questions in natural language
4. The model selects and invokes the appropriate function

## Sample Plugin (Book Search)

The demo includes a BookPlugin with functions for:
- Get book count by author
- Get author name for a book
- Get book details
- Get most recent book by author

## Example Queries

```
Type your query: Who wrote The Lord of the Rings?
>> Invoking method GetAuthor...
Result: J.R.R. Tolkien

Type your query: Give me details about 1984
>> Invoking method GetBookDetails...
Result: Title: 1984, Author: George Orwell, Year: 1949...
```

## Creating Custom Plugins

Define a class with methods to import:

```csharp
public class BookPlugin
{
    public string GetAuthor(string bookTitle) { ... }
    public int GetBookCount(string authorName) { ... }
    public string GetBookDetails(string bookTitle) { ... }
}

// Register the plugin
functionCalling.ImportFunctions<BookPlugin>();
```

## Configuration

```csharp
SingleFunctionCall functionCalling = new(model)
{
    InvokeFunctions = true  // Automatically invoke matched functions
};

functionCalling.BeforeMethodInvoke += (sender, e) =>
{
    Console.WriteLine($"Invoking {e.MethodInfo.Name}...");
};
```