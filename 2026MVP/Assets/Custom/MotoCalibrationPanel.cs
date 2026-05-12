using System.Collections.Generic;
using TlaxSim.MotoCalibration;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Panel de calibración Moto Simulator (F8 sostén 1.5s cuando Moto conectado y
// flag Moto_UseJsonMapping=1). Mirror simplificado de G923CalibrationPanel:
// sin tabs, sin variant detection (Moto es single-variant), sin FFB, sin marchas,
// sin paddles, sin reversa, sin horn/hazards (firmware-side).
//
// v1.9.0 — único writer del JSON moto_mapping.json. Ver:
// docs/superpowers/specs/2026-05-12-moto-v190-immutable-calibration-design.md
public class MotoCalibrationPanel : MonoBehaviour
{
    public static MotoCalibrationPanel Instance { get; private set; }
    public bool IsOpen { get; private set; }

    private Canvas _canvas;
    private GameObject _panelRoot;
    private MotoMapping _draft; // copia editable del Active
    private bool _hasChanges;
    private TMP_Text _headerCalibratedAt;

    private bool _capturing;
    private string _captureTargetField; // internal field name (e.g. "brake", "clutch", "lean", "handlebar", "gas")
    // Fix UX: rastrear última captura exitosa para mostrar "Guardado" 0.7s en esa fila.
    private string _lastSavedKey;
    private float _lastSavedAt = -999f;
    private const float SAVED_FEEDBACK_SECONDS = 0.7f;

    // Warning de rango muy estrecho tras CoCaptureAxisRange.
    private string _rangeWarning;
    private float _rangeWarningAt = -999f;
    private const float WARNING_VISIBLE_SECONDS = 5f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        if (FindFirstObjectByType<MotoCalibrationPanel>() != null) return;
        var go = new GameObject("[MotoCalibrationPanel]");
        var panel = go.AddComponent<MotoCalibrationPanel>();
        DontDestroyOnLoad(go);
        Instance = panel;
        Debug.Log("[MotoCalibrationPanel] AutoCreate OK · activa cuando Moto conectado + flag JSON=1");
    }

    private void Start()
    {
        // v1.9.0: Cargar JSON al boot del juego — corre una sola vez post-AutoCreate.
        // Si flag Moto_UseJsonMapping=0, LoadFromDisk deja Active=null y los consumers
        // caen al path PlayerPrefs MOTO_* legacy.
        MotoControlMapping.LoadFromDisk();
        Debug.Log("[MotoCalibrationPanel] Active mapping: " +
            (MotoControlMapping.Active != null
                ? "loaded fp=" + MotoControlMapping.Active.deviceFingerprint
                : "null"));
    }

    public static bool IsMotoConnected()
    {
        foreach (var dev in InputSystem.devices)
        {
            if (Gley.UrbanSystem.UIInputNew.IsMotoSimulator(dev)) return true;
        }
        return false;
    }

    public void Open()
    {
        if (IsOpen) return;
        // Snapshot del Active para edición (copia profunda via JSON round-trip)
        if (MotoControlMapping.Active != null)
        {
            string json = JsonUtility.ToJson(MotoControlMapping.Active);
            _draft = JsonUtility.FromJson<MotoMapping>(json);
        }
        else
        {
            _draft = new MotoMapping();
            // Detectar fingerprint del device conectado.
            var dev = FindMotoDevice();
            if (dev != null)
            {
                _draft.deviceFingerprint = dev.displayName;
            }
        }
        _hasChanges = false;
        BuildUI();
        IsOpen = true;
        Time.timeScale = 0f;
    }

    public void Close(bool save)
    {
        if (!IsOpen) return;
        if (save && _hasChanges)
        {
            _draft.calibratedBy = "F8-panel";
            _draft.calibratedAt = System.DateTime.UtcNow.ToString("o");
            MotoControlMapping.Save(_draft);

            // v1.9.0: refresh defensivo de UIInputNew. Misma estrategia que G923:
            // ReloadBindings vuelve a leer PlayerPrefs PREF_BIND_* — no refresca
            // binds Moto desde Active JSON. La solución confiable es volver al
            // MainMenu (recarga de escena re-attachea con Active fresco) o
            // reconectar USB.
            // TODO(v1.9.x): exponer UIInputNew.ReattachMotoDevice() y llamar aquí.
            var uiInput = FindFirstObjectByType<Gley.UrbanSystem.UIInputNew>();
            if (uiInput != null)
            {
                uiInput.ReloadBindings(); // best-effort
                uiInput.ReloadTuning();
                Debug.Log("[MotoCalibrationPanel] Save → ReloadBindings/ReloadTuning (refresh in-place del Active completo requiere reattach: volver a MainMenu).");
            }
        }
        if (_panelRoot != null) Destroy(_panelRoot);
        _panelRoot = null;
        IsOpen = false;
        Time.timeScale = 1f;
    }

    private void Update()
    {
        if (IsOpen)
        {
            // Esc cierra con auto-save (igual que G923 panel)
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close(save: _hasChanges);
                return;
            }

            // Auto-revertir "Guardado" → "Detectar" tras ventana SAVED_FEEDBACK_SECONDS.
            // Solo si NO hay captura activa (no queremos rebuildar mid-capture).
            if (!_capturing && _lastSavedKey != null
                && Time.unscaledTime - _lastSavedAt > SAVED_FEEDBACK_SECONDS)
            {
                _lastSavedKey = null;
                if (_panelRoot != null) Destroy(_panelRoot);
                BuildUI();
            }

            // Auto-limpiar warning de rango estrecho tras WARNING_VISIBLE_SECONDS.
            if (!_capturing && _rangeWarning != null
                && Time.unscaledTime - _rangeWarningAt > WARNING_VISIBLE_SECONDS)
            {
                _rangeWarning = null;
                if (_panelRoot != null) Destroy(_panelRoot);
                BuildUI();
            }
        }
    }

    private void BuildUI()
    {
        EnsureCanvas();
        _panelRoot = new GameObject("MotoCalPanel");
        _panelRoot.transform.SetParent(_canvas.transform, false);
        var bg = _panelRoot.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.95f);
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;

        // Card central — moto solo tiene ~5 controles, card más compacto que G923.
        var card = new GameObject("Card", typeof(Image));
        card.transform.SetParent(_panelRoot.transform, false);
        card.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.14f, 1f);
        var cardRt = card.GetComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(1100f, 820f);

        BuildHeader(card.transform);
        BuildControlRows(card.transform);
        BuildFooter(card.transform);
    }

    private void EnsureCanvas()
    {
        if (_canvas != null) return;
        var go = new GameObject("MotoCalCanvas");
        go.transform.SetParent(transform, false);
        _canvas = go.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 9999;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // EventSystem dedup. Misma estrategia que G923CalibrationPanel.
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            var existing = FindObjectsByType<UnityEngine.EventSystems.EventSystem>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (existing != null && existing.Length > 0)
            {
                var es = existing[0];
                if (!es.gameObject.activeSelf) es.gameObject.SetActive(true);
                if (!es.enabled) es.enabled = true;
                Debug.Log("[MotoCalibrationPanel] EventSystem existente reutilizado (estaba inactive/disabled)");
            }
            else
            {
                var esGo = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
                Debug.Log("[MotoCalibrationPanel] EventSystem ausente — creé uno (sin DontDestroyOnLoad)");
            }
        }
        go.AddComponent<GraphicRaycaster>();
    }

    // ─── Header ─────────────────────────────────────────────────────

    private void BuildHeader(Transform parent)
    {
        var title = CreateText(parent, "CALIBRACIÓN MOTO SIMULATOR", 32f, FontStyles.Bold, Color.white, 360f);
        title.alignment = TextAlignmentOptions.Center;

        // v1.9.0 rollback button: el operador puede desactivar JSON mode y volver
        // a PlayerPrefs MOTO_* legacy. PlayerPrefs no se borran, así que el
        // rollback es inmediato (próximo boot usa código legacy).
        var legacyBtn = CreateButton(parent, "Volver a modo legacy", 13f, () => {
            MotoControlMapping.SetJsonMode(false);
            Debug.Log("[MotoCalibrationPanel] Modo legacy activado — reinicia el juego para aplicar.");
            Close(save: false);
        });
        legacyBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(380f, 320f);
        legacyBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(220f, 36f);
        legacyBtn.GetComponent<Image>().color = new Color(0.55f, 0.35f, 0.15f, 1f);

        string fp = string.IsNullOrEmpty(_draft.deviceFingerprint) ? "(sin huella)" : _draft.deviceFingerprint;
        var fpTxt = CreateText(parent, $"Vehículo: {_draft.vehicleType} · Fingerprint: {fp}", 13f, FontStyles.Italic, new Color(0.7f, 0.7f, 0.75f), 280f);
        fpTxt.alignment = TextAlignmentOptions.Center;

        var vidPidTxt = CreateText(parent, $"VID: {_draft.vid} · PID: {_draft.pid}", 12f, FontStyles.Italic, new Color(0.6f, 0.6f, 0.65f), 258f);
        vidPidTxt.alignment = TextAlignmentOptions.Center;

        _headerCalibratedAt = CreateText(parent, FormatCalibratedAt(), 13f, FontStyles.Italic, new Color(0.7f, 0.7f, 0.75f), 236f);
        _headerCalibratedAt.alignment = TextAlignmentOptions.Center;
    }

    private string FormatCalibratedAt()
    {
        if (string.IsNullOrEmpty(_draft.calibratedAt)) return "Último cambio: (nunca)";
        if (System.DateTime.TryParse(_draft.calibratedAt, out var dt))
            return $"Último cambio: {dt.ToLocalTime():yyyy-MM-dd HH:mm} ({_draft.calibratedBy})";
        return $"Último cambio: {_draft.calibratedAt}";
    }

    // ─── Control rows ───────────────────────────────────────────────

    private void BuildControlRows(Transform parent)
    {
        // Moto tiene solo 5 controles — caben en una página sin tabs.
        // Espaciado generoso para que las secciones se vean separadas.
        const float ROW_START_Y = 170f;
        const float ROW_SPACING = 55f;

        CreateSectionHeader(parent, "CONDUCCIÓN", ROW_START_Y + 30f);
        BuildRow(parent, ROW_START_Y,                  "Inclinación (lean)", "lean",      () => FormatAxisRange(_draft.axes.lean),       () => true, () => DetectAxisRange("lean"));
        BuildRow(parent, ROW_START_Y - ROW_SPACING,    "Manubrio",           "handlebar", () => FormatAxisRange(_draft.axes.handlebar),  () => true, () => DetectAxisRange("handlebar"));
        BuildRow(parent, ROW_START_Y - ROW_SPACING*2,  "Acelerador",         "gas",       () => FormatPedal(_draft.axes.gas),            () => true, () => DetectPedalAxis("gas"));

        CreateSectionHeader(parent, "BOTONES", ROW_START_Y - ROW_SPACING*3 - 8f);
        BuildRow(parent, ROW_START_Y - ROW_SPACING*4,  "Freno",              "brake",     () => _draft.buttons.brake.path,               () => true, () => DetectButton("brake"));
        BuildRow(parent, ROW_START_Y - ROW_SPACING*5,  "Clutch",             "clutch",    () => _draft.buttons.clutch.path,              () => true, () => DetectButton("clutch"));

        // Warning de rango estrecho (si aplica)
        if (_rangeWarning != null)
        {
            var warn = CreateText(parent, _rangeWarning, 13f, FontStyles.Bold, new Color(0.95f, 0.7f, 0.3f), ROW_START_Y - ROW_SPACING*6 - 10f);
            warn.alignment = TextAlignmentOptions.Center;
            warn.rectTransform.sizeDelta = new Vector2(1000f, 30f);
        }
    }

    private string FormatAxisRange(MotoAxisRange a)
    {
        if (string.IsNullOrEmpty(a.path)) return "(vacío)";
        return $"{a.path}  min={a.min:F2}  max={a.max:F2}  c={a.center:F2}";
    }

    private string FormatPedal(MotoPedal p)
    {
        if (string.IsNullOrEmpty(p.path)) return "(vacío)";
        return $"{p.path}  rest={p.rest:F2}  press={p.press:F2}";
    }

    private void CreateSectionHeader(Transform parent, string title, float y)
    {
        var bg = new GameObject("SectionHeaderBg", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(parent, false);
        bg.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f, 1f);
        var rt = bg.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1020f, 32f);
        rt.anchoredPosition = new Vector2(0f, y);
        var titleTxt = CreateText(bg.transform, title, 16f, FontStyles.Bold, new Color(0.85f, 0.85f, 0.95f), 0f);
        titleTxt.alignment = TextAlignmentOptions.Center;
    }

    private void BuildRow(Transform parent, float y, string label, string captureKey,
        System.Func<string> getValue, System.Func<bool> required, System.Action onDetect)
    {
        var row = new GameObject("Row_" + label, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        var rt = row.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1000f, 40f);
        rt.anchoredPosition = new Vector2(0f, y);

        var lbl = CreateText(row.transform, label, 18f, FontStyles.Bold, Color.white, 0f);
        lbl.rectTransform.sizeDelta = new Vector2(260f, 30f);
        lbl.rectTransform.anchoredPosition = new Vector2(-360f, 0f);
        lbl.alignment = TextAlignmentOptions.Left;

        string val = getValue();
        var valTxt = CreateText(row.transform, string.IsNullOrEmpty(val) ? "(vacío)" : val, 14f, FontStyles.Italic, RowStateColor(required(), val), 0f);
        valTxt.rectTransform.sizeDelta = new Vector2(440f, 28f);
        valTxt.rectTransform.anchoredPosition = new Vector2(60f, 0f);

        // Determinar estado del botón.
        string btnLabel = "Detectar";
        Color btnColor = new Color(0.2f, 0.4f, 0.6f, 1f); // azul idle
        bool btnInteractable = true;
        if (_capturing && _captureTargetField == captureKey)
        {
            btnLabel = "Detectando...";
            btnColor = new Color(0.82f, 0.5f, 0.13f, 1f); // naranja active
            btnInteractable = false;
        }
        else if (_capturing)
        {
            btnLabel = "Esperando...";
            btnColor = new Color(0.32f, 0.32f, 0.36f, 1f); // gris disabled
            btnInteractable = false;
        }
        else if (_lastSavedKey == captureKey
                 && Time.unscaledTime - _lastSavedAt <= SAVED_FEEDBACK_SECONDS)
        {
            btnLabel = "Guardado";
            btnColor = new Color(0.36f, 0.72f, 0.34f, 1f); // verde saved
            btnInteractable = false;
        }

        var btn = CreateButton(row.transform, btnLabel, 14f, onDetect);
        btn.GetComponent<RectTransform>().anchoredPosition = new Vector2(420f, 0f);
        btn.GetComponent<RectTransform>().sizeDelta = new Vector2(160f, 32f);
        btn.GetComponent<Image>().color = btnColor;
        btn.interactable = btnInteractable;
    }

    private Color RowStateColor(bool required, string value)
    {
        bool empty = string.IsNullOrEmpty(value) || value == "(vacío)";
        if (!required) return new Color(0.45f, 0.45f, 0.45f);   // gris (no aplica)
        if (empty) return new Color(0.95f, 0.4f, 0.4f);          // rojo (faltante required)
        return new Color(0.45f, 0.85f, 0.4f);                    // verde (asignado)
    }

    // ─── DetectButton ───────────────────────────────────────────────

    private void DetectButton(string targetField)
    {
        if (_capturing)
        {
            Debug.LogWarning("[MotoCalibrationPanel] Ya hay una captura en curso");
            return;
        }
        _capturing = true;
        _captureTargetField = targetField;
        // Rebuildar para mostrar "Detectando..." en esta fila inmediatamente.
        if (_panelRoot != null) Destroy(_panelRoot);
        BuildUI();
        StartCoroutine(CoCaptureButton());
    }

    // Per-frame edge detect en el device Moto. Mantiene `prevPressed`
    // actualizado cada frame; captura un path solo cuando transiciona OFF→ON.
    private System.Collections.IEnumerator CoCaptureButton()
    {
        Debug.Log($"[MotoCalibrationPanel] Capturing button for '{_captureTargetField}' (per-frame edge detect)");
        float timeout = Time.unscaledTime + 10f;
        var prevPressed = new HashSet<string>();
        // Seed inicial: paths actualmente pressed se ignoran este frame.
        ScanPressed(prevPressed);

        while (Time.unscaledTime < timeout && _capturing)
        {
            var nowPressed = new HashSet<string>();
            ScanPressed(nowPressed);
            foreach (var path in nowPressed)
            {
                if (!prevPressed.Contains(path))
                {
                    // EDGE off→on real → captura.
                    ApplyCapturedButton(path);
                    _capturing = false;
                    yield break;
                }
            }
            prevPressed = nowPressed;
            yield return null;
        }

        if (_capturing)
        {
            Debug.LogWarning($"[MotoCalibrationPanel] Captura timeout para '{_captureTargetField}' (10s sin transición)");
            _capturing = false;
            if (_panelRoot != null) Destroy(_panelRoot);
            BuildUI();
        }
    }

    private static void ScanPressed(HashSet<string> output)
    {
        foreach (var dev in InputSystem.devices)
        {
            if (!Gley.UrbanSystem.UIInputNew.IsMotoSimulator(dev)) continue;
            foreach (var c in dev.allControls)
            {
                if (c is UnityEngine.InputSystem.Controls.ButtonControl bc && bc.isPressed)
                    output.Add(c.name);
            }
        }
    }

    private void ApplyCapturedButton(string path)
    {
        Debug.Log($"[MotoCalibrationPanel] Captured '{_captureTargetField}' = {path}");
        switch (_captureTargetField)
        {
            case "brake": _draft.buttons.brake.path = path; break;
            case "clutch": _draft.buttons.clutch.path = path; break;
        }
        _hasChanges = true;
        // Marca "Guardado" en esta fila durante SAVED_FEEDBACK_SECONDS.
        _lastSavedKey = _captureTargetField;
        _lastSavedAt = Time.unscaledTime;
        if (_panelRoot != null) Destroy(_panelRoot);
        BuildUI();
    }

    // ─── DetectPedalAxis (gas) ──────────────────────────────────────

    // Moto gas usa el Hall throttle del simbt001 firmware (canonical rest=-1 press=+1).
    // Lección v1.8.1 G923: NO inferir rest/press desde ReadUnprocessedValue al inicio
    // (puede ser 0 si InputSystem no había polled). HARDCODE por axis NAME.
    private void DetectPedalAxis(string which)
    {
        if (_capturing) return;
        _capturing = true;
        _captureTargetField = which;
        if (_panelRoot != null) Destroy(_panelRoot);
        BuildUI();
        StartCoroutine(CoCapturePedalAxis(which));
    }

    private System.Collections.IEnumerator CoCapturePedalAxis(string which)
    {
        Debug.Log($"[MotoCalibrationPanel] Capturing pedal axis for '{which}' — sample 3s");
        InputDevice moto = FindMotoDevice();
        if (moto == null)
        {
            Debug.LogWarning("[MotoCalibrationPanel] Moto Simulator not found");
            _capturing = false;
            if (_panelRoot != null) Destroy(_panelRoot);
            BuildUI();
            yield break;
        }

        // Candidatos para gas del Moto Simulator (Hall throttle firmware).
        // 'rz' es el canonical del simbt001. Otros axes incluidos por defensa.
        var initial = new Dictionary<string, float>();
        var maxAbs = new Dictionary<string, float>();
        string[] candidates = { "rz", "z", "stick/y" };
        foreach (var name in candidates)
        {
            var ctrl = moto.TryGetChildControl(name) as UnityEngine.InputSystem.Controls.AxisControl;
            if (ctrl != null)
            {
                initial[name] = ctrl.ReadUnprocessedValue();
                maxAbs[name] = 0f;
            }
        }

        float deadline = Time.unscaledTime + 3f;
        while (Time.unscaledTime < deadline)
        {
            foreach (var name in candidates)
            {
                var ctrl = moto.TryGetChildControl(name) as UnityEngine.InputSystem.Controls.AxisControl;
                if (ctrl == null) continue;
                float v = ctrl.ReadUnprocessedValue();
                if (!initial.ContainsKey(name)) continue;
                float delta = Mathf.Abs(v - initial[name]);
                if (delta > maxAbs[name]) maxAbs[name] = delta;
            }
            yield return null;
        }

        string chosen = null;
        float chosenDelta = 0.6f;
        foreach (var name in candidates)
            if (maxAbs.TryGetValue(name, out var d) && d > chosenDelta) { chosen = name; chosenDelta = d; }

        if (chosen == null)
        {
            Debug.LogWarning("[MotoCalibrationPanel] Ningún pedal axis superó threshold 0.6 en 3s");
            _capturing = false;
            if (_panelRoot != null) Destroy(_panelRoot);
            BuildUI();
            yield break;
        }

        // Hardcoded canonical por axis NAME (lección v1.8.1).
        // simbt001 Hall throttle: idle=-1, fullPress=+1 reportado vía 'rz'.
        float restVal, pressVal;
        switch (chosen)
        {
            case "rz":
                restVal = -1f; pressVal = 1f;
                break;
            default:
                // Fallback defensivo: si en el futuro otro axis se agrega al
                // candidate list, inferir por baseline + warning.
                restVal = initial[chosen];
                pressVal = restVal >= 0f ? -1f : 1f;
                Debug.LogWarning($"[MotoCalibrationPanel] Gas axis '{chosen}' no canónico, usando inferencia legacy rest={restVal:F2} press={pressVal:F2}");
                break;
        }

        Debug.Log($"[MotoCalibrationPanel] Captured pedal '{which}' = {chosen} (delta {chosenDelta:F2}, rest={restVal:F2}, press={pressVal:F2})");
        if (which == "gas")
        {
            _draft.axes.gas.path = chosen;
            _draft.axes.gas.rest = restVal;
            _draft.axes.gas.press = pressVal;
        }

        _hasChanges = true;
        _lastSavedKey = which; _lastSavedAt = Time.unscaledTime;
        _capturing = false;
        if (_panelRoot != null) Destroy(_panelRoot);
        BuildUI();
    }

    // ─── DetectAxisRange (lean / handlebar) ─────────────────────────

    // Lean y handlebar usan 3-phase capture igual que G923 steer:
    // Phase 1 (center, 1s): captura baseline center
    // Phase 2 (left/min, 3s): operador recuesta la moto a tope izquierda
    // Phase 3 (right/max, 3s): operador recuesta a tope derecha
    // Guarda min/max/center en MotoAxisRange.
    private void DetectAxisRange(string which)
    {
        if (_capturing) return;
        _capturing = true;
        _captureTargetField = which;
        if (_panelRoot != null) Destroy(_panelRoot);
        BuildUI();
        StartCoroutine(CoCaptureAxisRange(which));
    }

    private System.Collections.IEnumerator CoCaptureAxisRange(string which)
    {
        InputDevice moto = FindMotoDevice();
        if (moto == null)
        {
            Debug.LogWarning("[MotoCalibrationPanel] Moto Simulator not found");
            _capturing = false;
            if (_panelRoot != null) Destroy(_panelRoot);
            BuildUI();
            yield break;
        }

        // Candidatos por defecto según schema MotoMapping:
        //   lean → stick/x
        //   handlebar → stick/y
        string defaultPath = which == "lean" ? "stick/x" : "stick/y";
        string axisPath = which == "lean"
            ? (string.IsNullOrEmpty(_draft.axes.lean.path) ? defaultPath : _draft.axes.lean.path)
            : (string.IsNullOrEmpty(_draft.axes.handlebar.path) ? defaultPath : _draft.axes.handlebar.path);

        var ctrl = moto.TryGetChildControl(axisPath) as UnityEngine.InputSystem.Controls.AxisControl;
        if (ctrl == null)
        {
            Debug.LogWarning($"[MotoCalibrationPanel] Axis '{axisPath}' no encontrado en device");
            _capturing = false;
            if (_panelRoot != null) Destroy(_panelRoot);
            BuildUI();
            yield break;
        }

        Debug.Log($"[MotoCalibrationPanel] {which}: mantén CENTRO (1s)");
        float center = 0f;
        float t0 = Time.unscaledTime;
        while (Time.unscaledTime - t0 < 1f)
        {
            center = ctrl.ReadUnprocessedValue();
            yield return null;
        }

        Debug.Log($"[MotoCalibrationPanel] {which}: recuesta a la IZQ a tope (3s)");
        float minObserved = center;
        t0 = Time.unscaledTime;
        while (Time.unscaledTime - t0 < 3f)
        {
            float v = ctrl.ReadUnprocessedValue();
            if (v < minObserved) minObserved = v;
            yield return null;
        }

        Debug.Log($"[MotoCalibrationPanel] {which}: recuesta a la DER a tope (3s)");
        float maxObserved = center;
        t0 = Time.unscaledTime;
        while (Time.unscaledTime - t0 < 3f)
        {
            float v = ctrl.ReadUnprocessedValue();
            if (v > maxObserved) maxObserved = v;
            yield return null;
        }

        // Validación de rango: si span < 0.5 el operador no recostó suficiente
        // (calibración corrupta — match con MotoMappingMigration.ValidateRange).
        float span = maxObserved - minObserved;
        if (span < 0.5f)
        {
            Debug.LogWarning($"[MotoCalibrationPanel] {which} span={span:F2} < 0.5 (rango muy estrecho)");
            _rangeWarning = $"Rango muy estrecho ({which} span {span:F2}). Vuelve a recostar la moto a tope en ambos lados.";
            _rangeWarningAt = Time.unscaledTime;
            // NO guardar — mantener valores previos del draft.
            _capturing = false;
            if (_panelRoot != null) Destroy(_panelRoot);
            BuildUI();
            yield break;
        }

        if (which == "lean")
        {
            _draft.axes.lean.path = axisPath;
            _draft.axes.lean.min = minObserved;
            _draft.axes.lean.max = maxObserved;
            _draft.axes.lean.center = center;
        }
        else if (which == "handlebar")
        {
            _draft.axes.handlebar.path = axisPath;
            _draft.axes.handlebar.min = minObserved;
            _draft.axes.handlebar.max = maxObserved;
            _draft.axes.handlebar.center = center;
        }

        _hasChanges = true;
        _lastSavedKey = which; _lastSavedAt = Time.unscaledTime;
        _rangeWarning = null;
        Debug.Log($"[MotoCalibrationPanel] {which} captured: min={minObserved:F2} max={maxObserved:F2} center={center:F2}");

        _capturing = false;
        if (_panelRoot != null) Destroy(_panelRoot);
        BuildUI();
    }

    // ─── Footer + RestoreCanonical ──────────────────────────────────

    private void BuildFooter(Transform parent)
    {
        var saveBtn = CreateButton(parent, "Guardar y salir", 13f, () => Close(save: true));
        saveBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(-220f, -340f);
        saveBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(180f, 36f);

        var cancelBtn = CreateButton(parent, "Cancelar", 13f, () => Close(save: false));
        cancelBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(-10f, -340f);
        cancelBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(140f, 36f);
        cancelBtn.GetComponent<Image>().color = new Color(0.45f, 0.25f, 0.25f, 1f);

        var resetBtn = CreateButton(parent, "Restaurar defaults canónicos", 12f, () => RestoreCanonicalDefaults());
        resetBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(200f, -340f);
        resetBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(280f, 36f);

        var hint = CreateText(parent, "Esc cancela sin guardar · F8 sostén cierra con guardar", 11f, FontStyles.Italic, new Color(0.6f, 0.6f, 0.7f), -370f);
        hint.alignment = TextAlignmentOptions.Center;
    }

    private void RestoreCanonicalDefaults()
    {
        // Mapping Moto Simulator canónico (single-variant, simbt001 firmware).
        // Lean=stick/x, handlebar=stick/y, gas=rz (Hall throttle), brake=button1, clutch=button2.
        string fp = _draft.deviceFingerprint;
        _draft = new MotoMapping
        {
            schemaVersion = MotoControlMapping.CURRENT_SCHEMA,
            vehicleType = "motorcycle",
            deviceFingerprint = fp,
            vid = "303A",
            pid = "4D54",
            calibratedBy = "F8-panel",
        };

        // Axes canónicos
        _draft.axes.lean.path = "stick/x";
        _draft.axes.lean.min = -1f;
        _draft.axes.lean.max = 1f;
        _draft.axes.lean.center = 0f;

        _draft.axes.handlebar.path = "stick/y";
        _draft.axes.handlebar.min = -1f;
        _draft.axes.handlebar.max = 1f;
        _draft.axes.handlebar.center = 0f;

        _draft.axes.gas.path = "rz";
        _draft.axes.gas.rest = -1f;
        _draft.axes.gas.press = 1f;

        // Botones canónicos
        _draft.buttons.brake.path = "button1";
        _draft.buttons.brake.required = false;
        _draft.buttons.brake.kind = "hold";

        _draft.buttons.clutch.path = "button2";
        _draft.buttons.clutch.required = false;
        _draft.buttons.clutch.kind = "hold";

        _hasChanges = true;
        _rangeWarning = null;
        if (_panelRoot != null) Destroy(_panelRoot);
        BuildUI();
    }

    // ─── Helpers ────────────────────────────────────────────────────

    private InputDevice FindMotoDevice()
    {
        foreach (var dev in InputSystem.devices)
            if (Gley.UrbanSystem.UIInputNew.IsMotoSimulator(dev)) return dev;
        return null;
    }

    // ─── UI helpers ─────────────────────────────────────────────────

    private TMP_Text CreateText(Transform parent, string content, float size, FontStyles style, Color color, float y)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = content;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = TextAlignmentOptions.Left;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        // CRITICAL: text NO debe interceptar clicks; si hereda raycastTarget=true,
        // bloquea clicks a los buttons que lo contienen.
        t.raycastTarget = false;
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(800f, 30f);
        rt.anchoredPosition = new Vector2(0f, y);
        return t;
    }

    private Button CreateButton(Transform parent, string label, float fontSize, System.Action onClick)
    {
        var go = new GameObject("Btn", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = new Color(0.2f, 0.4f, 0.6f, 1f);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(100f, 30f);
        var txt = CreateText(go.transform, label, fontSize, FontStyles.Normal, Color.white, 0f);
        txt.alignment = TextAlignmentOptions.Center;
        var btn = go.GetComponent<Button>();
        btn.onClick.AddListener(() => onClick?.Invoke());
        return btn;
    }
}
