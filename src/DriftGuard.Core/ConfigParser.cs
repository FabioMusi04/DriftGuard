using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace DriftGuard.Core;

public sealed class ConfigParseException : Exception
{
    public ConfigParseException(string message) : base(message) { }
    public ConfigParseException(string message, Exception innerException) : base(message, innerException) { }
}

public static class ConfigParser
{
    public const int MaxFileBytes = 5 * 1024 * 1024;
    public const int MaxDepth = 128;

    public static JsonNode ParseFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ConfigParseException("A configuration path is required.");

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new ConfigParseException($"Configuration file not found: {fullPath}");

        var info = new FileInfo(fullPath);
        if (info.Length > MaxFileBytes)
            throw new ConfigParseException($"Configuration file exceeds the {MaxFileBytes / (1024 * 1024)} MiB limit: {fullPath}");

        var text = File.ReadAllText(fullPath);
        var extension = Path.GetExtension(fullPath);
        return Parse(text, extension);
    }

    public static JsonNode Parse(string text, string extension)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (System.Text.Encoding.UTF8.GetByteCount(text) > MaxFileBytes)
            throw new ConfigParseException($"Configuration content exceeds the {MaxFileBytes / (1024 * 1024)} MiB limit.");

        try
        {
            JsonNode? root;
            if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                root = JsonNode.Parse(text, documentOptions: new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = MaxDepth
                });
            }
            else if (extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) ||
                     extension.Equals(".yml", StringComparison.OrdinalIgnoreCase))
            {
                root = ParseYaml(text);
            }
            else
            {
                throw new ConfigParseException("Unsupported file extension. Use .json, .yaml, or .yml.");
            }

            if (root is not JsonObject)
                throw new ConfigParseException("The configuration root must be an object (a JSON/YAML mapping).");

            return root;
        }
        catch (ConfigParseException)
        {
            throw;
        }
        catch (JsonException ex)
        {
            throw new ConfigParseException($"Invalid JSON configuration: {ex.Message}", ex);
        }
        catch (YamlException ex)
        {
            throw new ConfigParseException($"Invalid YAML configuration: {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException or ArgumentException)
        {
            throw new ConfigParseException($"Could not parse configuration: {ex.Message}", ex);
        }
    }

    private static JsonNode ParseYaml(string text)
    {
        var stream = new YamlStream();
        using var reader = new StringReader(text);
        stream.Load(reader);

        if (stream.Documents.Count != 1)
            throw new ConfigParseException("YAML must contain exactly one document.");

        return ConvertYamlNode(stream.Documents[0].RootNode, 0);
    }

    private static JsonNode ConvertYamlNode(YamlNode node, int depth)
    {
        if (depth > MaxDepth)
            throw new ConfigParseException($"YAML nesting exceeds the maximum depth of {MaxDepth}.");

        switch (node)
        {
            case YamlMappingNode mapping:
            {
                var result = new JsonObject();
                foreach (var entry in mapping.Children)
                {
                    if (entry.Key is not YamlScalarNode key || key.Value is null)
                        throw new ConfigParseException("YAML mapping keys must be non-null scalar strings.");

                    if (result.ContainsKey(key.Value))
                        throw new ConfigParseException($"Duplicate YAML mapping key: '{key.Value}'.");

                    result.Add(key.Value, ConvertYamlNode(entry.Value, depth + 1));
                }
                return result;
            }
            case YamlSequenceNode sequence:
            {
                var result = new JsonArray();
                foreach (var child in sequence.Children)
                    result.Add(ConvertYamlNode(child, depth + 1));
                return result;
            }
            case YamlScalarNode scalar:
                return ConvertYamlScalar(scalar);
            default:
                throw new ConfigParseException($"Unsupported YAML node type: {node.GetType().Name}.");
        }
    }

    private static JsonNode ConvertYamlScalar(YamlScalarNode scalar)
    {
        var value = scalar.Value;
        if (value is null)
            return null!;

        // Quoted and block scalars stay strings even when their contents look numeric or boolean.
        if (scalar.Style is ScalarStyle.SingleQuoted or ScalarStyle.DoubleQuoted or ScalarStyle.Literal or ScalarStyle.Folded)
            return JsonValue.Create(value)!;

        if (value is "null" or "Null" or "NULL" or "~")
            return null!;
        if (bool.TryParse(value, out var boolean))
            return JsonValue.Create(boolean)!;
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
            return JsonValue.Create(integer)!;
        if (decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) &&
            (value.Contains('.') || (value.Contains('e') || value.Contains('E'))))
            return JsonValue.Create(number)!;

        return JsonValue.Create(value)!;
    }
}
