// HoriShifterReader.cs — continuous raw HID reader for HORI Truck shifter lever.
//
// Replaces the pulse-based button7 heuristic (v1.5.8-v1.5.12 sticky latch) with
// direct P/Invoke HID reading. Same pattern as HoriThrottleReader.cs.
//
// The HORI HPC-044U shifter reports gear positions as buttons (trigger=1st,
// button2-6=2nd-6th, button7=R) through Unity Input System, but button7 only
// fires as a 1-2 frame PULSE. This reader opens the raw HID handle and reads
// the actual byte state, bypassing Unity's parser.
//
// On first connect, logs all byte changes to the console for discovery. Once
// the gear byte is identified (either auto-detected or via PlayerPrefs config),
// provides persistent gear position to UIInputNew.

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
#define HORI_USE_PINVOKE
#endif

using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
#if HORI_USE_PINVOKE
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
#endif

public class HoriShifterReader : MonoBehaviour
{
    static HoriShifterReader _instance;
    public static HoriShifterReader Instance => _instance;

    public int CurrentGear => _currentGear;
    private volatile int _currentGear;

    public bool IsLeverInR => _currentGear == -1;
    public bool IsConnected => _connected;
    private volatile bool _connected;

#if HORI_USE_PINVOKE
    private const int VID = 0x0F0D;

    // Gear byte/bit mapping. Configured via PlayerPrefs after discovery.
    // -1 = not yet calibrated (discovery mode active).
    private int _gearByteIndex = -1;
    private int _reverseBit = -1;   // bit index 0-7 within the gear byte
    private int _gear1Bit = -1;
    private int _gear2Bit = -1;
    private int _gear3Bit = -1;
    private int _gear4Bit = -1;
    private int _gear5Bit = -1;
    private int _gear6Bit = -1;
    // Alternative: some shifters encode gear position as a single value
    // in one byte (hat-switch style), not as individual button bits.
    // Set to true if we discover that pattern.
    private bool _useValueMode;
    private int _valueByteIndex = -1;
    private int _valueReverse = -1;
    private int _valueGear1 = -1;
    private int _valueGear2 = -1;
    private int _valueGear3 = -1;
    private int _valueGear4 = -1;
    private int _valueGear5 = -1;
    private int _valueGear6 = -1;
    private int _valueNeutral = -1;

    private bool _calibrated;
    // Habilita el spam de "CHG b##:XX->YY" después de calibrate=True (default OFF
    // en producción para no ahogar Player.log con miles de líneas por minuto).
    // Activar desde shell: PlayerPrefs SetInt Diag_HoriHidProbe 1 (mismo patrón
    // que Diag_HoriShifterProbe que gatea HoriShifterProbe.Bootstrap).
    private bool _diagLogEnabled;
    private SafeFileHandle _hidHandle;
    private Thread _pollerThread;
    private volatile bool _running;
    private string _devicePath;
    private float _retryTimer;

