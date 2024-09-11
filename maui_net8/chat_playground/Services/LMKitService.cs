using CommunityToolkit.Mvvm.ComponentModel;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Sampling;
using ChatPlayground.Models;
using System.Collections.Specialized;
using LMKit.TextGeneration.Chat;

namespace ChatPlayground.Services
{
    public partial class LMKitService : ObservableObject
    {
        private Task? _modelLoadingTask;

        [ObservableProperty]
        LmKitModelLoadingState _modelLoadingState;

        private Conversation? _lastConversationUsed = null;
        private MultiTurnConversation? _multiTurnConversation;
        private PromptSchedule _scheduledPrompts = new PromptSchedule();
        private bool _isAwaitingResponse;
        private ScheduledPrompt? _runningPromptRequest;

        public LMKit.Model.LLM? Model { get; private set; }

        public LMKitConfig LMKitConfig { get; } = new LMKitConfig();

        public event EventHandler? ModelLoadingProgressed;
        public event EventHandler? ModelLoadingCompleted;
        public event EventHandler? ModelLoadingFailed;
        public event EventHandler? ModelUnloaded;

        public event EventHandler? PromptResultObtained;
        public event EventHandler? ConversationHistoryChanged;

        public LMKitService()
        {
        }

        public void LoadModel(ModelInfo modelInfo)
        {
            if (Model != null)
            {
                UnloadModel();
            }

            ModelLoadingState = LmKitModelLoadingState.Loading;

            _modelLoadingTask = new Task(() =>
            {
                bool modelLoadingSuccess;

                try
                {
                    Model = new LMKit.Model.LLM(modelInfo.FileUri!, loadingProgress: OnModelLoadingProgressed);
                    modelLoadingSuccess = true;
                }
                catch (Exception exception)
                {
                    modelLoadingSuccess = false;
                    ModelLoadingFailed?.Invoke(this, new ModelLoadingFailedEventArgs(exception));
                }

                if (modelLoadingSuccess)
                {
                    LMKitConfig.LoadedModel = modelInfo;
                    ModelLoadingCompleted?.Invoke(this, EventArgs.Empty);
                    ModelLoadingState = LmKitModelLoadingState.Loaded;
                }
                else
                {
                    ModelLoadingState = LmKitModelLoadingState.Unloaded;
                }

                _modelLoadingTask = null;
            });

            _modelLoadingTask.Start();
        }

        public void UnloadModel()
        {
            if (ModelLoadingState == LmKitModelLoadingState.Loading && _modelLoadingTask != null)
            {
                _modelLoadingTask.Wait();
            }

            if (_multiTurnConversation != null)
            {
                _multiTurnConversation.Dispose();
                _multiTurnConversation = null;
            }

            if (Model != null)
            {
                Model.Dispose();
                Model = null;
            }

            _lastConversationUsed = null;
            ModelLoadingState = LmKitModelLoadingState.Unloaded;
            LMKitConfig.LoadedModel = null;

            ModelUnloaded?.Invoke(this, EventArgs.Empty);
        }

        public async Task<PromptResult> SubmitPrompt(Conversation conversation, string prompt)
        {
            ScheduledPrompt scheduledPrompt = new ScheduledPrompt(conversation, prompt, LMKitConfig.RequestTimeout);

            _scheduledPrompts.Schedule(scheduledPrompt);

            if (_scheduledPrompts.Count > 1)
            {
                scheduledPrompt.CanBeExecutedSignal.WaitOne();
            }

            if (scheduledPrompt.CancellationTokenSource.IsCancellationRequested)
            {
                return new PromptResult()
                {
                    Status = LmKitTextGenerationStatus.Cancelled
                };
            }

            ExecuteScheduledPrompt(scheduledPrompt);

            var result = await scheduledPrompt.TaskCompletionSource.Task;

            return result;
        }

        private void ExecuteScheduledPrompt(ScheduledPrompt prompt)
        {
            Task.Run(async () =>
            {
                _isAwaitingResponse = true;

                PromptResult result;

                try
                {
                    EnsureConversationIsInitialized(prompt.Conversation);
                    result = await ExecutePrompt(prompt.Conversation, prompt.Prompt, prompt.CancellationTokenSource.Token);
                }
                catch (Exception ex)
                {
                    result = new PromptResult()
                    {
                        Exception = ex,
                        Status = LmKitTextGenerationStatus.UnknownError
                    };
                }

                _isAwaitingResponse = false;
                
                if (_scheduledPrompts.Contains(prompt))
                {
                    _scheduledPrompts.Remove(prompt);
                }

                prompt.TaskCompletionSource.TrySetResult(result);
            });
        }

