using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Networking;

/// <summary>
/// Captura todos los logs de Unity (Debug.Log/Warn/Error/Exception) en un buffer
/// circular y los sube periódicamente al backend, gzipped, mediante una presigned
/// URL emitida por el endpoint /simulator/logs/upload-url.
///
/// Flujo:
///   1. Subscribe a Application.logMessageReceivedThreaded (multi-thread safe).
///   2. Cada 5 minutos arma un payload con un snapshot de devices/inputs/prefs
///      seguido de las líneas de log, lo gzippa y lo sube via PUT.
///   3. Persiste cada línea a Application.persistentDataPath/logs/current.log
///      para sobrevivir crashes — al reiniciar incluye lo del archivo en el
///      próximo flush y luego trunca.
///   4. F7 panel sigue funcionando independientemente — este uploader no
///      modifica nada del LogConsolePanel.
/// </summary>
public class LogUploader : MonoBehaviour
{
    public static LogUploader Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoCreate()
    {
        if (Instance != null) return;
        GameObject go = new GameObject("[LogUploader]");
        go.AddComponent<LogUploader>();
        DontDestroyOnLoad(go);
    }

    // ── Constantes ───────────────────────────────────────────────────

    const int    MAX_BUFFER_LINES        = 5000;
    const float  FLUSH_INTERVAL_SECONDS  = 300f;   // 5 min
    const float  MIN_FLUSH_GAP_SECONDS   = 60f;    // anti-spam para ForceFlush
    const int    UPLOAD_TIMEOUT_SECONDS  = 60;
    const int    UPLOAD_URL_TIMEOUT_SEC  = 10;
    const int    MAX_CONSECUTIVE_FAILS   = 3;

    static string LogDirPath => Path.Combine(Application.persistentDataPath, "logs");
    static string LogFilePath => Path.Combine(LogDirPath, "current.log");

    // ── Modelos JSON (locked contract) ───────────────────────────────

    [Serializable]
    class UploadUrlRequest
    {
        public string pcId;
        public int size;
    }

    [Serializable]
    class UploadUrlResponse
    {
        public string uploadUrl;
        public string key;
        public int expiresIn;
    }

    // ── Buffer ───────────────────────────────────────────────────────

    [Serializable]
    class LogEntry
    {
        public string utcIso;
        public float realtime;
        public int frame;            // -1 si vino de un thread no-main
        public string type;
        public string condition;
        public string stackTrace;
    }

    readonly object bufferLock = new object();
    readonly List<LogEntry> buffer = new List<LogEntry>(1024);

    int consecutiveFails = 0;
    bool flushInFlight = false;
    float lastFlushAttempt = -9999f;

    // El callback threaded se ejecuta en cualquier thread; sólo el main thread
    // puede leer Time.frameCount, InputSystem, PlayerPrefs, etc. Usamos este
    // id para detectar si estamos en main.
    int mainThreadId = -1;

    // ── Lifecycle ────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        mainThreadId = Thread.CurrentThread.ManagedThreadId;

        try { Directory.CreateDirectory(LogDirPath); }
        catch (Exception e) { Debug.LogWarning($"[LogUploader] No pude crear logs dir: {e.Message}"); }

