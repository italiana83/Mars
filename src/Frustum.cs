using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mars
{
    using OpenTK.Mathematics;
    //using System.Numerics;

    /// <summary>
    /// Пирамида видимости камеры: шесть плоскостей и проверка пересечения с AABB.
    /// </summary>
    public class Frustum
    {
        private Plane[] _planes;

        /// <summary>Инициализирует массив из шести плоскостей фрустума.</summary>
        public Frustum()
        {
            _planes = new Plane[6];
        }

        /// <summary>
        /// Обновляет фрустум на основе переданной матрицы VP (View * Projection).
        /// </summary>
        /// <param name="vpMatrix">Матрица VP (View * Projection).</param>
        public void Update(Matrix4 vpMatrix)
        {
            // Извлечение плоскостей из матрицы VP
            _planes[0] = new Plane(vpMatrix.Column3 + vpMatrix.Column0); // Левая плоскость
            _planes[1] = new Plane(vpMatrix.Column3 - vpMatrix.Column0); // Правая плоскость
            _planes[2] = new Plane(vpMatrix.Column3 + vpMatrix.Column1); // Нижняя плоскость
            _planes[3] = new Plane(vpMatrix.Column3 - vpMatrix.Column1); // Верхняя плоскость
            _planes[4] = new Plane(vpMatrix.Column3 + vpMatrix.Column2); // Ближняя плоскость
            _planes[5] = new Plane(vpMatrix.Column3 - vpMatrix.Column2); // Дальняя плоскость

            // Нормализация плоскостей
            for (int i = 0; i < _planes.Length; i++)
            {
                _planes[i].Normalize();
            }
        }

        /// <summary>
        /// Проверяет, находится ли AABB (Axis-Aligned Bounding Box) внутри фрустума.
        /// </summary>
        /// <param name="aabb">AABB объекта.</param>
        /// <returns>True, если AABB находится внутри фрустума; иначе False.</returns>
        public bool Intersects(BoundingBox aabb)
        {
            foreach (var plane in _planes)
            {
                Vector3 positiveVertex = aabb.Min;
                Vector3 negativeVertex = aabb.Max;

                if (plane.Normal.X >= 0)
                {
                    positiveVertex.X = aabb.Max.X;
                    negativeVertex.X = aabb.Min.X;
                }

                if (plane.Normal.Y >= 0)
                {
                    positiveVertex.Y = aabb.Max.Y;
                    negativeVertex.Y = aabb.Min.Y;
                }

                if (plane.Normal.Z >= 0)
                {
                    positiveVertex.Z = aabb.Max.Z;
                    negativeVertex.Z = aabb.Min.Z;
                }

                // Если положительная вершина вне плоскости, объект не видим
                if (plane.DistanceTo(positiveVertex) < 0)
                {
                    return false;
                }
            }

            return true; // AABB внутри фрустума
        }

    }

    /// <summary>
    /// Класс для работы с плоскостями в 3D пространстве.
    /// </summary>
    public struct Plane
    {
        public Vector3 Normal;
        public float Distance;

        /// <summary>Создаёт плоскость из коэффициентов уравнения Ax+By+Cz+D=0 (Vector4).</summary>
        public Plane(Vector4 coefficients)
        {
            Normal = new Vector3(coefficients.X, coefficients.Y, coefficients.Z);
            Distance = coefficients.W;
        }

        /// <summary>Нормализует нормаль и расстояние плоскости до единичной длины.</summary>
        public void Normalize()
        {
            float length = Normal.Length;
            Normal /= length;
            Distance /= length;
        }

        /// <summary>
        /// Возвращает знаковое расстояние от точки до плоскости
        /// (положительное — «перед» плоскостью по направлению нормали).
        /// </summary>
        public float DistanceTo(Vector3 point)
        {
            return Vector3.Dot(Normal, point) + Distance;
        }
    }

    /// <summary>Axis-aligned bounding box: минимальная и максимальная вершины параллелепипеда.</summary>
    public struct BoundingBox
    {
        public Vector3 Min;
        public Vector3 Max;

        /// <summary>Создаёт AABB по минимальной и максимальной точкам.</summary>
        public BoundingBox(Vector3 min, Vector3 max)
        {
            Min = min;
            Max = max;
        }
    }



}
