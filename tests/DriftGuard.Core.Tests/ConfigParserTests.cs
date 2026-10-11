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
}
