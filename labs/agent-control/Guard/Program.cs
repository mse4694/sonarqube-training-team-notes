// Training PreToolUse path guard. Does not edit files or run subprocesses.
// Reads one hook event on stdin and exits 0 (continue normal permissions) or 2 (block).
using System.Text.Json;
using AgentControl;

return Guard.Run(Console.OpenStandardInput(), Environment.GetEnvironmentVariable("CLAUDE_PROJECT_DIR"));

namespace AgentControl
{
    public static class Guard
    {
        // The ONLY file the agent may change, relative to the project (WORKFLOW_DIR) root.
        private static readonly string[] Allowed = [Path.Combine("Voucher", "VoucherService.cs")];
        private const int MaxBytes = 1_000_000;

        public static int Run(Stream stdin, string? projectDir)
        {
            try
            {
                var raw = ReadBounded(stdin, MaxBytes + 1);
                if (raw.Length > MaxBytes)
                    return Deny("input too large");
                using var doc = JsonDocument.Parse(raw, new JsonDocumentOptions { AllowDuplicateProperties = false });
                var hookEvent = doc.RootElement;
                if (hookEvent.ValueKind != JsonValueKind.Object || Text(hookEvent, "hook_event_name") != "PreToolUse")
                    return Deny("invalid event");
                if (Text(hookEvent, "tool_name") is not ("Edit" or "Write"))
                    return Deny("tool outside this exercise");
                if (string.IsNullOrEmpty(projectDir) || !Path.IsPathFullyQualified(projectDir))
                    return Deny("missing absolute project root");
                var root = PathResolver.ResolveStrict(projectDir);
                if (!Directory.Exists(root))
                    return Deny("project root is not a directory");
                var cwdValue = Text(hookEvent, "cwd");
                if (cwdValue is null || !Path.IsPathFullyQualified(cwdValue))
                    return Deny("missing absolute cwd");
                var cwd = PathResolver.ResolveStrict(cwdValue);
                if (!Directory.Exists(cwd) || !PathResolver.IsSameOrInside(cwd, root))
                    return Deny("cwd outside project");
                if (!hookEvent.TryGetProperty("tool_input", out var toolInput) || toolInput.ValueKind != JsonValueKind.Object)
                    return Deny("invalid tool input");
                var value = Text(toolInput, "file_path");
                if (string.IsNullOrWhiteSpace(value) || value.Contains('\0'))
                    return Deny("missing or invalid file path");
                var requested = Path.IsPathFullyQualified(value) ? value : Path.Combine(cwd, value);
                var target = PathResolver.ResolveStrict(requested);
                // Compare against lexical project paths, not resolved allowlist entries:
                // a symlink installed at an allowed path must not enlarge the allowlist.
                var allowed = Allowed.Select(relative => Path.Combine(root, relative)).ToList();
                if (!allowed.Contains(target, StringComparer.Ordinal))
                    return Deny("file outside allowed source files");
                // The path must ALSO be allowed when ".." is collapsed lexically (as Windows and the
                // agent's own path handling do): "link/../x" must not mean two different files.
                // The project dir as given (macOS /var vs /private/var) is accepted as a prefix too.
                var lexical = Allowed.Select(relative => Path.Combine(Path.GetFullPath(projectDir), relative)).Concat(allowed);
                if (!lexical.Contains(Path.GetFullPath(requested), StringComparer.Ordinal))
                    return Deny("file outside allowed source files");
                var info = NativeLinks.Inspect(target);
                if (!info.IsRegularFile)
                    return Deny("not a regular file");
                if (info.LinkCount != 1)
                    return Deny("linked file requires human inspection");
                return 0; // Continue normal permissions; this is not a native allow override.
            }
            catch (Exception)
            {
                // Never echo source contents, paths or tool payloads in the block message.
                return Deny("cannot validate input or path");
            }
        }

        private static int Deny(string reason)
        {
            Console.Error.WriteLine("Training guard blocked: " + reason);
            return 2;
        }

        private static string? Text(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

        private static byte[] ReadBounded(Stream stream, int limit)
        {
            using var buffer = new MemoryStream();
            var chunk = new byte[64 * 1024];
            int read;
            while (buffer.Length < limit && (read = stream.Read(chunk, 0, (int)Math.Min(chunk.Length, limit - buffer.Length))) > 0)
                buffer.Write(chunk, 0, read);
            return buffer.ToArray();
        }
    }
}
