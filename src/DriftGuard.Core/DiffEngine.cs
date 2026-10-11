using System.Text.Json.Nodes;

namespace DriftGuard.Core;

public sealed class DiffEngine
{
    public IReadOnlyList<ConfigChange> Compare(JsonNode baseline, JsonNode current)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(current);
        var changes = new List<ConfigChange>();
        CompareNode(baseline, current, "$", changes);
        return changes;
    }

    private static void CompareNode(JsonNode? baseline, JsonNode? current, string path, List<ConfigChange> changes)
    {
        var baselineType = GetTypeName(baseline);
        var currentType = GetTypeName(current);
        if (!string.Equals(baselineType, currentType, StringComparison.Ordinal))
        {
            changes.Add(new ConfigChange(path, ChangeKind.TypeChanged, baseline, current));
            return;
        }

        if (baseline is JsonObject baselineObject && current is JsonObject currentObject)
        {
            var names = baselineObject.Select(pair => pair.Key)
                .Union(currentObject.Select(pair => pair.Key), StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal);

            foreach (var name in names)
            {
                var inBaseline = baselineObject.TryGetPropertyValue(name, out var oldValue);
                var inCurrent = currentObject.TryGetPropertyValue(name, out var newValue);
                var childPath = AppendProperty(path, name);

                if (inBaseline && !inCurrent)
                    changes.Add(new ConfigChange(childPath, ChangeKind.Removed, oldValue, null));
                else if (!inBaseline && inCurrent)
                    changes.Add(new ConfigChange(childPath, ChangeKind.Added, null, newValue));
                else
                    CompareNode(oldValue, newValue, childPath, changes);
            }
            return;
        }

        if (baseline is JsonArray oldArray && current is JsonArray newArray)
        {
            var count = Math.Max(oldArray.Count, newArray.Count);
            for (var index = 0; index < count; index++)
            {
                var childPath = $"{path}[{index}]";
                if (index >= oldArray.Count)
                    changes.Add(new ConfigChange(childPath, ChangeKind.Added, null, newArray[index]));
                else if (index >= newArray.Count)
                    changes.Add(new ConfigChange(childPath, ChangeKind.Removed, oldArray[index], null));
                else
                    CompareNode(oldArray[index], newArray[index], childPath, changes);
            }
            return;
        }

        // Comparing canonical JSON renderings keeps equivalent scalar values stable even
        // when one node came from JSON and the other from the YAML normalizer.
        if (!string.Equals(baseline?.ToJsonString(), current?.ToJsonString(), StringComparison.Ordinal))
            changes.Add(new ConfigChange(path, ChangeKind.Changed, baseline, current));
    }

    private static string GetTypeName(JsonNode? node)
    {
        if (node is null)
            return "null";
        if (node is JsonObject)
            return "object";
        if (node is JsonArray)
            return "array";

        using var document = System.Text.Json.JsonDocument.Parse(node.ToJsonString());
        return document.RootElement.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => "string",
            System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False => "boolean",
            System.Text.Json.JsonValueKind.Number => "number",
            System.Text.Json.JsonValueKind.Null => "null",
            _ => "unknown"
        };
    }

    private static string AppendProperty(string parent, string name)
    {
        // Use dot notation for normal keys and bracket notation for unusual keys.
        if (name.Length > 0 && (char.IsLetter(name[0]) || name[0] == '_') &&
            name.All(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-'))
            return $"{parent}.{name}";
        return $"{parent}['{name.Replace("'", "\\'", StringComparison.Ordinal)}']";
    }
}
