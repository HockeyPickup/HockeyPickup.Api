using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HockeyPickup.Api.Helpers;

[ExcludeFromCodeCoverage]
public class EnumDisplayNameConverter<T> : JsonConverter<T> where T : Enum
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return (T)Enum.Parse(typeof(T), value!, true);
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        var displayAttribute = value.GetType()
            .GetField(value.ToString())
            ?.GetCustomAttributes(false)
            .OfType<DisplayAttribute>()
            .FirstOrDefault();

        writer.WriteStringValue(displayAttribute?.Name ?? value.ToString());
    }
}