using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System.IO;
using System.Reflection.Metadata;

namespace Mars
{
    public enum ShaderSourceMode
    {
        File = 0,
        Code = 1
    }
    public class Shader
    {
        private readonly int _handle;

        public Shader(string vertexSource, string fragmentSource, ShaderSourceMode mode)
        {
            if (mode == ShaderSourceMode.File)
            {
                // Читаем исходный код шейдеров из файлов
                vertexSource = File.ReadAllText(vertexSource);
                fragmentSource = File.ReadAllText(fragmentSource);
            }

            // Компилируем шейдеры
            int vertexShader = CompileShader(ShaderType.VertexShader, vertexSource);
            int fragmentShader = CompileShader(ShaderType.FragmentShader, fragmentSource);

            // Создаем шейдерную программу и связываем шейдеры
            _handle = GL.CreateProgram();
            GL.AttachShader(_handle, vertexShader);
            GL.AttachShader(_handle, fragmentShader);
            GL.LinkProgram(_handle);

            // Проверяем наличие ошибок при связывании
            GL.GetProgram(_handle, GetProgramParameterName.LinkStatus, out int linkStatus);
            if (linkStatus == 0)
            {
                string infoLog = GL.GetProgramInfoLog(_handle);
                throw new Exception($"Ошибка связывания шейдерной программы: {infoLog}");
            }

            // Удаляем промежуточные объекты шейдеров после их связывания
            GL.DeleteShader(vertexShader);
            GL.DeleteShader(fragmentShader);
        }

        private int CompileShader(ShaderType type, string source)
        {
            // Создаем шейдер
            int shader = GL.CreateShader(type);
            GL.ShaderSource(shader, source);
            GL.CompileShader(shader);

            // Проверяем наличие ошибок компиляции
            GL.GetShader(shader, ShaderParameter.CompileStatus, out int compileStatus);
            if (compileStatus == 0)
            {
                string infoLog = GL.GetShaderInfoLog(shader);
                throw new Exception($"Ошибка компиляции {type}: {infoLog}");
            }

            return shader;
        }

        public void Use()
        {
            GL.UseProgram(_handle);
        }

        public void SetMatrix4(string name, Matrix4 matrix)
        {
            int location = GL.GetUniformLocation(_handle, name);
            if (location == -1)
                throw new Exception($"Не удалось найти uniform-переменную с именем {name}");

            GL.UniformMatrix4(location, false, ref matrix);
        }

        public void SetVector2(string name, Vector2 vector)
        {
            int location = GL.GetUniformLocation(_handle, name);
            if (location == -1)
                throw new Exception($"Не удалось найти uniform-переменную с именем {name}");

            GL.Uniform2(location, vector);
        }

        public void SetVector3(string name, Vector3 value)
        {
            int location = GL.GetUniformLocation(_handle, name);
            if (location == -1)
                throw new Exception($"Не удалось найти uniform-переменную с именем {name}");

            GL.Uniform3(location, value);
        }

        public void SetArray1(string name, float[] array)
        {
            int location = GL.GetUniformLocation(_handle, name);
            if (location == -1)
                throw new Exception($"Не удалось найти uniform-переменную с именем {name}");

            GL.Uniform1(location, array.Length, array);
        }

        public void SetArray3(string name, float[] array)
        {
            int location = GL.GetUniformLocation(_handle, name);
            if (location == -1)
                throw new Exception($"Не удалось найти uniform-переменную с именем {name}");

            GL.Uniform3(location, array.Length / 3, array);
        }

        public void SetFloat(string name, float value)
        {
            int location = GL.GetUniformLocation(_handle, name);
            if (location == -1)
                throw new Exception($"Не удалось найти uniform-переменную с именем {name}");

            GL.Uniform1(location, value);
        }

        public int GetAttribLocation(string attribName)
        {
            int location = GL.GetAttribLocation(_handle, attribName);
            if (location == -1)
            {
                Console.WriteLine($"Внимание: атрибут {attribName} не найден или не используется в шейдере.");
            }
            return location;
        }

        ~Shader()
        {
            GL.DeleteProgram(_handle);
        }
    }
}
