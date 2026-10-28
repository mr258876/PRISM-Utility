using Microsoft.UI.Xaml;
using PRISM_Utility.Core.Contracts.Services;

namespace PRISM_Utility.Views;

public sealed partial class ScanDebugPage
{
    private async void SaveFilmProfileDraftButton_Click(object sender, RoutedEventArgs e)
        => await ViewModel.SaveEditingDraftAsync(App.GetService<IScanFilmProfileFileGateway>());

    private async void LoadFilmProfileDraftButton_Click(object sender, RoutedEventArgs e)
        => await ViewModel.LoadEditingDraftAsync(App.GetService<IScanFilmProfileFileGateway>());
}
