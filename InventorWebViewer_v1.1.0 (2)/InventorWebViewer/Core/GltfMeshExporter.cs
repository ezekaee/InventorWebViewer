using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Inventor;
using IOFile = System.IO.File;

namespace InventorWebViewer.Core
{
    /// <summary>
    /// Builds a minimal glTF 2.0 binary (.glb) from Inventor SurfaceBody facets.
    /// Inventor VertexIndices are 1-based (first vertex = 1) per Autodesk docs.
    /// </summary>
    public static class GltfMeshExporter
    {
        public static bool TryExportDocument(Document doc, string glbPath, double chordTol, Action<string> log = null)
        {
            MeshGeom unused;
            return TryExportDocument(doc, glbPath, chordTol, out unused, log);
        }

        public static bool TryExportDocument(
            Document doc,
            string glbPath,
            double chordTol,
            out MeshGeom geom,
            Action<string> log = null)
        {
            geom = null;
            try
            {
                var positions = new List<float>(8192);
                var normals = new List<float>(8192);
                var indices = new List<int>(8192);

                if (!CollectDocumentFacets(doc, chordTol, positions, normals, indices, log))
                    return false;

                if (indices.Count < 3 || positions.Count < 9)
                    return false;

                SanitizeIndexedMesh(positions, normals, indices);

                if (indices.Count < 3)
                    return false;

                geom = new MeshGeom
                {
                    Positions = positions,
                    Normals = normals,
                    Indices = indices
                };

                var bytes = BuildGlb(positions, normals, indices);
                if (bytes == null || bytes.Length < 64)
                    return false;

                IOFile.WriteAllBytes(glbPath, bytes);
                return IOFile.Exists(glbPath) && new System.IO.FileInfo(glbPath).Length > 64;
            }
            catch (Exception ex)
            {
                log?.Invoke("GLB export failed: " + ex.Message);
                geom = null;
                return false;
            }
        }

        private static bool CollectDocumentFacets(
            Document doc,
            double chordTol,
            List<float> positions,
            List<float> normals,
            List<int> indices,
            Action<string> log)
        {
            object def = null;
            try
            {
                var part = doc as PartDocument;
                if (part != null) def = part.ComponentDefinition;
                else
                {
                    var asm = doc as AssemblyDocument;
                    if (asm != null) def = asm.ComponentDefinition;
                }
            }
            catch { }

            if (def == null) return false;

            object bodiesObj = null;
            try
            {
                var prop = def.GetType().GetProperty("SurfaceBodies");
                if (prop != null) bodiesObj = prop.GetValue(def, null);
            }
            catch { }

            if (!(bodiesObj is System.Collections.IEnumerable bodiesEnum))
                return false;

            double chord = Math.Max(0.001, chordTol);
            int bodyOk = 0;

            foreach (object bodyObj in bodiesEnum)
            {
                try
                {
                    if (!(bodyObj is SurfaceBody body)) continue;
                    if (AppendBody(body, chord, positions, normals, indices))
                        bodyOk++;
                }
                catch (Exception ex)
                {
                    log?.Invoke("Body facet skip: " + ex.Message);
                }
                finally
                {
                    // Release COM reference promptly to limit Inventor RAM growth on large assemblies
                    try
                    {
                        if (bodyObj != null && Marshal.IsComObject(bodyObj))
                            Marshal.FinalReleaseComObject(bodyObj);
                    }
                    catch { }
                }
            }

            return bodyOk > 0 && indices.Count >= 3;
        }

