using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System.Drawing;
using System.Reflection;

namespace Mars;

/// <summary>
/// Полупрозрачная sci-fi панель-оверлей без содержимого: chamfer-рамка с cyan-свечением
/// и контуром; занимает половину экрана, позиция задаётся через <see cref="SetPanelOrigin"/>.
/// </summary>
public sealed class SciFiPanelOverlay : IDisposable
{
    private const float Chamfer = 10f;
    private const float OutlineWidth = 1.5f;

    private static readonly Vector4 CyanBorder = new(0.02f, 0.90f, 0.98f, 0.95f);
    private static readonly Vector4 CyanGlowOuter = new(0.02f, 0.90f, 0.98f, 0.10f);
    private static readonly Vector4 CyanGlowInner = new(0.02f, 0.90f, 0.98f, 0.22f);
    private static readonly Vector4 FillDark = new(0.04f, 0.06f, 0.07f, 0.40f);

    public bool IsVisible { get; set; }

    private Vector2 _panelOrigin;
    private int _screenW, _screenH;

    private int _vaoPoly, _vboPoly;
    private int _uboOrtho;
    private Shader _colorShader;
    private Matrix4 _ortho;

    /// <summary>Задаёт левый верхний угол панели в экранных координатах (origin для отрисовки и hit-test).</summary>
    public void SetPanelOrigin(Vector2 origin) => _panelOrigin = origin;

    /// <summary>
    /// Инициализирует GL-буферы для векторных полигонов, color shader, UBO ортографии
    /// и проекцию под заданные размеры framebuffer.
    /// </summary>
    public SciFiPanelOverlay(int w, int h)
    {
        _screenW = w;
        _screenH = h;
        InitPolyBuffers();
        InitShaders();
        InitUbo();
        UpdateOrtho();
    }

    /// <summary>Обновляет размеры экрана и пересчитывает ортографическую матрицу в UBO.</summary>
    public void UpdateScreenSize(int w, int h)
    {
        _screenW = w;
        _screenH = h;
        UpdateOrtho();
    }

    /// <summary>
    /// При <see cref="IsVisible"/> рисует sci-fi панель поверх 3D-сцены
    /// с отключённым depth test и альфа-блендингом; восстанавливает GL-состояние после отрисовки.
    /// </summary>
    public void Render()
    {
        if (!IsVisible)
            return;

        int[] polygonMode = new int[2];
        GL.GetInteger(GetPName.PolygonMode, polygonMode);

        GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
        GL.Disable(EnableCap.DepthTest);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        var size = new Vector2(_screenW * 0.5f, _screenH * 0.5f);
        DrawSciFiPanel(_panelOrigin, size, FillDark);

        GL.Disable(EnableCap.Blend);
        GL.Enable(EnableCap.DepthTest);
        GL.PolygonMode(MaterialFace.FrontAndBack, (PolygonMode)polygonMode[0]);
    }

    /// <summary>Возвращает true, если панель видима и точка клика попадает в её прямоугольник.</summary>
    public bool HandleMouseDown(float mouseX, float mouseY) =>
        IsVisible && GetPanelRect().Contains(mouseX, mouseY);

    /// <summary>Прямоугольник панели: 50% ширины и высоты экрана от <see cref="_panelOrigin"/>.</summary>
    public RectangleF GetPanelRect() =>
        new(_panelOrigin.X, _panelOrigin.Y, _screenW * 0.5f, _screenH * 0.5f);

    /// <summary>Строит chamfer-полигон панели, рисует свечение, заливку и cyan-контур.</summary>
    private void DrawSciFiPanel(Vector2 pos, Vector2 size, Vector4 fill)
    {
        var poly = BuildPanelPolygon(size, Chamfer);
        Offset(poly, pos);
        DrawPolygonGlow(poly);
        DrawFilledPolygon(poly, fill);
        DrawPolygonOutline(poly, CyanBorder, OutlineWidth, pos.Y);
    }

    /// <summary>Два слоя масштабированного полигона (1.02× и 1.008×) для эффекта внешнего и внутреннего свечения.</summary>
    private void DrawPolygonGlow(List<Vector2> poly)
    {
        var center = Centroid(poly);
        DrawFilledPolygon(ScalePolygon(poly, center, 1.02f), CyanGlowOuter);
        DrawFilledPolygon(ScalePolygon(poly, center, 1.008f), CyanGlowInner);
    }

