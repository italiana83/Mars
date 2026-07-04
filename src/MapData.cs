using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mars
{
    /// <summary>
    /// Результат чтения MOLA heightmap: сетка вершин и её размеры.
    /// </summary>
    public class MapData
    {
        /// <summary>Вершины сетки: X,Y,Z — позиция на модели, W — высота (используется шейдером для раскраски).</summary>
        public List<Vector4> Vertices { get; set; } = new List<Vector4>();

        /// <summary>Число строк и столбцов в исходной heightmap-сетке.</summary>
        public int Rows { get; set; }
        public int Cols { get; set; }

        /// <summary>Шаг дискретизации при чтении .img (прореживание данных MOLA).</summary>
        public int Step { get; set; }

        /// <summary>Краткая сводка размеров сетки и числа вершин.</summary>
        public override string ToString()
        {
            return $"Rows: {Rows}, Cols: {Cols}, Vertices count: {Vertices.Count}";
        }
    }
}
