using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mars
{
    public class Chunk
    {
        public Chunk(int index, List<Vector4> vertices, List<int> indices)
        {
            Index = index;
            Vertices = vertices;
            Indices = indices;

            BoundingBoxe = CalculateAABB(vertices);
            // Центр чанка
            Center = (BoundingBoxe.Min + BoundingBoxe.Max) / 2;

            BoundingBoxRenderer = new BoundingBoxRenderer();
            BoundingBoxRenderer.CreateBoundingBox(BoundingBoxe.Min, BoundingBoxe.Max);
        }

        public int Index { get; set; }
        public BoundingBox BoundingBoxe { get; set; }

        public BoundingBoxRenderer BoundingBoxRenderer { get; set; }


        public List<Vector4> Vertices { get; set; }
        public List<int> Indices { get; set; }
        public Vector3 Center { get; set; }

        public BoundingBox CalculateAABB(List<Vector4> vertices)
        {
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            foreach (var vertex in vertices)
            {
                min.X = MathF.Min(min.X, vertex.X);
                min.Y = MathF.Min(min.Y, vertex.Y);
                min.Z = MathF.Min(min.Z, vertex.Z);

                max.X = MathF.Max(max.X, vertex.X);
                max.Y = MathF.Max(max.Y, vertex.Y);
                max.Z = MathF.Max(max.Z, vertex.Z);
            }

            return new BoundingBox(min, max);
        }

    }
}