    /// <summary>Строит пятиточечный полигон панели с фаской (chamfer) в правом верхнем углу.</summary>
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

    /// <summary>Сдвигает все вершины полигона на заданный вектор смещения.</summary>
    private static void Offset(List<Vector2> poly, Vector2 offset)
    {
        for (int i = 0; i < poly.Count; i++)
            poly[i] += offset;
    }

    /// <summary>Вычисляет центроид полигона как среднее арифметическое его вершин.</summary>
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

    /// <summary>Масштабирует полигон относительно центра на коэффициент <paramref name="scale"/>.</summary>
    private static List<Vector2> ScalePolygon(List<Vector2> poly, Vector2 center, float scale)
    {
        var result = new List<Vector2>(poly.Count);
        foreach (var p in poly)
            result.Add(center + (p - center) * scale);
        return result;
    }

    /// <summary>Триангулирует полигон веером и заливает его указанным цветом через color shader.</summary>
    private void DrawFilledPolygon(List<Vector2> poly, Vector4 color)
    {
        if (poly.Count < 3)
            return;

        UploadAndDraw(TriangulateFan(poly), color);
    }

    /// <summary>Разбивает выпуклый полигон на треугольники веером от первой вершины.</summary>
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

    /// <summary>Рисует контур полигона ребрами как утолщённые линии заданной ширины.</summary>
    private void DrawPolygonOutline(List<Vector2> poly, Vector4 color, float width, float minY)
    {
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % poly.Count];
            DrawLine(a, b, color, width, minY);
        }
    }

    /// <summary>Рисует отрезок quad'ом с толщиной <paramref name="width"/>; Y вершин не опускается ниже <paramref name="minY"/>.</summary>
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

    /// <summary>Ограничивает Y-координату точки снизу значением <paramref name="minY"/>.</summary>
    private static Vector2 ClampY(Vector2 p, float minY) => new(p.X, Math.Max(p.Y, minY));

    /// <summary>Загружает 2D-вершины в динамический VBO и рисует их треугольниками с uniform-цветом.</summary>
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

    /// <summary>Создаёт VAO/VBO для 2D-полигонов панели (атрибут position, 2 float на вершину).</summary>
    private void InitPolyBuffers()
    {
        _vaoPoly = GL.GenVertexArray();
        _vboPoly = GL.GenBuffer();

        GL.BindVertexArray(_vaoPoly);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vboPoly);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
        GL.EnableVertexAttribArray(0);
        GL.BindVertexArray(0);
    }

    /// <summary>Создаёт color shader для заливки полигонов и привязывает к нему UBO ортографии.</summary>
    private void InitShaders()
    {
        _colorShader = new Shader(VertexColor, FragmentColor, ShaderSourceMode.Code);
        BindUbo(_colorShader);
    }

    /// <summary>Выделяет uniform buffer для ортографической матрицы и привязывает к binding point 0.</summary>
    private void InitUbo()
    {
        _uboOrtho = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.UniformBuffer, _uboOrtho);
        GL.BufferData(BufferTarget.UniformBuffer, 64, IntPtr.Zero, BufferUsageHint.DynamicDraw);
        GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, _uboOrtho);
    }

    /// <summary>Привязывает uniform block «Ortho» шейдера к тому же binding point, что и UBO экрана.</summary>
    private static void BindUbo(Shader shader)
    {
        int program = (int)typeof(Shader)
            .GetField("_handle", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(shader)!;

        int index = GL.GetUniformBlockIndex(program, "Ortho");
        if (index >= 0)
            GL.UniformBlockBinding(program, index, 0);
    }

    /// <summary>Пересчитывает ортографию экрана (0..W, 0..H, Y вниз) и записывает матрицу в UBO.</summary>
    private void UpdateOrtho()
    {
        _ortho = Matrix4.CreateOrthographicOffCenter(0, _screenW, _screenH, 0, -1, 1);
        GL.BindBuffer(BufferTarget.UniformBuffer, _uboOrtho);
        GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, 64, ref _ortho);
    }

    /// <summary>Освобождает VAO, VBO полигонов и UBO ортографии.</summary>
    public void Dispose()
    {
        GL.DeleteBuffer(_vboPoly);
        GL.DeleteVertexArray(_vaoPoly);
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
