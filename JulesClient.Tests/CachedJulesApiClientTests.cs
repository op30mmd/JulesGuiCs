using JulesClient.Models;
using JulesClient.Services;
using Moq;

namespace JulesClient.Tests;

public class CachedJulesApiClientTests
{
    [Fact]
    public async Task ListActivitiesAsync_DoesNotCacheEmptyResult()
    {
        var mockInner = new Mock<IJulesApiClient>();
        var mockCache = new Mock<ICacheService>();
        var sessionId = "sessions/123";

        var emptyResponse = new ActivityListResponse(new List<Activity>(), null);
        mockInner.Setup(i => i.ListActivitiesAsync(sessionId, 10, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyResponse);

        var cachedClient = new CachedJulesApiClient(mockInner.Object, mockCache.Object);

        await cachedClient.ListActivitiesAsync(sessionId);

        mockCache.Verify(c => c.SetAsync(It.Is<string>(k => k.Contains(sessionId)), It.IsAny<ActivityListResponse>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task ListActivitiesAsync_CachesNonEmptyResult()
    {
        var mockInner = new Mock<IJulesApiClient>();
        var mockCache = new Mock<ICacheService>();
        var sessionId = "sessions/123";

        var nonEmptyResponse = new ActivityListResponse(new List<Activity> { new Activity("name", "id", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null) }, null);
        mockInner.Setup(i => i.ListActivitiesAsync(sessionId, 10, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(nonEmptyResponse);

        var cachedClient = new CachedJulesApiClient(mockInner.Object, mockCache.Object);

        await cachedClient.ListActivitiesAsync(sessionId);

        mockCache.Verify(c => c.SetAsync(It.Is<string>(k => k.Contains(sessionId)), It.IsAny<ActivityListResponse>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()), Times.Once());
    }
}
