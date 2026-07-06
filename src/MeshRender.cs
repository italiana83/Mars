using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace Mars;

/// <summary>
/// Рендеринг heightmap-меша MOLA: разбиение на чанки, frustum culling и раскраска по высоте.
/// </summary>
public class MeshRender : IDisposable
{
    /// <summary>
    /// Размер чанка в ячейках heightmap (100×100). Меньшие чанки — точнее отсечение по frustum,
    /// но больше draw calls; большие — наоборот.
    /// </summary>
    private const int ChunkSize = 100;

    /// <summary>Исходные вершины heightmap из MOLA (.img): позиция (X,Y,Z) + высота в W-компоненте.</summary>
    private readonly MapData _mapData;

    /// <summary>Шейдер с vertex/fragment stages; fragment shader интерполирует цвет по высоте вершины.</summary>
    private readonly Shader _shader;

    /// <summary>Набор чанков меша и логика выбора видимых чанков относительно камеры.</summary>
    private readonly ChunkManager _chunkManager;

    /// <summary>
    /// Нормализованные уровни высоты (0 = минимум рельефа, 1 = максимум) для построения цветовой шкалы.
    /// Каждый коэффициент задаёт опорную точку градиента; между соседними точками цвет линейно интерполируется в шейдере.
    /// Значения убывают от 1.0 к 0.0 — от высоких участков к низким (дно кратеров).
    /// </summary>
    private readonly List<float> _normalizedCoefficients =
    [
        1.0f, 0.931f, 0.861f, 0.792f, 0.723f, 0.653f,
        0.584f, 0.515f, 0.445f, 0.376f, 0.307f, 0.237f,
        0.168f, 0.0f
    ];

    /// <summary>
    /// RGB-цвета (0..1), соответствующие каждому элементу <see cref="_normalizedCoefficients"/>.
    /// Вместе образуют палитру «марсианского» рельефа: светлые тона на возвышенностях, тёмно-коричневые в низинах.
    /// Порядок элементов должен совпадать с <see cref="_normalizedCoefficients"/>.
    /// </summary>
    private readonly List<(float r, float g, float b)> _colors =
    [
        (0.863f, 0.863f, 0.863f),
        (0.800f, 0.749f, 0.706f),
        (0.741f, 0.639f, 0.549f),
        (0.682f, 0.525f, 0.392f),
        (0.624f, 0.416f, 0.235f),
        (0.682f, 0.490f, 0.294f),
        (0.741f, 0.569f, 0.353f),
        (0.800f, 0.647f, 0.412f),
        (0.859f, 0.725f, 0.471f),
        (0.922f, 0.804f, 0.533f),
        (0.827f, 0.682f, 0.439f),
        (0.737f, 0.565f, 0.349f),
        (0.643f, 0.447f, 0.259f),
        (0.553f, 0.329f, 0.169f)
    ];

    /// <summary>VAO/VBO с полным набором вершин heightmap для привязки атрибутов position + height.</summary>
    private int _vao;
    private int _vbo;
    private bool _disposed;

    /// <summary>Геометрический центр модели (середина AABB). Используется для осей координат и навигации камеры.</summary>
    public Vector3 ModelCenter { get; private set; }

    /// <summary>Минимальная и максимальная точки axis-aligned bounding box всего меша.</summary>
    public Vector3 Min { get; private set; }
    public Vector3 Max { get; private set; }

    /// <summary>Компоненты AABB по осям — нужны для расчёта абсолютных высот в <see cref="CalculateHeights"/>.</summary>
    public float MinX { get; private set; }
    public float MaxX { get; private set; }
    public float MinY { get; private set; }
    public float MaxY { get; private set; }
    public float MinZ { get; private set; }
    public float MaxZ { get; private set; }

    /// <summary>
    /// Создаёт рендерер heightmap-меша: вычисляет границы модели, разбивает её на чанки,
    /// инициализирует GPU-буферы и загружает шейдер раскраски по высоте.
    /// </summary>
    public MeshRender(MapData mapData)
    {
        _mapData = mapData;
        _chunkManager = new ChunkManager();

        ModelCenter = CalculateModelCenter();
        SplitModelIntoChunks(ChunkSize);
        InitializeBuffers();
        InitializeChunkBuffers();
        _shader = CreateShader();
    }

    /// <summary>
    /// Создаёт VAO и VBO с полным набором вершин heightmap и настраивает атрибуты position (location 0) и height (location 1).
    /// </summary>
    private void InitializeBuffers()
    {
        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();

        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(
            BufferTarget.ArrayBuffer,
            _mapData.Vertices.Count * sizeof(float) * 4,
            _mapData.Vertices.ToArray(),
            BufferUsageHint.StaticDraw);

        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
        GL.EnableVertexAttribArray(0);

        GL.VertexAttribPointer(1, 1, VertexAttribPointerType.Float, false, 4 * sizeof(float), 3 * sizeof(float));
        GL.EnableVertexAttribArray(1);
    }

