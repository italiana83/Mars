using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System.Drawing;
using System.Reflection;

namespace Mars;

/// <summary>
/// Sci-fi панель настроек: chamfer-рамка с cyan-свечением и вложенными строками параметров
/// (метка слева, переключатели «выкл» / «вкл» справа).
/// </summary>
public sealed class SciFiPanelOverlay : IDisposable
{
    private const int ToggleRowCount = 2;
    private const int HeightRowIndex = 2;
    private const int HeightmapStepRowIndex = 3;
    private const int MoveSpeedRowIndex = 4;
    private const int SettingsRowCount = 5;

    private const float PanelChamfer = 10f;
    private const float PanelOutlineWidth = 1.5f;
    private const float PanelPaddingMm = 3f;
    private const float RowGapMm = 2f;
    private const float RowChamfer = 6f;
    private const float ButtonWidthMm = 18f;
    private const float ButtonSpacingMm = 4f;
    private const float ButtonEdgeMarginMm = 3f;
    private const float ButtonChamfer = 5f;
    private const float LabelPaddingMm = 3f;
    private const float ButtonVerticalInsetPx = 1f;
    private const float HeightTriangleSizeMm = 6f;
    private const float HeightValueGapMm = 2f;
    private const float HeightScaleStep = 1f;
    private const float HeightScaleMax = 10f;
    private const int HeightmapStepMin = 1;
    private const int HeightmapStepMax = 32;
    private const float MoveSpeedMin = 5f;
    private const float MoveSpeedMax = 200f;
    private const float MoveSpeedStep = 5f;

    private const string FillLabel = "Заливка";
    private const string ChunksLabel = "Показывать чанки";
    private const string HeightLabel = "Высота";
    private const string HeightmapStepLabel = "Шаг выборки";
    private const string MoveSpeedLabel = "Скорость";
    private const string BtnOn = "вкл";
    private const string BtnOff = "выкл";

    private static readonly Vector4 CyanBorder = new(0.02f, 0.90f, 0.98f, 0.95f);
    private static readonly Vector4 RowFill = new(99f / 255f, 104f / 255f, 110f / 255f, 1f);
    private static readonly Vector4 ButtonActive = new(0.10f, 0.55f, 0.62f, 0.85f);
    private static readonly Vector4 ButtonActiveGlow = new(0.20f, 0.85f, 0.95f, 0.35f);
    private static readonly Vector4 ButtonOutlineInactive = new(0.02f, 0.90f, 0.98f, 0.45f);
    private static readonly Vector4 ButtonOutlineHover = new(0.02f, 0.90f, 0.98f, 0.70f);
    private static readonly Vector4 ButtonHover = new(0.08f, 0.28f, 0.32f, 0.55f);
    private static readonly Vector3 TextColor = new(0.94f, 0.95f, 0.93f);
    private static readonly Vector3 TextActiveColor = new(0.98f, 1f, 1f);

    public bool IsVisible { get; set; }

    /// <summary>Заливка меша (true) или каркас (false).</summary>
    public bool IsFillEnabled { get; private set; } = true;

    /// <summary>Отображение bbox чанков heightmap.</summary>
    public bool IsShowChunksEnabled { get; private set; }

    /// <summary>Масштаб вертикального рельефа меша (0 — плоскость, 1 — исходная высота).</summary>
    public float MeshHeightScale { get; private set; } = 1f;

    /// <summary>Шаг прореживания при чтении .img (1 — все сэмплы, 8 — каждый 8-й).</summary>
    public int HeightmapStep { get; private set; } = 8;

    /// <summary>Скорость перемещения камеры (WASD), единиц в секунду.</summary>
    public float MoveSpeed { get; private set; } = 50f;

    /// <summary>Вызывается после изменения <see cref="HeightmapStep"/> — нужно перечитать .img.</summary>
    public Action<int>? OnHeightmapStepChanged;

    /// <summary>Вызывается после изменения <see cref="MoveSpeed"/>.</summary>
    public Action<float>? OnMoveSpeedChanged;

    private Vector2 _panelOrigin;
    private int _screenW, _screenH;
    private float _scaleY;

