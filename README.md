[README.md](https://github.com/user-attachments/files/30874307/README.md)
# Inventor Web 3D Viewer

Bilingual (EN/FA) add-in for **Autodesk Inventor 2025+** that exports the active assembly to a standalone **HTML + Three.js** viewer.

UI is always **left-to-right (LTR)**. English is the default language; Persian only translates labels/text.

## Features

### Export engine (C#)
- **Direct geometry** via Inventor API (`CalculateFacets` / `SurfaceBody`) — STL only as last-resort fallback
- Combined **glTF 2.0 (`.glb`)** with assembly hierarchy, 4×4 transforms, and appearance colors
- Unique-part mesh cache + periodic GC every 8 parts (chunked memory hygiene)
- Explicit **COM release** (`Marshal.FinalReleaseComObject`) after each SurfaceBody
- Session log: `%AppData%\InventorWebViewer\log.txt` (+ copy next to package when possible)
- Interop reference: **Copy Local = False**

### HTML / Three.js viewer
- Design tree (BOM) with visibility toggles and color swatches
- Orbit / Pan / Zoom / **Fly (WASD + Q/E)** navigation
- **ViewCube** (top-right) for standard views
- **Smart pivot**: double-click a face to set the orbit center
- Dynamic grid sized to assembly bounding box + soft **contact shadow**
- Lighting presets: Standard, Bright, Studio, **Industrial**, Outdoor, Soft, **Flat/CAD**
- **LOD / Auto-fade** of tiny fasteners during camera motion (toggle)
- **Section plane** clipping (toggle)
- Visual styles: Realistic, Shaded, Edges, Wireframe, Monochrome, …
- Distance / radius / face-to-face measure tools
- Up-axis control (+Z / −Z / +Y / −Y / +X / −X)
- Modern glass UI + footer contacts

## Requirements

- Windows 10/11 x64  
- Autodesk Inventor **2025+** (SeriesMin 29.0)  
- .NET Framework 4.8  

## Build

1. Open `InventorWebViewer.sln` in Visual Studio 2022.
2. Interop reference is auto-resolved from Inventor install paths, or place DLL in `Lib\`.
   **Embed Interop Types = False**, **Private = False**, platform **x64**.
3. Build → **Release**.
4. DLL is auto-copied to `InventorWebViewer.bundle\Contents\` (see csproj target).

## Install

### Manual
Copy the entire `InventorWebViewer.bundle` folder to:

```
C:\ProgramData\Autodesk\ApplicationPlugins\
```

Restart Inventor. Ribbon tab: **Web 3D Viewer**.

### Inno Setup
1. Ensure DLL is in `bundle\Contents\`
2. Open `Installer\InventorWebViewer.iss` in Inno Setup 6 → Compile
3. Run the generated setup (admin)

## Usage

1. Open a top-level assembly.
2. Click **Web 3D Viewer**.
3. Choose output folder and mesh quality → **Export to HTML Viewer**.
4. Double-click `index.html` (no server needed for embedded GLB).

### Output structure

```
WebViewer_MyAsm/
  index.html      ← self-contained viewer
  scene.glb       ← combined glTF 2.0 binary
  scene_b64.js    ← base64 payload for file:// offline open
  export_log.txt  ← optional local log copy
```

## Contact settings

`%AppData%\InventorWebViewer\settings.ini`

- `Language` = `en` (default) or `fa`
- `LinkedInUrl`
- `WhatsAppNumber` (no +, e.g. `989121234567`)
- `WhatsAppMessage`

## Performance notes

- Unique parts tessellated **once** via facets API; combined into one `scene.glb`.
- Raise **Chord Tolerance** (e.g. 0.5–1.0 cm) for factory-scale assemblies.
- LOD auto-fades bolts/washers while orbiting to keep FPS stable.
- For extreme assemblies, monitor `%AppData%\InventorWebViewer\log.txt` for OOM skips.

## License

Free to use and modify.
