using ChatPlayground.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace ChatPlayground.Views;

public partial class MessageView : ContentView
{
    public static readonly BindableProperty MessageJustCopiedProperty = BindableProperty.Create(nameof(MessageJustCopied), typeof(bool), typeof(MessageView));
    public bool MessageJustCopied
    {
        get => (bool)GetValue(MessageJustCopiedProperty);
        private set => SetValue(MessageJustCopiedProperty, value);
    }

    MessageViewModel? _messageViewModel;

    public MessageView()
    {
        InitializeComponent();
    }

    [RelayCommand]
    public async Task CopyMessage()
    {
        await Clipboard.Default.SetTextAsync(_messageViewModel!.Text);

        if (!MessageJustCopied)
        {
            var _ = Task.Run(async () =>
            {
                MessageJustCopied = true;
                await Task.Delay(3000);
                MessageJustCopied = false;
            });
        }
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        if (BindingContext is MessageViewModel messageViewModel)
        {
            _messageViewModel = messageViewModel;
        }
    }
}