using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
namespace WinToggleSetup
{
    // ---------------------------------------------------------------------
    // Installer engine
    // ---------------------------------------------------------------------

    public static class Installer
    {
        public const string AppName = "WinToggle";
        public const string Version = "1.0.0";
        public const string ResourceName = "WinToggle.exe";

        public static string DefaultDir()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WinToggle");
        }

        public static string AppExe(string dir)
        {
            return Path.Combine(dir, "WinToggle.exe");
        }

        public static string SetupExe(string dir)
        {
            return Path.Combine(dir, "WinToggleSetup.exe");
        }

        public static void Install(string dir, bool desktopShortcut, bool startMenuShortcut, bool launch)
        {
            Directory.CreateDirectory(dir);

            // Extract the embedded application.
            Assembly asm = Assembly.GetExecutingAssembly();
            using (Stream src = asm.GetManifestResourceStream(ResourceName))
            {
                if (src == null)
                    throw new Exception("The embedded WinToggle application is missing from this installer.");

                string target = AppExe(dir);
                using (FileStream dst = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    byte[] buffer = new byte[81920];
                    int read;
                    while ((read = src.Read(buffer, 0, buffer.Length)) > 0)
                        dst.Write(buffer, 0, read);
                }
            }

            // Keep a copy of this installer next to the app so it can uninstall itself.
            string self = Application.ExecutablePath;
            string setupTarget = SetupExe(dir);
            if (!string.Equals(Path.GetFullPath(self), Path.GetFullPath(setupTarget), StringComparison.OrdinalIgnoreCase))
            {
                try { File.Copy(self, setupTarget, true); }
                catch { }
            }

            if (desktopShortcut)
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                CreateShortcut(Path.Combine(desktop, "WinToggle.lnk"), AppExe(dir), dir);
            }

            if (startMenuShortcut)
            {
                string programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                CreateShortcut(Path.Combine(programs, "WinToggle.lnk"), AppExe(dir), dir);
            }

            WriteUninstallEntry(dir);

