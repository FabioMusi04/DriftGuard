using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using DriftGuard.Core;

namespace DriftGuard.Cli;

/// <summary>
/// SARIF 2.1.0 for GitHub code scanning. Configuration paths do not carry source line
/// information, so findings are attributed to the analyzed file at line 1.
/// </summary>
internal static class SarifWriter
{
    public static void Write(string currentFile, IReadOnlyList<PolicyFinding> findings)
    {
        var uri = ToRepositoryUri(currentFile);
        var rules = new JsonArray();
        foreach (var finding in findings.DistinctBy(f => f.RuleId).OrderBy(f => f.RuleId, StringComparer.Ordinal))
        {
            rules.Add(new JsonObject
            {
                ["id"] = finding.RuleId,
                ["shortDescription"] = new JsonObject { ["text"] = finding.RuleId },
                ["fullDescription"] = new JsonObject { ["text"] = finding.Description },
                ["defaultConfiguration"] = new JsonObject { ["level"] = Level(finding.Severity) }
            });
        }

        var results = new JsonArray();
        foreach (var finding in findings)
        {
            results.Add(new JsonObject
            {
                ["ruleId"] = finding.RuleId,
                ["level"] = Level(finding.Severity),
                ["message"] = new JsonObject { ["text"] = finding.Description },
                ["partialFingerprints"] = new JsonObject
                {
                    ["driftguardFinding/v1"] = Fingerprint(uri, finding),
                    ["primaryLocationLineHash"] = Fingerprint(uri, finding) + ":1"
                },
                ["locations"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["physicalLocation"] = new JsonObject
                        {
                            ["artifactLocation"] = new JsonObject { ["uri"] = uri },
                            ["region"] = new JsonObject { ["startLine"] = 1 }
                        },
                        ["logicalLocations"] = new JsonArray
                        {
                            new JsonObject { ["name"] = finding.Path }
                        }
                    }
                },
                ["properties"] = new JsonObject
                {
                    ["configPath"] = finding.Path,
                    ["operation"] = ReportWriter.Operation(finding.ChangeKind)
                }
            });
        }

        ReportWriter.WriteObject(new JsonObject
        {
            ["$schema"] = "https://json.schemastore.org/sarif-2.1.0.json",
            ["version"] = "2.1.0",
            ["runs"] = new JsonArray
            {
                new JsonObject
                {
                    ["tool"] = new JsonObject
                    {
                        ["driver"] = new JsonObject
                        {
                            ["name"] = "DriftGuard",
                            ["version"] = BuildInfo.Version,
                            ["rules"] = rules
                        }
                    },
                    ["results"] = results
                }
            }
        });
    }

    private static string Fingerprint(string uri, PolicyFinding finding)
    {
        var canonical = string.Join("\u0000", uri, finding.RuleId, finding.Path, ReportWriter.Operation(finding.ChangeKind));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(hash).ToLowerInvariant()[..32];
    }

    private static string Level(Severity severity) => severity switch
    {
        Severity.Error => "error",
        Severity.Warning => "warning",
        _ => "note"
    };

    private static string ToRepositoryUri(string path)
    {
        // GitHub accepts repository-relative SARIF artifact URIs. Never emit local absolute
        // workstation paths. Callers running outside a checkout can still review a file-level alert.
        var normalized = path.Replace('\\', '/');
        var driveQualified = normalized.Length >= 3 && char.IsLetter(normalized[0]) &&
            normalized[1] == ':' && normalized[2] == '/';
        if (Path.IsPathFullyQualified(path) || driveQualified ||
            normalized.StartsWith("../", StringComparison.Ordinal) || normalized.Contains("/../", StringComparison.Ordinal))
            normalized = normalized[(normalized.LastIndexOf('/') + 1)..];
        if (normalized.StartsWith("./", StringComparison.Ordinal))
            normalized = normalized[2..];
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return string.Join("/", segments.Select(Uri.EscapeDataString));
    }
}
