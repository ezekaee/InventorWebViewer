# Changelog

## v1.1.0

### Export engine
- Primary tessellation via Inventor `CalculateFacets` / `SurfaceBody` (STL demoted to last-resort fallback)
- Explicit COM release after each SurfaceBody
- Periodic GC every 8 unique parts (chunked memory hygiene)
- Centralized file logger → `%AppData%\InventorWebViewer\log.txt`

### Web viewer
- ViewCube orientation gizmo
- Smart orbit pivot (double-click on geometry)
- Fly / first-person mode (WASD + Q/E)
- Dynamic grid sized to assembly bounds
- Soft contact shadow under model
- Lighting presets: Industrial, Flat/CAD (+ existing)
- LOD auto-fade for tiny parts during camera motion
- Section plane clipping toggle
- Headlight follows camera

### Deployment
- Inno Setup script (`Installer/InventorWebViewer.iss`)
- Standard `.resx` localization (EN + FA) with dictionary fallback
- Interop Copy Local = False (unchanged)

## v1.0.0
- Initial release: assembly → HTML + Three.js viewer
