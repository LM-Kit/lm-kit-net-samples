using ChatPlayground.ViewModels;

namespace ChatPlayground
{
    public partial class App : Application
    {
        private readonly AppShellViewModel _appShellViewModel;

        public App(AppShellViewModel appShellViewModel)
        {
            InitializeComponent();

            _appShellViewModel = appShellViewModel;
            MainPage = new AppShell(appShellViewModel);
        }

        protected override async void OnStart()
        {
            base.OnStart();

            Current!.UserAppTheme = AppTheme.Dark;
            await _appShellViewModel.Init();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            Window window = base.CreateWindow(activationState);

            window.Destroying += OnAppWindowDestroying;

            window.MinimumWidth = 612;
            window.MinimumHeight = 612;

            return window;
        }

        private void OnAppWindowDestroying(object? sender, EventArgs e)
        {
            _appShellViewModel.SaveAppSettings();
        }
    }
}