    private int _vaoPoly, _vboPoly;
    private int _uboOrtho;
    private Shader _colorShader;
    private Matrix4 _ortho;

    private readonly TextRenderer _text;
    private int _hoveredButton = -1;
    private int _hoveredNumericRow = -1;
    private int _hoveredNumericDir = -1;

    private float RowHeight => _text.LineHeight + ButtonVerticalInsetPx * 2f;
    private float PanelPadding => UiScreen.MmToPixels(PanelPaddingMm, _scaleY);
    private float ButtonWidth => UiScreen.MmToPixels(ButtonWidthMm, _scaleY);
    private float ButtonSpacing => UiScreen.MmToPixels(ButtonSpacingMm, _scaleY);
    private float ButtonEdgeMargin => UiScreen.MmToPixels(ButtonEdgeMarginMm, _scaleY);
    private float RowGap => UiScreen.MmToPixels(RowGapMm, _scaleY);
    private float HeightTriangleSize => UiScreen.MmToPixels(HeightTriangleSizeMm, _scaleY);
    private float HeightValueGap => UiScreen.MmToPixels(HeightValueGapMm, _scaleY);
    private float LabelPadding => UiScreen.MmToPixels(LabelPaddingMm, _scaleY);
    private float ContentWidth => _screenW * 0.5f;
    private float PanelHeight =>
        PanelPadding * 2f + SettingsRowCount * RowHeight + (SettingsRowCount - 1) * RowGap;

    /// <summary>Задаёт левый верхний угол панели в экранных координатах.</summary>
    public void SetPanelOrigin(Vector2 origin) => _panelOrigin = origin;

    /// <summary>
    /// Инициализирует GL-буферы, текстовый рендер и ортографическую проекцию.
    /// </summary>
    public SciFiPanelOverlay(UiScreen screen)
    {
        _screenW = screen.FramebufferWidth;
        _screenH = screen.FramebufferHeight;
        _scaleY = screen.ScaleY;

        _text = new TextRenderer(TextRenderer.ResolveSystemFont(), _screenW, _screenH, 20f);
        _text.EnsureGlyphs($"{FillLabel} {ChunksLabel} {HeightLabel} {HeightmapStepLabel} {MoveSpeedLabel} {BtnOn} {BtnOff} 0123456789.");

        InitPolyBuffers();
        InitShaders();
        InitUbo();
        UpdateOrtho();
    }

    /// <summary>Обновляет размеры экрана и пересчитывает ортографическую матрицу.</summary>
    public void UpdateScreenSize(UiScreen screen)
    {
        _screenW = screen.FramebufferWidth;
        _screenH = screen.FramebufferHeight;
        _scaleY = screen.ScaleY;
        _text.UpdateScreenSize(_screenW, _screenH);
        UpdateOrtho();
    }

    /// <summary>Определяет кнопку переключателя под курсором для подсветки hover.</summary>
    public void UpdateMouse(float mouseX, float mouseY)
    {
        _hoveredButton = -1;
        _hoveredNumericRow = -1;
        _hoveredNumericDir = -1;
        if (!IsVisible)
            return;

        for (int row = 0; row < ToggleRowCount; row++)
        {
            if (GetOffButtonRect(row).Contains(mouseX, mouseY))
            {
                _hoveredButton = row * 2;
                return;
            }

            if (GetOnButtonRect(row).Contains(mouseX, mouseY))
            {
                _hoveredButton = row * 2 + 1;
                return;
            }
        }

        UpdateNumericRowHover(HeightRowIndex, mouseX, mouseY);
        if (_hoveredNumericRow >= 0)
            return;

        UpdateNumericRowHover(HeightmapStepRowIndex, mouseX, mouseY);
        if (_hoveredNumericRow >= 0)
            return;

        UpdateNumericRowHover(MoveSpeedRowIndex, mouseX, mouseY);
    }

    private void UpdateNumericRowHover(int rowIndex, float mouseX, float mouseY)
    {
        if (GetNumericDecreaseRect(rowIndex).Contains(mouseX, mouseY))
        {
            _hoveredNumericRow = rowIndex;
            _hoveredNumericDir = 0;
        }
        else if (GetNumericIncreaseRect(rowIndex).Contains(mouseX, mouseY))
        {
            _hoveredNumericRow = rowIndex;
            _hoveredNumericDir = 1;
        }
    }

