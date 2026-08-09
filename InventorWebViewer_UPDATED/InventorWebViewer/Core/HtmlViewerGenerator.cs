using System;
using System.Text;
using IOPath = System.IO.Path;
using IOFile = System.IO.File;
using IODirectory = System.IO.Directory;

namespace InventorWebViewer.Core
{
    public static class HtmlViewerGenerator
    {
        public static void WritePackage(
            string outDir,
            SceneExport scene,
            AppSettings settings,
            string sceneJson = null,
            System.Collections.Generic.List<string> dataScripts = null,
            byte[] sceneGlb = null)
        {
            IODirectory.CreateDirectory(outDir);

            // Write base64 payload as a separate classic .js file (file:// safe, avoids huge HTML OOM)
            bool hasExternalB64 = false;
            // Always embed when GLB exists so double-click works offline (no server).
            if (sceneGlb != null && sceneGlb.Length > 64)
            {
                try
                {
                    var b64 = Convert.ToBase64String(sceneGlb);
                    var js = "window.__SCENE_GLB_B64__=\"" + b64 + "\";";
                    IOFile.WriteAllText(IOPath.Combine(outDir, "scene_b64.js"), js, Encoding.ASCII);
                    hasExternalB64 = true;
                    b64 = null;
                    js = null;
                }
                catch (Exception)
                {
                    hasExternalB64 = false;
                }
            }

            var html = BuildHtml(scene, settings, sceneJson, sceneGlb, hasExternalB64);
            IOFile.WriteAllText(IOPath.Combine(outDir, "index.html"), html, new UTF8Encoding(false));
        }

        private static string BuildHtml(
            SceneExport scene,
            AppSettings settings,
            string sceneJson,
            byte[] sceneGlb,
            bool hasExternalB64)
        {
            var title = Escape(scene.AssemblyName ?? "Assembly");
            var linkedIn = Escape(settings?.LinkedInUrl ?? "https://www.linkedin.com/in/zekaee/");
            var waNum = Escape(settings?.WhatsAppNumber ?? "989305741740");
            var waMsg = Uri.EscapeDataString(settings?.WhatsAppMessage ?? "Hi, about Inventor Web Viewer (FREE)");
            var email = Escape(settings?.Email ?? "e.zekaee.b@gmail.com");
            var upAxis = Escape(scene.UpAxis ?? "Z");
            // Always LTR; only labels are translated
            var isFa = (settings?.Language ?? "en") == "fa";
            var dir = "ltr";
            var lang = isFa ? "fa" : "en";
            string L(string fa, string en) => isFa ? fa : en;

            var supportText = L(
                "این برنامه رایگان است — من را در شبکه‌های اجتماعی حمایت کنید",
                "This app is FREE — Support me on social media");

            // Safe embed: </script> in JSON would break HTML; escape it
            var embeddedJson = string.IsNullOrEmpty(sceneJson)
                ? "{ \"roots\": [], \"upAxis\": \"" + upAxis + "\" }"
                : sceneJson.Replace("</", "<\\/");

            var sb = new StringBuilder(120000 + (embeddedJson != null ? embeddedJson.Length : 0));
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"" + lang + "\" dir=\"" + dir + "\">");
            sb.AppendLine("<head>");
            sb.AppendLine("<meta charset=\"utf-8\"/>");
            sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"/>");
            sb.AppendLine("<title>" + title + " – Web 3D Viewer</title>");
            sb.AppendLine("<script type=\"importmap\">");
            sb.AppendLine("{ \"imports\": {");
            sb.AppendLine("  \"three\": \"https://unpkg.com/three@0.160.0/build/three.module.js\",");
            sb.AppendLine("  \"three/addons/\": \"https://unpkg.com/three@0.160.0/examples/jsm/\"");
            sb.AppendLine("}}</script>");
            sb.AppendLine("<style>");
            sb.Append(Css());
            sb.AppendLine("</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("<div id=\"app\">");

            sb.AppendLine("  <div id=\"toolbar\" class=\"glass\">");
            sb.AppendLine("    <div class=\"tool-group\">");
            sb.AppendLine("      <button type=\"button\" class=\"tool active\" data-tool=\"orbit\">⟳ " + L("چرخش", "Orbit") + "</button>");
            sb.AppendLine("      <button type=\"button\" class=\"tool\" data-tool=\"pan\">✥ " + L("جابجایی", "Pan") + "</button>");
            sb.AppendLine("      <button type=\"button\" class=\"tool\" data-tool=\"zoom\">⊕ " + L("زوم", "Zoom") + "</button>");
            sb.AppendLine("      <button type=\"button\" class=\"tool\" data-tool=\"measure\">📏 " + L("اندازه", "Measure") + "</button>");
            sb.AppendLine("    </div>");
            sb.AppendLine("    <div class=\"tool-group\">");
            sb.AppendLine("      <button type=\"button\" class=\"tool\" id=\"btnFit\">⛶ " + L("فیت", "Fit") + "</button>");
            sb.AppendLine("      <button type=\"button\" class=\"tool\" id=\"btnHome\">⌂ " + L("خانه", "Home") + "</button>");
            sb.AppendLine("      <select id=\"axisSelect\" class=\"tool-select\" title=\"" + L("محور بالا (فقط در نمایشگر)", "Up axis (viewer only)") + "\">");
            sb.AppendLine("        <option value=\"Z\"" + (upAxis == "Z" ? " selected" : "") + ">+Z Up</option>");
            sb.AppendLine("        <option value=\"-Z\"" + (upAxis == "-Z" ? " selected" : "") + ">-Z Up</option>");
            sb.AppendLine("        <option value=\"Y\"" + (upAxis == "Y" ? " selected" : "") + ">+Y Up</option>");
            sb.AppendLine("        <option value=\"-Y\"" + (upAxis == "-Y" ? " selected" : "") + ">-Y Up</option>");
            sb.AppendLine("        <option value=\"X\"" + (upAxis == "X" ? " selected" : "") + ">+X Up</option>");
            sb.AppendLine("        <option value=\"-X\"" + (upAxis == "-X" ? " selected" : "") + ">-X Up</option>");
            sb.AppendLine("      </select>");
            sb.AppendLine("    </div>");
            sb.AppendLine("    <div class=\"tool-group\">");
            sb.AppendLine("      <select id=\"visualStyle\" class=\"tool-select\" title=\"" + L("حالت نمایش", "Visual style") + "\">");
            sb.AppendLine("        <option value=\"realistic\">" + L("واقع‌گرایانه", "Realistic") + "</option>");
            sb.AppendLine("        <option value=\"shaded\" selected>" + L("سایه‌دار", "Shaded") + "</option>");
            sb.AppendLine("        <option value=\"shadedEdges\">" + L("سایه‌دار با لبه", "Shaded with edges") + "</option>");
            sb.AppendLine("        <option value=\"shadedHidden\">" + L("سایه‌دار با لبه مخفی", "Shaded with hidden edges") + "</option>");
            sb.AppendLine("        <option value=\"wireframe\">" + L("سیم‌قاب", "Wireframe") + "</option>");
            sb.AppendLine("        <option value=\"wireHidden\">" + L("سیم‌قاب با لبه مخفی", "Wireframe with hidden edges") + "</option>");
            sb.AppendLine("        <option value=\"wireVisible\">" + L("فقط لبه‌های مرئی", "Wireframe visible edges only") + "</option>");
            sb.AppendLine("        <option value=\"monochrome\">" + L("تک‌رنگ", "Monochrome") + "</option>");
            sb.AppendLine("      </select>");
            sb.AppendLine("      <button type=\"button\" class=\"tool\" id=\"btnRandomColor\" title=\"" + L("رنگ تصادفی", "Random colors") + "\">🎲 " + L("رنگ", "Colors") + "</button>");
            sb.AppendLine("    </div>");
            sb.AppendLine("    <div class=\"tool-group\">");
            sb.AppendLine("      <select id=\"lightingSelect\" class=\"tool-select\" title=\"" + L("حالت نورپردازی", "Lighting mode") + "\">");
            sb.AppendLine("        <option value=\"standard\" selected>" + L("استاندارد", "Standard") + "</option>");
            sb.AppendLine("        <option value=\"bright\">" + L("روشن", "Bright") + "</option>");
            sb.AppendLine("        <option value=\"studio\">" + L("استودیو", "Studio") + "</option>");
            sb.AppendLine("        <option value=\"outdoor\">" + L("فضای باز", "Outdoor") + "</option>");
            sb.AppendLine("        <option value=\"soft\">" + L("نرم", "Soft") + "</option>");
            sb.AppendLine("      </select>");
            sb.AppendLine("    </div>");
            sb.AppendLine("    <div class=\"tool-group\">");
            sb.AppendLine("      <button type=\"button\" class=\"tool\" id=\"btnToggleTree\">☰ " + L("درخت", "Tree") + "</button>");
            sb.AppendLine("      <button type=\"button\" class=\"tool\" id=\"btnToggleGrid\"># " + L("شبکه", "Grid") + "</button>");
            sb.AppendLine("    </div>");
            sb.AppendLine("  </div>");

