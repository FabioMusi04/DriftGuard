using System.Text.Json;

namespace DriftGuard.Core;

public sealed class PolicyDocument
{
    public required int Version { get; set; }
    public required List<PolicyRule> Rules { get; set; }
}

public sealed class PolicyRule
{
    public required string Id { get; set; }
    public required string Description { get; set; }
    public string Severity { get; set; } = "warning";
    public string Operation { get; set; } = "any";
    public required string Path { get; set; }
    public JsonElement? NewValueEquals { get; set; }
}
