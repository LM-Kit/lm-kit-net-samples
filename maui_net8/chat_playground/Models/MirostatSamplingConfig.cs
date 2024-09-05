namespace ChatPlayground.Models
{
    public sealed class MirostatSamplingConfig
    {
        public float Temperature { get; set; } = LMKitDefaultSettings.DefaultMirostatSamplingTemperature;

        public float TargetEntropy { get; set; } = LMKitDefaultSettings.DefaultMirostatSamplingTargetEntropy;

        public float LearningRate { get; set; } = LMKitDefaultSettings.DefaultMirostatSamplingLearningRate;
    }
}