    /// <summary>
    /// При <see cref="IsVisible"/> рисует sci-fi панель и строки настроек поверх сцены.
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

        DrawSettingsRows();
        DrawPanelOutline();

        GL.Disable(EnableCap.Blend);
        GL.Enable(EnableCap.DepthTest);
        GL.PolygonMode(MaterialFace.FrontAndBack, (PolygonMode)polygonMode[0]);
    }

    /// <summary>Обрабатывает клик по переключателям; возвращает true, если клик внутри панели.</summary>
    public bool HandleMouseDown(float mouseX, float mouseY)
    {
        if (!IsVisible)
            return false;

        for (int row = 0; row < ToggleRowCount; row++)
        {
            if (GetOffButtonRect(row).Contains(mouseX, mouseY))
            {
                SetRowEnabled(row, false);
                return true;
            }

            if (GetOnButtonRect(row).Contains(mouseX, mouseY))
            {
                SetRowEnabled(row, true);
                return true;
            }
        }

        if (GetNumericDecreaseRect(HeightRowIndex).Contains(mouseX, mouseY))
        {
            AdjustMeshHeightScale(-HeightScaleStep);
            return true;
        }

        if (GetNumericIncreaseRect(HeightRowIndex).Contains(mouseX, mouseY))
        {
            AdjustMeshHeightScale(HeightScaleStep);
            return true;
        }

        if (GetNumericDecreaseRect(HeightmapStepRowIndex).Contains(mouseX, mouseY))
        {
            AdjustHeightmapStep(-1);
            return true;
        }

        if (GetNumericIncreaseRect(HeightmapStepRowIndex).Contains(mouseX, mouseY))
        {
            AdjustHeightmapStep(1);
            return true;
        }

        if (GetNumericDecreaseRect(MoveSpeedRowIndex).Contains(mouseX, mouseY))
        {
            AdjustMoveSpeed(-MoveSpeedStep);
            return true;
        }

        if (GetNumericIncreaseRect(MoveSpeedRowIndex).Contains(mouseX, mouseY))
        {
            AdjustMoveSpeed(MoveSpeedStep);
            return true;
        }

        return GetPanelRect().Contains(mouseX, mouseY);
    }

    /// <summary>Сбрасывает масштаб высоты (например, после загрузки нового тайла).</summary>
    public void ResetMeshHeightScale(float scale = 1f) => MeshHeightScale = Math.Clamp(scale, 0f, HeightScaleMax);

    /// <summary>Синхронизирует шаг выборки с фактически загруженным мешем.</summary>
    public void SetHeightmapStep(int step) =>
        HeightmapStep = Math.Clamp(step, HeightmapStepMin, HeightmapStepMax);

    /// <summary>Задаёт скорость перемещения камеры.</summary>
    public void SetMoveSpeed(float speed)
    {
        MoveSpeed = Math.Clamp(speed, MoveSpeedMin, MoveSpeedMax);
        OnMoveSpeedChanged?.Invoke(MoveSpeed);
    }

    private void AdjustMeshHeightScale(float delta)
    {
        MeshHeightScale = Math.Clamp(MeshHeightScale + delta, 0f, HeightScaleMax);
    }

    private void AdjustHeightmapStep(int direction)
    {
        int next = direction < 0
            ? Math.Max(HeightmapStepMin, HeightmapStep / 2)
            : Math.Min(HeightmapStepMax, HeightmapStep * 2);

        if (next == HeightmapStep)
            return;

        HeightmapStep = next;
        OnHeightmapStepChanged?.Invoke(HeightmapStep);
    }

    private void AdjustMoveSpeed(float delta) => SetMoveSpeed(MoveSpeed + delta);

    private void SetRowEnabled(int rowIndex, bool enabled)
    {
        switch (rowIndex)
        {
            case 0:
                IsFillEnabled = enabled;
                break;
            case 1:
                IsShowChunksEnabled = enabled;
                break;
        }
    }

    private bool IsRowEnabled(int rowIndex) => rowIndex switch
    {
        0 => IsFillEnabled,
        1 => IsShowChunksEnabled,
        _ => false,
    };

    private static string GetRowLabel(int rowIndex) => rowIndex switch
    {
        0 => FillLabel,
        1 => ChunksLabel,
        2 => HeightLabel,
        3 => HeightmapStepLabel,
        4 => MoveSpeedLabel,
        _ => string.Empty,
    };

    private static string FormatHeightScale(float value) =>
        MathF.Abs(value - MathF.Round(value)) < 0.001f
            ? ((int)MathF.Round(value)).ToString()
            : value.ToString("0.#");

    /// <summary>Область hit-test: контур панели, высота зависит от числа строк.</summary>
    public RectangleF GetPanelRect() =>
        new(_panelOrigin.X, _panelOrigin.Y, ContentWidth, PanelHeight);

    /// <summary>Контур sci-fi панели без заливки.</summary>
    private void DrawPanelOutline()
    {
        var rect = GetPanelRect();
        var poly = BuildPanelPolygon(new Vector2(rect.Width, rect.Height), PanelChamfer);
        Offset(poly, new Vector2(rect.X, rect.Y));
        DrawPolygonOutline(poly, CyanBorder, PanelOutlineWidth, rect.Top);
    }

    /// <summary>Рисует вложенные строки настроек внутри панели.</summary>
    private void DrawSettingsRows()
    {
        for (int row = 0; row < ToggleRowCount; row++)
            DrawToggleRow(row);

        DrawNumericTriangleRow(
            HeightRowIndex,
            HeightLabel,
            FormatHeightScale(MeshHeightScale),
            GetHeightValueWidth());

        DrawNumericTriangleRow(
            HeightmapStepRowIndex,
            HeightmapStepLabel,
            HeightmapStep.ToString(),
            GetHeightmapStepValueWidth());

        DrawNumericTriangleRow(
            MoveSpeedRowIndex,
            MoveSpeedLabel,
            ((int)MoveSpeed).ToString(),
            GetMoveSpeedValueWidth());
    }

    /// <summary>Строка настройки: метка слева, «выкл» / «вкл» справа.</summary>
    private void DrawToggleRow(int rowIndex)
    {
        var row = GetRowRect(rowIndex);
        DrawSettingRowBackground(row);

        string label = GetRowLabel(rowIndex);
        float labelW = _text.MeasureTextWidth(label);
        _text.RenderTextCenteredInRect(
            new RectangleF(row.Left + LabelPadding, row.Top, labelW, row.Height),
            label,
            1f,
            TextColor);

        bool isOn = IsRowEnabled(rowIndex);
        DrawToggleButton(GetOffButtonRect(rowIndex), BtnOff, active: !isOn, buttonIndex: rowIndex * 2);
        DrawToggleButton(GetOnButtonRect(rowIndex), BtnOn, active: isOn, buttonIndex: rowIndex * 2 + 1);
    }

    /// <summary>Строка с треугольниками ◀ ▶ и числовым значением между ними.</summary>
    private void DrawNumericTriangleRow(int rowIndex, string label, string valueText, float valueWidth)
    {
        var row = GetRowRect(rowIndex);
        DrawSettingRowBackground(row);

        float labelW = _text.MeasureTextWidth(label);
        _text.RenderTextCenteredInRect(
            new RectangleF(row.Left + LabelPadding, row.Top, labelW, row.Height),
            label,
            1f,
            TextColor);

        var valueRect = GetNumericValueRect(rowIndex, valueWidth);
        _text.RenderTextCenteredInRect(valueRect, valueText, 1f, TextColor);

        bool hoveredDecrease = _hoveredNumericRow == rowIndex && _hoveredNumericDir == 0;
        bool hoveredIncrease = _hoveredNumericRow == rowIndex && _hoveredNumericDir == 1;
        DrawNumericTriangle(GetNumericDecreaseRect(rowIndex, valueWidth), pointRight: false, hoveredDecrease);
        DrawNumericTriangle(GetNumericIncreaseRect(rowIndex, valueWidth), pointRight: true, hoveredIncrease);
    }

    private void DrawNumericTriangle(RectangleF bounds, bool pointRight, bool hovered)
    {
        var center = new Vector2(bounds.X + bounds.Width * 0.5f, bounds.Y + bounds.Height * 0.5f);
        var poly = BuildEquilateralTriangle(center, HeightTriangleSize, pointRight);
        var fill = hovered ? ButtonHover : ButtonOutlineInactive;
        DrawFilledPolygon(poly, new Vector4(fill.X, fill.Y, fill.Z, 0.35f));
        DrawPolygonOutline(poly, hovered ? ButtonOutlineHover : CyanBorder, 1f, bounds.Top);
    }

    private float GetHeightValueWidth() =>
        MathF.Max(_text.MeasureTextWidth(FormatHeightScale(HeightScaleMax)), _text.MeasureTextWidth("0")) + 4f;

    private float GetHeightmapStepValueWidth() =>
        MathF.Max(_text.MeasureTextWidth(HeightmapStepMax.ToString()), _text.MeasureTextWidth("1")) + 4f;

    private float GetMoveSpeedValueWidth() =>
        MathF.Max(_text.MeasureTextWidth(((int)MoveSpeedMax).ToString()), _text.MeasureTextWidth("5")) + 4f;

    private float GetNumericValueWidth(int rowIndex) => rowIndex switch
    {
        HeightRowIndex => GetHeightValueWidth(),
        HeightmapStepRowIndex => GetHeightmapStepValueWidth(),
        MoveSpeedRowIndex => GetMoveSpeedValueWidth(),
        _ => GetHeightValueWidth(),
    };

    private RectangleF GetNumericValueRect(int rowIndex, float valueWidth)
    {
        var row = GetRowRect(rowIndex);
        float controlW = HeightTriangleSize * 2f + valueWidth + HeightValueGap * 2f;
        float x = row.Right - ButtonEdgeMargin - controlW + HeightTriangleSize + HeightValueGap;
        float y = row.Y + ButtonVerticalInsetPx;
        float h = row.Height - ButtonVerticalInsetPx * 2f;
        return new RectangleF(x, y, valueWidth, h);
    }

    private RectangleF GetNumericDecreaseRect(int rowIndex) =>
        GetNumericDecreaseRect(rowIndex, GetNumericValueWidth(rowIndex));

    private RectangleF GetNumericIncreaseRect(int rowIndex) =>
        GetNumericIncreaseRect(rowIndex, GetNumericValueWidth(rowIndex));

    private RectangleF GetNumericDecreaseRect(int rowIndex, float valueWidth) =>
        GetNumericTriangleRect(rowIndex, valueWidth, decrease: true);

    private RectangleF GetNumericIncreaseRect(int rowIndex, float valueWidth) =>
        GetNumericTriangleRect(rowIndex, valueWidth, decrease: false);

    private RectangleF GetNumericTriangleRect(int rowIndex, float valueWidth, bool decrease)
    {
        var row = GetRowRect(rowIndex);
        float controlW = HeightTriangleSize * 2f + valueWidth + HeightValueGap * 2f;
        float controlLeft = row.Right - ButtonEdgeMargin - controlW;
        float y = row.Y + ButtonVerticalInsetPx;
        float h = row.Height - ButtonVerticalInsetPx * 2f;

        if (decrease)
            return new RectangleF(controlLeft, y, HeightTriangleSize, h);

        float x = controlLeft + HeightTriangleSize + HeightValueGap + valueWidth + HeightValueGap;
        return new RectangleF(x, y, HeightTriangleSize, h);
    }

    private static List<Vector2> BuildEquilateralTriangle(Vector2 center, float size, bool pointRight)
    {
        float halfH = size * 0.5f;
        float halfW = size * MathF.Sqrt(3f) / 4f;

        if (pointRight)
        {
            return new List<Vector2>
            {
                new(center.X - halfH, center.Y - halfW),
                new(center.X - halfH, center.Y + halfW),
                new(center.X + halfH, center.Y),
            };
        }

        return new List<Vector2>
        {
            new(center.X + halfH, center.Y - halfW),
            new(center.X + halfH, center.Y + halfW),
            new(center.X - halfH, center.Y),
        };
    }

    /// <summary>Прямоугольник строки настройки по индексу (0 — верхняя).</summary>
    private RectangleF GetRowRect(int rowIndex)
    {
        float y = _panelOrigin.Y + PanelPadding + rowIndex * (RowHeight + RowGap);
        float x = _panelOrigin.X + PanelPadding;
        float w = ContentWidth - PanelPadding * 2f;
        return new RectangleF(x, y, w, RowHeight);
    }

    /// <summary>Тёмная полупрозрачная подложка строки настройки.</summary>
    private void DrawSettingRowBackground(RectangleF row)
    {
        var poly = BuildPanelPolygon(new Vector2(row.Width, row.Height), RowChamfer);
        Offset(poly, new Vector2(row.X, row.Y));
        DrawFilledPolygon(poly, RowFill);
        DrawPolygonOutline(poly, CyanBorder, 1f, row.Top);
    }

    /// <summary>Кнопка переключателя со скосами левого нижнего и правого верхнего углов.</summary>
    private void DrawToggleButton(RectangleF rect, string label, bool active, int buttonIndex)
    {
        var poly = BuildToggleButtonPolygon(new Vector2(rect.Width, rect.Height), ButtonChamfer);
        Offset(poly, new Vector2(rect.X, rect.Y));

        bool hovered = _hoveredButton == buttonIndex;

        if (active)
        {
            var center = Centroid(poly);
            DrawFilledPolygon(ScalePolygon(poly, center, 1.04f), ButtonActiveGlow);
            DrawFilledPolygon(poly, ButtonActive);
            DrawPolygonOutline(poly, CyanBorder, 1f, rect.Top);
        }
        else
        {
            DrawPolygonOutline(poly, hovered ? ButtonOutlineHover : ButtonOutlineInactive, 1f, rect.Top);
        }

        var textColor = active ? TextActiveColor : TextColor;
        _text.RenderTextCenteredInRect(rect, label, 1f, textColor);
    }

    private RectangleF GetOffButtonRect(int rowIndex) => GetToggleButtonRect(rowIndex, onRight: false);

    private RectangleF GetOnButtonRect(int rowIndex) => GetToggleButtonRect(rowIndex, onRight: true);

    private RectangleF GetToggleButtonRect(int rowIndex, bool onRight)
    {
        var row = GetRowRect(rowIndex);
        float btnH = row.Height - ButtonVerticalInsetPx * 2f;
        float btnY = row.Y + ButtonVerticalInsetPx;

        float onX = row.Right - ButtonEdgeMargin - ButtonWidth;
        if (onRight)
            return new RectangleF(onX, btnY, ButtonWidth, btnH);

        float offX = onX - ButtonSpacing - ButtonWidth;
        return new RectangleF(offX, btnY, ButtonWidth, btnH);
    }

    /// <summary>Шестиугольник кнопки: скос правого верхнего и левого нижнего углов.</summary>
    private static List<Vector2> BuildToggleButtonPolygon(Vector2 size, float chamfer)
    {
        float w = size.X;
        float h = size.Y;
        chamfer = Math.Min(chamfer, Math.Min(w * 0.35f, h * 0.35f));

        return new List<Vector2>
        {
            new(0f, 0f),
            new(w - chamfer, 0f),
            new(w, chamfer),
            new(w, h),
            new(chamfer, h),
            new(0f, h - chamfer),
        };
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

        UploadAndDraw(TriangulateFan(poly), color);
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

    private void InitShaders()
    {
        _colorShader = new Shader(VertexColor, FragmentColor, ShaderSourceMode.Code);
        BindUbo(_colorShader);
    }

    private void InitUbo()
    {
        _uboOrtho = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.UniformBuffer, _uboOrtho);
        GL.BufferData(BufferTarget.UniformBuffer, 64, IntPtr.Zero, BufferUsageHint.DynamicDraw);
        GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, _uboOrtho);
    }

    private static void BindUbo(Shader shader)
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
        _text.Dispose();
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
