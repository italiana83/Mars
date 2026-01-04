using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace Mars
{
    public sealed class Minimap : IDisposable
    {
        // ---------- config ----------
        private const float CollapsedPx = 40f;
        private const float Margin = 12f;

        private const float DPI = 96f;
        private const float MmToPx = DPI / 25.4f;

        // image margins (mm)
        private const float ImgMarginL = 2f;
        private const float ImgMarginT = 10f;
        private const float ImgMarginR = 2f;
        private const float ImgMarginB = 2f;

        // ---------- state ----------
        public bool IsOpen { get; private set; }
        private bool isAnimating;
        private float anim; // 0..1

        // ---------- screen ----------
        private int screenW, screenH;
        private int imgW, imgH;

        // ---------- GL ----------
        private int vao, vbo;
        private int vaoColor, vboColor;
        private int textureId;
        private int uboOrtho;

        private Shader textureShader;
        private Shader colorShader;

        private Matrix4 ortho;

        private readonly float[] QuadVertices =
        {
        0f, 1f,  0f, 1f,
        1f, 1f,  1f, 1f,
        1f, 0f,  1f, 0f,

        0f, 1f,  0f, 1f,
        1f, 0f,  1f, 0f,
        0f, 0f,  0f, 0f
    };

        // ---------- ctor ----------
        public Minimap(string imagePath, int w, int h)
        {
            screenW = w;
            screenH = h;

            LoadTexture(imagePath);
            InitBuffers();
            InitColorBuffers();
            InitShaders();
            InitUBO();
            UpdateOrtho();
        }

        // ---------- init ----------
        private void LoadTexture(string path)
        {
            ImageGDI.LoadFromDisk(
                path,
                new TextureLoaderParameters(),
                out uint handle,
                out _,
                out imgW,
                out imgH);

            textureId = (int)handle;
        }

        private void InitBuffers()
        {
            vao = GL.GenVertexArray();
            vbo = GL.GenBuffer();

            GL.BindVertexArray(vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            GL.BufferData(BufferTarget.ArrayBuffer,
                QuadVertices.Length * sizeof(float),
                QuadVertices,
                BufferUsageHint.StaticDraw);

            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));
            GL.EnableVertexAttribArray(1);

            GL.BindVertexArray(0);
        }

        private void InitColorBuffers()
        {
            vaoColor = GL.GenVertexArray();
            vboColor = GL.GenBuffer();

            float[] verts =
            {
            0f, 1f,
            1f, 1f,
            1f, 0f,
            0f, 1f,
            1f, 0f,
            0f, 0f
        };

            GL.BindVertexArray(vaoColor);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vboColor);
            GL.BufferData(BufferTarget.ArrayBuffer, verts.Length * sizeof(float), verts, BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);
            GL.BindVertexArray(0);
        }

        private void InitShaders()
        {
            textureShader = new Shader(VertexTex, FragmentTex, ShaderSourceMode.Code);
            colorShader = new Shader(VertexColor, FragmentColor, ShaderSourceMode.Code);

            BindUBO(textureShader);
            BindUBO(colorShader);
        }

        private void InitUBO()
        {
            uboOrtho = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.UniformBuffer, uboOrtho);
            GL.BufferData(BufferTarget.UniformBuffer, 64, IntPtr.Zero, BufferUsageHint.DynamicDraw);
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, uboOrtho);
        }

        private void BindUBO(Shader shader)
        {
            int program = (int)typeof(Shader)
                .GetField("_handle", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(shader)!;

            int index = GL.GetUniformBlockIndex(program, "Ortho");
            if (index >= 0)
                GL.UniformBlockBinding(program, index, 0);
        }

        // ---------- ortho ----------
        public void UpdateScreenSize(int w, int h)
        {
            screenW = w;
            screenH = h;
            UpdateOrtho();
        }

        private void UpdateOrtho()
        {
            ortho = Matrix4.CreateOrthographicOffCenter(0, screenW, screenH, 0, -1, 1);
            GL.BindBuffer(BufferTarget.UniformBuffer, uboOrtho);
            GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, 64, ref ortho);
        }

        // ---------- render ----------
        public void Render(float dt)
        {
            Animate(dt);


            // 🔹 Сохраняем текущий PolygonMode
            int[] polygonMode = new int[2];
            GL.GetInteger(GetPName.PolygonMode, polygonMode);

            // 🔹 UI всегда рисуем ЗАПОЛНЕННЫМ
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);

            GL.Disable(EnableCap.DepthTest);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(
                BlendingFactor.SrcAlpha,
                BlendingFactor.OneMinusSrcAlpha
            );

            //GL.Disable(EnableCap.DepthTest);
            //GL.Enable(EnableCap.Blend);
            //GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            //GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);

            if (!IsOpen)
                RenderCollapsed();
            else
                RenderExpanded();

            //GL.Disable(EnableCap.Blend);
            //GL.Enable(EnableCap.DepthTest);

            GL.Disable(EnableCap.Blend);
            GL.Enable(EnableCap.DepthTest);

            // 🔹 ВОЗВРАЩАЕМ режим мира (Line)
            GL.PolygonMode(
                MaterialFace.FrontAndBack,
                (PolygonMode)polygonMode[0]
            );
        }

        private void Animate(float dt)
        {
            if (!isAnimating) return;
            anim += (IsOpen ? 1 : -1) * dt * 4f;
            anim = Math.Clamp(anim, 0, 1);
            if (anim == 0 || anim == 1) isAnimating = false;
        }

        private void RenderCollapsed()
        {
            DrawRect(
                new Vector2(Margin, Margin + CollapsedPx),
                new Vector2(CollapsedPx, CollapsedPx),
                new Vector4(0.2f, 0.4f, 0.8f, 0.7f)
            );
        }

        private void RenderExpanded()
        {
            Vector2 size = new Vector2(screenW * 0.5f, screenH * 0.5f);
            Vector2 pos = new Vector2(Margin, Margin + 40);

            // фон (RGB 4,31,77 + 70% alpha)
            DrawRect(
                pos,
                size,
                new Vector4(4 / 255f, 31 / 255f, 77 / 255f, 0.7f)
            );

            float l = ImgMarginL * MmToPx;
            float t = ImgMarginT * MmToPx;
            float r = ImgMarginR * MmToPx;
            float b = ImgMarginB * MmToPx;

            Vector2 imgPos = pos + new Vector2(l, t);
            Vector2 imgSize = size - new Vector2(l + r, t + b);

            textureShader.Use();
            textureShader.SetVector2("uPos", imgPos);
            textureShader.SetVector2("uSize", imgSize);
            textureShader.SetInt("uTex", 0);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, textureId);
            GL.BindVertexArray(vao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
        }

        // ---------- interaction ----------
        public void Toggle()
        {
            IsOpen = !IsOpen;
            isAnimating = true;
        }

        public bool HandleMouseDown(float mouseX, float mouseY)
        {
            if (!IsOpen)
            {
                var rect = new RectangleF(Margin, Margin, CollapsedPx, CollapsedPx);
                if (rect.Contains(mouseX, mouseY))
                {
                    Toggle();
                    return true;
                }
                return false;
            }

            var panel = new RectangleF(Margin, Margin, screenW * 0.5f, screenH * 0.5f);
            if (panel.Contains(mouseX, mouseY))
                return true;

            Toggle();
            return false;
        }

        // ---------- draw helpers ----------
        private void DrawRect(Vector2 pos, Vector2 size, Vector4 color)
        {
            colorShader.Use();
            colorShader.SetVector2("uPos", pos);
            colorShader.SetVector2("uSize", size);
            colorShader.SetVector4("uColor", color);
            GL.BindVertexArray(vaoColor);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
        }

        // ---------- shaders ----------
        private const string VertexTex = @"
