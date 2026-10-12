using DriftGuard.Core;
using Xunit;

namespace DriftGuard.Core.Tests;

public sealed class ConfigParserTests
{
    [Fact]
    public void ParsesYamlAndPreservesQuotedScalarTypes()
    {
        const string yaml = """
            service:
              port: 8080
              portAsText: "8080"
              enabled: true
              labels:
                - api
                - stable
            """;

        var document = ConfigParser.Parse(yaml, ".yaml");

        Assert.Equal("8080", document["service"]!["port"]!.ToJsonString());
        Assert.Equal("\"8080\"", document["service"]!["portAsText"]!.ToJsonString());
        Assert.Equal("true", document["service"]!["enabled"]!.ToJsonString());
        Assert.Equal(2, document["service"]!["labels"]!.AsArray().Count);
    }

    [Fact]
    public void RejectsNonObjectRoot()
    {
        Assert.Throws<ConfigParseException>(() => ConfigParser.Parse("[1,2,3]", ".json"));
    }

    [Fact]
    public void RejectsUnsupportedExtensions()
    {
        Assert.Throws<ConfigParseException>(() => ConfigParser.Parse("{}", ".toml"));
    }

    [Fact]
    public void RejectsYamlWithMultipleDocuments()
    {
        Assert.Throws<ConfigParseException>(() => ConfigParser.Parse("a: 1\n---\nb: 2\n", ".yaml"));
    }

    [Fact]
    public void RejectsDuplicateJsonPropertiesInNestedObjects()
    {
        Assert.Throws<ConfigParseException>(() =>
            ConfigParser.Parse("""{"logging":{"debug":false,"debug":true}}""", ".json"));
    }

    [Fact]
    public void RejectsYamlAliasesThatExpandTooFar()
    {
        // Exponential alias expansion from a very small untrusted source.
        const string yaml = """
            a: &a [x, x, x, x, x, x, x, x, x, x]
            b: &b [*a, *a, *a, *a, *a, *a, *a, *a, *a, *a]
            c: &c [*b, *b, *b, *b, *b, *b, *b, *b, *b, *b]
            d: &d [*c, *c, *c, *c, *c, *c, *c, *c, *c, *c]
            e: [*d, *d, *d, *d, *d, *d, *d, *d, *d, *d]
            """;

        Assert.Throws<ConfigParseException>(() => ConfigParser.Parse(yaml, ".yaml"));
    }

    [Fact]
    public void RejectsInvalidUtf8InFile()
    {
        var file = Path.Combine(Path.GetTempPath(), $"driftguard-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllBytes(file, [0xC3, 0x28]);
            Assert.Throws<ConfigParseException>(() => ConfigParser.ParseFile(file));
        }
        finally { File.Delete(file); }
    }
}
