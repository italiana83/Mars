using OpenTK.Windowing.Desktop;

namespace Mars;

/// <summary>
/// Точка входа приложения: запуск окна визуализации MOLA heightmap.
/// </summary>
class Program
{
    /// <summary>Создаёт и запускает главное окно <see cref="HeightmapGame"/>.</summary>
    static void Main(string[] args)
    {
        // MEGDR Data Online: https://pds-geosciences.wustl.edu/missions/mgs/megdr.html
        using var window = new HeightmapGame();
        window.Run();
    }
}
