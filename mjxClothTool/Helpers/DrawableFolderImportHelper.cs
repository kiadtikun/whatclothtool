using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace mjxClothTool.Helpers;

public static class DrawableFolderImportHelper
{
    public static bool IsSupportedDropPath(string path) =>
        IsDrawablePath(path) || Directory.Exists(path);

    public static string[] GetDroppedDrawablePaths(IEnumerable<string> paths)
    {
        var files = new List<string>();
        var folders = new List<string>();

        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                folders.Add(path);
            }
            else if (IsDrawablePath(path))
            {
                files.Add(Path.GetFullPath(path));
            }
        }

        return files.Concat(GetOrderedBatches(folders).SelectMany(batch => batch))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsDrawablePath(string path) =>
        string.Equals(Path.GetExtension(path), ".ydd", StringComparison.OrdinalIgnoreCase);

    public static List<string[]> GetOrderedBatches(IEnumerable<string> folders)
    {
        var naturalComparer = StringComparer.Create(CultureInfo.InvariantCulture,
            CompareOptions.IgnoreCase | CompareOptions.NumericOrdering);
        var seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var batches = new List<string[]>();

        foreach (var folder in folders.Select(Path.GetFullPath)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(path => Path.GetFileName(Path.TrimEndingDirectorySeparator(path)), naturalComparer)
                     .ThenBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var files = Directory.GetFiles(folder, "*.ydd", SearchOption.AllDirectories)
                .Where(seenFiles.Add)
                .OrderBy(path => FileHelper.GetDrawableNumberFromFileName(Path.GetFileName(path)) ?? int.MaxValue)
                .ThenBy(Path.GetFileName, naturalComparer)
                .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (files.Length > 0)
                batches.Add(files);
        }

        return batches;
    }
}
