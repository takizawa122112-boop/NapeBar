using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace NapeBar
{
    internal static class LogRotation
    {
        internal static bool ShouldRotate(long currentBytes, int incomingBytes, long maximumBytes)
        {
            return currentBytes > 0 && incomingBytes > maximumBytes - currentBytes;
        }

        internal static void RotateIfNeeded(
            string currentPath,
            string oldPath,
            long maximumBytes,
            int incomingBytes)
        {
            if (!File.Exists(currentPath) ||
                !ShouldRotate(new FileInfo(currentPath).Length, incomingBytes, maximumBytes))
            {
                return;
            }

            if (File.Exists(oldPath))
            {
                File.Delete(oldPath);
            }
            File.Move(currentPath, oldPath);
        }
    }

    internal static class StartupCommand
    {
        internal static string ExtractExecutablePath(string command)
        {
            if (String.IsNullOrWhiteSpace(command))
            {
                return null;
            }

            string trimmed = command.Trim();
            if (trimmed[0] == '"')
            {
                int closingQuote = trimmed.IndexOf('"', 1);
                return closingQuote > 1 ? trimmed.Substring(1, closingQuote - 1) : null;
            }

            int firstSpace = trimmed.IndexOf(' ');
            return firstSpace < 0 ? trimmed : trimmed.Substring(0, firstSpace);
        }

        internal static bool IsCurrentExecutable(string command, string currentExecutable)
        {
            string registeredExecutable = ExtractExecutablePath(command);
            if (String.IsNullOrEmpty(registeredExecutable) || String.IsNullOrEmpty(currentExecutable))
            {
                return false;
            }

            try
            {
                return String.Equals(
                    Path.GetFullPath(registeredExecutable),
                    Path.GetFullPath(currentExecutable),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }

    internal static class AppLog
    {
        private const long MaximumLogBytes = 2L * 1024L * 1024L;
        private static readonly object Gate = new object();
        private static readonly string DirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NapeProBatteryTray");
        private static readonly string FilePath = Path.Combine(DirectoryPath, "app.log");
        private static readonly string OldFilePath = Path.Combine(DirectoryPath, "app.old.log");

        internal static void Write(string message)
        {
            string line = String.Format("{0:yyyy-MM-dd HH:mm:ss.fff} {1}{2}",
                DateTime.Now, message, Environment.NewLine);
            lock (Gate)
            {
                try
                {
                    Directory.CreateDirectory(DirectoryPath);
                    int incomingBytes = Encoding.UTF8.GetByteCount(line);
                    LogRotation.RotateIfNeeded(
                        FilePath, OldFilePath, MaximumLogBytes, incomingBytes);
                    File.AppendAllText(FilePath, line);
                }
                catch
                {
                }
            }
        }

        internal static string LogPath { get { return FilePath; } }
    }

    internal static class StatusBarPlacement
    {
        internal static Point ClampToBounds(Point desired, Size size, Rectangle bounds)
        {
            int width = Math.Max(1, size.Width);
            int height = Math.Max(1, size.Height);
            int maxX = Math.Max(bounds.Left, bounds.Right - width);
            int maxY = Math.Max(bounds.Top, bounds.Bottom - height);
            int x = Math.Min(Math.Max(desired.X, bounds.Left), maxX);
            int y = Math.Min(Math.Max(desired.Y, bounds.Top), maxY);
            return new Point(x, y);
        }
    }

    internal static class StatusBarPositionStore
    {
        private static readonly string DirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NapeProBatteryTray");
        private static readonly string FilePath = Path.Combine(DirectoryPath, "statusbar-position.txt");

        internal static string Serialize(Point location)
        {
            return String.Format(CultureInfo.InvariantCulture, "{0},{1}", location.X, location.Y);
        }

        internal static bool TryParse(string value, out Point location)
        {
            location = Point.Empty;
            if (String.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string[] parts = value.Trim().Split(',');
            int x;
            int y;
            if (parts.Length != 2 ||
                !Int32.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out x) ||
                !Int32.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out y))
            {
                return false;
            }

            location = new Point(x, y);
            return true;
        }

        internal static bool TryLoad(out Point location)
        {
            location = Point.Empty;
            try
            {
                if (!File.Exists(FilePath))
                {
                    return false;
                }
                return TryParse(File.ReadAllText(FilePath), out location);
            }
            catch (Exception ex)
            {
                AppLog.Write("Failed to load status bar position: " + ex);
                return false;
            }
        }

        internal static void Save(Point location)
        {
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                File.WriteAllText(FilePath, Serialize(location));
            }
            catch (Exception ex)
            {
                AppLog.Write("Failed to save status bar position: " + ex);
            }
        }
    }

    internal static class StatusBarDisplaySettings
    {
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NapeProBatteryTray",
            "show-connection.txt");

        internal static bool Load()
        {
            bool showConnection;
            return TryLoad(out showConnection) ? showConnection : true;
        }

        internal static bool TryParse(string value, out bool showConnection)
        {
            showConnection = true;
            if (String.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalized = value.Trim();
            if (normalized == "1" || String.Equals(normalized, "true", StringComparison.OrdinalIgnoreCase))
            {
                showConnection = true;
                return true;
            }
            if (normalized == "0" || String.Equals(normalized, "false", StringComparison.OrdinalIgnoreCase))
            {
                showConnection = false;
                return true;
            }
            return false;
        }

        internal static bool TryLoad(out bool showConnection)
        {
            showConnection = true;
            try
            {
                if (!File.Exists(FilePath))
                {
                    return false;
                }
                return TryParse(File.ReadAllText(FilePath), out showConnection);
            }
            catch (Exception ex)
            {
                AppLog.Write("Failed to load status bar display setting: " + ex);
                return false;
            }
        }

        internal static void Save(bool showConnection)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, showConnection ? "1" : "0");
            }
            catch (Exception ex)
            {
                AppLog.Write("Failed to save status bar display setting: " + ex);
            }
        }
    }

    internal static class StatusBarLayout
    {
        internal const int ExpandedWidth = 190;
        internal const int CompactWidth = 112;
        internal const int Height = 38;

        internal static int WidthFor(bool showConnection)
        {
            return showConnection ? ExpandedWidth : CompactWidth;
        }
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
            Console.WriteLine("VID/PID 0x3434/0x0440 または 0xD026、UsagePage 0xFF60 / 0x008C を探索します。\n");
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
            int failures = 0;
            byte[] request = new byte[32];
            request[0] = 0xA7;
            request[1] = 0x31;
            if (request.Length != 32 || request[0] != 0xA7 || request[1] != 0x31)
            {
                Console.WriteLine("FAIL request");
                failures++;
            }
            byte[] response = new byte[] { 0xA7, 0x31, 97 };
            if (response.Length != 3 || response[0] != 0xA7 || response[1] != 0x31 || response[2] != 97)
            {
                Console.WriteLine("FAIL response");
                failures++;
            }

            List<HidDeviceInfo> candidates = new List<HidDeviceInfo>
            {
                new HidDeviceInfo { Path = "usb-alt", VendorId = 0x3434, ProductId = 0x0440, UsagePage = 0x008C, ProductName = "Nape Pro" },
                new HidDeviceInfo { Path = "receiver-alt", VendorId = 0x3434, ProductId = 0xD026, UsagePage = 0x008C, ProductName = "Keychron Link-KM" },
                new HidDeviceInfo { Path = "usb-vendor", VendorId = 0x3434, ProductId = 0x0440, UsagePage = 0xFF60, ProductName = "Nape Pro" },
                new HidDeviceInfo { Path = "receiver-vendor", VendorId = 0x3434, ProductId = 0xD026, UsagePage = 0xFF60, ProductName = "Keychron Link-KM" }
            };
            candidates.Sort(NapeBatteryReader.CompareCandidates);
            if (candidates[0].ProductId != 0xD026 || candidates[0].UsagePage != 0xFF60 ||
                candidates[1].ProductId != 0x0440 || candidates[1].UsagePage != 0xFF60 ||
                candidates[2].ProductId != 0xD026 || candidates[2].UsagePage != 0x008C ||
                candidates[3].ProductId != 0x0440 || candidates[3].UsagePage != 0x008C)
            {
                Console.WriteLine("FAIL candidate priority: 2.4G vendor HID must be first");
                failures++;
            }

            List<HidDeviceInfo> queryCandidates = NapeBatteryReader.SelectQueryCandidates(candidates);
            if (queryCandidates.Count != 2 ||
                queryCandidates[0].UsagePage != 0xFF60 ||
                queryCandidates[1].UsagePage != 0xFF60)
            {
                Console.WriteLine("FAIL query candidates: 0x008C must be excluded when 0xFF60 exists");
                failures++;
            }

            List<HidDeviceInfo> alternateOnly = NapeBatteryReader.SelectQueryCandidates(
                new List<HidDeviceInfo>
                {
                    new HidDeviceInfo
                    {
                        Path = "receiver-alt-only",
                        VendorId = 0x3434,
                        ProductId = 0xD026,
                        UsagePage = 0x008C,
                        ProductName = "Keychron Link-KM"
                    }
                });
            if (alternateOnly.Count != 1 || alternateOnly[0].UsagePage != 0x008C)
            {
                Console.WriteLine("FAIL query candidates: 0x008C fallback must remain when 0xFF60 is absent");
                failures++;
            }

            // 画面全体の矩形。下 48px はタスクバーだが、重ね置きを許すので配置可能。
            Rectangle screenBounds = new Rectangle(0, 0, 1440, 1440);
            Point taskbarPosition = StatusBarPlacement.ClampToBounds(
                new Point(100, 1400), new Size(190, 38), screenBounds);
            if (taskbarPosition.X != 100 || taskbarPosition.Y != 1400)
            {
                Console.WriteLine("FAIL placement: taskbar area must remain usable");
                failures++;
            }

            // 画面外だけは必ず引き戻す。1440 - 38 = 1402 が下限。
            Point offscreenPosition = StatusBarPlacement.ClampToBounds(
                new Point(-100, 2000), new Size(190, 38), screenBounds);
            if (offscreenPosition.X != 0 || offscreenPosition.Y != 1402)
            {
                Console.WriteLine("FAIL placement: off-screen position was not clamped");
                failures++;
            }

            Point savedPosition;
            if (!StatusBarPositionStore.TryParse("2853,1397", out savedPosition) ||
                savedPosition.X != 2853 || savedPosition.Y != 1397)
            {
                Console.WriteLine("FAIL position persistence format");
                failures++;
            }

            bool showConnection;
            if (!StatusBarDisplaySettings.TryParse("1", out showConnection) || !showConnection)
            {
                Console.WriteLine("FAIL connection display setting: 1 must show");
                failures++;
            }
            if (!StatusBarDisplaySettings.TryParse("0", out showConnection) || showConnection)
            {
                Console.WriteLine("FAIL connection display setting: 0 must hide");
                failures++;
            }
            if (StatusBarDisplaySettings.TryParse("unexpected", out showConnection))
            {
                Console.WriteLine("FAIL connection display setting: invalid value accepted");
                failures++;
            }

            if (StatusBarLayout.WidthFor(true) != 190 || StatusBarLayout.WidthFor(false) != 112)
            {
                Console.WriteLine("FAIL status bar layout width");
                failures++;
            }

            if (LogRotation.ShouldRotate(100, 20, 120) ||
                !LogRotation.ShouldRotate(100, 21, 120))
            {
                Console.WriteLine("FAIL log rotation boundary");
                failures++;
            }

            string rotationDirectory = Path.Combine(
                Path.GetTempPath(), "NapeBar-self-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(rotationDirectory);
                string currentLog = Path.Combine(rotationDirectory, "app.log");
                string oldLog = Path.Combine(rotationDirectory, "app.old.log");
                File.WriteAllText(currentLog, "1234567890");
                File.WriteAllText(oldLog, "stale");
                LogRotation.RotateIfNeeded(currentLog, oldLog, 10, 1);
                if (File.Exists(currentLog) || File.ReadAllText(oldLog) != "1234567890")
                {
                    Console.WriteLine("FAIL log file rotation");
                    failures++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAIL log file rotation: " + ex.Message);
                failures++;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(rotationDirectory))
                    {
                        Directory.Delete(rotationDirectory, true);
                    }
                }
                catch
                {
                }
            }

            string currentExecutable = @"C:\Tools\NapeBar\NapeBar.exe";
            if (!StartupCommand.IsCurrentExecutable(
                    "\"C:\\Tools\\NapeBar\\NapeBar.exe\"",
                    currentExecutable) ||
                StartupCommand.IsCurrentExecutable(
                    "\"C:\\Downloads\\NapeBar.exe\"",
                    currentExecutable))
            {
                Console.WriteLine("FAIL startup executable path comparison");
                failures++;
            }

            AssemblyProductAttribute product = (AssemblyProductAttribute)Attribute.GetCustomAttribute(
                Assembly.GetExecutingAssembly(), typeof(AssemblyProductAttribute));
            AssemblyDescriptionAttribute description = (AssemblyDescriptionAttribute)Attribute.GetCustomAttribute(
                Assembly.GetExecutingAssembly(), typeof(AssemblyDescriptionAttribute));
            AssemblyFileVersionAttribute fileVersion = (AssemblyFileVersionAttribute)Attribute.GetCustomAttribute(
                Assembly.GetExecutingAssembly(), typeof(AssemblyFileVersionAttribute));
            if (product == null || product.Product != "NapeBar" ||
                description == null || description.Description != "Battery monitor for Keychron Nape Pro" ||
                fileVersion == null || fileVersion.Version != "0.1.7.0")
            {
                Console.WriteLine("FAIL NapeBar assembly metadata");
                failures++;
            }

            if (failures != 0)
            {
                return 1;
            }
            Console.WriteLine("PASS protocol, UI, log rotation, startup path, and product identity self-test");
            return 0;
        }

        private static int RunPing(List<HidDeviceInfo> devices)
        {
            List<HidDeviceInfo> candidates = devices.FindAll(delegate(HidDeviceInfo item)
            {
                return item.IsNapeCandidate && item.UsagePage == 0xFF60;
            });
            candidates.Sort(NapeBatteryReader.CompareCandidates);
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
            StartupSettings.MigrateExistingRegistration();
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
            ToolStripMenuItem resetStatusBarItem = new ToolStripMenuItem("ステータスバーの位置をリセット");
            resetStatusBarItem.Click += delegate { _statusBar.ResetPosition(); };
            menu.Items.Add(resetStatusBarItem);
            ToolStripMenuItem showConnectionItem = new ToolStripMenuItem("2.4G表示");
            showConnectionItem.Checked = _statusBar.ShowConnection;
            showConnectionItem.CheckOnClick = true;
            showConnectionItem.Click += delegate
            {
                StatusBarDisplaySettings.Save(showConnectionItem.Checked);
                _statusBar.SetShowConnection(showConnectionItem.Checked);
            };
            menu.Items.Add(showConnectionItem);
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
            _notifyIcon.MouseClick += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                {
                    ShowStatusBar();
                }
            };
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

    internal static class WindowZOrder
    {
        private static readonly IntPtr HwndTopMost = new IntPtr(-1);
        private const uint SwpNoSize = 0x0001;
        private const uint SwpNoMove = 0x0002;
        private const uint SwpNoActivate = 0x0010;

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

        // タスクバーもこのバーと同じ TOPMOST 帯にいる。シェルがフライアウトなどで
        // 自分を持ち上げると、こちらは WS_EX_NOACTIVATE で自力復帰できず下敷きになる。
        // 定期的に TOPMOST 帯の先頭へ入れ直すことで、タスクバーの上に留まる。
        internal static bool RaiseToTop(IntPtr handle)
        {
            return SetWindowPos(handle, HwndTopMost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
        }
    }

    internal sealed class BatteryStatusBarForm : Form
    {
        private const int DragThreshold = 4;
        private const int TopMostRecheckMs = 400;
        private readonly System.Windows.Forms.Timer _topMostTimer;
        private int _battery = -1;
        private string _connection = "--";
        private bool _dragArmed;
        private bool _dragging;
        private Point _dragOffset;
        private bool _hasPosition;
        private bool _showConnection;

        internal BatteryStatusBarForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            ShowIcon = false;
            ControlBox = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(StatusBarLayout.ExpandedWidth, StatusBarLayout.Height);
            BackColor = Color.FromArgb(38, 48, 59);
            ForeColor = Color.White;
            DoubleBuffered = true;
            Cursor = Cursors.SizeAll;
            Text = "Nape Pro battery";
            _showConnection = StatusBarDisplaySettings.Load();
            Size = new Size(StatusBarLayout.WidthFor(_showConnection), StatusBarLayout.Height);

            Point savedPosition;
            if (StatusBarPositionStore.TryLoad(out savedPosition))
            {
                Location = ClampToScreen(savedPosition);
                _hasPosition = true;
            }

            MouseDown += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                {
                    _dragArmed = true;
                    _dragging = false;
                    _dragOffset = e.Location;
                    Capture = true;
                }
            };
            MouseMove += delegate(object sender, MouseEventArgs e)
            {
                if (!_dragArmed)
                {
                    return;
                }

                if (!_dragging &&
                    Math.Abs(e.X - _dragOffset.X) < DragThreshold &&
                    Math.Abs(e.Y - _dragOffset.Y) < DragThreshold)
                {
                    return;
                }

                _dragging = true;
                Point cursor = PointToScreen(e.Location);
                Point desired = new Point(cursor.X - _dragOffset.X, cursor.Y - _dragOffset.Y);
                Location = ClampToScreen(desired);
            };
            MouseUp += delegate
            {
                if (_dragArmed && _dragging)
                {
                    StatusBarPositionStore.Save(Location);
                }
                _dragArmed = false;
                _dragging = false;
                Capture = false;
            };

            _topMostTimer = new System.Windows.Forms.Timer();
            _topMostTimer.Interval = TopMostRecheckMs;
            _topMostTimer.Tick += delegate { KeepAboveTaskbar(); };
            _topMostTimer.Start();
        }

        private void KeepAboveTaskbar()
        {
            if (!Visible || !IsHandleCreated || IsDisposed)
            {
                return;
            }

            try
            {
                WindowZOrder.RaiseToTop(Handle);
            }
            catch (Exception ex)
            {
                AppLog.Write("Failed to keep the status bar above the taskbar: " + ex);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _topMostTimer != null)
            {
                _topMostTimer.Stop();
                _topMostTimer.Dispose();
            }
            base.Dispose(disposing);
        }

        internal void ShowAtTop()
        {
            if (!_hasPosition)
            {
                MoveToDefaultPosition();
            }
            else
            {
                Location = ClampToScreen(Location);
            }
            Show();
            BringToFront();
        }

        internal void ResetPosition()
        {
            MoveToDefaultPosition();
            if (Visible)
            {
                BringToFront();
            }
        }

        internal bool ShowConnection
        {
            get { return _showConnection; }
        }

        internal void SetShowConnection(bool showConnection)
        {
            if (_showConnection == showConnection)
            {
                return;
            }
            _showConnection = showConnection;
            Size = new Size(StatusBarLayout.WidthFor(_showConnection), StatusBarLayout.Height);
            Location = ClampToScreen(Location);
            StatusBarPositionStore.Save(Location);
            Invalidate();
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                parameters.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                return parameters;
            }
        }

        private void MoveToDefaultPosition()
        {
            Screen screen = Screen.FromPoint(Cursor.Position);
            Rectangle area = screen.WorkingArea;
            Point desired = new Point(area.Left + (area.Width - Width) / 2, area.Top + 8);
            Location = ClampToScreen(desired);
            _hasPosition = true;
            StatusBarPositionStore.Save(Location);
        }

        private Point ClampToScreen(Point desired)
        {
            // WorkingArea ではなく Bounds に収める。タスクバーへの重ね置きを許すためで、
            // 重なっても見え続けることは KeepAboveTaskbar の定期再前面化が担保する。
            // 画面外へ出ることだけは Bounds が防ぐ。
            Screen screen = Screen.FromRectangle(new Rectangle(desired, Size));
            return StatusBarPlacement.ClampToBounds(desired, Size, screen.Bounds);
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
                if (_showConnection)
                {
                    graphics.DrawLine(dividerPen, 106, 9, 106, 29);
                    graphics.DrawString(_connection, connectionFont, secondaryBrush, new RectangleF(115, 4, 66, 29), centered);
                }
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
        private const string ValueName = "NapeBar";
        private const string LegacyValueName = "NapeProBatteryTray";

        private static string CurrentExecutable
        {
            get { return Assembly.GetExecutingAssembly().Location; }
        }

        private static string CurrentCommand
        {
            get { return '"' + CurrentExecutable + '"'; }
        }

        internal static bool IsEnabled()
        {
            using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, false))
            {
                string command = key == null ? null : Convert.ToString(key.GetValue(ValueName));
                return StartupCommand.IsCurrentExecutable(command, CurrentExecutable);
            }
        }

        internal static void MigrateExistingRegistration()
        {
            try
            {
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    object currentValue = key.GetValue(ValueName);
                    object legacyValue = key.GetValue(LegacyValueName);
                    if (currentValue != null || legacyValue != null)
                    {
                        key.SetValue(ValueName, CurrentCommand);
                    }
                    key.DeleteValue(LegacyValueName, false);
                }
            }
            catch (Exception ex)
            {
                AppLog.Write("Failed to migrate startup registration: " + ex.Message);
            }
        }

        internal static void SetEnabled(bool enabled)
        {
            using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (enabled)
                {
                    key.SetValue(ValueName, CurrentCommand);
                }
                else
                {
                    key.DeleteValue(ValueName, false);
                }
                key.DeleteValue(LegacyValueName, false);
            }
        }
    }
#endif
}
