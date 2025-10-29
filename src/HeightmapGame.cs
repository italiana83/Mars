using System;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Mars;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System.Drawing;

namespace GravitationalWaveVisualizer
{
    public class HeightmapGame : GameWindow
    {
        private MapData mapData;

        //TextRenderer textRenderer;
        BoundingBoxRenderer boundingBoxRenderer;
        AxisRender axisRender;
        MeshRender meshRender;
        Matrix4 projection;

        Camera cam = new Camera(10.2f, 0.02f);
        private bool mouseDown = false;
        Vector2 lastMousePos = new Vector2();
        private Frustum frustum = new Frustum();

        private int _frameCount = 0;            // Количество кадров за текущую секунду
        private double _elapsedTime = 0.0;     // Прошедшее время с начала отсчёта
        private double _fps = 0.0;             // Текущее значение FPS

        Minimap minimap;

        public HeightmapGame()
            : base(new GameWindowSettings() { UpdateFrequency = 60.0 },
                  new NativeWindowSettings() { IsEventDriven = true })
        {
            MolaDataReader reader = new MolaDataReader();
            var parameters = reader.ReadLblFile(@"d:\Dev\_Graphics\Mars\data\mola\meg128\megt44n180hb.lbl");

            mapData = reader.ReadImgFile(@"d:\Dev\_Graphics\Mars\data\mola\meg128\megt44n180hb.img", parameters, 8);
        }


        protected override void OnLoad()
        {
            base.OnLoad();

            minimap = new Minimap("Mars_topography_(MOLA_dataset)_HiRes_2.jpg", Size.X, Size.Y);
            minimap.Toggle();

            //Position: (-142, 66658; 103,019356; 204,69543)       Orientation: (1, 5987905; -0,2156; 0)
            //cam.Position = new Vector3(25, 0, 200);
            //cam.Orientation = new Vector3((float)Math.PI, 0.694f, -0.14f);
            //textRenderer = new TextRenderer(@"C:\Windows\Fonts\arial.ttf", Size.X, Size.Y, 48);

            GL.ClearColor(0.1f, 0.2f, 0.3f, 1.0f);
            GL.Enable(EnableCap.DepthTest);

            // Включаем режим каркаса
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
            //GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            GL.ClearDepth(1.0f);	  									// Depth Buffer Setup
            GL.DepthFunc(DepthFunction.Lequal);
            GL.Enable(EnableCap.DepthTest);                                 // Enable Depth Testing

            //GL.Hint(HintTarget.PerspectiveCorrectionHint, HintMode.Nicest);         // Set Perspective Calculations To Most Accurate

            GL.Hint(HintTarget.PolygonSmoothHint, HintMode.Nicest);         // Set Perspective Calculations To Most Accurate


            //GL.CullFace(CullFaceMode.Back);
            //GL.PolygonMode(MaterialFace.Front, PolygonMode.Fill);
            //GL.FrontFace(FrontFaceDirection.Ccw);
            //var errorCode = GL.GetError();

            //GL.Enable(EnableCap.Normalize);
            //GL.Enable(EnableCap.ColorMaterial);

            meshRender = new MeshRender(mapData);
            //cam.Position = new Vector3(meshRender.ModelCenter.X, meshRender.ModelCenter.Y + 100.0f, meshRender.ModelCenter.Z); // Поднимаем камеру над центром
            //cam.Orientation = new Vector3(0.0f, -1.0f, 0.0f);
            cam.Position = new Vector3(749, 198, 203); // Поднимаем камеру над центром
            cam.Orientation = new Vector3(3.09f, -1.190f, 0.0f);
            boundingBoxRenderer = new BoundingBoxRenderer();
            boundingBoxRenderer.CreateBoundingBox(meshRender.Min, meshRender.Max);

            Console.WriteLine($"MinX: {meshRender.MinX}, MaxX: {meshRender.MaxX}");
            Console.WriteLine($"MinY: {meshRender.MinY}, MaxY: {meshRender.MaxY}");
            Console.WriteLine($"MinZ: {meshRender.MinZ}, MaxZ: {meshRender.MaxZ}");

            axisRender = new AxisRender(100.0f, 2.5f, 10.0f, meshRender.ModelCenter);

            projection = Matrix4.CreatePerspectiveFieldOfView(
                MathHelper.DegreesToRadians(45.0f),
                Size.X / (float)Size.Y,
                0.1f,
                90000.0f);
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);

            mouseDown = true;
            lastMousePos = new Vector2(MouseState.X, MouseState.Y);

            if (e.Button == MouseButton.Left)
            {
                minimap.HandleMouseDown((int)MouseState.X, (int)MouseState.Y);
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            mouseDown = false;
        }

        protected override void OnMouseMove(MouseMoveEventArgs e)
        {
            base.OnMouseMove(e);
            if (mouseDown)
            {
                if (MouseState.IsButtonDown(MouseButton.Left))
                {
                    //// Вычисляем смещение мыши
                    //Vector2 delta = lastMousePos - new Vector2(e.X, e.Y);

                    //// Поворот камеры
                    //cam.AddRotation(-delta.X, -delta.Y); // Инверсируем X и Y для естественного поведения

                    //// Обновляем последнюю позицию мыши
                    //lastMousePos = new Vector2(e.X, e.Y);


                    Vector2 delta = lastMousePos - new Vector2(e.X, e.Y);
                    cam.AddRotation(delta.X / 10, delta.Y / 10);
                    lastMousePos = new Vector2(e.X, e.Y);
                }

                if (MouseState.IsButtonDown(MouseButton.Right))
                {
                    //// Перемещение по оси Y
                    //float deltaY = lastMousePos.Y - e.Y;
                    //cam.Move(0f, deltaY * 0.1f, 0f);

                    //lastMousePos = new Vector2(e.X, e.Y);
                    if (e.Y > lastMousePos.Y)
                        cam.Move(0f, 0f, 0.1f);
                    else
                        cam.Move(0f, 0f, -0.1f);

                    lastMousePos = new Vector2(e.X, e.Y);
                }

            }
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);

            // Перемещение камеры вдоль оси Z при скроллинге мыши
            if (e.OffsetY > 0)
            {
                cam.Move(0f, 10000.0f, 0f); // Прокрутка вверх — движение вперёд
            }
            else if (e.OffsetY < 0)
            {
                cam.Move(0f, -10000.0f, 0f); // Прокрутка вниз — движение назад
            }
        }

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

                // Устанавливаем заголовок окна
                Title = $"RandomHeightmapGame3 - FPS: {_fps:F2}";
            }
        }

        protected override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            // Обновляем фруструм
            Matrix4 view = cam.GetViewMatrix();
            // Обновление фрустума
            frustum.Update(view * projection);

            axisRender.DrawAxis(view, projection);

            meshRender.DrawMesh(view, projection, Matrix4.Identity, cam.Position, frustum);

            boundingBoxRenderer.DrawBoundingBox(view, projection);

            minimap.Render((float)args.Time);

            SwapBuffers();
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);

            GL.Viewport(0, 0, Size.X, Size.Y);
        }
    }
}