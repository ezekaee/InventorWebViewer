using System;
using System.Windows.Forms;
using InventorWebViewer.Core;
using DrawPoint = System.Drawing.Point;
using DrawSize = System.Drawing.Size;

namespace InventorWebViewer.UI
{
    public class SettingsForm : Form
    {
        private AppSettings _settings;
        private ComboBox cmbLanguage;
        private TextBox txtLinkedIn;
        private TextBox txtWhatsApp;
        private TextBox txtWhatsAppMsg;

        public SettingsForm(AppSettings settings)
        {
            _settings = settings;
            BuildUi();
            LoadValues();
        }

        private void BuildUi()
        {
            InventorTheme.ApplyFormBase(this, Loc.Get("Title_Settings"));
            this.ClientSize = new DrawSize(500, 380);

            var header = InventorTheme.CreateHeader(Loc.Get("Title_Settings"), 500, 48);
            this.Controls.Add(header);

            var footer = InventorTheme.CreateFooter(52);
            var btnSave = InventorTheme.CreatePrimaryButton(Loc.Get("Btn_Save"), 100, 32);
            btnSave.Click += BtnSave_Click;
            var btnCancel = InventorTheme.CreateSecondaryButton(Loc.Get("Btn_Cancel"), 100, 32);
            btnCancel.DialogResult = DialogResult.Cancel;
            footer.Controls.Add(btnSave);
            footer.Controls.Add(btnCancel);
            footer.Resize += (s, e) =>
            {
                btnSave.Location = new DrawPoint(footer.ClientSize.Width - btnSave.Width - 12, 10);
                btnCancel.Location = new DrawPoint(btnSave.Left - btnCancel.Width - 8, 10);
            };
            this.Controls.Add(footer);

            var content = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = InventorTheme.ContentBack,
                Padding = new Padding(16)
            };
            this.Controls.Add(content);
            content.BringToFront();

            int y = 8;
            content.Controls.Add(new Label
            {
                Text = Loc.Get("Lbl_Language"),
                Location = new DrawPoint(0, y),
                AutoSize = true,
                ForeColor = InventorTheme.TextSecondary
            });
            y += 22;
            cmbLanguage = new ComboBox
            {
                Location = new DrawPoint(0, y),
                Width = 220,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            InventorTheme.StyleCombo(cmbLanguage);
            cmbLanguage.Items.AddRange(new object[] { "English", "فارسی (FA)" });
            content.Controls.Add(cmbLanguage);
            y += 40;

            content.Controls.Add(new Label
            {
                Text = Loc.Get("Lbl_LinkedIn"),
                Location = new DrawPoint(0, y),
                AutoSize = true,
                ForeColor = InventorTheme.TextSecondary
            });
            y += 22;
            txtLinkedIn = new TextBox { Location = new DrawPoint(0, y), Width = 450 };
            InventorTheme.StyleTextBox(txtLinkedIn);
            content.Controls.Add(txtLinkedIn);
            y += 40;

            content.Controls.Add(new Label
            {
                Text = Loc.Get("Lbl_WhatsApp"),
                Location = new DrawPoint(0, y),
                AutoSize = true,
                ForeColor = InventorTheme.TextSecondary
            });
            y += 22;
            txtWhatsApp = new TextBox { Location = new DrawPoint(0, y), Width = 220 };
            InventorTheme.StyleTextBox(txtWhatsApp);
            content.Controls.Add(txtWhatsApp);
            y += 40;

            content.Controls.Add(new Label
            {
                Text = Loc.Get("Lbl_WhatsAppMsg"),
                Location = new DrawPoint(0, y),
                AutoSize = true,
                ForeColor = InventorTheme.TextSecondary
            });
            y += 22;
            txtWhatsAppMsg = new TextBox { Location = new DrawPoint(0, y), Width = 450 };
            InventorTheme.StyleTextBox(txtWhatsAppMsg);
            content.Controls.Add(txtWhatsAppMsg);

            btnSave.Location = new DrawPoint(footer.ClientSize.Width - btnSave.Width - 12, 10);
            btnCancel.Location = new DrawPoint(btnSave.Left - btnCancel.Width - 8, 10);
        }

        private void LoadValues()
        {
            cmbLanguage.SelectedIndex = _settings.Language == "fa" ? 1 : 0;
            txtLinkedIn.Text = _settings.LinkedInUrl ?? "";
            txtWhatsApp.Text = _settings.WhatsAppNumber ?? "";
            txtWhatsAppMsg.Text = _settings.WhatsAppMessage ?? "";
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            _settings.Language = cmbLanguage.SelectedIndex == 1 ? "fa" : "en";
            _settings.LinkedInUrl = txtLinkedIn.Text.Trim();
            _settings.WhatsAppNumber = txtWhatsApp.Text.Trim().TrimStart('+');
            _settings.WhatsAppMessage = txtWhatsAppMsg.Text.Trim();
            _settings.Save();
            Loc.SetLanguage(_settings.Language);
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
