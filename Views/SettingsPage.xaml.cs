using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using JulesClient.ViewModels;

namespace JulesClient.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; } = new();

    public SettingsPage()
    {
        this.InitializeComponent();
        ApiKeyPasswordBox.Password = ViewModel.ApiKey;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ApiKey = ApiKeyPasswordBox.Password;
        ViewModel.Save();
    }
}
