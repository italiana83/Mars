using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mars
{
    public class ChunkManager
    {
        public List<Chunk> Chunks { get; set; }

        public ChunkManager()
        {
            Chunks = new List<Chunk>();
        }

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
