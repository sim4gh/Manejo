// HoriShifterProbe.cs — diagnostic-only HID byte capturer.
//
// PROPÓSITO (v1.5.10 únicamente):
//   El sticky latch de UIInputNew para reversa en HORI Manual depende de
//   button7 PULSE del shifter, que solo dispara al ENTRAR a R (verificado
//   empíricamente). Cuando el lever sale de R sin pasar por gears 1-6, no
//   hay señal de salida y el sticky queda pegado → "R pegada" reportada
//   por Norberto 2026-05-07.
//
//   La solución definitiva (v1.6.0) es leer el byte continuo de posición
//   del shifter — análogo a lo que HoriThrottleReader hizo con bytes
//   21-22 del wheel para el throttle. Pero NO sabemos qué byte usa el
//   shifter para posición de palanca. Esta probe captura el HID stream
//   raw del shifter, loggea cambios de bytes a Debug.Log, y LogUploader
//   lo sube a S3 cada 5 min. Norberto recorre la palanca en orden conocido
//   (N→1→N→2→...→6→N→R→N), descargamos los logs, identificamos el byte.
//
// CONVIVENCIA:
//   - Salta paths con "col01" — esos pertenecen al wheel y los polea
//     HoriThrottleReader. Abrirlos aquí también drowna la señal del
//     shifter con tráfico continuo del wheel (steering/pedales).
//   - Re-enumera cada 2s para hot-plug del shifter (mismo patrón que
//     HoriThrottleReader.TryStartPoller).
//   - Rate-limit por canal a 100ms para no spamear el buffer circular
//     de 5000 líneas del LogUploader.
//
// RETIRO:
//   Eliminar este archivo + meta cuando v1.6.0 ship con HoriShifterReader.

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

public class HoriShifterProbe : MonoBehaviour
{
    static HoriShifterProbe _instance;

    private const int VID = 0x0F0D;

#if HORI_USE_PINVOKE
    private class ProbeChannel
    {
        public string path;
        public SafeFileHandle handle;
        public Thread thread;
        public byte[] lastReport = new byte[64];
        public bool firstReport = true;
        public string label;
        public long lastLogTickMs;
        public volatile bool dead;
    }

    private readonly List<ProbeChannel> _channels = new List<ProbeChannel>();
    private readonly HashSet<string> _openedPaths = new HashSet<string>();
    private volatile bool _running = true;
    private float _retryTimer;
    private int _channelCounter;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("[HoriShifterProbe]");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<HoriShifterProbe>();
        Debug.Log("[HoriShifterProbe] Bootstrapped (v1.5.10 diagnostic — busca byte de posición palanca HORI)");
    }

    void Start()
    {
        TryOpenChannels();
    }

    void Update()
    {
        // Re-enumera cada 2s (hot-plug del shifter o re-enumeration de Windows).
        _retryTimer += Time.unscaledDeltaTime;
        if (_retryTimer < 2f) return;
        _retryTimer = 0f;
        TryOpenChannels();
    }

    void TryOpenChannels()
    {
        try
        {
            var paths = EnumerateHidPaths();
            foreach (var p in paths)
            {
                string lower = p.ToLowerInvariant();
                if (!lower.Contains("vid_0f0d")) continue;
                // col01 del wheel ya es policy de HoriThrottleReader y trae
                // tráfico continuo (steering/pedales) que tapa la señal del
                // shifter. No abrir aquí.
                if (lower.Contains("col01")) continue;
                if (_openedPaths.Contains(p)) continue;

                Debug.Log($"[HoriShifterProbe] HORI HID path encontrado: {p}");
                var handle = CreateFile(p, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE,
                    IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                if (handle.IsInvalid)
                {
                    int err = Marshal.GetLastWin32Error();
                    Debug.LogWarning($"[HoriShifterProbe] CreateFile fallo path='{p}' err={err}");
                    handle.Dispose();
                    continue;
                }

                var ch = new ProbeChannel
                {
                    path = p,
                    handle = handle,
                    label = $"ch{_channelCounter++}",
                };
                ch.thread = new Thread(() => PollLoop(ch))
                {
                    IsBackground = true,
                    Name = $"HoriShifterProbe-{ch.label}",
                };
                _channels.Add(ch);
                _openedPaths.Add(p);
                ch.thread.Start();
                Debug.Log($"[HoriShifterProbe] Reader iniciado [{ch.label}] sobre {p}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[HoriShifterProbe] TryOpenChannels exception: {ex.Message}");
        }
    }

    void PollLoop(ProbeChannel ch)
    {
        byte[] buffer = new byte[64];
        var sb = new System.Text.StringBuilder(192);
        while (_running)
        {
            try
            {
                bool ok = ReadFile(ch.handle, buffer, (uint)buffer.Length, out uint bytesRead, IntPtr.Zero);
                if (!ok || bytesRead == 0)
                {
                    int err = Marshal.GetLastWin32Error();
                    if (_running)
                        Debug.LogWarning($"[HoriShifterProbe][{ch.label}] ReadFile fallo err={err}, parando reader");
                    break;
                }

                // Rate-limit por canal: log máximo cada 100ms para no saturar
                // el buffer del LogUploader cuando varios bytes cambian rápido.
                // Environment.TickCount es thread-safe (Time.* no lo es).
                long nowMs = Environment.TickCount;
                bool rateLimited = !ch.firstReport && (nowMs - ch.lastLogTickMs) < 100;

                sb.Length = 0;
                bool anyChange = false;
                int bytesToScan = (int)Math.Min(bytesRead, ch.lastReport.Length);
                for (int i = 0; i < bytesToScan; i++)
                {
                    if (ch.firstReport || buffer[i] != ch.lastReport[i])
                    {
                        if (anyChange) sb.Append(' ');
                        sb.AppendFormat("b{0:D2}:{1:X2}->{2:X2}", i, ch.lastReport[i], buffer[i]);
                        ch.lastReport[i] = buffer[i];
                        anyChange = true;
                    }
                }

                if (anyChange && !rateLimited)
                {
                    string tag = ch.firstReport ? "INIT" : "CHG";
                    Debug.Log($"[HoriShifterProbe][{ch.label}][{tag}] {bytesRead}B {sb}");
                    ch.lastLogTickMs = nowMs;
                    ch.firstReport = false;
                }
                else if (anyChange)
                {
                    // Si rate-limited, marcamos firstReport=false para que el
                    // próximo log sólo muestre deltas (sin inundar con el INIT).
                    ch.firstReport = false;
                }
            }
            catch (Exception ex)
            {
                if (_running)
                    Debug.LogError($"[HoriShifterProbe][{ch.label}] Poll exception: {ex.Message}");
                break;
            }
        }
        ch.dead = true;
    }

    void OnDestroy()
    {
        _running = false;
        foreach (var ch in _channels)
        {
            try { ch.handle?.Dispose(); } catch { }
            try { ch.thread?.Join(500); } catch { }
        }
        _channels.Clear();
        _openedPaths.Clear();
    }

    // ─────────────────────────────────────────────────────────────────────
    // P/Invoke a hid.dll/setupapi.dll/kernel32.dll (clones de
    // HoriThrottleReader — mantener en sync si esa rutina cambia).
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
    // Stub no-op fuera de Windows. La probe es Windows-only (HORI HPC-044U lo es).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        // No-op.
    }
#endif
}
