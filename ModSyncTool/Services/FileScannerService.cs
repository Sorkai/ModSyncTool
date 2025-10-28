using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModSyncTool.Helpers;
using ModSyncTool.Models;

namespace ModSyncTool.Services;

public sealed class FileScannerService
{
    private readonly string _rootDirectory;

    public FileScannerService()
    {
        _rootDirectory = AppContext.BaseDirectory;
    }

    public Task<IReadOnlyList<string>> FindUntrackedFilesAsync(LocalConfig config, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var result = new List<string>();
            foreach (var relativePath in EnumerateRelevantFiles(config))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var status = ClassifyPath(relativePath, config, isDirectory: false);
                if (status == LocalFileStatus.Untracked)
                {
                    result.Add(relativePath);
                }
            }

            return (IReadOnlyList<string>)result;
        }, cancellationToken);
    }

    public Task<IReadOnlyList<LocalFileNode>> BuildTreeAsync(LocalConfig config, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var roots = DetermineRoots(config);
            var nodes = new List<LocalFileNode>();
            foreach (var root in roots)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fullPath = Path.Combine(_rootDirectory, root);
                if (!Directory.Exists(fullPath))
                {
                    continue;
                }

                nodes.Add(BuildNode(fullPath, root, config));
            }

            return (IReadOnlyList<LocalFileNode>)nodes;
        }, cancellationToken);
    }

    private LocalFileNode BuildNode(string fullPath, string relativePath, LocalConfig config)
    {
        var isDirectory = Directory.Exists(fullPath);
        var node = new LocalFileNode
        {
            Name = Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar)) ?? relativePath,
            FullPath = fullPath,
            IsDirectory = isDirectory,
            Status = ClassifyPath(relativePath, config, isDirectory)
        };

        if (isDirectory)
        {
            var directories = Directory.GetDirectories(fullPath).OrderBy(p => p, StringComparer.OrdinalIgnoreCase);
            foreach (var directory in directories)
            {
                var childRelative = NormalizeRelativePath(Path.Combine(relativePath, Path.GetFileName(directory)));
                node.Children.Add(BuildNode(directory, childRelative, config));
            }

            var files = Directory.GetFiles(fullPath).OrderBy(p => p, StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
            {
                var childRelative = NormalizeRelativePath(Path.Combine(relativePath, Path.GetFileName(file)));
                node.Children.Add(BuildNode(file, childRelative, config));
            }

            if (node.Children.Count > 0)
            {
                if (node.Children.All(c => c.Status == LocalFileStatus.Managed))
                {
                    node.Status = LocalFileStatus.Managed;
                }
                else if (node.Children.All(c => c.Status == LocalFileStatus.Ignored))
                {
                    node.Status = LocalFileStatus.Ignored;
                }
                else
                {
                    node.Status = LocalFileStatus.Untracked;
                }
            }
        }

        return node;
    }

    private IEnumerable<string> EnumerateRelevantFiles(LocalConfig config)
    {
        var roots = DetermineRoots(config);
        foreach (var root in roots)
        {
            var fullRoot = Path.Combine(_rootDirectory, root);
            if (!Directory.Exists(fullRoot))
            {
                continue;
            }

            foreach (var file in Directory.GetFiles(fullRoot, "*", SearchOption.AllDirectories))
            {
                var relative = NormalizeRelativePath(Path.GetRelativePath(_rootDirectory, file));
                yield return relative;
            }
        }
    }

    private IReadOnlyCollection<string> DetermineRoots(LocalConfig config)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var managed in config.ManagedFiles)
        {
            var root = ExtractRoot(managed);
            if (!string.IsNullOrWhiteSpace(root))
            {
                result.Add(root);
            }
        }

        foreach (var pattern in config.IgnorePatterns)
        {
            var root = ExtractRoot(pattern);
            if (!string.IsNullOrWhiteSpace(root))
            {
                result.Add(root);
            }
        }

        result.Add("Mods");
        result.Add("Config");
        return result;
    }

    private static string ExtractRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var normalized = path.Replace('\\', '/');
        var index = normalized.IndexOf('/', StringComparison.Ordinal);
        return index < 0 ? normalized : normalized.Substring(0, index);
    }

    private LocalFileStatus ClassifyPath(string relativePath, LocalConfig config, bool isDirectory)
    {
        var normalized = NormalizeRelativePath(relativePath);
        if (config.ManagedFiles.Any(p => string.Equals(NormalizeRelativePath(p), normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return LocalFileStatus.Managed;
        }

        if (isDirectory)
        {
            if (config.ManagedFiles.Any(p => NormalizeRelativePath(p).StartsWith(normalized + '/', StringComparison.OrdinalIgnoreCase)))
            {
                return LocalFileStatus.Managed;
            }
        }

        if (IsIgnored(normalized, config.IgnorePatterns, isDirectory))
        {
            return LocalFileStatus.Ignored;
        }

        return LocalFileStatus.Untracked;
    }

    private static bool IsIgnored(string relativePath, IReadOnlyCollection<string> patterns, bool isDirectory)
    {
        foreach (var pattern in patterns)
        {
            if (WildcardPattern.IsMatch(pattern, relativePath))
            {
                return true;
            }

            if (isDirectory && pattern.EndsWith("/*", StringComparison.OrdinalIgnoreCase))
            {
                var trimmed = pattern[..^2];
                if (WildcardPattern.IsMatch(trimmed, relativePath))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string NormalizeRelativePath(string value)
    {
        return value.Replace('\\', '/');
    }
}
