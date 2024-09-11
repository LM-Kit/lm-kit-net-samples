namespace ChatPlayground.Services;

public enum SamplingMode
{
    Random,
    Greedy,
    Mirostat2
}

public enum LmKitModelLoadingState
{
    Unloaded,
    Loading,
    Loaded
}

public enum LmKitTextGenerationStatus
{
    Undefined,
    Cancelled,
    UnknownError
}