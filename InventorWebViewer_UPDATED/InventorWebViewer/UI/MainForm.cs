using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;
using WinForms = System.Windows.Forms;
using Inventor;
using InventorWebViewer.Core;
using IOPath = System.IO.Path;
using IODirectory = System.IO.Directory;
using DrawPoint = System.Drawing.Point;
using DrawSize = System.Drawing.Size;

namespace InventorWebViewer.UI
{
    public class MainForm : Form
    {
        private readonly Inventor.Application _invApp;
        private readonly Document _asmDoc;
        private AppSettings _settings;

        private WinForms.TextBox txtOutput;
        private WinForms.TextBox txtTolerance;
        private CheckBox chkOpenBrowser;
        private Button btnExport;
        private Button btnBrowse;
        private Button btnOpenFolder;
        private Button btnSettings;
        private Button btnAbout;
        private Button btnClose;
        private WinForms.ProgressBar progressBar;
        private Label lblStatus;
        private Label lblAsmName;
        private WinForms.TextBox txtLog;

        public MainForm(Inventor.Application invApp, Document asmDoc)
        {
            _invApp = invApp;
            _asmDoc = asmDoc;
            _settings = AppSettings.Load();
            Loc.SetLanguage(_settings.Language);
            BuildUi();
            LoadSettingsToUI();
        }

        private void BuildUi()
        {
            InventorTheme.ApplyFormBase(this, Loc.Get("Title_Main"));
            this.ClientSize = new DrawSize(680, 620);
            this.MinimumSize = new DrawSize(640, 520);

            var header = InventorTheme.CreateHeader(Loc.Get("Title_Main"), 680, 52);
            this.Controls.Add(header);

            var footer = InventorTheme.CreateFooter(56);
            btnExport = InventorTheme.CreatePrimaryButton(Loc.Get("Btn_Export"), 180, 32);
            btnExport.Click += BtnExport_Click;
            btnClose = InventorTheme.CreateSecondaryButton(Loc.Get("Btn_Close"), 90, 32);
            btnClose.Click += (s, e) => Close();
            btnSettings = InventorTheme.CreateSecondaryButton(Loc.Get("Btn_Settings"), 100, 32);
            btnSettings.Click += BtnSettings_Click;
            btnAbout = InventorTheme.CreateSecondaryButton(Loc.Get("Btn_About"), 90, 32);
            btnAbout.Click += BtnAbout_Click;
            btnOpenFolder = InventorTheme.CreateSecondaryButton(Loc.Get("Btn_OpenFolder"), 140, 32);
            btnOpenFolder.Click += (s, e) =>
            {
                try
                {
                    var p = txtOutput.Text.Trim();
                    if (IODirectory.Exists(p))
                        Process.Start(new ProcessStartInfo { FileName = p, UseShellExecute = true });
                }
                catch { }
            };

            footer.Controls.Add(btnSettings);
            footer.Controls.Add(btnAbout);
            footer.Controls.Add(btnOpenFolder);
            footer.Controls.Add(btnClose);
            footer.Controls.Add(btnExport);
            btnSettings.Location = new DrawPoint(12, 12);
            btnAbout.Location = new DrawPoint(120, 12);
            footer.Resize += (s, e) =>
            {
                btnExport.Location = new DrawPoint(footer.ClientSize.Width - btnExport.Width - 12, 12);
                btnClose.Location = new DrawPoint(btnExport.Left - btnClose.Width - 8, 12);
                btnOpenFolder.Location = new DrawPoint(btnClose.Left - btnOpenFolder.Width - 8, 12);
            };
            this.Controls.Add(footer);

            var content = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = InventorTheme.ContentBack,
                Padding = new Padding(16, 12, 16, 8),
                AutoScroll = true
            };
            this.Controls.Add(content);
            content.BringToFront();
            header.SendToBack();

            int y = 8;

