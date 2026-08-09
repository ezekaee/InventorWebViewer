# Inventor Web 3D Viewer

Bilingual (EN/FA) add-in for **Autodesk Inventor 2025+** that exports the active assembly to a standalone **HTML + Three.js** viewer.

UI is always **left-to-right (LTR)**. English is the default language; Persian only translates labels/text.

## Features

- Direct export to `index.html` + `scene.json` + STL meshes
- Appearance colors and textures (when available)
- Visual styles: Realistic, Shaded, Shaded with edges, Wireframe, Monochrome, …
- Texture ON/OFF
- Design tree with visibility and color swatches
- Orbit / Pan / Zoom + distance measure
- **Up-axis control inside the HTML viewer** (+Z / −Z / +Y / −Y / +X / −X)
- Modern glass UI + footer with LinkedIn / WhatsApp / Email
- Free software — support message in the viewer footer

## Requirements

- Windows 10/11 x64  
- Autodesk Inventor **2025+** (SeriesMin 29.0)  
- .NET Framework 4.8  

## Build

1. Open `InventorWebViewer.sln` in Visual Studio 2022.
2. Interop reference:

   `C:\Program Files\Autodesk\Inventor 2025\Bin\Public Assemblies\Autodesk.Inventor.Interop.dll`

   **Embed Interop Types = False**, platform **x64**.
3. Build → **Release**.
4. Place `InventorWebViewer.dll` in:

   `InventorWebViewer.bundle\Contents\`

## Install

Copy the entire `InventorWebViewer.bundle` folder to:

```
C:\ProgramData\Autodesk\ApplicationPlugins\
```

Restart Inventor. You should see the **Web 3D Viewer** tab.

## Usage

1. Open a top-level assembly.
2. Click **Web 3D Viewer**.
3. Choose output folder and mesh quality → **Export to HTML Viewer**.
4. Double-click `index.html` (no server needed). Change **Up axis** anytime in the viewer toolbar.

### Output structure

```
WebViewer_MyAsm/
  index.html      ← self-contained viewer (glTF embedded)
  scene.json      ← tree + matrices + base64 glTF data
  meshes/         ← .glb (glTF 2.0 binary)
  textures/       ← if any
```

> **glTF 2.0 (.glb)** is the export format — standard for web 3D.
> Geometry is embedded in `index.html` so double-click works (no local server).
> Internet is only needed to load Three.js from CDN.

## Contact settings

`%AppData%\InventorWebViewer\settings.ini`

- `Language` = `en` (default) or `fa`
- `LinkedInUrl`
- `WhatsAppNumber` (no +, e.g. `989121234567`)
- `WhatsAppMessage`

## Performance notes

- Unique parts are tessellated once and written as compact **.glb** (mesh cache).
- The HTML viewer parses embedded glTF in parallel batches with geometry/texture caching.

## License

Free to use and modify.
