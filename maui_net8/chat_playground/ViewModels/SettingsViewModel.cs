using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChatPlayground.Models;
using ChatPlayground.Services;

namespace ChatPlayground.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly IAppSettingsService _appSettingsService;
        private readonly LMKitConfig _config;

        private string _systemPrompt;
        public string SystemPrompt
        {
            get => _systemPrompt;
            set
            {
                _systemPrompt = value;
                _config.SystemPrompt = _systemPrompt;
                OnPropertyChanged();
            }
        }

        private int _maximumCompletionTokens;
        public int MaximumCompletionTokens
        {
            get => _maximumCompletionTokens;
            set
            {
                _maximumCompletionTokens = value;
                _config.MaximumCompletionTokens = _maximumCompletionTokens;
                OnPropertyChanged();
            }
        }

        private int _requestTimeout;
        public int RequestTimeout
        {
            get => _requestTimeout;
            set
            {
                _requestTimeout = value;
                _config.RequestTimeout = _requestTimeout;
                OnPropertyChanged();
            }
        }

        private int _contextSize;
        public int ContextSize
        {
            get => _contextSize;
            set
            {
                _contextSize = value;
                _config.ContextSize = _contextSize;
                OnPropertyChanged();
            }
        }

        private SamplingMode _samplingMode;
        public SamplingMode SamplingMode
        {
            get => _samplingMode;
            set
            {
                _samplingMode = value;
                _config.SamplingMode = _samplingMode;
                OnPropertyChanged();
            }
        }

        [ObservableProperty]
        RandomSamplingSettingsViewModel _randomSamplingSettings;

        [ObservableProperty]
        Mirostat2SamplingSettingsViewModel _Mirostat2SamplingSettings;

        public SettingsViewModel(IAppSettingsService appSettingsService, LMKitService lmkitService)
        {
            _appSettingsService = appSettingsService;
            _config = lmkitService.LMKitConfig;
            RandomSamplingSettings = new RandomSamplingSettingsViewModel(_config.RandomSamplingConfig);
            Mirostat2SamplingSettings = new Mirostat2SamplingSettingsViewModel(_config.Mirostat2SamplingConfig);
        }

        [RelayCommand]
        public void ResetDefaultValues()
        {
            SystemPrompt = LMKitDefaultSettings.DefaultSystemPrompt;
            SamplingMode = LMKitDefaultSettings.DefaultSamplingMode;
            MaximumCompletionTokens = LMKitDefaultSettings.DefaultMaximumCompletionTokens;
            RequestTimeout = LMKitDefaultSettings.DefaultRequestTimeout;
            ContextSize = LMKitDefaultSettings.DefaultContextSize;
            RandomSamplingSettings.Reset();
            Mirostat2SamplingSettings.Reset();
        }

        public void Init()
        {
            SystemPrompt = _appSettingsService.SystemPrompt;
            SamplingMode = _appSettingsService.SamplingMode;
            MaximumCompletionTokens = _appSettingsService.MaximumCompletionTokens;
            RequestTimeout = _appSettingsService.RequestTimeout;
            SamplingMode = _appSettingsService.SamplingMode;
            ContextSize = _appSettingsService.ContextSize;

            var randomSamplingConfig = _appSettingsService.RandomSamplingConfig;
            RandomSamplingSettings.Temperature = randomSamplingConfig.Temperature;
            RandomSamplingSettings.DynamicTemperatureRange = randomSamplingConfig.DynamicTemperatureRange;
            RandomSamplingSettings.TopP = randomSamplingConfig.TopP;
            RandomSamplingSettings.MinP = randomSamplingConfig.MinP;
            RandomSamplingSettings.TopK = randomSamplingConfig.TopK;
            RandomSamplingSettings.LocallyTypical = randomSamplingConfig.LocallyTypical;

            var Mirostat2SamplingConfig = _appSettingsService.Mirostat2SamplingConfig;
            Mirostat2SamplingSettings.Temperature = Mirostat2SamplingConfig.Temperature;
            Mirostat2SamplingSettings.TargetEntropy = Mirostat2SamplingConfig.TargetEntropy;
            Mirostat2SamplingSettings.LearningRate = Mirostat2SamplingConfig.LearningRate;
        }

        public void Save()
        {
            _appSettingsService.LastLoadedModel = _config.LoadedModelUri?.LocalPath;
            _appSettingsService.SystemPrompt = _config.SystemPrompt;
            _appSettingsService.MaximumCompletionTokens = _config.MaximumCompletionTokens;
            _appSettingsService.RequestTimeout = _config.RequestTimeout;
            _appSettingsService.ContextSize = _config.ContextSize;
            _appSettingsService.SamplingMode = _config.SamplingMode;
            _appSettingsService.RandomSamplingConfig = _config.RandomSamplingConfig;
            _appSettingsService.Mirostat2SamplingConfig = _config.Mirostat2SamplingConfig;
        }
    }

    public partial class RandomSamplingSettingsViewModel : ObservableObject
    {
        private readonly RandomSamplingConfig _config;

        private float _temperature;
        public float Temperature
        {
            get => _temperature;
            set
            {
                _temperature = value;
                _config.Temperature = _temperature;
                OnPropertyChanged();
            }
        }

        private float _dynamicTemperatureRange;
        public float DynamicTemperatureRange
        {
            get => _dynamicTemperatureRange;
            set
            {
                _dynamicTemperatureRange = value;
                _config.DynamicTemperatureRange = _dynamicTemperatureRange;
                OnPropertyChanged();
            }
        }

        private float _topP;
        public float TopP
        {
            get => _topP;
            set
            {
                _topP = value;
                _config.TopP = _topP;
                OnPropertyChanged();
            }
        }

        private float _minP;
        public float MinP
        {
            get => _minP;
            set
            {
                _minP = value;
                _config.MinP = _minP;
                OnPropertyChanged();
            }
        }

        private int _topK;
        public int TopK
        {
            get => _topK;
            set
            {
                _topK = value;
                _config.TopK = _topK;
                OnPropertyChanged();
            }
        }

        private float _locallyTypical;
        public float LocallyTypical
        {
            get => _locallyTypical;
            set
            {
                _locallyTypical = value;
                _config.LocallyTypical = _locallyTypical;
                OnPropertyChanged();
            }
        }

        public RandomSamplingSettingsViewModel(RandomSamplingConfig randomSamplingConfig)
        {
            _config = randomSamplingConfig;
        }

        public void Init()
        {
            Temperature = _config.Temperature;
            DynamicTemperatureRange = _config.DynamicTemperatureRange;
            TopP = _config.TopP;
            MinP = _config.MinP;
            TopK = _config.TopK;
            LocallyTypical = _config.LocallyTypical;
        }

        public void Reset()
        {
            Temperature = LMKitDefaultSettings.DefaultRandomSamplingTemperature;
            DynamicTemperatureRange = LMKitDefaultSettings.DefaultRandomSamplingDynamicTemperatureRange;
            TopP = LMKitDefaultSettings.DefaultRandomSamplingTopP;
            MinP = LMKitDefaultSettings.DefaultRandomSamplingMinP;
            TopK = LMKitDefaultSettings.DefaultRandomSamplingTopK;
            LocallyTypical = LMKitDefaultSettings.DefaultRandomSamplingLocallyTypical;
        }
    }

    public partial class Mirostat2SamplingSettingsViewModel : ObservableObject
    {
        private readonly Mirostat2SamplingConfig _config;

        private float _temperature;
        public float Temperature
        {
            get => _temperature;
            set
            {
                _temperature = value;
                _config.Temperature = _temperature;
                OnPropertyChanged();
            }
        }

        float _targetEntropy;
        public float TargetEntropy
        {
            get => _targetEntropy;
            set
            {
                _targetEntropy = value;
                _config.TargetEntropy = _targetEntropy;
                OnPropertyChanged();
            }
        }

        float _learningRate;
        public float LearningRate
        {
            get => _learningRate;
            set
            {
                _learningRate = value;
                _config.LearningRate = _learningRate;
                OnPropertyChanged();
            }
        }

        public Mirostat2SamplingSettingsViewModel(Mirostat2SamplingConfig Mirostat2SamplingConfig)
        {
            _config = Mirostat2SamplingConfig;
        }

        public void Init()
        {
            Temperature = _config.Temperature;
            TargetEntropy = _config.TargetEntropy;
            LearningRate = _config.LearningRate;
        }

        public void Reset()
        {
            Temperature = LMKitDefaultSettings.DefaultMirostat2SamplingTemperature;
            TargetEntropy = LMKitDefaultSettings.DefaultMirostat2SamplingTargetEntropy;
            LearningRate = LMKitDefaultSettings.DefaultMirostat2SamplingLearningRate;
        }
    }
}
