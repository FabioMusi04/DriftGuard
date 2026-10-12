using System.Text.Json;
using System.Text.Json.Nodes;
using DriftGuard.Core;
using Xunit;

namespace DriftGuard.Core.Tests;

public sealed class PolicyEngineTests
{
    [Fact]
    public void MatchesGlobOperationAndTypedExpectedValue()
    {
        var baseline = JsonNode.Parse("""{"services":{"worker":{"replicas":3}}}""")!;
        var current = JsonNode.Parse("""{"services":{"worker":{"replicas":1}}}""")!;
        using var expectedJson = JsonDocument.Parse("1");
        var policy = new PolicyDocument
        {
            Version = 1,
            Rules =
            [
                new PolicyRule
                {
                    Id = "single-replica",
                    Description = "Do not go below two replicas.",
                    Severity = "warning",
                    Operation = "changed",
                    Path = "$.services.*.replicas",
                    NewValueEquals = expectedJson.RootElement.Clone()
                }
            ]
        };

        var changes = new DiffEngine().Compare(baseline, current);
        var findings = new PolicyEngine().Evaluate(policy, changes);

        var finding = Assert.Single(findings);
        Assert.Equal("single-replica", finding.RuleId);
        Assert.Equal(Severity.Warning, finding.Severity);
    }

    [Fact]
    public void RedactsSensitivePathsAndSecretLikeValues()
    {
        var config = JsonNode.Parse("""{"database":{"password":"the-real-password","host":"db.local"},"header":"Bearer abc123token"}""")!;

        var safe = ValueRedactor.SafeJson(config, "$", indented: true);

        Assert.DoesNotContain("the-real-password", safe, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123token", safe, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", safe, StringComparison.Ordinal);
        Assert.Contains("db.local", safe, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsDuplicateRuleIds()
    {
        var policy = new PolicyDocument
        {
            Version = 1,
            Rules =
            [
                new PolicyRule { Id = "same", Description = "first", Path = "$" },
                new PolicyRule { Id = "same", Description = "second", Path = "$" }
            ]
        };

        Assert.Throws<ConfigParseException>(() => PolicyLoader.Validate(policy));
    }
}
