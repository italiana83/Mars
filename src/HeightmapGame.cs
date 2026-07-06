using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System.Diagnostics;

namespace Mars;

/// <summary>
/// Главное окно приложения: 3D-просмотр MOLA heightmap, камера, UI (меню, миникарта, настройки).
/// </summary>
public class HeightmapGame : GameWindow
{
    private const int DefaultHeightmapStep = 8;
    private const string DefaultTileName = "megt00n000hb";

    private MapData mapData;
    private string _currentTileName = DefaultTileName;

    BoundingBoxRenderer boundingBoxRenderer;
    AxisRender axisRender;
    MeshRender meshRender;
    Matrix4 projection;

    Camera cam = new(10.2f, 0.02f);
    private bool mouseDown = false;
    Vector2 lastMousePos = new();
    private Frustum frustum = new();

    private int _frameCount;
    private double _elapsedTime;
    private double _fps;

    Minimap minimap;
    SciFiPanelOverlay settingsPanel;
    SidebarMenu sidebarMenu;
    UiScreen uiScreen;

    /// <summary>
    /// Инициализирует окно OpenTK 3.3 Core и загружает heightmap MEG128 из PDS-файлов (.lbl + .img).
    /// </summary>
    public HeightmapGame()
        : base(
            new GameWindowSettings { UpdateFrequency = 60.0 },
            new NativeWindowSettings
            {
                IsEventDriven = true,
                APIVersion = new Version(3, 3),
                Profile = ContextProfile.Core,
                NumberOfSamples = 0,
                AlphaBits = 8
            })
    {
        mapData = LoadTopographyTile(DefaultTileName, DefaultHeightmapStep);
    }

    private MapData LoadTopographyTile(string tileBaseName, int step)
    {
        var reader = new MolaDataReader();
        return reader.LoadTopographyTile(AppPaths.Meg128Directory, tileBaseName, step);
    }

    private int GetHeightmapStep() => settingsPanel?.HeightmapStep ?? DefaultHeightmapStep;

    private void ReloadCurrentTopographyTile()
    {
        try
        {
            var newData = LoadTopographyTile(_currentTileName, GetHeightmapStep());
            var newMesh = new MeshRender(newData);
            var newAxis = new AxisRender(100.0f, 2.5f, 10.0f, newMesh.ModelCenter);

            meshRender.Dispose();
            axisRender.Dispose();

            mapData = newData;
            meshRender = newMesh;
            axisRender = newAxis;
            boundingBoxRenderer.CreateBoundingBox(meshRender.Min, meshRender.Max);

            minimap.SetSelectedTile(_currentTileName);
            settingsPanel.ResetMeshHeightScale(1f);
            meshRender.SetHeightScale(1f);
            boundingBoxRenderer.CreateBoundingBox(meshRender.Min, meshRender.Max);
            Title = $"Mars MOLA Viewer - {_currentTileName.ToUpperInvariant()}";
        }
        catch (FileNotFoundException)
        {
            Title = $"Mars MOLA Viewer - {_currentTileName}.img not found";
        }
        catch (Exception ex)
        {
            Title = $"Mars MOLA Viewer - load failed: {_currentTileName}";
            Trace.WriteLine($"Failed to load tile {_currentTileName}: {ex}");
        }
    }

    private void ReloadTopographyTile(Meg128TileBounds tile)
    {
        _currentTileName = tile.Name;
        ReloadCurrentTopographyTile();
    }

