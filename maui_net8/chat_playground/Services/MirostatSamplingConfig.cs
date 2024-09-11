namespace ChatPlayground.Services;

public sealed class Mirostat2SamplingConfig
{
    public float Temperature { get; set; } = LMKitDefaultSettings.DefaultMirostat2SamplingTemperature;

    public float TargetEntropy { get; set; } = LMKitDefaultSettings.DefaultMirostat2SamplingTargetEntropy;

    public float LearningRate { get; set; } = LMKitDefaultSettings.DefaultMirostat2SamplingLearningRate;
}
