// HORI Truck Control System (HPC-044U) — workaround del bug en Unity HID parser.
//
// Bug verificado empíricamente:
//   - Raw HID via P/Invoke (CreateFile/ReadFile a \\?\hid#vid_0f0d&pid_017a&col01)
//     muestra bytes 21-22 cambiando 0..65535 al pisar throttle. Confirmado con
//     /tmp/hori-diag/hid-poll.ps1.
//   - Unity Input System auto-genera layout para el HORI pero por los 2 sliders
//     duplicados (HID Usage 0x36 declarado dos veces) en el descriptor, slider
//     y slider1 alias al mismo byte (probablemente clutch byte 19-20 o brake
//     byte 23-24). El byte del throttle (21-22) queda HUÉRFANO — ningún
//     AxisControl lo lee, y Unity NO emite state events cuando solo el byte
//     huérfano cambia. Verificado: HoriThrottleReader v1 con InputSystem.onEvent
//     intercept jamás vio cambios en los bytes 21-22 aunque el usuario pisaba
//     el throttle (los 40 events capturados solo mostraban movimiento del
//     wheel/sticks en bytes centered).
//
// Solución (Plan B): bypass completo de Unity Input System. Abrimos el HID
// device path \\?\hid#vid_0f0d&pid_017a&col01#... con CreateFile y un thread
// background hace ReadFile loops, leyendo el raw input report. Los bytes
// 21-22 (LE16, normalizado 0..1) van a la propiedad Value, expuesta a
// UIInputNew via Gley.UrbanSystem.UIInputNew.HoriThrottleProvider delegate.
//
// Windows-only (HORI HPC-044U es Windows-only, project es Windows-only).

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

public class HoriThrottleReader : MonoBehaviour
{
    static HoriThrottleReader _instance;
    public static HoriThrottleReader Instance => _instance;

    /// <summary>Throttle normalizado 0..1 (rest=0, press=1). volatile garantiza
    /// visibilidad cross-thread (worker escribe, main thread lee).</summary>
    public float Value => _value;
    private volatile float _value;

#if HORI_USE_PINVOKE
    private const int VID = 0x0F0D;
    private const int PID = 0x017A;

    private SafeFileHandle _hidHandle;
    private Thread _pollerThread;
    private volatile bool _running;
    private string _devicePath;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("[HoriThrottleReader]");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<HoriThrottleReader>();
        Gley.UrbanSystem.UIInputNew.HoriThrottleProvider = () =>
            _instance != null ? _instance.Value : 0f;
    }

    void Start()
    {
        TryStartPoller();
    }

    void Update()
    {
        // Si el wheel se conectó después del start, reintentar abrir cada 2s.
        if (_pollerThread != null && _pollerThread.IsAlive) return;
        _retryTimer += Time.unscaledDeltaTime;
        if (_retryTimer >= 2f)
        {
            _retryTimer = 0f;
            TryStartPoller();
        }
    }

    private float _retryTimer;

    void TryStartPoller()
    {
        try
        {
            string path = FindHoriCol01Path();
            if (string.IsNullOrEmpty(path))
            {
                return;  // Wheel not connected; will retry.
            }
            _devicePath = path;
            _hidHandle = CreateFile(path, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
            if (_hidHandle.IsInvalid)
            {
                int err = Marshal.GetLastWin32Error();
                Debug.LogWarning($"[HoriThrottleReader] CreateFile fallo path='{path}' lastError={err}");
                _hidHandle.Dispose();
                _hidHandle = null;
                return;
            }
            _running = true;
            _pollerThread = new Thread(PollLoop) { IsBackground = true, Name = "HoriHidPoller" };
            _pollerThread.Start();
            Debug.Log($"[HoriThrottleReader] Started P/Invoke poller on {path}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[HoriThrottleReader] TryStartPoller exception: {ex.Message}");
        }
    }

    void PollLoop()
    {
        byte[] buffer = new byte[64];
        while (_running)
        {
            try
            {
                bool ok = ReadFile(_hidHandle, buffer, (uint)buffer.Length, out uint bytesRead, IntPtr.Zero);
                if (!ok)
                {
                    int err = Marshal.GetLastWin32Error();
                    if (!_running) break;
                    Debug.LogWarning($"[HoriThrottleReader] ReadFile fallo lastError={err}, parando poller");
                    break;
                }
                // Bytes 21-22 LE16 = throttle raw (verificado vía hid-poll.ps1).
                if (bytesRead >= 23)
                {
                    ushort raw = (ushort)(buffer[21] | (buffer[22] << 8));
                    _value = raw / 65535f;
                }
            }
            catch (Exception ex)
            {
                if (_running) Debug.LogError($"[HoriThrottleReader] Poll exception: {ex.Message}");
                break;
            }
        }
        _running = false;
        // Reset Value a 0 al perder el handle/unplug — evita aceleración fantasma
        // si el wheel se desconecta con el throttle pisado (codex review).
        _value = 0f;
    }

    void OnDestroy()
    {
        _running = false;
        try { _hidHandle?.Dispose(); } catch {}
        _hidHandle = null;
        try { _pollerThread?.Join(500); } catch {}
        _pollerThread = null;
        if (Gley.UrbanSystem.UIInputNew.HoriThrottleProvider != null)
            Gley.UrbanSystem.UIInputNew.HoriThrottleProvider = null;
    }

    // ─────────────────────────────────────────────────────────────────────
    // P/Invoke a hid.dll/setupapi.dll/kernel32.dll
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
                    Marshal.WriteInt32(buf, IntPtr.Size == 8 ? 8 : 6); // cbSize
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

    static string FindHoriCol01Path()
    {
        var paths = EnumerateHidPaths();
        // Filtramos por VID/PID y la collection 01 (joystick interface).
        // Format del path: \\?\hid#vid_0f0d&pid_017a&col01#...
        string vidpid = $"vid_{VID:x4}&pid_{PID:x4}";
        foreach (var p in paths)
        {
            string lower = p.ToLowerInvariant();
            if (lower.Contains(vidpid) && lower.Contains("col01"))
                return p;
        }
        return null;
    }

#else
    // Stub para non-Windows (no-op).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("[HoriThrottleReader]");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<HoriThrottleReader>();
        Gley.UrbanSystem.UIInputNew.HoriThrottleProvider = () => 0f;
    }
#endif
}
