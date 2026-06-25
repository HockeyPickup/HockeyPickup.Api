using FluentAssertions;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Helpers;
using System.Text.Json;
using Xunit;

namespace HockeyPickup.Api.Tests.HelpersTests;

public class ApiJsonSerializerTests
{
    private sealed class EnumHolder
    {
        public LotteryClass LotteryClass { get; set; }
    }

    [Fact]
    public void Options_SerializesEnumsAsStringNames_NotNumbers()
    {
        // Arrange
        var holder = new EnumHolder { LotteryClass = LotteryClass.Standard };

        // Act
        var json = JsonSerializer.Serialize(holder, ApiJsonSerializer.Options);

        // Assert - the WebSocket push uses these options; LotteryClass must be the string member name
        // so the front-end's string comparison matches (a numeric value was the "entrants reset to 0"
        // bug). PascalCase property name is preserved.
        json.Should().Contain("\"LotteryClass\":\"Standard\"");
        json.Should().NotContain("\"LotteryClass\":3");
    }

    [Fact]
    public void Configure_AppliesCanonicalSettingsToExistingInstance()
    {
        // Arrange
        var options = new JsonSerializerOptions();

        // Act
        var configured = ApiJsonSerializer.Configure(options);

        // Assert - mutates and returns the same instance, keeps PascalCase, and registers the string
        // enum converter.
        configured.Should().BeSameAs(options);
        configured.PropertyNamingPolicy.Should().BeNull();
        JsonSerializer.Serialize(LotteryClass.PreferredPlus, configured).Should().Be("\"PreferredPlus\"");
    }
}
