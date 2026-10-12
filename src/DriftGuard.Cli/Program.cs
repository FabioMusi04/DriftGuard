using System.Text;
using DriftGuard.Core;

namespace DriftGuard.Cli;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0 || args[0] is "--help" or "-h" or "help")
            {
                HelpPrinter.Print();
                return 0;
            }

            var command = args[0].ToLowerInvariant();
            return command switch
            {
                "compare" => RunComparison(args[1..], isCheck: false),
                "check" => RunComparison(args[1..], isCheck: true),
                "explain" => RunExplain(args[1..]),
                "validate-policy" => RunValidatePolicy(args[1..]),
                "init" => RunInit(args[1..]),
                "version" or "--version" => PrintVersion(args[1..]),
                _ => throw new CliUsageException($"Unknown command '{command}'.")
            };
        }
        catch (Exception ex) when (ex is ConfigParseException or IOException or UnauthorizedAccessException or CliUsageException)
        {
            return HandleError(ex, args);
        }
        catch (Exception)
        {
            // Never print stack traces or raw config/policy content on unexpected failures.
            return HandleError(new CliInternalException(), args);
        }
    }

    private static int HandleError(Exception error, string[] arguments)
    {
        var (code, message) = error switch
        {
            ConfigParseException => ("invalid-input", error.Message),
            IOException => ("io-error", "I/O operation failed: " + error.Message),
            UnauthorizedAccessException => ("access-denied", "Access denied: " + error.Message),
            CliUsageException => ("usage-error", error.Message),
            _ => ("internal-error", "Unexpected failure; see troubleshooting documentation.")
        };

        if (Arguments.RequestedJson(arguments))
        {
            // A privacy-minimized CI artifact must not accidentally contain source paths or
            // snippets from parser exception messages when parsing fails.
            if (Arguments.RequestedMinimalError(arguments))
                message = code switch
                {
                    "invalid-input" => "Invalid input or policy. Re-run locally without privacy flags for details.",
                    "usage-error" => "Invalid command-line arguments. Re-run locally without privacy flags for details.",
                    "io-error" or "access-denied" => "Unable to read an input file. Re-run locally without privacy flags for details.",
                    _ => "Unexpected failure. See troubleshooting documentation."
                };
            ReportWriter.WriteErrorJson(code, message);
        }
        else
        {
            Console.Error.WriteLine($"error: {ReportWriter.SafeConsole(message)}");
            if (error is CliUsageException)
                Console.Error.WriteLine("Run 'driftguard --help' for usage.");
        }
        return 2;
    }

    private sealed class CliInternalException : Exception { }

    private static int RunComparison(string[] args, bool isCheck)
    {
        var parsed = Arguments.Parse(args);
        if (parsed.Options.ContainsKey("help"))
        {
            HelpPrinter.Print(isCheck ? "check" : "compare");
            return 0;
        }

        Arguments.AllowOnly(parsed, "help", "baseline", "current", "policy", "format", "fail-on", "values", "source-names");
        var (baselinePath, currentPath) = ReadPairPaths(parsed);
        var format = GetFormat(parsed, allowSarif: true);
        var values = GetValues(parsed);
        var sourceNames = GetSourceNames(parsed);
        if (format == "sarif" && sourceNames == "none")
            throw new CliUsageException("SARIF needs --source-names full to identify the analyzed file.");

        var failOn = (GetOption(parsed, "fail-on") ?? (isCheck ? "error" : "none")).ToLowerInvariant();
        if (failOn is not ("none" or "warning" or "error"))
            throw new CliUsageException("--fail-on must be 'none', 'warning', or 'error'.");

        var policyPath = GetOption(parsed, "policy");
        if (isCheck && policyPath is null)
            throw new CliUsageException("check requires --policy so a CI run cannot silently pass without rules.");
        if (policyPath is null && failOn != "none")
            throw new CliUsageException("--fail-on warning/error requires --policy; otherwise the check would silently pass.");

        // A malformed policy must fail before a report is emitted; exit 2 is distinct from a policy violation.
        var policy = policyPath is null ? null : PolicyLoader.LoadFile(policyPath);
        var baseline = ConfigParser.ParseFile(baselinePath);
        var current = ConfigParser.ParseFile(currentPath);
        var changes = new DiffEngine().Compare(baseline, current);
        var findings = policy is null
            ? Array.Empty<PolicyFinding>()
            : new PolicyEngine().Evaluate(policy, changes).ToArray();

        var failed = failOn switch
        {
            "warning" => findings.Any(finding => finding.Severity >= Severity.Warning),
            "error" => findings.Any(finding => finding.Severity >= Severity.Error),
            _ => false
        };
        var exitCode = failed ? 1 : 0;
        var reportBaseline = sourceNames == "none" ? "[omitted]" : baselinePath;
        var reportCurrent = sourceNames == "none" ? "[omitted]" : currentPath;

        switch (format)
        {
            case "json":
                ReportWriter.WriteJson(reportBaseline, reportCurrent, changes, findings, failOn, exitCode,
                    includeValues: values == "redacted", policyApplied: policy is not null);
                break;
            case "sarif":
                SarifWriter.Write(currentPath, findings);
                break;
            default:
                ReportWriter.WriteText(reportBaseline, reportCurrent, changes, findings, failOn, exitCode,
                    policyApplied: policy is not null, includeValues: values == "redacted");
                break;
        }
        return exitCode;
    }

    private static int RunExplain(string[] args)
    {
        var parsed = Arguments.Parse(args);
        if (parsed.Options.ContainsKey("help"))
        {
            HelpPrinter.Print("explain");
            return 0;
        }

        Arguments.AllowOnly(parsed, "help", "baseline", "current", "path", "policy", "format", "values", "source-names");
        string baselinePath;
        string currentPath;
        string path;
        if (parsed.Positionals.Count == 3 && !parsed.Options.ContainsKey("baseline") &&
            !parsed.Options.ContainsKey("current") && !parsed.Options.ContainsKey("path"))
        {
            baselinePath = parsed.Positionals[0];
            currentPath = parsed.Positionals[1];
            path = parsed.Positionals[2];
        }
        else
        {
            baselinePath = RequiredOption(parsed, "baseline");
            currentPath = RequiredOption(parsed, "current");
            path = RequiredOption(parsed, "path");
            if (parsed.Positionals.Count != 0)
                throw new CliUsageException("Use either positional arguments or --baseline/--current/--path.");
        }

        JsonPath.ValidateExactPath(path);
        var format = GetFormat(parsed, allowSarif: false);
        var values = GetValues(parsed);
        var sourceNames = GetSourceNames(parsed);
        var policyPath = GetOption(parsed, "policy");
        var policy = policyPath is null ? null : PolicyLoader.LoadFile(policyPath);
        var baseline = ConfigParser.ParseFile(baselinePath);
        var current = ConfigParser.ParseFile(currentPath);
        var changes = new DiffEngine().Compare(baseline, current);
        var relevant = changes.Where(change => IsSameOrBelow(change.Path, path)).ToArray();
        var findings = policy is null
            ? Array.Empty<PolicyFinding>()
            : new PolicyEngine().Evaluate(policy, relevant).ToArray();
        var reportBaseline = sourceNames == "none" ? "[omitted]" : baselinePath;
        var reportCurrent = sourceNames == "none" ? "[omitted]" : currentPath;

        if (format == "json")
        {
            ReportWriter.WriteJson(reportBaseline, reportCurrent, relevant, findings, "none", 0, path,
                includeValues: values == "redacted", policyApplied: policy is not null);
            return 0;
        }

        Console.WriteLine($"DriftGuard explanation: {ReportWriter.SafeConsole(path)}");
        Console.WriteLine($"Baseline: {ReportWriter.SafeConsole(reportBaseline)}");
        Console.WriteLine($"Current:  {ReportWriter.SafeConsole(reportCurrent)}");
        if (relevant.Length == 0)
            Console.WriteLine("No semantic changes found at this path or below it.");
        else
            ReportWriter.WriteChanges(relevant, includeValues: values == "redacted");
        ReportWriter.WriteFindings(findings);
        return 0;
    }

    private static int RunValidatePolicy(string[] args)
    {
        var parsed = Arguments.Parse(args);
        if (parsed.Options.ContainsKey("help"))
        {
            HelpPrinter.Print("validate-policy");
            return 0;
        }
        Arguments.AllowOnly(parsed, "help", "policy", "format");
        var format = GetFormat(parsed, allowSarif: false);
        string path;
        if (parsed.Positionals.Count == 1 && !parsed.Options.ContainsKey("policy"))
            path = parsed.Positionals[0];
        else if (parsed.Positionals.Count == 0)
            path = RequiredOption(parsed, "policy");
        else
            throw new CliUsageException("Provide one policy path or --policy file, not both.");

        var policy = PolicyLoader.LoadFile(path);
        if (format == "json")
            ReportWriter.WritePolicyValidationJson(policy);
        else
            Console.WriteLine($"Policy valid: {policy.Rules.Count} rule(s), version {policy.Version}.");
        return 0;
    }

    private static int RunInit(string[] args)
    {
        var parsed = Arguments.Parse(args);
        Arguments.AllowOnly(parsed, "help", "path", "force");
        if (parsed.Options.ContainsKey("help"))
        {
            HelpPrinter.Print("init");
            return 0;
        }
        if (parsed.Positionals.Count != 0)
            throw new CliUsageException("init accepts options only.");
        var path = GetOption(parsed, "path") ?? "driftguard.policy.json";
        var force = parsed.Options.ContainsKey("force");
        var fullPath = Path.GetFullPath(path);
        if (File.Exists(fullPath) && !force)
            throw new CliUsageException($"File already exists: {fullPath}. Pass --force to overwrite it.");

        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        // Files generated for Windows users default to BOM/CRLF, matching the project convention.
        File.WriteAllText(fullPath, PolicyTemplates.ProductionPolicy.ReplaceLineEndings("\r\n") + "\r\n", new UTF8Encoding(true));
        Console.WriteLine($"Created policy template: {ReportWriter.SafeConsole(fullPath)}");
        Console.WriteLine("Review the example paths and values before using it to gate a real deployment.");
        return 0;
    }

    private static (string Baseline, string Current) ReadPairPaths(ParsedArguments parsed)
    {
        if (parsed.Positionals.Count == 2 && !parsed.Options.ContainsKey("baseline") && !parsed.Options.ContainsKey("current"))
            return (parsed.Positionals[0], parsed.Positionals[1]);
        if (parsed.Positionals.Count == 0)
            return (RequiredOption(parsed, "baseline"), RequiredOption(parsed, "current"));
        throw new CliUsageException("Provide baseline and current as two positional paths, or use --baseline and --current.");
    }

    private static string GetFormat(ParsedArguments parsed, bool allowSarif)
    {
        var format = (GetOption(parsed, "format") ?? "text").ToLowerInvariant();
        if (format is "text" or "json" || allowSarif && format == "sarif")
            return format;
        throw new CliUsageException(allowSarif
            ? "--format must be 'text', 'json', or 'sarif'."
            : "--format must be 'text' or 'json'.");
    }

    private static string GetValues(ParsedArguments parsed)
    {
        var values = (GetOption(parsed, "values") ?? "redacted").ToLowerInvariant();
        if (values is not ("none" or "redacted"))
            throw new CliUsageException("--values must be 'redacted' or 'none'.");
        return values;
    }

    private static string GetSourceNames(ParsedArguments parsed)
    {
        var sourceNames = (GetOption(parsed, "source-names") ?? "full").ToLowerInvariant();
        if (sourceNames is not ("full" or "none"))
            throw new CliUsageException("--source-names must be 'full' or 'none'.");
        return sourceNames;
    }

    private static string RequiredOption(ParsedArguments parsed, string name) =>
        GetOption(parsed, name) ?? throw new CliUsageException($"Missing required option --{name}.");

    private static string? GetOption(ParsedArguments parsed, string name) =>
        parsed.Options.TryGetValue(name, out var value) ? value : null;

    private static bool IsSameOrBelow(string candidate, string path) =>
        candidate.Equals(path, StringComparison.Ordinal) ||
        candidate.StartsWith(path + ".", StringComparison.Ordinal) ||
        candidate.StartsWith(path + "[", StringComparison.Ordinal) ||
        path.StartsWith(candidate + ".", StringComparison.Ordinal) ||
        path.StartsWith(candidate + "[", StringComparison.Ordinal);

    private static int PrintVersion(string[] args)
    {
        if (args.Length != 0)
            throw new CliUsageException("version does not accept additional arguments.");
        Console.WriteLine($"DriftGuard {BuildInfo.Version}");
        return 0;
    }
}
