using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AppBridge.ControlPlane.Infrastructure.Conventions;

/// <summary>
/// Stores C# enums as the lower snake_case text MODELO-DE-DADOS.md actually writes (e.g.
/// <c>DeniedPermission</c> → <c>"denied_permission"</c>), instead of EF Core's default of the bare
/// enum member name. Keeps the database self-describing and consistent with every other
/// identifier in the schema (ADR-0011 §6) — a DBA reading the table shouldn't hit the one column
/// that's PascalCase.
/// </summary>
public static class SnakeCaseEnumConverter
{
    public static ValueConverter<TEnum, string> For<TEnum>() where TEnum : struct, Enum
    {
        return new ValueConverter<TEnum, string>(
            value => SnakeCaseNamingConvention.ToSnakeCase(value.ToString()),
            text => Parse<TEnum>(text));
    }

    private static TEnum Parse<TEnum>(string text) where TEnum : struct, Enum
    {
        var pascalCase = string.Concat(text.Split('_').Select(word =>
            char.ToUpperInvariant(word[0]) + word[1..]));
        return Enum.Parse<TEnum>(pascalCase);
    }
}
