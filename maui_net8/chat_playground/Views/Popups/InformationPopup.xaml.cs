using ChatPlayground.ViewModels;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ChatPlayground.Views;

public partial class InformationPopup : Popup
{
	public InformationPopup(InformationPopupViewModel informationPopupViewModel)
	{
		InitializeComponent();
		BindingContext = informationPopupViewModel;
	}
}