        private static bool AppendBody(
            SurfaceBody body,
            double chord,
            List<float> positions,
            List<float> normals,
            List<int> indices)
        {
            double[] verts = null;
            double[] norms = null;
            int[] inds = null;
            int vertexCount = 0;
            int facetCount = 0;

            if (!TryGetFacets(body, chord, out vertexCount, out facetCount, out verts, out norms, out inds))
                return false;
            if (verts == null || inds == null || facetCount <= 0)
                return false;

            int coordCount = verts.Length / 3;
            if (coordCount <= 0) return false;
            // Trust the actual array length over the reported count
            if (vertexCount <= 0 || vertexCount > coordCount)
                vertexCount = coordCount;

            // Inventor docs: "The first coordinate in the vertex coordinate list is index 1."
            // Detect safely: if any index == vertexCount → must be 1-based;
            // if min index is 0 → 0-based; otherwise default to 1-based (API contract).
            int minI = int.MaxValue, maxI = int.MinValue;
            int nIdx = Math.Min(inds.Length, facetCount * 3);
            for (int i = 0; i < nIdx; i++)
            {
                int v = inds[i];
                if (v < minI) minI = v;
                if (v > maxI) maxI = v;
            }
            bool oneBased;
            if (minI == 0 && maxI < vertexCount)
                oneBased = false;
            else if (maxI == vertexCount || minI >= 1)
                oneBased = true;
            else
                oneBased = true; // Autodesk default

            int baseVertex = positions.Count / 3;
            int vertsAdded = 0;

            for (int v = 0; v < vertexCount; v++)
            {
                int vi = v * 3;
                if (vi + 2 >= verts.Length) break;

                positions.Add((float)verts[vi]);
                positions.Add((float)verts[vi + 1]);
                positions.Add((float)verts[vi + 2]);

                if (norms != null && vi + 2 < norms.Length)
                {
                    float nx = (float)norms[vi];
                    float ny = (float)norms[vi + 1];
                    float nz = (float)norms[vi + 2];
                    // Normalize if needed
                    float len = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
                    if (len > 1e-12f) { nx /= len; ny /= len; nz /= len; }
                    else { nx = 0; ny = 0; nz = 1; }
                    normals.Add(nx);
                    normals.Add(ny);
                    normals.Add(nz);
                }
                else
                {
                    normals.Add(0f);
                    normals.Add(0f);
                    normals.Add(1f);
                }
                vertsAdded++;
            }

            if (vertsAdded == 0) return false;

            int addedTris = 0;
            for (int f = 0; f < facetCount; f++)
            {
                int bi = f * 3;
                if (bi + 2 >= inds.Length) break;

                int i0 = inds[bi];
                int i1 = inds[bi + 1];
                int i2 = inds[bi + 2];
                if (oneBased) { i0--; i1--; i2--; }

                if (i0 < 0 || i1 < 0 || i2 < 0) continue;
                if (i0 >= vertsAdded || i1 >= vertsAdded || i2 >= vertsAdded) continue;
                if (i0 == i1 || i1 == i2 || i0 == i2) continue; // degenerate

                indices.Add(baseVertex + i0);
                indices.Add(baseVertex + i1);
                indices.Add(baseVertex + i2);
                addedTris++;
            }

            return addedTris > 0;
        }

        private static void SanitizeIndexedMesh(List<float> positions, List<float> normals, List<int> indices)
        {
            int vc = positions.Count / 3;
            var clean = new List<int>(indices.Count);
            for (int i = 0; i + 2 < indices.Count; i += 3)
            {
                int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                if (a < 0 || b < 0 || c < 0) continue;
                if (a >= vc || b >= vc || c >= vc) continue;
                if (a == b || b == c || a == c) continue;
                clean.Add(a);
                clean.Add(b);
                clean.Add(c);
            }
            indices.Clear();
            indices.AddRange(clean);
        }

        // --- Facet interop ---

        private static bool TryGetFacets(
            SurfaceBody body,
            double chord,
            out int vertexCount,
            out int facetCount,
            out double[] vertices,
            out double[] normals,
            out int[] indices)
        {
            vertexCount = 0;
            facetCount = 0;
            vertices = null;
            normals = null;
            indices = null;

            try
            {
                int vc = 0, fc = 0;
                double[] verts = null, norms = null;
                int[] inds = null;
                body.CalculateFacets(chord, out vc, out fc, out verts, out norms, out inds);
                if (fc > 0 && verts != null && inds != null && verts.Length >= 9)
                {
                    vertexCount = vc > 0 ? vc : verts.Length / 3;
                    facetCount = fc;
                    vertices = verts;
                    normals = norms;
                    indices = inds;
                    return true;
                }
            }
            catch { }

            var t = body.GetType();
            if (InvokeFacetsMethod(body, t, "CalculateFacets", chord,
                    ref vertexCount, ref facetCount, ref vertices, ref normals, ref indices))
                return facetCount > 0 && vertices != null && indices != null;

            if (InvokeFacetsMethod(body, t, "GetExistingFacets", chord,
                    ref vertexCount, ref facetCount, ref vertices, ref normals, ref indices))
                return facetCount > 0 && vertices != null && indices != null;

            if (InvokeFacetsMethod(body, t, "GetExistingFacets", null,
                    ref vertexCount, ref facetCount, ref vertices, ref normals, ref indices))
                return facetCount > 0 && vertices != null && indices != null;

            // Per-face fallback — slower but works when body-level call fails
            try
            {
                if (TryFacetsPerFace(body, chord, out vertices, out normals, out indices, out vertexCount, out facetCount))
                    return true;
            }
            catch { }

            return false;
        }

