using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace DriftGuard.Cli.Tests;

public sealed class CliProcessTests
{
    [Fact]
    public async Task MinimalJsonReportMatchesGoldenFixture()
    {
        var result = await RunAsync("compare", "scenarios/08-safe-addition/baseline.json",
            "scenarios/08-safe-addition/current.json", "--format", "json", "--values", "none", "--source-names", "none");
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.Stderr);
        var golden = File.ReadAllText(Path.Combine(Root, "tests", "DriftGuard.Cli.Tests", "Fixtures", "compare-minimal.v1.json"));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(golden), JsonNode.Parse(result.Stdout)),
            $"The JSON report no longer matches its golden contract. Actual:\n{result.Stdout}");
    }

    [Fact]
    public async Task MinimalReportsNeverContainOldOrNewValues()
    {
        var result = await RunAsync("check", "scenarios/15-root-password/baseline.json",
            "scenarios/15-root-password/current.json", "--policy", "policies/production.json",
            "--format", "json", "--values", "none", "--source-names", "none", "--fail-on", "warning");
        Assert.Equal(1, result.ExitCode);
        using var document = JsonDocument.Parse(result.Stdout);
        var root = document.RootElement;
        Assert.True(root.GetProperty("policyApplied").GetBoolean());
        Assert.Equal("[omitted]", root.GetProperty("baseline").GetString());
        Assert.Equal("[omitted]", root.GetProperty("current").GetString());
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        foreach (var change in root.GetProperty("changes").EnumerateArray())
        {
            Assert.False(change.TryGetProperty("baselineValue", out _));
            Assert.False(change.TryGetProperty("currentValue", out _));
        }
        Assert.DoesNotContain("EXAMPLE_OLD_ROOT_PASSWORD_NEVER_USE", result.Stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("EXAMPLE_NEW_ROOT_PASSWORD_NEVER_USE", result.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidatePolicyAcceptsValidFile()
    {
        var result = await RunAsync("validate-policy", "policies/production.json", "--format", "json");
        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.Stdout);
        Assert.Equal("policy-validation", document.RootElement.GetProperty("kind").GetString());
        Assert.True(document.RootElement.GetProperty("valid").GetBoolean());
        Assert.True(document.RootElement.GetProperty("ruleCount").GetInt32() > 0);
    }

    [Fact]
    public async Task InvalidPolicyUsesStructuredJsonError()
    {
        var result = await RunAsync("validate-policy", "missing-policy-file.json", "--format", "json");
        Assert.Equal(2, result.ExitCode);
        Assert.Equal(string.Empty, result.Stderr);
        using var document = JsonDocument.Parse(result.Stdout);
        Assert.Equal("error", document.RootElement.GetProperty("kind").GetString());
        Assert.Equal("invalid-input", document.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(2, document.RootElement.GetProperty("exitCode").GetInt32());
    }

    [Fact]
    public async Task MinimalJsonErrorOmitsFilePathsAndPotentialParserSnippets()
    {
        var result = await RunAsync("compare", "private-identifier/secret.json", "other-private-file.json",
            "--format", "json", "--values", "none", "--source-names", "none");
        Assert.Equal(2, result.ExitCode);
        using var document = JsonDocument.Parse(result.Stdout);
        Assert.Equal("invalid-input", document.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.DoesNotContain("private-identifier", result.Stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("secret.json", result.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ParseErrorsAndUsageErrorsKeepExitTwo()
    {
        var malformed = await RunAsync("compare", "scenarios/16-duplicate-json/baseline.json",
            "scenarios/16-duplicate-json/current.json", "--format", "json");
        Assert.Equal(2, malformed.ExitCode);
        using var malformedJson = JsonDocument.Parse(malformed.Stdout);
        Assert.Equal("invalid-input", malformedJson.RootElement.GetProperty("error").GetProperty("code").GetString());

        var badArgs = await RunAsync("compare", "--format", "json", "--values", "invalid");
        Assert.Equal(2, badArgs.ExitCode);
        using var badArgsJson = JsonDocument.Parse(badArgs.Stdout);
        Assert.Equal("usage-error", badArgsJson.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task SarifHasStableFingerprintsAndNoConfigurationValues()
    {
        var arguments = new[] { "check", "scenarios/02-debug-enabled/baseline.json",
            "scenarios/02-debug-enabled/current.json", "--policy", "policies/production.json", "--format", "sarif" };
        var first = await RunAsync(arguments);
        var second = await RunAsync(arguments);
        Assert.Equal(1, first.ExitCode);
        Assert.Equal(first.Stdout, second.Stdout);
        using var document = JsonDocument.Parse(first.Stdout);
        Assert.Equal("2.1.0", document.RootElement.GetProperty("version").GetString());
        var finding = Assert.Single(document.RootElement.GetProperty("runs")[0].GetProperty("results").EnumerateArray());
        Assert.Equal("error", finding.GetProperty("level").GetString());
        Assert.True(finding.GetProperty("partialFingerprints").TryGetProperty("primaryLocationLineHash", out _));
        Assert.Equal(1, finding.GetProperty("locations")[0].GetProperty("physicalLocation")
            .GetProperty("region").GetProperty("startLine").GetInt32());
        Assert.DoesNotContain("debug\":true", first.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InitGeneratesWindowsUtf8BomAndCrlf()
    {
        var filename = Path.Combine(Path.GetTempPath(), $"driftguard-{Guid.NewGuid():N}.json");
        try
        {
            var result = await RunAsync("init", "--path", filename);
            Assert.Equal(0, result.ExitCode);
            var bytes = File.ReadAllBytes(filename);
            Assert.True(bytes.Length > 3);
            Assert.Equal((byte)0xEF, bytes[0]);
            Assert.Equal((byte)0xBB, bytes[1]);
            Assert.Equal((byte)0xBF, bytes[2]);
            for (var i = 3; i < bytes.Length; i++)
            {
                if (bytes[i] == (byte)'\n')
                    Assert.Equal((byte)'\r', bytes[i - 1]);
                if (bytes[i] == (byte)'\r')
                    Assert.Equal((byte)'\n', bytes[i + 1]);
            }
        }
        finally
        {
            if (File.Exists(filename))
                File.Delete(filename);
        }
    }

    [Fact]
    public async Task TextModeHidesValuesWhenRequested()
    {
        var result = await RunAsync("compare", "scenarios/08-safe-addition/baseline.json",
            "scenarios/08-safe-addition/current.json", "--values", "none");
        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("after:", result.Stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("before:", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("$.features.analyticsEnabled", result.Stdout, StringComparison.Ordinal);
    }

    private static readonly string Root = FindRepositoryRoot();

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "DriftGuard.sln")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate DriftGuard.sln from the test output folder.");
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(params string[] cliArguments)
    {
        var testOutputDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        var configuration = testOutputDirectory.Parent?.Name ?? "Debug";
        var cliDll = Path.Combine(Root, "src", "DriftGuard.Cli", "bin", configuration, "net10.0", "driftguard.dll");
        Assert.True(File.Exists(cliDll), $"CLI build output missing: {cliDll}");

        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false),
            UseShellExecute = false
        };
        start.ArgumentList.Add(cliDll);
        foreach (var argument in cliArguments)
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start dotnet process.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            throw new TimeoutException("CLI process did not finish within 45 seconds.");
        }
        return (process.ExitCode, await stdout, await stderr);
    }
}
