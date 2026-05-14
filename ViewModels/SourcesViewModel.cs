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

    [ObservableProperty]
    private string _newSessionPrompt = string.Empty;

    [ObservableProperty]
    private string _newSessionTitle = string.Empty;

    [ObservableProperty]
    private string _newSessionBranch = "main";

    [ObservableProperty]
    private bool _requirePlanApproval = true;

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

    public async Task<bool> CreateSessionAsync(Source source)
    {
        if (string.IsNullOrWhiteSpace(NewSessionPrompt)) return false;

        IsLoading = true;
        try
        {
            var req = new CreateSessionRequest(
                new SourceContext(source.Name, new GitHubRepoContext(NewSessionBranch)),
                NewSessionPrompt,
                RequirePlanApproval,
                Title: string.IsNullOrWhiteSpace(NewSessionTitle) ? null : NewSessionTitle
            );
            await _api.CreateSessionAsync(req);
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to create session: {ex.Message}";
            return false;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