        private static bool TryFacetsPerFace(
            SurfaceBody body,
            double chord,
            out double[] vertices,
            out double[] normals,
            out int[] indices,
            out int vertexCount,
            out int facetCount)
        {
            vertices = null;
            normals = null;
            indices = null;
            vertexCount = 0;
            facetCount = 0;

            var pos = new List<float>();
            var nrm = new List<float>();
            var idx = new List<int>();

            Faces faces = null;
            try { faces = body.Faces; } catch { return false; }
            if (faces == null || faces.Count == 0) return false;

            foreach (Face face in faces)
            {
                try
                {
                    int vc = 0, fc = 0;
                    double[] verts = null, norms = null;
                    int[] inds = null;
                    try
                    {
                        face.CalculateFacets(chord, out vc, out fc, out verts, out norms, out inds);
                    }
                    catch
                    {
                        var ft = face.GetType();
                        if (!InvokeFacetsMethod(face, ft, "CalculateFacets", chord,
                                ref vc, ref fc, ref verts, ref norms, ref inds))
                            continue;
                    }
                    if (verts == null || inds == null || fc <= 0) continue;

                    int coordCount = verts.Length / 3;
                    if (vc <= 0 || vc > coordCount) vc = coordCount;

                    int minI = int.MaxValue, maxI = int.MinValue;
                    int nIdx = Math.Min(inds.Length, fc * 3);
                    for (int i = 0; i < nIdx; i++)
                    {
                        if (inds[i] < minI) minI = inds[i];
                        if (inds[i] > maxI) maxI = inds[i];
                    }
                    bool oneBased = true;
                    if (minI == 0 && maxI < vc) oneBased = false;
                    else if (maxI == vc || minI >= 1) oneBased = true;

                    int baseV = pos.Count / 3;
                    int added = 0;
                    for (int v = 0; v < vc; v++)
                    {
                        int vi = v * 3;
                        if (vi + 2 >= verts.Length) break;
                        pos.Add((float)verts[vi]);
                        pos.Add((float)verts[vi + 1]);
                        pos.Add((float)verts[vi + 2]);
                        if (norms != null && vi + 2 < norms.Length)
                        {
                            nrm.Add((float)norms[vi]);
                            nrm.Add((float)norms[vi + 1]);
                            nrm.Add((float)norms[vi + 2]);
                        }
                        else { nrm.Add(0f); nrm.Add(0f); nrm.Add(1f); }
                        added++;
                    }
                    for (int f = 0; f < fc; f++)
                    {
                        int bi = f * 3;
                        if (bi + 2 >= inds.Length) break;
                        int i0 = inds[bi], i1 = inds[bi + 1], i2 = inds[bi + 2];
                        if (oneBased) { i0--; i1--; i2--; }
                        if (i0 < 0 || i1 < 0 || i2 < 0 || i0 >= added || i1 >= added || i2 >= added) continue;
                        idx.Add(baseV + i0);
                        idx.Add(baseV + i1);
                        idx.Add(baseV + i2);
                    }
                }
                catch { }
            }

            if (idx.Count < 3 || pos.Count < 9) return false;

            vertices = new double[pos.Count];
            normals = new double[nrm.Count];
            for (int i = 0; i < pos.Count; i++) vertices[i] = pos[i];
            for (int i = 0; i < nrm.Count; i++) normals[i] = nrm[i];
            indices = idx.ToArray();
            vertexCount = pos.Count / 3;
            facetCount = idx.Count / 3;
            return true;
        }


