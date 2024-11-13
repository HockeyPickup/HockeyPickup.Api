namespace HockeyPickup.Api.Models.Responses;

public record RegisterResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public IEnumerable<string> Errors { get; init; } = Enumerable.Empty<string>();
}
