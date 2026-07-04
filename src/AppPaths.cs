namespace Mars;

public static class AppPaths
{
    public static string ProjectRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    public static string DataPath(params string[] parts) =>
        Path.Combine(new[] { ProjectRoot, "data" }.Concat(parts).ToArray());

    public static string? FindMinimapImage()
    {
        foreach (var name in new[]
        {
            "Mars_topography_(MOLA_dataset)_HiRes.png",
            "Mars_topography_(MOLA_dataset)_HiRes_2.jpg",
            "Mars_topography_(MOLA_dataset)_HiRes.jpg",
        })
        {
            var path = DataPath(name);
            if (File.Exists(path))
                return path;
        }

        return Directory.EnumerateFiles(DataPath(), "Mars_topography_*.*", SearchOption.TopDirectoryOnly)
            .Where(p => p.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                     || p.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                     || p.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }
}
