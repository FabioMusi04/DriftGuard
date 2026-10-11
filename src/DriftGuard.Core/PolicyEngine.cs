using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace DriftGuard.Core;

public sealed class PolicyEngine
{
    public IReadOnlyList<PolicyFinding> Evaluate(PolicyDocument policy, IReadOnlyList<ConfigChange> changes)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(changes);
        PolicyLoader.Validate(policy);

        var findings = new List<PolicyFinding>();
        foreach (var rule in policy.Rules)
        {
            var pathRegex = GlobToRegex(rule.Path);
            var severity = Enum.Parse<Severity>(rule.Severity, ignoreCase: true);
            foreach (var change in changes)
            {
                if (!MatchesOperation(rule.Operation, change.Kind) || !pathRegex.IsMatch(change.Path))
                    continue;
                // A removed property has no current value; it must not satisfy
                // newValueEquals: null (that would confuse absence with explicit null).
                if (rule.NewValueEquals is { } expected &&
                    (change.Kind == ChangeKind.Removed || !JsonValueEquals(change.CurrentValue, expected)))
                    continue;
                findings.Add(new PolicyFinding(rule.Id, rule.Description, change.Path, severity, change.Kind));
            }
        }

        return findings
            .OrderByDescending(finding => finding.Severity)
            .ThenBy(finding => finding.Path, StringComparer.Ordinal)
            .ThenBy(finding => finding.RuleId, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool MatchesOperation(string pattern, ChangeKind kind) =>
        pattern.Equals("any", StringComparison.OrdinalIgnoreCase) ||
        pattern.Equals(ToPolicyOperation(kind), StringComparison.OrdinalIgnoreCase);

    private static string ToPolicyOperation(ChangeKind kind) => kind switch
    {
        ChangeKind.TypeChanged => "type-changed",
        _ => kind.ToString().ToLowerInvariant()
    };

    private static Regex GlobToRegex(string pattern)
    {
        var escaped = Regex.Escape(pattern)
            .Replace(@"\*\*", ".*", StringComparison.Ordinal)
            .Replace(@"\*", @"[^.\[\]]+", StringComparison.Ordinal);
        return new Regex("^" + escaped + "$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
    }

    private static bool JsonValueEquals(JsonNode? actual, JsonElement expected)
    {
        if (actual is null)
            return expected.ValueKind == JsonValueKind.Null;

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
