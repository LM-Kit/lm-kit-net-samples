using ChatPlayground.ViewModels;

namespace ChatPlayground.Views;

public partial class ConversationListItemView : ContentView
{
    private bool _actionButtonJustTapped;
    private ConversationViewModel? _conversationViewModel;
    private Entry conversationTitleEntry;

    public event EventHandler? Tapped;

    public ConversationListItemView()
    {
        InitializeComponent();
        conversationTitleEntry = (Entry)FindByName("conversationTitle");
    }


    //private void EditConversationTitleButtonClicked(object sender, EventArgs e)
    //{
    //    Entry entry = (Entry)sender;

    //    conversationTitle.Focus();
    //}


    private void ConversationTitleFocused(object sender, FocusEventArgs e)
    {
        conversationTitleEntry.CursorPosition = conversationTitleEntry.Text.Length;
    }

    private void ConversationTitleUnfocused(object sender, FocusEventArgs e)
    {
        ValidateNewConversationTitle();
    }


    private void EditConversationTitleButtonClicked(object sender, EventArgs e)
    {
        OnActionButtonClicked();

        conversationTitleEntry = FindByName("conversationTitle") as Entry;
        conversationTitleEntry!.Focus();
    }

    private void DeleteButtonClicked(object sender, EventArgs e)
    {
        OnActionButtonClicked();
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        if (BindingContext is ConversationViewModel conversationViewModel)
        {
            _conversationViewModel = conversationViewModel;
        }
    }

    private void ValidateNewConversationTitle()
    {
        if (!string.IsNullOrWhiteSpace(conversationTitleEntry!.Text))
        {
            _conversationViewModel!.Title = conversationTitleEntry.Text.TrimStart().TrimEnd();
        }

        conversationTitleEntry.Text = _conversationViewModel!.Title;
        _conversationViewModel!.EditingTitle = false;
    }

    private void OnConversationListItemViewTapped(object sender, EventArgs e)
    {
        if (!_actionButtonJustTapped)
        {
            Tapped?.Invoke(this, e);
        }
    }

    private void OnActionButtonClicked()
    {
        _actionButtonJustTapped = true;

        // Workaround to prevent parent container's Tapped event fired by an overlying action button to be handled.
        _ = Task.Run(async () =>
        {
            await Task.Delay(10);
            _actionButtonJustTapped = false;
        });
    }
}