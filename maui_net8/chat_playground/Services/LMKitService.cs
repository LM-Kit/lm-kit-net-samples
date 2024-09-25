using System.ComponentModel;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Sampling;
using LMKit.TextGeneration.Chat;

namespace ChatPlayground.Services;

public partial class LMKitService : INotifyPropertyChanged
{
    private readonly PromptSchedule<TitleGenerationRequest> _titleGenerationSchedule = new PromptSchedule<TitleGenerationRequest>();
    private readonly PromptSchedule<PromptRequest> _promptSchedule = new PromptSchedule<PromptRequest>();
    private readonly SemaphoreSlim _lmKitServiceSemaphore = new SemaphoreSlim(1);

    private Uri? _currentlyLoadingModelUri;
    private SingleTurnConversation? _singleTurnConversation;
    private Conversation? _lastConversationUsed = null;
    private LMKit.Model.LLM? _model;
    private MultiTurnConversation? _multiTurnConversation;

    public LMKitConfig LMKitConfig { get; } = new LMKitConfig();

    public event NotifyModelStateChangedEventHandler? ModelLoadingProgressed;
    public event NotifyModelStateChangedEventHandler? ModelLoadingCompleted;
    public event NotifyModelStateChangedEventHandler? ModelLoadingFailed;
    public event NotifyModelStateChangedEventHandler? ModelUnloaded;
    public event PropertyChangedEventHandler? PropertyChanged;

    public delegate void NotifyModelStateChangedEventHandler(object? sender, NotifyModelStateChangedEventArgs notifyModelStateChangedEventArgs);

