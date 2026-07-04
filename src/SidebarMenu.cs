using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System.Drawing;
using System.Reflection;

namespace Mars;

public sealed class SidebarMenu : IDisposable
{
    private float _scaleY = 1f;
    private float _blockGapPx;
    private float _itemInsetPx;
    private float MenuTop => UiScreen.MmToPixels(5f, _scaleY);
    private float BlockGap => _blockGapPx;
    private float ItemInset => _itemInsetPx;

    private const float Margin = 12f;
    private const float Width = 280f;
    private const float HeaderTextPadding = 5f;
    private const float ItemHeight = 38f;
    private const float Chamfer = 10f;
    private const float HeaderTabWidth = 44f;
    private const float HeaderTabHeight = 6f;
    private const float HeaderToItemsExtraGap = 2f;
    private const float OutlineWidth = 1.5f;

    private static readonly Vector4 CyanBorder = new(0.02f, 0.90f, 0.98f, 0.95f);
    private static readonly Vector4 CyanGlowOuter = new(0.02f, 0.90f, 0.98f, 0.10f);
    private static readonly Vector4 CyanGlowInner = new(0.02f, 0.90f, 0.98f, 0.22f);
    private static readonly Vector4 FillDark = new(0.04f, 0.06f, 0.07f, 0.40f);
    private static readonly Vector4 FillSelected = new(0.05f, 0.35f, 0.40f, 0.22f);
    private static readonly Vector4 FillHover = new(0.04f, 0.22f, 0.26f, 0.18f);
    private static readonly Vector3 TextColor = new(0.94f, 0.95f, 0.93f);
    private static readonly Vector3 TextSelectedColor = new(0.98f, 1f, 1f);

    private const string HeaderTitle = "Меню";
    private static readonly string[] MenuItems = { "Мини карта", "Настройки" };

    private readonly TextRenderer _text;
    private int _screenW, _screenH;
    private int _vaoPoly;
    private int _vboPoly;
    private int _uboOrtho;
    private Shader _colorShader;
    private Matrix4 _ortho;
    private int _hoveredItem = -1;

    private readonly float _headerTextHeight;

    private float HeaderHeight => _headerTextHeight + HeaderTextPadding * 2f;

    public bool IsExpanded { get; private set; }
    public bool IsMinimapVisible { get; private set; }
    public bool IsSettingsVisible { get; private set; }

    public float BottomY
    {
        get
        {
            float h = HeaderHeight;
            if (IsExpanded)
                h += BlockGap + HeaderToItemsExtraGap + MenuItems.Length * ItemHeight + (MenuItems.Length - 1) * BlockGap;
            return MenuTop + h;
        }
    }

    public SidebarMenu(UiScreen screen)
    {
        _screenW = screen.FramebufferWidth;
        _screenH = screen.FramebufferHeight;
        _scaleY = screen.ScaleY;
        _blockGapPx = UiScreen.MmToPixels(1f, _scaleY);
        _itemInsetPx = UiScreen.MmToPixels(3f, _scaleY);

        _text = new TextRenderer(TextRenderer.ResolveSystemFont(), _screenW, _screenH, 24f);
        _headerTextHeight = _text.MeasureTextHeight(HeaderTitle);
        _text.EnsureGlyphs(HeaderTitle + " " + string.Join(' ', MenuItems));

        InitGl();
        UpdateOrtho();
    }

    public void UpdateScreenSize(UiScreen screen)
    {
        _screenW = screen.FramebufferWidth;
        _screenH = screen.FramebufferHeight;
        _scaleY = screen.ScaleY;
        _blockGapPx = UiScreen.MmToPixels(1f, _scaleY);
        _itemInsetPx = UiScreen.MmToPixels(3f, _scaleY);
        _text.UpdateScreenSize(_screenW, _screenH);
        UpdateOrtho();
    }

    public void UpdateMouse(float mouseX, float mouseY)
    {
        _hoveredItem = -1;
        if (!IsExpanded)
            return;

        for (int i = 0; i < MenuItems.Length; i++)
        {
            if (GetItemRect(i).Contains(mouseX, mouseY))
            {
                _hoveredItem = i;
                break;
            }
        }
    }

