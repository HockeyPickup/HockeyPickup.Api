namespace HockeyPickup.Api.Models.Responses;

public record LoginResponse
{
    public required string Token { get; init; }
    public required DateTime Expiration { get; init; }
}
