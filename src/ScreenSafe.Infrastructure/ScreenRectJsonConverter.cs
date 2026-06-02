using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using ScreenSafe.Domain;

namespace ScreenSafe.Infrastructure;

/// <summary>
/// Custom JSON converter for <see cref="ScreenRect"/>.
/// Required because ScreenRect is a <see langword="readonly"/> struct with
/// <see langword="get"/>-only properties, which System.Text.Json cannot
/// deserialize via the default parameterless struct constructor.
/// </summary>
internal sealed class ScreenRectJsonConverter : JsonConverter<ScreenRect>
{
    public override ScreenRect Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected start of object for ScreenRect.");

        int left = 0, top = 0, right = 0, bottom = 0;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return new ScreenRect(left, top, right, bottom);

            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                var propertyName = reader.GetString();
                reader.Read();

                switch (propertyName)
                {
                    case "Left":
                        left = reader.GetInt32();
                        break;
                    case "Top":
                        top = reader.GetInt32();
                        break;
                    case "Right":
                        right = reader.GetInt32();
                        break;
                    case "Bottom":
                        bottom = reader.GetInt32();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }
        }

        throw new JsonException("Unexpected end of JSON when reading ScreenRect.");
    }

    public override void Write(Utf8JsonWriter writer, ScreenRect value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("Left", value.Left);
        writer.WriteNumber("Top", value.Top);
        writer.WriteNumber("Right", value.Right);
        writer.WriteNumber("Bottom", value.Bottom);
        writer.WriteEndObject();
    }
}
