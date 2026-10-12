using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DriftGuard.Core;

namespace DriftGuard.Cli;

internal static class ReportWriter
{
    private static readonly JsonSerializerOptions PrettyJson = new() { WriteIndented = true };

    public static void WriteText(
        string baselinePath,
        string currentPath,
        IReadOnlyList<ConfigChange> changes,
        IReadOnlyList<PolicyFinding> findings,
        string failOn,
        int exitCode,
        bool policyApplied,
        bool includeValues = true)
    {
        Console.WriteLine("DriftGuard · semantic configuration diff");
        Console.WriteLine($"Baseline: {SafeConsole(baselinePath)}");
        Console.WriteLine($"Current:  {SafeConsole(currentPath)}");
        Console.WriteLine();
        var added = changes.Count(change => change.Kind == ChangeKind.Added);
        var removed = changes.Count(change => change.Kind == ChangeKind.Removed);
        var changed = changes.Count(change => change.Kind == ChangeKind.Changed);
        var typeChanged = changes.Count(change => change.Kind == ChangeKind.TypeChanged);
        Console.WriteLine($"Changes: {changes.Count}  (+{added} added, -{removed} removed, ~{changed} changed, !{typeChanged} type changed)");

        if (changes.Count == 0)
        {
            SetColor(ConsoleColor.Green);
            Console.WriteLine("No semantic changes detected.");
            ResetColor();
        }
        else
        {
            Console.WriteLine();
            WriteChanges(changes, includeValues);
        }

        Console.WriteLine();
        WriteFindings(findings);
        Console.WriteLine();
        var failed = exitCode != 0;
        SetColor(failed ? ConsoleColor.Red : ConsoleColor.Green);
        Console.WriteLine(!policyApplied
            ? "COMPARISON COMPLETE (no policy applied; report only). Exit code: 0"
            : failed
                ? $"POLICY CHECK FAILED (threshold: {failOn}). Exit code: {exitCode}"
                : $"POLICY CHECK PASSED (threshold: {failOn}). Exit code: {exitCode}");
        ResetColor();
    }

    public static void WriteChanges(IReadOnlyList<ConfigChange> changes, bool includeValues = true)
    {
        foreach (var change in changes)
        {
            var (symbol, color) = change.Kind switch
            {
                ChangeKind.Added => ("+", ConsoleColor.Green),
                ChangeKind.Removed => ("-", ConsoleColor.Red),
                ChangeKind.TypeChanged => ("!", ConsoleColor.Yellow),
                _ => ("~", ConsoleColor.Cyan)
            };
            SetColor(color);
            Console.WriteLine($"{symbol} [{change.Kind}] {SafeConsole(change.Path)}");
            ResetColor();
            if (!includeValues)
                continue;
            if (change.Kind is ChangeKind.Removed or ChangeKind.Changed or ChangeKind.TypeChanged)
                Console.WriteLine($"    before: {ValueRedactor.SafeJson(change.BaselineValue, change.Path)}");
            if (change.Kind is ChangeKind.Added or ChangeKind.Changed or ChangeKind.TypeChanged)
                Console.WriteLine($"    after:  {ValueRedactor.SafeJson(change.CurrentValue, change.Path)}");
        }
    }

    public static void WriteFindings(IReadOnlyList<PolicyFinding> findings)
    {
        Console.WriteLine($"Policy findings: {findings.Count}");
        if (findings.Count == 0)
        {
            Console.WriteLine("  No rules matched the observed changes.");
            return;
        }

        foreach (var finding in findings)
        {
            SetColor(finding.Severity switch
            {
                Severity.Error => ConsoleColor.Red,
                Severity.Warning => ConsoleColor.Yellow,
                _ => ConsoleColor.Gray
            });
            Console.WriteLine($"  [{finding.Severity.ToString().ToUpperInvariant()}] {SafeConsole(finding.RuleId)} — {SafeConsole(finding.Path)}");
            ResetColor();
            Console.WriteLine($"    {SafeConsole(finding.Description)}");
        }
    }

