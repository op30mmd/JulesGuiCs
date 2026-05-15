using System.Net;
using System.Text.Json;
using JulesClient.Models;
using JulesClient.Services;
using Moq;

namespace JulesClient.Tests;

public class JulesApiClientTests
{
    private readonly Mock<ISettingsService> _mockSettings;

    public JulesApiClientTests()
    {
        _mockSettings = new Mock<ISettingsService>();
        _mockSettings.Setup(s => s.ApiKey).Returns("test-api-key");
    }

    [Fact]
    public async Task ListActivitiesAsync_FormatsCreateTimeQueryParameter()
    {
        // Arrange
        var timestamp = "2024-01-15T10:30:00Z";
        var sessionId = "sessions/123";
        var expectedUri = $"https://jules.googleapis.com/v1alpha/{sessionId}/activities?pageSize=30&createTime={Uri.EscapeDataString(timestamp)}";

        var handler = new MockHttpMessageHandler((request, ct) =>
        {
            if (request.RequestUri?.ToString() == expectedUri)
            {
                var response = new ActivityListResponse(new List<Activity>(), null);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(response))
                });
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
        });

        var client = new JulesApiClient(_mockSettings.Object, handler);

        // Act
        var result = await client.ListActivitiesAsync(sessionId, createTime: timestamp);

        // Assert
        Assert.NotNull(result);
    }
}

public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

    public MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return _handler(request, cancellationToken);
    }
}
