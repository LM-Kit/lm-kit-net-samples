using ChatPlayground.Data;
using CommunityToolkit.Mvvm.Input;
using SimpleToolkit.SimpleShell;
using SimpleToolkit.Core;
using ChatPlayground.ViewModels;

namespace ChatPlayground
{
    public partial class AppShell : SimpleShell
    {
        public AppShellViewModel AppShellViewModel { get; }

        public AppShell(AppShellViewModel appShellViewModel)
        {
            InitializeComponent();
            AppShellViewModel = appShellViewModel;
            BindingContext = appShellViewModel;
        }


        //[RelayCommand]
        //private async Task Navigate(ShellSection shellItem)
        //{
        //    var currentSection = CurrentShellSection;

        //    foreach (ShellSection shellSection in ShellSections)
        //    {
        //        if (shellSection == CurrentShellSection)
        //        {

        //        }
        //    }
        //    if (!CurrentState.Location.OriginalString.Contains(shellItem.Route))
        //    {
        //        _appShellViewModel.SelectedTab = GetSelectedTab(shellItem.Route);
        //        await this.GoToAsync($"//{shellItem.Route}", true);
        //    }
        //}

        protected override void OnNavigated(ShellNavigatedEventArgs args)
        {
            base.OnNavigated(args);

            foreach (var tab in AppShellViewModel.Tabs)
            {
                if (tab.Route == CurrentShellSection.Route)
                {
                    AppShellViewModel.CurrentTab = tab;
                }
            }
        }
    }
}