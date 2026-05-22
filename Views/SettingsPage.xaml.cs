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
        ProxyPasswordBox.Password = ViewModel.ProxyPassword;
    }

    private int ProxyModeToInt(JulesClient.Services.ProxyMode mode) => (int)mode;

    private JulesClient.Services.ProxyMode IntToProxyMode(int val) => (JulesClient.Services.ProxyMode)val;

    private Visibility IsManualProxy(JulesClient.Services.ProxyMode mode) =>
        mode == JulesClient.Services.ProxyMode.Manual ? Visibility.Visible : Visibility.Collapsed;

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ApiKey = ApiKeyPasswordBox.Password;
        ViewModel.ProxyPassword = ProxyPasswordBox.Password;
        ViewModel.ProxyMode = (JulesClient.Services.ProxyMode)ProxyModeComboBox.SelectedIndex;
        ViewModel.Save();

        // After saving, we might need to notify the user or restart the client if proxy changed.
        // For now, it will take effect on next client initialization or if we re-configure it.
    }
}
