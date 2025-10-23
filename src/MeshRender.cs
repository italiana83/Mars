using OpenTK.Compute.OpenCL;
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
    public class MeshRender
    {
        int maxDetailLevel = 8; // Максимальный уровень детализации
        MapData _mapData;

        private int _vao;
        private int _vbo;
        private int _ebo;

        public Vector3 ModelCenter { get; set; }
        public Vector3 Min { get; set; }
        public Vector3 Max { get; set; }

        public float MinX { get; set; }
        public float MaxX { get; set; }
        public float MinY { get; set; }
        public float MaxY { get; set; }
        public float MinZ { get; set; }
        public float MaxZ { get; set; }

        private Shader _shader;
        private ChunkManager _chunkManager;

        // Список нормализованных коэффициентов
        List<float> normalizedCoefficients = new List<float>
                    {
                        1.0f, 0.931f, 0.861f, 0.792f, 0.723f, 0.653f,
                        0.584f, 0.515f, 0.445f, 0.376f, 0.307f, 0.237f,
                        0.168f, 0.0f
                    };

        // Список RGB-значений в диапазоне 0-1
        List<(float r, float g, float b)> colors = new List<(float r, float g, float b)>
                    {
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
                    };


        public MeshRender(MapData mapData)
        {
            _mapData = mapData;
            //List<Vector4> vertices = GenerateHeightMapVertices(elevationData);
            _mapData = mapData;
            Console.WriteLine(_mapData.ToString());

            ModelCenter = CalculateModelCenter();

            _chunkManager = new ChunkManager();

            // Разбиваем модель на чанки и добавляем их в ChunkManager
            int chunkSize = 100; // Размер чанка
            SplitModelIntoChunks(chunkSize);

            InitializeBuffers();
            InitializeShader();
        }

        private void InitializeBuffers()
        {
            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();

            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, _mapData.Vertices.Count * sizeof(float) * 4, _mapData.Vertices.ToArray(), BufferUsageHint.StaticDraw);

            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            GL.VertexAttribPointer(1, 1, VertexAttribPointerType.Float, false, 4 * sizeof(float), 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);

            _ebo = GL.GenBuffer();
        }

        private void InitializeShader()
        {
            string vertexShaderSource = @"
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
}";

            string fragmentShaderSource = @"
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
}";

            _shader = new Shader(vertexShaderSource, fragmentShaderSource, ShaderSourceMode.Code);

            var heightColorMap = CalculateHeights(MinY, MaxY);

            foreach (var h in heightColorMap)
                Console.WriteLine(h);

            _shader.Use();
            SetHeightColorData(heightColorMap);
        }

        public void SetHeightColorData(Dictionary<int, (float r, float g, float b)> heightColorMap)
        {
            // Получаем массивы для высот и цветов
            var heights = new List<float>();
            var colors = new List<float>();

            foreach (var entry in heightColorMap)
            {
                heights.Add(entry.Key);
                colors.Add(entry.Value.r);
                colors.Add(entry.Value.g);
                colors.Add(entry.Value.b);
            }

            // Преобразуем массивы в массивы float[]
            float[] heightArray = heights.ToArray();
            float[] colorArray = colors.ToArray();

            _shader.SetArray1("heights", heightArray);
            _shader.SetArray3("colors", colorArray);

            //// Создаем и отправляем в шейдер массив высот
            //int heightLocation = GL.GetUniformLocation(_shader, "heights");
            //GL.ProgramUniform1fv(_shader, heightLocation, heightArray.Length, heightArray);

            //// Создаем и отправляем в шейдер массив цветов
            //int colorLocation = GL.GetUniformLocation(_shader, "colors");
            //GL.ProgramUniform3fv(_shader, colorLocation, colorArray.Length / 3, colorArray);
        }

        /// <summary>
        /// Вычисляет список высот на основе коэффициентов и диапазона высот.
        /// </summary>
        /// <param name="minHeight">Минимальная высота.</param>
        /// <param name="maxHeight">Максимальная высота.</param>
        /// <returns>Список высот.</returns>
        public Dictionary<int, (float r, float g, float b)> CalculateHeights(float minHeight, float maxHeight)
        {
            var heightColorMap = new Dictionary<int, (float r, float g, float b)>();

            // Проверка на корректность диапазона
            if (maxHeight == minHeight)
                throw new ArgumentException("maxHeight и minHeight не должны быть равны.");

            // Вычисление высот и заполнение словаря
            for (int i = 0; i < normalizedCoefficients.Count; i++)
            {
                // Проверка на корректность коэффициента
                if (normalizedCoefficients[i] < 0.0f || normalizedCoefficients[i] > 1.0f)
                    throw new ArgumentException("Коэффициент должен находиться в диапазоне от 0.0 до 1.0.");

                int height = (int)(minHeight + normalizedCoefficients[i] * (maxHeight - minHeight));
                heightColorMap[height] = colors[i];
            }

            return heightColorMap;
        }

        // Формируем вершины для текущего чанка с перекрытием
        public List<Vector4> GetVerticesForChunk(int startRow, int startCol, int chunkRows, int chunkCols)
        {
            List<Vector4> chunkVertices = new List<Vector4>();

            // Границы чанка с учётом одной дополнительной строки/столбца
            int endRow = Math.Min(startRow + chunkRows + 1, _mapData.Rows); // +1 строка только если она существует
            int endCol = Math.Min(startCol + chunkCols + 1, _mapData.Cols); // +1 столбец только если он существует

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

        // Генерация локальных индексов для вершины
        public List<int> GenerateHeightMapIndicesForChunk(int startRow, int startCol, int chunkRows, int chunkCols)
        {
            List<int> indices = new List<int>();

            // Границы чанка с учётом одной дополнительной строки/столбца
            int endRow = Math.Min(startRow + chunkRows, _mapData.Rows - 1); // +1 строка только если она существует
            int endCol = Math.Min(startCol + chunkCols, _mapData.Cols - 1); // +1 столбец только если он существует

            for (int y = startRow; y < endRow; y++)
            {
                for (int x = startCol; x < endCol; x++)
                {
                    // Индексы вершин для текущей ячейки
                    int topLeft = y * _mapData.Cols + x;
                    int topRight = topLeft + 1;
                    int bottomLeft = (y + 1) * _mapData.Cols + x;
                    int bottomRight = bottomLeft + 1;

                    // Проверяем, чтобы не выходить за границы чанка
                    if (x + 1 < _mapData.Cols && y + 1 < _mapData.Rows)
                    {
                        // Формируем два треугольника
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

        public Vector3 CalculateModelCenter()
        {
            MinX = float.MaxValue;
            MaxX = float.MinValue;
            MinY = float.MaxValue;
            MaxY = float.MinValue;
            MinZ = float.MaxValue;
            MaxZ = float.MinValue;

            foreach (var vertex in _mapData.Vertices)
            {
                float x = vertex.X;
                float y = vertex.Y;
                float z = vertex.Z;

                MinX = MathF.Min(MinX, x);
                MinY = MathF.Min(MinY, y);
                MinZ = MathF.Min(MinZ, z);

                MaxX = MathF.Max(MaxX, x);
                MaxY = MathF.Max(MaxY, y);
                MaxZ = MathF.Max(MaxZ, z);
            }

            Min = new Vector3(MinX, MinY, MinZ);
            Max = new Vector3(MaxX, MaxY, MaxZ);

            return new Vector3(
                (MinX + MaxX) / 2.0f,
                (MinY + MaxY) / 2.0f,
                (MinZ + MaxZ) / 2.0f
            );
        }

        public void SplitModelIntoChunks(int chunkSize)
        {
            for (int row = 0; row < _mapData.Rows; row += chunkSize)
            {
                for (int col = 0; col < _mapData.Cols; col += chunkSize)
                {
                    try
                    {
                        int chunkRows = Math.Min(chunkSize, _mapData.Rows - row);
                        int chunkCols = Math.Min(chunkSize, _mapData.Cols - col);

                        // Вершины и индексы с учётом перекрытия
                        var chunkVertices = GetVerticesForChunk(row, col, chunkRows, chunkCols);
                        var indices = GenerateHeightMapIndicesForChunk(row, col, chunkRows, chunkCols);

                        Console.WriteLine($"Чанк ({row}, {col}) - Вершин: {chunkVertices.Count}, Индексов: {indices.Count}");

                        int chunkId = _chunkManager.Chunks.Count;
                        var chunk = new Chunk(chunkId, chunkVertices, indices);
                        _chunkManager.Chunks.Add(chunk);

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка для чанка ({row}, {col}): {ex.Message}");
                    }
                }
            }
        }

        public int GetDetailLevel(Vector3 cameraPosition, BoundingBox chunkBounds, int maxDetailLevel)
        {
            // Центр чанка
            Vector3 chunkCenter = (chunkBounds.Min + chunkBounds.Max) / 2;

            // Расстояние от камеры до чанка
            float distance = Vector3.Distance(cameraPosition, chunkCenter);

            // Чем больше расстояние, тем выше значение detailLevel
            if (distance > 400)
                return maxDetailLevel; // Минимальная детализация
            else if (distance > 300)
                return 4; // Низкая детализация
            else if (distance > 100)
                return 2; // Средняя детализация
            else
                return 1; // Высокая детализация
        }

        public void DrawMesh(Matrix4 view, Matrix4 projection, Matrix4 model, Vector3 cameraPosition, Frustum frustum)
        {
            _shader.Use();
            _shader.SetMatrix4("view", view);
            _shader.SetMatrix4("projection", projection);
            _shader.SetMatrix4("model", model);

            GL.BindVertexArray(_vao);

            // Получаем видимые чанки
            var visibleChunks = _chunkManager.GetVisibleChunks(frustum);

            Console.WriteLine($"visibleChunks.Count: {visibleChunks.Count}");
            foreach (var chunk in visibleChunks)
            {
                // Определяем уровень детализации
                int detailLevel = GetDetailLevel(cameraPosition, chunk.BoundingBoxe, maxDetailLevel);

                //if (detailLevel < 8)
                //{
                var chunkIndices = chunk.Indices;

                // Обновляем EBO для текущего чанка
                GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
                GL.BufferData(BufferTarget.ElementArrayBuffer, chunkIndices.Count * sizeof(int), chunkIndices.ToArray(), BufferUsageHint.DynamicDraw);

                // Рисуем чанк
                GL.DrawElements(PrimitiveType.Triangles, chunkIndices.Count, DrawElementsType.UnsignedInt, 0);

                //chunk.BoundingBoxRenderer.DrawBoundingBox(view, projection);

                //}
            }
        }

        public List<int> GenerateHeightMapIndices()
        {
            List<int> indices = new List<int>();

            for (int y = 0; y < _mapData.Rows - 1; y++)
            {
                for (int x = 0; x < _mapData.Cols - 1; x++)
                {
                    int topLeft = y * _mapData.Cols + x;
                    int topRight = topLeft + 1;
                    int bottomLeft = (y + 1) * _mapData.Cols + x;
                    int bottomRight = bottomLeft + 1;

                    // Первый треугольник
                    indices.Add(topLeft);
                    indices.Add(bottomLeft);
                    indices.Add(topRight);

                    // Второй треугольник
                    indices.Add(topRight);
                    indices.Add(bottomLeft);
                    indices.Add(bottomRight);
                }
            }

            return indices;
        }
        public void Dispose()
        {
            if (_vao != 0)
                GL.DeleteVertexArray(_vao);
            if (_vbo != 0)
                GL.DeleteBuffer(_vbo);
            if (_ebo != 0)
                GL.DeleteBuffer(_ebo);
        }
    }










    //    public class MeshRender
    //    {
    //        MapData _mapData;

    //        //private List<Vector4> _vertices;
    //        private List<int> _indices;
    //        private int _vao;
    //        private int _vbo;
    //        private int _ebo;

    //        public Vector3 ModelCenter { get; set; }
    //        public Vector3 Min { get; set; }
    //        public Vector3 Max { get; set; }

    //        public float MinX { get; set; }
    //        public float MaxX { get; set; }
    //        public float MinY { get; set; }
    //        public float MaxY { get; set; }
    //        public float MinZ { get; set; }
    //        public float MaxZ { get; set; }

    //        private Shader _shader;

    //        // Список нормализованных коэффициентов
    //        List<float> normalizedCoefficients = new List<float>
    //                            {
    //                                1.0f, 0.931f, 0.861f, 0.792f, 0.723f, 0.653f,
    //                                0.584f, 0.515f, 0.445f, 0.376f, 0.307f, 0.237f,
    //                                0.168f, 0.0f
    //                            };

    //        // Список RGB-значений в диапазоне 0-1
    //        List<(float r, float g, float b)> colors = new List<(float r, float g, float b)>
    //                            {
    //                                (0.863f, 0.863f, 0.863f),
    //                                (0.800f, 0.749f, 0.706f),
    //                                (0.741f, 0.639f, 0.549f),
    //                                (0.682f, 0.525f, 0.392f),
    //                                (0.624f, 0.416f, 0.235f),
    //                                (0.682f, 0.490f, 0.294f),
    //                                (0.741f, 0.569f, 0.353f),
    //                                (0.800f, 0.647f, 0.412f),
    //                                (0.859f, 0.725f, 0.471f),
    //                                (0.922f, 0.804f, 0.533f),
    //                                (0.827f, 0.682f, 0.439f),
    //                                (0.737f, 0.565f, 0.349f),
    //                                (0.643f, 0.447f, 0.259f),
    //                                (0.553f, 0.329f, 0.169f)
    //                            };


    //        // Список чанков
    //        private List<(List<Vector4> vertices, List<int> indices, BoundingBox boundingBox)> _chunks;

    //        public MeshRender(MapData mapData, int chunkSize)
    //        {
    //            _mapData = mapData;

    //            ModelCenter = CalculateModelCenter();

    //            // Генерация чанков
    //            _chunks = GenerateChunks(chunkSize);

    //            // Инициализация шейдера
    //            InitializeShader();
    //        }

    //        private void InitializeShader()
    //        {
    //            string vertexShaderSource = @"
    //#version 330 core
    //layout (location = 0) in vec3 aPosition;
    //layout (location = 1) in float aHeight;

    //out float Height;

    //uniform mat4 model;
    //uniform mat4 view;
    //uniform mat4 projection;

    //void main()
    //{
    //    gl_Position = projection * view * model * vec4(aPosition, 1.0);
    //    Height = aHeight;
    //}";

    //            string fragmentShaderSource = @"
    //#version 330 core

    //in float Height;

    //out vec4 FragColor;

    //uniform float heights[14];
    //uniform vec3 colors[14];

    //vec3 GetColorByHeight(float height)
    //{
    //    for (int i = 0; i < 13; ++i) {
    //        if (height >= heights[i + 1] && height <= heights[i]) {
    //            float t = (height - heights[i + 1]) / (heights[i] - heights[i + 1]);
    //            return mix(colors[i + 1], colors[i], t);
    //        }
    //    }
    //    return colors[13];
    //}

    //void main()
    //{
    //    vec3 color = GetColorByHeight(Height);
    //    FragColor = vec4(color, 1.0);
    //}";

    //            _shader = new Shader(vertexShaderSource, fragmentShaderSource, ShaderSourceMode.Code);

    //            var heightColorMap = CalculateHeights(MinY, MaxY);

    //            foreach (var h in heightColorMap)
    //                Console.WriteLine(h);

    //            _shader.Use();
    //            SetHeightColorData(heightColorMap);
    //        }

    //        public void SetHeightColorData(Dictionary<int, (float r, float g, float b)> heightColorMap)
    //        {
    //            // Получаем массивы для высот и цветов
    //            var heights = new List<float>();
    //            var colors = new List<float>();

    //            foreach (var entry in heightColorMap)
    //            {
    //                heights.Add(entry.Key);
    //                colors.Add(entry.Value.r);
    //                colors.Add(entry.Value.g);
    //                colors.Add(entry.Value.b);
    //            }

    //            // Преобразуем массивы в массивы float[]
    //            float[] heightArray = heights.ToArray();
    //            float[] colorArray = colors.ToArray();

    //            _shader.SetArray1("heights", heightArray);
    //            _shader.SetArray3("colors", colorArray);

    //            //// Создаем и отправляем в шейдер массив высот
    //            //int heightLocation = GL.GetUniformLocation(_shader, "heights");
    //            //GL.ProgramUniform1fv(_shader, heightLocation, heightArray.Length, heightArray);

    //            //// Создаем и отправляем в шейдер массив цветов
    //            //int colorLocation = GL.GetUniformLocation(_shader, "colors");
    //            //GL.ProgramUniform3fv(_shader, colorLocation, colorArray.Length / 3, colorArray);
    //        }

    //        /// <summary>
    //        /// Вычисляет список высот на основе коэффициентов и диапазона высот.
    //        /// </summary>
    //        /// <param name="minHeight">Минимальная высота.</param>
    //        /// <param name="maxHeight">Максимальная высота.</param>
    //        /// <returns>Список высот.</returns>
    //        public Dictionary<int, (float r, float g, float b)> CalculateHeights(float minHeight, float maxHeight)
    //        {
    //            var heightColorMap = new Dictionary<int, (float r, float g, float b)>();

    //            // Проверка на корректность диапазона
    //            if (maxHeight == minHeight)
    //                throw new ArgumentException("maxHeight и minHeight не должны быть равны.");

    //            // Вычисление высот и заполнение словаря
    //            for (int i = 0; i < normalizedCoefficients.Count; i++)
    //            {
    //                // Проверка на корректность коэффициента
    //                if (normalizedCoefficients[i] < 0.0f || normalizedCoefficients[i] > 1.0f)
    //                    throw new ArgumentException("Коэффициент должен находиться в диапазоне от 0.0 до 1.0.");

    //                int height = (int)(minHeight + normalizedCoefficients[i] * (maxHeight - minHeight));
    //                heightColorMap[height] = colors[i];
    //            }

    //            return heightColorMap;
    //        }

    //        public Vector3 CalculateModelCenter()
    //        {
    //            MinX = float.MaxValue;
    //            MaxX = float.MinValue;
    //            MinY = float.MaxValue;
    //            MaxY = float.MinValue;
    //            MinZ = float.MaxValue;
    //            MaxZ = float.MinValue;

    //            foreach (var vertex in _mapData.Vertices)
    //            {
    //                float x = vertex.X;
    //                float y = vertex.Y;
    //                float z = vertex.Z;

    //                MinX = MathF.Min(MinX, x);
    //                MinY = MathF.Min(MinY, y);
    //                MinZ = MathF.Min(MinZ, z);

    //                MaxX = MathF.Max(MaxX, x);
    //                MaxY = MathF.Max(MaxY, y);
    //                MaxZ = MathF.Max(MaxZ, z);
    //            }

    //            Min = new Vector3(MinX, MinY, MinZ);
    //            Max = new Vector3(MaxX, MaxY, MaxZ);

    //            return new Vector3(
    //                (MinX + MaxX) / 2.0f,
    //                (MinY + MaxY) / 2.0f,
    //                (MinZ + MaxZ) / 2.0f
    //            );
    //        }


    //        private List<(List<Vector4>, List<int>, BoundingBox)> GenerateChunks(int chunkSize)
    //        {
    //            var chunks = new List<(List<Vector4>, List<int>, BoundingBox)>();

    //            int totalVertices = _mapData.Vertices.Count;
    //            for (int i = 0; i < totalVertices; i += chunkSize)
    //            {
    //                // Создаем чанки
    //                int end = Math.Min(i + chunkSize, totalVertices);
    //                var chunkVertices = _mapData.Vertices.GetRange(i, end - i);

    //                // Генерируем индексы для этого чанка
    //                var chunkIndices = GenerateHeightMapIndicesForChunk(i, end, _mapData.Cols);

    //                // Вычисляем границы AABB для чанка
    //                var boundingBox = CalculateAABB(chunkVertices);

    //                chunks.Add((chunkVertices, chunkIndices, boundingBox));
    //            }

    //            return chunks;
    //        }

    //        public BoundingBox CalculateAABB(List<Vector4> vertices)
    //        {
    //            if (vertices == null || vertices.Count == 0)
    //                throw new ArgumentException("Список вершин не может быть пустым.");

    //            // Инициализируем минимальные и максимальные значения
    //            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
    //            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

    //            // Проходим по всем вершинам и обновляем границы AABB
    //            foreach (var vertex in vertices)
    //            {
    //                min.X = MathF.Min(min.X, vertex.X);
    //                min.Y = MathF.Min(min.Y, vertex.Y);
    //                min.Z = MathF.Min(min.Z, vertex.Z);

    //                max.X = MathF.Max(max.X, vertex.X);
    //                max.Y = MathF.Max(max.Y, vertex.Y);
    //                max.Z = MathF.Max(max.Z, vertex.Z);
    //            }

    //            // Возвращаем вычисленный AABB
    //            return new BoundingBox(min, max);
    //        }

    //        public List<int> GenerateHeightMapIndicesForChunk(int startIndex, int endIndex, int cols)
    //        {
    //            List<int> chunkIndices = new List<int>();

    //            // Размер чанка
    //            int totalVerticesInChunk = endIndex - startIndex + 1;

    //            // Рассчитываем локальное количество столбцов для чанка
    //            int localCols = Math.Min(totalVerticesInChunk, cols);
    //            int rows = totalVerticesInChunk / localCols;

    //            // Если остались вершины вне полного ряда, добавляем еще одну строку
    //            if (totalVerticesInChunk % localCols != 0)
    //                rows++;

    //            // Проверяем минимальные размеры
    //            if (rows < 2 || localCols < 2)
    //            {
    //                Console.WriteLine($"Чанк слишком мал: rows={rows}, localCols={localCols}");
    //                return chunkIndices;
    //            }

    //            // Генерация индексов
    //            for (int y = 0; y < rows - 1; y++)
    //            {
    //                for (int x = 0; x < localCols - 1; x++)
    //                {
    //                    // Локальные индексы в пределах чанка
    //                    int topLeft = startIndex + y * localCols + x;
    //                    int topRight = topLeft + 1;
    //                    int bottomLeft = topLeft + localCols;
    //                    int bottomRight = bottomLeft + 1;

    //                    // Проверяем, что индексы не выходят за границы
    //                    if (bottomLeft > endIndex || bottomRight > endIndex)
    //                        continue;

    //                    // Добавляем индексы двух треугольников
    //                    chunkIndices.Add(topLeft);
    //                    chunkIndices.Add(bottomLeft);
    //                    chunkIndices.Add(topRight);

    //                    chunkIndices.Add(topRight);
    //                    chunkIndices.Add(bottomLeft);
    //                    chunkIndices.Add(bottomRight);
    //                }
    //            }

    //            return chunkIndices;
    //        }

    //        public void DrawMesh(Matrix4 view, Matrix4 projection, Matrix4 model, Frustum frustum)
    //        {
    //            _shader.Use();
    //            _shader.SetMatrix4("view", view);
    //            _shader.SetMatrix4("projection", projection);
    //            _shader.SetMatrix4("model", model);

    //            foreach (var (vertices, indices, boundingBox) in _chunks)
    //            {
    //                // Проверяем видимость чанка с помощью фрустума
    //                if (!frustum.Intersects(boundingBox))
    //                    continue;

    //                // Генерация VAO/VBO для чанка
    //                int vao = GL.GenVertexArray();
    //                int vbo = GL.GenBuffer();
    //                int ebo = GL.GenBuffer();

    //                GL.BindVertexArray(vao);

    //                // Загружаем вершины
    //                GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
    //                GL.BufferData(BufferTarget.ArrayBuffer, vertices.Count * Vector4.SizeInBytes, vertices.ToArray(), BufferUsageHint.StaticDraw);

    //                // Загружаем индексы
    //                GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
    //                GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Count * sizeof(int), indices.ToArray(), BufferUsageHint.StaticDraw);

    //                // Настройка атрибутов вершин
    //                GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
    //                GL.EnableVertexAttribArray(0);

    //                GL.VertexAttribPointer(1, 1, VertexAttribPointerType.Float, false, 4 * sizeof(float), 3 * sizeof(float));
    //                GL.EnableVertexAttribArray(1);

    //                // Отрисовка
    //                GL.BindVertexArray(vao);
    //                GL.DrawElements(PrimitiveType.Triangles, indices.Count, DrawElementsType.UnsignedInt, 0);

    //                // Очистка ресурсов
    //                GL.BindVertexArray(0);
    //                GL.DeleteBuffer(vbo);
    //                GL.DeleteBuffer(ebo);
    //                GL.DeleteVertexArray(vao);
    //            }
    //        }

    //        public void Dispose()
    //        {
    //            if (_vao != 0)
    //                GL.DeleteVertexArray(_vao);
    //            if (_vbo != 0)
    //                GL.DeleteBuffer(_vbo);
    //            if (_ebo != 0)
    //                GL.DeleteBuffer(_ebo);
    //        }
    //    }








    //    public class MeshRender
    //    {
    //        MapData _mapData;

    //        //private List<Vector4> _vertices;
    //        private List<int> _indices;
    //        private int _vao;
    //        private int _vbo;
    //        private int _ebo;

    //        public Vector3 ModelCenter { get; set; }
    //        public Vector3 Min { get; set; }
    //        public Vector3 Max { get; set; }

    //        public float MinX { get; set; }
    //        public float MaxX { get; set; }
    //        public float MinY { get; set; }
    //        public float MaxY { get; set; }
    //        public float MinZ { get; set; }
    //        public float MaxZ { get; set; }

    //        private Shader _shader;

    //        // Список нормализованных коэффициентов
    //        List<float> normalizedCoefficients = new List<float>
    //                    {
    //                        1.0f, 0.931f, 0.861f, 0.792f, 0.723f, 0.653f,
    //                        0.584f, 0.515f, 0.445f, 0.376f, 0.307f, 0.237f,
    //                        0.168f, 0.0f
    //                    };

    //        // Список RGB-значений в диапазоне 0-1
    //        List<(float r, float g, float b)> colors = new List<(float r, float g, float b)>
    //                    {
    //                        (0.863f, 0.863f, 0.863f),
    //                        (0.800f, 0.749f, 0.706f),
    //                        (0.741f, 0.639f, 0.549f),
    //                        (0.682f, 0.525f, 0.392f),
    //                        (0.624f, 0.416f, 0.235f),
    //                        (0.682f, 0.490f, 0.294f),
    //                        (0.741f, 0.569f, 0.353f),
    //                        (0.800f, 0.647f, 0.412f),
    //                        (0.859f, 0.725f, 0.471f),
    //                        (0.922f, 0.804f, 0.533f),
    //                        (0.827f, 0.682f, 0.439f),
    //                        (0.737f, 0.565f, 0.349f),
    //                        (0.643f, 0.447f, 0.259f),
    //                        (0.553f, 0.329f, 0.169f)
    //                    };


    //        public MeshRender(MapData mapData)
    //        {
    //            _mapData = mapData;
    //            //List<Vector4> vertices = GenerateHeightMapVertices(elevationData);
    //            ModelCenter = CalculateModelCenter();

    //            _vao = GL.GenVertexArray();
    //            _vbo = GL.GenBuffer();

    //            GL.BindVertexArray(_vao);
    //            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
    //            GL.BufferData(BufferTarget.ArrayBuffer, _mapData.Vertices.Count * sizeof(float) * 4, _mapData.Vertices.ToArray(), BufferUsageHint.StaticDraw);

    //            //GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
    //            //GL.EnableVertexAttribArray(0);

    //            _indices = GenerateHeightMapIndices();
    //            _ebo = GL.GenBuffer();

    //            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
    //            GL.BufferData(BufferTarget.ElementArrayBuffer, _indices.Count * sizeof(int), _indices.ToArray(), BufferUsageHint.StaticDraw);

    //            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
    //            GL.EnableVertexAttribArray(0);

    //            GL.VertexAttribPointer(1, 1, VertexAttribPointerType.Float, false, 4 * sizeof(float), 3 * sizeof(float));
    //            GL.EnableVertexAttribArray(1);


    //            InitializeShader();
    //        }

    //        private void InitializeShader()
    //        {
    //            string vertexShaderSource = @"
    //#version 330 core
    //layout (location = 0) in vec3 aPosition;
    //layout (location = 1) in float aHeight;

    //out float Height;

    //uniform mat4 model;
    //uniform mat4 view;
    //uniform mat4 projection;

    //void main()
    //{
    //    gl_Position = projection * view * model * vec4(aPosition, 1.0);
    //    Height = aHeight;
    //}";

    //            string fragmentShaderSource = @"
    //#version 330 core

    //in float Height;

    //out vec4 FragColor;

    //uniform float heights[14];
    //uniform vec3 colors[14];

    //vec3 GetColorByHeight(float height)
    //{
    //    for (int i = 0; i < 13; ++i) {
    //        if (height >= heights[i + 1] && height <= heights[i]) {
    //            float t = (height - heights[i + 1]) / (heights[i] - heights[i + 1]);
    //            return mix(colors[i + 1], colors[i], t);
    //        }
    //    }
    //    return colors[13];
    //}

    //void main()
    //{
    //    vec3 color = GetColorByHeight(Height);
    //    FragColor = vec4(color, 1.0);
    //}";

    //            _shader = new Shader(vertexShaderSource, fragmentShaderSource, ShaderSourceMode.Code);

    //            var heightColorMap = CalculateHeights(MinY, MaxY);

    //            foreach (var h in heightColorMap)
    //                Console.WriteLine(h);

    //            _shader.Use();
    //            SetHeightColorData(heightColorMap);
    //        }

    //        public void SetHeightColorData(Dictionary<int, (float r, float g, float b)> heightColorMap)
    //        {
    //            // Получаем массивы для высот и цветов
    //            var heights = new List<float>();
    //            var colors = new List<float>();

    //            foreach (var entry in heightColorMap)
    //            {
    //                heights.Add(entry.Key);
    //                colors.Add(entry.Value.r);
    //                colors.Add(entry.Value.g);
    //                colors.Add(entry.Value.b);
    //            }

    //            // Преобразуем массивы в массивы float[]
    //            float[] heightArray = heights.ToArray();
    //            float[] colorArray = colors.ToArray();

    //            _shader.SetArray1("heights", heightArray);
    //            _shader.SetArray3("colors", colorArray);

    //            //// Создаем и отправляем в шейдер массив высот
    //            //int heightLocation = GL.GetUniformLocation(_shader, "heights");
    //            //GL.ProgramUniform1fv(_shader, heightLocation, heightArray.Length, heightArray);

    //            //// Создаем и отправляем в шейдер массив цветов
    //            //int colorLocation = GL.GetUniformLocation(_shader, "colors");
    //            //GL.ProgramUniform3fv(_shader, colorLocation, colorArray.Length / 3, colorArray);
    //        }

    //        /// <summary>
    //        /// Вычисляет список высот на основе коэффициентов и диапазона высот.
    //        /// </summary>
    //        /// <param name="minHeight">Минимальная высота.</param>
    //        /// <param name="maxHeight">Максимальная высота.</param>
    //        /// <returns>Список высот.</returns>
    //        public Dictionary<int, (float r, float g, float b)> CalculateHeights(float minHeight, float maxHeight)
    //        {
    //            var heightColorMap = new Dictionary<int, (float r, float g, float b)>();

    //            // Проверка на корректность диапазона
    //            if (maxHeight == minHeight)
    //                throw new ArgumentException("maxHeight и minHeight не должны быть равны.");

    //            // Вычисление высот и заполнение словаря
    //            for (int i = 0; i < normalizedCoefficients.Count; i++)
    //            {
    //                // Проверка на корректность коэффициента
    //                if (normalizedCoefficients[i] < 0.0f || normalizedCoefficients[i] > 1.0f)
    //                    throw new ArgumentException("Коэффициент должен находиться в диапазоне от 0.0 до 1.0.");

    //                int height = (int)(minHeight + normalizedCoefficients[i] * (maxHeight - minHeight));
    //                heightColorMap[height] = colors[i];
    //            }

    //            return heightColorMap;
    //        }

    //        public List<int> GenerateHeightMapIndices()
    //        {
    //            List<int> indices = new List<int>();

    //            for (int y = 0; y < _mapData.Rows - 1; y++)
    //            {
    //                for (int x = 0; x < _mapData.Cols - 1; x++)
    //                {
    //                    int topLeft = y * _mapData.Cols + x;
    //                    int topRight = topLeft + 1;
    //                    int bottomLeft = (y + 1) * _mapData.Cols + x;
    //                    int bottomRight = bottomLeft + 1;

    //                    // Первый треугольник
    //                    indices.Add(topLeft);
    //                    indices.Add(bottomLeft);
    //                    indices.Add(topRight);

    //                    // Второй треугольник
    //                    indices.Add(topRight);
    //                    indices.Add(bottomLeft);
    //                    indices.Add(bottomRight);
    //                }
    //            }

    //            return indices;
    //        }

    //        public Vector3 CalculateModelCenter()
    //        {
    //            MinX = float.MaxValue;
    //            MaxX = float.MinValue;
    //            MinY = float.MaxValue;
    //            MaxY = float.MinValue;
    //            MinZ = float.MaxValue;
    //            MaxZ = float.MinValue;

    //            foreach (var vertex in _mapData.Vertices)
    //            {
    //                float x = vertex.X;
    //                float y = vertex.Y;
    //                float z = vertex.Z;

    //                MinX = MathF.Min(MinX, x);
    //                MinY = MathF.Min(MinY, y);
    //                MinZ = MathF.Min(MinZ, z);

    //                MaxX = MathF.Max(MaxX, x);
    //                MaxY = MathF.Max(MaxY, y);
    //                MaxZ = MathF.Max(MaxZ, z);
    //            }

    //            Min = new Vector3(MinX, MinY, MinZ);
    //            Max = new Vector3(MaxX, MaxY, MaxZ);

    //            return new Vector3(
    //                (MinX + MaxX) / 2.0f,
    //                (MinY + MaxY) / 2.0f,
    //                (MinZ + MaxZ) / 2.0f
    //            );
    //        }

    //        public List<BoundingBox> SplitModelIntoChunks(List<Vector4> vertices, int chunkSize)
    //        {
    //            List<BoundingBox> boundingBoxes = new List<BoundingBox>();
    //            int totalChunks = (vertices.Count + chunkSize - 1) / chunkSize;

    //            for (int i = 0; i < totalChunks; i++)
    //            {
    //                int start = i * chunkSize;
    //                int end = Math.Min(start + chunkSize, vertices.Count);

    //                var chunkVertices = vertices.GetRange(start, end - start);
    //                boundingBoxes.Add(CalculateAABB(chunkVertices));
    //            }

    //            return boundingBoxes;
    //        }

    //        public BoundingBox CalculateAABB(List<Vector4> vertices)
    //        {
    //            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
    //            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

    //            foreach (var vertex in vertices)
    //            {
    //                min.X = MathF.Min(min.X, vertex.X);
    //                min.Y = MathF.Min(min.Y, vertex.Y);
    //                min.Z = MathF.Min(min.Z, vertex.Z);

    //                max.X = MathF.Max(max.X, vertex.X);
    //                max.Y = MathF.Max(max.Y, vertex.Y);
    //                max.Z = MathF.Max(max.Z, vertex.Z);
    //            }

    //            return new BoundingBox(min, max);
    //        }

    //        public List<int> FilterVisibleTriangles(List<Vector4> vertices, List<int> indices, Matrix4 vpMatrix)
    //        {
    //            List<int> visibleIndices = new List<int>();

    //            for (int i = 0; i < indices.Count; i += 3)
    //            {
    //                int index1 = indices[i];
    //                int index2 = indices[i + 1];
    //                int index3 = indices[i + 2];

    //                Vector4 v1 = vertices[index1];
    //                Vector4 v2 = vertices[index2];
    //                Vector4 v3 = vertices[index3];

    //                // Проверяем, находится ли хотя бы одна вершина внутри фрустума
    //                if (IsVertexInFrustum(v1, vpMatrix) ||
    //                    IsVertexInFrustum(v2, vpMatrix) ||
    //                    IsVertexInFrustum(v3, vpMatrix))
    //                {
    //                    // Добавляем треугольник в список видимых
    //                    visibleIndices.Add(index1);
    //                    visibleIndices.Add(index2);
    //                    visibleIndices.Add(index3);
    //                }
    //            }

    //            return visibleIndices;
    //        }

    //        public bool IsVertexInFrustum(Vector4 vertex, Matrix4 vpMatrix)
    //        {
    //            Vector4 clipSpacePosition = new Vector4(vertex.X, vertex.Y,vertex.Z, 1.0f) * vpMatrix;

    //            if (Math.Abs(clipSpacePosition.X) > clipSpacePosition.W ||
    //                Math.Abs(clipSpacePosition.Y) > clipSpacePosition.W ||
    //                Math.Abs(clipSpacePosition.Z) > clipSpacePosition.W)
    //            {
    //                return false; // Вершина вне фрустума
    //            }

    //            return true; // Вершина внутри фрустума
    //        }

    //        public void DrawMesh(Matrix4 view, Matrix4 projection, Matrix4 model)
    //        {
    //            //Matrix4 vpMatrix = view * projection;

    //            //// Отсечение невидимых треугольников
    //            //var visibleIndices = FilterVisibleTriangles(_mapData.Vertices, _indices, vpMatrix);

    //            //Console.WriteLine(visibleIndices.Count);


    //            //if (visibleIndices.Count == 0)
    //            //{
    //            //    Console.WriteLine("No visible triangles.");
    //            //    return;
    //            //}

    //            //// Обновляем буфер индексов
    //            //GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
    //            //GL.BufferData(BufferTarget.ElementArrayBuffer, visibleIndices.Count * sizeof(int), visibleIndices.ToArray(), BufferUsageHint.DynamicDraw);

    //            //// Рисуем видимые треугольники
    //            //GL.BindVertexArray(_vao);
    //            //GL.DrawElements(PrimitiveType.Triangles, visibleIndices.Count, DrawElementsType.UnsignedInt, 0);


    //            _shader.Use();
    //            _shader.SetMatrix4("view", view);
    //            _shader.SetMatrix4("projection", projection);
    //            _shader.SetMatrix4("model", model);

    //            GL.BindVertexArray(_vao);
    //            GL.DrawElements(PrimitiveType.Triangles, _indices.Count, DrawElementsType.UnsignedInt, 0);
    //        }

    //        public void Dispose()
    //        {
    //            if (_vao != 0)
    //                GL.DeleteVertexArray(_vao);
    //            if (_vbo != 0)
    //                GL.DeleteBuffer(_vbo);
    //            if (_ebo != 0)
    //                GL.DeleteBuffer(_ebo);
    //        }
    //    }
}
