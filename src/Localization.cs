namespace Mars;

public enum UiLanguage
{
    English,
    Russian
}

/// <summary>Текущий язык пользовательского интерфейса и его текстовые ресурсы.</summary>
public static class Localization
{
    public static UiLanguage CurrentLanguage { get; private set; } = UiLanguage.English;

    public static event Action? LanguageChanged;

    public static void SetLanguage(UiLanguage language)
    {
        if (CurrentLanguage == language)
            return;

        CurrentLanguage = language;
        LanguageChanged?.Invoke();
    }

    private static bool IsRussian => CurrentLanguage == UiLanguage.Russian;

    public static string Menu => IsRussian ? "Меню" : "Menu";
    public static string Minimap => IsRussian ? "Мини-карта" : "Minimap";
    public static string Settings => IsRussian ? "Настройки" : "Settings";
    public static string Fill => IsRussian ? "Заливка" : "Fill";
    public static string ShowChunks => IsRussian ? "Показывать чанки" : "Show chunks";
    public static string Language => IsRussian ? "Язык" : "Language";
    public static string Russian => "Русский";
    public static string English => "English";
    public static string Height => IsRussian ? "Высота" : "Height";
    public static string SamplingStep => IsRussian ? "Шаг выборки" : "Sampling step";
    public static string Speed => IsRussian ? "Скорость" : "Speed";
    public static string On => IsRussian ? "вкл" : "on";
    public static string Off => IsRussian ? "выкл" : "off";

    public static string ControlsHint => IsRussian
        ? "W A S D — движение  •  ЛКМ + мышь — обзор  •  Колесо — вперёд / назад  •  Esc — закрыть меню"
        : "W A S D — move  •  LMB + mouse — look  •  Wheel — move forward / back  •  Esc — close menu";

    public static string ViewerTitle => "Mars MOLA Viewer";
    public static string FpsTitle(double fps) => $"{ViewerTitle} - FPS: {fps:F2}";
    public static string TileTitle(string tileName) => $"{ViewerTitle} - {tileName.ToUpperInvariant()}";
    public static string TileNotFoundTitle(string tileName) => IsRussian
        ? $"{ViewerTitle} - {tileName}.img не найден"
        : $"{ViewerTitle} - {tileName}.img not found";
    public static string TileLoadFailedTitle(string tileName) => IsRussian
        ? $"{ViewerTitle} - не удалось загрузить: {tileName}"
        : $"{ViewerTitle} - load failed: {tileName}";
    public static string MinimapImageNotFound => IsRussian
        ? "Изображение мини-карты не найдено. Поместите Mars_topography_(MOLA_dataset)_HiRes.png (или .jpg) в папку data/."
        : "Minimap image not found. Place Mars_topography_(MOLA_dataset)_HiRes.png (or .jpg) in the data/ folder.";
}