    /// <summary>
    /// Создаёт EBO для каждого чанка и загружает в GPU индексы треугольников чанка.
    /// </summary>
    private void InitializeChunkBuffers()
    {
        foreach (var chunk in _chunkManager.Chunks)
        {
            chunk.ElementBuffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, chunk.ElementBuffer);
            GL.BufferData(
                BufferTarget.ElementArrayBuffer,
                chunk.Indices.Count * sizeof(int),
                chunk.Indices.ToArray(),
                BufferUsageHint.StaticDraw);
        }
    }

    /// <summary>
    /// Компилирует vertex/fragment шейдеры раскраски по высоте и передаёт в uniform массивы опорных высот и цветов.
    /// </summary>
    private Shader CreateShader()
    {
        const string vertexShaderSource = """
            #version 330 core
            layout (location = 0) in vec3 aPosition;
            layout (location = 1) in float aHeight;

            out float Height;

            uniform mat4 model;
            uniform mat4 view;
            uniform mat4 projection;

            void main()
            {
                gl_Position = projection * view * model * vec4(aPosition, 1.0);
                Height = aHeight;
            }
            """;

        const string fragmentShaderSource = """
            #version 330 core

            in float Height;

            out vec4 FragColor;

            uniform float heights[14];
            uniform vec3 colors[14];

            vec3 GetColorByHeight(float height)
            {
                for (int i = 0; i < 13; ++i) {
                    if (height >= heights[i + 1] && height <= heights[i]) {
                        float t = (height - heights[i + 1]) / (heights[i] - heights[i + 1]);
                        return mix(colors[i + 1], colors[i], t);
                    }
                }
                return colors[13];
            }

            void main()
            {
                vec3 color = GetColorByHeight(Height);
                FragColor = vec4(color, 1.0);
            }
            """;

        var shader = new Shader(vertexShaderSource, fragmentShaderSource, ShaderSourceMode.Code);
        var heightColorMap = CalculateHeights(MinY, MaxY);

        shader.Use();
        SetHeightColorData(shader, heightColorMap);
        return shader;
    }

    /// <summary>
    /// Записывает в шейдер uniform-массивы heights и colors из словаря «высота → RGB».
    /// </summary>
    private void SetHeightColorData(Shader shader, Dictionary<int, (float r, float g, float b)> heightColorMap)
    {
        var heights = new List<float>();
        var colors = new List<float>();

        foreach (var entry in heightColorMap)
        {
            heights.Add(entry.Key);
            colors.Add(entry.Value.r);
            colors.Add(entry.Value.g);
            colors.Add(entry.Value.b);
        }

        shader.SetArray1("heights", heights.ToArray());
        shader.SetArray3("colors", colors.ToArray());
    }

    /// <summary>
    /// Переводит нормализованные коэффициенты в абсолютные высоты [minHeight..maxHeight]
    /// и сопоставляет каждой опорной высоте цвет из палитры.
    /// Результат передаётся во fragment shader как uniform-массивы heights/colors.
    /// </summary>
    public Dictionary<int, (float r, float g, float b)> CalculateHeights(float minHeight, float maxHeight)
    {
        if (maxHeight == minHeight)
            throw new ArgumentException("maxHeight и minHeight не должны быть равны.");

        var heightColorMap = new Dictionary<int, (float r, float g, float b)>();

        for (int i = 0; i < _normalizedCoefficients.Count; i++)
        {
            if (_normalizedCoefficients[i] is < 0.0f or > 1.0f)
                throw new ArgumentException("Коэффициент должен находиться в диапазоне от 0.0 до 1.0.");

            int height = (int)(minHeight + _normalizedCoefficients[i] * (maxHeight - minHeight));
            heightColorMap[height] = _colors[i];
        }

        return heightColorMap;
    }

    /// <summary>
    /// Извлекает подмножество вершин heightmap, принадлежащее заданному прямоугольному чанку сетки.
    /// </summary>
    private List<Vector4> GetVerticesForChunk(int startRow, int startCol, int chunkRows, int chunkCols)
    {
        List<Vector4> chunkVertices = new();

        int endRow = Math.Min(startRow + chunkRows + 1, _mapData.Rows);
        int endCol = Math.Min(startCol + chunkCols + 1, _mapData.Cols);

        for (int y = startRow; y < endRow; y++)
        {
            for (int x = startCol; x < endCol; x++)
            {
                int vertexIndex = y * _mapData.Cols + x;
                chunkVertices.Add(_mapData.Vertices[vertexIndex]);
            }
        }

        return chunkVertices;
    }

    /// <summary>
    /// Строит индексы двух треугольников на ячейку для чанка heightmap (топология regular grid).
    /// </summary>
    private List<int> GenerateHeightMapIndicesForChunk(int startRow, int startCol, int chunkRows, int chunkCols)
    {
        List<int> indices = new();

        int endRow = Math.Min(startRow + chunkRows, _mapData.Rows - 1);
        int endCol = Math.Min(startCol + chunkCols, _mapData.Cols - 1);

        for (int y = startRow; y < endRow; y++)
        {
            for (int x = startCol; x < endCol; x++)
            {
                int topLeft = y * _mapData.Cols + x;
                int topRight = topLeft + 1;
                int bottomLeft = (y + 1) * _mapData.Cols + x;
                int bottomRight = bottomLeft + 1;

                if (x + 1 < _mapData.Cols && y + 1 < _mapData.Rows)
                {
                    indices.Add(topLeft);
                    indices.Add(bottomLeft);
                    indices.Add(topRight);

                    indices.Add(topRight);
                    indices.Add(bottomLeft);
                    indices.Add(bottomRight);
                }
            }
        }

        return indices;
    }

    /// <summary>
    /// Проходит по всем вершинам heightmap, вычисляет AABB модели и его центр.
    /// MinY/MaxY определяют диапазон высот для цветовой шкалы; ModelCenter — точка привязки осей.
    /// </summary>
    private Vector3 CalculateModelCenter()
    {
        MinX = float.MaxValue;
        MaxX = float.MinValue;
        MinY = float.MaxValue;
        MaxY = float.MinValue;
        MinZ = float.MaxValue;
        MaxZ = float.MinValue;

        foreach (var vertex in _mapData.Vertices)
        {
            MinX = MathF.Min(MinX, vertex.X);
            MinY = MathF.Min(MinY, vertex.Y);
            MinZ = MathF.Min(MinZ, vertex.Z);

            MaxX = MathF.Max(MaxX, vertex.X);
            MaxY = MathF.Max(MaxY, vertex.Y);
            MaxZ = MathF.Max(MaxZ, vertex.Z);
        }

        Min = new Vector3(MinX, MinY, MinZ);
        Max = new Vector3(MaxX, MaxY, MaxZ);

        return (Min + Max) * 0.5f;
    }

    /// <summary>
    /// Делит heightmap на квадратные чанки <see cref="ChunkSize"/>×<see cref="ChunkSize"/> для frustum culling:
    /// отрисовываются только чанки, попадающие в поле зрения камеры.
    /// </summary>
    private void SplitModelIntoChunks(int chunkSize)
    {
        for (int row = 0; row < _mapData.Rows; row += chunkSize)
        {
            for (int col = 0; col < _mapData.Cols; col += chunkSize)
            {
                int chunkRows = Math.Min(chunkSize, _mapData.Rows - row);
                int chunkCols = Math.Min(chunkSize, _mapData.Cols - col);

                var chunkVertices = GetVerticesForChunk(row, col, chunkRows, chunkCols);
                var indices = GenerateHeightMapIndicesForChunk(row, col, chunkRows, chunkCols);

                int chunkId = _chunkManager.Chunks.Count;
                _chunkManager.Chunks.Add(new Chunk(chunkId, chunkVertices, indices));
            }
        }
    }

    /// <summary>
    /// Отрисовывает видимые по frustum чанки меша с заданными матрицами view, projection и model.
    /// </summary>
    public void DrawMesh(Matrix4 view, Matrix4 projection, Matrix4 model, Vector3 cameraPosition, Frustum frustum)
    {
        _shader.Use();
        _shader.SetMatrix4("view", view);
        _shader.SetMatrix4("projection", projection);
        _shader.SetMatrix4("model", model);

        GL.BindVertexArray(_vao);

        foreach (var chunk in _chunkManager.GetVisibleChunks(frustum))
        {
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, chunk.ElementBuffer);
            GL.DrawElements(PrimitiveType.Triangles, chunk.Indices.Count, DrawElementsType.UnsignedInt, 0);
        }
    }

    /// <summary>
    /// Освобождает GPU-ресурсы: EBO чанков, VAO и VBO вершин.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        foreach (var chunk in _chunkManager.Chunks)
        {
            if (chunk.ElementBuffer != 0)
                GL.DeleteBuffer(chunk.ElementBuffer);
        }

        if (_vao != 0)
            GL.DeleteVertexArray(_vao);
        if (_vbo != 0)
            GL.DeleteBuffer(_vbo);

        _shader.Dispose();

        _vao = 0;
        _vbo = 0;
        _disposed = true;
    }
}
