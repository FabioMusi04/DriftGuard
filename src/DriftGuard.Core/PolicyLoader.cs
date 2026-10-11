using System.Text.Json;
using System.Text.Json.Serialization;

namespace DriftGuard.Core;

public static class PolicyLoader
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 64
    };

    public static PolicyDocument LoadFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ConfigParseException("A policy file path is required.");
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new ConfigParseException($"Policy file not found: {fullPath}");
        var info = new FileInfo(fullPath);
        if (info.Length > 1024 * 1024)
            throw new ConfigParseException("Policy file exceeds the 1 MiB limit.");

        try
        {
            var text = File.ReadAllText(fullPath);
            var policy = JsonSerializer.Deserialize<PolicyDocument>(text, Options)
                ?? throw new ConfigParseException("Policy file must contain a JSON object.");
            Validate(policy);
            return policy;
        }
        catch (ConfigParseException) { throw; }
        catch (JsonException ex) { throw new ConfigParseException($"Invalid policy JSON: {ex.Message}", ex); }
    }

    public static void Validate(PolicyDocument policy)
    {
        if (policy.Version != 1)
            throw new ConfigParseException($"Unsupported policy version '{policy.Version}'. Supported version: 1.");
        if (policy.Rules is null)
            throw new ConfigParseException("Policy property 'rules' must be an array.");
        if (policy.Rules.Count == 0)
            throw new ConfigParseException("Policy must contain at least one rule; an empty policy can make a CI gate misleading.");
        if (policy.Rules.Count > 500)
            throw new ConfigParseException("A policy may contain at most 500 rules.");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rule in policy.Rules)
        {
            if (rule is null)
                throw new ConfigParseException("Policy rules cannot contain null entries.");
            if (string.IsNullOrWhiteSpace(rule.Id) || rule.Id.Length > 80)
                throw new ConfigParseException("Every policy rule needs an id of 1–80 characters.");
            if (!ids.Add(rule.Id))
                throw new ConfigParseException($"Duplicate policy rule id: '{rule.Id}'.");
            if (string.IsNullOrWhiteSpace(rule.Description))
                throw new ConfigParseException($"Rule '{rule.Id}' needs a description.");
            if (!Enum.TryParse<Severity>(rule.Severity, true, out _))
                throw new ConfigParseException($"Rule '{rule.Id}' has invalid severity '{rule.Severity}'. Use info, warning, or error.");
            if (!new[] { "any", "added", "removed", "changed", "type-changed" }.Contains(rule.Operation, StringComparer.OrdinalIgnoreCase))
                throw new ConfigParseException($"Rule '{rule.Id}' has invalid operation '{rule.Operation}'.");
            if (string.IsNullOrWhiteSpace(rule.Path) || !rule.Path.StartsWith('$'))
                throw new ConfigParseException($"Rule '{rule.Id}' path must start with '$'.");
            if (rule.NewValueEquals is { } value && (value.ValueKind is JsonValueKind.Object or JsonValueKind.Array))
                throw new ConfigParseException($"Rule '{rule.Id}' newValueEquals must be a scalar JSON value.");
        }
    }
}
