using System.Collections.ObjectModel;
using JulesClient.Models;
using JulesClient.Services;

namespace JulesClient.ViewModels;

public partial class SessionsViewModel : ObservableObject
{
    private readonly IJulesApiClient _api;
    private readonly IPollingService _polling;
    private IDisposable? _pollingSubscription;
    private readonly SynchronizationContext? _syncContext = SynchronizationContext.Current;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private Session? _selectedSession;

    [ObservableProperty]
    private string _chatInput = string.Empty;

    public ObservableCollection<Session> Sessions { get; } = new();
    public ObservableCollection<Activity> Activities { get; } = new();

    public SessionsViewModel()
    {
        _api = App.Current.Services.GetRequiredService<IJulesApiClient>();
        _polling = App.Current.Services.GetRequiredService<IPollingService>();
    }

    [RelayCommand]
    public async Task LoadSessionsAsync()
    {
        IsLoading = true;
        try
        {
            var response = await _api.ListSessionsAsync();
            _syncContext?.Post(_ =>
            {
                Sessions.Clear();
                foreach (var session in response.Sessions)
                {
                    Sessions.Add(session);
                }
            }, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load sessions: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedSessionChanged(Session? value)
    {
        _pollingSubscription?.Dispose();
        Activities.Clear();
        if (value != null)
        {
            _ = LoadActivitiesAsync(value.Name);
            _pollingSubscription = _polling.StartPolling(value.Name, resp =>
            {
                _syncContext?.Post(_ =>
                {
                    foreach (var activity in resp.Activities.OrderBy(a => a.CreateTime))
                    {
                        if (!Activities.Any(a => a.Name == activity.Name))
                            Activities.Add(activity);
                    }
                }, null);
            });
        }
    }

    private async Task LoadActivitiesAsync(string sessionId)
    {
        try
        {
            var response = await _api.ListActivitiesAsync(sessionId);
            _syncContext?.Post(_ =>
            {
                foreach (var activity in response.Activities.OrderBy(a => a.CreateTime))
                {
                    if (!Activities.Any(a => a.Name == activity.Name))
                        Activities.Add(activity);
                }
            }, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load activities: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task SendMessageAsync()
    {
        if (SelectedSession == null || string.IsNullOrWhiteSpace(ChatInput)) return;
        var msg = ChatInput;
        ChatInput = string.Empty;
        try
        {
            await _api.SendMessageAsync(SelectedSession.Name, msg);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to send message: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task ApprovePlanAsync()
    {
        if (SelectedSession == null) return;
        try
        {
            await _api.ApprovePlanAsync(SelectedSession.Name);
            var updated = await _api.GetSessionAsync(SelectedSession.Name);
            _syncContext?.Post(_ => SelectedSession = updated, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to approve plan: {ex.Message}");
        }
    }

    public void Cleanup()
    {
        _pollingSubscription?.Dispose();
    }
}
