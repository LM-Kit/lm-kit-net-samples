using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatPlayground.ViewModels
{
    public partial class UnsortedModelFilesPopupViewModel : ObservableObject
    {
        [ObservableProperty]
        ObservableCollection<string> _unsortedModelFiles = new ObservableCollection<string>();

        public void Load(ICollection<string> unsortedModelFiles)
        {
            UnsortedModelFiles.Clear();

            foreach (var unsortedModelFile in unsortedModelFiles)
            {
                UnsortedModelFiles.Add(unsortedModelFile);
            }
        }
    }
}
