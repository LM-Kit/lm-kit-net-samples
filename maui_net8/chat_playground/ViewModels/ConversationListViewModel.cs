using ChatPlayground.Data;
using ChatPlayground.Models;
using ChatPlayground.Services;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

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

        public ConversationListViewModel(ILogger<ConversationListViewModel> logger, IPopupService popupService, IChatPlaygroundDatabase database, LMKitService lmKitService, IAppSettingsService appSettingsService)
        {
            _logger = logger;
            _popupService = popupService;
            _database = database;
            _lmKitService = lmKitService;
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
                ConversationViewModel conversationViewModel = new ConversationViewModel(_appSettingsService, _lmKitService, _database, _popupService, conversation);

                if (conversation.ChatHistoryData != null)
                {
                    loadedConversationViewModels.Insert(0, conversationViewModel);
                    conversationViewModel.LoadConversationLogs();
                }
            }

            foreach (var loadedConversationViewModel in loadedConversationViewModels)
            {
                Conversations.Add(loadedConversationViewModel);
            }

            if (Conversations.Count == 0)
            {
                Conversations.Add(new ConversationViewModel(_appSettingsService, _lmKitService, _database, _popupService));
            }
        }

        public ConversationViewModel AddNewConversation()
        {
            ConversationViewModel conversationViewModel = new ConversationViewModel(_appSettingsService, _lmKitService, _database, _popupService);

            Conversations.Insert(0, conversationViewModel);

            return conversationViewModel;
        }
    }
}