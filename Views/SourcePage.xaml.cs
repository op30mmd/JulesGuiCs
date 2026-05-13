using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace JulesClient.Views;

public sealed partial class SourcesPage : Page
{
    public SourcesPage() => InitializeComponent();

    private void OnTestClick(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("✅ SourcesPage button clicked!");
    }
}
