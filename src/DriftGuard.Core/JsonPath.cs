using System.Text;

namespace DriftGuard.Core;

/// <summary>
/// The JSONPath-like subset used by DriftGuard reports and policy globs.
/// It is deliberately not a general JSONPath query evaluator.
/// </summary>
public static class JsonPath
{
    internal readonly record struct Segment(string Value, bool IsIndex, bool AllowsWildcard);

    public static string AppendProperty(string parent, string name)
    {
        if (name.Length > 0 && (char.IsLetter(name[0]) || name[0] == '_') &&
            name.All(character => char.IsLetterOrDigit(character) || character is '_' or '-'))
            return $"{parent}.{name}";

        var escaped = name.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("'", "\\'", StringComparison.Ordinal);
        return $"{parent}['{escaped}']";
    }

    public static string AppendIndex(string parent, int index) =>
        $"{parent}[{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}]";

    public static void ValidateExactPath(string path)
    {
        Parse(path, allowWildcards: false);
    }

    internal static IReadOnlyList<Segment> Parse(string path, bool allowWildcards)
    {
        if (string.IsNullOrEmpty(path) || path[0] != '$')
            throw new ConfigParseException("A path must start with '$'.");

        var parts = new List<Segment>();
        var index = 1;
        while (index < path.Length)
        {
            if (path[index] == '.')
            {
                index++;
                var start = index;
                while (index < path.Length && path[index] is not ('.' or '['))
                    index++;
                if (start == index)
                    throw new ConfigParseException("A path contains an empty property segment.");

                var name = path[start..index];
                if (!allowWildcards && name.Contains('*'))
                    throw new ConfigParseException("An unquoted path contains a wildcard.");
                if (!(char.IsLetter(name[0]) || name[0] == '_' || allowWildcards && name[0] == '*') ||
                    name.Any(character => !char.IsLetterOrDigit(character) && character is not ('_' or '-') &&
                        !(allowWildcards && character == '*')))
                    throw new ConfigParseException("A path contains an invalid unquoted property name.");
                parts.Add(new Segment(name, IsIndex: false, AllowsWildcard: allowWildcards));
                continue;
            }

            if (path[index] == '[')
            {
                index++;
                if (index >= path.Length)
                    throw new ConfigParseException("A path has an unclosed bracket.");

                if (path[index] is '\'' or '"')
                {
                    var quote = path[index++];
                    var name = new StringBuilder();
                    var terminated = false;
                    while (index < path.Length)
                    {
                        var character = path[index++];
                        if (character == quote)
                        {
                            terminated = true;
                            break;
                        }
                        if (character == '\\')
                        {
                            if (index >= path.Length || path[index] != quote && path[index] != '\\')
                                throw new ConfigParseException("A quoted path contains an invalid escape sequence.");
                            character = path[index++];
                        }
                        name.Append(character);
                    }
                    if (!terminated || index >= path.Length || path[index++] != ']')
                        throw new ConfigParseException("A path has an unterminated quoted property.");
                    parts.Add(new Segment(name.ToString(), IsIndex: false, AllowsWildcard: false));
                    continue;
                }

                var start = index;
                while (index < path.Length && path[index] >= '0' && path[index] <= '9')
                    index++;
                if (start == index || index >= path.Length || path[index++] != ']')
                    throw new ConfigParseException("Array selectors must use a non-negative index, such as [0].");
                parts.Add(new Segment(path[start..(index - 1)], IsIndex: true, AllowsWildcard: false));
                continue;
            }

            throw new ConfigParseException("A path must use dot properties or bracket notation.");
        }
        return parts;
    }

    internal static bool Matches(IReadOnlyList<Segment> pattern, IReadOnlyList<Segment> path)
    {
        // Dynamic programming avoids exponential backtracking with repeated ** globs.
        var matched = new bool[pattern.Count + 1, path.Count + 1];
        matched[pattern.Count, path.Count] = true;
        for (var p = pattern.Count - 1; p >= 0; p--)
        {
            for (var s = path.Count; s >= 0; s--)
            {
                var part = pattern[p];
                if (part.AllowsWildcard && part.Value == "**")
                    matched[p, s] = matched[p + 1, s] || (s < path.Count && matched[p, s + 1]);
                else if (s < path.Count && SegmentMatches(part, path[s]))
                    matched[p, s] = matched[p + 1, s + 1];
            }
        }
        return matched[0, 0];
    }

    private static bool SegmentMatches(Segment pattern, Segment actual)
    {
        if (pattern.AllowsWildcard && pattern.Value == "*")
            return true;
        if (pattern.IsIndex != actual.IsIndex)
            return false;
        if (!pattern.AllowsWildcard || !pattern.Value.Contains('*'))
            return string.Equals(pattern.Value, actual.Value, StringComparison.OrdinalIgnoreCase);

        // '*' within an unquoted segment matches characters inside that segment only.
        var p = 0;
        var a = 0;
        var lastStar = -1;
        var nextAfterStar = 0;
        while (a < actual.Value.Length)
        {
            if (p < pattern.Value.Length &&
                char.ToUpperInvariant(pattern.Value[p]) == char.ToUpperInvariant(actual.Value[a]) &&
                pattern.Value[p] != '*')
            {
                p++;
                a++;
            }
            else if (p < pattern.Value.Length && pattern.Value[p] == '*')
            {
                lastStar = p++;
                nextAfterStar = a;
            }
            else if (lastStar >= 0)
            {
                p = lastStar + 1;
                a = ++nextAfterStar;
            }
            else
            {
                return false;
            }
        }
        while (p < pattern.Value.Length && pattern.Value[p] == '*')
            p++;
        return p == pattern.Value.Length;
    }
}
