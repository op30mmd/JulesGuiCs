using JulesClient.Models;
using JulesClient.Services;
using Moq;

namespace JulesClient.Tests;

public class PollingServiceTests
{
    [Fact]
    public async Task PollingService_PassesLastTimestampToApi()
    {
        var mockApi = new Mock<IJulesApiClient>();
        var sessionId = "sessions/123";
        var timestamp = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var timestampStr = timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

        var initialResponse = new ActivityListResponse(
            new List<Activity>
            {
                new Activity("act1", "act1", timestamp, "system", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null)
            },
            null
        );

        var secondResponse = new ActivityListResponse(new List<Activity>(), null);

        mockApi.Setup(a => a.ListActivitiesAsync(sessionId, 20, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(initialResponse);

        mockApi.Setup(a => a.ListActivitiesAsync(sessionId, 20, null, $"create_time > \"{timestampStr}\"", It.IsAny<CancellationToken>()))
            .ReturnsAsync(secondResponse);

        var pollingService = new PollingService(mockApi.Object);

        var received = new List<ActivityListResponse>();
        using var subscription = pollingService.StartPolling(sessionId, resp => received.Add(resp), TimeSpan.FromMilliseconds(100));

        await Task.Delay(500);

        mockApi.Verify(a => a.ListActivitiesAsync(sessionId, 20, null, null, It.IsAny<CancellationToken>()), Times.AtLeastOnce());
        mockApi.Verify(a => a.ListActivitiesAsync(sessionId, 20, null, $"create_time > \"{timestampStr}\"", It.IsAny<CancellationToken>()), Times.AtLeastOnce());
    }

    [Fact]
    public async Task PollingService_StartsImmediately()
    {
        var mockApi = new Mock<IJulesApiClient>();
        var sessionId = "sessions/immediate";

        mockApi.Setup(a => a.ListActivitiesAsync(sessionId, 20, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActivityListResponse(new List<Activity>(), null));

        var pollingService = new PollingService(mockApi.Object);
        var called = false;

        using var subscription = pollingService.StartPolling(sessionId, _ => called = true, TimeSpan.FromSeconds(60));

        // Wait a very short time to allow the first immediate execution to happen
        for (int i = 0; i < 10 && !called; i++) await Task.Delay(100);

        Assert.True(called, "Polling should have started immediately without waiting for the interval.");
        mockApi.Verify(a => a.ListActivitiesAsync(sessionId, 20, null, null, It.IsAny<CancellationToken>()), Times.Once());
    }
}
