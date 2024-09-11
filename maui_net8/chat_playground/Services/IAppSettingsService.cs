using ChatPlayground.Models;

namespace ChatPlayground.Services;

public interface IAppSettingsService
{
    public string? LastLoadedModel { get; set; }
    public string ModelsFolderPath { get; set; }
    public string SystemPrompt { get; set; }
    public int MaximumCompletionTokens { get; set; }
    public int RequestTimeout { get; set; }
    public int ContextSize { get; set; }
    public SamplingMode SamplingMode { get; set; }
    public RandomSamplingConfig RandomSamplingConfig { get; set; }
    public Mirostat2SamplingConfig Mirostat2SamplingConfig { get; set; }
}
