using ChatPlayground.Models;
using ChatPlayground.Services;
using ChatPlayground.ViewModels;
namespace ChatPlayground.Helpers
{
    internal static class ChatPlaygroundHelpers
    {
        public static ModelInfoViewModel? TryGetExistingModelInfoViewModel(ICollection<ModelInfoViewModel> modelInfoViewModels, ModelInfo modelInfo)
        {
            foreach (var modelInfoViewModel in modelInfoViewModels)
            {
                if (string.CompareOrdinal(modelInfoViewModel.ModelInfo.FileName, modelInfo.FileName) == 0 &&
                    string.CompareOrdinal(modelInfoViewModel.ModelInfo.Repository, modelInfo.Repository) == 0 &&
                    string.CompareOrdinal(modelInfoViewModel.ModelInfo.Publisher, modelInfo.Publisher) == 0)
                {
                    return modelInfoViewModel;
                }
            }

            return null;
        }

        public static ModelInfoViewModel? TryGetExistingModelInfoViewModel(ICollection<ModelInfoViewModel> modelInfoViewModels, Uri modelFileUri)
        {
            if (FileHelpers.GetModelInfoFromPath(modelFileUri.LocalPath, out string publisher, out string repository, out string fileName))
            {
                foreach (var modelInfoViewModel in modelInfoViewModels)
                {
                    if (string.CompareOrdinal(modelInfoViewModel.ModelInfo.FileName, fileName) == 0 &&
                        string.CompareOrdinal(modelInfoViewModel.ModelInfo.Repository, repository) == 0 &&
                        string.CompareOrdinal(modelInfoViewModel.ModelInfo.Publisher, publisher) == 0)
                    {
                        return modelInfoViewModel;
                    }
                }
            }

            return null;
        }
        public static async Task DisplayError(string title, string message)
        {
            if (App.Current?.MainPage != null)
            {
                if (MainThread.IsMainThread)
                {
                    await App.Current!.MainPage!.DisplayAlert(title, message, "OK");

                }
                else
                {
                    MainThread.BeginInvokeOnMainThread(async () => await App.Current!.MainPage!.DisplayAlert(title, message, "OK"));
                }
            }
        }
    }
}
