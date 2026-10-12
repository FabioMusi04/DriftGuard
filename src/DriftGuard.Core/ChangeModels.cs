using System.Text.Json.Nodes;

namespace DriftGuard.Core;

public enum ChangeKind
{
    Added,
    Removed,
    Changed,
    TypeChanged
}

public sealed record ConfigChange(
    string Path,
    ChangeKind Kind,
    JsonNode? BaselineValue,
    JsonNode? CurrentValue);

public enum Severity
{
    Info = 0,
    Warning = 1,
    Error = 2
}

public sealed record PolicyFinding(
    string RuleId,
    string Description,
    string Path,
    Severity Severity,
    ChangeKind ChangeKind);
