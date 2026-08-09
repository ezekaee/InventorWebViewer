using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Inventor;
using InventorWebViewer.UI;
using InventorWebViewer.Core;

namespace InventorWebViewer
{
    [Guid("B7E8F901-2345-6789-ABCD-EF0123456789")]
    [ProgId("InventorWebViewer.StandardAddInServer")]
    [ComVisible(true)]
    public class StandardAddInServer : ApplicationAddInServer
    {
        private Inventor.Application _invApp;
        private ButtonDefinition _exportButton;
        private readonly string _clientId = "{B7E8F901-2345-6789-ABCD-EF0123456789}";

        public void Activate(ApplicationAddInSite addInSiteObject, bool firstTime)
        {
            try
            {
                _invApp = addInSiteObject.Application;
                var settings = AppSettings.Load();
                Loc.SetLanguage(settings.Language);

                Log("Activate firstTime=" + firstTime + " Inventor=" + Safe(() => _invApp.SoftwareVersion.DisplayName));

                EnsureButton();
                AddUiToRibbons();

                try
                {
                    _invApp.ApplicationEvents.OnActivateDocument += ApplicationEvents_OnActivateDocument;
                }
                catch (Exception ex)
                {
                    Log("OnActivateDocument hook failed: " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                Log("Activate FAILED: " + ex);
                try
                {
                    MessageBox.Show(
                        "Inventor Web Viewer failed to load:\n" + ex.Message +
                        "\n\nSee log:\n" + LogPath(),
                        "Web Viewer",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
                catch { }
            }
        }

        private void ApplicationEvents_OnActivateDocument(
            _Document documentObject,
            EventTimingEnum beforeOrAfter,
            NameValueMap context,
            out HandlingCodeEnum handlingCode)
        {
            handlingCode = HandlingCodeEnum.kEventNotHandled;
            if (beforeOrAfter != EventTimingEnum.kAfter) return;
            try { AddUiToRibbons(); } catch { }
        }

        private void EnsureButton()
        {
            var controlDefs = _invApp.CommandManager.ControlDefinitions;
            try
            {
                _exportButton = controlDefs["InventorWebViewer:ExportHtml"] as ButtonDefinition;
            }
            catch
            {
                _exportButton = null;
            }

            if (_exportButton == null)
            {
                _exportButton = controlDefs.AddButtonDefinition(
                    "Export to HTML Viewer",
                    "InventorWebViewer:ExportHtml",
                    CommandTypesEnum.kQueryOnlyCmdType,
                    _clientId,
                    "Export assembly to interactive HTML 3D viewer",
                    "Export to HTML Viewer",
                    Type.Missing,
                    Type.Missing,
                    ButtonDisplayEnum.kDisplayTextInLearningMode);

                _exportButton.OnExecute += ExportButton_OnExecute;
                Log("ButtonDefinition created.");
            }
        }

        private void AddUiToRibbons()
        {
            if (_exportButton == null) return;

            AddToCustomTab("Assembly", "id_TabIWV_WebViewer", "Web 3D Viewer");
            AddToCustomTab("Part", "id_TabIWV_WebViewer", "Web 3D Viewer");
            AddToCustomTab("Drawing", "id_TabIWV_WebViewer", "Web 3D Viewer");
            AddToCustomTab("ZeroDoc", "id_TabIWV_WebViewer", "Web 3D Viewer");

            AddToExistingTab("Assembly", "id_TabAssemble", "id_PanelIWV");
            AddToExistingTab("Assembly", "id_TabTools", "id_PanelIWV");
            AddToExistingTab("Part", "id_TabTools", "id_PanelIWV");
            AddToExistingTab("ZeroDoc", "id_GetStarted", "id_PanelIWV");
        }

        private void AddToCustomTab(string ribbonName, string tabId, string tabDisplayName)
        {
            try
            {
                Ribbon ribbon = _invApp.UserInterfaceManager.Ribbons[ribbonName];
                RibbonTab tab;
                try { tab = ribbon.RibbonTabs[tabId]; }
                catch { tab = ribbon.RibbonTabs.Add(tabDisplayName, tabId, _clientId); }

                RibbonPanel panel;
                try { panel = tab.RibbonPanels["id_PanelIWV_Main"]; }
                catch { panel = tab.RibbonPanels.Add("Viewer", "id_PanelIWV_Main", _clientId); }

                AddButtonIfMissing(panel);
            }
            catch (Exception ex)
            {
                Log("CustomTab " + ribbonName + ": " + ex.Message);
            }
        }

        private void AddToExistingTab(string ribbonName, string tabId, string panelId)
        {
            try
            {
                Ribbon ribbon = _invApp.UserInterfaceManager.Ribbons[ribbonName];
                RibbonTab tab = ribbon.RibbonTabs[tabId];
                RibbonPanel panel;
                try { panel = tab.RibbonPanels[panelId]; }
                catch { panel = tab.RibbonPanels.Add("Web 3D Viewer", panelId, _clientId); }
                AddButtonIfMissing(panel);
            }
            catch (Exception ex)
            {
                Log("ExistingTab " + ribbonName + "/" + tabId + ": " + ex.Message);
            }
        }

        private void AddButtonIfMissing(RibbonPanel panel)
        {
            foreach (CommandControl c in panel.CommandControls)
            {
                try
                {
                    if (c.InternalName == "InventorWebViewer:ExportHtml" ||
                        (c.ControlDefinition != null && c.ControlDefinition.InternalName == "InventorWebViewer:ExportHtml"))
                        return;
                }
                catch { }
            }
            panel.CommandControls.AddButton(_exportButton, true);
        }

        private void ExportButton_OnExecute(NameValueMap context)
        {
            var active = _invApp.ActiveDocument;
            if (active == null || active.DocumentType != DocumentTypeEnum.kAssemblyDocumentObject)
            {
                MessageBox.Show(Loc.Get("Msg_NeedAssembly"), Loc.Get("Title_Error"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                using (var mainForm = new MainForm(_invApp, (AssemblyDocument)active))
                    mainForm.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error:\n" + ex.Message, Loc.Get("Title_Error"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void Deactivate()
        {
            try
            {
                if (_invApp != null)
                    _invApp.ApplicationEvents.OnActivateDocument -= ApplicationEvents_OnActivateDocument;
            }
            catch { }

            if (_exportButton != null)
            {
                try { _exportButton.OnExecute -= ExportButton_OnExecute; } catch { }
                try { Marshal.ReleaseComObject(_exportButton); } catch { }
                _exportButton = null;
            }
            if (_invApp != null)
            {
                try { Marshal.ReleaseComObject(_invApp); } catch { }
                _invApp = null;
            }
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        public void ExecuteCommand(int commandID) { }
        public object Automation { get { return null; } }

        #region Logging
        private static string LogPath()
        {
            var dir = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                "InventorWebViewer");
            System.IO.Directory.CreateDirectory(dir);
            return System.IO.Path.Combine(dir, "addin_load.log");
        }

        private static void Log(string msg)
        {
            try
            {
                System.IO.File.AppendAllText(LogPath(),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + System.Environment.NewLine);
            }
            catch { }
        }

        private static string Safe(Func<string> f)
        {
            try { return f(); } catch { return "?"; }
        }
        #endregion
    }
}