#version 330 core
layout(location=0) in vec2 aPos;
layout(location=1) in vec2 aUV;
layout(std140) uniform Ortho { mat4 uOrtho; };
uniform vec2 uPos;
uniform vec2 uSize;
out vec2 vUV;
void main() {
    vec2 p = aPos * uSize + uPos;
    gl_Position = uOrtho * vec4(p, 0, 1);
    vUV = aUV;
}";

        private const string FragmentTex = @"
#version 330 core
in vec2 vUV;
uniform sampler2D uTex;
out vec4 FragColor;
void main() { FragColor = texture(uTex, vUV); }";

        private const string VertexColor = @"
#version 330 core
layout(location=0) in vec2 aPos;
layout(std140) uniform Ortho { mat4 uOrtho; };
uniform vec2 uPos;
uniform vec2 uSize;
void main() {
    vec2 p = aPos * uSize + uPos;
    gl_Position = uOrtho * vec4(p, 0, 1);
}";

        private const string FragmentColor = @"
#version 330 core
uniform vec4 uColor;
out vec4 FragColor;
void main() { FragColor = uColor; }";

        // ---------- dispose ----------
        public void Dispose()
        {
            GL.DeleteBuffer(vbo);
            GL.DeleteVertexArray(vao);
            GL.DeleteBuffer(vboColor);
            GL.DeleteVertexArray(vaoColor);
            GL.DeleteBuffer(uboOrtho);
            GL.DeleteTexture(textureId);
        }
    }


    //    public sealed class Minimap : IDisposable
    //    {
    //        // ---------- config ----------
    //        private const float CollapsedPx = 40f;
    //        private const float CloseBtnPx = 20f;
    //        private const float Margin = 12f;
    //        private const float ExpandedScale = 0.25f;
    //        private const float CloseBtnOffset = 8f;

    //        // ---------- state ----------
    //        public bool IsOpen { get; private set; }
    //        private bool isAnimating;
    //        private float anim; // 0..1

    //        // ---------- screen ----------
    //        private int screenW, screenH;
    //        private int imgW, imgH;

    //        // ---------- GL ----------
    //        private int vao, vbo;
    //        private int vaoColor, vboColor;
    //        private int textureId;
    //        private int uboOrtho;

    //        private Shader textureShader;
    //        private Shader colorShader;

    //        private Matrix4 ortho;

    //        // Вершины с правильными UV (Y=0 вверху)
    //        private readonly float[] QuadVertices =
    //        {
    //        // pos      // uv
    //        0f, 1f,    0f, 1f,  // левый верхний
    //        1f, 1f,    1f, 1f,  // правый верхний
    //        1f, 0f,    1f, 0f,  // правый нижний

    //        0f, 1f,    0f, 1f,  // левый верхний
    //        1f, 0f,    1f, 0f,  // правый нижний
    //        0f, 0f,    0f, 0f   // левый нижний
    //    };

    //        // ---------- ctor ----------
    //        public Minimap(string imagePath, int w, int h)
    //        {
    //            screenW = w;
    //            screenH = h;

    //            LoadTexture(imagePath);
    //            InitBuffers();
    //            InitColorBuffers();
    //            InitShaders();
    //            InitUBO();
    //            UpdateOrtho();
    //        }

    //        // ---------- init ----------
    //        private void LoadTexture(string path)
    //        {
    //            ImageGDI.LoadFromDisk(
    //                path,
    //                new TextureLoaderParameters(),
    //                out uint handle,
    //                out _,
    //                out imgW,
    //                out imgH);

    //            textureId = (int)handle;
    //        }

    //        private void InitBuffers()
    //        {
    //            vao = GL.GenVertexArray();
    //            vbo = GL.GenBuffer();

    //            GL.BindVertexArray(vao);
    //            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
    //            GL.BufferData(BufferTarget.ArrayBuffer,
    //                QuadVertices.Length * sizeof(float),
    //                QuadVertices,
    //                BufferUsageHint.StaticDraw);

    //            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
    //            GL.EnableVertexAttribArray(0);

    //            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));
    //            GL.EnableVertexAttribArray(1);

    //            GL.BindVertexArray(0);
    //        }

    //        private void InitColorBuffers()
    //        {
    //            vaoColor = GL.GenVertexArray();
    //            vboColor = GL.GenBuffer();

    //            // Вершины для цветных прямоугольников (Y=0 вверху)
    //            float[] colorVertices =
    //            {
    //            0f, 1f,  // левый верхний
    //            1f, 1f,  // правый верхний
    //            1f, 0f,  // правый нижний

    //            0f, 1f,  // левый верхний
    //            1f, 0f,  // правый нижний
    //            0f, 0f   // левый нижний
    //        };

    //            GL.BindVertexArray(vaoColor);
    //            GL.BindBuffer(BufferTarget.ArrayBuffer, vboColor);
    //            GL.BufferData(BufferTarget.ArrayBuffer,
    //                colorVertices.Length * sizeof(float),
    //                colorVertices,
    //                BufferUsageHint.StaticDraw);

    //            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
    //            GL.EnableVertexAttribArray(0);

    //            GL.BindVertexArray(0);
    //        }

    //        private void InitShaders()
    //        {
    //            textureShader = new Shader(VertexTex, FragmentTex, ShaderSourceMode.Code);
    //            colorShader = new Shader(VertexColor, FragmentColor, ShaderSourceMode.Code);

    //            BindUBO(textureShader);
    //            BindUBO(colorShader);
    //        }

    //        private void InitUBO()
    //        {
    //            uboOrtho = GL.GenBuffer();
    //            GL.BindBuffer(BufferTarget.UniformBuffer, uboOrtho);
    //            GL.BufferData(BufferTarget.UniformBuffer, 64, IntPtr.Zero, BufferUsageHint.DynamicDraw);
    //            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, uboOrtho);
    //        }

    //        private void BindUBO(Shader shader)
    //        {
    //            int program = typeof(Shader)
    //                .GetField("_handle", BindingFlags.NonPublic | BindingFlags.Instance)!
    //                .GetValue(shader) as int? ?? 0;

    //            int index = GL.GetUniformBlockIndex(program, "Ortho");
    //            if (index >= 0)
    //                GL.UniformBlockBinding(program, index, 0);
    //        }

    //        // ---------- ortho ----------
    //        public void UpdateScreenSize(int w, int h)
    //        {
    //            screenW = w;
    //            screenH = h;
    //            UpdateOrtho();
    //        }

    //        private void UpdateOrtho()
    //        {
    //            // Ортогональная проекция с Y=0 вверху
    //            ortho = Matrix4.CreateOrthographicOffCenter(0, screenW, screenH, 0, -1, 1);

    //            GL.BindBuffer(BufferTarget.UniformBuffer, uboOrtho);
    //            GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, 64, ref ortho);
    //        }

    //        // ---------- render ----------
    //        public void Render(float dt)
    //        {
    //            Animate(dt);

    //            GL.Disable(EnableCap.DepthTest);
    //            GL.Enable(EnableCap.Blend);
    //            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
    //            // Устанавливаем режим заполнения
    //            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);

    //            if (anim < 0.01f)
    //                RenderCollapsed();
    //            else
    //                RenderExpanded();

    //            GL.Disable(EnableCap.Blend);
    //            GL.Enable(EnableCap.DepthTest);
    //        }

    //        private void Animate(float dt)
    //        {
    //            if (!isAnimating) return;

    //            anim += (IsOpen ? 1 : -1) * dt * 4f;
    //            anim = Math.Clamp(anim, 0, 1);

    //            if (anim == 0 || anim == 1)
    //                isAnimating = false;
    //        }

    //        private void RenderCollapsed()
    //        {
    //            // Синий квадрат в левом верхнем углу
    //            DrawRect(new Vector2(Margin, Margin + CollapsedPx),
    //                    new Vector2(CollapsedPx, CollapsedPx),
    //                    new Vector4(0.2f, 0.4f, 0.8f, 1f));
    //        }

    //        private void RenderExpanded()
    //        {
    //            float aspect = imgH / (float)imgW;
    //            float w = MathHelper.Lerp(CollapsedPx, screenW * ExpandedScale, anim);
    //            float h = w * aspect;

    //            Vector2 pos = new Vector2(Margin, Margin);
    //            Vector2 size = new Vector2(w, h);

    //            // Рисуем фон миникарты
    //            DrawRect(pos, size, new Vector4(0.1f, 0.1f, 0.1f, 0.8f));

    //            // Рисуем текстуру миникарты
    //            textureShader.Use();
    //            textureShader.SetVector2("uPos", pos);
    //            textureShader.SetVector2("uSize", size);
    //            GL.ActiveTexture(TextureUnit.Texture0);
    //            GL.BindTexture(TextureTarget.Texture2D, textureId);
    //            textureShader.SetInt("uTex", 0);

    //            GL.BindVertexArray(vao);
    //            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);

    //            // Рисуем кнопку закрытия
    //            if (anim > 0.99f)
    //                DrawCloseButton(pos, size);
    //        }

    //        private void DrawCloseButton(Vector2 mapPos, Vector2 mapSize)
    //        {
    //            // Позиция кнопки в правом верхнем углу миникарты
    //            Vector2 btnPos = new Vector2(
    //                mapPos.X + mapSize.X - CloseBtnPx - CloseBtnOffset,
    //                mapPos.Y + CloseBtnPx + CloseBtnPx);

    //            // Красная кнопка закрытия
    //            DrawRect(
    //                btnPos,
    //                new Vector2(CloseBtnPx, CloseBtnPx),
    //                new Vector4(0.9f, 0.2f, 0.2f, 0.95f));

    //            // Белый крестик на кнопке
    //            DrawCrossOnButton(btnPos, new Vector2(CloseBtnPx, CloseBtnPx));
    //        }

    //        private void DrawCrossOnButton(Vector2 btnPos, Vector2 btnSize)
    //        {
    //            float centerX = btnPos.X + btnSize.X / 2;
    //            float centerY = btnPos.Y + btnSize.Y / 2;
    //            float armLength = btnSize.X * 0.3f;
    //            float thickness = 2f;

    //            // Первая линия крестика (\)
    //            Vector2 line1Start = new Vector2(centerX - armLength, centerY - armLength);
    //            Vector2 line1End = new Vector2(centerX + armLength, centerY + armLength);
    //            DrawLine(line1Start, line1End, thickness, new Vector4(1f, 1f, 1f, 1f));

    //            // Вторая линия крестика (/)
    //            Vector2 line2Start = new Vector2(centerX - armLength, centerY + armLength);
    //            Vector2 line2End = new Vector2(centerX + armLength, centerY - armLength);
    //            DrawLine(line2Start, line2End, thickness, new Vector4(1f, 1f, 1f, 1f));
    //        }

    //        private void DrawLine(Vector2 start, Vector2 end, float thickness, Vector4 color)
    //        {
    //            Vector2 dir = Vector2.Normalize(end - start);
    //            Vector2 perp = new Vector2(-dir.Y, dir.X) * thickness / 2;

    //            float[] lineVertices = {
    //            start.X - perp.X, start.Y - perp.Y,
    //            start.X + perp.X, start.Y + perp.Y,
    //            end.X + perp.X, end.Y + perp.Y,

    //            start.X - perp.X, start.Y - perp.Y,
    //            end.X + perp.X, end.Y + perp.Y,
    //            end.X - perp.X, end.Y - perp.Y
    //        };

    //            // Временный буфер для линии
    //            int lineVao = GL.GenVertexArray();
    //            int lineVbo = GL.GenBuffer();

    //            GL.BindVertexArray(lineVao);
    //            GL.BindBuffer(BufferTarget.ArrayBuffer, lineVbo);
    //            GL.BufferData(BufferTarget.ArrayBuffer, lineVertices.Length * sizeof(float), lineVertices, BufferUsageHint.StreamDraw);
    //            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
    //            GL.EnableVertexAttribArray(0);

    //            colorShader.Use();
    //            colorShader.SetVector2("uPos", Vector2.Zero);
    //            colorShader.SetVector2("uSize", Vector2.One);
    //            colorShader.SetVector4("uColor", color);

    //            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);

    //            GL.DeleteBuffer(lineVbo);
    //            GL.DeleteVertexArray(lineVao);
    //        }

    //        private void DrawRect(Vector2 pos, Vector2 size, Vector4 color)
    //        {
    //            colorShader.Use();
    //            colorShader.SetVector2("uPos", pos);
    //            colorShader.SetVector2("uSize", size);
    //            colorShader.SetVector4("uColor", color);

    //            GL.BindVertexArray(vaoColor);
    //            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
    //        }

    //        // ---------- interaction ----------
    //        public void Toggle()
    //        {
    //            IsOpen = !IsOpen;
    //            isAnimating = true;
    //        }

    //        public bool HandleMouseDown(float mouseX, float mouseY)
    //        {
    //            // координаты экрана: (0,0) — левый верх
    //            if (anim < 0.01f)
    //            {
    //                var rect = new RectangleF(Margin, Margin, CollapsedPx, CollapsedPx);
    //                if (rect.Contains(mouseX, mouseY))
    //                {
    //                    Toggle();
    //                    return true;
    //                }
    //                return false;
    //            }

    //            // ---------- expanded ----------
    //            float aspect = imgH / (float)imgW;
    //            float w = MathHelper.Lerp(CollapsedPx, screenW * ExpandedScale, anim);
    //            float h = w * aspect;

    //            Vector2 pos = new Vector2(Margin, Margin);
    //            Vector2 size = new Vector2(w, h);

    //            // Проверка клика на кнопку закрытия
    //            if (anim > 0.99f)
    //            {
    //                var closeRect = new RectangleF(
    //                    pos.X + size.X - CloseBtnPx - CloseBtnOffset,
    //                    pos.Y + CloseBtnOffset,
    //                    CloseBtnPx,
    //                    CloseBtnPx);

    //                if (closeRect.Contains(mouseX, mouseY))
    //                {
    //                    Toggle();
    //                    return true;
    //                }
    //            }

    //            // Если клик внутри миникарты, возвращаем true
    //            var mapRect = new RectangleF(pos.X, pos.Y, size.X, size.Y);
    //            if (mapRect.Contains(mouseX, mouseY))
    //                return true;

    //            return false;
    //        }

    //        // ---------- shaders ----------
    //        private const string VertexTex = @"
    //#version 330 core
    //layout(location=0) in vec2 aPos;
    //layout(location=1) in vec2 aUV;

    //layout(std140) uniform Ortho { mat4 uOrtho; };

    //uniform vec2 uPos;
    //uniform vec2 uSize;
    //out vec2 vUV;

    //void main() {
    //    vec2 p = aPos * uSize + uPos;
    //    gl_Position = uOrtho * vec4(p, 0, 1);
    //    vUV = aUV;
    //}";

    //        private const string FragmentTex = @"
    //#version 330 core
    //in vec2 vUV;
    //uniform sampler2D uTex;
    //out vec4 FragColor;
    //void main() {
    //    FragColor = texture(uTex, vUV);
    //}";

    //        private const string VertexColor = @"
    //#version 330 core
    //layout(location=0) in vec2 aPos;

    //layout(std140) uniform Ortho { mat4 uOrtho; };

    //uniform vec2 uPos;
    //uniform vec2 uSize;

    //void main() {
    //    vec2 p = aPos * uSize + uPos;
    //    gl_Position = uOrtho * vec4(p, 0, 1);
    //}";

    //        private const string FragmentColor = @"
    //#version 330 core
    //uniform vec4 uColor;
    //out vec4 FragColor;
    //void main() {
    //    FragColor = uColor;
    //}";

    //        // ---------- dispose ----------
    //        public void Dispose()
    //        {
    //            GL.DeleteBuffer(vbo);
    //            GL.DeleteVertexArray(vao);

    //            GL.DeleteBuffer(vboColor);
    //            GL.DeleteVertexArray(vaoColor);

    //            GL.DeleteBuffer(uboOrtho);
    //            GL.DeleteTexture(textureId);
    //        }
    //    }
}
