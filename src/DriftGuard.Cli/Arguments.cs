namespace DriftGuard.Cli;

internal sealed record ParsedArguments(IReadOnlyList<string> Positionals, IReadOnlyDictionary<string, string> Options);

internal sealed class CliUsageException : Exception
{
    public CliUsageException(string message) : base(message) { }
}

internal static class Arguments
{
    private static readonly HashSet<string> Flags = new(StringComparer.OrdinalIgnoreCase) { "help", "force" };

    public static ParsedArguments Parse(string[] args)
    {
        var positionals = new List<string>();
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                positionals.Add(argument);
                continue;
            }

            var key = argument[2..];
            if (key.Length == 0)
                throw new CliUsageException("Empty option name.");
            if (options.ContainsKey(key))
                throw new CliUsageException($"Option --{key} was supplied more than once.");

            if (Flags.Contains(key))
            {
                options.Add(key, "true");
                continue;
            }

            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                throw new CliUsageException($"Option --{key} requires a value.");
            options.Add(key, args[++index]);
        }

        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "help", "force", "baseline", "current", "path", "policy", "format", "fail-on"
        };
        var unsupported = options.Keys.FirstOrDefault(key => !allowed.Contains(key));
        if (unsupported is not null)
            throw new CliUsageException($"Unknown option --{unsupported}.");

        return new ParsedArguments(positionals, options);
    }
    public static void AllowOnly(ParsedArguments parsed, params string[] names)
    {
        var allowed = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
        var invalid = parsed.Options.Keys.FirstOrDefault(name => !allowed.Contains(name));
        if (invalid is not null)
            throw new CliUsageException($"Option --{invalid} is not supported by this command.");
    }

}
