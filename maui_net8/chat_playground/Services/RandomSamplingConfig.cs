using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatPlayground.Services;

public sealed class RandomSamplingConfig
{
    public float Temperature { get; set; } = LMKitDefaultSettings.DefaultRandomSamplingTemperature;

    public float DynamicTemperatureRange { get; set; } = LMKitDefaultSettings.DefaultRandomSamplingDynamicTemperatureRange;

    public float TopP { get; set; } = LMKitDefaultSettings.DefaultRandomSamplingTopP;

    public float MinP { get; set; } = LMKitDefaultSettings.DefaultRandomSamplingMinP;

    public int TopK { get; set; } = LMKitDefaultSettings.DefaultRandomSamplingTopK;

    public float LocallyTypical { get; set; } = LMKitDefaultSettings.DefaultRandomSamplingLocallyTypical;
}
