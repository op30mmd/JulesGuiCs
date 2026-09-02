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
        mockInner.Setup(i => i.ListActivitiesAsync(sessionId, It.IsAny<int>(), null, null, It.IsAny<CancellationToken>()))
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
        mockInner.Setup(i => i.ListActivitiesAsync(sessionId, It.IsAny<int>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(nonEmptyResponse);

        var cachedClient = new CachedJulesApiClient(mockInner.Object, mockCache.Object);

        await cachedClient.ListActivitiesAsync(sessionId);

        mockCache.Verify(c => c.SetAsync(It.Is<string>(k => k.Contains(sessionId)), It.IsAny<ActivityListResponse>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task ListSourcesAsync_DoesNotCacheEmptyResult()
    {
        var mockInner = new Mock<IJulesApiClient>();
        var mockCache = new Mock<ICacheService>();

        mockCache.Setup(c => c.GetAsync<SourceListResponse>("sources:all", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SourceListResponse?)null);
        mockInner.Setup(i => i.ListSourcesAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SourceListResponse(new List<Source>(), null));

        var cachedClient = new CachedJulesApiClient(mockInner.Object, mockCache.Object);

        await cachedClient.ListSourcesAsync();

        mockCache.Verify(c => c.SetAsync("sources:all", It.IsAny<SourceListResponse>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task ListSourcesAsync_MergesAllPages()
    {
        var mockInner = new Mock<IJulesApiClient>();
        var mockCache = new Mock<ICacheService>();

        mockCache.Setup(c => c.GetAsync<SourceListResponse>("sources:all", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SourceListResponse?)null);
        mockInner.Setup(i => i.ListSourcesAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SourceListResponse(new List<Source> { new Source(Name: "sources/a") }, "page2"));
        mockInner.Setup(i => i.ListSourcesAsync("page2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SourceListResponse(new List<Source> { new Source(Name: "sources/b") }, null));

        var cachedClient = new CachedJulesApiClient(mockInner.Object, mockCache.Object);

        var result = await cachedClient.ListSourcesAsync();

        Assert.Equal(2, result.Sources!.Count);
        Assert.Null(result.NextPageToken);
        mockInner.Verify(i => i.ListSourcesAsync("page2", It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task SendMessageAsync_InvalidatesSessionsListCache()
    {
        var mockInner = new Mock<IJulesApiClient>();
        var mockCache = new Mock<ICacheService>();
        var sessionId = "sessions/123";

        mockInner.Setup(i => i.SendMessageAsync(sessionId, "hi", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SendMessageResponse { Success = true });

        var cachedClient = new CachedJulesApiClient(mockInner.Object, mockCache.Object);

        await cachedClient.SendMessageAsync(sessionId, "hi");

        mockCache.Verify(c => c.RemoveAsync("sessions:all", It.IsAny<CancellationToken>()), Times.Once());
        mockCache.Verify(c => c.RemoveAsync($"activities:{sessionId}", It.IsAny<CancellationToken>()), Times.Once());
        mockCache.Verify(c => c.RemoveAsync($"session:{sessionId}", It.IsAny<CancellationToken>()), Times.Once());
    }
}
