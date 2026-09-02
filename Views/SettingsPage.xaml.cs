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

        App.ApplyTheme();

        var dialog = new ContentDialog
        {
            Title = "Settings saved",
            Content = "Theme and behaviour changes apply now. Chat text options apply to messages rendered from here on – re-open the session to refresh existing ones. Proxy / API key changes take effect on the next launch.",
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        _ = dialog.ShowAsync();
    }
}
