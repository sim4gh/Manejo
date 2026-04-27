using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using TMPro;

/// <summary>
/// Consola de diagnóstico — abrir manteniendo F7 por 1.5s.
/// Lee TODOS los devices conectados y muestra en vivo qué botón se presiona,
/// qué eje se mueve y con qué valor. Útil para descubrir el nombre técnico
/// de la reversa, los paddles, los pedales, etc., sin recompilar.
/// Esc o F7 (pulsado, no held) cierra.
/// </summary>
public class LogConsolePanel : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoCreate()
    {
        GameObject go = new GameObject("[LogConsolePanel]");
        go.AddComponent<LogConsolePanel>();
        DontDestroyOnLoad(go);
    }

    const float HOLD_TIME = 1.5f;
    const float AXIS_DELTA_THRESHOLD = 0.05f; // |raw - baseline| para considerar el eje "movido"
    const int   MAX_LOG_LINES = 120;

    // Paleta tipo terminal — fondo casi negro, texto claro, acentos sutiles.
    static readonly Color COL_BG_CARD      = new Color(0.04f, 0.04f, 0.05f, 0.98f);
    static readonly Color COL_BG_PANEL     = new Color(0.07f, 0.08f, 0.09f, 1f);
    static readonly Color COL_BG_HEADER    = new Color(0.10f, 0.11f, 0.12f, 1f);
    static readonly Color COL_TEXT         = new Color(0.83f, 0.83f, 0.85f, 1f);
    static readonly Color COL_TEXT_DIM     = new Color(0.55f, 0.57f, 0.60f, 1f);
    // Acentos via rich text TMP
    const string HEX_HEADER  = "#4ec9b0"; // cyan VS Code
    const string HEX_KEY     = "#9cdcfe"; // azul claro
    const string HEX_VAL     = "#ce9178"; // ámbar
    const string HEX_BTN     = "#6a9955"; // verde mate
    const string HEX_AXIS    = "#dcdcaa"; // amarillo pálido
    const string HEX_DEVICE  = "#4ec9b0";
    const string HEX_DIM     = "#6a737d";
    const string HEX_ERROR   = "#f48771";
    const string HEX_WARN    = "#dcdcaa";
    const string HEX_INFO    = "#569cd6";

    class AxisInfo
    {
        public float baseline;
        public float current;
        public float minSeen;
        public float maxSeen;
        public bool  everMoved;
    }

    float holdTimer = 0f;
    GameObject panelRoot;
    float prevTimeScale = 1f;

    TextMeshProUGUI deviceListLabel;
    TextMeshProUGUI activeInputsLabel;
    TextMeshProUGUI prefsLabel;
    TextMeshProUGUI logFeedLabel;

    readonly Queue<string> logBuffer = new Queue<string>();
    readonly Dictionary<string, AxisInfo> axisInfos = new Dictionary<string, AxisInfo>();

    void OnEnable()  { Application.logMessageReceived += OnLog; }
    void OnDisable() { Application.logMessageReceived -= OnLog; }

    void OnLog(string condition, string stackTrace, LogType type)
    {
        string hex = type switch
        {
            LogType.Error or LogType.Exception or LogType.Assert => HEX_ERROR,
            LogType.Warning => HEX_WARN,
            _ => HEX_INFO,
        };
        string tag = type switch
        {
            LogType.Error => "ERR",
            LogType.Exception => "EXC",
            LogType.Assert => "AST",
            LogType.Warning => "WRN",
            _ => "LOG",
        };
        string line = $"<color={hex}>[{tag}]</color> <color={HEX_DIM}>{Time.realtimeSinceStartup:F1}s</color> {condition}";
        if (logBuffer.Count >= MAX_LOG_LINES) logBuffer.Dequeue();
        logBuffer.Enqueue(line);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        bool open = panelRoot != null;

        if (!open)
        {
            if (kb.f7Key.isPressed)
            {
                holdTimer += Time.unscaledDeltaTime;
                if (holdTimer >= HOLD_TIME) { Open(); holdTimer = 0f; }
            }
            else holdTimer = 0f;
            return;
        }

        if (kb.escapeKey.wasPressedThisFrame || kb.f7Key.wasPressedThisFrame)
        {
            Close();
            return;
        }
        Refresh();
    }

    void Open()
    {
        prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        BuildUI();
        SnapshotBaselines();
        Refresh();
    }

    void Close()
    {
        if (panelRoot != null) Destroy(panelRoot);
        panelRoot = null;
        Time.timeScale = prevTimeScale;
    }

    // ── Tracking de ejes ────────────────────────────────────────────────

    void SnapshotBaselines()
    {
        axisInfos.Clear();
        foreach (var dev in InputSystem.devices)
        foreach (var ctrl in dev.allControls)
        {
            if (ctrl is ButtonControl) continue;
            if (!(ctrl is AxisControl ax)) continue;
            string key = $"{dev.deviceId}::{ctrl.path}";
            float v = ax.ReadValue();
            axisInfos[key] = new AxisInfo
            {
                baseline = v, current = v, minSeen = v, maxSeen = v
            };
        }
    }

    void TrackAxes()
    {
        foreach (var dev in InputSystem.devices)
        foreach (var ctrl in dev.allControls)
        {
            if (ctrl is ButtonControl) continue;
            if (!(ctrl is AxisControl ax)) continue;
            string key = $"{dev.deviceId}::{ctrl.path}";
            if (!axisInfos.TryGetValue(key, out var info))
            {
                float v0 = ax.ReadValue();
                info = new AxisInfo { baseline = v0, current = v0, minSeen = v0, maxSeen = v0 };
                axisInfos[key] = info;
            }
            float v = ax.ReadValue();
            info.current = v;
            if (v > info.maxSeen) info.maxSeen = v;
            if (v < info.minSeen) info.minSeen = v;
            if (Mathf.Abs(v - info.baseline) > AXIS_DELTA_THRESHOLD) info.everMoved = true;
        }
    }

    // ── Refresh ─────────────────────────────────────────────────────────

    void Refresh()
    {
        TrackAxes();
        if (deviceListLabel != null)   deviceListLabel.text   = BuildDeviceList();
        if (activeInputsLabel != null) activeInputsLabel.text = BuildActiveInputs();
        if (prefsLabel != null)        prefsLabel.text        = BuildPrefsList();
        if (logFeedLabel != null)      logFeedLabel.text      = BuildLogText();
    }

    string BuildDeviceList()
    {
        var sb = new StringBuilder(1024);
        sb.Append($"<color={HEX_HEADER}>// DEVICES CONECTADOS</color>\n");
        int n = 0;
        foreach (var d in InputSystem.devices)
        {
            n++;
            string product = string.IsNullOrEmpty(d.description.product) ? "?" : d.description.product;
            sb.Append($"<color={HEX_DEVICE}>{d.displayName}</color>  <color={HEX_DIM}>[{d.layout}]</color>\n");
            sb.Append($"  <color={HEX_DIM}>path</color>    = <color={HEX_VAL}>{d.path}</color>\n");
            sb.Append($"  <color={HEX_DIM}>product</color> = <color={HEX_VAL}>{product}</color>\n");
        }
        if (n == 0) sb.Append($"<color={HEX_DIM}>(ningún device detectado)</color>\n");
        return sb.ToString();
    }

    string BuildActiveInputs()
    {
        var sb = new StringBuilder(2048);
        sb.Append($"<color={HEX_HEADER}>// INPUTS EN VIVO</color>  <color={HEX_DIM}>(botones presionados · ejes que se han movido)</color>\n");

        bool anyDevice = false;
        foreach (var dev in InputSystem.devices)
        {
            var local = new StringBuilder(256);
            int hits = 0;

            foreach (var ctrl in dev.allControls)
            {
                if (!(ctrl is ButtonControl btn)) continue;
                if (!btn.isPressed) continue;
                local.Append($"   <color={HEX_BTN}>BTN </color> ");
                local.Append(GetDeviceRelativePath(ctrl, dev));
                local.Append('\n');
                hits++;
            }

            foreach (var ctrl in dev.allControls)
            {
                if (ctrl is ButtonControl) continue;
                if (!(ctrl is AxisControl ax)) continue;
                string key = $"{dev.deviceId}::{ctrl.path}";
                if (!axisInfos.TryGetValue(key, out var info)) continue;
                if (!info.everMoved) continue;
                string rel = GetDeviceRelativePath(ctrl, dev);
                local.Append($"   <color={HEX_AXIS}>AXIS</color> ");
                local.Append(rel.PadRight(22));
                local.Append($" v=<color={HEX_VAL}>{info.current,7:F3}</color>");
                local.Append($" base=<color={HEX_DIM}>{info.baseline,6:F2}</color>");
                local.Append($" rango=<color={HEX_DIM}>[{info.minSeen,5:F2}, {info.maxSeen,5:F2}]</color>\n");
                hits++;
            }

            if (hits > 0)
            {
                anyDevice = true;
                sb.Append($"<color={HEX_DEVICE}>{dev.displayName}</color>\n");
                sb.Append(local);
            }
        }

        if (!anyDevice)
            sb.Append($"\n   <color={HEX_DIM}>Mueve cualquier eje o presiona cualquier botón</color>\n");
        return sb.ToString();
    }

    // PlayerPrefs que usan los distintos sistemas del simulador. Solo se imprime
    // si la key existe — así uno ve de un vistazo qué quedó calibrado o no.
    static readonly (string key, string kind)[] KNOWN_PREFS = {
        // Setup general
        ("TransmisionManual", "int"),
        ("Cargolluvia",       "int"),
        ("NoPeatones",        "int"),
        ("NoCarros",          "int"),
        // Calibración G923 (capturada en pantalla 2 del menú)
        ("G923_GasAxis",      "string"),
        ("G923_BrakeAxis",    "string"),
        ("G923_GasRest",      "float"),
        ("G923_GasPress",     "float"),
        ("G923_BrakeRest",    "float"),
        ("G923_BrakePress",   "float"),
        ("G923_SteerCenter",  "float"),
        ("G923_SteerMax",     "float"),
        ("G923_SteerMin",     "float"),
        // Tuning (AdvancedInputPanel — F9)
        ("Adv_SteerCurveA",        "float"),
        ("Adv_SteerDeadzone",      "float"),
        ("Adv_BrakeSoftEnd",       "float"),
        ("Adv_BrakeSoftMaxOutput", "float"),
        ("Adv_GasCurveN",          "float"),
        // Bindings remapeables (BindingsPanel — F8)
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

    string BuildPrefsList()
    {
        var sb = new StringBuilder(1024);
        sb.Append($"<color={HEX_HEADER}>// PLAYERPREFS</color>\n");
        int n = 0;
        foreach (var (key, kind) in KNOWN_PREFS)
        {
            if (!PlayerPrefs.HasKey(key)) continue;
            string val = kind switch
            {
                "int"    => PlayerPrefs.GetInt(key).ToString(),
                "float"  => PlayerPrefs.GetFloat(key).ToString("F3"),
                _        => PlayerPrefs.GetString(key),
            };
            if (string.IsNullOrEmpty(val)) val = "(empty)";
            sb.Append($"   <color={HEX_KEY}>{key}</color> = <color={HEX_VAL}>{val}</color>\n");
            n++;
        }
        if (n == 0) sb.Append($"   <color={HEX_DIM}>(ninguna calibración guardada)</color>\n");
        return sb.ToString();
    }

    string BuildLogText()
    {
        if (logBuffer.Count == 0)
            return $"<color={HEX_DIM}>// log vacío</color>";
        var sb = new StringBuilder(8192);
        foreach (var line in logBuffer) { sb.Append(line); sb.Append('\n'); }
        return sb.ToString();
    }

    static string GetDeviceRelativePath(InputControl ctrl, InputDevice dev)
    {
        string p = ctrl.path ?? "";
        string dp = dev.path ?? "";
        if (!string.IsNullOrEmpty(dp) && p.StartsWith(dp + "/"))
            return p.Substring(dp.Length + 1);
        return ctrl.name ?? p;
    }

    // ── UI ──────────────────────────────────────────────────────────────

    void BuildUI()
    {
        panelRoot = new GameObject("LogConsolePanelCanvas");
        DontDestroyOnLoad(panelRoot);

        Canvas canvas = panelRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.targetDisplay = 0;
        canvas.sortingOrder = 32100; // arriba de los otros paneles (32000)

        var scaler = panelRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        panelRoot.AddComponent<GraphicRaycaster>();

        // Backdrop opaco
        GameObject backdrop = NewChild(panelRoot.transform, "Backdrop");
        var bdRt = backdrop.AddComponent<RectTransform>();
        bdRt.anchorMin = Vector2.zero; bdRt.anchorMax = Vector2.one;
        bdRt.offsetMin = Vector2.zero; bdRt.offsetMax = Vector2.zero;
        var bdImg = backdrop.AddComponent<Image>();
        bdImg.color = new Color(0, 0, 0, 0.92f);
        bdImg.raycastTarget = true;

        // Card grande casi pantalla completa
        GameObject card = NewChild(panelRoot.transform, "Card");
        var cardRt = card.AddComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.025f, 0.04f);
        cardRt.anchorMax = new Vector2(0.975f, 0.96f);
        cardRt.offsetMin = Vector2.zero; cardRt.offsetMax = Vector2.zero;
        var cardImg = card.AddComponent<Image>();
        cardImg.color = COL_BG_CARD;

        // Header bar
        GameObject header = NewChild(card.transform, "Header");
        var hRt = header.AddComponent<RectTransform>();
        hRt.anchorMin = new Vector2(0, 1); hRt.anchorMax = new Vector2(1, 1);
        hRt.pivot = new Vector2(0.5f, 1);
        hRt.sizeDelta = new Vector2(0, 56);
        hRt.anchoredPosition = Vector2.zero;
        header.AddComponent<Image>().color = COL_BG_HEADER;

        var titleRt = NewChild(header.transform, "Title").AddComponent<RectTransform>();
        titleRt.anchorMin = Vector2.zero; titleRt.anchorMax = Vector2.one;
        titleRt.offsetMin = new Vector2(20, 0); titleRt.offsetMax = new Vector2(-20, 0);
        var titleT = titleRt.gameObject.AddComponent<TextMeshProUGUI>();
        titleT.text = $"<color={HEX_HEADER}>console</color>  <color={HEX_DIM}>// diagnóstico de inputs · F7 hold · Esc cierra</color>";
        titleT.fontSize = 22;
        titleT.fontStyle = FontStyles.Bold;
        titleT.color = COL_TEXT;
        titleT.alignment = TextAlignmentOptions.MidlineLeft;
        titleT.richText = true;
        titleT.raycastTarget = false;

        // Footer (botones)
        GameObject footer = NewChild(card.transform, "Footer");
        var fRt = footer.AddComponent<RectTransform>();
        fRt.anchorMin = new Vector2(0, 0); fRt.anchorMax = new Vector2(1, 0);
        fRt.pivot = new Vector2(0.5f, 0);
        fRt.sizeDelta = new Vector2(0, 64);
        fRt.anchoredPosition = new Vector2(0, 0);
        footer.AddComponent<Image>().color = COL_BG_HEADER;

        CreateButton(footer.transform, "Limpiar log",            -270, 0, () => logBuffer.Clear());
        CreateButton(footer.transform, "Reset baseline ejes",     -90, 0, SnapshotBaselines);
        CreateButton(footer.transform, "Copiar al portapapeles",  120, 0, CopyToClipboard);
        CreateButton(footer.transform, "Cerrar (Esc · F7)",       330, 0, Close);

        // Content area entre header y footer
        GameObject content = NewChild(card.transform, "Content");
        var coRt = content.AddComponent<RectTransform>();
        coRt.anchorMin = Vector2.zero; coRt.anchorMax = Vector2.one;
        coRt.offsetMin = new Vector2(15, 70); coRt.offsetMax = new Vector2(-15, -64);

        // Columna izquierda: devices arriba, prefs abajo
        deviceListLabel   = AddPanel(content.transform, "Devices",      0.00f, 0.40f, 0.55f, 1.00f);
        prefsLabel        = AddPanel(content.transform, "Prefs",        0.00f, 0.40f, 0.00f, 0.55f);

        // Columna central: inputs en vivo (alta para que quepan todos los ejes)
        activeInputsLabel = AddPanel(content.transform, "ActiveInputs", 0.40f, 0.72f, 0.00f, 1.00f);

        // Columna derecha: feed de log
        logFeedLabel      = AddPanel(content.transform, "LogFeed",      0.72f, 1.00f, 0.00f, 1.00f);
        logFeedLabel.alignment = TextAlignmentOptions.BottomLeft; // últimos al final, visibles
        logFeedLabel.fontSize = 12;
    }

    TextMeshProUGUI AddPanel(Transform parent, string name, float xMin, float xMax, float yMin, float yMax)
    {
        GameObject panel = NewChild(parent, name);
        var rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(xMin, yMin);
        rt.anchorMax = new Vector2(xMax, yMax);
        rt.offsetMin = new Vector2(4, 4); rt.offsetMax = new Vector2(-4, -4);
        var img = panel.AddComponent<Image>();
        img.color = COL_BG_PANEL;

        // Mask para recortar texto que se desborde
        var mask = panel.AddComponent<RectMask2D>();
        mask.padding = new Vector4(2, 2, 2, 2);

        GameObject textObj = NewChild(panel.transform, "Text");
        var tRt = textObj.AddComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
        tRt.offsetMin = new Vector2(12, 8); tRt.offsetMax = new Vector2(-12, -8);
        var tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = "";
        tmp.fontSize = 13;
        tmp.color = COL_TEXT;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.richText = true;
        tmp.raycastTarget = false;
        return tmp;
    }

    void CreateButton(Transform parent, string text, float xOffset, float yOffset, System.Action onClick)
    {
        GameObject go = NewChild(parent, "Btn_" + text);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(xOffset, yOffset);
        rt.sizeDelta = new Vector2(200, 40);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.14f, 0.16f, 0.18f, 1f);
        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(() => onClick?.Invoke());
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.20f, 0.22f, 0.26f);
        colors.pressedColor = new Color(0.10f, 0.12f, 0.14f);
        btn.colors = colors;

        GameObject tObj = NewChild(go.transform, "Text");
        var tRt = tObj.AddComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
        tRt.offsetMin = Vector2.zero; tRt.offsetMax = Vector2.zero;
        var t = tObj.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = 14;
        t.color = COL_TEXT;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
    }

    static GameObject NewChild(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    void CopyToClipboard()
    {
        var sb = new StringBuilder(8192);
        sb.Append("=== DEVICES ===\n").Append(deviceListLabel?.text ?? "").Append("\n\n");
        sb.Append("=== INPUTS EN VIVO ===\n").Append(activeInputsLabel?.text ?? "").Append("\n\n");
        sb.Append("=== PLAYERPREFS ===\n").Append(prefsLabel?.text ?? "").Append("\n\n");
        sb.Append("=== LOG ===\n").Append(logFeedLabel?.text ?? "").Append('\n');
        // Quitar tags TMP <color=...>...</color>, <b>, etc.
        string clean = Regex.Replace(sb.ToString(), "<.*?>", "");
        GUIUtility.systemCopyBuffer = clean;
        Debug.Log("[LogConsolePanel] Snapshot copiado al portapapeles");
    }
}