            var infoPanel = new Panel
            {
                Location = new DrawPoint(0, y),
                Size = new DrawSize(640, 40),
                BackColor = InventorTheme.PanelBack,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            var isPartDoc = false;
            try { isPartDoc = _asmDoc.DocumentType == DocumentTypeEnum.kPartDocumentObject; } catch { }
            var lblInfoCap = new Label
            {
                Text = isPartDoc ? (_settings.Language == "fa" ? "پارت:" : "Part:") : Loc.Get("Lbl_Assembly"),
                Location = new DrawPoint(12, 11),
                AutoSize = true,
                Font = InventorTheme.FontSemiBold,
                ForeColor = InventorTheme.TextSecondary
            };
            lblAsmName = new Label
            {
                Text = IOPath.GetFileName(_asmDoc.FullFileName ?? ""),
                Location = new DrawPoint(120, 11),
                AutoSize = true,
                MaximumSize = new DrawSize(500, 0),
                ForeColor = InventorTheme.TextPrimary
            };
            infoPanel.Controls.Add(lblInfoCap);
            infoPanel.Controls.Add(lblAsmName);
            content.Controls.Add(infoPanel);
            y += 52;

            // Up-axis is controlled only inside the HTML viewer toolbar (not on this form).
            var grpOut = InventorTheme.CreateGroup(Loc.Get("Lbl_Output"));
            grpOut.Location = new DrawPoint(0, y);
            grpOut.Size = new DrawSize(640, 150);
            grpOut.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            txtOutput = new WinForms.TextBox { Location = new DrawPoint(16, 28), Width = 500 };
            InventorTheme.StyleTextBox(txtOutput);
            btnBrowse = InventorTheme.CreateSecondaryButton("…", 40, 26);
            btnBrowse.Location = new DrawPoint(530, 26);
            btnBrowse.Click += (s, e) =>
            {
                using (var dlg = new FolderBrowserDialog())
                {
                    dlg.SelectedPath = txtOutput.Text;
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                        txtOutput.Text = dlg.SelectedPath;
                }
            };

            // Row 2: tolerance — label and box with clear gap
            var lblTol = new Label
            {
                Text = Loc.Get("Lbl_Tolerance"),
                Location = new DrawPoint(16, 62),
                AutoSize = true,
                ForeColor = InventorTheme.TextSecondary
            };
            txtTolerance = new WinForms.TextBox { Location = new DrawPoint(16, 84), Width = 100, Text = "0.25" };
            InventorTheme.StyleTextBox(txtTolerance);

            // Row 3: checkbox alone so it never overlaps labels
            chkOpenBrowser = new CheckBox
            {
                Text = Loc.Get("Chk_OpenBrowser"),
                Location = new DrawPoint(16, 116),
                AutoSize = true
            };
            InventorTheme.StyleCheckBox(chkOpenBrowser);

            grpOut.Controls.AddRange(new Control[] { txtOutput, btnBrowse, lblTol, txtTolerance, chkOpenBrowser });
            content.Controls.Add(grpOut);
            y += 162;

            var lblProgress = new Label
            {
                Text = Loc.Get("Lbl_Progress"),
                Location = new DrawPoint(0, y),
                AutoSize = true,
                Font = InventorTheme.FontSemiBold,
                ForeColor = InventorTheme.TextSecondary
            };
            content.Controls.Add(lblProgress);
            y += 22;

            progressBar = new WinForms.ProgressBar
            {
                Location = new DrawPoint(0, y),
                Size = new DrawSize(640, 18),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            content.Controls.Add(progressBar);
            y += 26;

            lblStatus = new Label
            {
                Text = "",
                Location = new DrawPoint(0, y),
                AutoSize = true,
                ForeColor = InventorTheme.TextSecondary
            };
            content.Controls.Add(lblStatus);
            y += 24;

            var lblLog = new Label
            {
                Text = Loc.Get("Lbl_Log"),
                Location = new DrawPoint(0, y),
                AutoSize = true,
                Font = InventorTheme.FontSemiBold,
                ForeColor = InventorTheme.TextSecondary
            };
            content.Controls.Add(lblLog);
            y += 20;

            txtLog = new WinForms.TextBox
            {
                Location = new DrawPoint(0, y),
                Size = new DrawSize(640, 120),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Font = InventorTheme.FontMono,
                BackColor = System.Drawing.Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BorderStyle = BorderStyle.FixedSingle
            };
            content.Controls.Add(txtLog);

            footer.PerformLayout();
            btnExport.Location = new DrawPoint(footer.ClientSize.Width - btnExport.Width - 12, 12);
            btnClose.Location = new DrawPoint(btnExport.Left - btnClose.Width - 8, 12);
            btnOpenFolder.Location = new DrawPoint(btnClose.Left - btnOpenFolder.Width - 8, 12);
        }

        private void LoadSettingsToUI()
        {
            var defaultOut = IOPath.Combine(
                IOPath.GetDirectoryName(_asmDoc.FullFileName ?? System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments)),
                "WebViewer_" + IOPath.GetFileNameWithoutExtension(_asmDoc.FullFileName ?? "Assembly"));
            txtOutput.Text = defaultOut;
            txtTolerance.Text = _settings.TessellationTolerance.ToString(System.Globalization.CultureInfo.InvariantCulture);
            chkOpenBrowser.Checked = _settings.OpenBrowserAfterExport;
        }

