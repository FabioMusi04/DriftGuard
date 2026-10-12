namespace DriftGuard.Cli;

internal static class HelpPrinter
{
    public static void Print(string? command = null)
    {
        Console.WriteLine("DriftGuard — semantic configuration diff and policy checks");
        Console.WriteLine();
        switch (command)
        {
            case "compare":
                Console.WriteLine("Usage: driftguard compare <baseline> <current> [--policy file] [--format text|json|sarif] [--fail-on none|warning|error] [--values redacted|none] [--source-names full|none]");
                Console.WriteLine("Reports changes. Default --fail-on is none.");
                return;
            case "check":
                Console.WriteLine("Usage: driftguard check <baseline> <current> --policy file [--format text|json|sarif] [--fail-on none|warning|error] [--values redacted|none] [--source-names full|none]");
                Console.WriteLine("Suitable for CI. Default --fail-on is error.");
                return;
            case "explain":
                Console.WriteLine("Usage: driftguard explain <baseline> <current> <path> [--policy file] [--format text|json] [--values redacted|none] [--source-names full|none]");
                Console.WriteLine("Shows changes at a path, its descendants, or an enclosing changed subtree.");
                return;
            case "validate-policy":
                Console.WriteLine("Usage: driftguard validate-policy <policy.json> [--format text|json]");
                Console.WriteLine("       driftguard validate-policy --policy <policy.json> [--format text|json]");
                Console.WriteLine("Exit 0 if the policy is valid; exit 2 otherwise.");
                return;
            case "init":
                Console.WriteLine("Usage: driftguard init [--path file] [--force]");
                Console.WriteLine("Creates a starter policy file; --force overwrites an existing file.");
                return;
            default:
                Console.WriteLine("Usage:");
                Console.WriteLine("  driftguard compare <baseline> <current> [--policy file] [--format text|json|sarif] [--fail-on none|warning|error]");
                Console.WriteLine("  driftguard check <baseline> <current> --policy file [--format text|json|sarif] [--fail-on none|warning|error]");
                Console.WriteLine("  driftguard explain <baseline> <current> <path> [--policy file] [--format text|json]");
                Console.WriteLine("  driftguard validate-policy <policy.json> [--format text|json]");
                Console.WriteLine("  driftguard init [--path file] [--force]");
                Console.WriteLine("  driftguard version");
                Console.WriteLine();
                Console.WriteLine("Report privacy: --values none omits ALL old/new values; --source-names none omits input file paths.");
                Console.WriteLine("These options do not hide configuration key paths or policy descriptions.");
                Console.WriteLine("SARIF uses file-level locations (line 1), and requires --source-names full.");
                Console.WriteLine("Exit codes: 0 = pass; 1 = policy threshold failed; 2 = usage/input error.");
                Console.WriteLine("--format json also returns machine-readable errors on stdout (exit 2).");
                Console.WriteLine("Supported configuration formats: JSON, YAML. Root must be an object/mapping.");
                return;
        }
    }
}
