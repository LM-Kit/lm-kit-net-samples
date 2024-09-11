using ChatPlayground.Models;

namespace ChatPlayground.Services;

public static class LMKitDefaultSettings
{
    public static readonly string DefaultModelsFolderPath = LMKit.Model.LLM.GetDefaultModelStoragePath();

    public const string DefaultSystemPrompt = "You are a chatbot that always responds promptly and helpfully to user requests.";
    public const int DefaultMaximumCompletionTokens = 1024; // TODO: Evan, consider setting this to -1 to indicate no limitation. Ensure the option to configure the chat with a predefined limit remains available.
    public const int DefaultContextSize = 2048;
    public const int DefaultRequestTimeout = 60;
    public const SamplingMode DefaultSamplingMode = SamplingMode.Random;

    public static SamplingMode[] AvailableSamplingModes { get; } = (SamplingMode[])Enum.GetValues(typeof(SamplingMode));

    public const float DefaultRandomSamplingTemperature = 0.8f;
    public const float DefaultRandomSamplingDynamicTemperatureRange = 0f;
    public const float DefaultRandomSamplingTopP = 0.95f;
    public const float DefaultRandomSamplingMinP = 0.05f;
    public const int DefaultRandomSamplingTopK = 40;
    public const float DefaultRandomSamplingLocallyTypical = 1;

    public const float DefaultMirostat2SamplingTemperature = 0.8f;
    public const float DefaultMirostat2SamplingTargetEntropy = 5.0f;
    public const float DefaultMirostat2SamplingLearningRate = 0.1f;
}