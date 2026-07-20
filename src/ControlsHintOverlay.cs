using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System.Drawing;

namespace Mars;

/// <summary>Ненавязчивая стартовая подсказка по управлению, скрываемая после начала движения.</summary>
public sealed class ControlsHintOverlay : IDisposable
{
    private static readonly Vector3 TextColor = new(0.40f, 0.92f, 0.98f);

    private readonly TextRenderer _text;
    private int _screenWidth;
    private int _screenHeight;

    public bool IsVisible { get; private set; } = true;

    public ControlsHintOverlay(UiScreen screen)
    {
        _screenWidth = screen.FramebufferWidth;
        _screenHeight = screen.FramebufferHeight;
        _text = new TextRenderer(TextRenderer.ResolveSystemFont(), _screenWidth, _screenHeight, 18f);
        _text.EnsureGlyphs(Localization.ControlsHint);
    }

    /// <summary>Обновляет проекцию текста при изменении размеров окна.</summary>
    public void UpdateScreenSize(UiScreen screen)
    {
        _screenWidth = screen.FramebufferWidth;
        _screenHeight = screen.FramebufferHeight;
        _text.UpdateScreenSize(_screenWidth, _screenHeight);
    }

    /// <summary>Скрывает подсказку после первого использования WASD.</summary>
    public void Hide() => IsVisible = false;

    /// <summary>Рисует строку подсказки по центру внизу окна.</summary>
    public void Render()
    {
        if (!IsVisible)
            return;

        GL.Disable(EnableCap.DepthTest);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        _text.RenderTextCenteredInRect(
            new RectangleF(0f, _screenHeight - 44f, _screenWidth, 28f),
            Localization.ControlsHint,
            1f,
            TextColor);

        GL.Disable(EnableCap.Blend);
        GL.Enable(EnableCap.DepthTest);
    }

    public void Dispose() => _text.Dispose();
}
