using System.Text.Json;
using System.Text.Json.Nodes;
using DriftGuard.Core;
using Xunit;

namespace DriftGuard.Core.Tests;

public sealed class PolicyRegressionTests
{
    [Fact]
    public void AddedParentCannotHideADangerousNestedSetting()
    {
        var baseline = JsonNode.Parse("{}")!;
        var current = JsonNode.Parse("""{"logging":{"debug":true}}""")!;
        var changes = new DiffEngine().Compare(baseline, current);
        Assert.Equal("$.logging", Assert.Single(changes).Path); // compact diff stays stable

        var finding = Assert.Single(new PolicyEngine().Evaluate(
            Policy("$.logging.debug", "added", "true"), changes));
        Assert.Equal("$.logging.debug", finding.Path);
        Assert.Equal(ChangeKind.Added, finding.ChangeKind);
    }

    [Fact]
    public void RemovingParentCannotHideARemovedCriticalFeature()
    {
        var baseline = JsonNode.Parse("""{"features":{"billingEnabled":true,"other":false}}""")!;
        var current = JsonNode.Parse("{}")!;
        var changes = new DiffEngine().Compare(baseline, current);

        var finding = Assert.Single(new PolicyEngine().Evaluate(
            Policy("$.features.billingEnabled", "removed"), changes));
        Assert.Equal("$.features.billingEnabled", finding.Path);
        Assert.Equal(ChangeKind.Removed, finding.ChangeKind);
    }

    [Fact]
    public void RecursiveWildcardMatchesZeroOrSeveralLevels()
    {
        var baseline = JsonNode.Parse("""{"password":"old","services":{"api":{"password":"old"}}}""")!;
        var current = JsonNode.Parse("""{"password":"new","services":{"api":{"password":"new"}}}""")!;
        var changes = new DiffEngine().Compare(baseline, current);

        var findings = new PolicyEngine().Evaluate(Policy("$.**.password", "changed"), changes);
        Assert.Equal(2, findings.Count);
        Assert.Contains(findings, f => f.Path == "$.password");
        Assert.Contains(findings, f => f.Path == "$.services.api.password");
    }

    [Fact]
    public void WildcardsMatchBracketQuotedKeysButExactSelectorsRemainLiteral()
    {
        var baseline = JsonNode.Parse("""{"config":{"db.host":"before","apiKey":"old"}}""")!;
        var current = JsonNode.Parse("""{"config":{"db.host":"after","apiKey":"new"}}""")!;
        var changes = new DiffEngine().Compare(baseline, current);
        var specific = new PolicyEngine().Evaluate(Policy("$.config['db.host']", "changed"), changes);
        Assert.Equal("$.config['db.host']", Assert.Single(specific).Path);

        var wildcard = new PolicyEngine().Evaluate(Policy("$.config.*", "changed"), changes);
        Assert.Equal(2, wildcard.Count);
    }

    [Fact]
    public void EscapesBackslashesAndQuotesInPathsWithoutCollisions()
    {
        const string key = "name\\'value";
        var path = JsonPath.AppendProperty("$.settings", key);
        Assert.Equal("$.settings['name" + "\\\\" + "\\'" + "value']", path);
        JsonPath.ValidateExactPath(path);
        var baseline = new JsonObject { ["settings"] = new JsonObject { [key] = false } };
        var current = new JsonObject { ["settings"] = new JsonObject { [key] = true } };
        var changes = new DiffEngine().Compare(baseline, current);
        Assert.Equal(path, Assert.Single(changes).Path);
        Assert.Single(new PolicyEngine().Evaluate(Policy(path, "changed", "true"), changes));
    }

    [Fact]
    public void ScalarToObjectCannotHideNewChildValues()
    {
        var baseline = JsonNode.Parse("""{"logging":false}""")!;
        var current = JsonNode.Parse("""{"logging":{"debug":true}}""")!;
        var changes = new DiffEngine().Compare(baseline, current);
        Assert.Equal(ChangeKind.TypeChanged, Assert.Single(changes).Kind);

        Assert.Single(new PolicyEngine().Evaluate(Policy("$.logging.debug", "added", "true"), changes));
    }

    [Fact]
    public void RemovedPropertyDoesNotSatisfyExplicitNull()
    {
        var baseline = JsonNode.Parse("""{"foo":null}""")!;
        var current = JsonNode.Parse("{}")!;
        var changes = new DiffEngine().Compare(baseline, current);
        Assert.Empty(new PolicyEngine().Evaluate(Policy("$.foo", "removed", "null"), changes));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("999")]
    public void RejectsNumericSeverityValues(string invalid)
    {
        var policy = Policy("$.service.enabled", "any");
        policy.Rules[0].Severity = invalid;
        Assert.Throws<ConfigParseException>(() => PolicyLoader.Validate(policy));
    }

    [Fact]
    public void RejectsDuplicatePolicyJsonKeysBeforeDeserialization()
    {
        var filename = Path.Combine(Path.GetTempPath(), $"driftguard-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(filename, """
                {"version":1,"version":1,"rules":[{"id":"x","description":"rule","path":"$.x"}]}
                """);
            Assert.Throws<ConfigParseException>(() => PolicyLoader.LoadFile(filename));
        }
        finally { File.Delete(filename); }
    }

    [Theory]
    [InlineData("$..invalid")]
    [InlineData("$.settings[")]
    [InlineData("$.settings[abc]")]
    [InlineData("$.settings 'foo'")]
    [InlineData("$.settings.foo]")]
    public void RejectsMalformedPolicyPaths(string invalid)
    {
        Assert.Throws<ConfigParseException>(() => PolicyLoader.Validate(Policy(invalid, "any")));
    }

    private static PolicyDocument Policy(string path, string operation, string? expected = null)
    {
        return new PolicyDocument
        {
            Version = 1,
            Rules =
            [
                new PolicyRule
                {
                    Id = "regression-rule",
                    Description = "Regression test rule",
                    Path = path,
                    Operation = operation,
                    Severity = "error",
                    NewValueEquals = ParseExpected(expected)
                }
            ]
        };
    }
    private static JsonElement? ParseExpected(string? json)
    {
        if (json is null)
            return null;
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

}
