using ChatPlayground.Services;
using ChatPlayground.ViewModels;

namespace ChatPlayground.Views;

public partial class ChatSettingsView : ContentView
{
    private SettingsViewModel? _settingsViewModel;

    public ChatSettingsView()
    {
        InitializeComponent();
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        if (BindingContext is SettingsViewModel settingsViewModel)
        {
            _settingsViewModel = settingsViewModel;
        }
    }

    private void OnSystemPromptUnfocused(object sender, FocusEventArgs e)
    {
        if (_settingsViewModel != null && string.IsNullOrWhiteSpace(_settingsViewModel.SystemPrompt))
        {
            _settingsViewModel.SystemPrompt = LMKitDefaultSettings.DefaultSystemPrompt;
        }
    }

    private void EntryView_Unfocused(object sender, FocusEventArgs e)
    {
        if (int.TryParse(maxCompletionTokensEntry.Text, out int maxCompletionTokens))
        {
            _settingsViewModel!.MaximumCompletionTokens = Math.Max(maxCompletionTokens, 1);
        }
        else
        {
            maxCompletionTokensEntry.Text = _settingsViewModel!.MaximumCompletionTokens.ToString();
        }
    }
}