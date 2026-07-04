using OpenTK.Mathematics;
using OpenTK.Windowing.Desktop;

namespace Mars;

public readonly struct UiScreen
{
    public const float Dpi = 96f;
    public const float MmToPx = Dpi / 25.4f;

    public int FramebufferWidth { get; }
    public int FramebufferHeight { get; }
    public int ClientWidth { get; }
    public int ClientHeight { get; }

    public float ScaleX => FramebufferWidth / (float)Math.Max(1, ClientWidth);
    public float ScaleY => FramebufferHeight / (float)Math.Max(1, ClientHeight);

    public UiScreen(int framebufferWidth, int framebufferHeight, int clientWidth, int clientHeight)
    {
        FramebufferWidth = framebufferWidth;
        FramebufferHeight = framebufferHeight;
        ClientWidth = clientWidth;
        ClientHeight = clientHeight;
    }

    public static UiScreen From(NativeWindow window) =>
        new(window.FramebufferSize.X, window.FramebufferSize.Y, window.ClientSize.X, window.ClientSize.Y);

    public static float MmToPixels(float mm, float scaleY = 1f) => mm * MmToPx * scaleY;

    public Vector2 ClientToFramebuffer(float x, float y) => new(x * ScaleX, y * ScaleY);
}
