using System.IO;

namespace Aiden.RuntimeAgent.Infrastructure;

internal static class RuntimeExecutableLocator
{
    public static string? FindLatestExecutable(string componentDirName, string searchPattern, Func<FileInfo, bool>? predicate = null)
    {
        foreach (var runtimeRoot in EnumerateRuntimeRoots())
        {
            var componentRoot = Path.Combine(runtimeRoot, componentDirName);
            if (!Directory.Exists(componentRoot))
            {
                continue;
            }

            var selected = Directory.GetFiles(componentRoot, searchPattern, SearchOption.AllDirectories)
                .Select(path => new FileInfo(path))
                .Where(file => predicate is null || predicate(file))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .FirstOrDefault();
            if (selected is not null)
            {
                return selected.FullName;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateRuntimeRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var dir = new DirectoryInfo(root);
            for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
            {
                var runtimeRoot = Path.Combine(dir.FullName, "runtime");
                if (seen.Add(runtimeRoot))
                {
                    yield return runtimeRoot;
                }
            }
        }
    }
}
