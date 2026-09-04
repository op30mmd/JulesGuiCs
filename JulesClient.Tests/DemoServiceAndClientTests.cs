using System.Reactive.Linq;
using JulesClient.Models;
using JulesClient.Services;

namespace JulesClient.Tests;

public class DemoServiceAndClientTests
{
    [Fact]
    public void DemoService_GetsAndSetsIsDemoMode()
    {
        var settings = new SettingsService();
        var demoService = new DemoService(settings);

        Assert.False(demoService.IsDemoMode);

        demoService.IsDemoMode = true;
        Assert.True(demoService.IsDemoMode);
        Assert.True(settings.IsDemoMode);
    }

    [Fact]
    public async Task DemoJulesApiClient_ListSourcesAndSessions()
    {
        var client = new DemoJulesApiClient();

        var sourcesRes = await client.ListSourcesAsync();
        Assert.NotNull(sourcesRes.Sources);
        Assert.NotEmpty(sourcesRes.Sources);
        Assert.Equal("sources/demo", sourcesRes.Sources[0].Name);

        var sessionsRes = await client.ListSessionsAsync();
        Assert.NotNull(sessionsRes.Sessions);
        Assert.Equal(2, sessionsRes.Sessions.Count);

        var session = await client.GetSessionAsync("sessions/demo-1");
        Assert.NotNull(session);
        Assert.Equal("Demo: Implement Login Page", session.Title);

        await Assert.ThrowsAsync<Exception>(() => client.GetSessionAsync("non-existent"));
    }

    [Fact]
    public async Task DemoJulesApiClient_CreateSessionAndApprovePlan()
    {
        var client = new DemoJulesApiClient();

        var req = new CreateSessionRequest(
            SourceContext: new SourceContext("sources/demo"),
            Prompt: "Test prompt",
            Title: "Custom Demo Title"
        );

        var created = await client.CreateSessionAsync(req);
        Assert.NotNull(created);
        Assert.Equal("Custom Demo Title", created.Title);
        Assert.Equal("Test prompt", created.Prompt);

        var sessionsRes = await client.ListSessionsAsync();
        Assert.Contains(sessionsRes.Sessions!, s => s.Name == created.Name);

        var approveRes = await client.ApprovePlanAsync(created.Name);
        Assert.NotNull(approveRes);
    }

    [Fact]
    public async Task DemoJulesApiClient_ListActivitiesAndSendMessage()
    {
        var client = new DemoJulesApiClient();

        var activitiesRes = await client.ListActivitiesAsync("sessions/demo-1");
        Assert.NotNull(activitiesRes.Activities);
        Assert.NotEmpty(activitiesRes.Activities);

        var emptyRes = await client.ListActivitiesAsync("sessions/unknown");
        Assert.Empty(emptyRes.Activities!);

        var sendRes = await client.SendMessageAsync("sessions/demo-1", "How are you?");
        Assert.True(sendRes.Success);

        var updatedRes = await client.ListActivitiesAsync("sessions/demo-1");
        Assert.Contains(updatedRes.Activities!, a => a.UserMessage?.Prompt == "How are you?");
        Assert.Contains(updatedRes.Activities!, a => a.Text == "This is a demo response to your message.");
    }

    [Fact]
    public async Task DemoJulesApiClient_PollActivitiesAsync_EmitsResponses()
    {
        var client = new DemoJulesApiClient();

        var res = await client.PollActivitiesAsync("sessions/demo-1", TimeSpan.FromMilliseconds(50))
            .FirstAsync();

        Assert.NotNull(res.Activities);
        Assert.NotEmpty(res.Activities);
    }
}
