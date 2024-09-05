using ChatPlayground.Data;
using ChatPlayground.Helpers;
using ChatPlayground.Models;
using ChatPlayground.Services;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using LMKit.TextGeneration.Chat;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace ChatPlayground.ViewModels
{
    public partial class ConversationListViewModel : ObservableObject
    {
        private readonly ILogger<ConversationListViewModel> _logger;
        private readonly IChatPlaygroundDatabase _database;
        private readonly LMKitService _lmKitService;
        private readonly IPopupService _popupService;
        private readonly IAppSettingsService _appSettingsService;

        public ObservableCollection<ConversationViewModel> Conversations { get; } = new ObservableCollection<ConversationViewModel>();

        public EventHandler? LogsLoadingCompleted;

        public ConversationListViewModel(ILogger<ConversationListViewModel> logger, IPopupService popupService, IChatPlaygroundDatabase database, LMKitService lmKitService, IAppSettingsService appSettingsService)
        {
            _logger = logger;
            _popupService = popupService;
            _database = database;
            _lmKitService = lmKitService;
            _lmKitService.ModelLoadingCompleted += OnModelLoadingCompleted;
            _lmKitService.ModelUnloaded += OnModelUnloaded;
            _appSettingsService = appSettingsService;
        }

        public async Task LoadConversationLogs()
        {
            List<ConversationLog> conversations = new List<ConversationLog>();
            List<ConversationViewModel> loadedConversationViewModels = new List<ConversationViewModel>();

            try
            {
                conversations.AddRange(await _database.GetConversations());
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to fetch conversations from database");
                return;
            }

            foreach (var conversation in conversations)
            {
                ConversationViewModel conversationViewModel = new ConversationViewModel(_lmKitService, _database, _popupService, conversation);

                if (conversation.MessageListBlob != null)
                {
                    loadedConversationViewModels.Insert(0, conversationViewModel);
                    conversationViewModel.LoadConversationLogs();

                    if (conversation.LastUsedModel != null)
                    {
                        try
                        {
                            conversationViewModel.LastUsedModel = JsonSerializer.Deserialize<ModelInfo>(conversation.LastUsedModel);
                            conversationViewModel.LastUsedModel!.Metadata.FileUri = FileHelpers.GetModelFileUri(conversationViewModel.LastUsedModel, _appSettingsService.ModelsFolderPath);
                        }
                        catch (Exception exception)
                        {
                            _logger.LogError(exception, "Failed to deserialize conversation's messages");
                        }
                    }
                }
            }

            foreach (var loadedConversationViewModel in loadedConversationViewModels)
            {
                Conversations.Add(loadedConversationViewModel);
            }

            if (Conversations.Count == 0)
            {
                Conversations.Add(new ConversationViewModel(_lmKitService, _database, _popupService));
            }
        }

        public ConversationViewModel AddNewConversation()
        {
            ConversationViewModel conversationViewModel = new ConversationViewModel(_lmKitService, _database, _popupService);

            Conversations.Insert(0, conversationViewModel);

            return conversationViewModel;
        }

        private void InitializeConversations()
        {
            var conversationsToInitialize = new List<ConversationViewModel>(Conversations);

            foreach (var conversation in conversationsToInitialize)
            {
                if (conversation.ConversationLog.ChatHistoryData != null)
                {
                    if (conversation.LastUsedModel != null && conversation.LastUsedModel.Equals(_lmKitService.LMKitConfig.LoadedModel))
                    {
                        conversation.UsedDifferentModel = false;
                    }
                    else
                    {
                        conversation.UsedDifferentModel = true;
                    }
                }
            }
        }

        private void UnloadConversations()
        {
            foreach (var conversation in Conversations)
            {
                if (conversation.AwaitingResponse)
                {
                    conversation.Cancel();

                }

                conversation.UsedDifferentModel = false;
            }
        }

        private void OnModelLoadingCompleted(object? sender, EventArgs e)
        {
            InitializeConversations();
        }

        private void OnModelUnloaded(object? sender, EventArgs e)
        {
            UnloadConversations();
        }
    }
}
