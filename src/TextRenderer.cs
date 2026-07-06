using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using SkiaSharp;
using System.Drawing;

namespace Mars;

/// <summary>
/// Рендеринг текста через SkiaSharp (растрализация глифов) и OpenGL (отрисовка квадов с альфа-текстурой).
/// </summary>
public class TextRenderer : IDisposable
{
    private readonly int _vao, _vbo;
    private readonly Shader _textShader;
    private readonly Dictionary<int, Character> _characters = new();
    private readonly SKTypeface _typeface;
    private readonly float _fontSize;
    private readonly float _ascent;
    private readonly float _descent;

    public float LineHeight => _ascent + _descent;

    /// <summary>
    /// Измеряет высоту ограничивающего прямоугольника строки при текущем размере шрифта.
    /// </summary>
    public float MeasureTextHeight(string text)
    {
        using var font = new SKFont(_typeface, _fontSize) { Edging = SKFontEdging.Antialias };
        font.MeasureText(text, out var bounds);
        return bounds.Height;
    }

    /// <summary>Суммарная ширина строки в пикселях при заданном масштабе.</summary>
    public float MeasureTextWidth(string text, float scale = 1f)
    {
        EnsureGlyphs(text);
        float width = 0f;
        foreach (var rune in text.EnumerateRunes())
        {
            if (_characters.TryGetValue(rune.Value, out var ch))
                width += ch.Advance * scale;
        }

        return width;
    }

    /// <summary>Рисует текст по центру прямоугольника (горизонтально и вертикально).</summary>
    public void RenderTextCenteredInRect(RectangleF rect, string text, float scale, Vector3 color)
    {
        float width = MeasureTextWidth(text, scale);
        using var font = new SKFont(_typeface, _fontSize) { Edging = SKFontEdging.Antialias };
        font.MeasureText(text, out var bounds);

        float x = rect.X + (rect.Width - width) * 0.5f;
        float topY = rect.Y + (rect.Height - bounds.Height * scale) * 0.5f;
        RenderTextFromTop(text, x, topY, scale, color);
    }

    /// <summary>
    /// Рисует текст, задавая верхнюю границу строки (topY); внутренне переводит координаты в baseline для <see cref="RenderText"/>.
    /// </summary>
    public void RenderTextFromTop(string text, float x, float topY, float scale, Vector3 color)
    {
        using var font = new SKFont(_typeface, _fontSize) { Edging = SKFontEdging.Antialias };
        font.MeasureText(text, out var bounds);
        float baseline = topY - bounds.Top * scale;
        RenderText(text, x, baseline / scale - _ascent, scale, color);
    }

    private struct Character
    {
        public int TextureID;
        public Vector2 Size;
        public Vector2 Bearing;
        public float Advance;
    }

    /// <summary>
    /// Ищет в системной папке Fonts подходящий TTF (Segoe UI, Arial, Calibri, Tahoma).
    /// </summary>
    public static string ResolveSystemFont()
    {
        var fontsDir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        foreach (var name in new[] { "segoeui.ttf", "arial.ttf", "calibri.ttf", "tahoma.ttf" })
        {
            var path = Path.Combine(fontsDir, name);
            if (File.Exists(path))
                return path;
        }

        throw new FileNotFoundException("No suitable UI font found in system fonts folder.");
    }

    /// <summary>
    /// Загружает шрифт, создаёт VAO/VBO для квадов, компилирует text shader и задаёт orthographic projection под размер экрана.
    /// </summary>
    public TextRenderer(string fontPath, int screenWidth, int screenHeight, float fontSize = 24f)
    {
        _typeface = SKTypeface.FromFile(fontPath) ?? SKTypeface.FromFamilyName("Segoe UI") ?? SKTypeface.Default;
        _fontSize = fontSize;

        using (var probe = new SKFont(_typeface, fontSize))
        {
            probe.GetFontMetrics(out var metrics);
            _ascent = -metrics.Ascent;
            _descent = metrics.Descent;
        }

        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();

        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, 6 * 4 * sizeof(float), IntPtr.Zero, BufferUsageHint.DynamicDraw);
        GL.VertexAttribPointer(0, 4, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
        GL.EnableVertexAttribArray(0);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        GL.BindVertexArray(0);

        const string vertexShaderSource = @"
#version 330 core
layout (location = 0) in vec4 vertex;
out vec2 TexCoords;
uniform mat4 projection;
void main()
{
    gl_Position = projection * vec4(vertex.xy, 0.0, 1.0);
    TexCoords = vertex.zw;
}";

        const string fragmentShaderSource = @"
#version 330 core
in vec2 TexCoords;
out vec4 FragColor;
uniform sampler2D text;
uniform vec3 textColor;
void main()
{
    float alpha = texture(text, TexCoords).r;
    FragColor = vec4(textColor, alpha);
}";

        _textShader = new Shader(vertexShaderSource, fragmentShaderSource, ShaderSourceMode.Code);
        _textShader.Use();
        _textShader.SetInt("text", 0);
        UpdateScreenSize(screenWidth, screenHeight);
    }