        private async void BtnExport_Click(object sender, EventArgs e)
        {
            var outDir = txtOutput.Text.Trim();
            if (string.IsNullOrEmpty(outDir))
            {
                MessageBox.Show("Output folder required.", Loc.Get("Title_Error"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            double tol = 0.25;
            double.TryParse(txtTolerance.Text.Replace(',', '.'),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out tol);

            // Default up-axis for export; user changes it live in the HTML viewer
            _settings.DefaultUpAxis = "Z";
            _settings.OpenBrowserAfterExport = chkOpenBrowser.Checked;
            _settings.TessellationTolerance = tol;
            _settings.Save();

            btnExport.Enabled = false;
            progressBar.Value = 0;
            AppendLog(Loc.Get("Msg_Exporting"));

            var options = new ExportOptions
            {
                OutputFolder = outDir,
                UpAxis = "Z",
                OpenInBrowser = chkOpenBrowser.Checked,
                ChordTolerance = Math.Max(0.001, tol)
            };

            try
            {
                var exporter = new AssemblyExporter(_invApp, _settings);
                await Task.Run(() =>
                {
                    exporter.Export(_asmDoc, options,
                        (pct, msg) =>
                        {
                            if (IsDisposed) return;
                            BeginInvoke(new Action(() =>
                            {
                                progressBar.Value = Math.Min(100, Math.Max(0, pct));
                                lblStatus.Text = pct + "% – " + msg;
                            }));
                        },
                        msg =>
                        {
                            if (IsDisposed) return;
                            BeginInvoke(new Action(() => AppendLog(msg)));
                        });
                });

                MessageBox.Show(Loc.Get("Msg_Done"), Loc.Get("Title_Main"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                if (chkOpenBrowser.Checked)
                {
                    var index = IOPath.Combine(outDir, "index.html");
                    if (IOFileExists(index))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = index,
                            UseShellExecute = true
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Loc.Get("Title_Error"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                AppendLog("ERROR: " + ex.Message);
            }
            finally
            {
                btnExport.Enabled = true;
                progressBar.Value = 100;
                lblStatus.Text = "OK";
            }
        }

        private static bool IOFileExists(string path)
        {
            try { return System.IO.File.Exists(path); } catch { return false; }
        }

        private void BtnSettings_Click(object sender, EventArgs e)
        {
            using (var frm = new SettingsForm(_settings))
            {
                if (frm.ShowDialog(this) == DialogResult.OK)
                {
                    _settings = AppSettings.Load();
                    Loc.SetLanguage(_settings.Language);
                    this.Text = Loc.Get("Title_Main");
                }
            }
        }

        private void BtnAbout_Click(object sender, EventArgs e)
        {
            using (var frm = new AboutForm(_settings))
                frm.ShowDialog(this);
        }

        private void AppendLog(string message)
        {
            if (txtLog.IsDisposed) return;
            txtLog.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message + System.Environment.NewLine);
        }
    }
}