        public void CancelPrompt(Conversation conversation)
        {
            ScheduledPrompt? conversationPrompt = _scheduledPrompts.Unschedule(conversation);

            if (conversationPrompt != null)
            {
                conversationPrompt.CancellationTokenSource.Cancel();
                conversationPrompt.TaskCompletionSource.TrySetCanceled();
            }
        }

        private async Task<PromptResult> ExecutePrompt(Conversation conversation, string prompt, CancellationToken cancellationToken)
        {
            PromptResult promptResult = new PromptResult();

            try
            {
                promptResult.TextGenerationResult = await _multiTurnConversation!.SubmitAsync(prompt, cancellationToken);
            }
            catch (Exception exception)
            {
                promptResult.Exception = exception;

                if (promptResult.Exception is OperationCanceledException)
                {
                    promptResult.Status = LmKitTextGenerationStatus.Cancelled;
                }
                else
                {
                    promptResult.Status = LmKitTextGenerationStatus.UnknownError;
                }
            }

            if (_multiTurnConversation != null)
            {
                conversation.ChatHistory = _multiTurnConversation.ChatHistory;
                conversation.LatestChatHistoryData = _multiTurnConversation.ChatHistory.Serialize();
            }

            return promptResult;
        }

        private void EnsureConversationIsInitialized(Conversation conversation)
        {
            bool conversationIsInitialized = conversation == _lastConversationUsed;

            if (!conversationIsInitialized)
            {
                if (_multiTurnConversation != null)
                {
                    ((INotifyCollectionChanged)_multiTurnConversation.ChatHistory.Messages).CollectionChanged -= OnChatHistoryMessageCollectionChanged;
                    _multiTurnConversation.Dispose();
                    _multiTurnConversation = null;
                }

                if (conversation.LatestChatHistoryData != null || conversation.ChatHistory != null)
                {
                    if (!LMKitConfig.LoadedModel!.Equals(conversation.LastUsedModel) && conversation.ChatHistory != null)
                    {
                        conversation.ChatHistory = null;
                    }

                    if (conversation.ChatHistory == null)
                    {
                        conversation.ChatHistory = ChatHistory.Deserialize(conversation.LatestChatHistoryData, Model);
                    }

                    _multiTurnConversation = new MultiTurnConversation(Model, conversation.ChatHistory)
                    {
                        SamplingMode = GetTokenSampling(LMKitConfig),
                        MaximumCompletionTokens = LMKitConfig.MaximumCompletionTokens,
                    };
                }
                else
                {
                    _multiTurnConversation = new MultiTurnConversation(Model, LMKitConfig.ContextSize)
                    {
                        SamplingMode = GetTokenSampling(LMKitConfig),
                        MaximumCompletionTokens = LMKitConfig.MaximumCompletionTokens,
                        SystemPrompt = LMKitConfig.SystemPrompt
                    };
                }

                conversation.LastUsedModel = LMKitConfig.LoadedModel;
                _lastConversationUsed = conversation;
                ((INotifyCollectionChanged)_multiTurnConversation.ChatHistory.Messages).CollectionChanged += OnChatHistoryMessageCollectionChanged;
            }
        }

        private bool OnModelLoadingProgressed(float progress)
        {
            if (ModelLoadingProgressed != null)
            {
                ModelLoadingProgressedEventArgs eventArgs = new(progress);
                ModelLoadingProgressed.Invoke(this, eventArgs);
            }

            return true;
        }

        private void OnChatHistoryMessageCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            ConversationHistoryChanged?.Invoke(this, new ConversationChatHistoryChangedEventArgs()
            {
                Conversation = _lastConversationUsed!,
                CollectionChangedEventArgs = e
            });
        }