            if (launch)
                Process.Start(AppExe(dir));
        }

        public static void Uninstall(string dir, bool silent)
        {
            if (!silent)
            {
                DialogResult r = MessageBox.Show(
                    "Remove WinToggle and all of its shortcuts from this computer?",
                    "WinToggle Setup", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (r != DialogResult.Yes) return;
            }

            try
            {
                foreach (Process p in Process.GetProcessesByName("WinToggle"))
                {
                    try { p.Kill(); } catch { }
                }
            }
            catch { }

            try
            {
                string desktop = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "WinToggle.lnk");
                if (File.Exists(desktop)) File.Delete(desktop);

                string programs = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Programs), "WinToggle.lnk");
                if (File.Exists(programs)) File.Delete(programs);
            }
            catch { }

            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(
                    @"Software\Microsoft\Windows\CurrentVersion\Uninstall\WinToggle", false);
            }
            catch { }

            // The installer may be running from inside the target folder, so let
            // cmd.exe delete the folder after this process exits.
            try
            {
                string cmd = "/c ping 127.0.0.1 -n 3 > nul & rmdir /s /q \"" + dir + "\"";
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", cmd);
                psi.CreateNoWindow = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                psi.UseShellExecute = false;
                Process.Start(psi);
            }
            catch { }

            if (!silent)
            {
                MessageBox.Show("WinToggle has been removed.",
                    "WinToggle Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        public static string InstalledDir()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Uninstall\WinToggle"))
                {
                    if (k != null)
                    {
                        object v = k.GetValue("InstallLocation");
                        if (v != null && Directory.Exists(Convert.ToString(v)))
                            return Convert.ToString(v);
                    }
                }
            }
            catch { }
            return DefaultDir();
        }

        private static void WriteUninstallEntry(string dir)
        {
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Uninstall\WinToggle"))
            {
                k.SetValue("DisplayName", "WinToggle", RegistryValueKind.String);
                k.SetValue("DisplayVersion", Version, RegistryValueKind.String);
                k.SetValue("Publisher", "WinToggle", RegistryValueKind.String);
                k.SetValue("InstallLocation", dir, RegistryValueKind.String);
                k.SetValue("DisplayIcon", AppExe(dir) + ",0", RegistryValueKind.String);
                k.SetValue("UninstallString", "\"" + SetupExe(dir) + "\" --uninstall", RegistryValueKind.String);
                k.SetValue("QuietUninstallString", "\"" + SetupExe(dir) + "\" --uninstall --silent", RegistryValueKind.String);
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                try
                {
                    long size = new FileInfo(AppExe(dir)).Length / 1024;
                    k.SetValue("EstimatedSize", (int)size, RegistryValueKind.DWord);
                }
                catch { }
            }
        }

        private static void CreateShortcut(string linkPath, string target, string workingDir)
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;

            object shell = Activator.CreateInstance(shellType);
            object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod,
                null, shell, new object[] { linkPath });
            Type t = shortcut.GetType();
            t.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { target });
            t.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { workingDir });
            t.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { "WinToggle - Windows, simplified." });
            t.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { target + ",0" });
            t.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);

            if (Marshal.IsComObject(shortcut)) Marshal.ReleaseComObject(shortcut);
            if (Marshal.IsComObject(shell)) Marshal.ReleaseComObject(shell);
        }
    }

    // ---------------------------------------------------------------------
    // Wizard UI
    // ---------------------------------------------------------------------

    public class WizardForm : Form
    {
        private static readonly Color Bg = Color.FromArgb(23, 23, 29);
        private static readonly Color Side = Color.FromArgb(30, 30, 38);
        private static readonly Color Card = Color.FromArgb(36, 36, 45);
        private static readonly Color Accent = Color.FromArgb(139, 92, 246);
        private static readonly Color TextMain = Color.FromArgb(240, 240, 245);
        private static readonly Color TextDim = Color.FromArgb(154, 154, 168);

        private Panel pageWelcome;
        private Panel pageOptions;
        private Panel pageDone;
        private Label statusLbl;
        private Button backBtn;
        private Button nextBtn;

        private TextBox locationBox;
        private CheckBox desktopChk;
        private CheckBox startChk;
        private CheckBox launchChk;

        private int page = 0;
        private string installDir;

        public WizardForm(string presetDir)
        {
            Text = "WinToggle Setup";
            ClientSize = new Size(620, 470);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Bg;
            ForeColor = TextMain;
            Font = new Font("Segoe UI", 9f);

            installDir = string.IsNullOrEmpty(presetDir) ? Installer.DefaultDir() : presetDir;

            BuildHeader();
            BuildPages();
            BuildFooter();
            ShowPage(0);
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int dark = 1;
            DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
            DwmSetWindowAttribute(Handle, 19, ref dark, sizeof(int));
        }

        private void BuildHeader()
        {
            Panel header = new Panel();
            header.BackColor = Bg;
            header.SetBounds(0, 0, ClientSize.Width, 86);
            Controls.Add(header);

            Label mark = new Label();
            mark.Text = "W";
            mark.Font = new Font("Segoe UI Semibold", 15f);
            mark.ForeColor = Color.White;
            mark.BackColor = Accent;
            mark.TextAlign = ContentAlignment.MiddleCenter;
            mark.SetBounds(32, 24, 38, 38);
            using (GraphicsPath p = Round(new Rectangle(0, 0, 38, 38), 10))
                mark.Region = new Region(p);
            header.Controls.Add(mark);

            Label title = new Label();
            title.Text = "WinToggle Setup";
            title.Font = new Font("Segoe UI Semibold", 14f);
            title.ForeColor = TextMain;
            title.AutoSize = false;
            title.SetBounds(82, 22, 300, 26);
            header.Controls.Add(title);

            Label sub = new Label();
            sub.Text = "Version " + Installer.Version + "  \u00B7  Windows, simplified.";
            sub.Font = new Font("Segoe UI", 8.5f);
            sub.ForeColor = TextDim;
            sub.AutoSize = false;
            sub.SetBounds(84, 48, 320, 20);
            header.Controls.Add(sub);
        }

        private void BuildPages()
        {
            pageWelcome = NewPage();
            pageOptions = NewPage();
            pageDone = NewPage();

            // ---- Welcome ----
            Label h1 = Heading("Welcome to WinToggle");
            h1.SetBounds(32, 8, 540, 34);
            pageWelcome.Controls.Add(h1);

            Label p1 = Body("A simple switchboard for Windows. Flip the toggles to change taskbar, Explorer, appearance and privacy settings \u2014 no admin rights, no registry editor.");
            p1.SetBounds(32, 54, 552, 78);
            pageWelcome.Controls.Add(p1);

            Label bullets = Body(
                "\u2022  21 one-click tweaks, from clock seconds to dark mode\n" +
                "\u2022  Installs only for your user account\n" +
                "\u2022  Uninstall anytime from Settings \u203A Apps");
            bullets.SetBounds(32, 146, 540, 80);
            pageWelcome.Controls.Add(bullets);

            Label warning = Body("WinToggle edits your own user registry (HKCU). You can reverse every change with the same switch.");
            warning.ForeColor = Color.FromArgb(130, 130, 146);
            warning.SetBounds(32, 238, 540, 44);
            pageWelcome.Controls.Add(warning);

            // ---- Options ----
            Label h2 = Heading("Install options");
            h2.SetBounds(32, 8, 540, 34);
            pageOptions.Controls.Add(h2);

            Label locLbl = Small("INSTALL LOCATION");
            locLbl.SetBounds(32, 60, 200, 16);
            pageOptions.Controls.Add(locLbl);

            locationBox = new TextBox();
            locationBox.Text = installDir;
            locationBox.SetBounds(32, 80, 434, 27);
            locationBox.BackColor = Card;
            locationBox.ForeColor = TextMain;
            locationBox.BorderStyle = BorderStyle.FixedSingle;
            pageOptions.Controls.Add(locationBox);

            Button browse = MakeButton("Browse\u2026", Card, TextMain);
            browse.SetBounds(476, 78, 108, 30);
            browse.Click += delegate
            {
                using (FolderBrowserDialog dlg = new FolderBrowserDialog())
                {
                    dlg.Description = "Choose where WinToggle should be installed";
                    dlg.SelectedPath = locationBox.Text;
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                        locationBox.Text = dlg.SelectedPath;
                }
            };
            pageOptions.Controls.Add(browse);

            desktopChk = MakeCheck("Create a desktop shortcut", 32, 136);
            desktopChk.Checked = true;
            pageOptions.Controls.Add(desktopChk);

            startChk = MakeCheck("Add WinToggle to the Start menu", 32, 170);
            startChk.Checked = true;
            pageOptions.Controls.Add(startChk);

            Label note = Small("WinToggle installs per-user into your local app data folder. No administrator rights are required.");
            note.SetBounds(32, 214, 552, 40);
            pageOptions.Controls.Add(note);

            Label note2 = Small("A copy of this setup is kept in the install folder so you can uninstall later.");
            note2.SetBounds(32, 250, 552, 24);
            pageOptions.Controls.Add(note2);

            // ---- Done ----
            Label h3 = Heading("You're all set!");
            h3.SetBounds(32, 8, 540, 34);
            pageDone.Controls.Add(h3);

            Label p3 = Body("WinToggle has been installed. Launch it below and start flipping switches.");
            p3.SetBounds(32, 54, 540, 44);
            pageDone.Controls.Add(p3);

            launchChk = MakeCheck("Launch WinToggle now", 32, 118);
            launchChk.Checked = true;
            pageDone.Controls.Add(launchChk);

            Label pathLbl = Small("Installed to: " + installDir);
            pathLbl.SetBounds(32, 168, 552, 40);
            pageDone.Controls.Add(pathLbl);
        }

        private Panel NewPage()
        {
            Panel p = new Panel();
            p.BackColor = Bg;
            p.SetBounds(0, 86, ClientSize.Width, 300);
            p.Visible = false;
            Controls.Add(p);
            return p;
        }

        private Label Heading(string text)
        {
            Label l = new Label();
            l.Text = text;
            l.Font = new Font("Segoe UI Semibold", 17f);
            l.ForeColor = TextMain;
            l.AutoSize = false;
            return l;
        }

        private Label Body(string text)
        {
            Label l = new Label();
            l.Text = text;
            l.Font = new Font("Segoe UI", 9.5f);
            l.ForeColor = TextDim;
            l.AutoSize = false;
            return l;
        }

        private Label Small(string text)
        {
            Label l = new Label();
            l.Text = text;
            l.Font = new Font("Segoe UI", 8.25f);
            l.ForeColor = TextDim;
            l.AutoSize = false;
            return l;
        }

        private CheckBox MakeCheck(string text, int x, int y)
        {
            CheckBox c = new CheckBox();
            c.Text = text;
            c.ForeColor = TextMain;
            c.BackColor = Bg;
            c.Font = new Font("Segoe UI", 9.5f);
            c.AutoSize = false;
            c.SetBounds(x, y, 500, 26);
            return c;
        }

        private Button MakeButton(string text, Color back, Color fore)
        {
            Button b = new Button();
            b.Text = text;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = back;
            b.ForeColor = fore;
            b.Font = new Font("Segoe UI Semibold", 9f);
            b.Cursor = Cursors.Hand;
            return b;
        }

        private void BuildFooter()
        {
            Panel footer = new Panel();
            footer.BackColor = Side;
            footer.SetBounds(0, ClientSize.Height - 84, ClientSize.Width, 84);
            Controls.Add(footer);

            statusLbl = new Label();
            statusLbl.Text = "";
            statusLbl.Font = new Font("Segoe UI", 8.5f);
            statusLbl.ForeColor = TextDim;
            statusLbl.AutoSize = false;
            statusLbl.SetBounds(32, 32, 260, 20);
            footer.Controls.Add(statusLbl);

            nextBtn = MakeButton("Continue", Accent, Color.White);
            nextBtn.SetBounds(ClientSize.Width - 32 - 132, 24, 132, 36);
            nextBtn.Click += delegate { NextClicked(); };
            footer.Controls.Add(nextBtn);

            backBtn = MakeButton("Back", Card, TextMain);
            backBtn.SetBounds(ClientSize.Width - 32 - 132 - 10 - 92, 24, 92, 36);
            backBtn.Click += delegate
            {
                if (page == 1) ShowPage(0);
            };
            footer.Controls.Add(backBtn);
        }

        private void ShowPage(int index)
        {
            page = index;
            pageWelcome.Visible = index == 0;
            pageOptions.Visible = index == 1;
            pageDone.Visible = index == 2;

            backBtn.Visible = index == 1;
            if (index == 0)
            {
                nextBtn.Text = "Continue";
                nextBtn.Enabled = true;
            }
            else if (index == 1)
            {
                nextBtn.Text = "Install";
                nextBtn.Enabled = true;
            }
            else
            {
                nextBtn.Text = "Finish";
                nextBtn.Enabled = true;
            }
        }

        private void NextClicked()
        {
            if (page == 0)
            {
                ShowPage(1);
                return;
            }

            if (page == 1)
            {
                DoInstall();
                return;
            }

            Close();
        }

        private void DoInstall()
        {
            string dir = locationBox.Text.Trim();
            if (dir.Length == 0)
            {
                MessageBox.Show(this, "Please choose an install location.",
                    "WinToggle Setup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (dir.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                MessageBox.Show(this, "That install path contains invalid characters.",
                    "WinToggle Setup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            nextBtn.Enabled = false;
            backBtn.Enabled = false;
            statusLbl.Text = "Installing\u2026";
            Application.DoEvents();

            try
            {
                Installer.Install(dir, desktopChk.Checked, startChk.Checked, false);
                installDir = dir;

                // Update the path note on the final page.
                foreach (Control c in pageDone.Controls)
                {
                    Label l = c as Label;
                    if (l != null && l.Text.StartsWith("Installed to:"))
                        l.Text = "Installed to: " + dir;
                }

                statusLbl.Text = "";
                ShowPage(2);
            }
            catch (Exception ex)
            {
                statusLbl.Text = "";
                MessageBox.Show(this, "Installation failed.\n\n" + ex.Message,
                    "WinToggle Setup", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                nextBtn.Enabled = true;
                backBtn.Enabled = true;
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);

            if (page == 2 && launchChk.Checked)
            {
                try { Process.Start(Installer.AppExe(installDir)); }
                catch { }
            }
        }

        private static GraphicsPath Round(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();
            int d = radius * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    // ---------------------------------------------------------------------
    // Entry point
    // ---------------------------------------------------------------------

    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            bool silent = Has(args, "--silent");
            bool uninstall = Has(args, "--uninstall");
            string dir = Value(args, "--dir");

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (silent)
            {
                try
                {
                    if (uninstall) Installer.Uninstall(dir ?? Installer.InstalledDir(), true);
                    else Installer.Install(dir ?? Installer.DefaultDir(), true, true, false);
                    Environment.ExitCode = 0;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.Message);
                    Environment.ExitCode = 1;
                }
                return;
            }

            if (uninstall)
            {
                Installer.Uninstall(Installer.InstalledDir(), false);
                return;
            }

            Application.Run(new WizardForm(dir));
        }

        private static bool Has(string[] args, string flag)
        {
            foreach (string a in args)
                if (string.Equals(a, flag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string Value(string[] args, string flag)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return null;
        }
    }
}