    public static void WriteJson(
        string baselinePath,
        string currentPath,
        IReadOnlyList<ConfigChange> changes,
        IReadOnlyList<PolicyFinding> findings,
        string failOn,
        int exitCode,
        string? explainPath = null,
        bool includeValues = true,
        bool policyApplied = false)
    {
        var changeRows = new JsonArray();
        foreach (var change in changes)
        {
            var row = new JsonObject
            {
                ["path"] = change.Path,
                ["operation"] = Operation(change.Kind)
            };
            if (includeValues)
            {
                if (change.Kind is ChangeKind.Removed or ChangeKind.Changed or ChangeKind.TypeChanged)
                    row["baselineValue"] = ValueRedactor.SafeNode(change.BaselineValue, change.Path);
                if (change.Kind is ChangeKind.Added or ChangeKind.Changed or ChangeKind.TypeChanged)
                    row["currentValue"] = ValueRedactor.SafeNode(change.CurrentValue, change.Path);
            }
            changeRows.Add(row);
        }

        var findingRows = new JsonArray();
        foreach (var finding in findings)
        {
            findingRows.Add(new JsonObject
            {
                ["ruleId"] = finding.RuleId,
                ["description"] = finding.Description,
                ["path"] = finding.Path,
                ["severity"] = finding.Severity.ToString().ToLowerInvariant(),
                ["operation"] = Operation(finding.ChangeKind)
            });
        }

        // Keep the v1 wire contract unchanged. --values none is represented by omitting
        // BOTH value properties (rather than replacing values with unreliable placeholders).
        var report = new JsonObject
        {
            ["tool"] = "DriftGuard",
            ["version"] = BuildInfo.Version,
            ["schemaVersion"] = BuildInfo.ReportSchemaVersion,
            ["policyApplied"] = policyApplied,
            ["baseline"] = baselinePath,
            ["current"] = currentPath,
            ["changes"] = changeRows,
            ["findings"] = findingRows,
            ["summary"] = new JsonObject
            {
                ["changeCount"] = changes.Count,
                ["findingCount"] = findings.Count,
                ["added"] = changes.Count(change => change.Kind == ChangeKind.Added),
                ["removed"] = changes.Count(change => change.Kind == ChangeKind.Removed),
                ["changed"] = changes.Count(change => change.Kind == ChangeKind.Changed),
                ["typeChanged"] = changes.Count(change => change.Kind == ChangeKind.TypeChanged)
            },
            ["failOn"] = failOn,
            ["passed"] = exitCode == 0,
            ["exitCode"] = exitCode
        };
        if (explainPath is not null)
            report["explainPath"] = explainPath;
        WriteObject(report);
    }

    public static void WriteErrorJson(string code, string message)
    {
        WriteObject(new JsonObject
        {
            ["kind"] = "error",
            ["tool"] = "DriftGuard",
            ["version"] = BuildInfo.Version,
            ["schemaVersion"] = 1,
            ["error"] = new JsonObject { ["code"] = code, ["message"] = message },
            ["exitCode"] = 2
        });
    }

    public static void WritePolicyValidationJson(PolicyDocument policy)
    {
        WriteObject(new JsonObject
        {
            ["kind"] = "policy-validation",
            ["tool"] = "DriftGuard",
            ["version"] = BuildInfo.Version,
            ["schemaVersion"] = 1,
            ["valid"] = true,
            ["policyVersion"] = policy.Version,
            ["ruleCount"] = policy.Rules.Count,
            ["severityCounts"] = new JsonObject
            {
                ["info"] = policy.Rules.Count(rule => rule.Severity.Equals("info", StringComparison.OrdinalIgnoreCase)),
                ["warning"] = policy.Rules.Count(rule => rule.Severity.Equals("warning", StringComparison.OrdinalIgnoreCase)),
                ["error"] = policy.Rules.Count(rule => rule.Severity.Equals("error", StringComparison.OrdinalIgnoreCase))
            }
        });
    }

    internal static string Operation(ChangeKind change) => change switch
    {
        ChangeKind.TypeChanged => "type-changed",
        _ => change.ToString().ToLowerInvariant()
    };

    internal static void WriteObject(JsonObject value) => Console.WriteLine(value.ToJsonString(PrettyJson));

    // Prevent untrusted file paths, JSON keys and policy descriptions from inserting
    // escape sequences or forged extra lines into terminal/CI output.
    internal static string SafeConsole(string value)
    {
        var output = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (char.IsControl(character) || character is '\u2028' or '\u2029' ||
                char.GetUnicodeCategory(character) == System.Globalization.UnicodeCategory.Format)
                output.Append("\\u").Append(((int)character).ToString("X4"));
            else
                output.Append(character);
        }
        return output.ToString();
    }

    private static void SetColor(ConsoleColor color)
    {
        if (!Console.IsOutputRedirected)
            Console.ForegroundColor = color;
    }

    private static void ResetColor()
    {
        if (!Console.IsOutputRedirected)
            Console.ResetColor();
    }
}
