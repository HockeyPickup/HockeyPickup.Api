using HockeyPickup.Api.Data.Entities;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HockeyPickup.Api.Helpers;

// Canonical System.Text.Json configuration for the API. Used by both the HTTP/GraphQL pipeline
// (Program.cs) and the WebSocket push so enums serialize as their string/display names — matching
// the front-end's string enums — instead of raw numeric values. Without this, the WebSocket payload
// sends enums (e.g. LotteryClass) as numbers, which the front-end's string comparisons never match.
public static class ApiJsonSerializer
{
    // A ready-to-use options instance for ad-hoc serialization (e.g. the WebSocket push).
    public static JsonSerializerOptions Options { get; } = Configure(new JsonSerializerOptions());

    // Applies the canonical converters/naming to an options instance so callers that own their own
    // options (the MVC JSON pipeline) and callers that don't (the WebSocket push) stay in sync.
    public static JsonSerializerOptions Configure(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = null; // keep PascalCase
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new EnumDisplayNameConverter<TeamAssignment>());
        options.Converters.Add(new EnumDisplayNameConverter<PositionPreference>());
        options.Converters.Add(new EnumDisplayNameConverter<PlayerStatus>());
        options.Converters.Add(new EnumDisplayNameConverter<NotificationPreference>());
        options.Converters.Add(new EnumDisplayNameConverter<PaymentMethodType>());
        return options;
    }
}
