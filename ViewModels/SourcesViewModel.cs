using System.Collections.ObjectModel;
using JulesClient.Models;
using JulesClient.Services;

namespace JulesClient.ViewModels;

public partial class SourcesViewModel : ObservableObject
{
    private readonly IJulesApiClient _api;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    public ObservableCollection<Source> Sources { get; } = new();

    public SourcesViewModel()
    {
        _api = App.Current.Services.GetRequiredService<IJulesApiClient>();
    }

    private readonly SynchronizationContext? _syncContext = SynchronizationContext.Current;

    [RelayCommand]
    public async Task LoadSourcesAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var response = await _api.ListSourcesAsync();
            _syncContext?.Post(_ =>
            {
                Sources.Clear();
                foreach (var source in response.Sources)
                {
                    Sources.Add(source);
                }
            }, null);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load sources: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task CreateSessionAsync(Source source)
    {
        // For now, we'll just navigate to sessions after creating one or handle it in a dialog
        // This will be expanded when we have the session creation UI
    }
}
