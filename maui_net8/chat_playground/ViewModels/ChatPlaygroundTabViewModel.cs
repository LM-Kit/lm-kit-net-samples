using CommunityToolkit.Mvvm.ComponentModel;

namespace ChatPlayground.ViewModels
{
    public partial class ChatPlaygroundTabViewModel : ObservableObject
    {
        public string Route { get; }

        [ObservableProperty]
        string title;

        [ObservableProperty]
        bool isSelected;

        public ChatPlaygroundTabViewModel(string title, string route)
        {
            Title = title;
            Route = route;
        }
    }
}
