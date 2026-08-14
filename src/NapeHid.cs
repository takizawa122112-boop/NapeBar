using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace NapeProBatteryTray
{
    internal static class NativeMethods
    {
        internal const uint DIGCF_PRESENT = 0x00000002;
        internal const uint DIGCF_DEVICEINTERFACE = 0x00000010;
        internal const uint GENERIC_READ = 0x80000000;
        internal const uint GENERIC_WRITE = 0x40000000;
        internal const uint FILE_SHARE_READ = 0x00000001;
        internal const uint FILE_SHARE_WRITE = 0x00000002;
        internal const uint OPEN_EXISTING = 3;
        internal const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
        internal const int ERROR_NO_MORE_ITEMS = 259;
        internal static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        [StructLayout(LayoutKind.Sequential)]
        internal struct SP_DEVICE_INTERFACE_DATA
        {
            public int cbSize;
            public Guid InterfaceClassGuid;
            public int Flags;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct HIDD_ATTRIBUTES
        {
            public int Size;
            public ushort VendorID;
            public ushort ProductID;
            public ushort VersionNumber;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct HIDP_CAPS
        {
            public ushort Usage;
            public ushort UsagePage;
            public ushort InputReportByteLength;
            public ushort OutputReportByteLength;
            public ushort FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
            public ushort[] Reserved;
            public ushort NumberLinkCollectionNodes;
            public ushort NumberInputButtonCaps;
            public ushort NumberInputValueCaps;
            public ushort NumberInputDataIndices;
            public ushort NumberOutputButtonCaps;
            public ushort NumberOutputValueCaps;
            public ushort NumberOutputDataIndices;
            public ushort NumberFeatureButtonCaps;
            public ushort NumberFeatureValueCaps;
            public ushort NumberFeatureDataIndices;
        }

        [DllImport("hid.dll")]
        internal static extern void HidD_GetHidGuid(out Guid HidGuid);

        [DllImport("hid.dll", SetLastError = true)]
        internal static extern bool HidD_GetAttributes(IntPtr HidDeviceObject, ref HIDD_ATTRIBUTES Attributes);

        [DllImport("hid.dll", SetLastError = true)]
        internal static extern bool HidD_GetPreparsedData(IntPtr HidDeviceObject, out IntPtr PreparsedData);

        [DllImport("hid.dll", SetLastError = true)]
        internal static extern bool HidD_FreePreparsedData(IntPtr PreparsedData);

        [DllImport("hid.dll")]
        internal static extern int HidP_GetCaps(IntPtr PreparsedData, out HIDP_CAPS Capabilities);

        [DllImport("hid.dll", SetLastError = true)]
        internal static extern bool HidD_GetProductString(IntPtr HidDeviceObject, StringBuilder Buffer, int BufferLength);

        [DllImport("hid.dll", SetLastError = true)]
        internal static extern bool HidD_GetSerialNumberString(IntPtr HidDeviceObject, StringBuilder Buffer, int BufferLength);

        [DllImport("hid.dll", SetLastError = true)]
        internal static extern bool HidD_SetNumInputBuffers(IntPtr HidDeviceObject, int NumberBuffers);

        [DllImport("hid.dll", SetLastError = true)]
        internal static extern bool HidD_SetOutputReport(IntPtr HidDeviceObject, byte[] Report, int ReportBufferLength);

        [DllImport("setupapi.dll", SetLastError = true)]
        internal static extern IntPtr SetupDiGetClassDevs(
            ref Guid ClassGuid,
            IntPtr Enumerator,
            IntPtr hwndParent,
            uint Flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        internal static extern bool SetupDiEnumDeviceInterfaces(
            IntPtr DeviceInfoSet,
            IntPtr DeviceInfoData,
            ref Guid InterfaceClassGuid,
            uint MemberIndex,
            ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "SetupDiGetDeviceInterfaceDetailW")]
        internal static extern bool SetupDiGetDeviceInterfaceDetail(
            IntPtr DeviceInfoSet,
            ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData,
            IntPtr DeviceInterfaceDetailData,
            int DeviceInterfaceDetailDataSize,
            out int RequiredSize,
            IntPtr DeviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        internal static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr CreateFile(
            string FileName,
            uint DesiredAccess,
            uint ShareMode,
            IntPtr SecurityAttributes,
            uint CreationDisposition,
            uint FlagsAndAttributes,
            IntPtr TemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool CloseHandle(IntPtr Handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool ReadFile(
            IntPtr Handle,
            byte[] Buffer,
            int NumberOfBytesToRead,
            out int NumberOfBytesRead,
            IntPtr Overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool WriteFile(
            IntPtr Handle,
            byte[] Buffer,
            int NumberOfBytesToWrite,
            out int NumberOfBytesWritten,
            IntPtr Overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool CancelIoEx(IntPtr FileHandle, IntPtr Overlapped);
    }

    internal sealed class HidDeviceInfo
    {
        public string Path;
        public int VendorId;
        public int ProductId;
        public int UsagePage;
        public int Usage;
        public int InputReportLength;
        public int OutputReportLength;
        public string ProductName;
        public string SerialNumber;

        public bool IsNapeCandidate
        {
            get
            {
                if (VendorId != 0x3434 || (UsagePage != 0xFF60 && UsagePage != 0x008C))
                {
                    return false;
                }
                return ProductId == 0x0440 || ProductId == 0xD026 ||
                    ContainsIgnoreCase(ProductName, "Nape") ||
                    ContainsIgnoreCase(ProductName, "Link-KM");
            }
        }

        public string ConnectionLabel
        {
            get
            {
                if (ProductId == 0xD026 || ContainsIgnoreCase(ProductName, "Link-KM"))
                {
                    return "2.4G";
                }
                if (ProductId == 0x0440 || ContainsIgnoreCase(ProductName, "Nape"))
                {
                    return "USB / device";
                }
                return "HID";
            }
        }

        public override string ToString()
        {
            return String.Format(
                "{0} VID=0x{1:X4} PID=0x{2:X4} UsagePage=0x{3:X4} Usage=0x{4:X4} Input={5} Output={6}",
                String.IsNullOrEmpty(ProductName) ? "Unknown" : ProductName,
                VendorId,
                ProductId,
                UsagePage,
                Usage,
                InputReportLength,
                OutputReportLength);
        }

        private static bool ContainsIgnoreCase(string value, string part)
        {
            return !String.IsNullOrEmpty(value) && value.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static HidDeviceInfo Read(string path, Action<string> log)
        {
            IntPtr handle = NativeMethods.CreateFile(
                path,
                NativeMethods.GENERIC_READ | NativeMethods.GENERIC_WRITE,
                NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
                IntPtr.Zero,
                NativeMethods.OPEN_EXISTING,
                NativeMethods.FILE_ATTRIBUTE_NORMAL,
                IntPtr.Zero);

            if (handle == NativeMethods.INVALID_HANDLE_VALUE)
            {
                handle = NativeMethods.CreateFile(
                    path,
                    0,
                    NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    NativeMethods.OPEN_EXISTING,
                    NativeMethods.FILE_ATTRIBUTE_NORMAL,
                    IntPtr.Zero);
            }

            if (handle == NativeMethods.INVALID_HANDLE_VALUE)
            {
                if (log != null)
                {
                    log(String.Format("CreateFile failed for HID path: Win32 error {0}", Marshal.GetLastWin32Error()));
                }
                return null;
            }

            try
            {
                NativeMethods.HIDD_ATTRIBUTES attributes = new NativeMethods.HIDD_ATTRIBUTES();
                attributes.Size = Marshal.SizeOf(typeof(NativeMethods.HIDD_ATTRIBUTES));
                if (!NativeMethods.HidD_GetAttributes(handle, ref attributes))
                {
                    if (log != null)
                    {
                        log(String.Format("HidD_GetAttributes failed: Win32 error {0}", Marshal.GetLastWin32Error()));
                    }
                    return null;
                }

                IntPtr preparsed;
                if (!NativeMethods.HidD_GetPreparsedData(handle, out preparsed))
                {
                    if (log != null)
                    {
                        log(String.Format("HidD_GetPreparsedData failed: Win32 error {0}", Marshal.GetLastWin32Error()));
                    }
                    return null;
                }

                try
                {
                    NativeMethods.HIDP_CAPS caps;
                    int status = NativeMethods.HidP_GetCaps(preparsed, out caps);
                    if (status != 0 && status != 0x00110000)
                    {
                        if (log != null)
                        {
                            log(String.Format("HidP_GetCaps failed for {0}: NTSTATUS 0x{1:X8}", path, status));
                        }
                    }

                    StringBuilder product = new StringBuilder(256);
                    StringBuilder serial = new StringBuilder(256);
                    NativeMethods.HidD_GetProductString(handle, product, product.Capacity);
                    NativeMethods.HidD_GetSerialNumberString(handle, serial, serial.Capacity);

                    string productName = product.ToString();
                    if (attributes.ProductID == 0xD026)
                    {
                        productName = "Keychron Link-KM";
                    }
                    else if (attributes.ProductID == 0x0440)
                    {
                        productName = "Nape Pro";
                    }

                    return new HidDeviceInfo
                    {
                        Path = path,
                        VendorId = attributes.VendorID,
                        ProductId = attributes.ProductID,
                        UsagePage = caps.UsagePage,
                        Usage = caps.Usage,
                        InputReportLength = caps.InputReportByteLength,
                        OutputReportLength = caps.OutputReportByteLength,
                        ProductName = productName,
                        SerialNumber = serial.ToString()
                    };
                }
                finally
                {
                    NativeMethods.HidD_FreePreparsedData(preparsed);
                }
            }
            catch (Exception ex)
            {
                if (log != null)
                {
                    log("HID metadata read failed: " + ex.Message);
                }
                return null;
            }
            finally
            {
                NativeMethods.CloseHandle(handle);
            }
        }
    }

    internal static class HidEnumerator
    {
        internal static List<HidDeviceInfo> Enumerate(Action<string> log)
        {
            List<HidDeviceInfo> devices = new List<HidDeviceInfo>();
            Guid hidGuid;
            NativeMethods.HidD_GetHidGuid(out hidGuid);
            IntPtr infoSet = NativeMethods.SetupDiGetClassDevs(
                ref hidGuid,
                IntPtr.Zero,
                IntPtr.Zero,
                NativeMethods.DIGCF_PRESENT | NativeMethods.DIGCF_DEVICEINTERFACE);

            if (infoSet == IntPtr.Zero || infoSet == NativeMethods.INVALID_HANDLE_VALUE)
            {
                if (log != null)
                {
                    log("SetupDiGetClassDevs failed: " + Marshal.GetLastWin32Error());
                }
                return devices;
            }

            try
            {
                uint index = 0;
                while (true)
                {
                    NativeMethods.SP_DEVICE_INTERFACE_DATA interfaceData = new NativeMethods.SP_DEVICE_INTERFACE_DATA();
                    interfaceData.cbSize = Marshal.SizeOf(typeof(NativeMethods.SP_DEVICE_INTERFACE_DATA));

                    if (!NativeMethods.SetupDiEnumDeviceInterfaces(
                        infoSet,
                        IntPtr.Zero,
                        ref hidGuid,
                        index,
                        ref interfaceData))
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (error != NativeMethods.ERROR_NO_MORE_ITEMS && log != null)
                        {
                            log(String.Format("SetupDiEnumDeviceInterfaces failed at {0}: {1}", index, error));
                        }
                        break;
                    }

                    int requiredSize;
                    NativeMethods.SetupDiGetDeviceInterfaceDetail(
                        infoSet,
                        ref interfaceData,
                        IntPtr.Zero,
                        0,
                        out requiredSize,
                        IntPtr.Zero);

                    if (requiredSize > 0)
                    {
                        byte[] detail = new byte[requiredSize];
                        GCHandle pinned = GCHandle.Alloc(detail, GCHandleType.Pinned);
                        try
                        {
                            int cbSize = IntPtr.Size == 8 ? 8 : 6;
                            Marshal.WriteInt32(pinned.AddrOfPinnedObject(), cbSize);
                            if (NativeMethods.SetupDiGetDeviceInterfaceDetail(
                                infoSet,
                                ref interfaceData,
                                pinned.AddrOfPinnedObject(),
                                detail.Length,
                                out requiredSize,
                                IntPtr.Zero))
                            {
                                // The Windows API requires cbSize=8 in a 64-bit process,
                                // but the UTF-16 path still starts immediately after the
                                // 4-byte cbSize field when the detail buffer is byte-packed.
                                int pathOffset = 4;
                                IntPtr pathAddress = new IntPtr(pinned.AddrOfPinnedObject().ToInt64() + pathOffset);
                                string path = Marshal.PtrToStringUni(pathAddress);
                                if (!String.IsNullOrEmpty(path))
                                {
                                    HidDeviceInfo device = HidDeviceInfo.Read(path, log);
                                    if (device != null)
                                    {
                                        devices.Add(device);
                                    }
                                }
                            }
                            else if (log != null)
                            {
                                log(String.Format("SetupDiGetDeviceInterfaceDetail failed: Win32 error {0}", Marshal.GetLastWin32Error()));
                            }
                        }
                        finally
                        {
                            pinned.Free();
                        }
                    }
                    else if (log != null)
                    {
                        log(String.Format("No HID interface detail size at index {0}: Win32 error {1}", index, Marshal.GetLastWin32Error()));
                    }

                    index++;
                }
            }
            finally
            {
                NativeMethods.SetupDiDestroyDeviceInfoList(infoSet);
            }

            return devices;
        }
    }

    internal sealed class HidConnection : IDisposable
    {
        private readonly HidDeviceInfo _info;
        private readonly Action<string> _log;
        private readonly AutoResetEvent _batteryResponse = new AutoResetEvent(false);
        private readonly AutoResetEvent _anyResponse = new AutoResetEvent(false);
        private readonly object _responseLock = new object();
        private readonly object _anyResponseLock = new object();
        private IntPtr _readHandle;
        private IntPtr _writeHandle;
        private Thread _readThread;
        private volatile bool _stopping;
        private int _lastBattery = -1;
        private int _inputReportLogCount;
        private byte[] _lastResponse;

        private HidConnection(HidDeviceInfo info, IntPtr readHandle, IntPtr writeHandle, Action<string> log)
        {
            _info = info;
            _readHandle = readHandle;
            _writeHandle = writeHandle;
            _log = log;
        }

        internal HidDeviceInfo Info { get { return _info; } }

        internal static HidConnection Open(HidDeviceInfo info, Action<string> log)
        {
            IntPtr readHandle = CreateHidHandle(
                info.Path,
                NativeMethods.GENERIC_READ | NativeMethods.GENERIC_WRITE,
                NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
                IntPtr.Zero,
                NativeMethods.OPEN_EXISTING,
                NativeMethods.FILE_ATTRIBUTE_NORMAL,
                IntPtr.Zero);

            if (readHandle == NativeMethods.INVALID_HANDLE_VALUE)
            {
                if (log != null)
                {
                    log(String.Format("Cannot open {0}: Win32 error {1}", info, Marshal.GetLastWin32Error()));
                }
                return null;
            }

            IntPtr writeHandle = CreateHidHandle(
                info.Path,
                NativeMethods.GENERIC_READ | NativeMethods.GENERIC_WRITE,
                NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
                IntPtr.Zero,
                NativeMethods.OPEN_EXISTING,
                NativeMethods.FILE_ATTRIBUTE_NORMAL,
                IntPtr.Zero);
            if (writeHandle == NativeMethods.INVALID_HANDLE_VALUE)
            {
                if (log != null)
                {
                    log(String.Format("Cannot open second HID handle for {0}: Win32 error {1}", info, Marshal.GetLastWin32Error()));
                }
                NativeMethods.CloseHandle(readHandle);
                return null;
            }

            HidConnection connection = new HidConnection(info, readHandle, writeHandle, log);
            if (log != null)
            {
                log("HidConnection.Open: handle acquired");
            }
            NativeMethods.HidD_SetNumInputBuffers(readHandle, 64);
            connection.StartReader();
            if (log != null)
            {
                log("HidConnection.Open: reader started");
            }
            return connection;
        }

        private static IntPtr CreateHidHandle(
            string path,
            uint access,
            uint shareMode,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile)
        {
            return NativeMethods.CreateFile(
                path,
                access,
                shareMode,
                securityAttributes,
                creationDisposition,
                flagsAndAttributes,
                templateFile);
        }

        private void StartReader()
        {
            _readThread = new Thread(ReadLoop);
            _readThread.IsBackground = true;
            _readThread.Name = "Nape Pro HID reader";
            _readThread.Start();
        }

        internal bool TryGetBattery(int timeoutMs, out int battery)
        {
            battery = -1;
            if (_stopping || _writeHandle == IntPtr.Zero || _writeHandle == NativeMethods.INVALID_HANDLE_VALUE)
            {
                return false;
            }

            if (_log != null)
            {
                _log(String.Format("Battery query on UsagePage 0x{0:X4}", _info.UsagePage));
            }

            int attemptTimeout = Math.Max(300, timeoutMs / 4);
            byte[][] reports = new byte[][]
            {
                BuildCommandReport(0xA7, 0x31, true),
                BuildCommandReport(0xA7, 0x31, false)
            };

            for (int i = 0; i < reports.Length; i++)
            {
                if (TrySendAndWait(reports[i], true, attemptTimeout, out battery))
                {
                    return true;
                }
                if (TrySendAndWait(reports[i], false, attemptTimeout, out battery))
                {
                    return true;
                }
            }

            return false;
        }

        internal bool TryRawCommand(byte first, byte second, int timeoutMs, out byte[] response)
        {
            response = null;
            byte[][] reports = new byte[][]
            {
                BuildCommandReport(first, second, true),
                BuildCommandReport(first, second, false)
            };
            int attemptTimeout = Math.Max(300, timeoutMs / 4);
            for (int i = 0; i < reports.Length; i++)
            {
                for (int transport = 0; transport < 2; transport++)
                {
                    _anyResponse.Reset();
                    int written = 0;
                    bool sent;
                    if (transport == 0)
                    {
                        sent = NativeMethods.HidD_SetOutputReport(_writeHandle, reports[i], reports[i].Length);
                    }
                    else
                    {
                        sent = NativeMethods.WriteFile(_writeHandle, reports[i], reports[i].Length, out written, IntPtr.Zero) && written == reports[i].Length;
                    }
                    if (!sent || !_anyResponse.WaitOne(attemptTimeout, false))
                    {
                        continue;
                    }
                    lock (_anyResponseLock)
                    {
                        response = _lastResponse == null ? null : (byte[])_lastResponse.Clone();
                    }
                    if (response != null)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private byte[] BuildCommandReport(byte first, byte second, bool includeReportId)
        {
            int length = includeReportId ? 33 : 32;
            int payloadOffset = includeReportId ? 1 : 0;
            byte[] report = new byte[length];
            report[payloadOffset] = first;
            report[payloadOffset + 1] = second;
            return report;
        }

        private bool TrySendAndWait(byte[] report, bool useHidDSetOutputReport, int timeoutMs, out int battery)
        {
            battery = -1;
            _batteryResponse.Reset();
            int written = 0;
            bool sent;
            if (useHidDSetOutputReport)
            {
                sent = NativeMethods.HidD_SetOutputReport(_writeHandle, report, report.Length);
            }
            else
            {
                sent = NativeMethods.WriteFile(_writeHandle, report, report.Length, out written, IntPtr.Zero) && written == report.Length;
            }

            if (_log != null)
            {
                int error = sent ? 0 : Marshal.GetLastWin32Error();
                _log(String.Format(
                    "Battery query frame={0} transport={1} sent={2} bytes={3}/{4} error={5}",
                    report.Length == 33 ? "report-id" : "payload-only",
                    useHidDSetOutputReport ? "HidD_SetOutputReport" : "WriteFile",
                    sent,
                    useHidDSetOutputReport ? report.Length : written,
                    report.Length,
                    error));
            }

            if (!sent || !_batteryResponse.WaitOne(timeoutMs, false))
            {
                return false;
            }

            lock (_responseLock)
            {
                battery = _lastBattery;
            }
            return battery >= 0 && battery <= 100;
        }

        private void ReadLoop()
        {
            int length = _info.InputReportLength > 0 ? _info.InputReportLength : 64;
            byte[] buffer = new byte[length];
            while (!_stopping)
            {
                int bytesRead;
                bool ok;
                try
                {
                    ok = NativeMethods.ReadFile(_readHandle, buffer, buffer.Length, out bytesRead, IntPtr.Zero);
                }
                catch (Exception ex)
                {
                    if (!_stopping && _log != null)
                    {
                        _log("HID read failed: " + ex.Message);
                    }
                    break;
                }

                if (!ok)
                {
                    if (!_stopping && _log != null)
                    {
                        _log(String.Format("HID read stopped: Win32 error {0}", Marshal.GetLastWin32Error()));
                    }
                    break;
                }

                if (bytesRead <= 0)
                {
                    continue;
                }

                lock (_anyResponseLock)
                {
                    _lastResponse = new byte[bytesRead];
                    Array.Copy(buffer, _lastResponse, bytesRead);
                }
                _anyResponse.Set();

                int commandOffset = 0;
                if (bytesRead >= 3 && buffer[0] == 0xA7 && buffer[1] == 0x31)
                {
                    commandOffset = 0;
                }
                else if (bytesRead >= 4 && buffer[1] == 0xA7 && buffer[2] == 0x31)
                {
                    commandOffset = 1;
                }
                else
                {
                    if (_log != null && _inputReportLogCount < 8)
                    {
                        _inputReportLogCount++;
                        _log("HID input (unmatched): " + Hex(buffer, bytesRead, 20));
                    }
                    continue;
                }

                int value = buffer[commandOffset + 2];
                if (value > 100)
                {
                    continue;
                }
                if (_log != null)
                {
                    _log("HID battery response: " + Hex(buffer, bytesRead, 12));
                }
                lock (_responseLock)
                {
                    _lastBattery = value;
                }
                _batteryResponse.Set();
            }
        }

        private static string Hex(byte[] data, int length, int maxBytes)
        {
            int count = Math.Min(length, maxBytes);
            StringBuilder builder = new StringBuilder(count * 3);
            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                {
                    builder.Append(' ');
                }
                builder.Append(data[i].ToString("X2"));
            }
            if (length > count)
            {
                builder.Append(" ...");
            }
            return builder.ToString();
        }

        public void Dispose()
        {
            if (_stopping)
            {
                return;
            }
            _stopping = true;
            IntPtr readHandle = _readHandle;
            IntPtr writeHandle = _writeHandle;
            _readHandle = IntPtr.Zero;
            _writeHandle = IntPtr.Zero;
            if (readHandle != IntPtr.Zero && readHandle != NativeMethods.INVALID_HANDLE_VALUE)
            {
                if (_log != null)
                {
                    _log("HidConnection.Dispose: stopping reader");
                }
                NativeMethods.CancelIoEx(readHandle, IntPtr.Zero);
            }

            bool readerStopped = true;
            if (_readThread != null && _readThread.IsAlive && Thread.CurrentThread != _readThread)
            {
                if (_log != null)
                {
                    _log("HidConnection.Dispose: joining reader");
                }
                readerStopped = _readThread.Join(2000);
            }

            if (readHandle != IntPtr.Zero && readHandle != NativeMethods.INVALID_HANDLE_VALUE)
            {
                NativeMethods.CloseHandle(readHandle);
            }
            if (writeHandle != IntPtr.Zero && writeHandle != NativeMethods.INVALID_HANDLE_VALUE)
            {
                NativeMethods.CloseHandle(writeHandle);
            }

            if (readerStopped)
            {
                _batteryResponse.Close();
                _anyResponse.Close();
            }
            else if (_log != null)
            {
                _log("HidConnection.Dispose: reader did not stop; keeping wait handles alive");
            }
        }
    }

    internal sealed class BatteryReading
    {
        public bool Success;
        public int BatteryLevel;
        public string Error;
        public HidDeviceInfo Device;

        public static BatteryReading Failed(string error)
        {
            return new BatteryReading { Success = false, BatteryLevel = -1, Error = error };
        }
    }

    internal sealed class NapeBatteryReader : IDisposable
    {
        private readonly object _gate = new object();
        private readonly Action<string> _log;
        private HidConnection _connection;

        internal NapeBatteryReader(Action<string> log)
        {
            _log = log;
        }

        internal BatteryReading Query(int timeoutMs)
        {
            lock (_gate)
            {
                if (_connection != null)
                {
                    int battery;
                    if (_connection.TryGetBattery(timeoutMs, out battery))
                    {
                        return new BatteryReading
                        {
                            Success = true,
                            BatteryLevel = battery,
                            Device = _connection.Info
                        };
                    }

                    _connection.Dispose();
                    _connection = null;
                }

                List<HidDeviceInfo> candidates = GetCandidates();
                for (int i = 0; i < candidates.Count; i++)
                {
                    HidConnection connection = HidConnection.Open(candidates[i], _log);
                    if (connection == null)
                    {
                        continue;
                    }

                    int battery;
                    if (connection.TryGetBattery(timeoutMs, out battery))
                    {
                        _connection = connection;
                        return new BatteryReading
                        {
                            Success = true,
                            BatteryLevel = battery,
                            Device = connection.Info
                        };
                    }
                    connection.Dispose();
                }

                return BatteryReading.Failed("Nape Pro device was not found or did not return a battery response.");
            }
        }

        private List<HidDeviceInfo> GetCandidates()
        {
            List<HidDeviceInfo> devices = HidEnumerator.Enumerate(_log);
            List<HidDeviceInfo> candidates = devices.FindAll(delegate(HidDeviceInfo item) { return item.IsNapeCandidate; });
            candidates.Sort(CompareCandidates);
            if (candidates.Count == 0 && _log != null)
            {
                _log("No Nape Pro vendor HID interface found.");
            }
            return candidates;
        }

        internal static int CompareCandidates(HidDeviceInfo left, HidDeviceInfo right)
        {
            int byUsagePage = CandidatePriority(left).CompareTo(CandidatePriority(right));
            if (byUsagePage != 0)
            {
                return byUsagePage;
            }

            int byTransport = TransportPriority(left).CompareTo(TransportPriority(right));
            if (byTransport != 0)
            {
                return byTransport;
            }

            return String.CompareOrdinal(left.Path ?? String.Empty, right.Path ?? String.Empty);
        }

        private static int CandidatePriority(HidDeviceInfo device)
        {
            if (device.UsagePage == 0xFF60)
            {
                return 0;
            }
            if (device.UsagePage == 0x008C)
            {
                return 1;
            }
            return 2;
        }

        private static int TransportPriority(HidDeviceInfo device)
        {
            if (device.ConnectionLabel == "2.4G")
            {
                return 0;
            }
            if (device.ConnectionLabel == "USB / device")
            {
                return 1;
            }
            return 2;
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_connection != null)
                {
                    _connection.Dispose();
                    _connection = null;
                }
            }
        }
    }
}
