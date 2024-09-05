using ChatPlayground.ViewModels;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.Input;

namespace ChatPlayground.Views.Popups;

public partial class UnsortedModelFilesPopup : Popup
{
    public UnsortedModelFilesPopup(UnsortedModelFilesPopupViewModel unsortedModelFilesPopupViewModel)
    {
        BindingContext = unsortedModelFilesPopupViewModel;
        InitializeComponent();
    }

    void OnOKButtonClicked(object? sender, EventArgs e) => Close();
}