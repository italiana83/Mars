using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Mars
{
    public class Minimap
    {
        private int textureId;
        private int vao, vbo;
        private int shaderProgram;
        private Matrix4 ortho;

        private float animationProgress = 0f; // 0..1 — для анимации
        public bool IsOpen { get; set; } = false;
        public bool IsAnimating { get; set; } = false;

        private int screenWidth, screenHeight;
        private int imgWidth, imgHeight;

        private const float minScale = 0.1f; // 10% окна
        private const float maxScale = 0.25f; // 25% окна
        private const int margin = 12;

        uint starTextureHandle;
        OpenTK.Graphics.OpenGL.TextureTarget starTextureTarget;

        public Minimap(string imagePath, int screenW, int screenH)
        {
            screenWidth = screenW;
            screenHeight = screenH;

            var textureParams = new TextureLoaderParameters();
            ImageGDI.LoadFromDisk(imagePath,
                textureParams,
                out starTextureHandle,
                out starTextureTarget,
                out imgWidth,
                out imgHeight);

            textureId = (int)starTextureHandle;

            InitGL();
            ortho = Matrix4.CreateOrthographicOffCenter(0, screenWidth, screenHeight, 0, -1, 1);
        }

        private void InitGL()
        {
            float[] quadVertices = {
            // pos.xy | uv
            0f, 0f, 0f, 1f,
            1f, 0f, 1f, 1f,
            1f, 1f, 1f, 0f,
            0f, 1f, 0f, 0f
        };

            vao = GL.GenVertexArray();
            vbo = GL.GenBuffer();
            GL.BindVertexArray(vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, quadVertices.Length * sizeof(float), quadVertices, BufferUsageHint.StaticDraw);

            int stride = 4 * sizeof(float);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, 0);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 2 * sizeof(float));
            GL.EnableVertexAttribArray(1);

            shaderProgram = CreateShader();
        }

        private int CreateShader()
        {
            string vtx = @"
        #version 330 core
        layout(location = 0) in vec2 aPos;
        layout(location = 1) in vec2 aUV;
        uniform mat4 uOrtho;
        uniform vec2 uPos;
        uniform vec2 uSize;
        out vec2 vUV;
        void main() {
            vec2 scaled = aPos * uSize + uPos;
            gl_Position = uOrtho * vec4(scaled, 0.0, 1.0);
            vUV = aUV;
        }";

            string frag = @"
        #version 330 core
        in vec2 vUV;
        uniform sampler2D uTex;
        out vec4 FragColor;
        void main() {
            FragColor = texture(uTex, vUV);
        }";

            int vs = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vs, vtx);
            GL.CompileShader(vs);
            int fs = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fs, frag);
            GL.CompileShader(fs);

            int prog = GL.CreateProgram();
            GL.AttachShader(prog, vs);
            GL.AttachShader(prog, fs);
            GL.LinkProgram(prog);
            GL.DeleteShader(vs);
            GL.DeleteShader(fs);

            return prog;
        }

        public void Render(float deltaTime)
        {
            // 🔹 Анимация открытия/закрытия
            if (IsAnimating)
            {
                float speed = 4f * deltaTime;
                animationProgress += IsOpen ? speed : -speed;
                animationProgress = Math.Clamp(animationProgress, 0f, 1f);

                if ((IsOpen && animationProgress >= 1f) || (!IsOpen && animationProgress <= 0f))
                {
                    IsAnimating = false;
                }
            }

            // 🔹 Если карта закрыта — не рендерим
            if (animationProgress <= 0.01f)
                return;

            // 🔹 Интерполируем размер между min и max масштабом
            float scale = MathHelper.Lerp(minScale, maxScale, animationProgress);

            // Размеры миникарты с сохранением пропорций
            float aspect = imgHeight / (float)imgWidth;
            float width = screenWidth * scale;
            float height = width * aspect;

            Vector2 pos = new Vector2(margin, margin);
            Vector2 size = new Vector2(width, height);

            // 🔹 Подготовка к 2D-рендерингу
            GL.Disable(EnableCap.DepthTest);
            GL.Enable(EnableCap.Texture2D);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill); // 🔹 ключевая строка

            // 🔹 Передаём параметры в шейдер
            GL.UseProgram(shaderProgram);
            GL.UniformMatrix4(GL.GetUniformLocation(shaderProgram, "uOrtho"), false, ref ortho);
            GL.Uniform2(GL.GetUniformLocation(shaderProgram, "uPos"), ref pos);
            GL.Uniform2(GL.GetUniformLocation(shaderProgram, "uSize"), ref size);
            GL.Uniform1(GL.GetUniformLocation(shaderProgram, "uScale"), scale);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, textureId);
            GL.Uniform1(GL.GetUniformLocation(shaderProgram, "uTex"), 0);

            GL.BindVertexArray(vao);
            GL.DrawArrays(PrimitiveType.TriangleFan, 0, 4); // 🔹 теперь будет заливка, не линии

            // 🔹 Восстанавливаем состояние
            GL.BindVertexArray(0);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
            GL.Enable(EnableCap.DepthTest);
        }

        public void Toggle()
        {
            IsOpen = !IsOpen;
            IsAnimating = true;
        }

        public event Action<double, double> OnMinimapClicked;

        public void HandleMouseDown(int mouseX, int mouseY)
        {
            if (IsInside(mouseX, mouseY))
            {
                // Клик по миникарте - переключаем состояние
                Toggle();

                // Если нужно передать координаты в мировых координатах
                var (lon, lat) = ScreenToLonLat(mouseX, mouseY);
                OnMinimapClicked?.Invoke(lon, lat);
            }
            else
            {
                // Клик вне миникарты - сворачиваем
                if (IsOpen)
                {
                    IsOpen = false;
                    IsAnimating = true;
                }
            }
        }

        public bool IsInside(int mouseX, int mouseY)
        {
            // Используем текущий отображаемый размер для проверки клика
            float scale = MathHelper.Lerp(minScale, maxScale, animationProgress);
            float aspect = imgHeight / (float)imgWidth;
            float width = screenWidth * scale;
            float height = width * aspect;

            return mouseX >= margin && mouseX <= margin + width &&
                   mouseY >= margin && mouseY <= margin + height;
        }

        public (double lon360, double lat) ScreenToLonLat(int mouseX, int mouseY)
        {
            // Используем текущий отображаемый размер
            float scale = MathHelper.Lerp(minScale, maxScale, animationProgress);
            float width = screenWidth * scale;
            float height = width * (imgHeight / (float)imgWidth);

            // Координаты мыши относительно миникарты
            double u = (mouseX - margin) / width;
            double v = (mouseY - margin) / height;

            // Ограничиваем в диапазоне [0,1]
            u = Math.Clamp(u, 0.0, 1.0);
            v = Math.Clamp(v, 0.0, 1.0);

            // Конвертация в долготу и широту
            double lon = u * 360.0 - 180.0;
            if (lon < 0) lon += 360.0; // переводим в 0..360°
            double lat = 90.0 - v * 180.0;

            return (lon, lat);
        }

        public string GetMegdrFilename(double lon360, double lat)
        {
            int lonLeft = (int)(Math.Floor(lon360 / 90.0) * 90.0) % 360;
            if (lonLeft < 0) lonLeft += 360;
            string lonTag = lonLeft.ToString("D3");

            string latTag;
            if (lat >= 44.0) latTag = "88n";
            else if (lat >= 0.0) latTag = "44n";
            else if (lat >= -44.0) latTag = "44s";
            else latTag = "88s";

            return $"megr{latTag}{lonTag}hb.img";
        }

        public async Task DownloadMegdrAsync(string filename)
        {
            // Реализация загрузки...
        }
    }
}
