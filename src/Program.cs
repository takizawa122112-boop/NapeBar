using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace NapeProBatteryTray
{
    internal static class AppLog
    {
        private static readonly object Gate = new object();
        private static readonly string DirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NapeProBatteryTray");
        private static readonly string FilePath = Path.Combine(DirectoryPath, "app.log");

        internal static void Write(string message)
        {
            string line = String.Format("{0:yyyy-MM-dd HH:mm:ss.fff} {1}", DateTime.Now, message);
            lock (Gate)
            {
                try
                {
                    Directory.CreateDirectory(DirectoryPath);
                    File.AppendAllText(FilePath, line + Environment.NewLine);
                }
                catch
                {
                }
            }
        }

        internal static string LogPath { get { return FilePath; } }
    }

#if PROBE
    internal static class Program
    {
        private static int Main(string[] args)
        {
            bool listOnly = args.Length > 0 && String.Equals(args[0], "--list", StringComparison.OrdinalIgnoreCase);
            bool selfTest = args.Length > 0 && String.Equals(args[0], "--self-test", StringComparison.OrdinalIgnoreCase);
            bool pingOnly = args.Length > 0 && String.Equals(args[0], "--ping", StringComparison.OrdinalIgnoreCase);
            if (selfTest)
            {
                return RunSelfTest();
            }

            Console.WriteLine("Nape Pro Battery Probe");
            Console.WriteLine("VID/PID 0x3434/0x0440 または 0xD026、UsagePage 0xFF60 を探索します。\n");
            List<HidDeviceInfo> devices = HidEnumerator.Enumerate(delegate(string line) { Console.Error.WriteLine(line); });
            if (devices.Count == 0)
            {
                Console.WriteLine("HIDデバイスが見つかりませんでした。");
            }
            else
            {
                for (int i = 0; i < devices.Count; i++)
                {
                    HidDeviceInfo device = devices[i];
                    Console.WriteLine("[{0}] {1}{2}", i, device, device.IsNapeCandidate ? "  <NAPE CANDIDATE>" : "");
                }
            }

            if (listOnly)
            {
                return 0;
            }

            if (pingOnly)
            {
                return RunPing(devices);
            }

            using (NapeBatteryReader reader = new NapeBatteryReader(delegate(string line) { Console.Error.WriteLine(line); }))
            {
                BatteryReading result = reader.Query(2500);
                if (!result.Success)
                {
                    Console.WriteLine("QUERY FAILED: " + result.Error);
                    Console.WriteLine("ログ: " + AppLog.LogPath);
                    return 2;
                }
                Console.WriteLine("BATTERY={0}%  DEVICE={1}  CONNECTION={2}",
                    result.BatteryLevel,
                    result.Device.ProductName,
                    result.Device.ConnectionLabel);
                return 0;
            }
        }

        private static int RunSelfTest()
        {
            byte[] request = new byte[32];
            request[0] = 0xA7;
            request[1] = 0x31;
            if (request[0] != 0xA7 || request[1] != 0x31)
            {
                Console.WriteLine("FAIL request");
                return 1;
            }
            byte[] response = new byte[] { 0xA7, 0x31, 97 };
            if (response[2] != 97)
            {
                Console.WriteLine("FAIL response");
                return 1;
            }
            Console.WriteLine("PASS protocol packet self-test: A7 31 -> byte[2] battery");
            return 0;
        }

        private static int RunPing(List<HidDeviceInfo> devices)
        {
            List<HidDeviceInfo> candidates = devices.FindAll(delegate(HidDeviceInfo item)
            {
                return item.VendorId == 0x3434 && item.UsagePage == 0xFF60;
            });
            for (int i = 0; i < candidates.Count; i++)
            {
                using (HidConnection connection = HidConnection.Open(candidates[i], delegate(string line) { Console.Error.WriteLine(line); }))
                {
                    if (connection == null)
                    {
                        continue;
                    }
                    byte[] response;
                    if (connection.TryRawCommand(0x01, 0x00, 1800, out response))
                    {
                        Console.WriteLine("PING RESPONSE={0} DEVICE={1}", Hex(response), candidates[i]);
                        return 0;
                    }
                }
            }
            Console.WriteLine("PING FAILED: FF60 did not return a response.");
            return 2;
        }

        private static string Hex(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return "<empty>";
            }
            StringBuilder builder = new StringBuilder(bytes.Length * 3);
            for (int i = 0; i < bytes.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(' ');
                }
                builder.Append(bytes[i].ToString("X2"));
            }
            return builder.ToString();
        }
    }
