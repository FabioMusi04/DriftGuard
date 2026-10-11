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
                "init" => RunInit(args[1..]),
                "version" or "--version" => PrintVersion(),
                _ => UnknownCommand(command)
            };
        }
        catch (ConfigParseException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 2;
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"error: I/O operation failed: {ex.Message}");
            return 2;
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.Error.WriteLine($"error: access denied: {ex.Message}");
            return 2;
        }
        catch (CliUsageException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            Console.Error.WriteLine("Run 'driftguard --help' for usage.");
            return 2;
        }
        catch (Exception ex)
        {
            // No stack traces by default: configuration tooling should not dump file contents or internals.
            Console.Error.WriteLine($"error: unexpected failure ({ex.GetType().Name}): {ex.Message}");
            return 2;
        }
    }

    private static int RunComparison(string[] args, bool isCheck)
    {
        var parsed = Arguments.Parse(args);
        if (parsed.Options.ContainsKey("help"))
        {
            HelpPrinter.Print(isCheck ? "check" : "compare");
            return 0;
        }

        var (baselinePath, currentPath) = ReadPairPaths(parsed, expectedPositionals: 2);
        var format = GetOption(parsed, "format") ?? "text";
        if (format is not ("text" or "json"))
            throw new CliUsageException("--format must be 'text' or 'json'.");

        var failOn = (GetOption(parsed, "fail-on") ?? (isCheck ? "error" : "none")).ToLowerInvariant();
        if (failOn is not ("none" or "warning" or "error"))
            throw new CliUsageException("--fail-on must be 'none', 'warning', or 'error'.");

        var baseline = ConfigParser.ParseFile(baselinePath);
        var current = ConfigParser.ParseFile(currentPath);
        var changes = new DiffEngine().Compare(baseline, current);
        var policyPath = GetOption(parsed, "policy");
        if (isCheck && policyPath is null)
            throw new CliUsageException("check requires --policy so a CI run cannot silently pass without rules.");
        var findings = policyPath is null
            ? Array.Empty<PolicyFinding>()
            : new PolicyEngine().Evaluate(PolicyLoader.LoadFile(policyPath), changes).ToArray();

        var failed = failOn switch
        {
            "warning" => findings.Any(finding => finding.Severity >= Severity.Warning),
            "error" => findings.Any(finding => finding.Severity >= Severity.Error),
            _ => false
        };
        var exitCode = failed ? 1 : 0;

        if (format == "json")
            ReportWriter.WriteJson(baselinePath, currentPath, changes, findings, failOn, exitCode);
        else
            ReportWriter.WriteText(baselinePath, currentPath, changes, findings, failOn, exitCode);

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

        var baseline = ConfigParser.ParseFile(baselinePath);
        var current = ConfigParser.ParseFile(currentPath);
        var changes = new DiffEngine().Compare(baseline, current);
        var relevant = changes.Where(change => IsSameOrBelow(change.Path, path)).ToArray();
        var policyPath = GetOption(parsed, "policy");
        var findings = policyPath is null
            ? Array.Empty<PolicyFinding>()
            : new PolicyEngine().Evaluate(PolicyLoader.LoadFile(policyPath), relevant).ToArray();
        var format = GetOption(parsed, "format") ?? "text";
        if (format == "json")
        {
            ReportWriter.WriteJson(baselinePath, currentPath, relevant, findings, "none", 0, path);
            return 0;
        }
        if (format != "text")
            throw new CliUsageException("--format must be 'text' or 'json'.");

        Console.WriteLine($"DriftGuard explanation: {path}");
        Console.WriteLine($"Baseline: {baselinePath}");
        Console.WriteLine($"Current:  {currentPath}");
        if (relevant.Length == 0)
            Console.WriteLine("No semantic changes found at this path or below it.");
        else
            ReportWriter.WriteChanges(relevant);
        ReportWriter.WriteFindings(findings);
        return 0;
    }

    private static int RunInit(string[] args)
    {
        var parsed = Arguments.Parse(args);
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
        File.WriteAllText(fullPath, PolicyTemplates.ProductionPolicy, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        Console.WriteLine($"Created policy template: {fullPath}");
        Console.WriteLine("Review the example paths and values before using it to gate a real deployment.");
        return 0;
    }

    private static (string Baseline, string Current) ReadPairPaths(ParsedArguments parsed, int expectedPositionals)
    {
        if (parsed.Positionals.Count == expectedPositionals && !parsed.Options.ContainsKey("baseline") && !parsed.Options.ContainsKey("current"))
            return (parsed.Positionals[0], parsed.Positionals[1]);
        if (parsed.Positionals.Count == 0)
            return (RequiredOption(parsed, "baseline"), RequiredOption(parsed, "current"));
        throw new CliUsageException("Provide baseline and current as two positional paths, or use --baseline and --current.");
    }

    private static string RequiredOption(ParsedArguments parsed, string name) =>
        GetOption(parsed, name) ?? throw new CliUsageException($"Missing required option --{name}.");

    private static string? GetOption(ParsedArguments parsed, string name) =>
        parsed.Options.TryGetValue(name, out var value) ? value : null;

    private static bool IsSameOrBelow(string candidate, string path) =>
        candidate.Equals(path, StringComparison.Ordinal) || candidate.StartsWith(path + ".", StringComparison.Ordinal) || candidate.StartsWith(path + "[", StringComparison.Ordinal);

    private static int PrintVersion()
    {
        Console.WriteLine("DriftGuard 0.1.0");
        return 0;
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command '{command}'.");
        HelpPrinter.Print();
        return 2;
    }
}
