using System.Text.Json;
using System.Text.Json.Serialization;
using Dynamic.Users.Domain.Enums;

namespace Dynamic.Users.Application.Common;

public sealed class UserGenderJsonConverter : JsonConverter<UserGender>
{
    public override UserGender Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            string? rawValue = reader.GetString();
            if (TryParse(rawValue, out UserGender gender))
            {
                return gender;
            }
        }
        else if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out int numericValue))
        {
            if (Enum.IsDefined(typeof(UserGender), numericValue))
            {
                return (UserGender)numericValue;
            }
        }

        throw new JsonException("El campo gender debe ser Hombre, Mujer u OtroPrefieroNoEspecificar.");
    }

    public override void Write(Utf8JsonWriter writer, UserGender value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());

    private static bool TryParse(string? rawValue, out UserGender gender)
    {
        string normalized = Normalize(rawValue);

        gender = normalized switch
        {
            "hombre" or "male" or "masculino" => UserGender.Hombre,
            "mujer" or "female" or "femenino" => UserGender.Mujer,
            "otroprefieronoespecificar" or "prefernottosay" or "otro" or "noespecificado" or "prefieronodecirlo" =>
                UserGender.OtroPrefieroNoEspecificar,
            _ => default
        };

        return normalized.Length > 0 &&
            (gender == UserGender.Hombre ||
             gender == UserGender.Mujer ||
             normalized is "otroprefieronoespecificar" or "prefernottosay" or "otro" or "noespecificado" or "prefieronodecirlo");
    }

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Replace("_", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
}