    /// <summary>
    /// Вызывается после создания контекста OpenGL: настраивает UI, minimap, панели, рендереры меша, осей и bounding box.
    /// </summary>
    protected override void OnLoad()
    {
        base.OnLoad();

        UpdateUiLayout();

        var minimapImage = AppPaths.FindMinimapImage()
            ?? throw new FileNotFoundException(
                "Minimap image not found. Place Mars_topography_(MOLA_dataset)_HiRes.png (or .jpg) in the data/ folder.",
                AppPaths.DataPath("Mars_topography_(MOLA_dataset)_HiRes.png"));

        minimap = new Minimap(minimapImage, uiScreen.FramebufferWidth, uiScreen.FramebufferHeight, uiScreen.ScaleY);
        settingsPanel = new SciFiPanelOverlay(uiScreen);
        settingsPanel.SetHeightmapStep(DefaultHeightmapStep);
        settingsPanel.OnHeightmapStepChanged = _ => ReloadCurrentTopographyTile();
        sidebarMenu = new SidebarMenu(uiScreen);

        GL.ClearColor(0.1f, 0.2f, 0.3f, 1.0f);
        GL.Enable(EnableCap.DepthTest);
        GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
        GL.ClearDepth(1.0f);
        GL.DepthFunc(DepthFunction.Lequal);
        GL.Hint(HintTarget.PolygonSmoothHint, HintMode.Nicest);

        meshRender = new MeshRender(mapData);
        cam.Position = new Vector3(749, 198, 203);
        cam.Orientation = new Vector3(3.09f, -1.190f, 0.0f);
        boundingBoxRenderer = new BoundingBoxRenderer();
        boundingBoxRenderer.CreateBoundingBox(meshRender.Min, meshRender.Max);
        axisRender = new AxisRender(100.0f, 2.5f, 10.0f, meshRender.ModelCenter);
        minimap.SetSelectedTile(DefaultTileName);
    }

