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
                Console.WriteLine("Usage: driftguard compare <baseline> <current> [--policy file] [--format text|json] [--fail-on none|warning|error]");
                Console.WriteLine("Shows semantic changes. Default --fail-on is none.");
                return;
            case "check":
                Console.WriteLine("Usage: driftguard check <baseline> <current> --policy file [--format text|json] [--fail-on none|warning|error]");
                Console.WriteLine("Suitable for CI. Default --fail-on is error.");
                return;
            case "explain":
                Console.WriteLine("Usage: driftguard explain <baseline> <current> <path> [--policy file] [--format text|json]");
                Console.WriteLine("Shows changes at a path, its descendants, or an enclosing changed subtree.");
                return;
            case "init":
                Console.WriteLine("Usage: driftguard init [--path file] [--force]");
                Console.WriteLine("Creates a starter policy file; --force overwrites an existing file.");
                return;
            default:
                Console.WriteLine("Usage:");
                Console.WriteLine("  driftguard compare <baseline> <current> [--policy file] [--format text|json] [--fail-on none|warning|error]");
                Console.WriteLine("  driftguard check <baseline> <current> --policy file [--format text|json] [--fail-on none|warning|error]");
                Console.WriteLine("  driftguard explain <baseline> <current> <path> [--policy file] [--format text|json]");
                Console.WriteLine("  driftguard init [--path file] [--force]");
                Console.WriteLine("  driftguard version");
                Console.WriteLine();
                Console.WriteLine("Exit codes: 0 = pass/no blocking findings; 1 = policy threshold failed; 2 = usage or input error.");
                Console.WriteLine("Supported configuration formats: JSON, YAML. Root must be an object/mapping.");
                return;
        }
    }
}
