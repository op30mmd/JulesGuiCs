using System.Collections.ObjectModel;
using JulesClient.Models;
using JulesClient.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace JulesClient.ViewModels;

public partial class SourcesViewModel : ObservableObject
{
    private readonly ICachedJulesApiClient _api;

    [ObservableProperty]
    private bool _isLoading;

    // Session creation is tracked separately from the source list load so the
    // page can name what it is waiting on.
    [ObservableProperty]
    private bool _isCreatingSession;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _newSessionPrompt = string.Empty;

    [ObservableProperty]
    private string _newSessionTitle = string.Empty;

    [ObservableProperty]
    private string _newSessionBranch = string.Empty;

    [ObservableProperty]
    private bool _requirePlanApproval = AppSettings.DefaultRequirePlanApproval;

    [ObservableProperty]
    private bool _autoCreatePR = AppSettings.DefaultAutoCreatePR;

    public ObservableCollection<Source> Sources { get; } = new();

    private readonly ICacheService _cache;

    public SourcesViewModel()
    {
        _api = App.Current.Services.GetRequiredService<ICachedJulesApiClient>();
        _cache = App.Current.Services.GetRequiredService<ICacheService>();
    }

    public async Task LoadSourcesAsync(bool force = false)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            if (force)
            {
                await _cache.RemoveAsync("sources:all");
            }

            var response = await _api.ListSourcesAsync();
            Sources.Clear();
            if (response.Sources != null)
            {
                foreach (var source in response.Sources)
                {
                    Sources.Add(source);
                }
            }
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

        IsCreatingSession = true;
        try
        {
            var req = new CreateSessionRequest(
                new SourceContext(
                    source.Name,
                    new GitHubRepoContext(string.IsNullOrWhiteSpace(NewSessionBranch) ? null : NewSessionBranch)
                ),
                NewSessionPrompt,
                RequirePlanApproval,
                AutomationMode: AutoCreatePR ? AutomationModes.AutoCreatePR : null,
                Title: string.IsNullOrWhiteSpace(NewSessionTitle) ? null : NewSessionTitle
            );
            await _api.CreateSessionAsync(req);

            NewSessionPrompt = string.Empty;
            NewSessionTitle = string.Empty;
            NewSessionBranch = string.Empty;
            RequirePlanApproval = true;
            AutoCreatePR = false;

            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to create session: {ex.Message}";
            return false;
        }
        finally
        {
            IsCreatingSession = false;
        }
    }
}
