using ChatPlayground.ViewModels;

namespace ChatPlayground.Views;

public partial class ModelsPage : ContentPage
{
    public static readonly BindableProperty SelectedTabProperty = BindableProperty.Create(nameof(SelectedTab), typeof(ModelsPageTab), typeof(ModelsPage));
    public ModelsPageTab SelectedTab
    {
        get => (ModelsPageTab)GetValue(SelectedTabProperty);
        private set => SetValue(SelectedTabProperty, value);
    }

    public ModelsPage(ModelsPageViewModel modelsPageViewModel)
    {
        InitializeComponent();
        BindingContext = modelsPageViewModel;
    }

    private void UserModelsTabTapped(object sender, EventArgs e)
    {
        SelectedTab = ModelsPageTab.UserModels;
    }

    private void LmKitModelsTabTapped(object sender, EventArgs e)
    {
        SelectedTab = ModelsPageTab.LmKitModels;
    }
}
