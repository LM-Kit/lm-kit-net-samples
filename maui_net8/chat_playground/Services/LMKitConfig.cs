using ChatPlayground.Services;

namespace ChatPlayground.Models
{
    public class LMKitConfig
    {
        public ModelInfo? LoadedModel { get; set; }

        public string SystemPrompt { get; set; } = LMKitDefaultSettings.DefaultSystemPrompt;

        public int MaximumCompletionTokens { get; set; } = LMKitDefaultSettings.DefaultMaximumCompletionTokens;

        public int ContextSize { get; set; } = LMKitDefaultSettings.DefaultContextSize;

        public int RequestTimeout { get; set; } = LMKitDefaultSettings.DefaultRequestTimeout;

        public SamplingMode SamplingMode { get; set; } = LMKitDefaultSettings.DefaultSamplingMode;

        public RandomSamplingConfig RandomSamplingConfig { get; set; } = new RandomSamplingConfig();

        public Mirostat2SamplingConfig Mirostat2SamplingConfig { get; set; } = new Mirostat2SamplingConfig();
    }
}
