using System;
using System.Diagnostics;
using System.Windows.Forms;
using InventorWebViewer.Core;
using DrawPoint = System.Drawing.Point;
using DrawSize = System.Drawing.Size;

namespace InventorWebViewer.UI
{
    public class AboutForm : Form
    {
        private readonly AppSettings _settings;

        public AboutForm(AppSettings settings)
        {
            _settings = settings;
            BuildUi();
        }

        private void BuildUi()
        {
            InventorTheme.ApplyFormBase(this, Loc.Get("Title_About"));
            this.ClientSize = new DrawSize(480, 360);

            var header = InventorTheme.CreateHeader(Loc.Get("Title_About"), 460, 48);
            this.Controls.Add(header);

            var footer = InventorTheme.CreateFooter(52);
            var btnClose = InventorTheme.CreatePrimaryButton(Loc.Get("Btn_Close"), 100, 32);
            btnClose.DialogResult = DialogResult.OK;
            footer.Controls.Add(btnClose);
            footer.Resize += (s, e) =>
            {
                btnClose.Location = new DrawPoint(footer.ClientSize.Width - btnClose.Width - 12, 10);
            };
            this.Controls.Add(footer);

            var content = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = InventorTheme.ContentBack,
                Padding = new Padding(20)
            };
            this.Controls.Add(content);
            content.BringToFront();

            var lbl = new Label
            {
                Text = Loc.Get("About_Text"),
                Location = new DrawPoint(0, 8),
                Size = new DrawSize(430, 160),
                AutoSize = false,
                ForeColor = InventorTheme.TextPrimary,
                Font = InventorTheme.FontRegular
            };
            content.Controls.Add(lbl);

            // Text hyperlinks (not buttons)
            var lnkLi = new LinkLabel
            {
                Text = "LinkedIn",
                Location = new DrawPoint(0, 175),
                AutoSize = true,
                LinkColor = InventorTheme.Accent,
                ActiveLinkColor = InventorTheme.AccentHover,
                VisitedLinkColor = InventorTheme.Accent
            };
            lnkLi.LinkClicked += (s, e) =>
            {
                try
                {
                    var url = string.IsNullOrWhiteSpace(_settings.LinkedInUrl)
                        ? "https://www.linkedin.com/in/zekaee/"
                        : _settings.LinkedInUrl;
                    Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                }
                catch { }
            };
            content.Controls.Add(lnkLi);

            var lnkWa = new LinkLabel
            {
                Text = "WhatsApp",
                Location = new DrawPoint(90, 175),
                AutoSize = true,
                LinkColor = InventorTheme.Accent,
                ActiveLinkColor = InventorTheme.AccentHover,
                VisitedLinkColor = InventorTheme.Accent
            };
            lnkWa.LinkClicked += (s, e) =>
            {
                try
                {
                    var num = (_settings.WhatsAppNumber ?? "989305741740").TrimStart('+');
                    var msg = Uri.EscapeDataString(_settings.WhatsAppMessage ?? "");
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://wa.me/" + num + "?text=" + msg,
                        UseShellExecute = true
                    });
                }
                catch { }
            };
            content.Controls.Add(lnkWa);

            var lnkMail = new LinkLabel
            {
                Text = "Email",
                Location = new DrawPoint(190, 175),
                AutoSize = true,
                LinkColor = InventorTheme.Accent,
                ActiveLinkColor = InventorTheme.AccentHover,
                VisitedLinkColor = InventorTheme.Accent
            };
            lnkMail.LinkClicked += (s, e) =>
            {
                try
                {
                    var em = string.IsNullOrWhiteSpace(_settings.Email) ? "e.zekaee.b@gmail.com" : _settings.Email;
                    Process.Start(new ProcessStartInfo { FileName = "mailto:" + em, UseShellExecute = true });
                }
                catch { }
            };
            content.Controls.Add(lnkMail);

            var ver = new Label
            {
                Text = "Version 1.0.0  |  FREE  |  Inventor 2025+",
                Location = new DrawPoint(0, 220),
                AutoSize = true,
                ForeColor = InventorTheme.TextSecondary
            };
            content.Controls.Add(ver);

            btnClose.Location = new DrawPoint(footer.ClientSize.Width - btnClose.Width - 12, 10);
        }
    }
}
