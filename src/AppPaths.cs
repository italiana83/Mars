namespace Mars;

/// <summary>
/// Пути к корню проекта и каталогу data с файлами MOLA и мини-картой.
/// </summary>
public static class AppPaths
{
    /// <summary>Абсолютный путь к корню проекта (относительно каталога сборки).</summary>
    public static string ProjectRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    /// <summary>Собирает путь внутри каталога data из переданных частей.</summary>
    public static string DataPath(params string[] parts) =>
        Path.Combine(new[] { ProjectRoot, "data" }.Concat(parts).ToArray());

    /// <summary>Каталог с тайлами MOLA MEG128 (.lbl / .img).</summary>
    public static string Meg128Directory => DataPath("mola", "meg128");

    /// <summary>
    /// Ищет файл мини-карты топографии Mars_topography_* в data;
    /// возвращает null, если подходящий файл не найден.
    /// </summary>
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
