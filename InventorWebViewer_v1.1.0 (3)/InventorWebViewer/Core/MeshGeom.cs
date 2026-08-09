using System.Collections.Generic;

namespace InventorWebViewer.Core
{
    /// <summary>In-memory triangle mesh used to build a combined scene GLB.</summary>
    public class MeshGeom
    {
        public List<float> Positions { get; set; } = new List<float>();
        public List<float> Normals { get; set; } = new List<float>();
        public List<int> Indices { get; set; } = new List<int>();

        public int VertexCount => Positions != null ? Positions.Count / 3 : 0;
        public int TriangleCount => Indices != null ? Indices.Count / 3 : 0;
        public bool IsValid => VertexCount >= 3 && TriangleCount >= 1;
    }
}
