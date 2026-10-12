using System.Text.Json.Nodes;
using DriftGuard.Core;
using Xunit;

namespace DriftGuard.Core.Tests;

public sealed class PropertyRegressionTests
{
    [Fact]
    public void SeededNestedChangesCannotBypassRecursivePasswordGlob()
    {
        // Deterministic generated cases exercise different object depths and bracketed keys,
        // without adding a third-party property-testing dependency to the Core project.
        var random = new Random(347194);
        var policy = new PolicyDocument
        {
            Version = 1,
            Rules = [new PolicyRule
            {
                Id = "password-change",
                Description = "Generated path regression",
                Severity = "error",
                Path = "$.**.password",
                Operation = "added"
            }]
        };

        for (var iteration = 0; iteration < 120; iteration++)
        {
            JsonNode subtree = new JsonObject { ["password"] = "sensitive-fixture-abc" };
            var depth = random.Next(0, 12);
            for (var level = 0; level < depth; level++)
            {
                var property = level % 3 == 0 ? $"service.{level}" : $"service-{level}";
                subtree = new JsonObject { [property] = subtree };
            }
            var current = new JsonObject { ["new"] = subtree };
            var changes = new DiffEngine().Compare(new JsonObject(), current);
            var findings = new PolicyEngine().Evaluate(policy, changes);
            Assert.Single(findings);
            Assert.EndsWith(".password", findings[0].Path, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void GeneratedSensitiveSubtreesNeverExposePayloadsThroughValueRedactor()
    {
        var random = new Random(452984);
        for (var iteration = 0; iteration < 200; iteration++)
        {
            var secret = "not-for-output-" + random.NextInt64().ToString("X");
            var original = new JsonObject
            {
                ["nested"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["apiKey"] = secret,
                        ["public"] = iteration
                    }
                }
            };
            var safe = ValueRedactor.SafeJson(original, "$");
            Assert.DoesNotContain(secret, safe, StringComparison.Ordinal);
            Assert.Contains("[REDACTED]", safe, StringComparison.Ordinal);
        }
    }
}
