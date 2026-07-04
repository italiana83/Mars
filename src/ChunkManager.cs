using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mars
{
    /// <summary>
    /// Хранит все чанки меша и возвращает только те, чей bbox пересекается с frustum камеры.
    /// </summary>
    public class ChunkManager
    {
        public List<Chunk> Chunks { get; set; }

        /// <summary>Инициализирует пустой список чанков меша.</summary>
        public ChunkManager()
        {
            Chunks = new List<Chunk>();
        }

        /// <summary>Возвращает чанки, чей bbox пересекается с frustum камеры.</summary>
        public List<Chunk> GetVisibleChunks(Frustum frustum)
        {
            List<Chunk> visibleChunks = new List<Chunk>();

            for (int i = 0; i < Chunks.Count; i++)
            {
                if (frustum.Intersects(Chunks[i].BoundingBoxe))
                {
                    visibleChunks.Add(Chunks[i]);
                }
            }

            return visibleChunks;
        }
    }

}
