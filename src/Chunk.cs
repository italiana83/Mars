using OpenTK.Mathematics;

namespace Mars;

public class Chunk
{
    public Chunk(int index, List<Vector4> vertices, List<int> indices)
    {
        Index = index;
        Vertices = vertices;
        Indices = indices;

        BoundingBoxe = CalculateAabb(vertices);
        Center = (BoundingBoxe.Min + BoundingBoxe.Max) / 2;

        BoundingBoxRenderer = new BoundingBoxRenderer();
        BoundingBoxRenderer.CreateBoundingBox(BoundingBoxe.Min, BoundingBoxe.Max);
    }

    public int Index { get; set; }
    public int ElementBuffer { get; set; }
    public BoundingBox BoundingBoxe { get; set; }
    public BoundingBoxRenderer BoundingBoxRenderer { get; set; }
    public List<Vector4> Vertices { get; set; }
    public List<int> Indices { get; set; }
    public Vector3 Center { get; set; }

    private static BoundingBox CalculateAabb(List<Vector4> vertices)
    {
        Vector3 min = new(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new(float.MinValue, float.MinValue, float.MinValue);

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