    public bool HandleMouseDown(float mouseX, float mouseY)
    {
        if (GetHeaderRect().Contains(mouseX, mouseY))
        {
            IsExpanded = !IsExpanded;
            if (!IsExpanded)
            {
                _hoveredItem = -1;
                IsMinimapVisible = false;
                IsSettingsVisible = false;
            }
            return true;
        }

        if (!IsExpanded)
            return false;

        for (int i = 0; i < MenuItems.Length; i++)
        {
            if (!GetItemRect(i).Contains(mouseX, mouseY))
                continue;

            if (i == 0)
            {
                IsMinimapVisible = !IsMinimapVisible;
                if (IsMinimapVisible)
                    IsSettingsVisible = false;
            }
            else if (i == 1)
            {
                IsSettingsVisible = !IsSettingsVisible;
                if (IsSettingsVisible)
                    IsMinimapVisible = false;
            }

            return true;
        }

        return GetPanelBounds().Contains(mouseX, mouseY);
    }

    public void Render()
    {
        int[] polygonMode = new int[2];
        GL.GetInteger(GetPName.PolygonMode, polygonMode);

        GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
        GL.Disable(EnableCap.DepthTest);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        DrawHeaderPanel();
        if (IsExpanded)
        {
            for (int i = 0; i < MenuItems.Length; i++)
                DrawItemPanel(i);
        }

        GL.Disable(EnableCap.Blend);
        GL.Enable(EnableCap.DepthTest);
        GL.PolygonMode(MaterialFace.FrontAndBack, (PolygonMode)polygonMode[0]);
    }

    private void DrawHeaderPanel()
    {
        var pos = new Vector2(Margin, MenuTop);
        var size = new Vector2(Width, HeaderHeight);

        DrawSciFiPanel(pos, size, FillDark, drawGlow: false);

        var tabX = pos.X + size.X * 0.5f - HeaderTabWidth * 0.5f;
        DrawSciFiPanel(
            new Vector2(tabX, pos.Y + 2f),
            new Vector2(HeaderTabWidth, HeaderTabHeight),
            FillDark,
            drawGlow: false);

        _text.RenderTextFromTop(HeaderTitle, pos.X + 16f, pos.Y + HeaderTextPadding, 1f, TextColor);
    }

    private void DrawItemPanel(int index)
    {
        var rect = GetItemRect(index);
        var pos = new Vector2(rect.X, rect.Y);
        var size = new Vector2(rect.Width, rect.Height);

        bool selected = (index == 0 && IsMinimapVisible) || (index == 1 && IsSettingsVisible);
        bool hovered = index == _hoveredItem;
        var fill = selected ? FillSelected : hovered ? FillHover : FillDark;

        DrawSciFiPanel(pos, size, fill);

        var textColor = selected ? TextSelectedColor : TextColor;
        _text.RenderText(MenuItems[index], pos.X + 16f, TextY(pos.Y, size.Y), 1f, textColor);
    }

    private void DrawSciFiPanel(Vector2 pos, Vector2 size, Vector4 fill, bool drawGlow = true)
    {
        var poly = BuildPanelPolygon(size, Chamfer);
        Offset(poly, pos);
        if (drawGlow)
            DrawPolygonGlow(poly);
        DrawFilledPolygon(poly, fill);
        DrawPolygonOutline(poly, CyanBorder, OutlineWidth, pos.Y);
    }

    private void DrawPolygonGlow(List<Vector2> poly)
    {
        var center = Centroid(poly);
        DrawFilledPolygon(ScalePolygon(poly, center, 1.02f), CyanGlowOuter);
        DrawFilledPolygon(ScalePolygon(poly, center, 1.008f), CyanGlowInner);
    }

    private static List<Vector2> BuildPanelPolygon(Vector2 size, float chamfer)
    {
        float w = size.X;
        float h = size.Y;
        chamfer = Math.Min(chamfer, Math.Min(w * 0.4f, h * 0.4f));

        return new List<Vector2>
        {
            new(0f, 0f),
            new(w - chamfer, 0f),
            new(w, chamfer),
            new(w, h),
            new(0f, h),
        };
    }

    private static void Offset(List<Vector2> poly, Vector2 offset)
    {
        for (int i = 0; i < poly.Count; i++)
            poly[i] += offset;
    }

    private static Vector2 Centroid(List<Vector2> poly)
    {
        float x = 0, y = 0;
        foreach (var p in poly)
        {
            x += p.X;
            y += p.Y;
        }

        return new Vector2(x / poly.Count, y / poly.Count);
    }

    private static List<Vector2> ScalePolygon(List<Vector2> poly, Vector2 center, float scale)
    {
        var result = new List<Vector2>(poly.Count);
        foreach (var p in poly)
            result.Add(center + (p - center) * scale);
        return result;
    }

    private void DrawFilledPolygon(List<Vector2> poly, Vector4 color)
    {
        if (poly.Count < 3)
            return;

        var triangles = TriangulateFan(poly);
        UploadAndDraw(triangles, color);
    }