        // Pre-cargar lo que haya quedado de una sesión crasheada.
        LoadCrashedSession();
    }

    void OnEnable()  { Application.logMessageReceivedThreaded += OnLogThreaded; }
    void OnDisable() { Application.logMessageReceivedThreaded -= OnLogThreaded; }

    void Start()
    {
        StartCoroutine(PeriodicFlushLoop());
    }

    void OnApplicationQuit()
    {
        // Best-effort: arrancamos el flush, Unity puede matar la corutina
        // antes que termine. La próxima vez que arranque va a recoger lo
        // que esté en current.log.
        try { StartCoroutine(FlushAsync()); } catch { }
    }

    // ── Captura de logs ──────────────────────────────────────────────

    void OnLogThreaded(string condition, string stackTrace, LogType type)
    {
        // Evita feedback loop con nuestros propios mensajes.
        if (!string.IsNullOrEmpty(condition) && condition.StartsWith("[LogUploader]"))
            return;

        bool inMain = Thread.CurrentThread.ManagedThreadId == mainThreadId;

        var entry = new LogEntry
        {
            utcIso     = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            realtime   = inMain ? Time.realtimeSinceStartup : -1f,
            frame      = inMain ? Time.frameCount : -1,
            type       = type.ToString(),
            condition  = condition ?? "",
            stackTrace = stackTrace ?? "",
        };

        string line = FormatLine(entry);

        lock (bufferLock)
        {
            buffer.Add(entry);
            if (buffer.Count > MAX_BUFFER_LINES)
                buffer.RemoveAt(0); // drop oldest
        }

        // Append durable al disco (best-effort, sin spamear errores).
        try
        {
            File.AppendAllText(LogFilePath, line + "\n", Encoding.UTF8);
        }
        catch { /* disco lleno o permisos — el buffer en memoria sigue */ }
    }

    static string FormatLine(LogEntry e)
    {
        // Formato compacto, una línea, parseable.
        // [iso] [type] f=frame t=realtime cond | stack...
        string stack = (e.stackTrace ?? "").Replace("\r", "").Replace("\n", " | ");
        return $"[{e.utcIso}] [{e.type}] f={e.frame} t={e.realtime:F2} {e.condition} :: {stack}";
    }

    void LoadCrashedSession()
    {
        if (!File.Exists(LogFilePath)) return;
        try
        {
            string[] lines = File.ReadAllLines(LogFilePath);
            if (lines.Length == 0) return;

            lock (bufferLock)
            {
                int toTake = Math.Min(lines.Length, MAX_BUFFER_LINES);
                int start = lines.Length - toTake;
                for (int i = start; i < lines.Length; i++)
                {
                    buffer.Add(new LogEntry
                    {
                        utcIso     = "",
                        realtime   = -1f,
                        frame      = -1,
                        type       = "Recovered",
                        condition  = lines[i],
                        stackTrace = "",
                    });
                }
            }
            Debug.Log($"[LogUploader] Recuperadas {Math.Min(lines.Length, MAX_BUFFER_LINES)} líneas de sesión previa");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LogUploader] Error leyendo log previo: {e.Message}");
        }
    }

    // ── Flush loop ───────────────────────────────────────────────────

    IEnumerator PeriodicFlushLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(FLUSH_INTERVAL_SECONDS);
            if (!flushInFlight) StartCoroutine(FlushAsync());
        }
    }

    /// <summary>
    /// Forzar un flush manual (debounced a >=60s entre intentos).
    /// </summary>
    public void ForceFlush()
    {
        if (flushInFlight) return;
        if (Time.realtimeSinceStartup - lastFlushAttempt < MIN_FLUSH_GAP_SECONDS) return;
        StartCoroutine(FlushAsync());
    }

    IEnumerator FlushAsync()
    {
        if (flushInFlight) yield break;
        flushInFlight = true;
        lastFlushAttempt = Time.realtimeSinceStartup;

        // Snapshot del buffer (copia bajo lock, luego operamos sobre la copia).
        List<LogEntry> snapshot;
        lock (bufferLock)
        {
            if (buffer.Count == 0)
            {
                flushInFlight = false;
                yield break;
            }
            snapshot = new List<LogEntry>(buffer);
        }

        // Header con devices/inputs/prefs (main thread, seguro aquí).
        string header = BuildSnapshotHeader();

        // Construir payload.
        var sb = new StringBuilder(8192 + snapshot.Count * 128);
        sb.Append(header);
        sb.Append("=== LOGS ===\n");
        for (int i = 0; i < snapshot.Count; i++)
            sb.Append(FormatLine(snapshot[i])).Append('\n');

        byte[] gzipped;
        try
        {
            gzipped = Gzip(Encoding.UTF8.GetBytes(sb.ToString()));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LogUploader] Error gzipping: {e.Message}");
            flushInFlight = false;
            yield break;
        }

        // Paso 1: pedir presigned URL.
        var config = SimulatorConfig.Instance?.data;
        string pcId = config?.pcId ?? "";
        string baseUrl = (config != null && !string.IsNullOrEmpty(config.apiBaseUrl))
            ? config.apiBaseUrl
            : "https://d6twaegbhg.execute-api.us-east-1.amazonaws.com";

        string uploadEndpoint = $"{baseUrl}/simulator/logs/upload-url";
        string reqJson = JsonUtility.ToJson(new UploadUrlRequest { pcId = pcId, size = gzipped.Length });

        UploadUrlResponse urlResp = null;
        using (var request = new UnityWebRequest(uploadEndpoint, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(reqJson));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = UPLOAD_URL_TIMEOUT_SEC;

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                HandleFailure($"upload-url HTTP error: {request.error} ({request.responseCode})");
                flushInFlight = false;
                yield break;
            }

            try
            {
                urlResp = JsonUtility.FromJson<UploadUrlResponse>(request.downloadHandler.text);
            }
            catch (Exception e)
            {
                HandleFailure($"upload-url parse error: {e.Message}");
                flushInFlight = false;
                yield break;
            }
        }

        if (urlResp == null || string.IsNullOrEmpty(urlResp.uploadUrl))
        {
            HandleFailure("upload-url respuesta inválida");
            flushInFlight = false;
            yield break;
        }

        // Paso 2: PUT del gzip.
        using (var put = UnityWebRequest.Put(urlResp.uploadUrl, gzipped))
        {
            put.SetRequestHeader("Content-Type", "application/gzip");
            put.timeout = UPLOAD_TIMEOUT_SECONDS;
            yield return put.SendWebRequest();

            if (put.result != UnityWebRequest.Result.Success)
            {
                HandleFailure($"PUT failed: {put.error} ({put.responseCode})");
                flushInFlight = false;
                yield break;
            }
        }

        // Éxito — limpiar lo que SUBIMOS, no lo que pudo haberse acumulado
        // mientras la subida estaba en vuelo.
        int uploadedCount = snapshot.Count;
        lock (bufferLock)
        {
            if (buffer.Count <= uploadedCount)
                buffer.Clear();
            else
                buffer.RemoveRange(0, uploadedCount);
        }

        TruncateLogFile();

        consecutiveFails = 0;
        Debug.Log($"[LogUploader] Flushed {uploadedCount} lines, key={urlResp.key}");
        flushInFlight = false;
    }

    void HandleFailure(string reason)
    {
        consecutiveFails++;
        Debug.LogWarning($"[LogUploader] Flush failed ({consecutiveFails}/{MAX_CONSECUTIVE_FAILS}): {reason}");

        if (consecutiveFails >= MAX_CONSECUTIVE_FAILS)
        {
            lock (bufferLock) { buffer.Clear(); }
            TruncateLogFile();
            consecutiveFails = 0;
            Debug.LogWarning("[LogUploader] Buffer descartado tras 3 fallos consecutivos");
        }
    }

    void TruncateLogFile()
    {
        try
        {
            if (File.Exists(LogFilePath))
                File.WriteAllText(LogFilePath, string.Empty);
        }
        catch { /* no-op */ }
    }

    static byte[] Gzip(byte[] input)
    {
        using (var ms = new MemoryStream())
        {
            using (var gz = new GZipStream(ms, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
            {
                gz.Write(input, 0, input.Length);
            }
            return ms.ToArray();
        }
    }

    // ── Snapshot (main thread) ───────────────────────────────────────

    string BuildSnapshotHeader()
    {
        var sb = new StringBuilder(4096);
        string iso = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        sb.Append($"=== SNAPSHOT @ {iso} ===\n");

        // Identidad
        var cfg = SimulatorConfig.Instance?.data;
        sb.Append("-- IDENTITY --\n");
        sb.Append($"pcId         = {cfg?.pcId ?? ""}\n");
        sb.Append($"name         = {cfg?.name ?? ""}\n");
        sb.Append($"simulatorId  = {cfg?.simulatorId ?? ""}\n");
        sb.Append($"appVersion   = {Application.version}\n");
        sb.Append($"unity        = {Application.unityVersion}\n");
        sb.Append($"platform     = {Application.platform}\n");
        sb.Append($"productName  = {Application.productName}\n");
        sb.Append('\n');

        // Devices
        sb.Append("-- DEVICES --\n");
        try
        {
            int n = 0;
            foreach (var d in InputSystem.devices)
            {
                n++;
                string product = string.IsNullOrEmpty(d.description.product) ? "?" : d.description.product;
                sb.Append($"{d.displayName}  [{d.layout}]\n");
                sb.Append($"  path    = {d.path}\n");
                sb.Append($"  product = {product}\n");
            }
            if (n == 0) sb.Append("(ningún device detectado)\n");
        }
        catch (Exception e) { sb.Append($"(error listando devices: {e.Message})\n"); }
        sb.Append('\n');

        // Inputs activos (botones presionados ahora mismo + ejes con valor != 0)
        sb.Append("-- ACTIVE INPUTS --\n");
        try
        {
            bool any = false;
            foreach (var dev in InputSystem.devices)
            {
                var local = new StringBuilder(256);
                int hits = 0;
                foreach (var ctrl in dev.allControls)
                {
                    if (ctrl is ButtonControl btn)
                    {
                        if (!btn.isPressed) continue;
                        local.Append($"   BTN  {GetDeviceRelativePath(ctrl, dev)}\n");
                        hits++;
                    }
                    else if (ctrl is AxisControl ax)
                    {
                        float v = ax.ReadValue();
                        if (Mathf.Abs(v) < 0.05f) continue;
                        local.Append($"   AXIS {GetDeviceRelativePath(ctrl, dev),-22} v={v,7:F3}\n");
                        hits++;
                    }
                }
                if (hits > 0)
                {
                    any = true;
                    sb.Append($"{dev.displayName}\n");
                    sb.Append(local);
                }
            }
            if (!any) sb.Append("(ningún input activo en este instante)\n");
        }
        catch (Exception e) { sb.Append($"(error leyendo inputs: {e.Message})\n"); }
        sb.Append('\n');

        // PlayerPrefs (mismas keys que LogConsolePanel)
        sb.Append("-- PLAYERPREFS --\n");
        try
        {
            int n = 0;
            foreach (var (key, kind) in KNOWN_PREFS)
            {
                if (!PlayerPrefs.HasKey(key)) continue;
                string val = kind switch
                {
                    "int"   => PlayerPrefs.GetInt(key).ToString(),
                    "float" => PlayerPrefs.GetFloat(key).ToString("F3"),
                    _       => PlayerPrefs.GetString(key),
                };
                if (string.IsNullOrEmpty(val)) val = "(empty)";
                sb.Append($"   {key} = {val}\n");
                n++;
            }
            if (n == 0) sb.Append("   (ninguna calibración guardada)\n");
        }
        catch (Exception e) { sb.Append($"(error leyendo prefs: {e.Message})\n"); }

        sb.Append("=== END SNAPSHOT ===\n");
        return sb.ToString();
    }

    // Mismas keys que LogConsolePanel — duplicadas a propósito para no
    // crear acoplamiento entre ambos archivos.
    static readonly (string key, string kind)[] KNOWN_PREFS = {
        ("TransmisionManual", "int"),
        ("Cargolluvia",       "int"),
        ("NoPeatones",        "int"),
        ("NoCarros",          "int"),
        ("G923_GasAxis",      "string"),
        ("G923_BrakeAxis",    "string"),
        ("G923_GasRest",      "float"),
        ("G923_GasPress",     "float"),
        ("G923_BrakeRest",    "float"),
        ("G923_BrakePress",   "float"),
        ("G923_SteerCenter",  "float"),
        ("G923_SteerMax",     "float"),
        ("G923_SteerMin",     "float"),
        ("Adv_SteerCurveA",        "float"),
        ("Adv_SteerDeadzone",      "float"),
        ("Adv_BrakeSoftEnd",       "float"),
        ("Adv_BrakeSoftMaxOutput", "float"),
        ("Adv_GasCurveN",          "float"),
        ("Bind_SteerAxis",   "string"),
        ("Bind_Reverse",     "string"),
        ("Bind_Drive",       "string"),
        ("Bind_PaddleLeft",  "string"),
        ("Bind_PaddleRight", "string"),
        ("Bind_Restart",     "string"),
        ("Bind_MenuA",       "string"),
        ("Bind_MenuB",       "string"),
        ("Bind_RestartA",    "string"),
        ("Bind_RestartB",    "string"),
    };

    static string GetDeviceRelativePath(InputControl ctrl, InputDevice dev)
    {
        string p = ctrl.path ?? "";
        string dp = dev.path ?? "";
        if (!string.IsNullOrEmpty(dp) && p.StartsWith(dp + "/"))
            return p.Substring(dp.Length + 1);
        return ctrl.name ?? p;
    }
}
