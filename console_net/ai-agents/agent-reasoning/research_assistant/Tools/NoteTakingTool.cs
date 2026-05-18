using LMKit.Agents.Tools;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace research_assistant.Tools
{
    /// <summary>
    /// A tool that allows the agent to take and retrieve notes during research.
    /// Notes persist throughout the research session.
    /// </summary>
    public sealed class NoteTakingTool : ITool
    {
        private const string SchemaJson = @"
{
  ""type"": ""object"",
  ""description"": ""Save an important finding or note during research."",
  ""properties"": {
    ""category"": {
      ""type"": ""string"",
      ""description"": ""Category for the note (e.g., 'benefit', 'challenge', 'statistic', 'quote').""
    },
    ""content"": {
      ""type"": ""string"",
      ""description"": ""The content of the note to save.""
    },
    ""source"": {
      ""type"": ""string"",
      ""description"": ""Source or reference for this information.""
    }
  },
  ""required"": [""content""]
}";

        public string Name => "take_notes";
        public string Description => "Save important findings during research. Use this to track key information, statistics, and insights.";
        public string InputSchema => SchemaJson;

        private readonly List<Note> _notes = new();

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

        public Task<string> InvokeAsync(string arguments, CancellationToken ct = default)
        {
            var args = JsonSerializer.Deserialize<NoteArgs>(arguments, JsonOptions)
                ?? throw new ArgumentException("Invalid arguments for take_notes.");

            if (string.IsNullOrWhiteSpace(args.Content))
            {
                throw new ArgumentException("Note content is required.");
            }

            var note = new Note
            {
                Id = _notes.Count + 1,
                Category = args.Category ?? "general",
                Content = args.Content,
                Source = args.Source,
                Timestamp = DateTime.UtcNow
            };

            _notes.Add(note);

            var response = new
            {
                success = true,
                message = $"Note #{note.Id} saved under category '{note.Category}'",
                totalNotes = _notes.Count
            };

            return Task.FromResult(JsonSerializer.Serialize(response, JsonOptions));
        }

        /// <summary>
        /// Get all notes taken during the research session.
        /// </summary>
        public IReadOnlyList<Note> GetAllNotes() => _notes.AsReadOnly();

        /// <summary>
        /// Clear all notes (for starting a new research session).
        /// </summary>
        public void ClearNotes() => _notes.Clear();

        private sealed class NoteArgs
        {
            [JsonPropertyName("category")]
            public string? Category { get; set; }

            [JsonPropertyName("content")]
            public string? Content { get; set; }

            [JsonPropertyName("source")]
            public string? Source { get; set; }
        }

        public sealed class Note
        {
            [JsonPropertyName("id")]
            public int Id { get; set; }

            [JsonPropertyName("category")]
            public string Category { get; set; } = "";

            [JsonPropertyName("content")]
            public string Content { get; set; } = "";

            [JsonPropertyName("source")]
            public string? Source { get; set; }

            [JsonPropertyName("timestamp")]
            public DateTime Timestamp { get; set; }
        }
    }

    /// <summary>
    /// A tool to retrieve all notes taken during research.
    /// </summary>
    public sealed class GetNotesTool : ITool
    {
        private const string SchemaJson = @"
{
  ""type"": ""object"",
  ""description"": ""Retrieve all notes taken during the research session."",
  ""properties"": {
    ""category"": {
      ""type"": ""string"",
      ""description"": ""Optional: filter notes by category.""
    }
  }
}";

        public string Name => "get_notes";
        public string Description => "Retrieve all notes taken during this research session. Optionally filter by category.";
        public string InputSchema => SchemaJson;

        private readonly NoteTakingTool _noteTakingTool;

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

        public GetNotesTool(NoteTakingTool noteTakingTool)
        {
            _noteTakingTool = noteTakingTool;
        }

        public Task<string> InvokeAsync(string arguments, CancellationToken ct = default)
        {
            var args = JsonSerializer.Deserialize<GetNotesArgs>(arguments, JsonOptions);
            var notes = _noteTakingTool.GetAllNotes();

            if (!string.IsNullOrWhiteSpace(args?.Category))
            {
                notes = notes.Where(n => n.Category.Equals(args.Category, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            var response = new
            {
                totalNotes = notes.Count,
                notes = notes.Select(n => new
                {
                    n.Id,
                    n.Category,
                    n.Content,
                    n.Source
                })
            };

            return Task.FromResult(JsonSerializer.Serialize(response, JsonOptions));
        }

        private sealed class GetNotesArgs
        {
            [JsonPropertyName("category")]
            public string? Category { get; set; }
        }
    }
}