        private static bool InvokeFacetsMethod(
            object body,
            Type t,
            string methodName,
            double? tolerance,
            ref int vertexCount,
            ref int facetCount,
            ref double[] vertices,
            ref double[] normals,
            ref int[] indices)
        {
            try
            {
                foreach (var m in t.GetMethods())
                {
                    if (m.Name != methodName) continue;
                    var ps = m.GetParameters();
                    object[] args = null;

                    // Layouts seen in interop:
                    // CalculateFacets(tol, vc, fc, verts, norms, inds)           → 6
                    // CalculateFacets(tol, vc, fc, faceCount, verts, norms, inds) → 7
                    // GetExistingFacets(tol, vc, fc, verts, norms, inds)          → 6
                    // GetExistingFacets(vc, fc, verts, norms, inds)               → 5
                    if (tolerance.HasValue)
                    {
                        if (ps.Length == 7)
                            args = new object[] { tolerance.Value, 0, 0, 0, null, null, null };
                        else if (ps.Length == 6)
                            args = new object[] { tolerance.Value, 0, 0, null, null, null };
                        else
                            continue;
                    }
                    else
                    {
                        if (ps.Length == 6)
                            args = new object[] { 0.0, 0, 0, null, null, null };
                        else if (ps.Length == 5)
                            args = new object[] { 0, 0, null, null, null };
                        else
                            continue;
                    }

                    try { m.Invoke(body, args); }
                    catch { continue; }

                    int n = args.Length;
                    if (n < 5) continue;

                    var inds = ToIntArray(args[n - 1]);
                    var norms = args[n - 2] as double[];
                    var verts = args[n - 3] as double[];
                    if (verts == null || inds == null) continue;

                    int offset = tolerance.HasValue ? 1 : 0;
                    // When layout has no tolerance, offset 0 is vc
                    // When GetExistingFacets(tol,...) offset 1 is vc
                    if (!tolerance.HasValue && ps.Length == 6)
                        offset = 1; // first arg was tolerance-like double 0.0

                    int vc = ToInt(args[offset]);
                    int fc = ToInt(args[offset + 1]);
                    if (fc <= 0) fc = inds.Length / 3;
                    if (vc <= 0) vc = verts.Length / 3;

                    vertexCount = vc;
                    facetCount = fc;
                    vertices = verts;
                    normals = norms;
                    indices = inds;
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static int ToInt(object o)
        {
            if (o == null) return 0;
            try { return Convert.ToInt32(o, CultureInfo.InvariantCulture); }
            catch { return 0; }
        }

        private static int[] ToIntArray(object o)
        {
            if (o == null) return null;
            if (o is int[] ia) return ia;
            if (o is long[] la)
            {
                var r = new int[la.Length];
                for (int i = 0; i < la.Length; i++) r[i] = (int)la[i];
                return r;
            }
            if (o is short[] sa)
            {
                var r = new int[sa.Length];
                for (int i = 0; i < sa.Length; i++) r[i] = sa[i];
                return r;
            }
            if (o is Array arr && arr.Rank == 1)
            {
                var r = new int[arr.Length];
                for (int i = 0; i < arr.Length; i++)
                    r[i] = ToInt(arr.GetValue(i));
                return r;
            }
            return null;
        }

        /// <summary>Build a single-mesh GLB byte array.</summary>
        public static byte[] BuildGlb(List<float> positions, List<float> normals, List<int> indices)
        {
            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
            int vc = positions.Count / 3;
            for (int i = 0; i < vc; i++)
            {
                float x = positions[i * 3], y = positions[i * 3 + 1], z = positions[i * 3 + 2];
                if (x < minX) minX = x; if (y < minY) minY = y; if (z < minZ) minZ = z;
                if (x > maxX) maxX = x; if (y > maxY) maxY = y; if (z > maxZ) maxZ = z;
            }

            int posByteLen = positions.Count * 4;
            int normByteLen = normals.Count * 4;
            int idxByteLen = indices.Count * 4;
            int posPad = (4 - (posByteLen % 4)) % 4;
            int normPad = (4 - (normByteLen % 4)) % 4;
            int idxPad = (4 - (idxByteLen % 4)) % 4;

            int posOffset = 0;
            int normOffset = posByteLen + posPad;
            int idxOffset = normOffset + normByteLen + normPad;
            int totalBin = idxOffset + idxByteLen + idxPad;

            var bin = new byte[totalBin];
            Buffer.BlockCopy(ToFloatBytes(positions), 0, bin, posOffset, posByteLen);
            Buffer.BlockCopy(ToFloatBytes(normals), 0, bin, normOffset, normByteLen);
            Buffer.BlockCopy(ToIntBytes(indices), 0, bin, idxOffset, idxByteLen);

            var inv = CultureInfo.InvariantCulture;
            var json = new StringBuilder(768);
            json.Append("{");
            json.Append("\"asset\":{\"version\":\"2.0\",\"generator\":\"InventorWebViewer\"},");
            json.Append("\"scene\":0,");
            json.Append("\"scenes\":[{\"nodes\":[0]}],");
            json.Append("\"nodes\":[{\"mesh\":0}],");
            json.Append("\"meshes\":[{\"primitives\":[{\"attributes\":{\"POSITION\":0,\"NORMAL\":1},\"indices\":2,\"mode\":4}]}],");
            json.Append("\"accessors\":[");
            json.AppendFormat(inv,
                "{{\"bufferView\":0,\"componentType\":5126,\"count\":{0},\"type\":\"VEC3\",\"max\":[{1},{2},{3}],\"min\":[{4},{5},{6}]}},",
                vc,
                F(maxX), F(maxY), F(maxZ),
                F(minX), F(minY), F(minZ));
            json.AppendFormat(inv,
                "{{\"bufferView\":1,\"componentType\":5126,\"count\":{0},\"type\":\"VEC3\"}},",
                vc);
            json.AppendFormat(inv,
                "{{\"bufferView\":2,\"componentType\":5125,\"count\":{0},\"type\":\"SCALAR\"}}",
                indices.Count);
            json.Append("],");
            json.Append("\"bufferViews\":[");
            json.AppendFormat(inv, "{{\"buffer\":0,\"byteOffset\":{0},\"byteLength\":{1},\"target\":34962}},", posOffset, posByteLen);
            json.AppendFormat(inv, "{{\"buffer\":0,\"byteOffset\":{0},\"byteLength\":{1},\"target\":34962}},", normOffset, normByteLen);
            json.AppendFormat(inv, "{{\"buffer\":0,\"byteOffset\":{0},\"byteLength\":{1},\"target\":34963}}", idxOffset, idxByteLen);
            json.Append("],");
            json.AppendFormat(inv, "\"buffers\":[{{\"byteLength\":{0}}}]", totalBin);
            json.Append("}");

            var jsonBytes = Encoding.UTF8.GetBytes(json.ToString());
            int jsonPad = (4 - (jsonBytes.Length % 4)) % 4;
            int jsonChunkLen = jsonBytes.Length + jsonPad;
            int binChunkLen = totalBin;
            int totalLength = 12 + 8 + jsonChunkLen + 8 + binChunkLen;

            var glb = new byte[totalLength];
            int o = 0;
            WriteU32(glb, ref o, 0x46546C67); // glTF
            WriteU32(glb, ref o, 2);
            WriteU32(glb, ref o, (uint)totalLength);
            WriteU32(glb, ref o, (uint)jsonChunkLen);
            WriteU32(glb, ref o, 0x4E4F534A); // JSON
            Buffer.BlockCopy(jsonBytes, 0, glb, o, jsonBytes.Length);
            o += jsonBytes.Length;
            for (int i = 0; i < jsonPad; i++) glb[o++] = 0x20;
            WriteU32(glb, ref o, (uint)binChunkLen);
            WriteU32(glb, ref o, 0x004E4942); // BIN
            Buffer.BlockCopy(bin, 0, glb, o, totalBin);

            return glb;
        }

        private static string F(float v)
        {
            if (float.IsNaN(v) || float.IsInfinity(v)) return "0";
            return v.ToString("G9", CultureInfo.InvariantCulture);
        }

        private static byte[] ToFloatBytes(List<float> list)
        {
            var bytes = new byte[list.Count * 4];
            for (int i = 0; i < list.Count; i++)
            {
                var b = BitConverter.GetBytes(list[i]);
                if (!BitConverter.IsLittleEndian) Array.Reverse(b);
                Buffer.BlockCopy(b, 0, bytes, i * 4, 4);
            }
            return bytes;
        }

        private static byte[] ToIntBytes(List<int> list)
        {
            var bytes = new byte[list.Count * 4];
            for (int i = 0; i < list.Count; i++)
            {
                var b = BitConverter.GetBytes(unchecked((uint)list[i]));
                if (!BitConverter.IsLittleEndian) Array.Reverse(b);
                Buffer.BlockCopy(b, 0, bytes, i * 4, 4);
            }
            return bytes;
        }

        private static void WriteU32(byte[] buf, ref int offset, uint value)
        {
            buf[offset++] = (byte)(value & 0xFF);
            buf[offset++] = (byte)((value >> 8) & 0xFF);
            buf[offset++] = (byte)((value >> 16) & 0xFF);
            buf[offset++] = (byte)((value >> 24) & 0xFF);
        }
    }
}