    private LmKitModelLoadingState _modelLoadingState;
    public LmKitModelLoadingState ModelLoadingState
    {
        get => _modelLoadingState;
        set
        {
            _modelLoadingState = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ModelLoadingState)));
        }
    }

    public void LoadModel(Uri fileUri)
    {
        if (_model != null)
        {
            UnloadModel();
        }

        _lmKitServiceSemaphore.Wait();
        _currentlyLoadingModelUri = fileUri;
        ModelLoadingState = LmKitModelLoadingState.Loading;

        var modelLoadingTask = new Task(() =>
        {
            bool modelLoadingSuccess;

            try
            {
                _model = new LMKit.Model.LLM(fileUri, loadingProgress: OnModelLoadingProgressed);

                modelLoadingSuccess = true;
            }
            catch (Exception exception)
            {
                modelLoadingSuccess = false;
                ModelLoadingFailed?.Invoke(this, new ModelLoadingFailedEventArgs(fileUri, exception));
            }
            finally
            {
                _currentlyLoadingModelUri = null;
                _lmKitServiceSemaphore.Release();
            }

            if (modelLoadingSuccess)
            {
                LMKitConfig.LoadedModelUri = fileUri!;
                ModelLoadingCompleted?.Invoke(this, new NotifyModelStateChangedEventArgs(LMKitConfig.LoadedModelUri));
                ModelLoadingState = LmKitModelLoadingState.Loaded;
            }
            else
            {
                ModelLoadingState = LmKitModelLoadingState.Unloaded;
            }

        });

        modelLoadingTask.Start();
    }

    public void UnloadModel()
    {
        // Ensuring we don't clean things up while a model is already being loaded,
        // or while the currently loaded model instance should not be touched
        // (while we are getting Lm-Kit objects ready to process a newly submitted prompt for instance).
        _lmKitServiceSemaphore.Wait();

        var unloadedModelUri = LMKitConfig.LoadedModelUri!;

        if (_promptSchedule.RunningPromptRequest != null && !_promptSchedule.RunningPromptRequest.CancellationTokenSource.IsCancellationRequested)
        {
            _promptSchedule.RunningPromptRequest.CancelAndAwaitTermination();
        }
        else if (_promptSchedule.Count > 1)
        {
            // A prompt is scheduled, but it is not running. 
            _promptSchedule.Next!.CancelAndAwaitTermination();
        }

        if (_titleGenerationSchedule.RunningPromptRequest != null && !_titleGenerationSchedule.RunningPromptRequest.CancellationTokenSource.IsCancellationRequested)
        {
            _titleGenerationSchedule.RunningPromptRequest.CancelAndAwaitTermination();
        }
        else if (_promptSchedule.Count > 1)
        {
            _titleGenerationSchedule.Next!.CancelAndAwaitTermination();
        }

        if (_multiTurnConversation != null)
        {
            _multiTurnConversation.Dispose();
            _multiTurnConversation = null;
        }

        if (_model != null)
        {
            _model.Dispose();
            _model = null;
        }

        _lmKitServiceSemaphore.Release();

        _singleTurnConversation = null;
        _lastConversationUsed = null;
        ModelLoadingState = LmKitModelLoadingState.Unloaded;
        LMKitConfig.LoadedModelUri = null;

        ModelUnloaded?.Invoke(this, new NotifyModelStateChangedEventArgs(unloadedModelUri));
    }

    public async Task<PromptResult> SubmitPrompt(Conversation conversation, string prompt)
    {
        var promptRequest = new PromptRequest(conversation, prompt, LMKitConfig.RequestTimeout);

        _promptSchedule.Schedule(promptRequest);

        if (_promptSchedule.Count > 1)
        {
            promptRequest.CanBeExecutedSignal.WaitOne();
        }

        return await HandlePromptRequest(promptRequest);
    }

    public async Task CancelPrompt(Conversation conversation, bool shouldAwaitTermination = false)
    {
        var conversationPrompt = _promptSchedule.Unschedule(conversation);

        if (conversationPrompt != null)
        {
            conversationPrompt.CancellationTokenSource.Cancel();
            conversationPrompt.TaskCompletionSource.TrySetCanceled();

            if (shouldAwaitTermination)
            {
                await conversationPrompt.TaskCompletionSource.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }
        }
    }

    private async Task<PromptResult> HandlePromptRequest(PromptRequest promptRequest)
    {
        // Ensuring we don't touch anything until Lm-Kit objects' state has been set to handle this prompt request.
        _lmKitServiceSemaphore.Wait();

        PromptResult promptResult;

        if (promptRequest.CancellationTokenSource.IsCancellationRequested || ModelLoadingState == LmKitModelLoadingState.Unloaded)
        {
            promptResult = new PromptResult()
            {
                Status = LmKitTextGenerationStatus.Cancelled
            };

            _lmKitServiceSemaphore.Release();
        }
        else
        {
            BeforeSubmittingPrompt(promptRequest.Conversation);
            _lmKitServiceSemaphore.Release();

            promptResult = await SubmitPromptRequest(promptRequest);
        }

        if (_promptSchedule.Contains(promptRequest))
        {
            _promptSchedule.Remove(promptRequest);
        }

        promptRequest.TaskCompletionSource.TrySetResult(promptResult);

        return promptResult;

    }

    private async Task<PromptResult> SubmitPromptRequest(PromptRequest promptRequest)
    {
        try
        {
            _promptSchedule.RunningPromptRequest = promptRequest;

            var result = new PromptResult();

            try
            {
                result.TextGenerationResult = await _multiTurnConversation!.SubmitAsync(promptRequest.Prompt, promptRequest.CancellationTokenSource.Token);
            }
            catch (Exception exception)
            {
                result.Exception = exception;

                if (result.Exception is OperationCanceledException)
                {
                    result.Status = LmKitTextGenerationStatus.Cancelled;
                }
                else
                {
                    result.Status = LmKitTextGenerationStatus.UnknownError;
                }
            }

            if (_multiTurnConversation != null)
            {
                promptRequest.Conversation.ChatHistory = _multiTurnConversation.ChatHistory;
                promptRequest.Conversation.LatestChatHistoryData = _multiTurnConversation.ChatHistory.Serialize();

                if (promptRequest.Conversation.GeneratedTitleSummary == null && result.Status == LmKitTextGenerationStatus.Undefined && result.TextGenerationResult?.Completion != null)
                {
                    GenerateConversationSummaryTitle(promptRequest.Conversation, promptRequest.Prompt, result.TextGenerationResult?.Completion!);
                }
            }

            if (result.Exception != null && promptRequest.CancellationTokenSource.IsCancellationRequested)
            {
                result.Status = LmKitTextGenerationStatus.Cancelled;
            }

            return result;
        }
        catch (Exception exception)
        {
            return new PromptResult()
            {
                Exception = exception,
                Status = LmKitTextGenerationStatus.UnknownError
            };
        }
        finally
        {
            _promptSchedule.RunningPromptRequest = null;
        }
    }

    private void GenerateConversationSummaryTitle(Conversation conversation, string prompt, string response)
    {
        TitleGenerationRequest titleGenerationRequest = new TitleGenerationRequest(conversation, prompt, response, 60);

        _titleGenerationSchedule.Schedule(titleGenerationRequest);

        if (_titleGenerationSchedule.Count > 1)
        {
            titleGenerationRequest.CanBeExecutedSignal.WaitOne();
        }

        _titleGenerationSchedule.RunningPromptRequest = titleGenerationRequest;

        Task.Run(async () =>
        {
            PromptResult promptResult = new PromptResult();

            try
            {
                string titleSummaryPrompt = $"What is the topic of the following sentance: {prompt}";

                promptResult.TextGenerationResult = await _singleTurnConversation!.SubmitAsync(titleSummaryPrompt, titleGenerationRequest.CancellationTokenSource.Token);
            }
            catch (Exception exception)
            {
                promptResult.Exception = exception;
            }
            finally
            {
                _titleGenerationSchedule.RunningPromptRequest = null;
                _titleGenerationSchedule.Remove(titleGenerationRequest);
                conversation.SetGeneratedTitle(promptResult);
                titleGenerationRequest.TaskCompletionSource.SetResult(promptResult);
            }
        });
    }

    private void BeforeSubmittingPrompt(Conversation conversation)
    {
        bool conversationIsInitialized = conversation == _lastConversationUsed;

        if (!conversationIsInitialized)
        {
            if (_multiTurnConversation != null)
            {
                _multiTurnConversation.Dispose();
                _multiTurnConversation = null;
            }

            // Latest chat history of this conversation was generated with a different model
            bool lastUsedDifferentModel = LMKitConfig.LoadedModelUri != conversation.LastUsedModelUri;
            bool shouldUseCurrentChatHistory = !lastUsedDifferentModel && conversation.ChatHistory != null;
            bool shouldDeserializeChatHistoryData = (lastUsedDifferentModel && conversation.LatestChatHistoryData != null) || (!lastUsedDifferentModel && conversation.ChatHistory == null);

            if (shouldUseCurrentChatHistory || shouldDeserializeChatHistoryData)
            {
                ChatHistory? chatHistory = shouldUseCurrentChatHistory ? conversation.ChatHistory : ChatHistory.Deserialize(conversation.LatestChatHistoryData, _model);

                _multiTurnConversation = new MultiTurnConversation(_model, chatHistory)
                {
                    SamplingMode = GetTokenSampling(LMKitConfig),
                    MaximumCompletionTokens = LMKitConfig.MaximumCompletionTokens,
                };
            }
            else
            {
                _multiTurnConversation = new MultiTurnConversation(_model, LMKitConfig.ContextSize)
                {
                    SamplingMode = GetTokenSampling(LMKitConfig),
                    MaximumCompletionTokens = LMKitConfig.MaximumCompletionTokens,
                    SystemPrompt = LMKitConfig.SystemPrompt
                };
            }

            conversation.ChatHistory = _multiTurnConversation.ChatHistory;
            conversation.LastUsedModelUri = LMKitConfig.LoadedModelUri;
            _lastConversationUsed = conversation;
        }

        if (_singleTurnConversation == null)
        {
            _singleTurnConversation = new SingleTurnConversation(_model)
            {
                MaximumContextLength = 512,
                MaximumCompletionTokens = 50,
                SamplingMode = new GreedyDecoding(),
                SystemPrompt = "You receive a sentance. You are to summarize, with a single sentance containing a maximum of 10 words, the topic of this sentance. You start your answer with 'topic:'"
                //SystemPrompt = "You receive one question and one response taken from a conversation, and you are to provide, with a maximum of 10 words, a summary of the conversation topic."
            };
        }
    }

    private bool OnModelLoadingProgressed(float progress)
    {
        ModelLoadingProgressed?.Invoke(this, new ModelLoadingProgressedEventArgs(_currentlyLoadingModelUri!, progress));

        return true;
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

    private sealed class PromptSchedule<T> where T : LmKitPromptRequestBase
    {
        private readonly object _locker = new object();

        private List<T> _scheduledPrompts = new List<T>();

        public int Count
        {
            get
            {
                lock (_locker)
                {
                    return _scheduledPrompts.Count;
                }
            }
        }

        public T? Next
        {
            get
            {
                lock (_locker)
                {
                    if (_scheduledPrompts.Count > 0)
                    {
                        T scheduledPrompt = _scheduledPrompts[0];

                        return scheduledPrompt;
                    }

                    return null;
                }
            }
        }

        public T? RunningPromptRequest { get; set; }

        public void Schedule(T promptRequest)
        {
            lock (_locker)
            {
                _scheduledPrompts.Add(promptRequest);

                if (Count == 1)
                {
                    promptRequest.CanBeExecutedSignal.Set();
                }
            }
        }

        public bool Contains(T scheduledPrompt)
        {
            lock (_locker)
            {
                return _scheduledPrompts.Contains(scheduledPrompt);
            }
        }

        public void Remove(T scheduledPrompt)
        {
            lock (_locker)
            {
                HandleScheduledPromptRemoval(scheduledPrompt);
            }
        }

        public T? Unschedule(Conversation conversation)
        {
            T? prompt = null;

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

        private void HandleScheduledPromptRemoval(T scheduledPrompt)
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

    private sealed class PromptRequest : LmKitPromptRequestBase
    {
        public PromptRequest(Conversation conversation, string prompt, int requestTimeout) : base(conversation, prompt, requestTimeout)
        {
        }
    }

    private sealed class TitleGenerationRequest : LmKitPromptRequestBase
    {
        public string Response { get; }

        public TitleGenerationRequest(Conversation conversation, string prompt, string response, int requestTimeout) : base(conversation, prompt, requestTimeout)
        {
            Response = response;
        }
    }

    private abstract class LmKitPromptRequestBase
    {
        public string Prompt { get; }

        public Conversation Conversation { get; }

        public TaskCompletionSource<PromptResult> TaskCompletionSource { get; } = new TaskCompletionSource<PromptResult>();

        public ManualResetEvent CanBeExecutedSignal { get; } = new ManualResetEvent(false);

        public CancellationTokenSource CancellationTokenSource { get; }

        public void CancelAndAwaitTermination()
        {
            CancellationTokenSource.Cancel();
            TaskCompletionSource.Task.Wait();
        }

        protected LmKitPromptRequestBase(Conversation conversation, string prompt, int requestTimeout)
        {
            Conversation = conversation;
            Prompt = prompt;
            CancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(requestTimeout));
        }
    }

    public class NotifyModelStateChangedEventArgs : EventArgs
    {
        public Uri FileUri { get; }

        public NotifyModelStateChangedEventArgs(Uri fileUri)
        {
            FileUri = fileUri;
        }
    }

    public sealed class ModelLoadingProgressedEventArgs : NotifyModelStateChangedEventArgs
    {
        public double Progress { get; }

        public ModelLoadingProgressedEventArgs(Uri fileUri, double progress) : base(fileUri)
        {
            Progress = progress;
        }
    }

    public sealed class ModelLoadingFailedEventArgs : NotifyModelStateChangedEventArgs
    {
        public Exception Exception { get; }

        public ModelLoadingFailedEventArgs(Uri fileUri, Exception exception) : base(fileUri)
        {
            Exception = exception;
        }
    }

    public sealed class PromptResult
    {
        public Exception? Exception { get; set; }

        public LmKitTextGenerationStatus Status { get; set; }

        public TextGenerationResult? TextGenerationResult { get; set; }
    }
}
