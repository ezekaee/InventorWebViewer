using System;
using System.Text;
using IOPath = System.IO.Path;
using IODirectory = System.IO.Directory;
using IOFile = System.IO.File;

namespace InventorWebViewer.Core
{
    public class AppSettings
    {
        public string Language { get; set; } = "en";
        public string LinkedInUrl { get; set; } = "https://www.linkedin.com/in/zekaee/";
        public string WhatsAppNumber { get; set; } = "989305741740"; // without +
        public string WhatsAppMessage { get; set; } = "Hi, about Inventor Web Viewer (FREE)";
        public string Email { get; set; } = "e.zekaee.b@gmail.com";
        public bool IsFree { get; set; } = true;
        public string DefaultUpAxis { get; set; } = "Z";
        public bool OpenBrowserAfterExport { get; set; } = true;
        public double TessellationTolerance { get; set; } = 0.25;

        private static string SettingsFolder => IOPath.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
            "InventorWebViewer");

        private static string SettingsPath => IOPath.Combine(SettingsFolder, "settings.ini");

        public static AppSettings Load()
        {
            var s = new AppSettings();
            try
            {
                if (!IOFile.Exists(SettingsPath)) return s;
                foreach (var line in IOFile.ReadAllLines(SettingsPath, Encoding.UTF8))
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || !line.Contains("=")) continue;
                    var i = line.IndexOf('=');
                    var key = line.Substring(0, i).Trim();
                    var val = line.Substring(i + 1).Trim();
                    switch (key)
                    {
                        case "Language": s.Language = val; break;
                        case "LinkedInUrl": s.LinkedInUrl = val; break;
                        case "WhatsAppNumber": s.WhatsAppNumber = val; break;
                        case "WhatsAppMessage": s.WhatsAppMessage = val; break;
                        case "Email": s.Email = val; break;
                        case "DefaultUpAxis": s.DefaultUpAxis = val; break;
                        case "OpenBrowserAfterExport":
                            s.OpenBrowserAfterExport = val == "1" || val.Equals("true", StringComparison.OrdinalIgnoreCase);
                            break;
                        case "TessellationTolerance":
                            if (double.TryParse(val, System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out var t))
                                s.TessellationTolerance = t;
                            break;
                    }
                }
            }
            catch { }
            return s;
        }

        public void Save()
        {
            try
            {
                IODirectory.CreateDirectory(SettingsFolder);
                var sb = new StringBuilder();
                sb.AppendLine("Language=" + (Language ?? "en"));
                sb.AppendLine("LinkedInUrl=" + (LinkedInUrl ?? ""));
                sb.AppendLine("WhatsAppNumber=" + (WhatsAppNumber ?? ""));
                sb.AppendLine("WhatsAppMessage=" + (WhatsAppMessage ?? ""));
                sb.AppendLine("Email=" + (Email ?? ""));
                sb.AppendLine("DefaultUpAxis=" + (DefaultUpAxis ?? "Z"));
                sb.AppendLine("OpenBrowserAfterExport=" + (OpenBrowserAfterExport ? "1" : "0"));
                sb.AppendLine("TessellationTolerance=" + TessellationTolerance.ToString(System.Globalization.CultureInfo.InvariantCulture));
                IOFile.WriteAllText(SettingsPath, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }
    }
}