    private const string PREF_PREFIX = "HoriShifter_";
    private const string PREF_DIAG_LOG = "Diag_HoriHidProbe";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("[HoriShifterReader]");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<HoriShifterReader>();
        Gley.UrbanSystem.UIInputNew.HoriShifterStateProvider = () =>
            _instance != null && _instance._connected ? _instance.CurrentGear : int.MinValue;
        Debug.Log("[HoriShifterReader] Bootstrapped — raw HID reader for HORI shifter lever position");
    }

    void Start()
    {
        _diagLogEnabled = PlayerPrefs.GetInt(PREF_DIAG_LOG, 0) == 1;
        LoadCalibration();
        if (!_calibrated)
            ApplyHPC044UDefaults();
        if (_diagLogEnabled)
            Debug.Log("[HoriShifterReader] Diag_HoriHidProbe=1 — CHG spam habilitado para diagnóstico");
        TryStartPoller();
    }

    // HPC-044U (PID 0x0186) verified mapping: b01 bits, persistent while in gear.
    void ApplyHPC044UDefaults()
    {
        _useValueMode = false;
        _gearByteIndex = 1;
        _gear1Bit = 0;   // 0x01
        _gear2Bit = 1;   // 0x02
        _gear3Bit = 2;   // 0x04
        _gear4Bit = 3;   // 0x08
        _gear5Bit = 4;   // 0x10
        _gear6Bit = 5;   // 0x20
        _reverseBit = 6; // 0x40
        _calibrated = true;
        Debug.Log("[HoriShifterReader] Applied HPC-044U defaults: byte[1] bits 0-5=gears, 6=R");
    }

    void Update()
    {
        if (_pollerThread != null && _pollerThread.IsAlive) return;
        _retryTimer += Time.unscaledDeltaTime;
        if (_retryTimer >= 2f)
        {
            _retryTimer = 0f;
            TryStartPoller();
        }
    }

    void LoadCalibration()
    {
        string mode = PlayerPrefs.GetString(PREF_PREFIX + "Mode", "");
        if (mode == "bits")
        {
            _gearByteIndex = PlayerPrefs.GetInt(PREF_PREFIX + "ByteIndex", -1);
            _reverseBit = PlayerPrefs.GetInt(PREF_PREFIX + "ReverseBit", -1);
            _gear1Bit = PlayerPrefs.GetInt(PREF_PREFIX + "Gear1Bit", -1);
            _gear2Bit = PlayerPrefs.GetInt(PREF_PREFIX + "Gear2Bit", -1);
            _gear3Bit = PlayerPrefs.GetInt(PREF_PREFIX + "Gear3Bit", -1);
            _gear4Bit = PlayerPrefs.GetInt(PREF_PREFIX + "Gear4Bit", -1);
            _gear5Bit = PlayerPrefs.GetInt(PREF_PREFIX + "Gear5Bit", -1);
            _gear6Bit = PlayerPrefs.GetInt(PREF_PREFIX + "Gear6Bit", -1);
            _calibrated = _gearByteIndex >= 0 && _reverseBit >= 0;
            if (_calibrated)
                Debug.Log($"[HoriShifterReader] Loaded bits calibration: byte[{_gearByteIndex}] R=bit{_reverseBit}");
        }
        else if (mode == "value")
        {
            _useValueMode = true;
            _valueByteIndex = PlayerPrefs.GetInt(PREF_PREFIX + "ValueByteIndex", -1);
            _valueNeutral = PlayerPrefs.GetInt(PREF_PREFIX + "ValueNeutral", -1);
            _valueReverse = PlayerPrefs.GetInt(PREF_PREFIX + "ValueReverse", -1);
            _valueGear1 = PlayerPrefs.GetInt(PREF_PREFIX + "ValueGear1", -1);
            _valueGear2 = PlayerPrefs.GetInt(PREF_PREFIX + "ValueGear2", -1);
            _valueGear3 = PlayerPrefs.GetInt(PREF_PREFIX + "ValueGear3", -1);
            _valueGear4 = PlayerPrefs.GetInt(PREF_PREFIX + "ValueGear4", -1);
            _valueGear5 = PlayerPrefs.GetInt(PREF_PREFIX + "ValueGear5", -1);
            _valueGear6 = PlayerPrefs.GetInt(PREF_PREFIX + "ValueGear6", -1);
            _calibrated = _valueByteIndex >= 0 && _valueReverse >= 0;
            if (_calibrated)
                Debug.Log($"[HoriShifterReader] Loaded value calibration: byte[{_valueByteIndex}] R={_valueReverse}");
        }
    }

    /// <summary>Configure gear mapping in bits mode (each gear = one bit in a byte).
    /// Call from console or discovery tool, then call SaveCalibration().</summary>
    public void ConfigureBitsMode(int byteIndex, int rBit, int g1Bit, int g2Bit, int g3Bit, int g4Bit, int g5Bit, int g6Bit)
    {
        _useValueMode = false;
        _gearByteIndex = byteIndex;
        _reverseBit = rBit;
        _gear1Bit = g1Bit;
        _gear2Bit = g2Bit;
        _gear3Bit = g3Bit;
        _gear4Bit = g4Bit;
        _gear5Bit = g5Bit;
        _gear6Bit = g6Bit;
        _calibrated = true;
        SaveCalibration();
        Debug.Log($"[HoriShifterReader] Configured bits mode: byte[{byteIndex}] R=bit{rBit} 1=bit{g1Bit} ... 6=bit{g6Bit}");
    }

    /// <summary>Configure gear mapping in value mode (single byte encodes gear as a number).</summary>
    public void ConfigureValueMode(int byteIndex, int neutral, int reverse, int g1, int g2, int g3, int g4, int g5, int g6)
    {
        _useValueMode = true;
        _valueByteIndex = byteIndex;
        _valueNeutral = neutral;
        _valueReverse = reverse;
        _valueGear1 = g1;
        _valueGear2 = g2;
        _valueGear3 = g3;
        _valueGear4 = g4;
        _valueGear5 = g5;
        _valueGear6 = g6;
        _calibrated = true;
        SaveCalibration();
        Debug.Log($"[HoriShifterReader] Configured value mode: byte[{byteIndex}] N={neutral} R={reverse} 1={g1} ... 6={g6}");
    }

    void SaveCalibration()
    {
        if (_useValueMode)
        {
            PlayerPrefs.SetString(PREF_PREFIX + "Mode", "value");
            PlayerPrefs.SetInt(PREF_PREFIX + "ValueByteIndex", _valueByteIndex);
            PlayerPrefs.SetInt(PREF_PREFIX + "ValueNeutral", _valueNeutral);
            PlayerPrefs.SetInt(PREF_PREFIX + "ValueReverse", _valueReverse);
            PlayerPrefs.SetInt(PREF_PREFIX + "ValueGear1", _valueGear1);
            PlayerPrefs.SetInt(PREF_PREFIX + "ValueGear2", _valueGear2);
            PlayerPrefs.SetInt(PREF_PREFIX + "ValueGear3", _valueGear3);
            PlayerPrefs.SetInt(PREF_PREFIX + "ValueGear4", _valueGear4);
            PlayerPrefs.SetInt(PREF_PREFIX + "ValueGear5", _valueGear5);
            PlayerPrefs.SetInt(PREF_PREFIX + "ValueGear6", _valueGear6);
        }
        else
        {
            PlayerPrefs.SetString(PREF_PREFIX + "Mode", "bits");
            PlayerPrefs.SetInt(PREF_PREFIX + "ByteIndex", _gearByteIndex);
            PlayerPrefs.SetInt(PREF_PREFIX + "ReverseBit", _reverseBit);
            PlayerPrefs.SetInt(PREF_PREFIX + "Gear1Bit", _gear1Bit);
            PlayerPrefs.SetInt(PREF_PREFIX + "Gear2Bit", _gear2Bit);
            PlayerPrefs.SetInt(PREF_PREFIX + "Gear3Bit", _gear3Bit);
            PlayerPrefs.SetInt(PREF_PREFIX + "Gear4Bit", _gear4Bit);
            PlayerPrefs.SetInt(PREF_PREFIX + "Gear5Bit", _gear5Bit);
            PlayerPrefs.SetInt(PREF_PREFIX + "Gear6Bit", _gear6Bit);
        }
        PlayerPrefs.Save();
    }

    void TryStartPoller()
    {
        try
        {
            string path = FindShifterPath();
            if (string.IsNullOrEmpty(path)) return;
            _devicePath = path;
            _hidHandle = CreateFile(path, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
            if (_hidHandle.IsInvalid)
            {
                int err = Marshal.GetLastWin32Error();
                Debug.LogWarning($"[HoriShifterReader] CreateFile failed path='{path}' err={err}");
                _hidHandle.Dispose();
                _hidHandle = null;
                return;
            }
            _running = true;
            _connected = true;
            _pollerThread = new Thread(PollLoop) { IsBackground = true, Name = "HoriShifterPoller" };
            _pollerThread.Start();
            Debug.Log($"[HoriShifterReader] Started poller on {path} (calibrated={_calibrated})");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[HoriShifterReader] TryStartPoller exception: {ex.Message}");
        }
    }

    void PollLoop()
    {
        byte[] buffer = new byte[64];
        byte[] prev = new byte[64];
        bool first = true;
        var sb = new System.Text.StringBuilder(256);
        long lastLogMs = 0;

        while (_running)
        {
            try
            {
                bool ok = ReadFile(_hidHandle, buffer, (uint)buffer.Length, out uint bytesRead, IntPtr.Zero);
                if (!ok)
                {
                    if (_running)
                    {
                        int err = Marshal.GetLastWin32Error();
                        Debug.LogWarning($"[HoriShifterReader] ReadFile failed err={err}, stopping");
                    }
                    break;
                }
                if (bytesRead == 0) continue;

                // --- Discovery logging: always log byte changes ---
                long nowMs = Environment.TickCount;
                if (first)
                {
                    sb.Length = 0;
                    sb.Append($"[HoriShifterReader] INIT {bytesRead}B:");
                    for (int i = 0; i < (int)bytesRead && i < 32; i++)
                        sb.AppendFormat(" b{0:D2}={1:X2}", i, buffer[i]);
                    Debug.Log(sb.ToString());
                    Buffer.BlockCopy(buffer, 0, prev, 0, (int)bytesRead);
                    first = false;
                    continue;
                }

                bool anyChange = false;
                for (int i = 0; i < (int)bytesRead && i < prev.Length; i++)
                {
                    if (buffer[i] != prev[i]) { anyChange = true; break; }
                }

                // Spam silenciado en producción cuando ya hay calibración activa.
                // Durante discovery (!_calibrated) seguimos logueando para no
                // perder el descubrimiento del byte de palanca. Diag_HoriHidProbe=1
                // re-habilita el spam para troubleshooting remoto.
                bool logChanges = !_calibrated || _diagLogEnabled;
                if (logChanges && anyChange && (nowMs - lastLogMs) >= 100)
                {
                    sb.Length = 0;
                    sb.Append("[HoriShifterReader] CHG");
                    for (int i = 0; i < (int)bytesRead && i < prev.Length; i++)
                    {
                        if (buffer[i] != prev[i])
                            sb.AppendFormat(" b{0:D2}:{1:X2}->{2:X2}", i, prev[i], buffer[i]);
                    }
                    Debug.Log(sb.ToString());
                    lastLogMs = nowMs;
                }
                Buffer.BlockCopy(buffer, 0, prev, 0, (int)bytesRead);

                // --- Derive current gear from raw bytes ---
                if (_calibrated)
                {
                    if (_useValueMode)
                    {
                        if (_valueByteIndex >= 0 && _valueByteIndex < (int)bytesRead)
                        {
                            int val = buffer[_valueByteIndex];
                            if (val == _valueReverse)     _currentGear = -1;
                            else if (val == _valueGear1)  _currentGear = 1;
                            else if (val == _valueGear2)  _currentGear = 2;
                            else if (val == _valueGear3)  _currentGear = 3;
                            else if (val == _valueGear4)  _currentGear = 4;
                            else if (val == _valueGear5)  _currentGear = 5;
                            else if (val == _valueGear6)  _currentGear = 6;
                            else                          _currentGear = 0;
                        }
                    }
                    else
                    {
                        if (_gearByteIndex >= 0 && _gearByteIndex < (int)bytesRead)
                        {
                            int b = buffer[_gearByteIndex];
                            if (_reverseBit >= 0 && (b & (1 << _reverseBit)) != 0)
                                _currentGear = -1;
                            else if (_gear1Bit >= 0 && (b & (1 << _gear1Bit)) != 0)
                                _currentGear = 1;
                            else if (_gear2Bit >= 0 && (b & (1 << _gear2Bit)) != 0)
                                _currentGear = 2;
                            else if (_gear3Bit >= 0 && (b & (1 << _gear3Bit)) != 0)
                                _currentGear = 3;
                            else if (_gear4Bit >= 0 && (b & (1 << _gear4Bit)) != 0)
                                _currentGear = 4;
                            else if (_gear5Bit >= 0 && (b & (1 << _gear5Bit)) != 0)
                                _currentGear = 5;
                            else if (_gear6Bit >= 0 && (b & (1 << _gear6Bit)) != 0)
                                _currentGear = 6;
                            else
                                _currentGear = 0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (_running) Debug.LogError($"[HoriShifterReader] Poll exception: {ex.Message}");
                break;
            }
        }
        _running = false;
        _connected = false;
        _currentGear = 0;
    }

    void OnDestroy()
    {
        _running = false;
        try { _hidHandle?.Dispose(); } catch { }
        _hidHandle = null;
        try { _pollerThread?.Join(500); } catch { }
        _pollerThread = null;
        if (Gley.UrbanSystem.UIInputNew.HoriShifterStateProvider != null)
            Gley.UrbanSystem.UIInputNew.HoriShifterStateProvider = null;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Device enumeration: find the HORI shifter (PID 0x0186, col01)
    // ─────────────────────────────────────────────────────────────────────

    static string FindShifterPath()
    {
        var paths = EnumerateHidPaths();
        string fallback = null;
        foreach (var p in paths)
        {
            string lower = p.ToLowerInvariant();
            if (!lower.Contains("vid_0f0d")) continue;
            // Skip wheel device (PID 017A) — owned by HoriThrottleReader
            if (lower.Contains("pid_017a")) continue;
            // Prefer shifter PID 0186 col01 (game controller interface with gear data)
            if (lower.Contains("pid_0186") && lower.Contains("col01"))
                return p;
            if (fallback == null)
                fallback = p;
        }
        return fallback;
    }

    // ─────────────────────────────────────────────────────────────────────
    // P/Invoke — same as HoriThrottleReader/HoriShifterProbe
    // ─────────────────────────────────────────────────────────────────────

    private const uint GENERIC_READ = 0x80000000;
    private const uint FILE_SHARE_READ = 0x1;
    private const uint FILE_SHARE_WRITE = 0x2;
    private const uint OPEN_EXISTING = 3;
    private const uint DIGCF_PRESENT = 0x2;
    private const uint DIGCF_DEVICEINTERFACE = 0x10;

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVICE_INTERFACE_DATA { public int cbSize; public Guid g; public uint flags; public IntPtr reserved; }

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SetupDiGetClassDevs(ref Guid g, IntPtr enumerator, IntPtr hwndParent, uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInterfaces(IntPtr di, IntPtr deviceInfoData, ref Guid g, uint memberIndex, ref SP_DEVICE_INTERFACE_DATA o);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr di, ref SP_DEVICE_INTERFACE_DATA d, IntPtr buf, uint bufLen, ref uint required, IntPtr deviceInfo);

    [DllImport("setupapi.dll")]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr di);

    [DllImport("hid.dll")]
    private static extern void HidD_GetHidGuid(out Guid g);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern SafeFileHandle CreateFile(string fileName, uint access, uint share, IntPtr sa, uint creation, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadFile(SafeFileHandle handle, [Out] byte[] buffer, uint numBytes, out uint bytesRead, IntPtr overlapped);

    static List<string> EnumerateHidPaths()
    {
        var paths = new List<string>();
        Guid g; HidD_GetHidGuid(out g);
        IntPtr di = SetupDiGetClassDevs(ref g, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
        if (di == new IntPtr(-1)) return paths;
        try
        {
            uint i = 0;
            while (true)
            {
                var sdid = new SP_DEVICE_INTERFACE_DATA();
                sdid.cbSize = Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DATA));
                if (!SetupDiEnumDeviceInterfaces(di, IntPtr.Zero, ref g, i++, ref sdid)) break;
                uint req = 0;
                SetupDiGetDeviceInterfaceDetail(di, ref sdid, IntPtr.Zero, 0, ref req, IntPtr.Zero);
                IntPtr buf = Marshal.AllocHGlobal((int)req);
                try
                {
                    Marshal.WriteInt32(buf, IntPtr.Size == 8 ? 8 : 6);
                    if (SetupDiGetDeviceInterfaceDetail(di, ref sdid, buf, req, ref req, IntPtr.Zero))
                    {
                        string p = Marshal.PtrToStringAuto(new IntPtr(buf.ToInt64() + 4));
                        if (!string.IsNullOrEmpty(p)) paths.Add(p);
                    }
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
        }
        finally { SetupDiDestroyDeviceInfoList(di); }
        return paths;
    }

#else
    // Non-Windows stub (HORI HPC-044U is Windows-only).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("[HoriShifterReader]");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<HoriShifterReader>();
        Gley.UrbanSystem.UIInputNew.HoriShifterStateProvider = () => int.MinValue;
    }
#endif
}
