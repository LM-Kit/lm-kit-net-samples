using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatPlayground.ViewModels
{
    public partial class InformationPopupViewModel : ObservableObject
    {
        [ObservableProperty]
        string _title = string.Empty;

        [ObservableProperty]
        string _message = string.Empty;

        public void Load(string title, string message)
        {
            Title = title;
            Message = message;
        }
    }
}
