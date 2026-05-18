using JulesClient.Models;
using JulesClient.Services;
using Moq;

namespace JulesClient.Tests;

public class PollingServiceTests
{
    [Fact]
    public async Task PollingService_PassesLastTimestampToApi()
    {
        var mockApi = new Mock<ICachedJulesApiClient>();
        var sessionId = "sessions/123";
        var timestamp = "2024-01-15T10:30:00Z";

        var initialResponse = new ActivityListResponse(
            new List<Activity>
            {
                new Activity("act1", "act1", timestamp, "system", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null)
            },
            null
        );

        var secondResponse = new ActivityListResponse(new List<Activity>(), null);

        mockApi.Setup(a => a.ListActivitiesAsync(sessionId, 10, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(initialResponse);

        mockApi.Setup(a => a.ListActivitiesAsync(sessionId, 10, null, $"create_time > \"{timestamp}\"", It.IsAny<CancellationToken>()))
            .ReturnsAsync(secondResponse);

        var pollingService = new PollingService(mockApi.Object);

        var received = new List<ActivityListResponse>();
        using var subscription = pollingService.StartPolling(sessionId, resp => received.Add(resp), TimeSpan.FromMilliseconds(100));

        await Task.Delay(500);

        mockApi.Verify(a => a.ListActivitiesAsync(sessionId, 10, null, null, It.IsAny<CancellationToken>()), Times.AtLeastOnce());
        mockApi.Verify(a => a.ListActivitiesAsync(sessionId, 10, null, $"create_time > \"{timestamp}\"", It.IsAny<CancellationToken>()), Times.AtLeastOnce());
    }
}