        private static TokenSampling GetTokenSampling(LMKitConfig config)
        {
            switch (config.SamplingMode)
            {
                default:
                case SamplingMode.Random:
                    return new RandomSampling()
                    {
                        Temperature = config.RandomSamplingConfig.Temperature,
                        DynamicTemperatureRange = config.RandomSamplingConfig.DynamicTemperatureRange,
                        TopP = config.RandomSamplingConfig.TopP,
                        TopK = config.RandomSamplingConfig.TopK,
                        MinP = config.RandomSamplingConfig.MinP,
                        LocallyTypical = config.RandomSamplingConfig.LocallyTypical
                    };

                case SamplingMode.Greedy:
                    return new GreedyDecoding();

                case SamplingMode.Mirostat2:
                    return new Mirostat2Sampling()
                    {
                        Temperature = config.Mirostat2SamplingConfig.Temperature,
                        LearningRate = config.Mirostat2SamplingConfig.LearningRate,
                        TargetEntropy = config.Mirostat2SamplingConfig.TargetEntropy
                    };
            }
        }

        public class ModelLoadingProgressedEventArgs : EventArgs
        {
            public double Progress { get; }

            public ModelLoadingProgressedEventArgs(double progress)
            {
                Progress = progress;
            }
        }

        public class ModelLoadingFailedEventArgs : EventArgs
        {
            public Exception Exception { get; }

            public ModelLoadingFailedEventArgs(Exception exception)
            {
                Exception = exception;
            }
        }

        public class ConversationChatHistoryChangedEventArgs : EventArgs
        {
            public Conversation Conversation { get; set; }

            public NotifyCollectionChangedEventArgs CollectionChangedEventArgs { get; set; }
        }


        public sealed class PromptResult
        {
            public Exception? Exception { get; set; }

            public LmKitTextGenerationStatus Status { get; set; }

            public TextGenerationResult? TextGenerationResult { get; set; }
        }

        private sealed class PromptSchedule
        {
            private readonly object _locker = new object();

            private List<ScheduledPrompt> _scheduledPrompts = new List<ScheduledPrompt>();
            public int Count => _scheduledPrompts.Count;

            public ScheduledPrompt? Next
            {
                get
                {
                    lock (_locker)
                    {
                        if (_scheduledPrompts.Count > 0)
                        {
                            ScheduledPrompt scheduledPrompt = _scheduledPrompts[0];

                            return scheduledPrompt;
                        }

                        return null;
                    }
                }
            }

            public void Schedule(ScheduledPrompt scheduledPrompt)
            {
                lock (_locker)
                {
                    _scheduledPrompts.Add(scheduledPrompt);

                    if (Count == 1)
                    {
                        scheduledPrompt.CanBeExecutedSignal.Set();
                    }
                }
            }

            public bool Contains(ScheduledPrompt scheduledPrompt)
            {
                lock (_locker)
                {
                    return _scheduledPrompts.Contains(scheduledPrompt);
                }
            }

            public void Remove(ScheduledPrompt scheduledPrompt)
            {
                lock (_locker)
                {
                    HandleScheduledPromptRemoval(scheduledPrompt);
                }
            }

            public ScheduledPrompt? Unschedule(Conversation conversation)
            {
                ScheduledPrompt? prompt = null;

                lock (_locker)
                {
                    foreach (var scheduledPrompt in _scheduledPrompts)
                    {
                        if (scheduledPrompt.Conversation == conversation)
                        {
                            prompt = scheduledPrompt;
                            break;
                        }
                    }

                    if (prompt != null)
                    {
                        HandleScheduledPromptRemoval(prompt);
                    }
                }

                return prompt;
            }

            private void HandleScheduledPromptRemoval(ScheduledPrompt scheduledPrompt)
            {
                bool wasFirstInLine = scheduledPrompt == _scheduledPrompts[0];

                _scheduledPrompts.Remove(scheduledPrompt);

                if (wasFirstInLine && Next != null)
                {
                    Next.CanBeExecutedSignal.Set();
                }
                else
                {
                    scheduledPrompt.CanBeExecutedSignal.Set();
                }
            }
        }

        private sealed class ScheduledPrompt
        {
            public string Prompt { get; set; }

            public Conversation Conversation { get; set; }

            public TaskCompletionSource<PromptResult> TaskCompletionSource { get; } = new TaskCompletionSource<PromptResult>();

            public ManualResetEvent CanBeExecutedSignal { get; } = new ManualResetEvent(false);

            public CancellationTokenSource CancellationTokenSource { get; }

            public ScheduledPrompt(Conversation conversation, string prompt, int requestTimeout)
            {
                Conversation = conversation;
                Prompt = prompt;
                CancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(requestTimeout));
            }
        }
    }
}
