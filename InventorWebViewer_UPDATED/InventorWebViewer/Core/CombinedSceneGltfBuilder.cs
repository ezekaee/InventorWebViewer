using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace InventorWebViewer.Core
{
    /// <summary>
    /// Builds ONE glTF 2.0 binary (.glb) for the entire assembly:
    /// hierarchy + shared geometries + per-node PBR materials.
    /// This single file can be base64-embedded in index.html (no fetch / no server).
    /// </summary>
    public static class CombinedSceneGltfBuilder
    {
        public static byte[] Build(
            SceneExport scene,
            Dictionary<string, MeshGeom> geomByMeshFile,
            Action<string> log = null)
        {
            if (scene == null || scene.Roots == null || scene.Roots.Count == 0)
                return null;
            if (geomByMeshFile == null) geomByMeshFile = new Dictionary<string, MeshGeom>();

            // --- 1) Assign unique geometry slots ---
            var geomKeys = new List<string>();
            var geomIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in geomByMeshFile)
            {
                if (kv.Value == null || !kv.Value.IsValid) continue;
                if (geomIndex.ContainsKey(kv.Key)) continue;
                geomIndex[kv.Key] = geomKeys.Count;
                geomKeys.Add(kv.Key);
            }

            if (geomKeys.Count == 0)
            {
                log?.Invoke("Combined GLB: no valid geometries");
                return null;
            }

            // --- 2) Binary buffer layout: for each geom → POS | NORM | IDX (padded) ---
            var bin = new List<byte>(1024 * 1024);
            var posView = new int[geomKeys.Count];   // byte offset
            var posLen = new int[geomKeys.Count];
            var normView = new int[geomKeys.Count];
            var normLen = new int[geomKeys.Count];
            var idxView = new int[geomKeys.Count];
            var idxLen = new int[geomKeys.Count];
            var vCount = new int[geomKeys.Count];
            var iCount = new int[geomKeys.Count];
            var minMax = new float[geomKeys.Count, 6]; // minxyz maxxyz

            for (int g = 0; g < geomKeys.Count; g++)
            {
                var geom = geomByMeshFile[geomKeys[g]];
                vCount[g] = geom.VertexCount;
                iCount[g] = geom.Indices.Count;

                float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
                float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
                for (int v = 0; v < vCount[g]; v++)
                {
                    float x = geom.Positions[v * 3], y = geom.Positions[v * 3 + 1], z = geom.Positions[v * 3 + 2];
                    if (x < minX) minX = x; if (y < minY) minY = y; if (z < minZ) minZ = z;
                    if (x > maxX) maxX = x; if (y > maxY) maxY = y; if (z > maxZ) maxZ = z;
                }
                minMax[g, 0] = minX; minMax[g, 1] = minY; minMax[g, 2] = minZ;
                minMax[g, 3] = maxX; minMax[g, 4] = maxY; minMax[g, 5] = maxZ;

                // Quantize positions (~0.01 mm) to shrink binary slightly and stabilize
                Quantize(geom.Positions, 1000f);
                Quantize(geom.Normals, 1000f);
                posView[g] = bin.Count;
                posLen[g] = geom.Positions.Count * 4;
                AppendFloats(bin, geom.Positions);
                Pad4(bin);

                normView[g] = bin.Count;
                normLen[g] = geom.Normals.Count * 4;
                AppendFloats(bin, geom.Normals);
                Pad4(bin);

                idxView[g] = bin.Count;
                idxLen[g] = geom.Indices.Count * 4;
                AppendInts(bin, geom.Indices);
                Pad4(bin);
            }

            int totalBin = bin.Count;

            // --- 3) Walk tree → materials, meshes, nodes ---
            var materialsJson = new List<string>();
            var meshesJson = new List<string>();
            var nodesJson = new List<string>();
            // accessor layout: for each geom: posAcc, normAcc, idxAcc  → 3 * geomCount
            // bufferViews: same

            int NextMat(SceneNode n)
            {
                double r = 0.72, g = 0.76, b = 0.82, a = 1.0;
                if (n.Color != null && n.Color.Length >= 3)
                {
                    r = Clamp01(n.Color[0]); g = Clamp01(n.Color[1]); b = Clamp01(n.Color[2]);
                }
                a = Clamp01(n.Opacity);
                double metal = Clamp01(n.Metalness);
                double rough = Clamp01(n.Roughness);
                var inv = CultureInfo.InvariantCulture;
                materialsJson.Add(string.Format(inv,
                    "{{\"name\":{0},\"pbrMetallicRoughness\":{{\"baseColorFactor\":[{1},{2},{3},{4}],\"metallicFactor\":{5},\"roughnessFactor\":{6}}},\"doubleSided\":true{7}}}",
                    Q(n.Name ?? "part"),
                    F(r), F(g), F(b), F(a),
                    F(metal), F(rough),
                    a < 0.999 ? ",\"alphaMode\":\"BLEND\"" : ""));
                return materialsJson.Count - 1;
            }

            int NextMesh(int geomSlot, int matIndex)
            {
                // accessors: geomSlot*3 + 0 pos, +1 norm, +2 idx
                int posAcc = geomSlot * 3;
                int normAcc = geomSlot * 3 + 1;
                int idxAcc = geomSlot * 3 + 2;
                meshesJson.Add(
                    "{\"primitives\":[{\"attributes\":{\"POSITION\":" + posAcc +
                    ",\"NORMAL\":" + normAcc + "},\"indices\":" + idxAcc +
                    ",\"material\":" + matIndex + ",\"mode\":4}]}");
                return meshesJson.Count - 1;
            }

            // Recursively emit nodes; returns node index
            int EmitNode(SceneNode n)
            {
                int meshIdx = -1;
                if (!string.IsNullOrEmpty(n.MeshFile) && geomIndex.TryGetValue(n.MeshFile, out int gSlot))
                {
                    int mat = NextMat(n);
                    meshIdx = NextMesh(gSlot, mat);
                }

                var childIndices = new List<int>();
                if (n.Children != null)
                {
                    foreach (var ch in n.Children)
                    {
                        if (ch == null) continue;
                        childIndices.Add(EmitNode(ch));
                    }
                }

                var inv = CultureInfo.InvariantCulture;
                var sb = new StringBuilder();
                sb.Append("{\"name\":").Append(Q(n.Name ?? n.Id ?? "node"));
                if (meshIdx >= 0)
                    sb.Append(",\"mesh\":").Append(meshIdx);
                if (n.Matrix != null && n.Matrix.Length == 16)
                {
                    sb.Append(",\"matrix\":[");
                    for (int i = 0; i < 16; i++)
                    {
                        if (i > 0) sb.Append(',');
                        sb.Append(F(n.Matrix[i]));
                    }
                    sb.Append(']');
                }
                if (childIndices.Count > 0)
                {
                    sb.Append(",\"children\":[");
                    for (int i = 0; i < childIndices.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        sb.Append(childIndices[i]);
                    }
                    sb.Append(']');
                }
                // extras for design tree / visibility
                sb.Append(",\"extras\":{\"id\":").Append(Q(n.Id ?? "")).Append(",\"type\":").Append(Q(n.Type ?? ""));
                sb.Append(",\"visible\":").Append(n.Visible ? "true" : "false").Append('}');
                sb.Append('}');
                nodesJson.Add(sb.ToString());
                return nodesJson.Count - 1;
            }

            var rootNodeIndices = new List<int>();
            foreach (var root in scene.Roots)
            {
                if (root == null) continue;
                rootNodeIndices.Add(EmitNode(root));
            }

            // --- 4) JSON document ---
            var invC = CultureInfo.InvariantCulture;
            var json = new StringBuilder(4096);
            json.Append("{\"asset\":{\"version\":\"2.0\",\"generator\":\"InventorWebViewer\"},");
            json.Append("\"scene\":0,");
            json.Append("\"scenes\":[{\"name\":").Append(Q(scene.AssemblyName ?? "Assembly")).Append(",\"nodes\":[");
            for (int i = 0; i < rootNodeIndices.Count; i++)
            {
                if (i > 0) json.Append(',');
                json.Append(rootNodeIndices[i]);
            }
            json.Append("]}],");

            // nodes
            json.Append("\"nodes\":[");
            for (int i = 0; i < nodesJson.Count; i++)
            {
                if (i > 0) json.Append(',');
                json.Append(nodesJson[i]);
            }
            json.Append("],");

            // meshes
            json.Append("\"meshes\":[");
            for (int i = 0; i < meshesJson.Count; i++)
            {
                if (i > 0) json.Append(',');
                json.Append(meshesJson[i]);
            }
            json.Append("],");

            // materials
            json.Append("\"materials\":[");
            for (int i = 0; i < materialsJson.Count; i++)
            {
                if (i > 0) json.Append(',');
                json.Append(materialsJson[i]);
            }
            json.Append("],");

            // accessors (3 per geom)
            json.Append("\"accessors\":[");
            for (int g = 0; g < geomKeys.Count; g++)
            {
                if (g > 0) json.Append(',');
                // POSITION
                json.AppendFormat(invC,
                    "{{\"bufferView\":{0},\"componentType\":5126,\"count\":{1},\"type\":\"VEC3\",\"max\":[{2},{3},{4}],\"min\":[{5},{6},{7}]}},",
                    g * 3, vCount[g],
                    F(minMax[g, 3]), F(minMax[g, 4]), F(minMax[g, 5]),
                    F(minMax[g, 0]), F(minMax[g, 1]), F(minMax[g, 2]));
                // NORMAL
                json.AppendFormat(invC,
                    "{{\"bufferView\":{0},\"componentType\":5126,\"count\":{1},\"type\":\"VEC3\"}},",
                    g * 3 + 1, vCount[g]);
                // INDICES
                json.AppendFormat(invC,
                    "{{\"bufferView\":{0},\"componentType\":5125,\"count\":{1},\"type\":\"SCALAR\"}}",
                    g * 3 + 2, iCount[g]);
            }
            json.Append("],");

            // bufferViews
            json.Append("\"bufferViews\":[");
            for (int g = 0; g < geomKeys.Count; g++)
            {
                if (g > 0) json.Append(',');
                json.AppendFormat(invC, "{{\"buffer\":0,\"byteOffset\":{0},\"byteLength\":{1},\"target\":34962}},", posView[g], posLen[g]);
                json.AppendFormat(invC, "{{\"buffer\":0,\"byteOffset\":{0},\"byteLength\":{1},\"target\":34962}},", normView[g], normLen[g]);
                json.AppendFormat(invC, "{{\"buffer\":0,\"byteOffset\":{0},\"byteLength\":{1},\"target\":34963}}", idxView[g], idxLen[g]);
            }
            json.Append("],");

            json.AppendFormat(invC, "\"buffers\":[{{\"byteLength\":{0}}}]}}", totalBin);

            // --- 5) Pack GLB ---
            var jsonBytes = Encoding.UTF8.GetBytes(json.ToString());
            int jsonPad = (4 - (jsonBytes.Length % 4)) % 4;
            int jsonChunkLen = jsonBytes.Length + jsonPad;
            int binChunkLen = totalBin;
            int totalLength = 12 + 8 + jsonChunkLen + 8 + binChunkLen;

            var glb = new byte[totalLength];
            int o = 0;
            WriteU32(glb, ref o, 0x46546C67);
            WriteU32(glb, ref o, 2);
            WriteU32(glb, ref o, (uint)totalLength);
            WriteU32(glb, ref o, (uint)jsonChunkLen);
            WriteU32(glb, ref o, 0x4E4F534A);
            Buffer.BlockCopy(jsonBytes, 0, glb, o, jsonBytes.Length);
            o += jsonBytes.Length;
            for (int i = 0; i < jsonPad; i++) glb[o++] = 0x20;
            WriteU32(glb, ref o, (uint)binChunkLen);
            WriteU32(glb, ref o, 0x004E4942);
            var binArr = bin.ToArray();
            Buffer.BlockCopy(binArr, 0, glb, o, totalBin);

            log?.Invoke("Combined GLB: " + geomKeys.Count + " unique meshes, " + nodesJson.Count + " nodes, " +
                        (totalLength / 1024) + " KB");
            return glb;
        }

        private static void Quantize(List<float> vals, float scale)
        {
            if (vals == null) return;
            for (int i = 0; i < vals.Count; i++)
            {
                float v = vals[i];
                if (float.IsNaN(v) || float.IsInfinity(v)) { vals[i] = 0; continue; }
                vals[i] = (float)Math.Round(v * scale) / scale;
            }
        }

        private static void AppendFloats(List<byte> bin, List<float> vals)
        {
            for (int i = 0; i < vals.Count; i++)
            {
                var b = BitConverter.GetBytes(vals[i]);
                if (!BitConverter.IsLittleEndian) Array.Reverse(b);
                bin.Add(b[0]); bin.Add(b[1]); bin.Add(b[2]); bin.Add(b[3]);
            }
        }

        private static void AppendInts(List<byte> bin, List<int> vals)
        {
            for (int i = 0; i < vals.Count; i++)
            {
                var b = BitConverter.GetBytes(unchecked((uint)vals[i]));
                if (!BitConverter.IsLittleEndian) Array.Reverse(b);
                bin.Add(b[0]); bin.Add(b[1]); bin.Add(b[2]); bin.Add(b[3]);
            }
        }

        private static void Pad4(List<byte> bin)
        {
            int pad = (4 - (bin.Count % 4)) % 4;
            for (int i = 0; i < pad; i++) bin.Add(0);
        }

        private static void WriteU32(byte[] buf, ref int offset, uint value)
        {
            buf[offset++] = (byte)(value & 0xFF);
            buf[offset++] = (byte)((value >> 8) & 0xFF);
            buf[offset++] = (byte)((value >> 16) & 0xFF);
            buf[offset++] = (byte)((value >> 24) & 0xFF);
        }

        private static double Clamp01(double v)
        {
            if (v < 0) return 0;
            if (v > 1) return 1;
            return v;
        }

        private static string F(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "0";
            return v.ToString("G9", CultureInfo.InvariantCulture);
        }

        private static string Q(string s)
        {
            if (s == null) return "\"\"";
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", "") + "\"";
        }
    }
}