            sb.AppendLine("  <aside id=\"sidebar\" class=\"glass\">");
            sb.AppendLine("    <div class=\"side-header\">");
            sb.AppendLine("      <strong>" + L("درخت طراحی", "Design Tree") + "</strong>");
            sb.AppendLine("      <div class=\"side-actions\">");
            sb.AppendLine("        <button type=\"button\" id=\"btnShowAll\" class=\"mini\">" + L("نمایش همه", "Show all") + "</button>");
            sb.AppendLine("        <button type=\"button\" id=\"btnHideAll\" class=\"mini\">" + L("پنهان همه", "Hide all") + "</button>");
            sb.AppendLine("      </div>");
            sb.AppendLine("    </div>");
            sb.AppendLine("    <div id=\"tree\"></div>");
            sb.AppendLine("  </aside>");

            sb.AppendLine("  <div id=\"viewport\"><canvas id=\"c\"></canvas></div>");
            sb.AppendLine("  <div id=\"loadingOverlay\">");
            sb.AppendLine("    <div class=\"spinner\"></div>");
            sb.AppendLine("    <div id=\"loadingLabel\">" + L("در حال بارگذاری مدل…", "Loading model…") + "</div>");
            sb.AppendLine("    <div id=\"loadingBarWrap\"><div id=\"loadingBar\"></div></div>");
            sb.AppendLine("  </div>");
            sb.AppendLine("  <div id=\"measureHud\" class=\"glass hidden\">");
            sb.AppendLine("    <select id=\"measureModeSelect\" class=\"tool-select\">");
            sb.AppendLine("      <option value=\"point\" selected>" + L("نقطه به نقطه", "Point to point") + "</option>");
            sb.AppendLine("      <option value=\"radius\">" + L("شعاع/قطر (۳ نقطه)", "Radius / Diameter (3 pts)") + "</option>");
            sb.AppendLine("      <option value=\"face\">" + L("سطح به سطح", "Surface to surface") + "</option>");
            sb.AppendLine("    </select>");
            sb.AppendLine("    <span id=\"measureText\">" + L("دو نقطه روی مدل کلیک کنید", "Click two points on the model") + "</span>");
            sb.AppendLine("    <button type=\"button\" id=\"btnClearMeasure\" class=\"mini\">" + L("پاک‌کردن", "Clear") + "</button>");
            sb.AppendLine("  </div>");
            sb.AppendLine("  <div id=\"selectHud\" class=\"glass hidden\">");
            sb.AppendLine("    <span id=\"selectText\"></span>");
            sb.AppendLine("    <button type=\"button\" id=\"btnHideSelected\" class=\"mini\">👁 " + L("مخفی", "Hide") + "</button>");
            sb.AppendLine("    <button type=\"button\" id=\"btnCloseSelect\" class=\"mini\">✕</button>");
            sb.AppendLine("  </div>");
            sb.AppendLine("  <div id=\"status\" class=\"glass\">" + L("در حال بارگذاری…", "Loading…") + "</div>");
            sb.AppendLine("  <div id=\"bootError\" class=\"boot-error hidden\"></div>");

            sb.AppendLine("  <footer id=\"footer\" class=\"glass\">");
            sb.AppendLine("    <div class=\"footer-left\">");
            sb.AppendLine("      <span class=\"brand\">Inventor Web 3D Viewer</span>");
            sb.AppendLine("      <span class=\"free-badge\">FREE</span>");
            sb.AppendLine("    </div>");
            sb.AppendLine("    <span class=\"support-msg\">" + Escape(supportText) + "</span>");
            sb.AppendLine("    <span class=\"contacts\">");
            sb.AppendLine("      <a href=\"" + linkedIn + "\" target=\"_blank\" rel=\"noopener\" class=\"contact-link\">LinkedIn</a>");
            sb.AppendLine("      <span class=\"contact-sep\">·</span>");
            sb.AppendLine("      <a href=\"https://wa.me/" + waNum + "?text=" + waMsg + "\" target=\"_blank\" rel=\"noopener\" class=\"contact-link\">WhatsApp</a>");
            sb.AppendLine("      <span class=\"contact-sep\">·</span>");
            sb.AppendLine("      <a href=\"mailto:" + email + "\" class=\"contact-link\">Email</a>");
            sb.AppendLine("    </span>");
            sb.AppendLine("  </footer>");
            sb.AppendLine("</div>");

            // Metadata
            sb.AppendLine("<script>");
            sb.AppendLine("window.__SCENE_META__ = " + embeddedJson + ";");
            sb.AppendLine("window.__SCENE_GLB_B64__ = window.__SCENE_GLB_B64__ || null;");
            sb.AppendLine("</script>");

            // External base64 payload (classic script works under file://)
            if (hasExternalB64)
            {
                sb.AppendLine("<script src=\"scene_b64.js\"></script>");
            }
            else if (sceneGlb != null && sceneGlb.Length > 64 && sceneGlb.Length < 4 * 1024 * 1024)
            {
                // Small models: inline
                sb.AppendLine("<script>");
                sb.Append("window.__SCENE_GLB_B64__ = \"");
                sb.Append(Convert.ToBase64String(sceneGlb));
                sb.AppendLine("\";");
                sb.AppendLine("</script>");
            }

