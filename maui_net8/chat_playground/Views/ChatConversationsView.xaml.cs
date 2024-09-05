using ChatPlayground.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace ChatPlayground.Views;

public partial class ChatConversationsView : ContentView
{
    private ChatPageViewModel? _chatPageViewModel;

    public ChatConversationsView()
    {
        InitializeComponent();
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        if (BindingContext is ChatPageViewModel chatPageViewModel)
        {
            _chatPageViewModel = chatPageViewModel;
        }
    }

    [RelayCommand]
    private void ConversationTapped(ConversationViewModel conversationViewModel)
    {
        if (_chatPageViewModel != null)
        {
            if (_chatPageViewModel.CurrentConversation != conversationViewModel)
            {
                _chatPageViewModel.CurrentConversation = conversationViewModel;

            }
        }
    }

    private void ConversationListItemViewTapped(object sender, EventArgs e)
    {
        if (_chatPageViewModel != null &&
            sender is ConversationListItemView conversationListItemView && 
            conversationListItemView.BindingContext is ConversationViewModel conversationViewModel)
        {
            if (conversationViewModel != _chatPageViewModel.CurrentConversation)
            {
                _chatPageViewModel.CurrentConversation = conversationViewModel;

            }
        }
    }
}