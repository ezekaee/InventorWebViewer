using System.Collections.Generic;

namespace InventorWebViewer.Core
{
    public class SceneNode
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; } // Part, Assembly
        public string MeshFile { get; set; }
        public double[] Matrix { get; set; } // 4x4 column-major flat 16
        public bool Visible { get; set; } = true;
        public List<SceneNode> Children { get; set; } = new List<SceneNode>();
        public double[] Color { get; set; } // r,g,b 0-1
        public double Opacity { get; set; } = 1.0;
        public double Metalness { get; set; } = 0.2;
        public double Roughness { get; set; } = 0.45;
        public string TextureFile { get; set; } // relative path e.g. textures/xxx.jpg
        public string SourcePath { get; set; }
    }

    public class SceneExport
    {
        public string AssemblyName { get; set; }
        public string ProjectRoot { get; set; }
        public string UpAxis { get; set; } = "Z";
        public List<SceneNode> Roots { get; set; } = new List<SceneNode>();
        public List<string> MeshFiles { get; set; } = new List<string>();
        public string ExportTimeUtc { get; set; }
        public bool HasTextures { get; set; }
        /// <summary>Optional inline base64 (small models only).</summary>
        public Dictionary<string, string> MeshData { get; set; }
        public Dictionary<string, string> TextureData { get; set; }
        /// <summary>mesh path → data/*.js script for offline on-demand load.</summary>
        public Dictionary<string, string> MeshScripts { get; set; } = new Dictionary<string, string>();
        /// <summary>texture path → data/*.js script.</summary>
        public Dictionary<string, string> TextureScripts { get; set; } = new Dictionary<string, string>();
    }

    public class ExportOptions
    {
        public string OutputFolder { get; set; }
        public string UpAxis { get; set; } = "Z";
        public bool OpenInBrowser { get; set; } = true;
        /// <summary>Chord height (cm). Higher = fewer triangles = smoother view on large assemblies.</summary>
        public double ChordTolerance { get; set; } = 0.25; // higher = fewer triangles = smaller files
        public bool IncludeHidden { get; set; } = false;
        public bool ExportTextures { get; set; } = false; // textures off — random colors instead
    }
}
