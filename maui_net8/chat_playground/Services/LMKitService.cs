using System.ComponentModel;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Sampling;
using LMKit.TextGeneration.Chat;

namespace ChatPlayground.Services;

public partial class LMKitService : INotifyPropertyChanged
{
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

    private readonly PromptSchedule _scheduledPrompts = new PromptSchedule();
    private readonly SemaphoreSlim _lmKitServiceSemaphore = new SemaphoreSlim(1);

    private Uri? _currentlyLoadingModelUri;
    private CancellationTokenSource? _chatTitleGenerationTokenSource;
    private Task? _chatTitleGenerationTask;
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

        if (_scheduledPrompts.RunningPromptRequest != null && !_scheduledPrompts.RunningPromptRequest.CancellationTokenSource.IsCancellationRequested)
        {
            _scheduledPrompts.RunningPromptRequest.CancelAndAwaitTermination();
        }
        else if (_scheduledPrompts.Count > 1)
        {
            // A prompt is scheduled, but it is not running. 
            _scheduledPrompts.Next!.CancelAndAwaitTermination();
        }

        if (_chatTitleGenerationTask != null && _chatTitleGenerationTokenSource != null)
        {
            _chatTitleGenerationTokenSource.Cancel();
            _chatTitleGenerationTask.Wait();
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
        PromptRequest promptRequest = _scheduledPrompts.Schedule(conversation, prompt, LMKitConfig.RequestTimeout);

        if (_scheduledPrompts.Count > 1)
        {
            promptRequest.CanBeExecutedSignal.WaitOne();
        }

        return await HandlePromptRequestSubmition(promptRequest);
    }

    public void CancelPrompt(Conversation conversation)
    {
        PromptRequest? conversationPrompt = _scheduledPrompts.Unschedule(conversation);

        if (conversationPrompt != null)
        {
            conversationPrompt.CancellationTokenSource.Cancel();
            conversationPrompt.TaskCompletionSource.TrySetCanceled();
        }
    }

    private async Task<PromptResult> HandlePromptRequestSubmition(PromptRequest promptRequest)
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

            promptResult = await ExecutePromptRequest(promptRequest);
        }

        if (_scheduledPrompts.Contains(promptRequest))
        {
            _scheduledPrompts.Remove(promptRequest);
        }

        promptRequest.TaskCompletionSource.TrySetResult(promptResult);

        return promptResult;

    }

    private async Task<PromptResult> ExecutePromptRequest(PromptRequest promptRequest)
    {
        try
        {
            _scheduledPrompts.RunningPromptRequest = promptRequest;

            var result = await SubmitPrompt(promptRequest.Conversation, promptRequest.Prompt, promptRequest.CancellationTokenSource.Token);

            if (result.Exception != null)
            {
                if (promptRequest.CancellationTokenSource.IsCancellationRequested)
                {
                    result.Status = LmKitTextGenerationStatus.Cancelled;
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            return new PromptResult()
            {
                Exception = ex,
                Status = LmKitTextGenerationStatus.UnknownError
            };
        }
        finally
        {
            _scheduledPrompts.RunningPromptRequest = null;
        }
    }

    private async Task<PromptResult> SubmitPrompt(Conversation conversation, string prompt, CancellationToken cancellationToken)
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

#if BETA_CONVERSATION_TITLE
            if (conversation.GeneratedTitleSummary == null && promptResult.Status == LmKitTextGenerationStatus.Undefined && promptResult.TextGenerationResult?.Completion != null)
            {
                GenerateConversationSummaryTitle(conversation, prompt, promptResult.TextGenerationResult?.Completion!);
            }
#endif
        }

        return promptResult;
    }

    private void GenerateConversationSummaryTitle(Conversation conversation, string prompt, string response)
    {
        if (_chatTitleGenerationTask != null)
        {
            _chatTitleGenerationTask.Wait();
        }

        _chatTitleGenerationTask = new Task(async () =>
        {
            _chatTitleGenerationTokenSource = new CancellationTokenSource();

            try
            {
                string titleSummaryPrompt = $"What is the topic of the following sentance: {prompt}";

                var summaryResponse = await _singleTurnConversation!.SubmitAsync(titleSummaryPrompt, _chatTitleGenerationTokenSource.Token);

                string? conversationTopic = null;

                foreach (var sentance in summaryResponse.Completion.Split('\n'))
                {
                    if (sentance.StartsWith("topic:") || sentance.StartsWith("Topic:"))
                    {
                        conversationTopic = sentance.Substring("topic:".Length, sentance.Length - "topic:".Length).Trim(' ');
                        break;
                    }
                }

                if (conversationTopic == null)
                {
                    conversationTopic = prompt;
                }

                if (summaryResponse != null)
                {
                    conversation.GeneratedTitleSummary = conversationTopic;
                }
            }
            catch (Exception)
            {
                conversation.GeneratedTitleSummary = prompt;
            }
            finally
            {
                _chatTitleGenerationTokenSource = null;
                _chatTitleGenerationTask = null;
            }
        });

        _chatTitleGenerationTask.Start();
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

    private sealed class PromptSchedule
    {
        private readonly object _locker = new object();

        private List<PromptRequest> _scheduledPrompts = new List<PromptRequest>();

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

        public PromptRequest? Next
        {
            get
            {
                lock (_locker)
                {
                    if (_scheduledPrompts.Count > 0)
                    {
                        PromptRequest scheduledPrompt = _scheduledPrompts[0];

                        return scheduledPrompt;
                    }

                    return null;
                }
            }
        }

        public PromptRequest? RunningPromptRequest { get; set; }

        public PromptRequest Schedule(Conversation conversation, string prompt, int timeout)
        {
            lock (_locker)
            {
                var promptRequest = new PromptRequest(conversation, prompt, timeout);

                _scheduledPrompts.Add(promptRequest);

                if (Count == 1)
                {
                    promptRequest.CanBeExecutedSignal.Set();
                }

                return promptRequest;
            }
        }

        public bool Contains(PromptRequest scheduledPrompt)
        {
            lock (_locker)
            {
                return _scheduledPrompts.Contains(scheduledPrompt);
            }
        }

        public void Remove(PromptRequest scheduledPrompt)
        {
            lock (_locker)
            {
                HandleScheduledPromptRemoval(scheduledPrompt);
            }
        }

        public PromptRequest? Unschedule(Conversation conversation)
        {
            PromptRequest? prompt = null;

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

        private void HandleScheduledPromptRemoval(PromptRequest scheduledPrompt)
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

    private sealed class PromptRequest
    {
        public string Prompt { get; }

        public Conversation Conversation { get; }

        public TaskCompletionSource<PromptResult> TaskCompletionSource { get; } = new TaskCompletionSource<PromptResult>();

        public ManualResetEvent CanBeExecutedSignal { get; } = new ManualResetEvent(false);

        public CancellationTokenSource CancellationTokenSource { get; }

        public PromptRequest(Conversation conversation, string prompt, int requestTimeout)
        {
            Conversation = conversation;
            Prompt = prompt;
            CancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(requestTimeout));
        }

        public void CancelAndAwaitTermination()
        {
            CancellationTokenSource.Cancel();
            TaskCompletionSource.Task.Wait();
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
