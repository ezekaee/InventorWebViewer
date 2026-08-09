using System.Drawing;
using System.Windows.Forms;

namespace InventorWebViewer.UI
{
    public static class InventorTheme
    {
        public static readonly Color HeaderBack = Color.FromArgb(30, 41, 59);
        public static readonly Color HeaderFore = Color.White;
        public static readonly Color ContentBack = Color.FromArgb(241, 245, 249);
        public static readonly Color PanelBack = Color.White;
        public static readonly Color Accent = Color.FromArgb(14, 165, 233);
        public static readonly Color AccentHover = Color.FromArgb(2, 132, 199);
        public static readonly Color Border = Color.FromArgb(203, 213, 225);
        public static readonly Color TextPrimary = Color.FromArgb(15, 23, 42);
        public static readonly Color TextSecondary = Color.FromArgb(100, 116, 139);
        public static readonly Color Success = Color.FromArgb(16, 124, 16);
        public static readonly Color Danger = Color.FromArgb(196, 43, 28);

        public static readonly Font FontRegular = new Font("Segoe UI", 9F, FontStyle.Regular);
        public static readonly Font FontSemiBold = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        public static readonly Font FontHeader = new Font("Segoe UI", 12F, FontStyle.Bold);
        public static readonly Font FontMono = new Font("Consolas", 8.5F);

        public static void ApplyFormBase(Form form, string title)
        {
            form.Text = title;
            form.BackColor = ContentBack;
            form.Font = FontRegular;
            form.ForeColor = TextPrimary;
            form.StartPosition = FormStartPosition.CenterScreen;
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.MaximizeBox = false;
            form.MinimizeBox = false;
            form.ShowInTaskbar = false;
            form.AutoScaleMode = AutoScaleMode.Dpi;
        }

        public static Panel CreateHeader(string title, int width, int height = 48)
        {
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = height,
                BackColor = HeaderBack,
                Padding = new Padding(16, 0, 16, 0)
            };
            var lbl = new Label
            {
                Text = title,
                ForeColor = HeaderFore,
                Font = FontHeader,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            header.Controls.Add(lbl);
            return header;
        }

        public static Button CreatePrimaryButton(string text, int width = 110, int height = 30)
        {
            var btn = new Button
            {
                Text = text,
                Size = new Size(width, height),
                FlatStyle = FlatStyle.Flat,
                BackColor = Accent,
                ForeColor = Color.White,
                Font = FontSemiBold,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = AccentHover;
            return btn;
        }

        public static Button CreateSecondaryButton(string text, int width = 100, int height = 30)
        {
            var btn = new Button
            {
                Text = text,
                Size = new Size(width, height),
                FlatStyle = FlatStyle.Flat,
                BackColor = PanelBack,
                ForeColor = TextPrimary,
                Font = FontRegular,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = Border;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(235, 238, 242);
            return btn;
        }

        public static GroupBox CreateGroup(string title)
        {
            return new GroupBox
            {
                Text = title,
                Font = FontSemiBold,
                ForeColor = TextPrimary,
                BackColor = PanelBack,
                Padding = new Padding(12, 8, 12, 12)
            };
        }

        public static void StyleTextBox(System.Windows.Forms.TextBox tb)
        {
            tb.BorderStyle = BorderStyle.FixedSingle;
            tb.BackColor = Color.White;
            tb.ForeColor = TextPrimary;
            tb.Font = FontRegular;
        }

        public static void StyleCombo(ComboBox cmb)
        {
            cmb.FlatStyle = FlatStyle.Flat;
            cmb.BackColor = Color.White;
            cmb.ForeColor = TextPrimary;
            cmb.Font = FontRegular;
        }

        public static void StyleCheckBox(CheckBox chk)
        {
            chk.ForeColor = TextPrimary;
            chk.Font = FontRegular;
            chk.BackColor = Color.Transparent;
            chk.AutoSize = true;
        }

        public static Panel CreateFooter(int height = 52)
        {
            return new Panel
            {
                Dock = DockStyle.Bottom,
                Height = height,
                BackColor = PanelBack,
                Padding = new Padding(12, 10, 12, 10)
            };
        }
    }
}
