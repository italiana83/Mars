using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mars
{
    public class BoundingBoxRenderer
    {
        private int _vao;
        private int _vbo;
        private Shader _shader;

        public BoundingBoxRenderer()
        {
        }

        public void CreateBoundingBox(Vector3 min, Vector3 max)
        {
            // Вершины параллелепипеда
            float[] vertices = {
            // Нижняя грань
            min.X, min.Y, min.Z, max.X, min.Y, min.Z,
            max.X, min.Y, min.Z, max.X, max.Y, min.Z,
            max.X, max.Y, min.Z, min.X, max.Y, min.Z,
            min.X, max.Y, min.Z, min.X, min.Y, min.Z,
            
            // Верхняя грань
            min.X, min.Y, max.Z, max.X, min.Y, max.Z,
            max.X, min.Y, max.Z, max.X, max.Y, max.Z,
            max.X, max.Y, max.Z, min.X, max.Y, max.Z,
            min.X, max.Y, max.Z, min.X, min.Y, max.Z,
            
            // Связь верхней и нижней граней
            min.X, min.Y, min.Z, min.X, min.Y, max.Z,
            max.X, min.Y, min.Z, max.X, min.Y, max.Z,
            max.X, max.Y, min.Z, max.X, max.Y, max.Z,
            min.X, max.Y, min.Z, min.X, max.Y, max.Z
        };

            // Генерация VAO/VBO
            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();

            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

            // Настройка атрибутов вершин
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);

            string vertexShaderSource = @"
                #version 330 core
                layout (location = 0) in vec3 aPos;

                uniform mat4 view;
                uniform mat4 projection;

                void main()
                {
                    gl_Position = projection * view * vec4(aPos, 1.0);
                }";

            string fragmentShaderSource = @"
                #version 330 core
                out vec4 FragColor;

                void main()
                {
                    FragColor = vec4(1.0, 0.0, 0.0, 1.0);
                }";

            _shader = new Shader(vertexShaderSource, fragmentShaderSource, ShaderSourceMode.Code);

        }

        public void DrawBoundingBox(Matrix4 view, Matrix4 projection)
        {
            _shader.Use();

            // Передаем матрицы в шейдер
            _shader.SetMatrix4("view", view);
            _shader.SetMatrix4("projection", projection);

            GL.BindVertexArray(_vao);
            GL.DrawArrays(PrimitiveType.Lines, 0, 24); // 12 линий (24 вершины)
            GL.BindVertexArray(0);

        }

        public void Dispose()
        {
            if (_vao != 0) GL.DeleteVertexArray(_vao);
            if (_vbo != 0) GL.DeleteBuffer(_vbo);
        }
    }

}
