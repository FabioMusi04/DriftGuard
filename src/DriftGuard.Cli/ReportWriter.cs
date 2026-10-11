using System.Text.Json;
using System.Text.Json.Nodes;
using DriftGuard.Core;

namespace DriftGuard.Cli;

internal static class ReportWriter
{
    public static void WriteText(
        string baselinePath,
        string currentPath,
        IReadOnlyList<ConfigChange> changes,
        IReadOnlyList<PolicyFinding> findings,
        string failOn,
        int exitCode)
    {
        Console.WriteLine("DriftGuard · semantic configuration diff");
        Console.WriteLine($"Baseline: {baselinePath}");
        Console.WriteLine($"Current:  {currentPath}");
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
            WriteChanges(changes);
        }

        Console.WriteLine();
        WriteFindings(findings);
        Console.WriteLine();
        var failed = exitCode != 0;
        SetColor(failed ? ConsoleColor.Red : ConsoleColor.Green);
        Console.WriteLine(failed
            ? $"POLICY CHECK FAILED (threshold: {failOn}). Exit code: {exitCode}"
            : $"POLICY CHECK PASSED (threshold: {failOn}). Exit code: {exitCode}");
        ResetColor();
    }

    public static void WriteChanges(IReadOnlyList<ConfigChange> changes)
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
            Console.WriteLine($"{symbol} [{change.Kind}] {change.Path}");
            ResetColor();
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
            Console.WriteLine($"  [{finding.Severity.ToString().ToUpperInvariant()}] {finding.RuleId} — {finding.Path}");
            ResetColor();
            Console.WriteLine($"    {finding.Description}");
        }
    }

    public static void WriteJson(
        string baselinePath,
        string currentPath,
        IReadOnlyList<ConfigChange> changes,
        IReadOnlyList<PolicyFinding> findings,
        string failOn,
        int exitCode,
        string? explainPath = null)
    {
        var changeRows = new JsonArray();
        foreach (var change in changes)
        {
            var row = new JsonObject
            {
                ["path"] = change.Path,
                ["operation"] = change.Kind.ToString().ToLowerInvariant().Replace("typechanged", "type-changed", StringComparison.Ordinal),
            };
            if (change.Kind is ChangeKind.Removed or ChangeKind.Changed or ChangeKind.TypeChanged)
                row["baselineValue"] = ValueRedactor.SafeNode(change.BaselineValue, change.Path);
            if (change.Kind is ChangeKind.Added or ChangeKind.Changed or ChangeKind.TypeChanged)
                row["currentValue"] = ValueRedactor.SafeNode(change.CurrentValue, change.Path);
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
                ["operation"] = finding.ChangeKind.ToString().ToLowerInvariant().Replace("typechanged", "type-changed", StringComparison.Ordinal)
            });
        }

        var report = new JsonObject
        {
            ["tool"] = "DriftGuard",
            ["version"] = "0.1.0",
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
        Console.WriteLine(report.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
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
