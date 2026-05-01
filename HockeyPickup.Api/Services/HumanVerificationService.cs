using System.Net.Http.Json;
using System.Text.Json.Serialization;
using HockeyPickup.Api.Helpers;
using HockeyPickup.Api.Models.Domain;
using Microsoft.Extensions.Options;

namespace HockeyPickup.Api.Services;

public interface IHumanVerificationService
{
    Task<ServiceResult<HumanVerificationResult>> VerifyBuySpotAsync(
        string? token,
        string userId,
        int sessionId,
        DateTime buyWindow,
        bool enforceBuyWindowFreshness,
        CancellationToken cancellationToken = default);
}

public class HumanVerificationResult
{
    public bool IsRequired { get; set; }
    public DateTime? ChallengeTimestamp { get; set; }
    public string? Hostname { get; set; }
    public string? Action { get; set; }
    public string[] ErrorCodes { get; set; } = [];
}

public class TurnstileHumanVerificationService : IHumanVerificationService
{
    private const string SiteVerifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";
    private const string TurnstileProvider = "Turnstile";

    private readonly HttpClient _httpClient;
    private readonly BotProtectionOptions _options;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<TurnstileHumanVerificationService> _logger;

    public TurnstileHumanVerificationService(
        HttpClient httpClient,
        IOptions<BotProtectionOptions> options,
        IHttpContextAccessor httpContextAccessor,
        ILogger<TurnstileHumanVerificationService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<ServiceResult<HumanVerificationResult>> VerifyBuySpotAsync(
        string? token,
        string userId,
        int sessionId,
        DateTime buyWindow,
        bool enforceBuyWindowFreshness,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return ServiceResult<HumanVerificationResult>.CreateSuccess(new HumanVerificationResult
            {
                IsRequired = false
            });
        }

        if (!string.Equals(_options.Provider, TurnstileProvider, StringComparison.OrdinalIgnoreCase))
        {
            return CreateFailure(
                userId,
                sessionId,
                new HumanVerificationResult { IsRequired = true, ErrorCodes = ["unsupported-provider"] },
                "Human verification provider is not supported");
        }

        if (string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            return CreateFailure(
                userId,
                sessionId,
                new HumanVerificationResult { IsRequired = true, ErrorCodes = ["missing-secret"] },
                "Human verification is not configured");
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return CreateFailure(
                userId,
                sessionId,
                new HumanVerificationResult { IsRequired = true, ErrorCodes = ["missing-input-response"] },
                "Human verification token is required");
        }

        TurnstileSiteVerifyResponse? siteVerifyResponse;
        try
        {
            using var response = await _httpClient.PostAsync(SiteVerifyUrl, CreateRequestContent(token), cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return CreateFailure(
                    userId,
                    sessionId,
                    new HumanVerificationResult { IsRequired = true, ErrorCodes = [$"siteverify-http-{(int)response.StatusCode}"] },
                    "Human verification could not be completed");
            }

            siteVerifyResponse = await response.Content.ReadFromJsonAsync<TurnstileSiteVerifyResponse>(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            _logger.LogWarning(ex, "Turnstile verification request failed for user {UserId}, session {SessionId}", userId, sessionId);
            return ServiceResult<HumanVerificationResult>.CreateFailure("Human verification could not be completed");
        }

        if (siteVerifyResponse == null)
        {
            return CreateFailure(
                userId,
                sessionId,
                new HumanVerificationResult { IsRequired = true, ErrorCodes = ["invalid-siteverify-response"] },
                "Human verification could not be completed");
        }

        var result = MapResult(siteVerifyResponse);

        if (!siteVerifyResponse.Success)
        {
            return CreateFailure(userId, sessionId, result, "Human verification failed");
        }

        if (siteVerifyResponse.ChallengeTimestamp == null)
        {
            result.ErrorCodes = AppendErrorCode(result.ErrorCodes, "missing-challenge-ts");
            return CreateFailure(userId, sessionId, result, "Human verification challenge timestamp is missing");
        }

        if (!IsExpectedHostname(siteVerifyResponse.Hostname))
        {
            result.ErrorCodes = AppendErrorCode(result.ErrorCodes, "unexpected-hostname");
            return CreateFailure(userId, sessionId, result, "Human verification hostname is not valid");
        }

        if (!string.Equals(siteVerifyResponse.Action, _options.ExpectedAction, StringComparison.Ordinal))
        {
            result.ErrorCodes = AppendErrorCode(result.ErrorCodes, "unexpected-action");
            return CreateFailure(userId, sessionId, result, "Human verification action is not valid");
        }

        var challengeTimestamp = siteVerifyResponse.ChallengeTimestamp.Value;
        if (DateTimeOffset.UtcNow - challengeTimestamp.ToUniversalTime() > TimeSpan.FromSeconds(_options.MaxTokenAgeSeconds))
        {
            result.ErrorCodes = AppendErrorCode(result.ErrorCodes, "expired-challenge-ts");
            return CreateFailure(userId, sessionId, result, "Human verification token is too old");
        }

        if (enforceBuyWindowFreshness && !_options.AllowBeforeBuyWindow)
        {
            var challengePacificTime = challengeTimestamp.UtcDateTime.UtcToPacific();
            if (challengePacificTime < buyWindow)
            {
                result.ErrorCodes = AppendErrorCode(result.ErrorCodes, "challenge-before-buy-window");
                return CreateFailure(userId, sessionId, result, "Human verification token was created before the buy window opened");
            }
        }

        return ServiceResult<HumanVerificationResult>.CreateSuccess(result);
    }

    private FormUrlEncodedContent CreateRequestContent(string token)
    {
        var values = new Dictionary<string, string>
        {
            ["secret"] = _options.SecretKey!,
            ["response"] = token,
            ["idempotency_key"] = Guid.NewGuid().ToString()
        };

        var remoteIp = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
        if (!string.IsNullOrWhiteSpace(remoteIp))
        {
            values["remoteip"] = remoteIp;
        }

        return new FormUrlEncodedContent(values);
    }

    private bool IsExpectedHostname(string? hostname)
    {
        return !string.IsNullOrWhiteSpace(hostname)
            && _options.ExpectedHostnames.Any(expected => string.Equals(expected, hostname, StringComparison.OrdinalIgnoreCase));
    }

    private static HumanVerificationResult MapResult(TurnstileSiteVerifyResponse response)
    {
        return new HumanVerificationResult
        {
            IsRequired = true,
            ChallengeTimestamp = response.ChallengeTimestamp?.UtcDateTime,
            Hostname = response.Hostname,
            Action = response.Action,
            ErrorCodes = response.ErrorCodes ?? []
        };
    }

    private ServiceResult<HumanVerificationResult> CreateFailure(
        string userId,
        int sessionId,
        HumanVerificationResult result,
        string message)
    {
        _logger.LogWarning(
            "Turnstile verification failed for user {UserId}, session {SessionId}. Hostname: {Hostname}; Action: {Action}; ErrorCodes: {ErrorCodes}",
            userId,
            sessionId,
            result.Hostname,
            result.Action,
            string.Join(",", result.ErrorCodes));

        var failureMessage = result.ErrorCodes.Length == 0
            ? message
            : $"{message}: {string.Join(", ", result.ErrorCodes)}";

        return ServiceResult<HumanVerificationResult>.CreateFailure(failureMessage);
    }

    private static string[] AppendErrorCode(string[] errorCodes, string errorCode)
    {
        return errorCodes.Contains(errorCode)
            ? errorCodes
            : [.. errorCodes, errorCode];
    }

    private sealed class TurnstileSiteVerifyResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("challenge_ts")]
        public DateTimeOffset? ChallengeTimestamp { get; set; }

        [JsonPropertyName("hostname")]
        public string? Hostname { get; set; }

        [JsonPropertyName("action")]
        public string? Action { get; set; }

        [JsonPropertyName("cdata")]
        public string? CData { get; set; }

        [JsonPropertyName("error-codes")]
        public string[]? ErrorCodes { get; set; }
    }
}
