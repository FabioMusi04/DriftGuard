using System.Text.Json.Nodes;
using DriftGuard.Core;
using Xunit;

namespace DriftGuard.Core.Tests;

public sealed class DiffEngineTests
{
    private readonly DiffEngine _engine = new();

    [Fact]
    public void ObjectPropertyOrderingAndFormattingDoNotProduceDrift()
    {
        var baseline = ConfigParser.Parse("""{ "name": "api", "logging": { "debug": false, "level": "Info" } }""", ".json");
        var current = ConfigParser.Parse("""
            {
              "logging": { "level": "Info", "debug": false },
              "name": "api"
            }
            """, ".json");

        Assert.Empty(_engine.Compare(baseline, current));
    }

    [Fact]
    public void EquivalentJsonAndYamlValuesDoNotProduceDrift()
    {
        var json = ConfigParser.Parse("""{"service":"api","port":8080,"enabled":true,"tags":["one","two"]}""", ".json");
        var yaml = ConfigParser.Parse("""
            service: api
            port: 8080
            enabled: true
            tags:
              - one
              - two
            """, ".yaml");

        Assert.Empty(_engine.Compare(json, yaml));
    }

    [Fact]
    public void DetectsAddedRemovedChangedAndTypeChangedValues()
    {
        var baseline = JsonNode.Parse("""{"keep":"old","remove":true,"port":"5432","items":["a"]}""")!;
        var current = JsonNode.Parse("""{"keep":"new","add":7,"port":5432,"items":["a","b"]}""")!;

        var changes = _engine.Compare(baseline, current);

        Assert.Contains(changes, change => change.Path == "$.add" && change.Kind == ChangeKind.Added);
        Assert.Contains(changes, change => change.Path == "$.remove" && change.Kind == ChangeKind.Removed);
        Assert.Contains(changes, change => change.Path == "$.keep" && change.Kind == ChangeKind.Changed);
        Assert.Contains(changes, change => change.Path == "$.port" && change.Kind == ChangeKind.TypeChanged);
        Assert.Contains(changes, change => change.Path == "$.items[1]" && change.Kind == ChangeKind.Added);
        Assert.Equal(5, changes.Count);
    }

    [Fact]
    public void ReportsChangesInDeterministicPathOrder()
    {
        var baseline = JsonNode.Parse("""{"z":1,"a":1,"m":1}""")!;
        var current = JsonNode.Parse("""{"z":2,"a":2,"m":2}""")!;

        var paths = _engine.Compare(baseline, current).Select(change => change.Path).ToArray();

        Assert.Equal(new[] { "$.a", "$.m", "$.z" }, paths);
    }
}