    /// <summary>
    /// Ленивая подгрузка текстур глифов для всех code point строки, ещё не встречавшихся в кэше.
    /// </summary>
    public void EnsureGlyphs(string text)
    {
        foreach (var rune in text.EnumerateRunes())
        {
            int cp = rune.Value;
            if (!_characters.ContainsKey(cp))
                LoadGlyph(cp);
        }
    }

    /// <summary>
    /// Обновляет orthographic projection шейдера при изменении размеров окна или фреймбуфера.
    /// </summary>
    public void UpdateScreenSize(int width, int height)
    {
        var projection = Matrix4.CreateOrthographicOffCenter(0, width, height, 0, -1f, 1f);
        _textShader.Use();
        _textShader.SetMatrix4("projection", projection);
    }

    /// <summary>
    /// Отрисовывает строку текста в экранных координатах с масштабом и цветом; глифы выводятся квадами с альфа-текстурой.
    /// </summary>
    public void RenderText(string text, float x, float y, float scale, Vector3 color)
    {
        EnsureGlyphs(text);

        _textShader.Use();
        _textShader.SetVector3("textColor", color);
        _textShader.SetInt("text", 0);
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindVertexArray(_vao);

        float baseline = y + _ascent * scale;

        foreach (var rune in text.EnumerateRunes())
        {
            int cp = rune.Value;
            if (!_characters.TryGetValue(cp, out var ch))
                continue;

            float xpos = x + ch.Bearing.X * scale;
            float ypos = baseline + ch.Bearing.Y * scale;
            float w = ch.Size.X * scale;
            float h = ch.Size.Y * scale;

            float[] vertices =
            {
                xpos,     ypos,     0f, 0f,
                xpos,     ypos + h, 0f, 1f,
                xpos + w, ypos + h, 1f, 1f,

                xpos,     ypos,     0f, 0f,
                xpos + w, ypos + h, 1f, 1f,
                xpos + w, ypos,     1f, 0f
            };

            GL.BindTexture(TextureTarget.Texture2D, ch.TextureID);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, vertices.Length * sizeof(float), vertices);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);

            x += ch.Advance * scale;
        }

        GL.BindVertexArray(0);
        GL.BindTexture(TextureTarget.Texture2D, 0);
    }

    /// <summary>
    /// Растеризует один глиф через SkiaSharp, создаёт R8 OpenGL-текстуру и сохраняет метрики в кэше символов.
    /// </summary>
    private void LoadGlyph(int codepoint)
    {
        if (_characters.ContainsKey(codepoint))
            return;

        var text = char.ConvertFromUtf32(codepoint);
        using var font = new SKFont(_typeface, _fontSize)
        {
            Edging = SKFontEdging.Antialias,
            Subpixel = false
        };
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = SKColors.White
        };

        font.MeasureText(text, out var bounds);
        float advance = font.MeasureText(text);

        const int pad = 2;
        int width = Math.Max(1, (int)Math.Ceiling(bounds.Width) + pad * 2);
        int height = Math.Max(1, (int)Math.Ceiling(bounds.Height) + pad * 2);

        using var bitmap = new SKBitmap(width, height, SKColorType.Alpha8, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawText(text, pad - bounds.Left, pad - bounds.Top, font, paint);
        }

        var pixels = bitmap.GetPixelSpan();
        int texture = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, texture);
        GL.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.R8, width, height, 0,
            PixelFormat.Red, PixelType.UnsignedByte, ref pixels[0]);
        GL.PixelStore(PixelStoreParameter.UnpackAlignment, 4);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

        _characters[codepoint] = new Character
        {
            TextureID = texture,
            Size = new Vector2(width, height),
            Bearing = new Vector2(pad - bounds.Left, bounds.Top - pad),
            Advance = advance
        };
    }

    /// <summary>
    /// Удаляет текстуры глифов, VBO/VAO и освобождает Skia typeface.
    /// </summary>
    public void Dispose()
    {
        foreach (var ch in _characters.Values)
            GL.DeleteTexture(ch.TextureID);

        GL.DeleteBuffer(_vbo);
        GL.DeleteVertexArray(_vao);
        _typeface.Dispose();
    }
}
