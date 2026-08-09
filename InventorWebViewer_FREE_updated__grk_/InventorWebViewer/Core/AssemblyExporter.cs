using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Inventor;
using Env = System.Environment;
using IOPath = System.IO.Path;
using IODirectory = System.IO.Directory;
using IOFile = System.IO.File;

namespace InventorWebViewer.Core
{
    /// <summary>
    /// Walks the assembly tree, tessellates unique parts to STL, builds scene JSON + HTML viewer package.
    /// </summary>
    public class AssemblyExporter
    {
        private readonly Inventor.Application _invApp;
        private readonly AppSettings _settings;
        private int _nodeCounter;
        private readonly Dictionary<string, string> _meshCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _textureCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        /// <summary>Relative mesh path → in-memory geometry for combined scene GLB.</summary>
        private readonly Dictionary<string, MeshGeom> _geomByMeshFile = new Dictionary<string, MeshGeom>(StringComparer.OrdinalIgnoreCase);
        private string _texturesDir;
        private bool _anyTexture;
        private Action<int, string> _progress;
        private Action<string> _log;

        public AssemblyExporter(Inventor.Application invApp, AppSettings settings)
        {
            _invApp = invApp;
            _settings = settings ?? AppSettings.Load();
        }

        public string Export(AssemblyDocument topAsm, ExportOptions options, Action<int, string> progress = null, Action<string> log = null)
        {
            _progress = progress;
            _log = log;
            _nodeCounter = 0;
            _meshCache.Clear();
            _textureCache.Clear();
            _geomByMeshFile.Clear();
            _anyTexture = false;

            if (topAsm == null) throw new ArgumentNullException(nameof(topAsm));
            if (string.IsNullOrWhiteSpace(options?.OutputFolder))
                throw new ArgumentException("Output folder required.");

            var outDir = options.OutputFolder;
            var meshesDir = IOPath.Combine(outDir, "meshes");
            _texturesDir = IOPath.Combine(outDir, "textures");
            IODirectory.CreateDirectory(outDir);
            IODirectory.CreateDirectory(meshesDir);
            if (options.ExportTextures)
                IODirectory.CreateDirectory(_texturesDir);

            Report(5, "Building design tree + colors/textures...");
            var scene = new SceneExport
            {
                AssemblyName = IOPath.GetFileNameWithoutExtension(topAsm.FullFileName ?? "Assembly"),
                ProjectRoot = IOPath.GetDirectoryName(topAsm.FullFileName ?? ""),
                UpAxis = options.UpAxis ?? "Z",
                ExportTimeUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
            };

            var rootNode = BuildNodeFromDocument((Document)topAsm, null, meshesDir, options);
            if (rootNode != null)
                scene.Roots.Add(rootNode);

            scene.MeshFiles = _meshCache.Values
                .Where(v => !string.IsNullOrEmpty(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            scene.HasTextures = _anyTexture;

            // Nuclear fallback: if no part mesh produced, export whole assembly as one mesh
            if (_geomByMeshFile.Count == 0)
            {
                _log?.Invoke("No part meshes — exporting entire assembly as single mesh via STL…");
                try
                {
                    var wholeRel = "meshes/_assembly_whole.glb";
                    var wholePath = IOPath.Combine(meshesDir, "_assembly_whole.glb");
                    var geom = TryExportViaStlTranslator((Document)topAsm, wholePath, meshesDir, "_assembly_whole");
                    if (geom != null && geom.IsValid)
                    {
                        _geomByMeshFile[wholeRel] = geom;
                        if (scene.Roots.Count > 0)
                            scene.Roots[0].MeshFile = wholeRel;
                        else
                        {
                            scene.Roots.Add(new SceneNode
                            {
                                Id = "n_root",
                                Name = scene.AssemblyName ?? "Assembly",
                                Type = "Assembly",
                                MeshFile = wholeRel,
                                Visible = true,
                                Color = new double[] { 0.72, 0.76, 0.82 }
                            });
                        }
                        scene.MeshFiles = new List<string> { wholeRel };
                        _log?.Invoke("Whole-assembly mesh OK — " + geom.TriangleCount + " tris");
                    }
                    else
                        _log?.Invoke("Whole-assembly STL export also FAILED");
                }
                catch (Exception ex)
                {
                    _log?.Invoke("Whole-assembly fallback error: " + ex.Message);
                }
            }

            // Keep only geometries referenced by the scene tree (drop unused local templates)
            {
                var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                void Collect(SceneNode n)
                {
                    if (n == null) return;
                    if (!string.IsNullOrEmpty(n.MeshFile)) used.Add(n.MeshFile);
                    if (n.Children == null) return;
                    foreach (var c in n.Children) Collect(c);
                }
                foreach (var r in scene.Roots) Collect(r);
                var drop = _geomByMeshFile.Keys.Where(k => !used.Contains(k)).ToList();
                foreach (var k in drop) _geomByMeshFile.Remove(k);
                scene.MeshFiles = used.ToList();
                _log?.Invoke("Geometries kept for GLB: " + _geomByMeshFile.Count + " (dropped " + drop.Count + " local templates)");
            }

            Report(80, "Preparing geometry cache (" + _geomByMeshFile.Count + " in memory)...");
            EnsureGeometryCacheComplete(outDir, scene);

            Report(85, "Building combined scene GLB...");
            byte[] sceneGlb = null;
            try
            {
                _log?.Invoke("Geometry cache entries: " + _geomByMeshFile.Count +
                             " | MeshFiles: " + (scene.MeshFiles != null ? scene.MeshFiles.Count : 0));
                sceneGlb = CombinedSceneGltfBuilder.Build(scene, _geomByMeshFile, _log);
                if (sceneGlb != null && sceneGlb.Length > 64)
                {
                    IOFile.WriteAllBytes(IOPath.Combine(outDir, "scene.glb"), sceneGlb);
                    _log?.Invoke("scene.glb written (" + (sceneGlb.Length / 1024) + " KB)");
                }
                else
                {
                    _log?.Invoke("ERROR: Combined GLB is empty — check tessellation log above");
                }
            }
            catch (OutOfMemoryException)
            {
                _log?.Invoke("Out of memory building combined GLB");
                try { GC.Collect(); } catch { }
                sceneGlb = null;
            }
            catch (Exception ex)
            {
                _log?.Invoke("Combined GLB failed: " + ex.GetType().Name + ": " + ex.Message);
                if (ex.InnerException != null)
                    _log?.Invoke("  inner: " + ex.InnerException.Message);
                sceneGlb = null;
            }

            // Free per-part geometry RAM before embedding
            _geomByMeshFile.Clear();
            try { GC.Collect(1, GCCollectionMode.Optimized); } catch { }

            // Remove intermediate meshes/ folder — data is already inside scene.glb (saves ~50% disk)
            try
            {
                if (IODirectory.Exists(meshesDir))
                {
                    IODirectory.Delete(meshesDir, true);
                    _log?.Invoke("Removed temporary meshes/ folder (geometry is in scene.glb)");
                }
            }
            catch (Exception ex)
            {
                _log?.Invoke("Could not delete meshes/: " + ex.Message);
            }

            Report(92, "Writing scene.json (metadata)...");
            scene.MeshData = null;
            scene.TextureData = null;
            var sceneJson = SerializeScene(scene);
            IOFile.WriteAllText(IOPath.Combine(outDir, "scene.json"), sceneJson, Encoding.UTF8);

            Report(95, "Generating HTML viewer...");
            if (sceneGlb == null || sceneGlb.Length < 64)
            {
                _log?.Invoke("WARNING: No geometry produced.");
            }
            else
            {
                double mb = sceneGlb.Length / (1024.0 * 1024.0);
                _log?.Invoke(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "scene.glb size: {0:0.0} MB", mb));
                if (mb > 50)
                    _log?.Invoke("TIP: File is large for email. Increase Mesh quality value (e.g. 0.5 or 1.0) and re-export.");
            }

            // Only embed base64 for small models (keeps email-friendly). Large models: scene.glb only.
            HtmlViewerGenerator.WritePackage(outDir, scene, _settings, sceneJson, null, sceneGlb);

            var b64Path = IOPath.Combine(outDir, "scene_b64.js");
            if (IOFile.Exists(b64Path))
                _log?.Invoke("Embedded viewer data: scene_b64.js (" + (new System.IO.FileInfo(b64Path).Length / 1024) + " KB)");
            else if (sceneGlb != null && sceneGlb.Length > 64)
                _log?.Invoke("WARNING: scene_b64.js was NOT written — open may fail offline. Check free disk/RAM.");
            else
                _log?.Invoke("WARNING: No scene.glb produced — check mesh/tessellation messages above.");

            Report(100, "Done");
            _log?.Invoke("Exported to: " + outDir);
            _log?.Invoke("Unique part meshes: " + scene.MeshFiles.Count);
            if (sceneGlb != null && sceneGlb.Length > 64)
            {
                _log?.Invoke("Output: index.html + scene.glb (+ scene_b64.js if model is small)");
                _log?.Invoke("For email: zip scene.glb only, or raise mesh tolerance for a smaller file.");
            }
            return outDir;
        }

        /// <summary>
        /// Fill missing MeshGeom entries so CombinedSceneGltfBuilder always has data.
        /// </summary>
        private void EnsureGeometryCacheComplete(string outDir, SceneExport scene)
        {
            var needed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void Walk(SceneNode n)
            {
                if (n == null) return;
                if (!string.IsNullOrEmpty(n.MeshFile)) needed.Add(n.MeshFile);
                if (n.Children == null) return;
                foreach (var c in n.Children) Walk(c);
            }
            if (scene.Roots != null)
                foreach (var r in scene.Roots) Walk(r);

            foreach (var rel in needed)
            {
                if (_geomByMeshFile.ContainsKey(rel)) continue;
                try
                {
                    var full = IOPath.Combine(outDir, rel.Replace('/', IOPath.DirectorySeparatorChar));
                    if (!IOFile.Exists(full))
                    {
                        _log?.Invoke("Missing mesh file on disk: " + rel);
                        continue;
                    }
                    // Last resort: cannot easily re-parse arbitrary GLB; log it
                    _log?.Invoke("Geometry not in RAM for " + rel + " (file exists, " +
                                 (new System.IO.FileInfo(full).Length / 1024) + " KB) — re-tessellate may have failed silently");
                }
                catch { }
            }
        }


        private SceneNode BuildNodeFromDocument(Document doc, ComponentOccurrence occ, string meshesDir, ExportOptions options)
        {
            if (doc == null || string.IsNullOrEmpty(doc.FullFileName)) return null;

            // Top-level assembly: place every leaf part with its transform relative to THIS assembly.
            // AllLeafOccurrences.Transformation is cumulative w.r.t. the assembly that owns Occurrences —
            // this avoids broken nested relative matrices that scramble the model.
            if (occ == null && doc.DocumentType == DocumentTypeEnum.kAssemblyDocumentObject)
            {
                return BuildAssemblyFromLeafOccurrences((AssemblyDocument)doc, meshesDir, options);
            }

            var node = new SceneNode
            {
                Id = "n" + (++_nodeCounter),
                Name = IOPath.GetFileNameWithoutExtension(doc.FullFileName),
                SourcePath = doc.FullFileName,
                Visible = true,
                Color = new double[] { 0.72, 0.76, 0.82 },
                Matrix = GetOccurrenceMatrix(occ)
            };

            try
            {
                if (occ != null && occ.Suppressed)
                {
                    node.Visible = false;
                    if (!options.IncludeHidden) return null;
                }
            }
            catch { }

            if (doc.DocumentType == DocumentTypeEnum.kAssemblyDocumentObject)
            {
                node.Type = "Assembly";
                // Nested assembly node: children handled by parent leaf walk when top-level.
                // If we still get here (e.g. single sub-assembly export), use definition occurrences.
                try
                {
                    var asm = doc as AssemblyDocument;
                    if (asm != null)
                    {
                        foreach (ComponentOccurrence child in asm.ComponentDefinition.Occurrences)
                        {
                            try
                            {
                                if (child.Suppressed && !options.IncludeHidden) continue;
                                Document childDoc = null;
                                try { childDoc = child.Definition?.Document as Document; } catch { }
                                if (childDoc == null) continue;
                                var childNode = BuildNodeFromDocument(childDoc, child, meshesDir, options);
                                if (childNode != null)
                                    node.Children.Add(childNode);
                            }
                            catch (Exception ex)
                            {
                                _log?.Invoke("Skip occurrence: " + ex.Message);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _log?.Invoke("Assembly walk error: " + ex.Message);
                }
            }
            else
            {
                node.Type = "Part";
                Report(Math.Min(80, 10 + _nodeCounter), "Part: " + node.Name);
                try
                {
                    node.MeshFile = EnsureMesh(doc, meshesDir, options.ChordTolerance);
                    ApplyAppearance(doc, occ, node, options);
                }
                catch (Exception ex)
                {
                    _log?.Invoke("Mesh/appearance failed for " + node.Name + ": " + ex.Message);
                }
            }

            return node;
        }



        /// <summary>
        /// Flat export: every leaf under root with TOP-assembly transform.
        /// Inventor proxies from AllLeafOccurrences already report Transformation in the
        /// top assembly coordinate system — do NOT parent*child (that explodes the model).
        /// Units stay in Inventor cm for BOTH mesh and matrix (consistent; Three.js is unitless).
        /// </summary>
        private SceneNode BuildAssemblyFromLeafOccurrences(AssemblyDocument topAsm, string meshesDir, ExportOptions options)
        {
            var root = new SceneNode
            {
                Id = "n" + (++_nodeCounter),
                Name = IOPath.GetFileNameWithoutExtension(topAsm.FullFileName ?? "Assembly"),
                SourcePath = topAsm.FullFileName,
                Type = "Assembly",
                Visible = true,
                Color = new double[] { 0.72, 0.76, 0.82 },
                Matrix = IdentityMatrix()
            };

            Report(10, "Collecting leaf occurrences (flat / top transforms)…");
            ComponentOccurrencesEnumerator leaves = null;
            try { leaves = topAsm.ComponentDefinition.Occurrences.AllLeafOccurrences; }
            catch (Exception ex)
            {
                _log?.Invoke("AllLeafOccurrences failed: " + ex.Message);
            }

            if (leaves == null)
            {
                // Fallback: top-level only, recursive via definition
                try
                {
                    foreach (ComponentOccurrence occ in topAsm.ComponentDefinition.Occurrences)
                    {
                        try
                        {
                            if (occ.Suppressed && !options.IncludeHidden) continue;
                            Document childDoc = null;
                            try { childDoc = occ.Definition?.Document as Document; } catch { }
                            if (childDoc == null) continue;
                            var child = BuildNodeFromDocument(childDoc, occ, meshesDir, options);
                            if (child != null) root.Children.Add(child);
                        }
                        catch (Exception ex) { _log?.Invoke("Skip: " + ex.Message); }
                    }
                }
                catch (Exception ex) { _log?.Invoke("Fallback walk failed: " + ex.Message); }
                return root;
            }

            int count = 0;
            try { count = leaves.Count; } catch { }
            int idx = 0;
            foreach (ComponentOccurrence leaf in leaves)
            {
                idx++;
                try
                {
                    if (leaf == null) continue;
                    try { if (leaf.Suppressed && !options.IncludeHidden) continue; } catch { }

                    Document partDoc = null;
                    try { partDoc = leaf.Definition?.Document as Document; } catch { }
                    if (partDoc == null) continue;
                    try
                    {
                        if (partDoc.DocumentType == DocumentTypeEnum.kAssemblyDocumentObject)
                            continue;
                    }
                    catch { }

                    var partNode = new SceneNode
                    {
                        Id = "n" + (++_nodeCounter),
                        Name = SafeOccurrenceName(leaf, partDoc),
                        SourcePath = partDoc.FullFileName,
                        Type = "Part",
                        Visible = true,
                        Color = new double[] { 0.72, 0.76, 0.82 },
                        // Top-assembly transform only — shared mesh in part-local cm
                        Matrix = MatrixFromOccurrenceChecked(leaf, SafeOccurrenceName(leaf, partDoc))
                    };

                    try
                    {
                        partNode.MeshFile = EnsureMesh(partDoc, meshesDir, options.ChordTolerance);
                        ApplyAppearance(partDoc, leaf, partNode, options);
                    }
                    catch (Exception ex)
                    {
                        _log?.Invoke("Mesh failed " + partNode.Name + ": " + ex.Message);
                    }

                    root.Children.Add(partNode);
                    if (idx % 25 == 0)
                        Report(Math.Min(75, 10 + (idx * 65) / Math.Max(1, count)),
                            "Part " + idx + "/" + count + ": " + partNode.Name);
                }
                catch (Exception ex)
                {
                    _log?.Invoke("Skip leaf: " + ex.Message);
                }
            }

            _log?.Invoke("Flat placement: " + root.Children.Count + " leaves (cm units, top transforms)");
            return root;
        }

        private static string SafeOccurrenceName(ComponentOccurrence leaf, Document partDoc)
        {
            try
            {
                if (!string.IsNullOrEmpty(leaf.Name))
                    return leaf.Name;
            }
            catch { }
            return IOPath.GetFileNameWithoutExtension(partDoc.FullFileName ?? "Part");
        }

        private static double[] IdentityMatrix()
        {
            return new double[]
            {
                1,0,0,0,
                0,1,0,0,
                0,0,1,0,
                0,0,0,1
            };
        }

        private string EnsureMesh(Document doc, string meshesDir, double chordTol)
        {
            var key = doc.FullFileName;
            if (_meshCache.TryGetValue(key, out var existing))
                return existing;

            var safeName = SanitizeFileName(IOPath.GetFileNameWithoutExtension(key));
            var glbName = safeName + "_" + Math.Abs(key.GetHashCode()).ToString("X8") + ".glb";
            var glbPath = IOPath.Combine(meshesDir, glbName);

            var relative = "meshes/" + glbName;
            try
            {
                MeshGeom geom = null;

                // PRIMARY: Inventor STL translator (reliable across versions)
                geom = TryExportViaStlTranslator(doc, glbPath, meshesDir, safeName);
                if (geom != null && geom.IsValid)
                {
                    TryFixMeshUnitScale(doc, geom, safeName);
                    _geomByMeshFile[relative] = geom;
                    _log?.Invoke("Mesh OK (STL) " + safeName + " — " + geom.TriangleCount + " tris");
                }
                else
                {
                    // SECONDARY: CalculateFacets API
                    _log?.Invoke("STL failed for " + safeName + " — trying facet API…");
                    bool ok = GltfMeshExporter.TryExportDocument(doc, glbPath, chordTol, out geom, _log);
                    if (ok && geom != null && geom.IsValid)
                    {
                        TryFixMeshUnitScale(doc, geom, safeName);
                        _geomByMeshFile[relative] = geom;
                        _log?.Invoke("Mesh OK (facets) " + safeName + " — " + geom.TriangleCount + " tris");
                    }
                    else
                        _log?.Invoke("FAILED mesh: " + safeName);
                }
            }
            catch (OutOfMemoryException)
            {
                _log?.Invoke("Out of memory tessellating " + safeName + " — skipped");
                try { GC.Collect(); GC.WaitForPendingFinalizers(); } catch { }
                return null;
            }
            catch (Exception ex)
            {
                _log?.Invoke("EnsureMesh error " + safeName + ": " + ex.Message);
            }

            _meshCache[key] = relative;
            return relative;
        }

        /// <summary>
        /// Self-correcting unit check. The STL translator's "ExportUnits" option index
        /// is undocumented/version-dependent (see comment in ExportStlViaTranslator) —
        /// trusting index 2 == centimeters is a guess that can silently be wrong on some
        /// Inventor builds/locales, or for parts authored in a different unit system
        /// (e.g. McMaster-Carr / ANSI hardware modeled in inches, mixed into an otherwise
        /// metric assembly). Rather than trust the guess, compare the tessellated STL's
        /// own bounding box against the SAME body's RangeBox, which the Inventor API
        /// always reports in internal database units (cm) regardless of the STL option,
        /// and rescale the mesh if the two disagree. This is exactly the mechanism behind
        /// the "small hardware renders as a giant rod flung across the scene" symptom.
        /// </summary>
        private void TryFixMeshUnitScale(Document doc, MeshGeom geom, string debugName)
        {
            if (geom == null || geom.Positions == null || geom.Positions.Count < 3) return;
            try
            {
                Box rangeBox = null;
                var part = doc as PartDocument;
                if (part != null) rangeBox = part.ComponentDefinition.RangeBox;
                else
                {
                    var asm = doc as AssemblyDocument;
                    if (asm != null) rangeBox = asm.ComponentDefinition.RangeBox;
                }
                if (rangeBox == null || rangeBox.MinPoint == null || rangeBox.MaxPoint == null) return;

                double invDx = Math.Abs(rangeBox.MaxPoint.X - rangeBox.MinPoint.X);
                double invDy = Math.Abs(rangeBox.MaxPoint.Y - rangeBox.MinPoint.Y);
                double invDz = Math.Abs(rangeBox.MaxPoint.Z - rangeBox.MinPoint.Z);
                double invMax = Math.Max(invDx, Math.Max(invDy, invDz));
                if (invMax < 0.001) return; // degenerate/tiny body — comparison not reliable

                var pos = geom.Positions;
                float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
                float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
                for (int i = 0; i + 2 < pos.Count; i += 3)
                {
                    float x = pos[i], y = pos[i + 1], z = pos[i + 2];
                    if (x < minX) minX = x; if (y < minY) minY = y; if (z < minZ) minZ = z;
                    if (x > maxX) maxX = x; if (y > maxY) maxY = y; if (z > maxZ) maxZ = z;
                }
                double stlDx = maxX - minX, stlDy = maxY - minY, stlDz = maxZ - minZ;
                double stlMax = Math.Max(stlDx, Math.Max(stlDy, stlDz));
                if (stlMax < 1e-6) return;

                double factor = invMax / stlMax;
                if (double.IsNaN(factor) || double.IsInfinity(factor)) return;
                if (factor < 0.01 || factor > 100)
                {
                    _log?.Invoke("Unit check for '" + debugName + "': ratio " + factor.ToString("0.###", CultureInfo.InvariantCulture) +
                                 " is too extreme to trust — leaving mesh unscaled (check manually).");
                    return;
                }

                if (Math.Abs(factor - 1.0) > 0.02)
                {
                    for (int i = 0; i < pos.Count; i++) pos[i] = (float)(pos[i] * factor);
                    _log?.Invoke(string.Format(CultureInfo.InvariantCulture,
                        "UNIT MISMATCH fixed for '{0}': STL export was not in cm as expected — rescaled mesh by {1:0.###}x " +
                        "(compared against Inventor's own RangeBox).", debugName, factor));
                }
            }
            catch (Exception ex)
            {
                _log?.Invoke("Unit check failed for '" + debugName + "': " + ex.Message);
            }
        }

        private MeshGeom TryExportViaStlTranslator(Document doc, string glbPath, string meshesDir, string safeName)
        {
            try
            {
                var stlPath = IOPath.Combine(meshesDir, safeName + "_tmp.stl");
                if (!ExportStlViaTranslator(doc, stlPath))
                    return null;
                var geom = StlMeshReader.TryRead(stlPath);
                try { if (IOFile.Exists(stlPath)) IOFile.Delete(stlPath); } catch { }
                if (geom == null || !geom.IsValid) return null;

                // Keep geometry in memory only — final scene.glb is written later (saves disk)
                try
                {
                    var bytes = GltfMeshExporter.BuildGlb(geom.Positions, geom.Normals, geom.Indices);
                    // do not write per-part files; combined scene.glb is enough
                }
                catch { }
                return geom;
            }
            catch (Exception ex)
            {
                _log?.Invoke("STL translator fallback error: " + ex.Message);
                return null;
            }
        }

        private bool ExportStlViaTranslator(Document doc, string stlPath)
        {
            if (doc == null) return false;
            const string stlAddInGuid = "{533E9A98-FC3B-11D4-8E3E-0010B541CDAB}";
            TranslatorAddIn trans = null;
            try { trans = _invApp.ApplicationAddIns.ItemById[stlAddInGuid] as TranslatorAddIn; }
            catch { }
            if (trans == null)
            {
                foreach (ApplicationAddIn ai in _invApp.ApplicationAddIns)
                {
                    try
                    {
                        if (ai is TranslatorAddIn t &&
                            ai.DisplayName != null &&
                            ai.DisplayName.IndexOf("STL", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            trans = t;
                            break;
                        }
                    }
                    catch { }
                }
            }
            if (trans == null)
            {
                _log?.Invoke("STL Translator AddIn not found");
                return false;
            }

            try
            {
                if (!trans.Activated)
                {
                    try { trans.Activate(); } catch { }
                }

                var ctx = _invApp.TransientObjects.CreateTranslationContext();
                ctx.Type = IOMechanismEnum.kFileBrowseIOMechanism;
                var opts = _invApp.TransientObjects.CreateNameValueMap();
                var dm = _invApp.TransientObjects.CreateDataMedium();
                dm.FileName = stlPath;

                // Populate options (Inventor requires this call even if we only set a few keys)
                try
                {
                    if (trans.HasSaveCopyAsOptions[doc, ctx, opts])
                    {
                        // 0=Inch, 1=Foot, 2=Centimeter, 3=mm, 4=Meter, 5=Micron... (varies by version)
                        // cm to match Inventor internal units used by Transformation
                        try { opts.Value["ExportUnits"] = 2; } catch { }
                        try { opts.Value["Resolution"] = 2; } catch { } // Low = fewer triangles (smaller files)
                        try { opts.Value["Binary"] = true; } catch { }
                        try { opts.Value["AllowMoveMeshNode"] = false; } catch { }
                        try { opts.Value["ExportFileStructure"] = 0; } catch { }
                    }
                }
                catch (Exception opEx)
                {
                    _log?.Invoke("STL options: " + opEx.Message);
                }

                try { if (IOFile.Exists(stlPath)) IOFile.Delete(stlPath); } catch { }

                trans.SaveCopyAs(doc, ctx, opts, dm);

                if (IOFile.Exists(stlPath) && new System.IO.FileInfo(stlPath).Length > 84)
                    return true;

                _log?.Invoke("STL file not created: " + stlPath);
                return false;
            }
            catch (Exception ex)
            {
                _log?.Invoke("STL SaveCopyAs failed: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Call GetExistingFacets / CalculateFacets via reflection to support
        /// both 5-arg and 6-arg (with FaceCount) Inventor interop signatures.
        /// Expected data order: VertexCoordinates(double[]), NormalVectors(double[]), VertexIndices(int[]).
        /// </summary>
        private void AppendBodyFacets(SurfaceBody body, StringBuilder sb, double chordTol)
        {
            if (body == null) return;

            double chord = Math.Max(0.001, chordTol);
            double[] vertices = null;
            double[] normals = null;
            int[] indices = null;
            int facetCount = 0;
            int vertexCount = 0;

            if (!TryGetFacets(body, chord, out vertexCount, out facetCount, out vertices, out normals, out indices))
                return;
            if (vertices == null || indices == null || facetCount <= 0)
                return;

            // Detect 0-based vs 1-based indices
            bool oneBased = false;
            int nIdx = Math.Min(indices.Length, facetCount * 3);
            for (int i = 0; i < nIdx; i++)
            {
                if (indices[i] == vertexCount) { oneBased = true; break; }
                if (indices[i] == 0) { oneBased = false; break; }
            }

            for (int f = 0; f < facetCount; f++)
            {
                int baseIdx = f * 3;
                if (baseIdx + 2 >= indices.Length) break;

                int i0 = indices[baseIdx];
                int i1 = indices[baseIdx + 1];
                int i2 = indices[baseIdx + 2];
                if (oneBased) { i0--; i1--; i2--; }
                if (i0 < 0 || i1 < 0 || i2 < 0) continue;
                if (i0 * 3 + 2 >= vertices.Length || i1 * 3 + 2 >= vertices.Length || i2 * 3 + 2 >= vertices.Length)
                    continue;

                double nx = 0, ny = 0, nz = 1;
                if (normals != null && normals.Length >= (i0 + 1) * 3)
                {
                    nx = normals[i0 * 3];
                    ny = normals[i0 * 3 + 1];
                    nz = normals[i0 * 3 + 2];
                }

                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "  facet normal {0} {1} {2}\n", nx, ny, nz);
                sb.AppendLine("    outer loop");
                AppendVertex(sb, vertices, i0);
                AppendVertex(sb, vertices, i1);
                AppendVertex(sb, vertices, i2);
                sb.AppendLine("    endloop");
                sb.AppendLine("  endfacet");
            }
        }

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

            var t = body.GetType();

            // Prefer existing facets
            if (InvokeFacetsMethod(body, t, "GetExistingFacets", null,
                    ref vertexCount, ref facetCount, ref vertices, ref normals, ref indices))
                return facetCount > 0 && vertices != null && indices != null;

            // Calculate new facets
            if (InvokeFacetsMethod(body, t, "CalculateFacets", chord,
                    ref vertexCount, ref facetCount, ref vertices, ref normals, ref indices))
                return facetCount > 0 && vertices != null && indices != null;

            return false;
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

                    // Layout A (with FaceCount): [tol?] vCount fCount faceCount verts normals indices
                    // Layout B (no FaceCount):   [tol?] vCount fCount verts normals indices
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
                            args = new object[] { 0, 0, 0, null, null, null };
                        else if (ps.Length == 5)
                            args = new object[] { 0, 0, null, null, null };
                        else
                            continue;
                    }

                    try { m.Invoke(body, args); }
                    catch { continue; }

                    // Unpack from the end: last = indices(int[]), prev = normals(double[]), prev = verts(double[])
                    int n = args.Length;
                    if (n < 5) continue;

                    var inds = args[n - 1] as int[];
                    var norms = args[n - 2] as double[];
                    var verts = args[n - 3] as double[];
                    if (verts == null || inds == null) continue;

                    int offset = tolerance.HasValue ? 1 : 0;
                    int vc = 0, fc = 0;
                    try { vc = Convert.ToInt32(args[offset]); } catch { }
                    try { fc = Convert.ToInt32(args[offset + 1]); } catch { }
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

        private static void AppendVertex(StringBuilder sb, double[] vertices, int index)
        {
            double x = vertices[index * 3];
            double y = vertices[index * 3 + 1];
            double z = vertices[index * 3 + 2];
            // Inventor internal units are cm
            sb.AppendFormat(CultureInfo.InvariantCulture, "      vertex {0} {1} {2}\n", x, y, z);
        }


        /// <summary>
        /// Local transform of occurrence relative to its immediate parent (column-major).
        /// </summary>

        /// <summary>
        /// Build column-major 4x4 matrix for glTF/Three.js from an occurrence.
        /// Prefer GetCoordinateSystem (origin + axes) — matches Inventor assembly placement.
        /// For AllLeafOccurrences proxies, Transformation is already in top-assembly space.
        /// </summary>
        /// <summary>
        /// Occurrence matrix in Inventor cm, column-major for glTF/Three.js.
        /// Same units as tessellated mesh — no cm→m (mismatch causes explosion).
        /// </summary>
        private static double[] MatrixFromOccurrence(ComponentOccurrence occ)
        {
            var m = IdentityMatrix();
            if (occ == null) return m;

            try
            {
                Inventor.Point origin = null;
                Inventor.Vector xAxis = null, yAxis = null, zAxis = null;
                Matrix tm = occ.Transformation;
                tm.GetCoordinateSystem(out origin, out xAxis, out yAxis, out zAxis);
                if (origin != null && xAxis != null && yAxis != null && zAxis != null)
                {
                    m = new double[16];
                    m[0] = xAxis.X; m[1] = xAxis.Y; m[2] = xAxis.Z; m[3] = 0;
                    m[4] = yAxis.X; m[5] = yAxis.Y; m[6] = yAxis.Z; m[7] = 0;
                    m[8] = zAxis.X; m[9] = zAxis.Y; m[10] = zAxis.Z; m[11] = 0;
                    m[12] = origin.X; m[13] = origin.Y; m[14] = origin.Z; m[15] = 1;
                    return m;
                }
            }
            catch { }

            try
            {
                Matrix tm = occ.Transformation;
                m = new double[16];
                for (int col = 0; col < 4; col++)
                    for (int row = 0; row < 4; row++)
                        m[col * 4 + row] = tm.Cell[row + 1, col + 1];
            }
            catch { }
            return m;
        }

        // Kept for nested fallback walks (definition Occurrences — relative to parent)
        private static double[] GetOccurrenceMatrix(ComponentOccurrence occ)
        {
            return MatrixFromOccurrence(occ);
        }

        private int _matrixDiagLogged;

        /// <summary>
        /// Same as MatrixFromOccurrence but validates the result (orthonormal axes, finite
        /// translation) and logs when a part's placement looks broken instead of silently
        /// emitting a garbage/huge/NaN matrix that would fling the part far from the assembly.
        /// This is the scattered-parts diagnostic: enable and re-export, then check the log
        /// for "BAD MATRIX" lines to see exactly which occurrences are affected.
        /// </summary>
        private double[] MatrixFromOccurrenceChecked(ComponentOccurrence occ, string debugName)
        {
            double[] m = null;
            bool usedFallback = false;

            try
            {
                Inventor.Point origin = null;
                Inventor.Vector xAxis = null, yAxis = null, zAxis = null;
                Matrix tm = occ.Transformation;
                tm.GetCoordinateSystem(out origin, out xAxis, out yAxis, out zAxis);
                if (origin != null && xAxis != null && yAxis != null && zAxis != null)
                {
                    m = new double[16];
                    m[0] = xAxis.X; m[1] = xAxis.Y; m[2] = xAxis.Z; m[3] = 0;
                    m[4] = yAxis.X; m[5] = yAxis.Y; m[6] = yAxis.Z; m[7] = 0;
                    m[8] = zAxis.X; m[9] = zAxis.Y; m[10] = zAxis.Z; m[11] = 0;
                    m[12] = origin.X; m[13] = origin.Y; m[14] = origin.Z; m[15] = 1;
                }
            }
            catch (Exception ex)
            {
                LogMatrixDiag("GetCoordinateSystem threw for '" + debugName + "': " + ex.Message);
            }

            if (m == null || !IsPlausibleMatrix(m))
            {
                if (m != null)
                    LogMatrixDiag("GetCoordinateSystem gave an implausible matrix for '" + debugName +
                                   "' (origin=" + m[12] + "," + m[13] + "," + m[14] + ") — trying Cell fallback.");
                usedFallback = true;
                try
                {
                    Matrix tm = occ.Transformation;
                    var m2 = new double[16];
                    for (int col = 0; col < 4; col++)
                        for (int row = 0; row < 4; row++)
                            m2[col * 4 + row] = tm.Cell[row + 1, col + 1];
                    m = m2;
                }
                catch (Exception ex)
                {
                    LogMatrixDiag("Cell fallback also threw for '" + debugName + "': " + ex.Message);
                    m = null;
                }
            }

            if (m == null || !IsPlausibleMatrix(m))
            {
                LogMatrixDiag("BAD MATRIX for '" + debugName + "' (fallback=" + usedFallback +
                               ") — placing at origin instead of a garbage position. " +
                               "This part is mis-tracked in Inventor's own Transformation for this occurrence.");
                return IdentityMatrix();
            }

            if (usedFallback)
                LogMatrixDiag("Used Cell fallback for '" + debugName + "' (GetCoordinateSystem unavailable) — result looked plausible.");

            return m;
        }

        /// <summary>
        /// Rejects NaN/Infinity, non-finite translation, wildly non-orthonormal axes,
        /// or a translation magnitude that is absurd for a mechanical assembly (helps
        /// catch garbage transforms from proxy occurrences instead of scattering parts).
        /// </summary>
        private static bool IsPlausibleMatrix(double[] m)
        {
            if (m == null || m.Length != 16) return false;
            for (int i = 0; i < 16; i++)
                if (double.IsNaN(m[i]) || double.IsInfinity(m[i])) return false;

            // Column vectors (rotation basis) should be close to unit length.
            double lx = Math.Sqrt(m[0] * m[0] + m[1] * m[1] + m[2] * m[2]);
            double ly = Math.Sqrt(m[4] * m[4] + m[5] * m[5] + m[6] * m[6]);
            double lz = Math.Sqrt(m[8] * m[8] + m[9] * m[9] + m[10] * m[10]);
            if (lx < 0.5 || lx > 2.0 || ly < 0.5 || ly > 2.0 || lz < 0.5 || lz > 2.0) return false;

            // Sanity cap on translation: 100,000 cm = 1 km, absurd for a CAD assembly.
            double tx = Math.Abs(m[12]), ty = Math.Abs(m[13]), tz = Math.Abs(m[14]);
            if (tx > 100000 || ty > 100000 || tz > 100000) return false;

            return true;
        }

        private void LogMatrixDiag(string msg)
        {
            if (_matrixDiagLogged >= 40)
            {
                if (_matrixDiagLogged == 40)
                {
                    _log?.Invoke("(further matrix diagnostics suppressed — 40+ occurrences flagged)");
                    _matrixDiagLogged++;
                }
                return;
            }
            _matrixDiagLogged++;
            _log?.Invoke(msg);
        }

        private void ApplyAppearance(Document doc, ComponentOccurrence occ, SceneNode node, ExportOptions options)
        {
            // Textures disabled (crash-prone). Neutral gray; user can randomize colors in HTML viewer.
            node.Color = new double[] { 0.72, 0.76, 0.82 };
            node.TextureFile = null;
            node.Opacity = 1.0;
            node.Metalness = 0.15;
            node.Roughness = 0.55;
        }

        private void TryOccurrenceOverrideColor(ComponentOccurrence occ, SceneNode node)
        {
            // Appearance overrides disabled with textures.
        }

        private void ReadAsset(Asset asset, SceneNode node, ExportOptions options)
        {
            if (asset == null) return;

            var rgb = TryGetAssetColor(asset);
            if (rgb != null) node.Color = rgb;

            // Opacity / transparency
            try
            {
                foreach (AssetValue av in asset)
                {
                    var name = "";
                    try { name = av.Name ?? av.DisplayName ?? ""; } catch { continue; }
                    var lower = name.ToLowerInvariant();

                    if (av is FloatAssetValue fav)
                    {
                        if (lower.Contains("opacity") || lower.Contains("transparency") || lower == "amount")
                        {
                            var v = fav.Value;
                            if (lower.Contains("transparency"))
                                node.Opacity = Math.Max(0.05, 1.0 - Clamp01(v > 1 ? v / 100.0 : v));
                            else
                                node.Opacity = Clamp01(v > 1 ? v / 100.0 : v);
                        }
                        else if (lower.Contains("metal"))
                            node.Metalness = Clamp01(fav.Value > 1 ? fav.Value / 100.0 : fav.Value);
                        else if (lower.Contains("rough") || lower.Contains("gloss"))
                        {
                            var v = Clamp01(fav.Value > 1 ? fav.Value / 100.0 : fav.Value);
                            // gloss is inverse of roughness
                            node.Roughness = lower.Contains("gloss") ? (1.0 - v) : v;
                        }
                    }
                    else if (av is ColorAssetValue cav)
                    {
                        if (lower.Contains("color") || lower.Contains("tint") || lower.Contains("diffuse"))
                        {
                            try
                            {
                                Inventor.Color c = cav.Value;
                                node.Color = new double[] { c.Red / 255.0, c.Green / 255.0, c.Blue / 255.0 };
                            }
                            catch { }
                        }
                    }
                    else if (options.ExportTextures && av is TextureAssetValue)
                    {
                        // Texture connected – try to resolve image path via related values
                    }
                    else if (options.ExportTextures && av is StringAssetValue sav)
                    {
                        if (lower.Contains("image") || lower.Contains("texture") || lower.Contains("filename") || lower.Contains("path"))
                        {
                            var path = sav.Value;
                            if (!string.IsNullOrWhiteSpace(path) && IOFile.Exists(path))
                            {
                                var rel = CopyTexture(path);
                                if (rel != null)
                                {
                                    node.TextureFile = rel;
                                    _anyTexture = true;
                                }
                            }
                        }
                    }
                    else if (options.ExportTextures)
                    {
                        // FilenameAssetValue / generic – probe via reflection-free ToString heuristics
                        try
                        {
                            var typeName = av.GetType().Name ?? "";
                            if (typeName.IndexOf("Filename", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                typeName.IndexOf("Texture", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                // Dynamic value access
                                var prop = av.GetType().GetProperty("Value");
                                if (prop != null)
                                {
                                    var val = prop.GetValue(av, null) as string;
                                    if (!string.IsNullOrWhiteSpace(val) && IOFile.Exists(val))
                                    {
                                        var rel = CopyTexture(val);
                                        if (rel != null)
                                        {
                                            node.TextureFile = rel;
                                            _anyTexture = true;
                                        }
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }

            // Second pass: common texture connected values on Generic / Metal materials
            if (options.ExportTextures && string.IsNullOrEmpty(node.TextureFile))
            {
                try
                {
                    foreach (AssetValue av in asset)
                    {
                        try
                        {
                            if (!(av is TextureAssetValue) && av.GetType().Name.IndexOf("Texture", StringComparison.OrdinalIgnoreCase) < 0)
                                continue;
                            // Linked texture asset may expose Image nested values – best effort skip if locked
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }

        private static double[] TryGetAssetColor(Asset asset)
        {
            if (asset == null) return null;
            try
            {
                foreach (AssetValue av in asset)
                {
                    if (!(av is ColorAssetValue)) continue;
                    var name = "";
                    try { name = (av.Name ?? av.DisplayName ?? "").ToLowerInvariant(); } catch { }
                    // Prefer diffuse / color / tint
                    if (name.Contains("color") || name.Contains("diffuse") || name.Contains("tint") || name.Contains("opaque") || string.IsNullOrEmpty(name))
                    {
                        try
                        {
                            Inventor.Color c = ((ColorAssetValue)av).Value;
                            return new double[] { c.Red / 255.0, c.Green / 255.0, c.Blue / 255.0 };
                        }
                        catch { }
                    }
                }
            }
            catch { }
            return null;
        }

        private void ReadLegacyRenderStyle(Document doc, SceneNode node)
        {
            try
            {
                var part = doc as PartDocument;
                if (part == null) return;
                // Older API via reflection (avoids Microsoft.CSharp dynamic binder dependency)
                object rs = null;
                try
                {
                    var prop = part.GetType().GetProperty("ActiveRenderStyle");
                    if (prop != null) rs = prop.GetValue(part, null);
                }
                catch { }
                if (rs == null) return;
                try
                {
                    var colorProp = rs.GetType().GetProperty("Color");
                    if (colorProp != null)
                    {
                        var c = colorProp.GetValue(rs, null) as Inventor.Color;
                        if (c != null)
                            node.Color = new double[] { c.Red / 255.0, c.Green / 255.0, c.Blue / 255.0 };
                    }
                }
                catch { }
                try
                {
                    var opProp = rs.GetType().GetProperty("Opacity");
                    if (opProp != null)
                    {
                        var opObj = opProp.GetValue(rs, null);
                        if (opObj != null)
                        {
                            double op = Convert.ToDouble(opObj, CultureInfo.InvariantCulture);
                            node.Opacity = Clamp01(op > 1 ? op / 100.0 : op);
                        }
                    }
                }
                catch { }
            }
            catch { }
        }

        private string CopyTexture(string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !IOFile.Exists(sourcePath))
                return null;
            if (_textureCache.TryGetValue(sourcePath, out var existing))
                return existing;

            try
            {
                var ext = IOPath.GetExtension(sourcePath);
                if (string.IsNullOrEmpty(ext)) ext = ".jpg";
                var safe = SanitizeFileName(IOPath.GetFileNameWithoutExtension(sourcePath));
                var destName = safe + "_" + Math.Abs(sourcePath.GetHashCode()).ToString("X8") + ext.ToLowerInvariant();
                var destPath = IOPath.Combine(_texturesDir, destName);
                if (!IOFile.Exists(destPath))
                    IOFile.Copy(sourcePath, destPath, true);
                var rel = "textures/" + destName;
                _textureCache[sourcePath] = rel;
                return rel;
            }
            catch (Exception ex)
            {
                _log?.Invoke("Texture copy failed: " + ex.Message);
                return null;
            }
        }

        private static double Clamp01(double v)
        {
            if (v < 0) return 0;
            if (v > 1) return 1;
            return v;
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "part";
            foreach (var c in IOPath.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Length > 60 ? name.Substring(0, 60) : name;
        }

        private void Report(int pct, string msg)
        {
            _progress?.Invoke(pct, msg);
            _log?.Invoke(msg);
        }

        /// <summary>
        /// Stream each mesh/texture to its own classic JS file under data/.
        /// Classic &lt;script src&gt; works under file:// (unlike fetch), without
        /// holding the entire assembly as one base64 string in memory.
        /// </summary>
        private List<string> WriteOfflineDataScripts(string outDir, SceneExport scene)
        {
            var scripts = new List<string>();
            scene.MeshScripts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            scene.TextureScripts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var dataDir = IOPath.Combine(outDir, "data");
            IODirectory.CreateDirectory(dataDir);

            int idx = 0;
            var meshFiles = scene.MeshFiles ?? new List<string>();
            int total = meshFiles.Count;
            foreach (var rel in meshFiles)
            {
                if (string.IsNullOrEmpty(rel)) continue;
                try
                {
                    var full = IOPath.Combine(outDir, rel.Replace('/', IOPath.DirectorySeparatorChar));
                    if (!IOFile.Exists(full)) continue;
                    var fi = new System.IO.FileInfo(full);
                    if (fi.Length < 64) continue;

                    // Skip extremely large single parts (>80 MB) to avoid OOM on base64
                    if (fi.Length > 80L * 1024 * 1024)
                    {
                        _log?.Invoke("Skip offline embed (too large): " + rel + " (" + (fi.Length / 1024 / 1024) + " MB)");
                        continue;
                    }

                    var bytes = IOFile.ReadAllBytes(full);
                    var b64 = Convert.ToBase64String(bytes);
                    bytes = null; // allow GC

                    var scriptName = "m" + idx.ToString("D4") + ".js";
                    var scriptRel = "data/" + scriptName;
                    var scriptFull = IOPath.Combine(dataDir, scriptName);

                    // Classic JS — assign into global map
                    var js = new StringBuilder(b64.Length + 128);
                    js.Append("window.__MESHDATA__=window.__MESHDATA__||{};");
                    js.Append("window.__MESHDATA__[");
                    js.Append(Q(rel));
                    js.Append("]=");
                    js.Append(Q(b64));
                    js.Append(";");
                    IOFile.WriteAllText(scriptFull, js.ToString(), Encoding.ASCII);
                    js = null;
                    b64 = null;

                    scripts.Add(scriptRel);
                    scene.MeshScripts[rel] = scriptRel;
                    idx++;

                    if (idx % 10 == 0)
                    {
                        Report(82 + Math.Min(7, (idx * 7) / Math.Max(1, total)),
                            "Offline data " + idx + "/" + total);
                        try { GC.Collect(1, GCCollectionMode.Optimized); } catch { }
                    }
                }
                catch (OutOfMemoryException)
                {
                    _log?.Invoke("Out of memory embedding " + rel + " — remaining meshes skipped for offline map.");
                    try { GC.Collect(); } catch { }
                    break;
                }
                catch (Exception ex)
                {
                    _log?.Invoke("Offline mesh script failed " + rel + ": " + ex.Message);
                }
            }

            // Textures (usually small)
            var texPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void Walk(SceneNode n)
            {
                if (n == null) return;
                if (!string.IsNullOrEmpty(n.TextureFile)) texPaths.Add(n.TextureFile);
                if (n.Children == null) return;
                foreach (var c in n.Children) Walk(c);
            }
            foreach (var r in scene.Roots) Walk(r);

            int tIdx = 0;
            foreach (var rel in texPaths)
            {
                try
                {
                    var full = IOPath.Combine(outDir, rel.Replace('/', IOPath.DirectorySeparatorChar));
                    if (!IOFile.Exists(full)) continue;
                    var fi = new System.IO.FileInfo(full);
                    if (fi.Length < 8 || fi.Length > 20L * 1024 * 1024) continue;

                    var bytes = IOFile.ReadAllBytes(full);
                    var ext = (IOPath.GetExtension(full) ?? ".png").ToLowerInvariant();
                    string mime = "image/png";
                    if (ext == ".jpg" || ext == ".jpeg") mime = "image/jpeg";
                    else if (ext == ".gif") mime = "image/gif";
                    else if (ext == ".webp") mime = "image/webp";
                    else if (ext == ".bmp") mime = "image/bmp";
                    var dataUrl = "data:" + mime + ";base64," + Convert.ToBase64String(bytes);
                    bytes = null;

                    var scriptName = "t" + tIdx.ToString("D4") + ".js";
                    var scriptRel = "data/" + scriptName;
                    var scriptFull = IOPath.Combine(dataDir, scriptName);
                    var js = "window.__TEXTUREDATA__=window.__TEXTUREDATA__||{};window.__TEXTUREDATA__[" +
                             Q(rel) + "]=" + Q(dataUrl) + ";";
                    IOFile.WriteAllText(scriptFull, js, Encoding.ASCII);
                    scripts.Add(scriptRel);
                    scene.TextureScripts[rel] = scriptRel;
                    tIdx++;
                }
                catch (Exception ex)
                {
                    _log?.Invoke("Offline texture script failed " + rel + ": " + ex.Message);
                }
            }

            _log?.Invoke("Wrote " + scripts.Count + " offline data scripts under data/");
            return scripts;
        }

        /// <summary>Minimal JSON serializer (no external deps, net48-safe).</summary>
        private static string SerializeScene(SceneExport scene)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendFormat("  \"assemblyName\": {0},\n", Q(scene.AssemblyName));
            sb.AppendFormat("  \"upAxis\": {0},\n", Q(scene.UpAxis));
            sb.AppendFormat("  \"exportTimeUtc\": {0},\n", Q(scene.ExportTimeUtc));
            sb.AppendFormat("  \"hasTextures\": {0},\n", scene.HasTextures ? "true" : "false");

            sb.Append("  \"meshData\": {},\n");
            sb.Append("  \"textureData\": {},\n");

            // path → data/mXXXX.js (viewer loads on demand)
            sb.Append("  \"meshScripts\": {");
            if (scene.MeshScripts != null && scene.MeshScripts.Count > 0)
            {
                sb.AppendLine();
                var first = true;
                foreach (var kv in scene.MeshScripts)
                {
                    if (!first) sb.AppendLine(",");
                    first = false;
                    sb.Append("    ").Append(Q(kv.Key)).Append(": ").Append(Q(kv.Value));
                }
                sb.AppendLine();
                sb.Append("  },\n");
            }
            else sb.Append("},\n");

            sb.Append("  \"textureScripts\": {");
            if (scene.TextureScripts != null && scene.TextureScripts.Count > 0)
            {
                sb.AppendLine();
                var first = true;
                foreach (var kv in scene.TextureScripts)
                {
                    if (!first) sb.AppendLine(",");
                    first = false;
                    sb.Append("    ").Append(Q(kv.Key)).Append(": ").Append(Q(kv.Value));
                }
                sb.AppendLine();
                sb.Append("  },\n");
            }
            else sb.Append("},\n");

            sb.Append("  \"roots\": [");
            for (int i = 0; i < scene.Roots.Count; i++)
            {
                if (i > 0) sb.Append(",");
                SerializeNode(sb, scene.Roots[i], 2);
            }
            sb.AppendLine();
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static void SerializeNode(StringBuilder sb, SceneNode n, int indent)
        {
            var pad = new string(' ', indent * 2);
            sb.AppendLine();
            sb.Append(pad).AppendLine("{");
            sb.Append(pad).AppendFormat("  \"id\": {0},\n", Q(n.Id));
            sb.Append(pad).AppendFormat("  \"name\": {0},\n", Q(n.Name));
            sb.Append(pad).AppendFormat("  \"type\": {0},\n", Q(n.Type));
            sb.Append(pad).AppendFormat("  \"visible\": {0},\n", n.Visible ? "true" : "false");
            if (!string.IsNullOrEmpty(n.MeshFile))
                sb.Append(pad).AppendFormat("  \"meshFile\": {0},\n", Q(n.MeshFile));
            if (!string.IsNullOrEmpty(n.TextureFile))
                sb.Append(pad).AppendFormat("  \"textureFile\": {0},\n", Q(n.TextureFile));
            if (n.Color != null && n.Color.Length >= 3)
                sb.Append(pad).AppendFormat(CultureInfo.InvariantCulture,
                    "  \"color\": [{0}, {1}, {2}],\n", n.Color[0], n.Color[1], n.Color[2]);
            sb.Append(pad).AppendFormat(CultureInfo.InvariantCulture,
                "  \"opacity\": {0},\n", n.Opacity);
            sb.Append(pad).AppendFormat(CultureInfo.InvariantCulture,
                "  \"metalness\": {0},\n", n.Metalness);
            sb.Append(pad).AppendFormat(CultureInfo.InvariantCulture,
                "  \"roughness\": {0},\n", n.Roughness);
            if (n.Matrix != null && n.Matrix.Length == 16)
            {
                sb.Append(pad).Append("  \"matrix\": [");
                for (int i = 0; i < 16; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(n.Matrix[i].ToString("G9", CultureInfo.InvariantCulture));
                }
                sb.AppendLine("],");
            }
            sb.Append(pad).Append("  \"children\": [");
            if (n.Children != null && n.Children.Count > 0)
            {
                for (int i = 0; i < n.Children.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    SerializeNode(sb, n.Children[i], indent + 2);
                }
                sb.AppendLine();
                sb.Append(pad).Append("  ]");
            }
            else
            {
                sb.Append("]");
            }
            sb.AppendLine();
            sb.Append(pad).Append("}");
        }

        private static string Q(string s)
        {
            if (s == null) return "null";
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "") + "\"";
        }
    }
}
