using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Threading;

namespace InventorWebViewer
{
    /// <summary>
    /// Localization facade. Tries standard .resx (Resources / Resources.fa)
    /// with in-memory dictionary fallback so the add-in still works if satellite
    /// assemblies are missing.
    /// </summary>
    public static class Loc
    {
        private static ResourceManager _rm;
        private static CultureInfo _culture = CultureInfo.InvariantCulture;
        private static string _lang = "en";

        private static readonly Dictionary<string, string> En = new Dictionary<string, string>
        {
            ["Title_Main"] = "Inventor Web 3D Viewer",
            ["Title_Error"] = "Error",
            ["Title_Settings"] = "Settings",
            ["Title_About"] = "About",
            ["Btn_Export"] = "Export to HTML Viewer",
            ["Btn_Settings"] = "Settings",
            ["Btn_About"] = "About",
            ["Btn_Close"] = "Close",
            ["Btn_Save"] = "Save",
            ["Btn_Cancel"] = "Cancel",
            ["Btn_OpenFolder"] = "Open Output Folder",
            ["Lbl_Assembly"] = "Assembly:",
            ["Lbl_UpAxis"] = "Up axis:",
            ["Lbl_Output"] = "Output folder:",
            ["Lbl_Tolerance"] = "Mesh tolerance cm (higher = smaller file):",
            ["Lbl_Progress"] = "Progress:",
            ["Lbl_Log"] = "Log",
            ["Lbl_Language"] = "Language / زبان:",
            ["Lbl_LinkedIn"] = "LinkedIn URL:",
            ["Lbl_WhatsApp"] = "WhatsApp number (no +):",
            ["Lbl_WhatsAppMsg"] = "WhatsApp preset message:",
            ["Chk_OpenBrowser"] = "Open browser after export",
            ["Msg_NeedAssembly"] = "Please open a top-level assembly or a part document first.",
            ["Msg_Done"] = "Export finished. Viewer is ready.",
            ["Msg_Exporting"] = "Exporting geometry and building HTML viewer...",
            ["About_Text"] = "Inventor Web 3D Viewer v1.1 — FREE\n\nExports assembly to HTML + Three.js viewer.\nThis software is free to use and distribute.\nSupport me on social media!\n\nContact:\nLinkedIn: linkedin.com/in/zekaee\nEmail: e.zekaee.b@gmail.com\nWhatsApp: +98 930 574 1740",
            ["Panel_Name"] = "Web 3D Viewer",
            ["Tooltip_Export"] = "Export assembly to interactive HTML 3D viewer",
            ["Axis_Z"] = "+Z Up (default CAD)",
            ["Axis_Y"] = "+Y Up (glTF / many engines)",
            ["Axis_X"] = "+X Up",
            ["Axis_NegZ"] = "-Z Up",
            ["Axis_NegY"] = "-Y Up",
            ["Axis_NegX"] = "-X Up",
        };

        private static readonly Dictionary<string, string> Fa = new Dictionary<string, string>
        {
            ["Title_Main"] = "نمایشگر سه‌بعدی وب Inventor",
            ["Title_Error"] = "خطا",
            ["Title_Settings"] = "تنظیمات",
            ["Title_About"] = "درباره",
            ["Btn_Export"] = "خروجی به نمایشگر HTML",
            ["Btn_Settings"] = "تنظیمات",
            ["Btn_About"] = "درباره",
            ["Btn_Close"] = "بستن",
            ["Btn_Save"] = "ذخیره",
            ["Btn_Cancel"] = "انصراف",
            ["Btn_OpenFolder"] = "باز کردن پوشه خروجی",
            ["Lbl_Assembly"] = "اسمبلی:",
            ["Lbl_UpAxis"] = "محور بالا:",
            ["Lbl_Output"] = "پوشه خروجی:",
            ["Lbl_Tolerance"] = "تلرانس مش cm (بیشتر = فایل کوچک‌تر):",
            ["Lbl_Progress"] = "پیشرفت:",
            ["Lbl_Log"] = "گزارش",
            ["Lbl_Language"] = "Language / زبان:",
            ["Lbl_LinkedIn"] = "آدرس LinkedIn:",
            ["Lbl_WhatsApp"] = "شماره واتساپ (بدون +):",
            ["Lbl_WhatsAppMsg"] = "پیام پیش‌فرض واتساپ:",
            ["Chk_OpenBrowser"] = "باز کردن مرورگر پس از خروجی",
            ["Msg_NeedAssembly"] = "لطفاً ابتدا یک اسمبلی بالادست یا یک فایل پارت باز کنید.",
            ["Msg_Done"] = "خروجی تمام شد. نمایشگر آماده است.",
            ["Msg_Exporting"] = "در حال خروجی هندسه و ساخت نمایشگر HTML...",
            ["About_Text"] = "نمایشگر سه‌بعدی وب Inventor نسخه ۱.۱ — رایگان\n\nاسمبلی را به نمایشگر HTML + Three.js تبدیل می‌کند.\nاین نرم‌افزار رایگان است.\nمن را در شبکه‌های اجتماعی حمایت کنید!\n\nتماس:\nLinkedIn: linkedin.com/in/zekaee\nایمیل: e.zekaee.b@gmail.com\nواتساپ: +۹۸ ۹۳۰ ۵۷۴ ۱۷۴۰",
            ["Panel_Name"] = "نمایشگر وب ۳بعدی",
            ["Tooltip_Export"] = "خروجی اسمبلی به نمایشگر تعاملی HTML سه‌بعدی",
            ["Axis_Z"] = "+Z بالا (پیش‌فرض CAD)",
            ["Axis_Y"] = "+Y بالا (glTF / بسیاری موتورها)",
            ["Axis_X"] = "+X بالا",
            ["Axis_NegZ"] = "-Z بالا",
            ["Axis_NegY"] = "-Y بالا",
            ["Axis_NegX"] = "-X بالا",
        };

        static Loc()
        {
            var asm = Assembly.GetExecutingAssembly();
            // Try common logical names produced by SDK-style projects
            string[] candidates =
            {
                "InventorWebViewer.Localization.Resources",
                "InventorWebViewer.Resources",
                "Localization.Resources",
            };
            foreach (var name in candidates)
            {
                try
                {
                    var rm = new ResourceManager(name, asm);
                    // Probe one known key; if it throws MissingManifestResourceException, try next
                    rm.GetString("Title_Main", CultureInfo.InvariantCulture);
                    _rm = rm;
                    break;
                }
                catch
                {
                    _rm = null;
                }
            }
        }

        public static bool IsPersian => _lang == "fa";

        public static void SetLanguage(string lang)
        {
            _lang = (lang != null && lang.ToLowerInvariant() == "fa") ? "fa" : "en";
            try
            {
                _culture = _lang == "fa"
                    ? CultureInfo.GetCultureInfo("fa-IR")
                    : CultureInfo.InvariantCulture;
                Thread.CurrentThread.CurrentUICulture = _culture;
            }
            catch
            {
                _culture = CultureInfo.InvariantCulture;
            }
        }

        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;

            if (_rm != null)
            {
                try
                {
                    var fromResx = _rm.GetString(key, _culture);
                    if (!string.IsNullOrEmpty(fromResx)) return fromResx;
                    fromResx = _rm.GetString(key, CultureInfo.InvariantCulture);
                    if (!string.IsNullOrEmpty(fromResx)) return fromResx;
                }
                catch { /* fall through to dictionary */ }
            }

            var dict = _lang == "fa" ? Fa : En;
            return dict.TryGetValue(key, out var value) ? value : key;
        }
    }
}
