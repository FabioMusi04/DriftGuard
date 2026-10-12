using System.Text.Json;
using System.Text.Json.Nodes;

namespace DriftGuard.Core;

public sealed class PolicyEngine
{
    private const int MaxCandidateChanges = 20_000;
    private const int MaxRuleComparisons = 2_000_000;
    private const int MaxFindings = 100_000;

    public IReadOnlyList<PolicyFinding> Evaluate(PolicyDocument policy, IReadOnlyList<ConfigChange> changes)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(changes);
        PolicyLoader.Validate(policy);

        // Diff reports deliberately keep added/removed subtrees compact. Policies must
        // still inspect their descendants: adding {"logging":{"debug":true}} cannot
        // bypass a rule targeting $.logging.debug.
        var candidates = ExpandForPolicies(changes)
            .Select(change => (Change: change, Segments: JsonPath.Parse(change.Path, allowWildcards: false)))
            .ToArray();

        if ((long)candidates.Length * policy.Rules.Count > MaxRuleComparisons)
            throw new ConfigParseException("Policy evaluation requires too many rule comparisons; split the configuration or policy.");

        var findings = new List<PolicyFinding>();
        foreach (var rule in policy.Rules)
        {
            var pattern = JsonPath.Parse(rule.Path, allowWildcards: true);
            var severity = Enum.Parse<Severity>(rule.Severity, ignoreCase: true);
            foreach (var candidate in candidates)
            {
                var change = candidate.Change;
                if (!MatchesOperation(rule.Operation, change.Kind) || !JsonPath.Matches(pattern, candidate.Segments))
                    continue;
                // A missing property must not satisfy newValueEquals:null.
                if (rule.NewValueEquals is { } expected &&
                    (change.Kind == ChangeKind.Removed || !JsonValueEquals(change.CurrentValue, expected)))
                    continue;

                if (findings.Count >= MaxFindings)
                    throw new ConfigParseException("Policy evaluation exceeds the maximum number of findings.");
                findings.Add(new PolicyFinding(rule.Id, rule.Description, change.Path, severity, change.Kind));
            }
        }

        return findings
            .OrderByDescending(finding => finding.Severity)
            .ThenBy(finding => finding.Path, StringComparer.Ordinal)
            .ThenBy(finding => finding.RuleId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<ConfigChange> ExpandForPolicies(IReadOnlyList<ConfigChange> changes)
    {
        var expanded = new List<ConfigChange>();
        foreach (var change in changes)
        {
            AddCandidate(expanded, change);
            switch (change.Kind)
            {
                case ChangeKind.Added:
                    AppendDescendants(expanded, change.CurrentValue, change.Path, ChangeKind.Added);
                    break;
                case ChangeKind.Removed:
                    AppendDescendants(expanded, change.BaselineValue, change.Path, ChangeKind.Removed);
                    break;
                case ChangeKind.TypeChanged:
                    // A subtree appearing after a type change has newly observable
                    // descendant properties. The old subtree's properties disappear.
                    AppendDescendants(expanded, change.BaselineValue, change.Path, ChangeKind.Removed);
                    AppendDescendants(expanded, change.CurrentValue, change.Path, ChangeKind.Added);
                    break;
            }
        }
        return expanded;
    }

    private static void AppendDescendants(List<ConfigChange> expanded, JsonNode? node, string path, ChangeKind kind)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj)
            {
                var childPath = JsonPath.AppendProperty(path, property.Key);
                AddCandidate(expanded, kind == ChangeKind.Added
                    ? new ConfigChange(childPath, kind, null, property.Value)
                    : new ConfigChange(childPath, kind, property.Value, null));
                AppendDescendants(expanded, property.Value, childPath, kind);
            }
        }
        else if (node is JsonArray array)
        {
            for (var index = 0; index < array.Count; index++)
            {
                var childPath = JsonPath.AppendIndex(path, index);
                AddCandidate(expanded, kind == ChangeKind.Added
                    ? new ConfigChange(childPath, kind, null, array[index])
                    : new ConfigChange(childPath, kind, array[index], null));
                AppendDescendants(expanded, array[index], childPath, kind);
            }
        }
    }

    private static void AddCandidate(List<ConfigChange> expanded, ConfigChange change)
    {
        if (expanded.Count >= MaxCandidateChanges)
            throw new ConfigParseException($"Policy evaluation exceeds {MaxCandidateChanges} changed paths; split the comparison into smaller configurations.");
        expanded.Add(change);
    }

    private static bool MatchesOperation(string pattern, ChangeKind kind) =>
        pattern.Equals("any", StringComparison.OrdinalIgnoreCase) ||
        pattern.Equals(ToPolicyOperation(kind), StringComparison.OrdinalIgnoreCase);

    private static string ToPolicyOperation(ChangeKind kind) => kind switch
    {
        ChangeKind.TypeChanged => "type-changed",
        _ => kind.ToString().ToLowerInvariant()
    };

    private static bool JsonValueEquals(JsonNode? actual, JsonElement expected)
    {
        if (actual is null)
            return expected.ValueKind == JsonValueKind.Null;
        if (actual is JsonObject or JsonArray)
            return false;
        try
        {
            var expectedNode = JsonNode.Parse(expected.GetRawText());
            return string.Equals(actual.ToJsonString(), expectedNode?.ToJsonString(), StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
