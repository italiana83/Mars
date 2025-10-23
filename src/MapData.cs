using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mars
{
    public class MapData
    {
        public List<Vector4> Vertices { get; set; } = new List<Vector4>();
        public int Rows { get; set; }
        public int Cols { get; set; }
        public int Step { get; set; }

        public override string ToString()
        {
            return $"Rows: {Rows}, Cols: {Cols}, Vertices count: {Vertices.Count}";
        }
    }
}
