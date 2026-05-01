using System.Net;
using System.Text;
using FluentAssertions;
using HockeyPickup.Api.Helpers;
using HockeyPickup.Api.Models.Domain;
using HockeyPickup.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace HockeyPickup.Api.Tests.ServicesTests;

public class TurnstileHumanVerificationServiceTest
{
    private readonly Mock<ILogger<TurnstileHumanVerificationService>> _mockLogger = new();

    [Fact]
    public async Task VerifyBuySpotAsync_ProtectionDisabled_ReturnsSuccessWithoutHttpCall()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler((_, _) => throw new InvalidOperationException("HTTP should not be called"));
        var service = CreateService(handler, new BotProtectionOptions { Enabled = false });

        // Act
        var result = await service.VerifyBuySpotAsync(null, "user-1", 1, DateTime.UtcNow, true);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.IsRequired.Should().BeFalse();
        handler.Calls.Should().Be(0);
    }

    [Fact]
    public async Task VerifyBuySpotAsync_EnabledAndMissingToken_ReturnsFailure()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler((_, _) => throw new InvalidOperationException("HTTP should not be called"));
        var service = CreateService(handler);

        // Act
        var result = await service.VerifyBuySpotAsync("", "user-1", 1, DateTime.UtcNow, true);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Human verification token is required");
        result.Message.Should().Contain("missing-input-response");
        handler.Calls.Should().Be(0);
    }

    [Fact]
    public async Task VerifyBuySpotAsync_SiteverifySuccess_ReturnsSuccess()
    {
        // Arrange
        var challengeTimestamp = DateTimeOffset.UtcNow;
        var handler = new FakeHttpMessageHandler((_, _) => JsonResponse(SuccessJson(challengeTimestamp)));
        var service = CreateService(handler);

        // Act
        var result = await service.VerifyBuySpotAsync(
            "fresh-token",
            "user-1",
            1,
            challengeTimestamp.UtcDateTime.UtcToPacific().AddMinutes(-1),
            true);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.IsRequired.Should().BeTrue();
        result.Data.Hostname.Should().Be("app.hockeypickup.com");
        result.Data.Action.Should().Be("buy_spot");
        handler.Calls.Should().Be(1);
        handler.LastBody.Should().Contain("secret=test-secret");
        handler.LastBody.Should().Contain("response=fresh-token");
        handler.LastBody.Should().Contain("idempotency_key=");
    }

    [Fact]
    public async Task VerifyBuySpotAsync_SiteverifyFailure_ReturnsFailureWithErrorCodes()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler((_, _) => JsonResponse("""
            {
              "success": false,
              "error-codes": ["timeout-or-duplicate"]
            }
            """));
        var service = CreateService(handler);

        // Act
        var result = await service.VerifyBuySpotAsync("used-token", "user-1", 1, DateTime.UtcNow, true);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Human verification failed");
        result.Message.Should().Contain("timeout-or-duplicate");
    }

    [Fact]
    public async Task VerifyBuySpotAsync_RejectsUnexpectedHostname()
    {
        // Arrange
        var challengeTimestamp = DateTimeOffset.UtcNow;
        var handler = new FakeHttpMessageHandler((_, _) => JsonResponse(SuccessJson(challengeTimestamp, hostname: "evil.example")));
        var service = CreateService(handler);

        // Act
        var result = await service.VerifyBuySpotAsync(
            "fresh-token",
            "user-1",
            1,
            challengeTimestamp.UtcDateTime.UtcToPacific().AddMinutes(-1),
            true);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("unexpected-hostname");
    }

    [Fact]
    public async Task VerifyBuySpotAsync_RejectsUnexpectedAction()
    {
        // Arrange
        var challengeTimestamp = DateTimeOffset.UtcNow;
        var handler = new FakeHttpMessageHandler((_, _) => JsonResponse(SuccessJson(challengeTimestamp, action: "page_load")));
        var service = CreateService(handler);

        // Act
        var result = await service.VerifyBuySpotAsync(
            "fresh-token",
            "user-1",
            1,
            challengeTimestamp.UtcDateTime.UtcToPacific().AddMinutes(-1),
            true);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("unexpected-action");
    }

    [Fact]
    public async Task VerifyBuySpotAsync_RejectsExpiredChallengeTimestamp()
    {
        // Arrange
        var challengeTimestamp = DateTimeOffset.UtcNow.AddMinutes(-2);
        var handler = new FakeHttpMessageHandler((_, _) => JsonResponse(SuccessJson(challengeTimestamp)));
        var service = CreateService(handler);

        // Act
        var result = await service.VerifyBuySpotAsync(
            "fresh-token",
            "user-1",
            1,
            challengeTimestamp.UtcDateTime.UtcToPacific().AddMinutes(-1),
            true);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("expired-challenge-ts");
    }

    [Fact]
    public async Task VerifyBuySpotAsync_RejectsChallengeTimestampBeforeBuyWindow()
    {
        // Arrange
        var challengeTimestamp = DateTimeOffset.UtcNow;
        var buyWindow = challengeTimestamp.UtcDateTime.UtcToPacific().AddSeconds(1);
        var handler = new FakeHttpMessageHandler((_, _) => JsonResponse(SuccessJson(challengeTimestamp)));
        var service = CreateService(handler);

        // Act
        var result = await service.VerifyBuySpotAsync("fresh-token", "user-1", 1, buyWindow, true);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("challenge-before-buy-window");
    }

    [Fact]
    public async Task VerifyBuySpotAsync_AllowsAdminBypassOfBuyWindowFreshness_WhenConfiguredOrFlagged()
    {
        // Arrange
        var challengeTimestamp = DateTimeOffset.UtcNow;
        var buyWindow = challengeTimestamp.UtcDateTime.UtcToPacific().AddMinutes(5);
        var handler = new FakeHttpMessageHandler((_, _) => JsonResponse(SuccessJson(challengeTimestamp)));
        var service = CreateService(handler);

        // Act
        var result = await service.VerifyBuySpotAsync("fresh-token", "admin-user", 1, buyWindow, false);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyBuySpotAsync_DoesNotLogRawToken()
    {
        // Arrange
        var rawToken = "sensitive-turnstile-token";
        var handler = new FakeHttpMessageHandler((_, _) => JsonResponse("""
            {
              "success": false,
              "error-codes": ["timeout-or-duplicate"]
            }
            """));
        var service = CreateService(handler);

        // Act
        await service.VerifyBuySpotAsync(rawToken, "user-1", 1, DateTime.UtcNow, true);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((value, _) => value.ToString()!.Contains(rawToken)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    private TurnstileHumanVerificationService CreateService(
        HttpMessageHandler handler,
        BotProtectionOptions? options = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");

        return new TurnstileHumanVerificationService(
            new HttpClient(handler),
            Options.Create(options ?? CreateEnabledOptions()),
            new HttpContextAccessor { HttpContext = httpContext },
            _mockLogger.Object);
    }

    private static BotProtectionOptions CreateEnabledOptions()
    {
        return new BotProtectionOptions
        {
            Enabled = true,
            Provider = "Turnstile",
            SecretKey = "test-secret",
            ExpectedAction = "buy_spot",
            ExpectedHostnames = ["app.hockeypickup.com"],
            MaxTokenAgeSeconds = 30,
            AllowBeforeBuyWindow = false
        };
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static string SuccessJson(
        DateTimeOffset challengeTimestamp,
        string hostname = "app.hockeypickup.com",
        string action = "buy_spot")
    {
        return $$"""
            {
              "success": true,
              "challenge_ts": "{{challengeTimestamp.UtcDateTime:O}}",
              "hostname": "{{hostname}}",
              "action": "{{action}}",
              "cdata": "s_1",
              "error-codes": []
            }
            """;
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _handler;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        public int Calls { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastBody = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return _handler(request, cancellationToken);
        }
    }
}

