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
    private const float DefaultMoveSpeed = 50f;

    private MapData mapData;
    private string _currentTileName = DefaultTileName;

    BoundingBoxRenderer boundingBoxRenderer;
    AxisRender axisRender;
    MeshRender meshRender;
    Matrix4 projection;

    private const float MaxMovementDelta = 1f / 60f;
    private const float WheelStepDistance = 15f;

    Camera cam = new(DefaultMoveSpeed, 0.003f);
    private bool _cameraLookActive;
    private Vector2 _lastMousePos;
    private Frustum frustum = new();

    private int _frameCount;
    private double _elapsedTime;
    private double _fps;

    Minimap minimap;
    SciFiPanelOverlay settingsPanel;
    SidebarMenu sidebarMenu;
    ControlsHintOverlay controlsHint;
    UiScreen uiScreen;

    /// <summary>
    /// Инициализирует окно OpenTK 3.3 Core и загружает heightmap MEG128 из PDS-файлов (.lbl + .img).
    /// </summary>
    public HeightmapGame()
        : base(
            new GameWindowSettings { UpdateFrequency = 60.0 },
            new NativeWindowSettings
            {
                IsEventDriven = false,
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
            Title = Localization.TileTitle(_currentTileName);
        }
        catch (FileNotFoundException)
        {
            Title = Localization.TileNotFoundTitle(_currentTileName);
        }
        catch (Exception ex)
        {
            Title = Localization.TileLoadFailedTitle(_currentTileName);
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
                Localization.MinimapImageNotFound,
                AppPaths.DataPath("Mars_topography_(MOLA_dataset)_HiRes.png"));

        minimap = new Minimap(minimapImage, uiScreen.FramebufferWidth, uiScreen.FramebufferHeight, uiScreen.ScaleY);
        settingsPanel = new SciFiPanelOverlay(uiScreen);
        settingsPanel.SetHeightmapStep(DefaultHeightmapStep);
        settingsPanel.SetMoveSpeed(cam.MoveSpeed);
        settingsPanel.OnHeightmapStepChanged = _ => ReloadCurrentTopographyTile();
        settingsPanel.OnMoveSpeedChanged = speed => cam.MoveSpeed = speed;
        Localization.LanguageChanged += () => Title = Localization.FpsTitle(_fps);
        sidebarMenu = new SidebarMenu(uiScreen);
        controlsHint = new ControlsHintOverlay(uiScreen);

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
    /// Обрабатывает клики UI; ЛКМ вне UI включает поворот камеры (FPS).
    /// </summary>
    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button != MouseButton.Left)
            return;

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

        _cameraLookActive = true;
        _lastMousePos = new Vector2(MouseState.X, MouseState.Y);
    }

    /// <summary>
    /// Отключает поворот камеры и возвращает курсор при отпускании ЛКМ.
    /// </summary>
    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);

        if (e.Button != MouseButton.Left)
            return;

        _cameraLookActive = false;
    }

    /// <summary>
    /// Поворачивает камеру при зажатой ЛКМ; обновляет hover-состояние UI.
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

        if (!_cameraLookActive || !MouseState.IsButtonDown(MouseButton.Left))
            return;

        var current = new Vector2(MouseState.X, MouseState.Y);
        var delta = current - _lastMousePos;
        _lastMousePos = current;

        if (delta != Vector2.Zero)
            cam.AddRotation(delta.X, delta.Y);
    }

    /// <summary>
    /// Приближает или отдаляет камеру вдоль направления взгляда.
    /// </summary>
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        float distance = WheelStepDistance * (cam.MoveSpeed / DefaultMoveSpeed);
        cam.MoveForwardStep(MathF.Sign(e.OffsetY), distance);
    }

    /// <summary>Сворачивает меню и его панели при нажатии Esc.</summary>
    protected override void OnKeyDown(KeyboardKeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Keys.Escape)
            sidebarMenu?.Collapse();
    }

    /// <summary>
    /// Обрабатывает WASD, обновляет FPS в заголовке окна.
    /// </summary>
    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);

        ProcessKeyboardMovement((float)args.Time);

        _frameCount++;
        _elapsedTime += args.Time;

        if (_elapsedTime >= 1.0)
        {
            _fps = _frameCount / _elapsedTime;
            _frameCount = 0;
            _elapsedTime = 0.0;
            Title = Localization.FpsTitle(_fps);
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
        controlsHint.Render();

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
        controlsHint?.UpdateScreenSize(uiScreen);

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

    private void ProcessKeyboardMovement(float deltaTime)
    {
        deltaTime = Math.Min(deltaTime, MaxMovementDelta);

        cam.MoveSpeed = settingsPanel.MoveSpeed;

        var kb = KeyboardState;

        float forward = 0f;
        float right = 0f;

        if (kb.IsKeyDown(Keys.W)) forward += 1f;
        if (kb.IsKeyDown(Keys.S)) forward -= 1f;
        if (kb.IsKeyDown(Keys.D)) right += 1f;
        if (kb.IsKeyDown(Keys.A)) right -= 1f;

        if (forward != 0f || right != 0f)
            controlsHint.Hide();

        cam.MoveRelative(forward, right, deltaTime);
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
