using System.Text;
using System.Text.Json;

namespace DriftGuard.Core;

internal static class InputSafety
{
    public static string ReadBoundedFile(string path, int maxBytes, string label)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new ConfigParseException($"{label} file not found: {fullPath}");

        using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > maxBytes)
            throw new ConfigParseException($"{label} file exceeds the {maxBytes / (1024 * 1024)} MiB limit.");

        using var bytes = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var left = maxBytes + 1 - (int)bytes.Length;
            if (left <= 0)
                throw new ConfigParseException($"{label} file exceeds the {maxBytes / (1024 * 1024)} MiB limit.");
            var count = stream.Read(buffer, 0, Math.Min(buffer.Length, left));
            if (count == 0)
                break;
            bytes.Write(buffer, 0, count);
            if (bytes.Length > maxBytes)
                throw new ConfigParseException($"{label} file exceeds the {maxBytes / (1024 * 1024)} MiB limit.");
        }

        bytes.Position = 0;
        try
        {
            using var reader = new StreamReader(bytes, new UTF8Encoding(false, true),
                detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
            return reader.ReadToEnd();
        }
        catch (DecoderFallbackException ex)
        {
            throw new ConfigParseException($"{label} file has invalid text encoding.", ex);
        }
    }

    public static void RejectDuplicateJsonProperties(string text, int maxDepth)
    {
        using var document = JsonDocument.Parse(text, new JsonDocumentOptions
        {
            MaxDepth = maxDepth,
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow
        });
        var pending = new Stack<JsonElement>();
        pending.Push(document.RootElement);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (node.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in node.EnumerateObject())
                {
                    if (!names.Add(property.Name))
                        throw new ConfigParseException("Duplicate JSON property names are not allowed.");
                    pending.Push(property.Value);
                }
            }
            else if (node.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in node.EnumerateArray())
                    pending.Push(item);
            }
        }
    }
}
