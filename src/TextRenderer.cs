using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using StbTrueTypeSharp;
using static StbTrueTypeSharp.StbTrueType;


namespace Mars
{
    using OpenTK.Graphics.OpenGL4;
    using OpenTK.Mathematics;
    using StbTrueTypeSharp;
    using System;
    using System.Collections.Generic;
    using System.IO;

    public class TextRenderer
    {
        private readonly int _vao, _vbo;
        private readonly Shader _textShader;
        private readonly Dictionary<char, Character> _characters = new();

        private struct Character
        {
            public int TextureID;    // ID текстуры глифа
            public Vector2 Size;     // Размер глифа
            public Vector2 Bearing;  // Смещение глифа относительно базовой линии
            public float Advance;    // Смещение для следующего глифа
        }

        public TextRenderer(string fontPath, int screenWidth, int screenHeight, float fontSize = 24f)
        {
            // Загружаем шрифт
            var fontInfo = new StbTrueType.stbtt_fontinfo();
            byte[] fontData = File.ReadAllBytes(fontPath);
            unsafe
            {
                fixed (byte* fontPtr = fontData)
                {
                    StbTrueType.stbtt_InitFont(fontInfo, fontPtr, StbTrueType.stbtt_GetFontOffsetForIndex(fontPtr, 0));
                }
            }

            // Генерация текстур для символов
            float scale = StbTrueType.stbtt_ScaleForPixelHeight(fontInfo, fontSize);
            for (char c = ' '; c <= '~'; c++)
            {
                unsafe
                {
                    int width, height, xOffset, yOffset;
                    byte* bitmap = StbTrueType.stbtt_GetCodepointBitmap(fontInfo, 0, scale, c, &width, &height, &xOffset, &yOffset);

                    if (bitmap != null)
                    {
                        int texture = GL.GenTexture();
                        GL.BindTexture(TextureTarget.Texture2D, texture);

                        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.R8, width, height, 0,
                            PixelFormat.Red, PixelType.UnsignedByte, (IntPtr)bitmap);

                        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

                        int advanceWidth, leftSideBearing;
                        StbTrueType.stbtt_GetCodepointHMetrics(fontInfo, c, &advanceWidth, &leftSideBearing);
                        float advance = advanceWidth * scale;

                        // Храним данные символа
                        _characters[c] = new Character
                        {
                            TextureID = texture,
                            Size = new Vector2(width, height),
                            Bearing = new Vector2(xOffset, yOffset),
                            Advance = advance
                        };

                        StbTrueType.stbtt_FreeBitmap(bitmap, null);
                    }
                }
            }

            // Создание VAO и VBO
            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();

            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);

            GL.BufferData(BufferTarget.ArrayBuffer, 6 * 4 * sizeof(float), IntPtr.Zero, BufferUsageHint.DynamicDraw);

            GL.VertexAttribPointer(0, 4, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);




            // Создаем шейдеры
            string vertexShaderSource = @"
            #version 330 core
            layout (location = 0) in vec4 vertex;
            out vec2 TexCoords;

            uniform mat4 projection;

            void main()
            {
                gl_Position = projection * vec4(vertex.xy, 0.0, 1.0);
                TexCoords = vertex.zw;
            }";

            string fragmentShaderSource = @"
            #version 330 core
            in vec2 TexCoords;
            out vec4 FragColor;

            uniform sampler2D text;
            uniform vec3 textColor;

            void main()
            {
                vec4 sampled = vec4(1.0, 1.0, 1.0, texture(text, TexCoords).r);
                FragColor = vec4(textColor, 1.0) * sampled;
            }";

            _textShader = new Shader(vertexShaderSource, fragmentShaderSource, ShaderSourceMode.Code);

            // Устанавливаем проекционную матрицу
            Matrix4 projection = Matrix4.CreateOrthographicOffCenter(0, screenWidth, 0, screenHeight, -1.0f, 1.0f);
            _textShader.Use();
            _textShader.SetMatrix4("projection", projection);
        }

        public void RenderText(string text, float x, float y, float scale, Vector3 color)
        {
            _textShader.Use();
            _textShader.SetVector3("textColor", color);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindVertexArray(_vao);

            foreach (char c in text)
            {
                if (!_characters.TryGetValue(c, out var ch))
                    continue;

                float xpos = x + ch.Bearing.X * scale;
                float ypos = y - (ch.Size.Y - ch.Bearing.Y) * scale;

                float w = ch.Size.X * scale;
                float h = ch.Size.Y * scale;

                float[] vertices = {
                xpos,     ypos + h,   0.0f, 0.0f,
                xpos,     ypos,       0.0f, 1.0f,
                xpos + w, ypos,       1.0f, 1.0f,

                xpos,     ypos + h,   0.0f, 0.0f,
                xpos + w, ypos,       1.0f, 1.0f,
                xpos + w, ypos + h,   1.0f, 0.0f
            };

                GL.BindTexture(TextureTarget.Texture2D, ch.TextureID);
                GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
                GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, vertices.Length * sizeof(float), vertices);

                GL.DrawArrays(PrimitiveType.Triangles, 0, 6);

                x += (ch.Advance / 64.0f) * scale; // Advance хранится в 1/64 пикселя
            }

            GL.BindVertexArray(0);
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }
    }


}
