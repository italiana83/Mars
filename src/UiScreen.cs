using OpenTK.Mathematics;
using OpenTK.Windowing.Desktop;

namespace Mars;

/// <summary>
/// Метрики экрана для перевода координат клиентской области окна в framebuffer (HiDPI).
/// </summary>
public readonly struct UiScreen
{
    /// <summary>Стандартное разрешение экрана в DPI (96).</summary>
    public const float Dpi = 96f;

    /// <summary>Коэффициент перевода миллиметров в пиксели при 96 DPI.</summary>
    public const float MmToPx = Dpi / 25.4f;

    /// <summary>Ширина framebuffer в физических пикселях.</summary>
    public int FramebufferWidth { get; }

    /// <summary>Высота framebuffer в физических пикселях.</summary>
    public int FramebufferHeight { get; }

    /// <summary>Ширина клиентской области окна в логических пикселях.</summary>
    public int ClientWidth { get; }

    /// <summary>Высота клиентской области окна в логических пикселях.</summary>
    public int ClientHeight { get; }

    /// <summary>Масштаб по X: отношение framebuffer к client width.</summary>
    public float ScaleX => FramebufferWidth / (float)Math.Max(1, ClientWidth);

    /// <summary>Масштаб по Y: отношение framebuffer к client height.</summary>
    public float ScaleY => FramebufferHeight / (float)Math.Max(1, ClientHeight);

    /// <summary>Создаёт экранные метрики из размеров framebuffer и клиентской области.</summary>
    public UiScreen(int framebufferWidth, int framebufferHeight, int clientWidth, int clientHeight)
    {
        FramebufferWidth = framebufferWidth;
        FramebufferHeight = framebufferHeight;
        ClientWidth = clientWidth;
        ClientHeight = clientHeight;
    }

    /// <summary>Строит <see cref="UiScreen"/> из нативного окна OpenTK.</summary>
    public static UiScreen From(NativeWindow window) =>
        new(window.FramebufferSize.X, window.FramebufferSize.Y, window.ClientSize.X, window.ClientSize.Y);

    /// <summary>Переводит размер в миллиметрах в пиксели с учётом вертикального масштаба.</summary>
    public static float MmToPixels(float mm, float scaleY = 1f) => mm * MmToPx * scaleY;

    /// <summary>Преобразует координаты клиентской области в координаты framebuffer.</summary>
    public Vector2 ClientToFramebuffer(float x, float y) => new(x * ScaleX, y * ScaleY);
}
