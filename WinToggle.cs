using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace WinToggle
{
    // ---------------------------------------------------------------------
    // Models
    // ---------------------------------------------------------------------

    public enum RegKind { Dword, String, KeyExists }

    public class Setting
    {
        public string Title;
        public string Description;
        public string Category;
        public string KeyPath;
        public string ValueName;
        public object OnValue;
        public object OffValue;
        public bool DefaultOn;
        public RegKind Kind;

        public Setting(string title, string description, string category,
                       string keyPath, string valueName, object onValue, object offValue,
                       bool defaultOn, RegKind kind)
        {
            Title = title;
            Description = description;
            Category = category;
            KeyPath = keyPath;
            ValueName = valueName;
            OnValue = onValue;
            OffValue = offValue;
            DefaultOn = defaultOn;
            Kind = kind;
        }
    }

    public static class Tweaks
    {
        public const string Adv = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        public const string Explorer = @"Software\Microsoft\Windows\CurrentVersion\Explorer";
        public const string Personalize = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        public const string WindowMetrics = @"Control Panel\Desktop\WindowMetrics";
        public const string SearchPolicy = @"Software\Policies\Microsoft\Windows\Explorer";
        public const string ContentDelivery = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
        public const string ClassicMenu = @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32";

        public static List<Setting> All()
        {
            List<Setting> s = new List<Setting>();

            // ---------------- Taskbar ----------------
            s.Add(new Setting("Show seconds in clock", "Add seconds to the taskbar clock.",
                "Taskbar", Adv, "ShowSecondsInSystemClock", 1, 0, false, RegKind.Dword));

            s.Add(new Setting("Align taskbar to the left", "Move the Start button and app icons to the left edge of the screen.",
                "Taskbar", Adv, "TaskbarAl", 0, 1, false, RegKind.Dword));

            s.Add(new Setting("Compact search box", "Collapse the taskbar search box into a single icon.",
                "Taskbar", Adv, "SearchboxTaskbarMode", 1, 2, false, RegKind.Dword));

            s.Add(new Setting("Hide Task View button", "Remove the Task View button from the taskbar.",
                "Taskbar", Adv, "ShowTaskViewButton", 0, 1, false, RegKind.Dword));

            s.Add(new Setting("Hide Widgets button", "Remove the Widgets button from the taskbar.",
                "Taskbar", Adv, "TaskbarDa", 0, 1, false, RegKind.Dword));

            s.Add(new Setting("Hide Chat button", "Remove the Chat / Meet Now button from the taskbar.",
                "Taskbar", Adv, "TaskbarMn", 0, 1, false, RegKind.Dword));

            // ---------------- Explorer ----------------
            s.Add(new Setting("Show hidden files", "Reveal files and folders marked as hidden.",
                "Explorer", Adv, "Hidden", 1, 2, false, RegKind.Dword));

            s.Add(new Setting("Show file extensions", "Always display file extensions like .txt and .exe.",
                "Explorer", Adv, "HideFileExt", 0, 1, false, RegKind.Dword));

            s.Add(new Setting("Show protected system files", "Show operating system files that are normally hidden.",
                "Explorer", Adv, "ShowSuperHidden", 1, 0, false, RegKind.Dword));

            s.Add(new Setting("Open Explorer to This PC", "Start File Explorer at This PC instead of Quick Access.",
                "Explorer", Adv, "LaunchTo", 1, 2, false, RegKind.Dword));

            s.Add(new Setting("Classic right-click menu", "Restore the full Windows 10 context menu on Windows 11.",
                "Explorer", ClassicMenu, "", "", null, false, RegKind.KeyExists));

            // ---------------- Appearance ----------------
            s.Add(new Setting("Dark mode for apps", "Use the dark theme in Windows apps and settings.",
                "Appearance", Personalize, "AppsUseLightTheme", 0, 1, false, RegKind.Dword));

            s.Add(new Setting("Dark mode for Windows", "Use the dark theme for the taskbar, Start menu and shell.",
                "Appearance", Personalize, "SystemUsesLightTheme", 0, 1, false, RegKind.Dword));

            s.Add(new Setting("Transparency effects", "Enable the frosted-glass look on windows and the taskbar.",
                "Appearance", Personalize, "EnableTransparency", 1, 0, true, RegKind.Dword));

            s.Add(new Setting("Window animations", "Animate windows when minimizing, maximizing and opening.",
                "Appearance", WindowMetrics, "MinAnimate", "1", "0", true, RegKind.String));

            s.Add(new Setting("Accent color on title bars", "Show your accent color on window title bars and borders.",
                "Appearance", Personalize, "ColorPrevalence", 1, 0, false, RegKind.Dword));

            s.Add(new Setting("Hide desktop icons", "Hide everything on the desktop for a clean look.",
                "Appearance", Adv, "HideIcons", 1, 0, false, RegKind.Dword));

            // ---------------- Privacy ----------------
            s.Add(new Setting("Disable Bing in Start search", "Keep web results out of the Start menu search.",
                "Privacy", SearchPolicy, "DisableSearchBoxSuggestions", 1, 0, false, RegKind.Dword));

            s.Add(new Setting("Turn off suggested settings", "Stop Windows from showing suggested content in Settings.",
                "Privacy", ContentDelivery, "SubscribedContent-338393Enabled", 0, 1, false, RegKind.Dword));

            s.Add(new Setting("Turn off Windows tips", "Disable pop-up tips, tricks and suggestions from Windows.",
                "Privacy", ContentDelivery, "SoftLandingEnabled", 0, 1, false, RegKind.Dword));

            s.Add(new Setting("Turn off Explorer ads", "Hide OneDrive and other promotional banners in File Explorer.",
                "Privacy", Adv, "ShowSyncProviderNotifications", 0, 1, false, RegKind.Dword));

            return s;
        }
    }

    // ---------------------------------------------------------------------
    // Registry helper
    // ---------------------------------------------------------------------

    public static class Reg
    {
        public static bool IsOn(Setting s)
        {
            try
            {
                if (s.Kind == RegKind.KeyExists)
                {
                    using (RegistryKey k = Registry.CurrentUser.OpenSubKey(s.KeyPath))
                    {
                        return k != null && k.GetValue("") != null;
                    }
                }

                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(s.KeyPath))
                {
                    if (k == null) return s.DefaultOn;
                    object v = k.GetValue(s.ValueName);
                    if (v == null) return s.DefaultOn;

                    if (s.Kind == RegKind.Dword)
                    {
                        int n;
                        try { n = Convert.ToInt32(v); }
                        catch { return s.DefaultOn; }
                        return n == Convert.ToInt32(s.OnValue);
                    }

                    return string.Equals(Convert.ToString(v), Convert.ToString(s.OnValue),
                                         StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
                return s.DefaultOn;
            }
        }

        public static void Set(Setting s, bool on)
        {
            if (s.Kind == RegKind.KeyExists)
            {
                if (on)
                {
                    using (RegistryKey k = Registry.CurrentUser.CreateSubKey(s.KeyPath))
                    {
                        k.SetValue("", "", RegistryValueKind.String);
                    }
                }
                else
                {
                    try { Registry.CurrentUser.DeleteSubKeyTree(s.KeyPath, false); }
                    catch { }
                }
                return;
            }

            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(s.KeyPath))
            {
                object v = on ? s.OnValue : s.OffValue;
                if (s.Kind == RegKind.Dword)
                    k.SetValue(s.ValueName, Convert.ToInt32(v), RegistryValueKind.DWord);
                else
                    k.SetValue(s.ValueName, Convert.ToString(v), RegistryValueKind.String);
            }
        }
    }

    // ---------------------------------------------------------------------
    // Toggle switch control
    // ---------------------------------------------------------------------

    public class ToggleSwitch : Control
    {
        private bool on;
        private double pos;
        private double target;
        private Timer timer;

        public event EventHandler Toggled;

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.Selectable, false);
            BackColor = Color.Transparent;
            Size = new Size(52, 28);
            Cursor = Cursors.Hand;
            TabStop = false;

            timer = new Timer();
            timer.Interval = 15;
            timer.Tick += delegate
            {
                double diff = target - pos;
                if (Math.Abs(diff) < 0.06)
                {
                    pos = target;
                    timer.Stop();
                }
                else
                {
                    pos += diff * 0.32;
                }
                Invalidate();
            };
        }

        public bool IsOn { get { return on; } }

        public void SetOn(bool value, bool animate)
        {
            on = value;
            target = value ? 1.0 : 0.0;
            if (!animate) { pos = target; timer.Stop(); }
            else timer.Start();
            Invalidate();
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            on = !on;
            target = on ? 1.0 : 0.0;
            timer.Start();
            Invalidate();
            if (Toggled != null) Toggled(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int trackW = 46;
            int trackH = 24;
            Rectangle track = new Rectangle((Width - trackW) / 2, (Height - trackH) / 2, trackW, trackH);

            Color offColor = Color.FromArgb(74, 74, 88);
            Color onColor = Color.FromArgb(52, 211, 153);
            Color c = Blend(offColor, onColor, (float)pos);

            using (GraphicsPath path = Round(track, trackH / 2))
            using (SolidBrush b = new SolidBrush(c))
            {
                g.FillPath(b, path);
            }

            int d = 18;
            int x = track.X + 3 + (int)Math.Round((trackW - d - 6) * pos);
            int y = track.Y + 3;
            using (SolidBrush b = new SolidBrush(Color.White))
            {
                g.FillEllipse(b, x, y, d, d);
            }
        }

        private static Color Blend(Color a, Color b, float t)
        {
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        public static GraphicsPath Round(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();
            int d = radius * 2;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    // ---------------------------------------------------------------------
    // Slim scrollbar
    // ---------------------------------------------------------------------

    public class SlimBar : Control
    {
        private ScrollableControl target;
        private bool dragging;
        private bool hover;
        private int grab;

        public SlimBar(ScrollableControl target)
        {
            this.target = target;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = MainForm.Bg;
            Cursor = Cursors.Hand;
            Width = 11;
        }

        private int MaxScroll
        {
            get
            {
                int m = target.VerticalScroll.Maximum - target.VerticalScroll.LargeChange;
                return m < 0 ? 0 : m;
            }
        }

        private Rectangle Thumb()
        {
            int h = Height;
            int total = target.VerticalScroll.Maximum;
            int view = target.VerticalScroll.LargeChange;
            if (total <= 0 || view <= 0) return new Rectangle(3, 0, Width - 6, h);

            int th = (int)((long)h * view / total);
            if (th < 28) th = 28;
            if (th > h) th = h;

            int span = h - th;
            int max = MaxScroll;
            int value = target.VerticalScroll.Value;
            if (value < 0) value = 0;
            if (value > max) value = max;
            int y = max <= 0 ? 0 : (int)((long)span * value / max);
            return new Rectangle(3, y, Width - 6, th);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle track = new Rectangle(3, 0, Width - 6, Height);
            using (GraphicsPath p = ToggleSwitch.Round(track, (Width - 6) / 2))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(35, 35, 45)))
            {
                g.FillPath(b, p);
            }

            Rectangle th = Thumb();
            Color c = hover || dragging ? Color.FromArgb(112, 112, 136) : Color.FromArgb(78, 78, 96);
            using (GraphicsPath p = ToggleSwitch.Round(th, (Width - 6) / 2))
            using (SolidBrush b = new SolidBrush(c))
            {
                g.FillPath(b, p);
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            hover = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hover = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Rectangle th = Thumb();
            if (th.Contains(e.Location))
            {
                dragging = true;
                grab = e.Y - th.Y;
                Capture = true;
            }
            else
            {
                int page = target.VerticalScroll.LargeChange;
                int v = target.VerticalScroll.Value + (e.Y < th.Y ? -page : page);
                SetValue(v);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!dragging) return;
            Rectangle th = Thumb();
            int span = Height - th.Height;
            int y = e.Y - grab;
            if (y < 0) y = 0;
            if (y > span) y = span;
            int max = MaxScroll;
            int v = span <= 0 ? 0 : (int)((long)max * y / span);
            SetValue(v);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            dragging = false;
            Capture = false;
            Invalidate();
        }

        public void SetValue(int v)
        {
            int max = MaxScroll;
            if (v < 0) v = 0;
            if (v > max) v = max;
            target.AutoScrollPosition = new Point(0, v);
            Invalidate();
        }
    }

    // ---------------------------------------------------------------------
    // Setting row
    // ---------------------------------------------------------------------

    public class SettingRow : Panel
    {
        public Setting Setting;
        public ToggleSwitch Toggle;
        private Label titleLbl;
        private Label descLbl;
        private bool hover;

        public event EventHandler Changed;

        public SettingRow(Setting setting, bool state)
        {
            Setting = setting;
            Height = 84;
            Width = 640;
            Margin = new Padding(0, 0, 0, 12);
            BackColor = MainForm.Card;
            Cursor = Cursors.Hand;

            titleLbl = new Label();
            titleLbl.Text = setting.Title;
            titleLbl.Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Regular);
            titleLbl.ForeColor = MainForm.TextMain;
            titleLbl.BackColor = MainForm.Card;
            titleLbl.AutoSize = false;
            titleLbl.Cursor = Cursors.Hand;
            titleLbl.Click += RowClick;
            Controls.Add(titleLbl);

            descLbl = new Label();
            descLbl.Text = setting.Description;
            descLbl.Font = new Font("Segoe UI", 8.75f);
            descLbl.ForeColor = MainForm.TextDim;
            descLbl.BackColor = MainForm.Card;
            descLbl.AutoSize = false;
            descLbl.Cursor = Cursors.Hand;
            descLbl.Click += RowClick;
            Controls.Add(descLbl);

            Toggle = new ToggleSwitch();
            Toggle.SetOn(state, false);
            Toggle.Toggled += delegate { RaiseChanged(); };
            Controls.Add(Toggle);

            AttachHover(this);
            AttachHover(titleLbl);
            AttachHover(descLbl);

            OnResize(EventArgs.Empty);
        }

        private void RowClick(object sender, EventArgs e)
        {
            Toggle.SetOn(!Toggle.IsOn, true);
            RaiseChanged();
        }

        private void RaiseChanged()
        {
            if (Changed != null) Changed(this, EventArgs.Empty);
        }

        private void AttachHover(Control c)
        {
            c.MouseEnter += delegate { SetHover(true); };
            c.MouseLeave += delegate
            {
                if (!ClientRectangle.Contains(PointToClient(Cursor.Position)))
                    SetHover(false);
            };
        }

        private void SetHover(bool value)
        {
            if (hover == value) return;
            hover = value;
            Color c = value ? MainForm.CardHover : MainForm.Card;
            BackColor = c;
            titleLbl.BackColor = c;
            descLbl.BackColor = c;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            int w = Width - 110;
            if (w < 120) w = 120;
            if (titleLbl != null)
            {
                titleLbl.SetBounds(20, 15, w, 22);
                descLbl.SetBounds(20, 39, w, 36);
                Toggle.Location = new Point(Width - 74, 28);
            }
            using (GraphicsPath p = ToggleSwitch.Round(new Rectangle(0, 0, Width, Height), 12))
            {
                Region = new Region(p);
            }
        }
    }

    // ---------------------------------------------------------------------
    // Main window
    // ---------------------------------------------------------------------

    public class MainForm : Form
    {
        public static readonly Color Bg = Color.FromArgb(23, 23, 29);
        public static readonly Color Side = Color.FromArgb(30, 30, 38);
        public static readonly Color Card = Color.FromArgb(36, 36, 45);
        public static readonly Color CardHover = Color.FromArgb(44, 44, 56);
        public static readonly Color Accent = Color.FromArgb(139, 92, 246);
        public static readonly Color AccentHover = Color.FromArgb(124, 76, 235);
        public static readonly Color TextMain = Color.FromArgb(240, 240, 245);
        public static readonly Color TextDim = Color.FromArgb(154, 154, 168);

        private const int SidebarW = 212;
        private const int HeaderH = 98;
        private const int FooterH = 60;

        private Panel sidebar;
        private Panel header;
        private Panel footer;
        private FlowLayoutPanel list;
        private Label headerTitle;
        private Label headerSub;
        private Label searchLbl;
        private TextBox searchBox;
        private Label statusLbl;
        private Button restartBtn;
        private Button refreshBtn;
        private SlimBar scrollBar;
        private Label versionLbl;

        private Dictionary<string, Button> nav = new Dictionary<string, Button>();
        private string current = "Taskbar";
        private List<Setting> all;
        private List<SettingRow> rows = new List<SettingRow>();

        private readonly Dictionary<string, string> descriptions = new Dictionary<string, string>();

        public MainForm()
        {
            Text = "WinToggle";

            Rectangle work = Screen.PrimaryScreen.WorkingArea;
            int winW = Math.Min(1020, work.Width - 40);
            int winH = Math.Min(700, work.Height - 40);
            if (winW < 900) winW = Math.Min(900, work.Width);
            if (winH < 600) winH = Math.Min(600, work.Height);
            ClientSize = new Size(winW, winH);
            MinimumSize = new Size(Math.Min(880, work.Width), Math.Min(560, work.Height));
            StartPosition = FormStartPosition.Manual;
            Location = new Point(
                work.Left + Math.Max(0, (work.Width - (winW + 16)) / 2),
                work.Top + Math.Max(0, (work.Height - (winH + 39)) / 2));
            BackColor = Bg;
            ForeColor = TextMain;
            Font = new Font("Segoe UI", 9f);
            DoubleBuffered = true;

            descriptions["Taskbar"] = "Clock, search, buttons and alignment.";
            descriptions["Explorer"] = "Files, folders and the right-click menu.";
            descriptions["Appearance"] = "Dark mode, animations and desktop icons.";
            descriptions["Privacy"] = "Search suggestions, tips and ads.";

            all = Tweaks.All();

            BuildUi();
            LayoutUi();
            SelectCategory("Taskbar");
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string subApp, string subIdList);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);

        [DllImport("user32.dll")]
        private static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool show);

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            // Make sure the window is always reachable, even if display
            // configuration changed since the app started.
            Rectangle work = Screen.FromControl(this).WorkingArea;
            bool visible = false;
            foreach (Screen s in Screen.AllScreens)
            {
                Rectangle overlap = Rectangle.Intersect(s.WorkingArea, Bounds);
                if (overlap.Width > 120 && overlap.Height > 80) { visible = true; break; }
            }
            if (!visible)
            {
                Location = new Point(
                    work.Left + Math.Max(0, (work.Width - Width) / 2),
                    work.Top + Math.Max(0, (work.Height - Height) / 2));
            }

            SyncScroll();
            if (scrollBar != null) scrollBar.Invalidate();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int dark = 1;
            DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
            DwmSetWindowAttribute(Handle, 19, ref dark, sizeof(int));
            if (list != null && list.IsHandleCreated)
                SetWindowTheme(list.Handle, "DarkMode_Explorer", null);
        }

        private void BuildUi()
        {
            // Sidebar
            sidebar = new Panel();
            sidebar.BackColor = Side;
            Controls.Add(sidebar);

            Label logo = new Label();
            logo.Text = "WinToggle";
            logo.Font = new Font("Segoe UI Semibold", 16f);
            logo.ForeColor = TextMain;
            logo.AutoSize = false;
            logo.SetBounds(22, 24, 170, 30);
            sidebar.Controls.Add(logo);

            Label tagline = new Label();
            tagline.Text = "Windows, simplified.";
            tagline.Font = new Font("Segoe UI", 8.5f);
            tagline.ForeColor = TextDim;
            tagline.AutoSize = false;
            tagline.SetBounds(23, 56, 170, 20);
            sidebar.Controls.Add(tagline);

            string[] cats = new string[] { "Taskbar", "Explorer", "Appearance", "Privacy" };
            int y = 108;
            foreach (string cat in cats)
            {
                Button b = new Button();
                b.Text = "   " + cat;
                b.TextAlign = ContentAlignment.MiddleLeft;
                b.FlatStyle = FlatStyle.Flat;
                b.FlatAppearance.BorderSize = 0;
                b.BackColor = Side;
                b.ForeColor = TextDim;
                b.Font = new Font("Segoe UI Semibold", 9.5f);
                b.SetBounds(20, y, 172, 38);
                b.Cursor = Cursors.Hand;
                b.TabStop = false;
                string captured = cat;
                b.Click += delegate { SelectCategory(captured); };
                sidebar.Controls.Add(b);
                nav[cat] = b;
                y += 46;
            }

            versionLbl = new Label();
            versionLbl.Text = "v1.0.0";
            versionLbl.Font = new Font("Segoe UI", 8f);
            versionLbl.ForeColor = Color.FromArgb(110, 110, 124);
            versionLbl.AutoSize = false;
            sidebar.Controls.Add(versionLbl);

            // Header
            header = new Panel();
            header.BackColor = Bg;
            Controls.Add(header);

            headerTitle = new Label();
            headerTitle.Font = new Font("Segoe UI Semibold", 18f);
            headerTitle.ForeColor = TextMain;
            headerTitle.AutoSize = false;
            header.Controls.Add(headerTitle);

            headerSub = new Label();
            headerSub.Font = new Font("Segoe UI", 9f);
            headerSub.ForeColor = TextDim;
            headerSub.AutoSize = false;
            header.Controls.Add(headerSub);

            searchLbl = new Label();
            searchLbl.Text = "SEARCH";
            searchLbl.Font = new Font("Segoe UI Semibold", 7.5f);
            searchLbl.ForeColor = Color.FromArgb(120, 120, 136);
            searchLbl.AutoSize = false;
            header.Controls.Add(searchLbl);

            searchBox = new TextBox();
            searchBox.BorderStyle = BorderStyle.FixedSingle;
            searchBox.BackColor = Card;
            searchBox.ForeColor = TextMain;
            searchBox.Font = new Font("Segoe UI", 9.5f);
            searchBox.TextChanged += delegate { Rebuild(); };
            header.Controls.Add(searchBox);

            // List
            list = new FlowLayoutPanel();
            list.BackColor = Bg;
            list.AutoScroll = true;
            list.FlowDirection = FlowDirection.TopDown;
            list.WrapContents = false;
            list.Padding = new Padding(24);
            list.Resize += delegate { ApplyRowWidths(); SyncScroll(); };
            list.Scroll += delegate { if (scrollBar != null) scrollBar.Invalidate(); };
            Controls.Add(list);

            scrollBar = new SlimBar(list);
            scrollBar.Visible = false;
            Controls.Add(scrollBar);
            scrollBar.BringToFront();

            // Footer
            footer = new Panel();
            footer.BackColor = Side;
            Controls.Add(footer);

            statusLbl = new Label();
            statusLbl.Text = "Ready. Changes apply to your user account only.";
            statusLbl.Font = new Font("Segoe UI", 8.5f);
            statusLbl.ForeColor = TextDim;
            statusLbl.AutoSize = false;
            statusLbl.SetBounds(24, 21, 520, 20);
            footer.Controls.Add(statusLbl);

            refreshBtn = MakeFlatButton("Refresh", Card, TextMain);
            refreshBtn.Click += delegate
            {
                Rebuild();
                statusLbl.Text = "Refreshed from the registry.";
            };
            footer.Controls.Add(refreshBtn);

            restartBtn = MakeFlatButton("Restart Explorer", Accent, Color.White);
            restartBtn.Click += delegate { RestartExplorer(); };
            footer.Controls.Add(restartBtn);
        }

        private Button MakeFlatButton(string text, Color back, Color fore)
        {
            Button b = new Button();
            b.Text = text;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = back;
            b.ForeColor = fore;
            b.Font = new Font("Segoe UI Semibold", 9f);
            b.Cursor = Cursors.Hand;
            b.TabStop = false;
            b.MouseEnter += delegate { b.BackColor = Lighten(back); };
            b.MouseLeave += delegate { b.BackColor = back; };
            return b;
        }

        private static Color Lighten(Color c)
        {
            return Color.FromArgb(
                Math.Min(255, c.R + 18),
                Math.Min(255, c.G + 18),
                Math.Min(255, c.B + 18));
        }

        private void LayoutUi()
        {
            sidebar.SetBounds(0, 0, SidebarW, ClientSize.Height);
            header.SetBounds(SidebarW, 0, ClientSize.Width - SidebarW, HeaderH);
            footer.SetBounds(SidebarW, ClientSize.Height - FooterH, ClientSize.Width - SidebarW, FooterH);
            list.SetBounds(SidebarW, HeaderH, ClientSize.Width - SidebarW, ClientSize.Height - HeaderH - FooterH);

            headerTitle.SetBounds(26, 20, 500, 34);
            headerSub.SetBounds(28, 56, 500, 22);

            int right = header.Width - 26;
            searchLbl.SetBounds(right - 210, 15, 210, 15);
            searchBox.SetBounds(right - 212, 33, 212, 26);

            versionLbl.SetBounds(23, ClientSize.Height - 34, 170, 18);

            int bx = footer.Width - 24;
            restartBtn.SetBounds(bx - 132, 15, 132, 32);
            refreshBtn.SetBounds(bx - 132 - 10 - 84, 15, 84, 32);
            statusLbl.SetBounds(24, 21, Math.Max(100, footer.Width - 300), 20);

            ApplyRowWidths();
            SyncScroll();
        }

        private void SyncScroll()
        {
            if (list == null || scrollBar == null) return;
            if (!list.IsHandleCreated) return;

            ShowScrollBar(list.Handle, 1, false); // SB_VERT

            int view = list.VerticalScroll.LargeChange;
            int total = list.VerticalScroll.Maximum;
            bool need = total > view + 1 && total > list.ClientSize.Height;

            scrollBar.Visible = need;
            if (need)
            {
                scrollBar.Bounds = new Rectangle(list.Right - 14, list.Top + 8, 11, list.Height - 16);
                scrollBar.BringToFront();
            }
        }

        public bool HandleWheel(Point screenPoint, int delta)
        {
            if (list == null || !list.IsHandleCreated) return false;
            Point p = list.PointToClient(screenPoint);
            if (!list.ClientRectangle.Contains(p)) return false;

            int max = list.VerticalScroll.Maximum - list.VerticalScroll.LargeChange;
            if (max <= 0) return false;

            int v = list.VerticalScroll.Value - Math.Sign(delta) * 96;
            if (v < 0) v = 0;
            if (v > max) v = max;
            list.AutoScrollPosition = new Point(0, v);
            if (scrollBar != null) scrollBar.Invalidate();
            return true;
        }

        private int RowWidth()
        {
            int w = list.ClientSize.Width - list.Padding.Horizontal - 30;
            if (w < 320) w = 320;
            return w;
        }

        private void ApplyRowWidths()
        {
            if (rows == null) return;
            int w = RowWidth();
            foreach (SettingRow r in rows) r.Width = w;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (sidebar != null) LayoutUi();
        }

        private void SelectCategory(string cat)
        {
            current = cat;
            searchBox.Text = "";
            foreach (KeyValuePair<string, Button> kv in nav)
            {
                bool selected = kv.Key == cat;
                kv.Value.BackColor = selected ? Color.FromArgb(46, 40, 64) : Side;
                kv.Value.ForeColor = selected ? Color.White : TextDim;
            }
            Rebuild();
        }

        private void Rebuild()
        {
            if (list == null) return;
            list.SuspendLayout();

            foreach (SettingRow r in rows)
            {
                list.Controls.Remove(r);
                r.Dispose();
            }
            rows.Clear();

            string q = searchBox.Text.Trim().ToLowerInvariant();
            List<Setting> items;
            if (q.Length > 0)
            {
                items = all.Where(delegate (Setting s)
                {
                    return s.Title.ToLowerInvariant().Contains(q)
                        || s.Description.ToLowerInvariant().Contains(q)
                        || s.Category.ToLowerInvariant().Contains(q);
                }).ToList();
                headerTitle.Text = "Search results";
                headerSub.Text = items.Count + (items.Count == 1 ? " tweak" : " tweaks") + " matching \"" + searchBox.Text.Trim() + "\"";
            }
            else
            {
                items = all.Where(delegate (Setting s) { return s.Category == current; }).ToList();
                headerTitle.Text = current;
                headerSub.Text = descriptions[current] + "   \u00B7   " + items.Count + " tweaks";
            }

            if (items.Count == 0)
            {
                Label empty = new Label();
                empty.Text = "No tweaks found.";
                empty.Font = new Font("Segoe UI", 10f);
                empty.ForeColor = TextDim;
                empty.AutoSize = false;
                empty.Size = new Size(RowWidth(), 40);
                empty.Margin = new Padding(0);
                list.Controls.Add(empty);
            }

            foreach (Setting s in items)
            {
                SettingRow row = new SettingRow(s, Reg.IsOn(s));
                row.Width = RowWidth();
                row.Changed += RowChanged;
                rows.Add(row);
                list.Controls.Add(row);
            }

            list.ResumeLayout();

            if (IsHandleCreated)
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    SyncScroll();
                    if (scrollBar != null) scrollBar.Invalidate();
                });
            }
            else
            {
                SyncScroll();
            }
        }

        private void RowChanged(object sender, EventArgs e)
        {
            SettingRow row = (SettingRow)sender;
            bool on = row.Toggle.IsOn;
            try
            {
                Reg.Set(row.Setting, on);
                statusLbl.Text = row.Setting.Title + (on ? "  \u2192  on" : "  \u2192  off")
                    + "   \u00B7   Restart Explorer if it does not apply.";
            }
            catch (Exception ex)
            {
                row.Toggle.SetOn(!on, true);
                MessageBox.Show(this,
                    "Could not update the registry.\n\n" + ex.Message,
                    "WinToggle", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RestartExplorer()
        {
            DialogResult r = MessageBox.Show(this,
                "Restart Explorer now?\n\nOpen File Explorer windows will close.",
                "WinToggle", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r != DialogResult.Yes) return;

            try
            {
                foreach (Process p in Process.GetProcessesByName("explorer"))
                {
                    try { p.Kill(); } catch { }
                }

                System.Threading.Thread.Sleep(1200);

                if (Process.GetProcessesByName("explorer").Length == 0)
                    Process.Start("explorer.exe");

                statusLbl.Text = "Explorer restarted.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not restart Explorer.\n\n" + ex.Message,
                    "WinToggle", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    // ---------------------------------------------------------------------
    // Wheel routing
    // ---------------------------------------------------------------------

    public class WheelRouter : IMessageFilter
    {
        private MainForm form;

        public WheelRouter(MainForm form)
        {
            this.form = form;
        }

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg == 0x020A) // WM_MOUSEWHEEL
            {
                long lp = m.LParam.ToInt64();
                int x = (short)(lp & 0xFFFF);
                int y = (short)((lp >> 16) & 0xFFFF);
                int delta = (short)((long)m.WParam >> 16);
                return form.HandleWheel(new Point(x, y), delta);
            }
            return false;
        }
    }

    // ---------------------------------------------------------------------
    // Entry point
    // ---------------------------------------------------------------------

    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            MainForm form = new MainForm();
            Application.AddMessageFilter(new WheelRouter(form));
            Application.Run(form);
        }
    }
}
