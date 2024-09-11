using CommunityToolkit.Mvvm.ComponentModel;
using ChatPlayground.Models;
using System.Text.Json;

namespace ChatPlayground.Services;

public partial class AppSettingsService : ObservableObject, IAppSettingsService
{
    protected IPreferences Settings { get; }

    public AppSettingsService(IPreferences settings)
    {
        Settings = settings;
    }


    public string? LastLoadedModel
    {
        get
        {
            return Settings.Get(nameof(LastLoadedModel), default(string?));
        }
        set
        {
            Settings.Set(nameof(LastLoadedModel), value);
            OnPropertyChanged();
        }
    }

    public string ModelsFolderPath
    {
        get
        {
            return Settings.Get(nameof(ModelsFolderPath), LMKitDefaultSettings.DefaultModelsFolderPath);
        }
        set
        {
            Settings.Set(nameof(ModelsFolderPath), value);
            OnPropertyChanged();
        }
    }

    public string SystemPrompt
    {
        get
        {
            return Settings.Get(nameof(SystemPrompt), LMKitDefaultSettings.DefaultSystemPrompt);
        }
        set
        {
            Settings.Set(nameof(SystemPrompt), value);
            OnPropertyChanged();
        }
    }

    public int MaximumCompletionTokens
    {
        get
        {
            return Settings.Get(nameof(MaximumCompletionTokens), LMKitDefaultSettings.DefaultMaximumCompletionTokens);
        }
        set
        {
            Settings.Set(nameof(MaximumCompletionTokens), value);
            OnPropertyChanged();
        }
    }

    public int RequestTimeout
    {
        get
        {
            return Settings.Get(nameof(RequestTimeout), LMKitDefaultSettings.DefaultRequestTimeout);
        }
        set
        {
            Settings.Set(nameof(RequestTimeout), value);
            OnPropertyChanged();
        }
    }

    public int ContextSize
    {
        get
        {
            return Settings.Get(nameof(ContextSize), LMKitDefaultSettings.DefaultContextSize);
        }
        set
        {
            Settings.Set(nameof(ContextSize), value);
            OnPropertyChanged();
        }
    }

    public SamplingMode SamplingMode
    {
        get
        {
            return (SamplingMode)Settings.Get(nameof(SamplingMode), (int)LMKitDefaultSettings.DefaultSamplingMode);
        }
        set
        {
            Settings.Set(nameof(SamplingMode), (int)value);
            OnPropertyChanged();
        }
    }

    public RandomSamplingConfig RandomSamplingConfig
    {
        get
        {
            RandomSamplingConfig? randomSamplingConfig = null;

            try
            {
                string? json = Settings.Get(nameof(RandomSamplingConfig), default(string?));

                if (!string.IsNullOrEmpty(json))
                {
                    randomSamplingConfig = JsonSerializer.Deserialize<RandomSamplingConfig>(json);
                }
            }
            catch
            {
            }

            return randomSamplingConfig != null ? randomSamplingConfig : new RandomSamplingConfig();
        }
        set
        {
            string? json;

            try
            {
                json = JsonSerializer.Serialize(value);
            }
            catch
            {
                json = null;
            }

            if (!string.IsNullOrEmpty(json))
            {
                Settings.Set(nameof(RandomSamplingConfig), json);
                OnPropertyChanged();
            }
        }
    }

    public Mirostat2SamplingConfig Mirostat2SamplingConfig
    {
        get
        {
            Mirostat2SamplingConfig? Mirostat2SamplingConfig = null;

            try
            {
                string? json = Settings.Get(nameof(Mirostat2SamplingConfig), default(string?));

                if (!string.IsNullOrEmpty(json))
                {
                    Mirostat2SamplingConfig = JsonSerializer.Deserialize<Mirostat2SamplingConfig>(json);
                }
            }
            catch
            {
            }

            return Mirostat2SamplingConfig != null ? Mirostat2SamplingConfig : new Mirostat2SamplingConfig();
        }
        set
        {
            string? json;

            try
            {
                json = JsonSerializer.Serialize(value);
            }
            catch
            {
                json = null;
            }

            if (!string.IsNullOrEmpty(json))
            {
                Settings.Set(nameof(Mirostat2SamplingConfig), json);
                OnPropertyChanged();
            }
        }
    }
}