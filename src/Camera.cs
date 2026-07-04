using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mars
{
    /// <summary>
    /// Камера от первого лица: позиция, ориентация по yaw/pitch и управление WASD + мышь.
    /// </summary>
    public class Camera
    {
        /// <summary>
        /// Текущая позиция камеры в 3D-пространстве.
        /// </summary>
        public Vector3 Position = Vector3.Zero;

        /// <summary>
        /// Ориентация камеры в радианах (углы поворота вокруг осей X и Y).
        /// X: вращение вокруг вертикальной оси(горизонтальная ориентация).
        /// Y: вращение вверх и вниз(вертикальная ориентация).
        /// </summary>
        public Vector3 Orientation = new Vector3((float)Math.PI, 0f, 0f);

        /// <summary>
        /// Скорость движения камеры.
        /// </summary>
        public float MoveSpeed { get; private set; } = 1.2f;

        /// <summary>
        /// Чувствительность камеры при повороте с помощью мыши.
        /// </summary>
        public float MouseSensitivity { get; private set; } = 0.002f;

        /// <summary>Создаёт камеру с заданной скоростью перемещения и чувствительностью мыши.</summary>
        public Camera(float moveSpeed, float mouseSensitivity)
        {
            MoveSpeed = moveSpeed;
            MouseSensitivity = mouseSensitivity;
        }

        /// <summary>Строит матрицу вида LookAt из текущей позиции и углов ориентации.</summary>
        public Matrix4 GetViewMatrix()
        {
            Vector3 lookat = new Vector3();

            lookat.X = (float)(Math.Sin((float)Orientation.X) * Math.Cos((float)Orientation.Y));
            lookat.Y = (float)Math.Sin((float)Orientation.Y);
            lookat.Z = (float)(Math.Cos((float)Orientation.X) * Math.Cos((float)Orientation.Y));

            return Matrix4.LookAt(Position, Position + lookat, Vector3.UnitY);
        }

        /// <summary>
        /// Смещает камеру по локальным осям: x — вправо/влево, y — вперёд/назад, z — вверх/вниз.
        /// </summary>
        public void Move(float x, float y, float z)
        {
            Vector3 offset = new Vector3();

            Vector3 forward = new Vector3((float)Math.Sin((float)Orientation.X), 0, (float)Math.Cos((float)Orientation.X));
            Vector3 right = new Vector3(-forward.Z, 0, forward.X);

            offset += x * right;
            offset += y * forward;
            offset.Y += z;

            offset.NormalizeFast();
            offset = Vector3.Multiply(offset, MoveSpeed);

            Position += offset;
        }

        /// <summary>Добавляет поворот по горизонтали и вертикали с ограничением угла pitch.</summary>
        public void AddRotation(float x, float y)
        {
            x = x * MouseSensitivity;
            y = y * MouseSensitivity;

            Orientation.X = (Orientation.X + x) % ((float)Math.PI * 2.0f);
            Orientation.Y = Math.Max(Math.Min(Orientation.Y + y, (float)Math.PI / 2.0f - 0.1f), (float)-Math.PI / 2.0f + 0.1f);
        }

        /// <summary>Строковое представление позиции и ориентации для отладки.</summary>
        public override string ToString()
        {
            return $"Position: {Position}       Orientation: {Orientation}";
        }
    }

}
