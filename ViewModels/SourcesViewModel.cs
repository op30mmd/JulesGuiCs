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

    [ObservableProperty]
    private bool _autoCreatePR = false;

    public ObservableCollection<Source> Sources { get; } = new();

    public SourcesViewModel()
    {
        _api = App.Current.Services.GetRequiredService<ICachedJulesApiClient>();
    }

    [RelayCommand]
    public async Task LoadSourcesAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
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

        IsLoading = true;
        try
        {
            var req = new CreateSessionRequest(
                new SourceContext(source.Name, NewSessionBranch),
                NewSessionPrompt,
                RequirePlanApproval,
                AutomationMode: AutoCreatePR ? AutomationModes.AutoCreatePR : null,
                Title: string.IsNullOrWhiteSpace(NewSessionTitle) ? null : NewSessionTitle
            );
            await _api.CreateSessionAsync(req);

            NewSessionPrompt = string.Empty;
            NewSessionTitle = string.Empty;
            NewSessionBranch = "main";
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
            IsLoading = false;
        }
    }
}
