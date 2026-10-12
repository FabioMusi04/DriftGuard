using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace DriftGuard.Core;

public static partial class ValueRedactor
{
    private static readonly string[] SensitiveNameFragments =
    [
        "password", "passwd", "secret", "token", "apikey", "privatekey",
        "credential", "authorization", "connectionstring", "accesskey", "clientsecret"
    ];

    [GeneratedRegex(@"(?i)\bBearer\s+[^\s,;]+|\bAKIA[0-9A-Z]{16}\b|-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----|(?i)(?:api[_-]?key|client[_-]?secret)\s*[:=]\s*[^\s,;]+", RegexOptions.CultureInvariant)]
    private static partial Regex SecretLikeValueRegex();

    public static bool IsSensitivePath(string path)
    {
        var segments = path.Split(new[] { '.', '[', ']', '\'', '"' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(segment => !string.IsNullOrWhiteSpace(segment) && segment != "$");
        return segments.Any(segment =>
        {
            var normalized = new string(segment.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
            return SensitiveNameFragments.Any(fragment => normalized.Contains(fragment, StringComparison.Ordinal));
        });
    }

    public static JsonNode? SafeNode(JsonNode? node, string path)
    {
        if (node is null)
            return null;
        if (IsSensitivePath(path))
            return JsonValue.Create("[REDACTED]");

        if (node is JsonObject obj)
        {
            var copy = new JsonObject();
            foreach (var property in obj)
                copy[property.Key] = SafeNode(property.Value, Append(path, property.Key));
            return copy;
        }

        if (node is JsonArray array)
        {
            var copy = new JsonArray();
            for (var i = 0; i < array.Count; i++)
                copy.Add(SafeNode(array[i], JsonPath.AppendIndex(path, i)));
            return copy;
        }

        if (node is JsonValue)
        {
            using var document = JsonDocument.Parse(node.ToJsonString());
            if (document.RootElement.ValueKind == JsonValueKind.String)
            {
                var text = document.RootElement.GetString();
                if (text is not null && SecretLikeValueRegex().IsMatch(text))
                    return JsonValue.Create("[REDACTED]");
            }
        }

        return node.DeepClone();
    }

    public static string SafeJson(JsonNode? node, string path, bool indented = false)
    {
        var safe = SafeNode(node, path);
        return safe is null ? "null" : safe.ToJsonString(new JsonSerializerOptions { WriteIndented = indented });
    }

    private static string Append(string parent, string property) =>
        JsonPath.AppendProperty(parent, property);
}
