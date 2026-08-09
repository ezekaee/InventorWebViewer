using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace InventorWebViewer.Core
{
    /// <summary>Parse ASCII or binary STL into MeshGeom (triangle soup).</summary>
    public static class StlMeshReader
    {
        public static MeshGeom TryRead(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try
            {
                var bytes = File.ReadAllBytes(path);
                if (bytes.Length < 84) return null;

                // Binary STL: 80-byte header + uint32 tri count + 50 bytes/tri
                // ASCII starts with "solid"
                bool maybeAscii = bytes.Length > 5 &&
                    (bytes[0] == (byte)'s' || bytes[0] == (byte)'S') &&
                    bytes[1] == (byte)'o' && bytes[2] == (byte)'l' &&
                    bytes[3] == (byte)'i' && bytes[4] == (byte)'d';

                if (maybeAscii)
                {
                    // Heuristic: if declared triangle count matches binary size, prefer binary
                    uint triCount = BitConverter.ToUInt32(bytes, 80);
                    long expected = 84L + (long)triCount * 50L;
                    if (triCount > 0 && expected == bytes.Length)
                        return ReadBinary(bytes);
                    return ReadAscii(Encoding.ASCII.GetString(bytes));
                }

                return ReadBinary(bytes);
            }
            catch
            {
                return null;
            }
        }

        private static MeshGeom ReadBinary(byte[] bytes)
        {
            if (bytes.Length < 84) return null;
            uint triCount = BitConverter.ToUInt32(bytes, 80);
            long need = 84L + (long)triCount * 50L;
            if (need > bytes.Length) return null;

            var pos = new List<float>((int)triCount * 9);
            var nrm = new List<float>((int)triCount * 9);
            var idx = new List<int>((int)triCount * 3);
            int vi = 0;
            int o = 84;
            for (uint t = 0; t < triCount; t++)
            {
                float nx = BitConverter.ToSingle(bytes, o); o += 4;
                float ny = BitConverter.ToSingle(bytes, o); o += 4;
                float nz = BitConverter.ToSingle(bytes, o); o += 4;
                for (int v = 0; v < 3; v++)
                {
                    float x = BitConverter.ToSingle(bytes, o); o += 4;
                    float y = BitConverter.ToSingle(bytes, o); o += 4;
                    float z = BitConverter.ToSingle(bytes, o); o += 4;
                    pos.Add(x); pos.Add(y); pos.Add(z);
                    nrm.Add(nx); nrm.Add(ny); nrm.Add(nz);
                    idx.Add(vi++);
                }
                o += 2; // attribute byte count
            }
            if (idx.Count < 3) return null;
            return new MeshGeom { Positions = pos, Normals = nrm, Indices = idx };
        }

        private static MeshGeom ReadAscii(string text)
        {
            var pos = new List<float>();
            var nrm = new List<float>();
            var idx = new List<int>();
            float nx = 0, ny = 0, nz = 1;
            int vi = 0;
            using (var sr = new StringReader(text))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.StartsWith("facet normal", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 5)
                        {
                            float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out nx);
                            float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out ny);
                            float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out nz);
                        }
                    }
                    else if (line.StartsWith("vertex", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 4)
                        {
                            float x, y, z;
                            float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out x);
                            float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out y);
                            float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out z);
                            pos.Add(x); pos.Add(y); pos.Add(z);
                            nrm.Add(nx); nrm.Add(ny); nrm.Add(nz);
                            idx.Add(vi++);
                        }
                    }
                }
            }
            if (idx.Count < 3) return null;
            return new MeshGeom { Positions = pos, Normals = nrm, Indices = idx };
        }
    }
}
