using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using TMPro;
using Gley.UrbanSystem;

/// <summary>
/// Panel de remapeo de controles (F8 hold 1.5s). Permite asignar qué botón
/// del volante dispara cada acción (reversa, drive, paddles, restart, combos)
/// sin recompilar. Muestra también inputs en vivo para diagnóstico.
/// </summary>
public class BindingsPanel : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoCreate()
    {
        GameObject go = new GameObject("[BindingsPanel]");
        go.AddComponent<BindingsPanel>();
        DontDestroyOnLoad(go);
        Debug.Log("[BindingsPanel] AutoCreate OK · mantén F8 1.5s para abrir");
    }

    const float HOLD_TIME = 1.5f;

    enum ActionKind { Button, Axis }

    class ActionEntry
    {
        public string id;            // nombre corto p/debug
        public string prefKey;       // PlayerPrefs key
        public string defaultValue;  // default binding
        public string displayName;   // visible en UI
        public ActionKind kind = ActionKind.Button;
        public TextMeshProUGUI valueLabel;
        public Image rowBg;
    }

    readonly List<ActionEntry> actions = new List<ActionEntry>();
    ActionEntry listening = null;
    float holdTimer = 0f;
    GameObject panelRoot;
    float prevTimeScale = 1f;
    TextMeshProUGUI liveInputsLabel;

    // Snapshot de ejes al iniciar escucha de axis → detectar delta > threshold
    Dictionary<string, float> axisBaseline;
    const float AXIS_DETECT_DELTA = 0.35f;

    // Snapshot de botones al iniciar escucha de button → detectar transición
    // not-pressed → pressed (sin esto, una palanca H ya pegada en una posición
    // nunca dispara wasPressedThisFrame y la asignación falla).
    Dictionary<string, bool> buttonBaseline;

    // Paths fantasma del G923 PS: SIEMPRE reportan estado fijo, no son input
    // del usuario. Si F8 o Pantalla 2 los captura, el binding queda inservible
    // (verificado en F7 en kiosk: button19=PRESSED, stick/y=-1, stick/down=PRESSED
    // de manera constante sin tocar nada). Los del stick/up..right son los
    // componentes derivados del eje stick — no son botones independientes.
    static readonly System.Collections.Generic.HashSet<string> PHANTOM_PATHS =
        new System.Collections.Generic.HashSet<string>
        {
            "button19",
            "stick/up", "stick/down", "stick/left", "stick/right",
            "stick/y",
        };

    static bool IsPhantomPath(string path) => path != null && PHANTOM_PATHS.Contains(path);

    void Awake()
    {
        // Eje del volante (primero, es el más crítico)
        actions.Add(new ActionEntry { id = "steerAxis",   prefKey = UIInputNew.PREF_BIND_STEER_AXIS,    defaultValue = UIInputNew.DEFAULT_BIND_STEER_AXIS,    displayName = "Eje del volante", kind = ActionKind.Axis });
        // Catálogo de acciones configurables
        actions.Add(new ActionEntry { id = "reverse",     prefKey = UIInputNew.PREF_BIND_REVERSE,       defaultValue = UIInputNew.DEFAULT_BIND_REVERSE,       displayName = "Reversa" });
        actions.Add(new ActionEntry { id = "drive",       prefKey = UIInputNew.PREF_BIND_DRIVE,         defaultValue = UIInputNew.DEFAULT_BIND_DRIVE,         displayName = "Drive" });
        actions.Add(new ActionEntry { id = "paddleLeft",  prefKey = UIInputNew.PREF_BIND_PADDLE_LEFT,   defaultValue = UIInputNew.DEFAULT_BIND_PADDLE_LEFT,   displayName = "Direccional Izq (paddle)" });
        actions.Add(new ActionEntry { id = "paddleRight", prefKey = UIInputNew.PREF_BIND_PADDLE_RIGHT,  defaultValue = UIInputNew.DEFAULT_BIND_PADDLE_RIGHT,  displayName = "Direccional Der (paddle)" });
        actions.Add(new ActionEntry { id = "restart",     prefKey = UIInputNew.PREF_BIND_RESTART,       defaultValue = UIInputNew.DEFAULT_BIND_RESTART,       displayName = "Reiniciar escena (botón)" });
        actions.Add(new ActionEntry { id = "menuA",       prefKey = UIInputNew.PREF_BIND_MENU_A,        defaultValue = UIInputNew.DEFAULT_BIND_MENU_A,        displayName = "Combo menú A (hold + B)" });
        actions.Add(new ActionEntry { id = "menuB",       prefKey = UIInputNew.PREF_BIND_MENU_B,        defaultValue = UIInputNew.DEFAULT_BIND_MENU_B,        displayName = "Combo menú B" });
        actions.Add(new ActionEntry { id = "restartA",    prefKey = UIInputNew.PREF_BIND_RESTART_A,     defaultValue = UIInputNew.DEFAULT_BIND_RESTART_A,     displayName = "Combo restart A (hold + B)" });
        actions.Add(new ActionEntry { id = "restartB",    prefKey = UIInputNew.PREF_BIND_RESTART_B,     defaultValue = UIInputNew.DEFAULT_BIND_RESTART_B,     displayName = "Combo restart B" });
    }

    void Update()
    {
        var kb = Keyboard.current;
        bool panelOpen = panelRoot != null;

        // Hold F8 para abrir
        if (!panelOpen)
        {
            if (kb != null && kb.f8Key.isPressed)
            {
                float prev = holdTimer;
                holdTimer += Time.unscaledDeltaTime;
                // Log un único tick por cada medio segundo cruzado — sirve para
                // confirmar en LogConsolePanel (F7) que el sistema sí está leyendo F8.
                if (Mathf.FloorToInt(holdTimer * 2) != Mathf.FloorToInt(prev * 2))
                    Debug.Log($"[BindingsPanel] F8 held {holdTimer:F1}s / {HOLD_TIME}s");
                if (holdTimer >= HOLD_TIME) { Debug.Log("[BindingsPanel] Abriendo panel"); Open(); holdTimer = 0f; }
            }
            else holdTimer = 0f;
            return;
        }

        // Abierto: Esc / F8 pulsado cierra
        if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.f8Key.wasPressedThisFrame))
        {
            Close();
            return;
        }

        // Refrescar inputs en vivo
        RefreshLiveInputs();

        // Modo escucha: detectar input según el tipo de acción
        if (listening != null)
        {
            InputDevice dev = FindWheelDevice();
            if (dev == null) return;

            if (listening.kind == ActionKind.Button)
            {
                // Detectar transición not-pressed → pressed contra el snapshot
                // tomado al entrar listening. wasPressedThisFrame solo no basta
                // porque un H-shifter ya puesto en posición no dispara flanco.
                foreach (var ctrl in dev.allControls)
                {
                    if (!(ctrl is ButtonControl btn)) continue;
                    string path = GetDeviceRelativePath(ctrl, dev);
                    if (IsPhantomPath(path)) continue; // saltar siempre-pressed del G923
                    bool nowPressed = btn.isPressed;
                    if (buttonBaseline == null) break;
                    if (!buttonBaseline.TryGetValue(path, out bool wasPressed))
                    {
                        // control nuevo (device hot-plug u otro) → registrar baseline
                        buttonBaseline[path] = nowPressed;
                        continue;
                    }
                    if (nowPressed && !wasPressed)
                    {
                        AssignBinding(listening, path);
                        StopListening();
                        break;
                    }
                    // refrescar baseline para captar futuros flancos sin atarse al snapshot inicial
                    buttonBaseline[path] = nowPressed;
                }
            }
            else // Axis
            {
                foreach (var ctrl in dev.allControls)
                {
                    if (!(ctrl is AxisControl) || ctrl is ButtonControl) continue;
                    string path = GetDeviceRelativePath(ctrl, dev);
                    if (IsPhantomPath(path)) continue; // skip stick/y phantom
                    float cur = ReadAxis(ctrl);
                    if (!axisBaseline.ContainsKey(path)) axisBaseline[path] = cur;
                    float baseline = axisBaseline[path];
                    if (Mathf.Abs(cur - baseline) >= AXIS_DETECT_DELTA)
                    {
                        AssignBinding(listening, path);
                        StopListening();
                        break;
                    }
                }
            }
        }
    }

    static float ReadAxis(InputControl ctrl)
    {
        if (ctrl is AxisControl a) return a.ReadValue();
        return 0f;
    }

    void Open()
    {
        prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        BuildUI();
    }

    void Close()
    {
        if (panelRoot != null) Destroy(panelRoot);
        panelRoot = null;
        listening = null;
        Time.timeScale = prevTimeScale;
    }

    void BuildUI()
    {
        panelRoot = new GameObject("BindingsPanelCanvas");
        DontDestroyOnLoad(panelRoot);
        Canvas canvas = panelRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.targetDisplay = 0;
        canvas.sortingOrder = 32000;
        CanvasScaler scaler = panelRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        panelRoot.AddComponent<GraphicRaycaster>();

        // Backdrop
        GameObject bd = new GameObject("Backdrop");
        bd.transform.SetParent(panelRoot.transform, false);
        RectTransform bdRt = bd.AddComponent<RectTransform>();
        bdRt.anchorMin = Vector2.zero; bdRt.anchorMax = Vector2.one;
        bdRt.offsetMin = Vector2.zero; bdRt.offsetMax = Vector2.zero;
        Image bdImg = bd.AddComponent<Image>();
        bdImg.color = new Color(0, 0, 0, 0.75f);
        bdImg.raycastTarget = true;

        // Card
        GameObject card = new GameObject("Card");
        card.transform.SetParent(panelRoot.transform, false);
        RectTransform cardRt = card.AddComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.anchoredPosition = Vector2.zero;
        cardRt.sizeDelta = new Vector2(960, 900);
        Image cardImg = card.AddComponent<Image>();
        cardImg.color = new Color(0.1f, 0.12f, 0.16f, 0.98f);

        float y = 410f;
        CreateText(card.transform, "Mapeo de controles del volante", 26f, FontStyles.Bold, Color.white, y);
        y -= 42f;
        CreateText(card.transform, "Click en [Detectar] y presiona el botón que quieras asignar · Esc o F8 cierra", 12f, FontStyles.Italic, new Color(0.7f, 0.7f, 0.7f), y);
        y -= 40f;

        // Tabla de bindings
        foreach (var a in actions)
        {
            BuildRow(card.transform, a, y);
            y -= 46f;
        }

        y -= 10f;

        // Sección de inputs en vivo
        CreateText(card.transform, "BOTONES EN VIVO (leyendas verdes)", 14f, FontStyles.Bold, new Color(0.4f, 1f, 0.5f), y);
        y -= 28f;
        GameObject liveObj = new GameObject("LiveInputs");
        liveObj.transform.SetParent(card.transform, false);
        RectTransform liveRt = liveObj.AddComponent<RectTransform>();
        liveRt.anchorMin = new Vector2(0.05f, 0.5f);
        liveRt.anchorMax = new Vector2(0.95f, 0.5f);
        liveRt.pivot = new Vector2(0.5f, 1);
        liveRt.anchoredPosition = new Vector2(0, y);
        liveRt.sizeDelta = new Vector2(0, 110);
        liveInputsLabel = liveObj.AddComponent<TextMeshProUGUI>();
        liveInputsLabel.fontSize = 15;
        liveInputsLabel.color = new Color(0.4f, 1f, 0.5f);
        liveInputsLabel.alignment = TextAlignmentOptions.TopLeft;
        liveInputsLabel.textWrappingMode = TextWrappingModes.Normal;
        liveInputsLabel.raycastTarget = false;
        liveInputsLabel.richText = true;
        liveInputsLabel.text = "(mueve el volante o presiona botones)";
        y -= 120f;

        // Botones
        CreateButton(card.transform, "Restaurar defaults", -220f, y, new Color(0.5f, 0.35f, 0.1f), OnRestoreDefaults);
        CreateButton(card.transform, "Limpiar binding activo", 0f, y, new Color(0.5f, 0.15f, 0.15f), OnClearActive);
        CreateButton(card.transform, "Cerrar", 220f, y, new Color(0.12f, 0.4f, 0.6f), Close);
    }

    void BuildRow(Transform parent, ActionEntry a, float yOffset)
    {
        GameObject row = new GameObject("Row_" + a.id);
        row.transform.SetParent(parent, false);
        RectTransform rowRt = row.AddComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0, 0.5f);
        rowRt.anchorMax = new Vector2(1, 0.5f);
        rowRt.pivot = new Vector2(0.5f, 0.5f);
        rowRt.anchoredPosition = new Vector2(0, yOffset);
        rowRt.sizeDelta = new Vector2(-40, 40);

        // Fondo de fila (cambia color en modo escucha)
        a.rowBg = row.AddComponent<Image>();
        a.rowBg.color = new Color(0.15f, 0.17f, 0.21f, 0.5f);

        // Nombre (izq)
        GameObject lbl = new GameObject("Name");
        lbl.transform.SetParent(row.transform, false);
        RectTransform lblRt = lbl.AddComponent<RectTransform>();
        lblRt.anchorMin = new Vector2(0, 0); lblRt.anchorMax = new Vector2(0.45f, 1);
        lblRt.offsetMin = new Vector2(10, 0); lblRt.offsetMax = Vector2.zero;
        TextMeshProUGUI lblT = lbl.AddComponent<TextMeshProUGUI>();
        lblT.text = a.displayName;
        lblT.fontSize = 16;
        lblT.color = Color.white;
        lblT.alignment = TextAlignmentOptions.MidlineLeft;
        lblT.raycastTarget = false;

        // Valor actual (centro)
        GameObject val = new GameObject("Value");
        val.transform.SetParent(row.transform, false);
        RectTransform valRt = val.AddComponent<RectTransform>();
        valRt.anchorMin = new Vector2(0.45f, 0); valRt.anchorMax = new Vector2(0.78f, 1);
        valRt.offsetMin = Vector2.zero; valRt.offsetMax = Vector2.zero;
        TextMeshProUGUI valT = val.AddComponent<TextMeshProUGUI>();
        valT.text = FormatBindingValue(PlayerPrefs.GetString(a.prefKey, a.defaultValue));
        valT.fontSize = 16;
        valT.fontStyle = FontStyles.Bold;
        valT.color = new Color(0.12f, 0.94f, 0.96f);
        valT.alignment = TextAlignmentOptions.Center;
        valT.raycastTarget = false;
        a.valueLabel = valT;

        // Botón "Detectar" (derecha)
        GameObject btnObj = new GameObject("Detect");
        btnObj.transform.SetParent(row.transform, false);
        RectTransform btnRt = btnObj.AddComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0.8f, 0.15f); btnRt.anchorMax = new Vector2(0.99f, 0.85f);
        btnRt.offsetMin = Vector2.zero; btnRt.offsetMax = Vector2.zero;
        Image btnImg = btnObj.AddComponent<Image>();
        btnImg.color = new Color(0.2f, 0.4f, 0.6f);
        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(() => StartListening(a));

        GameObject btnT = new GameObject("Text");
        btnT.transform.SetParent(btnObj.transform, false);
        RectTransform btnTRt = btnT.AddComponent<RectTransform>();
        btnTRt.anchorMin = Vector2.zero; btnTRt.anchorMax = Vector2.one;
        btnTRt.offsetMin = Vector2.zero; btnTRt.offsetMax = Vector2.zero;
        TextMeshProUGUI btnTT = btnT.AddComponent<TextMeshProUGUI>();
        btnTT.text = "Detectar";
        btnTT.fontSize = 14;
        btnTT.color = Color.white;
        btnTT.alignment = TextAlignmentOptions.Center;
        btnTT.raycastTarget = false;
    }

    string FormatBindingValue(string v) => string.IsNullOrEmpty(v) ? "(no asignado)" : v;

    void StartListening(ActionEntry a)
    {
        StopListening(); // apagar el anterior si hay
        listening = a;
        string hint = a.kind == ActionKind.Axis ? "← mueve el volante/eje..." : "← presiona un botón...";
        if (a.valueLabel != null) a.valueLabel.text = hint;
        if (a.rowBg != null) a.rowBg.color = new Color(0.5f, 0.35f, 0.0f, 0.9f);

        // Si es axis, snapshot baseline de todos los axes para detectar delta
        if (a.kind == ActionKind.Axis)
        {
            axisBaseline = new Dictionary<string, float>();
            InputDevice dev = FindWheelDevice();
            if (dev != null)
            {
                foreach (var ctrl in dev.allControls)
                {
                    if (!(ctrl is AxisControl) || ctrl is ButtonControl) continue;
                    string path = GetDeviceRelativePath(ctrl, dev);
                    if (IsPhantomPath(path)) continue;
                    axisBaseline[path] = ReadAxis(ctrl);
                }
            }
        }
        else // Button
        {
            // Snapshot de estado actual de cada botón. Botones ya pegados al
            // entrar listening NO se autoasignan — el usuario debe transicionar
            // (sacar y volver a meter la palanca) para registrar la asignación.
            buttonBaseline = new Dictionary<string, bool>();
            InputDevice dev = FindWheelDevice();
            if (dev != null)
            {
                foreach (var ctrl in dev.allControls)
                {
                    if (!(ctrl is ButtonControl btn)) continue;
                    string path = GetDeviceRelativePath(ctrl, dev);
                    if (IsPhantomPath(path)) continue;
                    buttonBaseline[path] = btn.isPressed;
                }
            }
        }
    }

    void StopListening()
    {
        if (listening == null) return;
        // refrescar visual de la fila anterior
        if (listening.valueLabel != null)
            listening.valueLabel.text = FormatBindingValue(PlayerPrefs.GetString(listening.prefKey, listening.defaultValue));
        if (listening.rowBg != null)
            listening.rowBg.color = new Color(0.15f, 0.17f, 0.21f, 0.5f);
        listening = null;
    }

    void AssignBinding(ActionEntry a, string path)
    {
        PlayerPrefs.SetString(a.prefKey, path);
        PlayerPrefs.Save();
        Debug.Log($"[BindingsPanel] {a.id} ← '{path}'");
        NotifyInputReload();
    }

    void OnClearActive()
    {
        if (listening == null) return;
        PlayerPrefs.DeleteKey(listening.prefKey);
        PlayerPrefs.Save();
        Debug.Log($"[BindingsPanel] {listening.id} limpiado (volvió al default)");
        NotifyInputReload();
        StopListening();
    }

    void OnRestoreDefaults()
    {
        foreach (var a in actions)
        {
            PlayerPrefs.DeleteKey(a.prefKey);
            if (a.valueLabel != null) a.valueLabel.text = FormatBindingValue(a.defaultValue);
        }
        PlayerPrefs.Save();
        Debug.Log("[BindingsPanel] Bindings restaurados a defaults");
        NotifyInputReload();
        StopListening();
    }

    void NotifyInputReload()
    {
#pragma warning disable CS0618
        var input = Object.FindObjectOfType<UIInputNew>();
#pragma warning restore CS0618
        if (input != null) input.ReloadBindings();
    }

    // ── Live inputs ─────────────────────────────────────────────────

    void RefreshLiveInputs()
    {
        if (liveInputsLabel == null) return;
        InputDevice dev = FindWheelDevice();
        if (dev == null)
        {
            liveInputsLabel.text = "(sin volante detectado)";
            return;
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder(512);
        sb.Append("<b>Device:</b> ");
        sb.Append(dev.displayName ?? "?");
        sb.Append("\n<b>Botones:</b> ");
        bool anyBtn = false;
        foreach (var ctrl in dev.allControls)
        {
            if (!(ctrl is ButtonControl btn)) continue;
            if (btn.isPressed)
            {
                if (anyBtn) sb.Append(" · ");
                sb.Append(GetDeviceRelativePath(ctrl, dev));
                anyBtn = true;
            }
        }
        if (!anyBtn) sb.Append("(ninguno)");

        sb.Append("\n<b>Ejes activos (|v|>0.1):</b> ");
        bool anyAxis = false;
        foreach (var ctrl in dev.allControls)
        {
            if (!(ctrl is AxisControl) || ctrl is ButtonControl) continue;
            float v = ReadAxis(ctrl);
            if (Mathf.Abs(v) > 0.1f)
            {
                if (anyAxis) sb.Append(" · ");
                sb.Append(GetDeviceRelativePath(ctrl, dev));
                sb.Append("=");
                sb.Append(v.ToString("F2"));
                anyAxis = true;
            }
        }
        if (!anyAxis) sb.Append("(ninguno)");

        liveInputsLabel.text = sb.ToString();
    }

    // ── Helpers ─────────────────────────────────────────────────────

    InputDevice FindWheelDevice()
    {
#pragma warning disable CS0618
        var input = Object.FindObjectOfType<UIInputNew>();
#pragma warning restore CS0618
        if (input != null && input.WheelDevice != null) return input.WheelDevice;
        // Fallback: buscar por nombre
        foreach (var d in InputSystem.devices)
        {
            string n = d.displayName ?? "";
            if (n.IndexOf("G923", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("G920", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Racing Wheel", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Steering Wheel", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Driving Wheel", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return d;
        }
        return null;
    }

    static string GetDeviceRelativePath(InputControl ctrl, InputDevice dev)
    {
        string p = ctrl.path ?? "";
        string dp = dev.path ?? "";
        if (!string.IsNullOrEmpty(dp) && p.StartsWith(dp + "/"))
            return p.Substring(dp.Length + 1);
        return ctrl.name ?? p;
    }

    // ── UI helpers ──────────────────────────────────────────────────

    TextMeshProUGUI CreateText(Transform parent, string text, float size, FontStyles style, Color color, float yOffset)
    {
        GameObject obj = new GameObject("Text");
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0.5f); rt.anchorMax = new Vector2(1, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, yOffset);
        rt.sizeDelta = new Vector2(-40, 28);
        TextMeshProUGUI t = obj.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        return t;
    }

    void CreateButton(Transform parent, string text, float xOffset, float yOffset, Color bgColor, System.Action onClick)
    {
        GameObject btnObj = new GameObject("Btn_" + text);
        btnObj.transform.SetParent(parent, false);
        RectTransform btnRt = btnObj.AddComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0.5f, 0.5f); btnRt.anchorMax = new Vector2(0.5f, 0.5f);
        btnRt.pivot = new Vector2(0.5f, 0.5f);
        btnRt.anchoredPosition = new Vector2(xOffset, yOffset);
        btnRt.sizeDelta = new Vector2(230, 50);
        Image img = btnObj.AddComponent<Image>();
        img.color = bgColor;
        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(() => onClick?.Invoke());

        GameObject tObj = new GameObject("Text");
        tObj.transform.SetParent(btnObj.transform, false);
        RectTransform tRt = tObj.AddComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
        tRt.offsetMin = Vector2.zero; tRt.offsetMax = Vector2.zero;
        TextMeshProUGUI t = tObj.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = 16;
        t.color = Color.white;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
    }
}
