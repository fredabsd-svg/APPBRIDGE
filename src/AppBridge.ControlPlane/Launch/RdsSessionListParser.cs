using System.Globalization;
using System.Text.Json;

namespace AppBridge.ControlPlane.Launching;

/// <summary>Lê o JSON produzido por <c>Get-RDUserSession</c> no <see cref="RdsSessionBackend"/>.</summary>
public static class RdsSessionListParser
{
    public static IReadOnlyList<BackendSessionSnapshot> Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new RdsSessionException("A resposta do Connection Broker não é JSON válido.", exception);
        }

        using (document)
        {
            // O PowerShell 5.1 serializa um único item como objeto, e não como lista.
            var items = document.RootElement.ValueKind switch
            {
                JsonValueKind.Array => document.RootElement.EnumerateArray().ToList(),
                JsonValueKind.Object => [document.RootElement],
                JsonValueKind.Null => [],
                _ => throw new RdsSessionException("A resposta do Connection Broker tem formato inesperado.")
            };

            var sessions = new List<BackendSessionSnapshot>(items.Count);
            foreach (var item in items)
            {
                var host = ReadString(item, "host");
                var id = ReadString(item, "id");
                if (string.IsNullOrWhiteSpace(host)
                    || string.IsNullOrWhiteSpace(id)
                    || !int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out _))
                {
                    throw new RdsSessionException("O Connection Broker devolveu sessão sem host ou identificador válido.");
                }

                var created = ReadString(item, "created");
                sessions.Add(new BackendSessionSnapshot(
                    host,
                    id,
                    ReadString(item, "sid"),
                    DateTimeOffset.TryParse(created, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var createdAt)
                        ? createdAt.ToUniversalTime()
                        : null));
            }

            return sessions;
        }
    }

    public static string NormalizeHost(string fqdn) => fqdn.Trim().TrimEnd('.').ToLowerInvariant();

    private static string? ReadString(JsonElement item, string property)
        => item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