    private static List<Vector2> TriangulateFan(List<Vector2> poly)
    {
        var tris = new List<Vector2>();
        for (int i = 1; i < poly.Count - 1; i++)
        {
            tris.Add(poly[0]);
            tris.Add(poly[i]);
            tris.Add(poly[i + 1]);
        }

        return tris;
    }

    private void DrawPolygonOutline(List<Vector2> poly, Vector4 color, float width, float minY)
    {
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % poly.Count];
            DrawLine(a, b, color, width, minY);
        }
    }

    private void DrawLine(Vector2 a, Vector2 b, Vector4 color, float width, float minY)
    {
        var dir = b - a;
        float len = dir.Length;
        if (len < 0.001f)
            return;

        dir /= len;
        var normal = new Vector2(-dir.Y, dir.X) * width * 0.5f;

        var quad = new List<Vector2>
        {
            ClampY(a - normal, minY),
            ClampY(a + normal, minY),
            ClampY(b + normal, minY),
            ClampY(b - normal, minY),
        };

        UploadAndDraw(new List<Vector2>
        {
            quad[0], quad[1], quad[2],
            quad[0], quad[2], quad[3],
        }, color);
    }

    private static Vector2 ClampY(Vector2 p, float minY) => new(p.X, Math.Max(p.Y, minY));

    private void UploadAndDraw(List<Vector2> vertices, Vector4 color)
    {
        if (vertices.Count == 0)
            return;

        var data = new float[vertices.Count * 2];
        for (int i = 0; i < vertices.Count; i++)
        {
            data[i * 2] = vertices[i].X;
            data[i * 2 + 1] = vertices[i].Y;
        }

        _colorShader.Use();
        _colorShader.SetVector4("uColor", color);

        GL.BindVertexArray(_vaoPoly);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vboPoly);
        GL.BufferData(BufferTarget.ArrayBuffer, data.Length * sizeof(float), data, BufferUsageHint.DynamicDraw);
        GL.DrawArrays(PrimitiveType.Triangles, 0, vertices.Count);
    }

    private RectangleF GetPanelBounds()
    {
        var bounds = GetHeaderRect();
        if (!IsExpanded)
            return bounds;

        for (int i = 0; i < MenuItems.Length; i++)
            bounds = RectangleF.Union(bounds, GetItemRect(i));
        return bounds;
    }

    private float TextY(float blockY, float blockHeight) =>
        blockY + (blockHeight - _text.LineHeight) * 0.5f;

    private RectangleF GetHeaderRect() => new(Margin, MenuTop, Width, HeaderHeight);

    private float ItemsStartY() => MenuTop + HeaderHeight + BlockGap + HeaderToItemsExtraGap;

    private RectangleF GetItemRect(int index)
    {
        float y = ItemsStartY() + index * (ItemHeight + BlockGap);
        return new RectangleF(Margin + ItemInset, y, Width - ItemInset, ItemHeight);
    }

    private void InitGl()
    {
        _vaoPoly = GL.GenVertexArray();
        _vboPoly = GL.GenBuffer();

        GL.BindVertexArray(_vaoPoly);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vboPoly);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
        GL.EnableVertexAttribArray(0);
        GL.BindVertexArray(0);

        _colorShader = new Shader(VertexColor, FragmentColor, ShaderSourceMode.Code);
        BindUbo(_colorShader);

        _uboOrtho = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.UniformBuffer, _uboOrtho);
        GL.BufferData(BufferTarget.UniformBuffer, 64, IntPtr.Zero, BufferUsageHint.DynamicDraw);
        GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, _uboOrtho);
    }

    private void BindUbo(Shader shader)
    {
        int program = (int)typeof(Shader)
            .GetField("_handle", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(shader)!;

        int index = GL.GetUniformBlockIndex(program, "Ortho");
        if (index >= 0)
            GL.UniformBlockBinding(program, index, 0);
    }

    private void UpdateOrtho()
    {
        _ortho = Matrix4.CreateOrthographicOffCenter(0, _screenW, _screenH, 0, -1, 1);
        GL.BindBuffer(BufferTarget.UniformBuffer, _uboOrtho);
        GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, 64, ref _ortho);
    }

    public void Dispose()
    {
        GL.DeleteVertexArray(_vaoPoly);
        GL.DeleteBuffer(_vboPoly);
        GL.DeleteBuffer(_uboOrtho);
    }

    private const string VertexColor = @"
#version 330 core
layout(location=0) in vec2 aPos;
layout(std140) uniform Ortho { mat4 uOrtho; };
void main() {
    gl_Position = uOrtho * vec4(aPos, 0, 1);
}";

    private const string FragmentColor = @"
#version 330 core
uniform vec4 uColor;
out vec4 FragColor;
void main() {
    FragColor = uColor;
}";
}