    /// <summary>
    /// Обрабатывает нажатие левой кнопки: передаёт событие UI (sidebar, minimap, settings) или начинает захват мыши для камеры.
    /// </summary>
    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button == MouseButton.Left)
        {
            var mouse = uiScreen.ClientToFramebuffer(MouseState.X, MouseState.Y);
            if (sidebarMenu.HandleMouseDown(mouse.X, mouse.Y))
                return;

            minimap.IsVisible = sidebarMenu.IsMinimapVisible;
            if (minimap.IsVisible && minimap.HandleMouseDown(mouse.X, mouse.Y))
            {
                if (minimap.TryPickTile(mouse.X, mouse.Y, out var tile))
                    ReloadTopographyTile(tile);
                return;
            }

            settingsPanel.IsVisible = sidebarMenu.IsSettingsVisible;
            if (settingsPanel.IsVisible && settingsPanel.HandleMouseDown(mouse.X, mouse.Y))
            {
                ApplyMeshHeightFromSettings();
                return;
            }
        }

        mouseDown = true;
        lastMousePos = new Vector2(MouseState.X, MouseState.Y);
    }

    /// <summary>
    /// Сбрасывает флаг захвата мыши при отпускании кнопки.
    /// </summary>
    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        mouseDown = false;
    }

    /// <summary>
    /// Вращает камеру левой кнопкой, перемещает по Z правой; обновляет hover-состояние sidebar.
    /// </summary>
    protected override void OnMouseMove(MouseMoveEventArgs e)
    {
        base.OnMouseMove(e);
        var mouse = uiScreen.ClientToFramebuffer(MouseState.X, MouseState.Y);
        sidebarMenu.UpdateMouse(mouse.X, mouse.Y);

        minimap.IsVisible = sidebarMenu.IsMinimapVisible;
        if (minimap.IsVisible)
            minimap.UpdateMouse(mouse.X, mouse.Y);

        settingsPanel.IsVisible = sidebarMenu.IsSettingsVisible;
        if (settingsPanel.IsVisible)
            settingsPanel.UpdateMouse(mouse.X, mouse.Y);

        if (!mouseDown)
            return;

        if (MouseState.IsButtonDown(MouseButton.Left))
        {
            Vector2 delta = lastMousePos - new Vector2(e.X, e.Y);
            cam.AddRotation(delta.X / 10, delta.Y / 10);
            lastMousePos = new Vector2(e.X, e.Y);
        }

        if (MouseState.IsButtonDown(MouseButton.Right))
        {
            if (e.Y > lastMousePos.Y)
                cam.Move(0f, 0f, 0.1f);
            else
                cam.Move(0f, 0f, -0.1f);

            lastMousePos = new Vector2(e.X, e.Y);
        }
    }

    /// <summary>
    /// Приближает или отдаляет камеру по оси Y в зависимости от направления прокрутки колёсика.
    /// </summary>
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        if (e.OffsetY > 0)
            cam.Move(0f, 10000.0f, 0f);
        else if (e.OffsetY < 0)
            cam.Move(0f, -10000.0f, 0f);
    }

    /// <summary>
    /// Обновляет счётчик кадров и раз в секунду выводит FPS в заголовок окна.
    /// </summary>
    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);

        _frameCount++;
        _elapsedTime += args.Time;

        if (_elapsedTime >= 1.0)
        {
            _fps = _frameCount / _elapsedTime;
            _frameCount = 0;
            _elapsedTime = 0.0;
            Title = $"Mars MOLA Viewer - FPS: {_fps:F2}";
        }
    }

    /// <summary>
    /// Очищает буферы, отрисовывает 3D-сцену (оси, меш, AABB) и поверх — UI (minimap, панели, sidebar).
    /// </summary>
    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);

        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        Matrix4 view = cam.GetViewMatrix();
        frustum.Update(view * projection);

        axisRender.DrawAxis(view, projection);

        GL.PolygonMode(
            MaterialFace.FrontAndBack,
            settingsPanel.IsFillEnabled ? PolygonMode.Fill : PolygonMode.Line);

        meshRender.DrawMesh(view, projection, Matrix4.Identity, cam.Position, frustum);

        if (settingsPanel.IsShowChunksEnabled)
            meshRender.DrawChunkBounds(view, projection);

        boundingBoxRenderer.DrawBoundingBox(view, projection);

        var panelOrigin = new Vector2(12f, sidebarMenu.BottomY + 8f);

        minimap.IsVisible = sidebarMenu.IsMinimapVisible;
        minimap.SetPanelOrigin(panelOrigin);
        minimap.Render((float)args.Time);

        settingsPanel.IsVisible = sidebarMenu.IsSettingsVisible;
        settingsPanel.SetPanelOrigin(panelOrigin);
        settingsPanel.Render();

        sidebarMenu.Render();

        SwapBuffers();
    }

    /// <summary>
    /// Пересчитывает layout UI, viewport и матрицу проекции при изменении размера окна или первой загрузке.
    /// </summary>
    private void UpdateUiLayout()
    {
        uiScreen = UiScreen.From(this);

        int fbW = Math.Max(1, uiScreen.FramebufferWidth);
        int fbH = Math.Max(1, uiScreen.FramebufferHeight);
        GL.Viewport(0, 0, fbW, fbH);
        minimap?.UpdateScreenSize(fbW, fbH, uiScreen.ScaleY);
        settingsPanel?.UpdateScreenSize(uiScreen);
        sidebarMenu?.UpdateScreenSize(uiScreen);

        if (uiScreen.ClientWidth <= 0 || uiScreen.ClientHeight <= 0)
            return;

        projection = Matrix4.CreatePerspectiveFieldOfView(
            MathHelper.DegreesToRadians(45.0f),
            uiScreen.ClientWidth / (float)uiScreen.ClientHeight,
            0.1f,
            90000.0f);
    }

    private void ApplyMeshHeightFromSettings()
    {
        meshRender.SetHeightScale(settingsPanel.MeshHeightScale);
        boundingBoxRenderer.CreateBoundingBox(meshRender.Min, meshRender.Max);
    }

    /// <summary>
    /// Реагирует на изменение размера окна: пересчитывает UI и projection.
    /// </summary>
    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        UpdateUiLayout();
    }
}
