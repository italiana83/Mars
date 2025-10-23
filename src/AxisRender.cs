using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OpenTK.Graphics.OpenGL.GL;

namespace Mars
{
    public class AxisRender
    {
        private int _vao;
        private int _vbo;
        private Shader _shader;
        float[] axisVertices;

        public AxisRender(float length, float tickSize, float tickSpacing, Vector3 offset)
        {
            // Генерация данных для осей
            axisVertices = GenerateAxisWithTicks(100.0f, 2.5f, 10.0f, offset);
            // Создаем VAO и VBO для осей
            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();

            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, axisVertices.Length * sizeof(float), axisVertices, BufferUsageHint.StaticDraw);

            // Настройка атрибутов вершин для осей
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 0); // Координаты
            GL.EnableVertexAttribArray(0);

            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 3 * sizeof(float)); // Цвет
            GL.EnableVertexAttribArray(1);

            // Создаем отдельные шейдеры для осей
            string axisVertexShaderSource = @"
            #version 330 core
            layout (location = 0) in vec3 aPosition;
            layout (location = 1) in vec3 aColor;

            uniform mat4 view;
            uniform mat4 projection;

            out vec3 ourColor;

            void main()
            {
                gl_Position = projection * view * vec4(aPosition, 1.0);
                ourColor = aColor;
            }";

            string axisFragmentShaderSource = @"
            #version 330 core
            out vec4 FragColor;
            uniform float alpha;
            in vec3 ourColor;

            void main()
            {
                FragColor = vec4(ourColor, alpha);
            }";

            _shader = new Shader(axisVertexShaderSource, axisFragmentShaderSource, ShaderSourceMode.Code);
        }

        public float[] GenerateAxisWithTicks(float length, float tickSize, float tickSpacing, Vector3 offset)
        {
            List<float> vertices = new List<float>();

            // Ось X
            vertices.AddRange(new float[] {
        -length / 2 + offset.X, offset.Y, offset.Z, 1.0f, 0.0f, 0.0f,
         length / 2 + offset.X, offset.Y, offset.Z, 1.0f, 0.0f, 0.0f
    });

            // Тики по оси X
            for (float i = -length / 2; i <= length / 2; i += tickSpacing)
            {
                vertices.AddRange(new float[] {
            i + offset.X, offset.Y - tickSize / 2, offset.Z, 1.0f, 0.0f, 0.0f,
            i + offset.X, offset.Y + tickSize / 2, offset.Z, 1.0f, 0.0f, 0.0f
        });
            }

            // Ось Y
            vertices.AddRange(new float[] {
        offset.X, -length / 2 + offset.Y, offset.Z, 0.0f, 1.0f, 0.0f,
        offset.X,  length / 2 + offset.Y, offset.Z, 0.0f, 1.0f, 0.0f
    });

            // Тики по оси Y
            for (float i = -length / 2; i <= length / 2; i += tickSpacing)
            {
                vertices.AddRange(new float[] {
            offset.X - tickSize / 2, i + offset.Y, offset.Z, 0.0f, 1.0f, 0.0f,
            offset.X + tickSize / 2, i + offset.Y, offset.Z, 0.0f, 1.0f, 0.0f
        });
            }

            // Ось Z
            vertices.AddRange(new float[] {
        offset.X, offset.Y, -length / 2 + offset.Z, 0.0f, 0.0f, 1.0f,
        offset.X, offset.Y,  length / 2 + offset.Z, 0.0f, 0.0f, 1.0f
    });

            // Тики по оси Z
            for (float i = -length / 2; i <= length / 2; i += tickSpacing)
            {
                vertices.AddRange(new float[] {
            offset.X - tickSize / 2, offset.Y, i + offset.Z, 0.0f, 0.0f, 1.0f,
            offset.X + tickSize / 2, offset.Y, i + offset.Z, 0.0f, 0.0f, 1.0f
        });
            }

            return vertices.ToArray();
        }

        public void DrawAxis(Matrix4 view, Matrix4 projection)
        {
            // Рисуем оси с отдельным шейдером
            _shader.Use();
            _shader.SetMatrix4("view", view);
            _shader.SetMatrix4("projection", projection);

            GL.BindVertexArray(_vao);
            GL.DrawArrays(PrimitiveType.Lines, 0, axisVertices.Length / 6);
            GL.BindVertexArray(0);


            //_shader.Use();

            //// Передаем матрицы в шейдер
            //_shader.SetMatrix4("view", view);
            //_shader.SetMatrix4("projection", projection);

            //GL.BindVertexArray(_vao);
            //GL.DrawArrays(PrimitiveType.Lines, 0, 24); // 12 линий (24 вершины)
            //GL.BindVertexArray(0);
        }

        public void Dispose()
        {
            if (_vao != 0) GL.DeleteVertexArray(_vao);
            if (_vbo != 0) GL.DeleteBuffer(_vbo);
        }

        //void RenderAxisLabels()
        //{
        //    // Метки для X-оси
        //    for (int i = -50; i <= 50; i += 10)
        //    {
        //        if (i == 0) continue; // Пропускаем центральную метку
        //        textRenderer.RenderText(i.ToString(), i, 0.0f, 0.5f, new Vector3(1.0f, 0.0f, 0.0f)); // Красный текст
        //    }

        //    // Метки для Y-оси
        //    for (int i = -50; i <= 50; i += 10)
        //    {
        //        if (i == 0) continue;
        //        textRenderer.RenderText(i.ToString(), 0.0f, i, 0.5f, new Vector3(0.0f, 1.0f, 0.0f)); // Зеленый текст
        //    }

        //    // Аналогично можно добавить для Z-оси
        //}
    }
}