            sb.AppendLine("<script type=\"module\">");
            sb.Append(ViewerJs(isFa));
            sb.AppendLine("</script>");
            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        private static string Css()
        {
            return @"
*{box-sizing:border-box;margin:0;padding:0}
html,body{width:100%;height:100%;overflow:hidden;font-family:'Segoe UI',system-ui,-apple-system,sans-serif;background:#0a0f1a;color:#e8eef7}
#app{position:relative;width:100%;height:100%}
#viewport{position:absolute;inset:0;z-index:0}
#c{width:100%;height:100%;display:block}
.glass{
  background:rgba(10,16,28,0.82);
  backdrop-filter:blur(16px) saturate(160%);
  -webkit-backdrop-filter:blur(16px) saturate(160%);
  border:1px solid rgba(255,255,255,0.14);
  box-shadow:0 8px 32px rgba(0,0,0,0.5), inset 0 1px 0 rgba(255,255,255,0.08);
  transition:background 0.2s ease, border-color 0.2s ease, opacity 0.2s ease;
  opacity:0.96;
  color:#f4f7fc;
  text-shadow:0 1px 2px rgba(0,0,0,0.55);
}
.glass:hover{opacity:1;background:rgba(14,22,38,0.9);border-color:rgba(255,255,255,0.22)}
#toolbar{
  position:absolute;top:12px;left:50%;transform:translateX(-50%);
  z-index:20;display:flex;gap:8px;padding:8px 12px;border-radius:16px;
  flex-wrap:wrap;justify-content:center;max-width:96vw;
}
.tool-group{display:flex;gap:5px;align-items:center;flex-wrap:wrap}
.tool,.tool-select{
  appearance:none;border:1px solid rgba(255,255,255,0.16);
  background:rgba(255,255,255,0.08);color:#f4f7fc;
  padding:7px 11px;border-radius:10px;cursor:pointer;font-size:12.5px;font-weight:600;
  transition:background 0.15s,border-color 0.15s,transform 0.12s;
  text-shadow:0 1px 2px rgba(0,0,0,0.55);
}
.tool:hover,.tool-select:hover{background:rgba(0,140,255,0.35);border-color:rgba(100,190,255,0.65);transform:translateY(-1px)}
.tool.active{background:rgba(0,130,255,0.58);border-color:rgba(120,205,255,0.85);box-shadow:0 0 12px rgba(0,140,255,0.35)}
.tool-select{padding:6px 10px;outline:none;max-width:210px}
.tool-select option{color:#0a0f1a;background:#f4f7fc}
#sidebar{
  position:absolute;top:66px;bottom:62px;left:12px;width:280px;z-index:15;
  border-radius:16px;padding:12px;display:flex;flex-direction:column;overflow:hidden;
}
#sidebar.collapsed{display:none}
.side-header{display:flex;flex-direction:column;gap:8px;margin-bottom:10px;padding-bottom:8px;border-bottom:1px solid rgba(255,255,255,0.08)}
.side-header strong{font-size:13px;letter-spacing:0.02em;color:#fff}
.side-actions{display:flex;gap:6px}
.mini{font-size:11px;padding:4px 9px;border-radius:8px;border:1px solid rgba(255,255,255,0.16);background:rgba(255,255,255,0.08);color:#eef2fa;cursor:pointer;font-weight:600}
.mini:hover{background:rgba(0,140,255,0.4)}
#tree{flex:1;overflow:auto;font-size:12.5px;line-height:1.45;scrollbar-width:thin;color:#eef2fa}
.tree-item{display:flex;align-items:center;gap:6px;padding:3px 4px;border-radius:7px;cursor:default}
.tree-item:hover{background:rgba(255,255,255,0.09)}
.tree-item.selected{background:rgba(0,140,255,0.32);outline:1px solid rgba(120,200,255,0.6)}
.tree-item .twisty{width:14px;text-align:center;cursor:pointer;opacity:0.85}
.tree-item .eye{cursor:pointer;opacity:0.95;user-select:none;width:18px;text-align:center}
.tree-item .eye.off{opacity:0.4}
.tree-item .label{flex:1;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.tree-item .badge{font-size:9.5px;opacity:0.75;padding:0 4px;color:#9fc4ff;font-weight:700}
.tree-item .swatch{width:11px;height:11px;border-radius:3px;border:1px solid rgba(255,255,255,0.3);flex-shrink:0}
.tree-children{margin-left:14px;border-left:1px solid rgba(255,255,255,0.1);padding-left:4px}
#measureHud{
  position:absolute;top:66px;left:50%;transform:translateX(-50%);
  z-index:18;padding:10px 14px;border-radius:12px;font-size:15.5px;font-weight:800;
  color:#d6ffb0;display:flex;align-items:center;gap:12px;letter-spacing:0.01em;
  background:rgba(8,14,24,0.92);
}
#measureHud #measureText{text-shadow:0 1px 3px rgba(0,0,0,0.85);font-variant-numeric:tabular-nums}
#measureHud.hidden{display:none}
#measureHud .mini{pointer-events:auto}
#measureModeSelect{font-size:12.5px;padding:5px 8px;border-radius:8px}
#loadingOverlay{
  position:absolute;inset:0;z-index:60;display:flex;flex-direction:column;
  align-items:center;justify-content:center;gap:18px;
  background:radial-gradient(ellipse at center,#111b2e 0%,#080d18 100%);
  transition:opacity 0.35s ease;
}
#loadingOverlay.hidden{opacity:0;pointer-events:none}
.spinner{
  width:64px;height:64px;border-radius:50%;
  border:5px solid rgba(120,190,255,0.18);
  border-top-color:#4fb3ff;border-right-color:#8fd6ff;
  animation:spin 0.9s linear infinite;
  will-change:transform;
}
@keyframes spin{to{transform:rotate(360deg)}}
#loadingLabel{font-size:14px;font-weight:600;color:#dce8f7;text-align:center;max-width:80vw}
#loadingBarWrap{width:240px;height:6px;border-radius:4px;background:rgba(255,255,255,0.12);overflow:hidden}
#loadingBar{height:100%;width:35%;border-radius:4px;background:linear-gradient(90deg,#3fa9ff,#8fe0ff);animation:loadbar 1.3s ease-in-out infinite}
@keyframes loadbar{0%{transform:translateX(-100%)}50%{transform:translateX(60%)}100%{transform:translateX(220%)}}
#selectHud{
  position:absolute;top:66px;left:50%;transform:translateX(-50%);
  z-index:18;padding:8px 10px 8px 16px;border-radius:12px;font-size:13px;font-weight:700;
  color:#ffd479;display:flex;align-items:center;gap:8px;
}
#selectHud.hidden{display:none}
#status{
  position:absolute;bottom:62px;left:50%;transform:translateX(-50%);
  z-index:12;padding:6px 14px;border-radius:10px;font-size:12px;opacity:0.92;
  pointer-events:none;max-width:80vw;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;
}
.boot-error{
  position:absolute;inset:0;z-index:50;display:flex;align-items:center;justify-content:center;
  background:rgba(10,15,26,0.92);padding:24px;text-align:center;font-size:14px;line-height:1.6;
}
.boot-error.hidden{display:none}
.boot-error code{background:rgba(255,255,255,0.08);padding:2px 6px;border-radius:4px}
#footer{
  position:absolute;bottom:8px;left:50%;transform:translateX(-50%);
  z-index:20;display:flex;align-items:center;gap:14px;padding:8px 16px;border-radius:14px;font-size:12px;
  flex-wrap:wrap;justify-content:center;max-width:96vw;
}
.footer-left{display:flex;align-items:center;gap:8px}
.brand{opacity:0.95;font-weight:700}
.free-badge{
  font-size:10px;font-weight:700;letter-spacing:0.06em;
  background:linear-gradient(135deg,#22c55e,#16a34a);color:#fff;
  padding:2px 7px;border-radius:6px;
}
.support-msg{opacity:0.9;font-size:11.5px;max-width:320px;text-align:center}
.contacts{display:flex;gap:8px;align-items:center;flex-wrap:wrap}.contact-sep{opacity:.6;font-size:12px}
.contact-link{color:#8fc4ff;text-decoration:underline;text-underline-offset:2px;font-size:12px;font-weight:600;opacity:1}
.contact-link:hover{color:#c3e0ff}



@media (max-width:720px){
  #sidebar{width:240px}
  .support-msg{display:none}
}
";
        }

        private static string ViewerJs(bool isFa)
        {
            var clickTwo = isFa ? "دو نقطه روی مدل کلیک کنید" : "Click two points on the model";
            var distLbl = isFa ? "فاصله" : "Distance";
            var loading = isFa ? "در حال بارگذاری مش و تکسچر…" : "Loading meshes & textures…";
            var ready = isFa ? "آماده" : "Ready";
            var err = isFa ? "خطا در بارگذاری" : "Load error";
            var loadingPct = isFa ? "بارگذاری" : "Loading";
            var selPrefix = isFa ? "انتخاب" : "Selected";
            var noMesh = isFa ? "هیچ مشی یافت نشد — خروجی STL را بررسی کنید" : "No meshes found — check STL export";
            var fileProto = isFa
                ? "مرورگر file:// را محدود می‌کند. در پوشه خروجی اجرا کنید: python -m http.server 8080"
                : "Browser blocks file:// loads. In the output folder run: python -m http.server 8080";
            var clickPoint2 = isFa ? "نقطه دوم را کلیک کنید" : "Click the second point";
            var clickRadius3 = isFa ? "روی لبه دایره‌ای ۳ نقطه کلیک کنید (اکنون: {n}/۳)" : "Click 3 points on a circular edge (now {n}/3)";
            var radiusLbl = isFa ? "شعاع" : "Radius";
            var diameterLbl = isFa ? "قطر" : "Diameter";
            var clickFace1 = isFa ? "روی سطح اول کلیک کنید" : "Click on the first surface";
            var clickFace2 = isFa ? "روی سطح دوم کلیک کنید" : "Click on the second surface";
            var faceDistLbl = isFa ? "فاصله سطح تا سطح" : "Surface-to-surface distance";
            var badCircle = isFa ? "این ۳ نقطه روی یک دایره نیستند — دوباره امتحان کنید" : "Those 3 points don't fit a circle — try again";

            // Build JS without fragile C# string interleaving mid-line.
            // All localized strings are injected once at the top via JsonStr.
            var sb = new StringBuilder(20000);
            sb.AppendLine("import * as THREE from 'three';");
            sb.AppendLine("import { OrbitControls } from 'three/addons/controls/OrbitControls.js';");
            sb.AppendLine("import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';");
            sb.AppendLine();
            sb.AppendLine("const L_CLICK = " + JsonStr(clickTwo) + ";");
            sb.AppendLine("const L_DIST = " + JsonStr(distLbl) + ";");
            sb.AppendLine("const L_LOADING = " + JsonStr(loading) + ";");
            sb.AppendLine("const L_READY = " + JsonStr(ready) + ";");
            sb.AppendLine("const L_ERR = " + JsonStr(err) + ";");
            sb.AppendLine("const L_LOAD_PCT = " + JsonStr(loadingPct) + ";");
            sb.AppendLine("const L_SEL_PREFIX = " + JsonStr(selPrefix) + ";");
            sb.AppendLine("const L_NO_MESH = " + JsonStr(noMesh) + ";");
            sb.AppendLine("const L_FILE_PROTO = " + JsonStr(fileProto) + ";");
            sb.AppendLine("const L_CLICK_PT2 = " + JsonStr(clickPoint2) + ";");
            sb.AppendLine("const L_CLICK_R3 = " + JsonStr(clickRadius3) + ";");
            sb.AppendLine("const L_RADIUS = " + JsonStr(radiusLbl) + ";");
            sb.AppendLine("const L_DIAMETER = " + JsonStr(diameterLbl) + ";");
            sb.AppendLine("const L_CLICK_FACE1 = " + JsonStr(clickFace1) + ";");
            sb.AppendLine("const L_CLICK_FACE2 = " + JsonStr(clickFace2) + ";");
            sb.AppendLine("const L_FACE_DIST = " + JsonStr(faceDistLbl) + ";");
            sb.AppendLine("const L_BAD_CIRCLE = " + JsonStr(badCircle) + ";");
            sb.AppendLine();
            sb.Append(@"
const $ = (s) => document.querySelector(s);
const statusEl = $('#status');
const measureHud = $('#measureHud');
const measureText = $('#measureText');
const measureModeSelect = $('#measureModeSelect');
const treeEl = $('#tree');
const sidebar = $('#sidebar');
const bootError = $('#bootError');
const loadingOverlay = $('#loadingOverlay');
const loadingLabel = $('#loadingLabel');

let scene, camera, renderer, controls, rootGroup, gridHelper, axesHelper;
let hemiLight, dirLight, fillLight;
let axisLabelSprites = [];
let currentTool = 'orbit';
let measureMode = 'point';
let measurePts = [];
let measureHistory = [];
let measureMarkers = [];
let measureExtras = []; // circle rings, plane helpers, text-label sprites tied to completed measurements
let nodeMap = new Map();
let objToEyeEl = new Map();
let selectedObj = null;
let walkAdvanceThreshold = 1;
let textureEnabled = true;
let visualStyle = 'shaded';
const textureLoader = new THREE.TextureLoader();
const gltfLoader = new GLTFLoader();
const selectHud = $('#selectHud');
const selectText = $('#selectText');

const LIGHTING_PRESETS = {
  standard: { hemi: 0.95, dir: 1.15, fill: 0.45, exposure: 1.10 },
  bright:   { hemi: 1.35, dir: 1.65, fill: 0.55, exposure: 1.35 },
  studio:   { hemi: 1.00, dir: 1.95, fill: 0.80, exposure: 1.15 },
  outdoor:  { hemi: 1.55, dir: 2.25, fill: 0.60, exposure: 1.50 },
  soft:     { hemi: 1.10, dir: 0.70, fill: 0.50, exposure: 1.00 }
};
function applyLightingPreset(name) {
  var p = LIGHTING_PRESETS[name] || LIGHTING_PRESETS.standard;
  if (hemiLight) hemiLight.intensity = p.hemi;
  if (dirLight) dirLight.intensity = p.dir;
  if (fillLight) fillLight.intensity = p.fill;
  if (renderer) renderer.toneMappingExposure = p.exposure;
}

function showBootError(msg) {
  if (!bootError) return;
  bootError.classList.remove('hidden');
  bootError.innerHTML = '<div><strong>' + L_ERR + '</strong><br/>' + msg + '</div>';
}

try {
  init();
  loadScene();
} catch (e) {
  console.error(e);
  showBootError(String(e && e.message ? e.message : e));
}

function init() {
  const canvas = $('#c');
  if (!canvas) throw new Error('Canvas #c missing');

  renderer = new THREE.WebGLRenderer({ canvas: canvas, antialias: true, alpha: true, powerPreference: 'high-performance' });
  // Cap DPR for large assemblies (smoother FPS)
  renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 1.5));
  renderer.setSize(window.innerWidth, window.innerHeight);
  renderer.outputColorSpace = THREE.SRGBColorSpace;
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.toneMappingExposure = 1.10;

  scene = new THREE.Scene();
  scene.background = new THREE.Color(0x0a0f1a);
  // Linear fog whose near/far are rescaled to the loaded model's own size in
  // updateSceneScale() — a fixed density (the old FogExp2 approach) looks fine
  // on a small part but completely swallows a large model (e.g. an industrial
  // shed spanning thousands of cm) in haze until you zoom in very close.
  scene.fog = new THREE.Fog(0x0a0f1a, 500, 4000);

  camera = new THREE.PerspectiveCamera(50, window.innerWidth / window.innerHeight, 0.01, 1e6);
  camera.position.set(80, 60, 100);

  controls = new OrbitControls(camera, canvas);
  controls.enableDamping = true;
  controls.dampingFactor = 0.08;
  controls.screenSpacePanning = true;
  // Navisworks-style ""walk"" zoom: once the camera gets very close to the pivot,
  // OrbitControls' multiplicative dolly asymptotically stalls (it scales the
  // remaining distance, so it never actually reaches/passes the target). Nudge
  // the pivot forward when that happens so scrolling keeps carrying you through
  // the model instead of hitting an invisible wall.
  controls.addEventListener('change', maybeAdvanceWalkTarget);

  hemiLight = new THREE.HemisphereLight(0xb8d4ff, 0x24304a, 0.95);
  scene.add(hemiLight);
  dirLight = new THREE.DirectionalLight(0xffffff, 1.15);
  dirLight.position.set(40, 80, 50);
  scene.add(dirLight);
  scene.add(dirLight.target);
  fillLight = new THREE.DirectionalLight(0x88aaff, 0.45);
  fillLight.position.set(-50, 20, -30);
  scene.add(fillLight);

  const ground = new THREE.Mesh(
    new THREE.CircleGeometry(500, 64),
    new THREE.MeshBasicMaterial({ color: 0x152038, transparent: true, opacity: 0.35, side: THREE.DoubleSide })
  );
  ground.rotation.x = -Math.PI / 2;
  ground.position.y = -0.02;
  scene.add(ground);
  window.__groundMesh = ground;

  gridHelper = new THREE.GridHelper(200, 40, 0x3a5a8a, 0x1e2e48);
  gridHelper.material.transparent = true;
  gridHelper.material.opacity = 0.55;
  scene.add(gridHelper);
  axesHelper = new THREE.AxesHelper(25);
  scene.add(axesHelper);
  buildAxisLabels(25);

  rootGroup = new THREE.Group();
  scene.add(rootGroup);

  window.addEventListener('resize', onResize);
  canvas.addEventListener('pointerdown', onPointerDown);
  canvas.addEventListener('pointerdown', onSelectPointerDown);
  canvas.addEventListener('pointerup', onSelectPointerUp);
  window.addEventListener('keydown', onKeyDown);

  document.querySelectorAll('.tool[data-tool]').forEach(function(btn) {
    btn.addEventListener('click', function() {
      document.querySelectorAll('.tool[data-tool]').forEach(function(b) { b.classList.remove('active'); });
      btn.classList.add('active');
      currentTool = btn.dataset.tool;
      setToolMode(currentTool);
    });
  });
  $('#btnFit').addEventListener('click', fitView);
  $('#btnHome').addEventListener('click', function() { applyUpAxis($('#axisSelect').value); fitView(); });
  $('#axisSelect').addEventListener('change', function(e) { applyUpAxis(e.target.value); });
  $('#btnToggleTree').addEventListener('click', function() { sidebar.classList.toggle('collapsed'); });
  $('#btnToggleGrid').addEventListener('click', function() {
    gridHelper.visible = !gridHelper.visible;
    axesHelper.visible = gridHelper.visible;
    axisLabelSprites.forEach(function(s) { s.visible = gridHelper.visible; });
  });
  $('#btnShowAll').addEventListener('click', function() { setAllVisible(true); });
  $('#btnHideAll').addEventListener('click', function() { setAllVisible(false); });

  $('#visualStyle').addEventListener('change', function(e) {
    visualStyle = e.target.value;
    applyVisualStyle();
  });
  var lightingSel = $('#lightingSelect');
  if (lightingSel) lightingSel.addEventListener('change', function(e) { applyLightingPreset(e.target.value); });

  var btnClearMeasure = $('#btnClearMeasure');
  if (btnClearMeasure) btnClearMeasure.addEventListener('click', function(e) { e.stopPropagation(); clearMeasure(); });

  if (measureModeSelect) {
    measureModeSelect.addEventListener('click', function(e) { e.stopPropagation(); });
    measureModeSelect.addEventListener('change', function(e) {
      e.stopPropagation();
      measureMode = e.target.value;
      measurePts = [];
      updateMeasurePrompt();
    });
  }

  var btnHideSelected = $('#btnHideSelected');
  if (btnHideSelected) btnHideSelected.addEventListener('click', function(e) { e.stopPropagation(); hideSelected(); });
  var btnCloseSelect = $('#btnCloseSelect');
  if (btnCloseSelect) btnCloseSelect.addEventListener('click', function(e) { e.stopPropagation(); clearSelection(); });

  setToolMode('orbit');
  var btnRC = $('#btnRandomColor');
  if (btnRC) btnRC.addEventListener('click', randomizeColors);
  animate();
}

function setToolMode(tool) {
  currentTool = tool;
  // Defaults: always allow wheel zoom
  controls.enableZoom = true;
  controls.enablePan = true;
  controls.enableRotate = true;
  if (typeof THREE !== 'undefined' && THREE.MOUSE) {
    if (tool === 'orbit') {
      controls.mouseButtons.LEFT = THREE.MOUSE.ROTATE;
      controls.mouseButtons.MIDDLE = THREE.MOUSE.DOLLY;
      controls.mouseButtons.RIGHT = THREE.MOUSE.PAN;
    } else if (tool === 'pan') {
      controls.enableRotate = false;
      controls.mouseButtons.LEFT = THREE.MOUSE.PAN;
      controls.mouseButtons.RIGHT = THREE.MOUSE.PAN;
    } else if (tool === 'zoom') {
      controls.enableRotate = false;
      controls.enablePan = false;
      controls.mouseButtons.LEFT = THREE.MOUSE.DOLLY;
    } else if (tool === 'measure') {
      controls.enableRotate = false;
      controls.enablePan = false;
    }
  } else {
    controls.enableRotate = tool === 'orbit';
    controls.enablePan = tool === 'pan' || tool === 'orbit';
    controls.enableZoom = true;
  }
  if (tool === 'measure') {
    measureHud.classList.remove('hidden');
    updateMeasurePrompt();
  } else {
    measureHud.classList.add('hidden');
    measurePts = []; // drop only the in-progress point; keep completed measurements until Clear
  }
}

function updateMeasurePrompt() {
  if (measureMode === 'radius') {
    measureText.textContent = L_CLICK_R3.replace('{n}', measurePts.length);
  } else if (measureMode === 'face') {
    measureText.textContent = measurePts.length === 0 ? L_CLICK_FACE1 : L_CLICK_FACE2;
  } else {
    measureText.textContent = measurePts.length === 0 ? L_CLICK : L_CLICK_PT2;
  }
}

function onResize() {
  camera.aspect = window.innerWidth / window.innerHeight;
  camera.updateProjectionMatrix();
  renderer.setSize(window.innerWidth, window.innerHeight);
}

function animate() {
  requestAnimationFrame(animate);
  controls.update();
  renderer.render(scene, camera);
}


function b64ToArrayBuffer(b64) {
  var bin = atob(b64);
  var len = bin.length;
  var bytes = new Uint8Array(len);
  for (var i = 0; i < len; i++) bytes[i] = bin.charCodeAt(i);
  return bytes.buffer;
}

function nextPaint() {
  // Two rAFs so the browser actually paints the spinner/overlay before we run
  // heavy synchronous work (base64 decode + glTF parse) that would otherwise
  // block the main thread before the first frame ever renders.
  return new Promise(function(resolve) {
    requestAnimationFrame(function() { requestAnimationFrame(resolve); });
  });
}

function hideLoadingOverlay() {
  if (loadingOverlay) loadingOverlay.classList.add('hidden');
}

async function loadScene() {
  try {
    statusEl.textContent = L_LOADING;
    if (loadingLabel) loadingLabel.textContent = L_LOADING;
    await nextPaint();

    var b64 = window.__SCENE_GLB_B64__;
    var meta = window.__SCENE_META__ || {};

    if (!b64) {
      throw new Error('No embedded scene GLB. Rebuild add-in, delete old output, re-export. See Log for Mesh OK.');
    }

    var ab = b64ToArrayBuffer(b64);
    // Free base64 string to reduce peak RAM after decode
    try { window.__SCENE_GLB_B64__ = null; } catch (e0) {}
    await mountGltfBuffer(ab, meta);
    hideLoadingOverlay();
  } catch (e) {
    console.error(e);
    statusEl.textContent = L_ERR + ': ' + (e && e.message ? e.message : e);
    showBootError(String(e && e.message ? e.message : e));
    hideLoadingOverlay();
  }
}

async function mountGltfBuffer(arrayBuffer, meta) {
  var gltf = await new Promise(function(resolve, reject) {
    gltfLoader.parse(arrayBuffer, '', resolve, reject);
  });

  // Clear previous
  while (rootGroup.children.length) rootGroup.remove(rootGroup.children[0]);
  nodeMap.clear();
  clearMeasure();

  var model = gltf.scene || (gltf.scenes && gltf.scenes[0]);
  if (!model) throw new Error('Empty glTF scene');

  rootGroup.add(model);

  // Index nodes for design tree + visibility
  var counter = 0;
  model.traverse(function(obj) {
    var id = (obj.userData && obj.userData.id) || obj.name || ('n' + (counter++));
    // glTF extras land on userData when parsed by three.js
    if (obj.userData && obj.userData.extras) {
      try {
        var ex = obj.userData.extras;
        if (ex.id) id = ex.id;
      } catch (e1) {}
    }
    obj.userData.nodeId = id;
    if (obj.isMesh) {
      obj.userData.baseColor = (obj.material && obj.material.color)
        ? obj.material.color.clone()
        : new THREE.Color(0.72, 0.76, 0.82);
      obj.userData.baseMap = (obj.material && obj.material.map) ? obj.material.map : null;
      obj.userData.metalness = (obj.material && obj.material.metalness != null) ? obj.material.metalness : 0.15;
      obj.userData.roughness = (obj.material && obj.material.roughness != null) ? obj.material.roughness : 0.5;
      obj.userData.opacity = (obj.material && obj.material.opacity != null) ? obj.material.opacity : 1;
      obj.frustumCulled = true;
      // Mark parent group for lazy edges
      if (obj.parent) {
        obj.parent.userData.needsEdges = true;
        obj.parent.userData.needsWire = true;
        obj.parent.userData.geo = obj.geometry;
      }
    }
    if (!nodeMap.has(id))
      nodeMap.set(id, { obj3d: obj, data: { id: id, name: obj.name, type: obj.isMesh ? 'Part' : 'Assembly' } });
  });

  var axisSel = $('#axisSelect');
  var up = (meta && meta.upAxis) || 'Z';
  if (axisSel) axisSel.value = up;
  applyUpAxis(up, false);

  buildTreeFromObject3D(model);
  applyVisualStyle();
  fitView();

  var meshCount = 0;
  model.traverse(function(o) { if (o.isMesh) meshCount++; });
  statusEl.textContent = L_READY + ' — ' + ((meta && meta.assemblyName) || model.name || '') +
    ' (' + meshCount + ' meshes)';
}

function buildTreeFromObject3D(root) {
  treeEl.innerHTML = '';
  objToEyeEl.clear();
  clearSelection();
  var frag = document.createDocumentFragment();

  function makeItem(obj) {
    var wrap = document.createElement('div');
    var row = document.createElement('div');
    row.className = 'tree-item';

    var hasKids = obj.children && obj.children.length > 0;
    var twisty = document.createElement('span');
    twisty.className = 'twisty';
    twisty.textContent = hasKids ? '▾' : '·';

    var eye = document.createElement('span');
    eye.className = 'eye' + (obj.visible === false ? ' off' : '');
    eye.textContent = obj.visible === false ? '○' : '●';

    var swatch = document.createElement('span');
    swatch.className = 'swatch';
    var col = '#8899aa';
    obj.traverse(function(c) {
      if (c.isMesh && c.material && c.material.color) {
        col = '#' + c.material.color.getHexString();
      }
    });
    swatch.style.background = col;

    var label = document.createElement('span');
    label.className = 'label';
    label.textContent = obj.name || obj.userData.nodeId || 'node';

    var badge = document.createElement('span');
    badge.className = 'badge';
    badge.textContent = obj.isMesh ? 'MESH' : (hasKids ? 'ASM' : '');

    row.appendChild(twisty);
    row.appendChild(eye);
    row.appendChild(swatch);
    row.appendChild(label);
    row.appendChild(badge);
    wrap.appendChild(row);

    var kids = document.createElement('div');
    kids.className = 'tree-children';
    if (hasKids) {
      for (var i = 0; i < obj.children.length; i++) {
        // Skip helper objects
        var ch = obj.children[i];
        if (ch.userData && (ch.userData.isEdges || ch.userData.isWire)) continue;
        kids.appendChild(makeItem(ch));
      }
      wrap.appendChild(kids);
      twisty.addEventListener('click', function() {
        var open = kids.style.display !== 'none';
        kids.style.display = open ? 'none' : '';
        twisty.textContent = open ? '▸' : '▾';
      });
    }

    eye.addEventListener('click', function(e) {
      e.stopPropagation();
      obj.visible = !obj.visible;
      eye.classList.toggle('off', !obj.visible);
      eye.textContent = obj.visible ? '●' : '○';
      if (!obj.visible && selectedObj === obj) clearSelection();
    });

    label.addEventListener('click', function() { flash(obj); selectObject(obj); });
    objToEyeEl.set(obj, eye);
    return wrap;
  }

  frag.appendChild(makeItem(root));
  treeEl.appendChild(frag);
}

function ensureEdges(group) {
  if (!group.userData || !group.userData.needsEdges) return;
  var geo = group.userData.geo;
  if (!geo) {
    group.traverse(function(c) {
      if (!geo && c.isMesh && c.geometry) geo = c.geometry;
    });
  }
  if (!geo) return;
  try {
    var edges = new THREE.EdgesGeometry(geo, 30);
    var edgeLines = new THREE.LineSegments(edges, new THREE.LineBasicMaterial({
      color: 0x1a1a1a, transparent: true, opacity: 0.85
    }));
    edgeLines.userData.isEdges = true;
    edgeLines.visible = false;
    group.add(edgeLines);
  } catch (e) {}
  group.userData.needsEdges = false;
}

function ensureWire(group) {
  if (!group.userData || !group.userData.needsWire) return;
  var geo = group.userData.geo;
  if (!geo) {
    group.traverse(function(c) {
      if (!geo && c.isMesh && c.geometry) geo = c.geometry;
    });
  }
  if (!geo) return;
  var wireMesh = new THREE.Mesh(geo, new THREE.MeshBasicMaterial({
    color: 0x111111, wireframe: true, transparent: true, opacity: 0.9
  }));
  wireMesh.userData.isWire = true;
  wireMesh.visible = false;
  group.add(wireMesh);
  group.userData.needsWire = false;
}

function applyVisualStyle() {
  var style = visualStyle;
  var wantEdges = style === 'shadedEdges' || style === 'shadedHidden' || style === 'wireVisible';
  var wantWire = style === 'wireframe' || style === 'wireHidden' || style === 'wireVisible';

  // Lazily build heavy edge/wire helpers only when style needs them
  if (wantEdges || wantWire) {
    rootGroup.traverse(function(obj) {
      if (obj.userData && obj.userData.needsEdges && wantEdges) ensureEdges(obj);
      if (obj.userData && obj.userData.needsWire && wantWire) ensureWire(obj);
    });
  }

  rootGroup.traverse(function(obj) {
    if (obj.userData && obj.userData.isEdges) {
      var showEdges = wantEdges;
      obj.visible = showEdges;
      if (obj.material) {
        if (style === 'shadedHidden') {
          obj.material.opacity = 0.35;
          obj.material.color.setHex(0x555555);
        } else {
          obj.material.opacity = 0.9;
          obj.material.color.setHex(0x111111);
        }
      }
      return;
    }
    if (obj.userData && obj.userData.isWire) {
      var pureWire = style === 'wireframe' || style === 'wireHidden' || style === 'wireVisible';
      obj.visible = pureWire;
      if (obj.material) {
        obj.material.opacity = style === 'wireHidden' ? 0.4 : 0.95;
        obj.material.color.setHex(0x111111);
      }
      return;
    }
    if (!obj.isMesh || !obj.material || obj.userData.isWire) return;

    var mat = obj.material;
    if (!mat.isMeshStandardMaterial && !mat.isMeshBasicMaterial) return;

    var baseColor = obj.userData.baseColor || new THREE.Color(0.75, 0.78, 0.82);
    var baseMap = obj.userData.baseMap || null;
    var useTex = textureEnabled && baseMap &&
      (style === 'realistic' || style === 'shaded' || style === 'shadedEdges' || style === 'shadedHidden');

    if (mat.map !== undefined) mat.map = useTex ? baseMap : null;

    if (style === 'monochrome') {
      mat.color.setHex(0xb0b4ba);
      if (mat.map) mat.map = null;
      mat.metalness = 0.1;
      mat.roughness = 0.7;
      mat.wireframe = false;
      mat.opacity = obj.userData.opacity != null ? obj.userData.opacity : 1;
      mat.transparent = mat.opacity < 0.999;
      obj.visible = true;
    } else if (style === 'wireframe' || style === 'wireHidden' || style === 'wireVisible') {
      obj.visible = style === 'wireHidden';
      if (style === 'wireHidden') {
        mat.color.copy(baseColor);
        mat.opacity = 0.15;
        mat.transparent = true;
        mat.wireframe = false;
        mat.map = null;
      }
    } else {
      obj.visible = true;
      mat.color.copy(baseColor);
      mat.metalness = style === 'realistic'
        ? (obj.userData.metalness != null ? obj.userData.metalness : 0.25)
        : (obj.userData.metalness != null ? obj.userData.metalness : 0.15);
      mat.roughness = style === 'realistic'
        ? (obj.userData.roughness != null ? obj.userData.roughness : 0.4)
        : Math.max(0.35, obj.userData.roughness != null ? obj.userData.roughness : 0.5);
      mat.wireframe = false;
      mat.opacity = obj.userData.opacity != null ? obj.userData.opacity : 1;
      mat.transparent = mat.opacity < 0.999;
      if (!useTex) mat.map = null;
    }
    mat.needsUpdate = true;
  });
}

function buildTreeUI(roots) {
  treeEl.innerHTML = '';
  var frag = document.createDocumentFragment();
  for (var i = 0; i < roots.length; i++) frag.appendChild(makeTreeItem(roots[i]));
  treeEl.appendChild(frag);
}

function makeTreeItem(node) {
  var wrap = document.createElement('div');
  var row = document.createElement('div');
  row.className = 'tree-item';

  var twisty = document.createElement('span');
  twisty.className = 'twisty';
  var hasKids = node.children && node.children.length;
  twisty.textContent = hasKids ? '▾' : '·';

  var eye = document.createElement('span');
  eye.className = 'eye' + (node.visible === false ? ' off' : '');
  eye.textContent = node.visible === false ? '○' : '●';

  var swatch = document.createElement('span');
  swatch.className = 'swatch';
  if (node.color && node.color.length >= 3) {
    var r = Math.round(node.color[0] * 255);
    var g = Math.round(node.color[1] * 255);
    var b = Math.round(node.color[2] * 255);
    swatch.style.background = 'rgb(' + r + ',' + g + ',' + b + ')';
  } else {
    swatch.style.background = '#8899aa';
  }

  var label = document.createElement('span');
  label.className = 'label';
  label.textContent = node.name || node.id;

  var badge = document.createElement('span');
  badge.className = 'badge';
  badge.textContent = node.type === 'Assembly' ? 'ASM' : (node.textureFile ? 'TEX' : (node.meshFile ? 'IPT' : ''));

  row.appendChild(twisty);
  row.appendChild(eye);
  row.appendChild(swatch);
  row.appendChild(label);
  row.appendChild(badge);
  wrap.appendChild(row);

  var kids = document.createElement('div');
  kids.className = 'tree-children';
  if (hasKids) {
    for (var i = 0; i < node.children.length; i++) kids.appendChild(makeTreeItem(node.children[i]));
    wrap.appendChild(kids);
    twisty.addEventListener('click', function() {
      var open = kids.style.display !== 'none';
      kids.style.display = open ? 'none' : '';
      twisty.textContent = open ? '▸' : '▾';
    });
  }

  eye.addEventListener('click', function(e) {
    e.stopPropagation();
    var entry = nodeMap.get(node.id);
    if (!entry) return;
    entry.obj3d.visible = !entry.obj3d.visible;
    eye.classList.toggle('off', !entry.obj3d.visible);
    eye.textContent = entry.obj3d.visible ? '●' : '○';
  });

  label.addEventListener('click', function() {
    var entry = nodeMap.get(node.id);
    if (entry) flash(entry.obj3d);
  });

  var entry = nodeMap.get(node.id);
  if (entry) entry.eyeEl = eye;
  return wrap;
}

function flash(obj) {
  obj.traverse(function(c) {
    if (c.isMesh && c.material && c.material.emissive) {
      var old = c.material.emissive.clone();
      c.material.emissive.setHex(0x2266ff);
      setTimeout(function() { c.material.emissive.copy(old); }, 350);
    }
  });
}

function setAllVisible(v) {
  objToEyeEl.forEach(function(eyeEl, obj) {
    obj.visible = v;
    eyeEl.classList.toggle('off', !v);
    eyeEl.textContent = v ? '●' : '○';
  });
}

function selectObject(obj) {
  clearSelection();
  selectedObj = obj;
  if (obj.material && obj.material.emissive) {
    obj.userData._prevEmissive = obj.material.emissive.clone();
    obj.material.emissive.setHex(0x3388ff);
  }
  var name = obj.name || (obj.userData && obj.userData.nodeId) || 'node';
  selectText.textContent = L_SEL_PREFIX + ': ' + name;
  selectHud.classList.remove('hidden');
  document.querySelectorAll('.tree-item.selected').forEach(function(el) { el.classList.remove('selected'); });
  var eyeEl = objToEyeEl.get(obj);
  if (eyeEl) {
    var row = eyeEl.closest('.tree-item');
    if (row) { row.classList.add('selected'); row.scrollIntoView({ block: 'nearest' }); }
  }
}

function clearSelection() {
  if (selectedObj && selectedObj.material && selectedObj.userData && selectedObj.userData._prevEmissive) {
    selectedObj.material.emissive.copy(selectedObj.userData._prevEmissive);
  }
  selectedObj = null;
  selectHud.classList.add('hidden');
  document.querySelectorAll('.tree-item.selected').forEach(function(el) { el.classList.remove('selected'); });
}

function hideSelected() {
  if (!selectedObj) return;
  selectedObj.visible = false;
  var eyeEl = objToEyeEl.get(selectedObj);
  if (eyeEl) { eyeEl.classList.add('off'); eyeEl.textContent = '○'; }
  clearSelection();
}

let _selectDownPos = null;
function onSelectPointerDown(ev) {
  if (currentTool === 'measure' || ev.button !== 0) { _selectDownPos = null; return; }
  _selectDownPos = { x: ev.clientX, y: ev.clientY };
}
function onSelectPointerUp(ev) {
  if (currentTool === 'measure' || ev.button !== 0 || !_selectDownPos) return;
  var dx = ev.clientX - _selectDownPos.x, dy = ev.clientY - _selectDownPos.y;
  _selectDownPos = null;
  if (Math.sqrt(dx * dx + dy * dy) > 5) return; // was a drag (orbit/pan), not a click
  var rect = renderer.domElement.getBoundingClientRect();
  var mouse = new THREE.Vector2(
    ((ev.clientX - rect.left) / rect.width) * 2 - 1,
    -((ev.clientY - rect.top) / rect.height) * 2 + 1
  );
  var raycaster = new THREE.Raycaster();
  raycaster.setFromCamera(mouse, camera);
  var hits = raycaster.intersectObjects(rootGroup.children, true);
  if (!hits.length) { clearSelection(); return; }
  selectObject(hits[0].object);
}
function onKeyDown(ev) {
  if (!selectedObj) return;
  if (ev.key === 'h' || ev.key === 'H' || ev.key === 'Delete') hideSelected();
  else if (ev.key === 'Escape') clearSelection();
}


function randomizeColors() {
  rootGroup.traverse(function(obj) {
    if (!obj.isMesh || !obj.material || obj.userData.isWire) return;
    var c = new THREE.Color(
      Math.random() * 0.55 + 0.25,
      Math.random() * 0.55 + 0.25,
      Math.random() * 0.55 + 0.25
    );
    obj.userData.baseColor = c.clone();
    if (obj.material.color) obj.material.color.copy(c);
    obj.material.needsUpdate = true;
  });
}

function applyUpAxis(axis, refit) {
  if (refit === undefined) refit = true;
  rootGroup.rotation.set(0, 0, 0);
  if (axis === 'Z') rootGroup.rotation.x = -Math.PI / 2;
  else if (axis === '-Z') rootGroup.rotation.x = Math.PI / 2;
  else if (axis === 'Y') { /* identity */ }
  else if (axis === '-Y') rootGroup.rotation.x = Math.PI;
  else if (axis === 'X') rootGroup.rotation.z = Math.PI / 2;
  else if (axis === '-X') rootGroup.rotation.z = -Math.PI / 2;
  else rootGroup.rotation.x = -Math.PI / 2;
  gridHelper.rotation.set(0, 0, 0);
  if (refit) fitView();
}

function fitView() {
  var box = new THREE.Box3().setFromObject(rootGroup);
  if (box.isEmpty()) return;
  var size = box.getSize(new THREE.Vector3());
  var center = box.getCenter(new THREE.Vector3());
  var maxDim = Math.max(size.x, size.y, size.z) || 1;
  walkAdvanceThreshold = Math.max(maxDim * 0.01, 0.05);
  var dist = maxDim * 1.8;
  controls.target.copy(center);
  camera.position.set(center.x + dist * 0.7, center.y + dist * 0.55, center.z + dist * 0.7);
  camera.near = Math.max(dist / 1000, 0.01);
  camera.far = dist * 100;
  camera.updateProjectionMatrix();
  controls.update();
  updateSceneScale(maxDim, center);
}

// Rescales everything that was originally tuned for a ""typical"" small/medium part
// (fog distance, light throw, grid size, axis-label size) to match whatever just
// got loaded. Without this a huge model (e.g. a steel-frame industrial shed, tens
// of meters across) starts almost entirely fogged out / underlit because those
// values were fixed absolute numbers, while a tiny part ends up with an
// oversized grid/axes relative to itself.
function updateSceneScale(maxDim, center) {
  if (scene.fog) {
    scene.fog.color.set(0x0a0f1a);
    scene.fog.near = maxDim * 2.2;
    scene.fog.far = maxDim * 9;
  }

  var L = maxDim * 1.6;
  if (dirLight) {
    dirLight.position.set(center.x + L * 0.45, center.y + L * 0.95, center.z + L * 0.55);
    dirLight.target.position.copy(center);
    dirLight.target.updateMatrixWorld();
  }
  if (fillLight) {
    fillLight.position.set(center.x - L * 0.55, center.y + L * 0.22, center.z - L * 0.35);
  }

  if (gridHelper) {
    var gridSize = Math.max(maxDim * 3, 10);
    var newGrid = new THREE.GridHelper(gridSize, 40, 0x3a5a8a, 0x1e2e48);
    newGrid.material.transparent = true;
    newGrid.material.opacity = 0.55;
    newGrid.visible = gridHelper.visible;
    newGrid.rotation.copy(gridHelper.rotation);
    newGrid.position.set(center.x, box_bottomY(center, maxDim), center.z);
    scene.remove(gridHelper);
    gridHelper.geometry.dispose();
    gridHelper.material.dispose();
    gridHelper = newGrid;
    scene.add(gridHelper);
  }
  if (window.__groundMesh) {
    window.__groundMesh.geometry.dispose();
    window.__groundMesh.geometry = new THREE.CircleGeometry(Math.max(maxDim * 2.5, 10), 64);
    window.__groundMesh.position.set(center.x, box_bottomY(center, maxDim) - 0.001, center.z);
  }

  var axisLen = Math.max(maxDim * 0.6, 1);
  if (axesHelper) axesHelper.scale.setScalar(axisLen / 25);
  positionAxisLabels(axisLen);
}

// Grid/ground sit at the bottom of the model's bounding box rather than always
// at world Y=0, so they still read as a ""floor"" when the up-axis differs or the
// model doesn't straddle the origin.
function box_bottomY(center, maxDim) {
  return center.y - maxDim * 0.001; // effectively at center; kept as a hook for future per-axis floor placement
}

function makeTextSprite(text, color, scale) {
  var canvas = document.createElement('canvas');
  canvas.width = 128; canvas.height = 128;
  var ctx = canvas.getContext('2d');
  ctx.clearRect(0, 0, 128, 128);
  ctx.font = 'bold 96px Segoe UI, sans-serif';
  ctx.fillStyle = color;
  ctx.strokeStyle = 'rgba(0,0,0,0.65)';
  ctx.lineWidth = 10;
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.strokeText(text, 64, 68);
  ctx.fillText(text, 64, 68);
  var tex = new THREE.CanvasTexture(canvas);
  tex.needsUpdate = true;
  var mat = new THREE.SpriteMaterial({ map: tex, depthTest: false, transparent: true, sizeAttenuation: true });
  var sprite = new THREE.Sprite(mat);
  sprite.renderOrder = 999;
  var s = scale || 1;
  sprite.scale.set(s, s, s);
  return sprite;
}

// World-space X/Y/Z labels at the tips of the AxesHelper so the axes are
// identifiable at a glance instead of three unlabeled colored lines.
function buildAxisLabels(initialLen) {
  axisLabelSprites.forEach(function(s) { scene.remove(s); });
  axisLabelSprites = [];
  var defs = [
    { text: 'X', color: '#ff5555' },
    { text: 'Y', color: '#55ff55' },
    { text: 'Z', color: '#5599ff' }
  ];
  defs.forEach(function(d) {
    var sp = makeTextSprite(d.text, d.color, Math.max(initialLen * 0.12, 1));
    scene.add(sp);
    axisLabelSprites.push(sp);
  });
  positionAxisLabels(initialLen);
}

function positionAxisLabels(axisLen) {
  if (axisLabelSprites.length < 3) return;
  var pad = axisLen * 1.08;
  axisLabelSprites[0].position.set(pad, 0, 0);
  axisLabelSprites[1].position.set(0, pad, 0);
  axisLabelSprites[2].position.set(0, 0, pad);
  var s = Math.max(axisLen * 0.16, 0.5);
  axisLabelSprites.forEach(function(sp) { sp.scale.set(s, s, s); });
}

// See the note where this is registered on controls' 'change' event: OrbitControls'
// dolly scales the remaining camera-to-target distance, so it can get arbitrarily
// close but never actually reach/cross the target — it just keeps slowing down,
// which reads as ""stuck"" on large models where cm-scale steps become imperceptible.
// Once we're inside the comfort threshold, walk the pivot forward so the next
// scroll/drag has real distance to work with again — like advancing a Navisworks
// walk camera instead of dollying toward a fixed point.
function maybeAdvanceWalkTarget() {
  var dist = camera.position.distanceTo(controls.target);
  if (dist < walkAdvanceThreshold) {
    var dirVec = new THREE.Vector3();
    camera.getWorldDirection(dirVec);
    var step = Math.max(walkAdvanceThreshold * 2, 0.05);
    controls.target.addScaledVector(dirVec, step);
  }
}

function raycastModel(ev) {
  var rect = renderer.domElement.getBoundingClientRect();
  var mouse = new THREE.Vector2(
    ((ev.clientX - rect.left) / rect.width) * 2 - 1,
    -((ev.clientY - rect.top) / rect.height) * 2 + 1
  );
  var raycaster = new THREE.Raycaster();
  raycaster.setFromCamera(mouse, camera);
  var hits = raycaster.intersectObjects(rootGroup.children, true);
  return hits.length ? hits[0] : null;
}

function onPointerDown(ev) {
  if (currentTool !== 'measure') return;
  if (ev.button !== 0) return;
  var hit = raycastModel(ev);
  if (!hit) return;

  if (measureMode === 'radius') onRadiusClick(hit);
  else if (measureMode === 'face') onFaceClick(hit);
  else onPointClick(hit);
}

function onPointClick(hit) {
  var p = hit.point.clone();
  measurePts.push(p);
  addMarker(p);
  if (measurePts.length === 1) {
    updateMeasurePrompt();
  } else if (measurePts.length >= 2) {
    var a = measurePts[0], b = measurePts[1];
    var d = a.distanceTo(b);
    var dx = Math.abs(b.x - a.x), dy = Math.abs(b.y - a.y), dz = Math.abs(b.z - a.z);
    var label = L_DIST + ': ' + d.toFixed(3) + ' cm   ΔX ' + dx.toFixed(2) +
      '  ΔY ' + dy.toFixed(2) + '  ΔZ ' + dz.toFixed(2) + ' cm';
    measureText.textContent = label;
    drawMeasureLine(a, b);
    addResultLabel(label.split('   ')[0], a.clone().lerp(b, 0.5));
    measurePts = [];
  }
}

// Fits a circle through 3 clicked points (assumed to lie on a circular edge or
// hole boundary) and reports its radius/diameter. There's no true B-rep face
// data available client-side (the model is a plain triangle mesh), so a
// 3-point circle fit is the standard practical stand-in CAD viewers use when
// they only have geometry, not topology.
function onRadiusClick(hit) {
  var p = hit.point.clone();
  measurePts.push(p);
  addMarker(p);
  updateMeasurePrompt();
  if (measurePts.length < 3) return;

  var fit = circleFrom3Points(measurePts[0], measurePts[1], measurePts[2]);
  measurePts = [];
  if (!fit) {
    measureText.textContent = L_BAD_CIRCLE;
    return;
  }
  var label = L_RADIUS + ': ' + fit.radius.toFixed(3) + ' cm   ' +
    L_DIAMETER + ': ' + (fit.radius * 2).toFixed(3) + ' cm';
  measureText.textContent = label;
  drawMeasureCircle(fit);
  addResultLabel('R ' + fit.radius.toFixed(2) + ' / ⌀' + (fit.radius * 2).toFixed(2), fit.center);
}

function circleFrom3Points(p1, p2, p3) {
  var v1 = p2.clone().sub(p1), v2 = p3.clone().sub(p1);
  var normal = v1.clone().cross(v2);
  if (normal.lengthSq() < 1e-12) return null; // collinear
  normal.normalize();
  var u = v1.clone().normalize();
  var v = normal.clone().cross(u).normalize();
  function to2D(p) { var d = p.clone().sub(p1); return { x: d.dot(u), y: d.dot(v) }; }
  var a = { x: 0, y: 0 }, b = to2D(p2), c = to2D(p3);
  var D = 2 * (a.x * (b.y - c.y) + b.x * (c.y - a.y) + c.x * (a.y - b.y));
  if (Math.abs(D) < 1e-9) return null;
  var ux = ((a.x * a.x + a.y * a.y) * (b.y - c.y) + (b.x * b.x + b.y * b.y) * (c.y - a.y) + (c.x * c.x + c.y * c.y) * (a.y - b.y)) / D;
  var uy = ((a.x * a.x + a.y * a.y) * (c.x - b.x) + (b.x * b.x + b.y * b.y) * (a.x - c.x) + (c.x * c.x + c.y * c.y) * (b.x - a.x)) / D;
  var radius = Math.sqrt((ux - a.x) * (ux - a.x) + (uy - a.y) * (uy - a.y));
  if (!isFinite(radius) || radius <= 1e-6) return null;
  var center = p1.clone().addScaledVector(u, ux).addScaledVector(v, uy);
  return { center: center, radius: radius, normal: normal, u: u, v: v };
}

function drawMeasureCircle(fit) {
  var segs = 64;
  var pts = [];
  for (var i = 0; i <= segs; i++) {
    var t = (i / segs) * Math.PI * 2;
    pts.push(fit.center.clone()
      .addScaledVector(fit.u, Math.cos(t) * fit.radius)
      .addScaledVector(fit.v, Math.sin(t) * fit.radius));
  }
  var geo = new THREE.BufferGeometry().setFromPoints(pts);
  var line = new THREE.Line(geo, new THREE.LineBasicMaterial({ color: 0xffcc33 }));
  scene.add(line);
  measureHistory.push(line);
}

// Approximates a ""surface to surface"" distance: the first click establishes a
// plane (hit point + triangle normal), the second click's point is projected
// onto that plane's normal to get the perpendicular gap. Works well for the
// common case of flat/near-flat faces (plates, flanges, walls).
function onFaceClick(hit) {
  if (measurePts.length === 0) {
    var normal = faceWorldNormal(hit);
    measurePts.push({ point: hit.point.clone(), normal: normal });
    addMarker(hit.point);
    updateMeasurePrompt();
    return;
  }
  var first = measurePts[0];
  var p2 = hit.point.clone();
  addMarker(p2);
  var dist = Math.abs(p2.clone().sub(first.point).dot(first.normal));
  var label = L_FACE_DIST + ': ' + dist.toFixed(3) + ' cm';
  measureText.textContent = label;
  drawMeasureLine(first.point, p2);
  addResultLabel(label.split(':')[1].trim(), first.point.clone().lerp(p2, 0.5));
  measurePts = [];
}

function faceWorldNormal(hit) {
  var n = new THREE.Vector3(0, 1, 0);
  try {
    if (hit.face && hit.face.normal) {
      n.copy(hit.face.normal);
      var normalMatrix = new THREE.Matrix3().getNormalMatrix(hit.object.matrixWorld);
      n.applyMatrix3(normalMatrix).normalize();
    }
  } catch (e) {}
  return n;
}

// Floating always-facing-camera text near a completed measurement so the
// number is readable directly in the 3D view, not just in the top HUD strip.
function addResultLabel(text, worldPos) {
  var dist = camera.position.distanceTo(controls.target);
  var sprite = makeMeasureLabelSprite(text, '#ffe27a', Math.max(dist * 0.05, 0.5));
  sprite.position.copy(worldPos);
  scene.add(sprite);
  measureExtras.push(sprite);
}

function roundRectPath(ctx, x, y, w, h, r) {
  ctx.beginPath();
  ctx.moveTo(x + r, y);
  ctx.arcTo(x + w, y, x + w, y + h, r);
  ctx.arcTo(x + w, y + h, x, y + h, r);
  ctx.arcTo(x, y + h, x, y, r);
  ctx.arcTo(x, y, x + w, y, r);
  ctx.closePath();
}

// Pill-shaped, high-contrast label (dark background + light bold text) sized
// to fit the given string, used for in-scene measurement readouts. A plain
// transparent-background text sprite (fine for single-letter axis labels)
// gets lost against busy/light-colored geometry, which was the readability
// complaint with the old measurement text.
function makeMeasureLabelSprite(text, color, worldScale) {
  var fontSize = 42;
  var padX = 22, padY = 16;
  var measureCanvas = document.createElement('canvas');
  var mctx = measureCanvas.getContext('2d');
  mctx.font = 'bold ' + fontSize + 'px Segoe UI, sans-serif';
  var textWidth = mctx.measureText(text).width;

  var canvas = document.createElement('canvas');
  canvas.width = Math.ceil(textWidth + padX * 2);
  canvas.height = fontSize + padY * 2;
  var ctx = canvas.getContext('2d');
  ctx.font = 'bold ' + fontSize + 'px Segoe UI, sans-serif';

  ctx.fillStyle = 'rgba(6,11,20,0.92)';
  roundRectPath(ctx, 1, 1, canvas.width - 2, canvas.height - 2, 16);
  ctx.fill();
  ctx.strokeStyle = 'rgba(255,255,255,0.35)';
  ctx.lineWidth = 2;
  roundRectPath(ctx, 1, 1, canvas.width - 2, canvas.height - 2, 16);
  ctx.stroke();

  ctx.fillStyle = color;
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.fillText(text, canvas.width / 2, canvas.height / 2 + 2);

  var tex = new THREE.CanvasTexture(canvas);
  tex.needsUpdate = true;
  var mat = new THREE.SpriteMaterial({ map: tex, depthTest: false, transparent: true });
  var sprite = new THREE.Sprite(mat);
  sprite.renderOrder = 999;
  var aspect = canvas.width / canvas.height;
  var h = worldScale;
  sprite.scale.set(h * aspect, h, h);
  return sprite;
}

// Markers/lines from completed measurements persist (Navisworks-style running list)
// instead of being replaced by the next measurement — only the explicit Clear
// button empties them, so you can compare several distances at once.
function addMarker(p) {
  var s = new THREE.Mesh(
    new THREE.SphereGeometry(Math.max(0.15, camera.position.distanceTo(controls.target) * 0.008), 16, 12),
    new THREE.MeshBasicMaterial({ color: 0xffcc33 })
  );
  s.position.copy(p);
  scene.add(s);
  measureMarkers.push(s);
}

function drawMeasureLine(a, b) {
  var geo = new THREE.BufferGeometry().setFromPoints([a, b]);
  var line = new THREE.Line(geo, new THREE.LineBasicMaterial({ color: 0x9fe870 }));
  scene.add(line);
  measureHistory.push(line);
}

function clearMeasure() {
  measurePts = [];
  measureHistory.forEach(function(l) { scene.remove(l); l.geometry.dispose(); });
  measureHistory = [];
  measureMarkers.forEach(function(m) { scene.remove(m); m.geometry.dispose(); });
  measureMarkers = [];
  measureExtras.forEach(function(s) { scene.remove(s); if (s.material && s.material.map) s.material.map.dispose(); if (s.material) s.material.dispose(); });
  measureExtras = [];
  updateMeasurePrompt();
}
");
            return sb.ToString();
        }

        private static string JsonStr(string s)
        {
            if (s == null) s = "";
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n") + "\"";
        }
    }
}