#else
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool createdNew;
            using (Mutex singleInstance = new Mutex(true, "Local\\NapeProBatteryTray", out createdNew))
            {
                if (!createdNew)
                {
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += OnThreadException;
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
                using (TrayApplicationContext context = new TrayApplicationContext())
                {
                    Application.Run(context);
                }
            }
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            AppLog.Write("UI thread exception: " + e.Exception);
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception exception = e.ExceptionObject as Exception;
            AppLog.Write("Unhandled exception (terminating=" + e.IsTerminating + "): " +
                (exception == null ? Convert.ToString(e.ExceptionObject) : exception.ToString()));
        }
    }

    internal sealed class TrayApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly System.Windows.Forms.Timer _timer;
        private readonly NapeBatteryReader _reader;
        private readonly ToolStripMenuItem _startWithWindowsItem;
        private readonly Control _dispatcher;
        private readonly object _iconGate = new object();
        private readonly BatteryStatusBarForm _statusBar;
        private ToolStripMenuItem _statusBarItem;
        private Icon _currentIcon;
        private bool _busy;
        private volatile bool _closing;

        internal TrayApplicationContext()
        {
            _reader = new NapeBatteryReader(AppLog.Write);
            _dispatcher = new Control();
            _dispatcher.CreateControl();
            _notifyIcon = new NotifyIcon();
            _notifyIcon.Visible = true;
            _notifyIcon.Text = "Nape Pro Battery";
            _currentIcon = TrayIconFactory.Create(null);
            _notifyIcon.Icon = _currentIcon;
            _statusBar = new BatteryStatusBarForm();

            ContextMenuStrip menu = new ContextMenuStrip();
            ToolStripMenuItem statusItem = new ToolStripMenuItem("Nape Pro: 確認中...");
            statusItem.Enabled = false;
            menu.Items.Add(statusItem);
            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem refreshItem = new ToolStripMenuItem("今すぐ更新");
            refreshItem.Click += delegate { RefreshBattery(statusItem); };
            menu.Items.Add(refreshItem);
            ToolStripMenuItem launcherItem = new ToolStripMenuItem("Keychron Launcherを開く");
            launcherItem.Click += delegate { OpenLauncher(); };
            menu.Items.Add(launcherItem);
            _statusBarItem = new ToolStripMenuItem("上部ステータスバーを隠す");
            _statusBarItem.Click += delegate { ToggleStatusBar(); };
            menu.Items.Add(_statusBarItem);
            menu.Items.Add(new ToolStripSeparator());
            _startWithWindowsItem = new ToolStripMenuItem("Windows起動時に自動起動");
            _startWithWindowsItem.Checked = StartupSettings.IsEnabled();
            _startWithWindowsItem.Click += delegate
            {
                StartupSettings.SetEnabled(!_startWithWindowsItem.Checked);
                _startWithWindowsItem.Checked = StartupSettings.IsEnabled();
            };
            menu.Items.Add(_startWithWindowsItem);
            ToolStripMenuItem logItem = new ToolStripMenuItem("ログを開く");
            logItem.Click += delegate { OpenLog(); };
            menu.Items.Add(logItem);
            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem exitItem = new ToolStripMenuItem("終了");
            exitItem.Click += delegate { ExitThread(); };
            menu.Items.Add(exitItem);
            _notifyIcon.ContextMenuStrip = menu;
            _statusBar.VisibleChanged += delegate { UpdateStatusBarMenu(); };
            _statusBar.ShowAtTop();
            UpdateStatusBarMenu();

            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 60000;
            _timer.Tick += delegate { RefreshBattery(statusItem); };
            _timer.Start();

            _notifyIcon.DoubleClick += delegate { ShowStatusBar(); };
            RefreshBattery(statusItem);
        }

        private void RefreshBattery(ToolStripMenuItem statusItem)
        {
            if (_closing || _busy)
            {
                return;
            }
            _busy = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                BatteryReading result;
                try
                {
                    result = _reader.Query(2500);
                }
                catch (Exception ex)
                {
                    AppLog.Write("Battery query exception: " + ex);
                    result = BatteryReading.Failed("Battery query failed.");
                }

                BeginInvokeIfAlive(delegate
                {
                    try
                    {
                        if (result.Success)
                        {
                            string connection = result.Device == null ? "HID" : result.Device.ConnectionLabel;
                            string text = String.Format("Nape Pro: {0}% ({1})", result.BatteryLevel, connection);
                            statusItem.Text = text;
                            _notifyIcon.Text = text.Length > 63 ? text.Substring(0, 63) : text;
                            ReplaceIcon(TrayIconFactory.Create(result.BatteryLevel));
                            _statusBar.SetStatus(result.BatteryLevel, connection);
                        }
                        else
                        {
                            statusItem.Text = "Nape Pro: 未接続 / 未取得";
                            _notifyIcon.Text = "Nape Pro: 未接続 / 未取得";
                            ReplaceIcon(TrayIconFactory.Create(null));
                            _statusBar.SetError();
                        }
                    }
                    catch (Exception ex)
                    {
                        AppLog.Write("Battery UI update exception: " + ex);
                    }
                    finally
                    {
                        _busy = false;
                    }
                });
            });
        }

        private void ToggleStatusBar()
        {
            if (_closing)
            {
                return;
            }

            if (_statusBar.Visible)
            {
                HideStatusBar();
            }
            else
            {
                ShowStatusBar();
            }
        }

        private void ShowStatusBar()
        {
            if (_closing)
            {
                return;
            }

            try
            {
                _statusBar.ShowAtTop();
                UpdateStatusBarMenu();
            }
            catch (Exception ex)
            {
                AppLog.Write("Failed to show status bar: " + ex);
            }
        }

        private void HideStatusBar()
        {
            try
            {
                _statusBar.Hide();
                UpdateStatusBarMenu();
            }
            catch (Exception ex)
            {
                AppLog.Write("Failed to hide status bar: " + ex);
            }
        }

        private void UpdateStatusBarMenu()
        {
            if (_statusBarItem == null)
            {
                return;
            }

            bool visible = _statusBar.Visible;
            _statusBarItem.Checked = visible;
            _statusBarItem.Text = visible ? "上部ステータスバーを隠す" : "上部ステータスバーを表示";
        }

        private void BeginInvokeIfAlive(MethodInvoker callback)
        {
            try
            {
                if (_closing || _dispatcher.IsDisposed || !_dispatcher.IsHandleCreated)
                {
                    return;
                }

                _dispatcher.BeginInvoke(new MethodInvoker(delegate
                {
                    if (_closing)
                    {
                        return;
                    }

                    try
                    {
                        callback();
                    }
                    catch (Exception ex)
                    {
                        AppLog.Write("UI callback exception: " + ex);
                    }
                }));
            }
            catch (Exception ex)
            {
                if (!_closing)
                {
                    AppLog.Write("UI dispatch exception: " + ex);
                }
            }
        }

        private void ReplaceIcon(Icon next)
        {
            lock (_iconGate)
            {
                Icon old = _currentIcon;
                _currentIcon = next;
                _notifyIcon.Icon = next;
                if (old != null)
                {
                    old.Dispose();
                }
            }
        }

        private void OpenLauncher()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://launcher.keychron.com/#/trackball/key",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AppLog.Write("Failed to open Launcher: " + ex.Message);
            }
        }

        private void OpenLog()
        {
            try
            {
                if (!File.Exists(AppLog.LogPath))
                {
                    File.WriteAllText(AppLog.LogPath, "Nape Pro Battery Tray log\r\n");
                }
                Process.Start(new ProcessStartInfo
                {
                    FileName = AppLog.LogPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AppLog.Write("Failed to open log: " + ex.Message);
            }
        }

        protected override void ExitThreadCore()
        {
            _closing = true;
            _timer.Stop();
            try
            {
                _reader.Dispose();
            }
            catch (Exception ex)
            {
                AppLog.Write("Reader dispose exception: " + ex);
            }
            try
            {
                _statusBar.Hide();
                _statusBar.Dispose();
            }
            catch (Exception ex)
            {
                AppLog.Write("Status bar dispose exception: " + ex);
            }
            try
            {
                _dispatcher.Dispose();
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                if (_currentIcon != null)
                {
                    _currentIcon.Dispose();
                }
            }
            catch (Exception ex)
            {
                AppLog.Write("Tray cleanup exception: " + ex);
            }
            base.ExitThreadCore();
        }
    }

    internal sealed class BatteryStatusBarForm : Form
    {
        private int _battery = -1;
        private string _connection = "--";
        private bool _dragging;
        private Point _dragOffset;

        internal BatteryStatusBarForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            ShowIcon = false;
            ControlBox = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(190, 38);
            BackColor = Color.FromArgb(38, 48, 59);
            ForeColor = Color.White;
            DoubleBuffered = true;
            Cursor = Cursors.SizeAll;
            Text = "Nape Pro battery";

            MouseDown += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                {
                    _dragging = true;
                    _dragOffset = e.Location;
                }
            };
            MouseMove += delegate(object sender, MouseEventArgs e)
            {
                if (_dragging)
                {
                    Location = new Point(Left + e.X - _dragOffset.X, Top + e.Y - _dragOffset.Y);
                }
            };
            MouseUp += delegate { _dragging = false; };
            KeyPreview = true;
            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape)
                {
                    Hide();
                }
            };
        }

        internal void ShowAtTop()
        {
            Screen screen = Screen.FromPoint(Cursor.Position);
            Rectangle area = screen.WorkingArea;
            Location = new Point(area.Left + (area.Width - Width) / 2, area.Top + 8);
            Show();
            BringToFront();
        }

        internal void SetStatus(int battery, string connection)
        {
            _battery = battery;
            _connection = connection;
            Invalidate();
        }

        internal void SetError()
        {
            _battery = -1;
            _connection = "未取得";
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            using (GraphicsPath path = RoundedRectangle(new Rectangle(0, 0, Width, Height), Height / 2))
            {
                Region = new Region(path);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(BackColor);

            Color accent = _battery >= 0 && _battery <= 20 ? Color.IndianRed : Color.FromArgb(96, 183, 238);
            using (Pen batteryPen = new Pen(Color.White, 1.5f))
            using (Pen dividerPen = new Pen(Color.FromArgb(105, 120, 133), 1.0f))
            using (SolidBrush fill = new SolidBrush(accent))
            using (SolidBrush terminalBrush = new SolidBrush(Color.White))
            using (SolidBrush textBrush = new SolidBrush(Color.White))
            using (SolidBrush secondaryBrush = new SolidBrush(Color.FromArgb(205, 215, 224)))
            using (Font percentFont = new Font("Segoe UI", 12.0f, FontStyle.Bold, GraphicsUnit.Point))
            using (Font connectionFont = new Font("Segoe UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point))
            using (StringFormat centered = new StringFormat())
            {
                Rectangle battery = new Rectangle(12, 12, 18, 12);
                using (GraphicsPath batteryPath = RoundedRectangle(battery, 2))
                {
                    graphics.DrawPath(batteryPen, batteryPath);
                }
                graphics.FillRectangle(fill, 14, 14, Math.Max(1, (battery.Width - 4) * Math.Max(0, Math.Min(100, _battery)) / 100), 8);
                graphics.FillRectangle(terminalBrush, 30, 16, 3, 4);

                centered.Alignment = StringAlignment.Center;
                centered.LineAlignment = StringAlignment.Center;
                string percent = _battery < 0 ? "--" : _battery.ToString() + "%";
                graphics.DrawString(percent, percentFont, textBrush, new RectangleF(38, 3, 62, 31), centered);
                graphics.DrawLine(dividerPen, 106, 9, 106, 29);
                graphics.DrawString(_connection, connectionFont, secondaryBrush, new RectangleF(115, 4, 66, 29), centered);
            }
        }

        private static GraphicsPath RoundedRectangle(Rectangle rectangle, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal static class TrayIconFactory
    {
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr handle);

        internal static Icon Create(int? battery)
        {
            using (Bitmap bitmap = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                using (Graphics graphics = Graphics.FromImage(bitmap))
                using (Font font = new Font("Segoe UI", 9.0f, FontStyle.Bold, GraphicsUnit.Point))
                using (SolidBrush background = new SolidBrush(battery.HasValue && battery.Value <= 20 ? Color.Firebrick : Color.FromArgb(30, 115, 190)))
                using (SolidBrush foreground = new SolidBrush(Color.White))
                using (StringFormat format = new StringFormat())
                {
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.Clear(Color.Transparent);
                    graphics.FillEllipse(background, 1, 1, 30, 30);
                    format.Alignment = StringAlignment.Center;
                    format.LineAlignment = StringAlignment.Center;
                    string label = battery.HasValue ? battery.Value.ToString() : "?";
                    graphics.DrawString(label, font, foreground, new RectangleF(1, 1, 30, 30), format);
                }

                IntPtr hIcon = bitmap.GetHicon();
                try
                {
                    using (Icon source = Icon.FromHandle(hIcon))
                    {
                        return (Icon)source.Clone();
                    }
                }
                finally
                {
                    DestroyIcon(hIcon);
                }
            }
        }
    }

    internal static class StartupSettings
    {
        private const string RunKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
        private const string ValueName = "NapeProBatteryTray";

        internal static bool IsEnabled()
        {
            using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, false))
            {
                return key != null && key.GetValue(ValueName) != null;
            }
        }

        internal static void SetEnabled(bool enabled)
        {
            using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (enabled)
                {
                    key.SetValue(ValueName, '"' + Assembly.GetExecutingAssembly().Location + '"');
                }
                else
                {
                    key.DeleteValue(ValueName, false);
                }
            }
        }
    }
#endif
}
