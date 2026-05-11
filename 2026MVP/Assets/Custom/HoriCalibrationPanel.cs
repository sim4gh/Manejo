using System.Collections.Generic;
using TlaxSim.HoriCalibration;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Panel de calibración HORI (F8 sostén 1.5s cuando HORI conectado).
// Reemplaza al BindingsPanel cuando hay HORI; G923/Moto siguen usando BindingsPanel.
//
// v1.7.0 — único writer del JSON hori_mapping.json. Ver:
// docs/superpowers/specs/2026-05-11-hori-v170-immutable-calibration-design.md
public class HoriCalibrationPanel : MonoBehaviour
{
    public static HoriCalibrationPanel Instance { get; private set; }
    public bool IsOpen { get; private set; }

    private Canvas _canvas;
    private GameObject _panelRoot;
    private HoriMapping _draft; // copia editable del Active
    private bool _hasChanges;
    private bool _manualMode;
    private TMP_Text _headerCalibratedAt;

    private bool _capturing;
    private string _captureTargetField;
    private bool _captureWheelOnly;
    private bool _captureShifterOnly;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        if (FindFirstObjectByType<HoriCalibrationPanel>() != null) return;
        var go = new GameObject("[HoriCalibrationPanel]");
        var panel = go.AddComponent<HoriCalibrationPanel>();
        DontDestroyOnLoad(go);
        Instance = panel;
        Debug.Log("[HoriCalibrationPanel] AutoCreate OK · activa cuando HORI conectado");
    }

    private void Start()
    {
        // Task 5.8: Cargar JSON al boot del juego — corre una sola vez post-AutoCreate.
        HoriControlMapping.LoadFromDisk();
        Debug.Log("[HoriCalibrationPanel] Active mapping: " + (HoriControlMapping.Active != null ? "loaded" : "null"));
    }

    public static bool IsHoriConnected()
    {
        foreach (var dev in InputSystem.devices)
        {
            // Reuse IsHORITruck del UIInputNew para detection consistency
            if (Gley.UrbanSystem.UIInputNew.IsHORITruck(dev)) return true;
        }
        return false;
    }

    public void Open()
    {
        if (IsOpen) return;
        // Snapshot del Active para edición (copia profunda via JSON round-trip)
        if (HoriControlMapping.Active != null)
        {
            string json = JsonUtility.ToJson(HoriControlMapping.Active);
            _draft = JsonUtility.FromJson<HoriMapping>(json);
        }
        else
        {
            _draft = new HoriMapping();
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
            HoriControlMapping.Save(_draft);

            // Forzar a UIInputNew a re-attach (relee los binds del nuevo Active)
            var uiInput = FindFirstObjectByType<Gley.UrbanSystem.UIInputNew>();
            if (uiInput != null)
            {
                // ReloadBindings/ReloadTuning existen si el UIInputNew los expone; si no,
                // un reattach completo via OnDeviceChange manual sería ideal pero costoso.
                // Por ahora confiamos en que se refresque al volver al MainMenu.
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
            // Esc cierra con prompt si hay cambios (por ahora auto-save sin prompt)
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close(save: _hasChanges);
            }
        }
    }

    private void BuildUI()
    {
        EnsureCanvas();
        _panelRoot = new GameObject("HoriCalPanel");
        _panelRoot.transform.SetParent(_canvas.transform, false);
        var bg = _panelRoot.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.95f);
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;

        // Card central
        var card = new GameObject("Card", typeof(Image));
        card.transform.SetParent(_panelRoot.transform, false);
        card.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.14f, 1f);
        var cardRt = card.GetComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(900f, 720f);

        BuildHeader(card.transform);
        BuildControlRows(card.transform);
        BuildFooter(card.transform);
    }

    private void EnsureCanvas()
    {
        if (_canvas != null) return;
        var go = new GameObject("HoriCalCanvas");
        go.transform.SetParent(transform, false);
        _canvas = go.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 9999;
        go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        go.AddComponent<GraphicRaycaster>();
    }

    // ─── Task 5.2: Header ───────────────────────────────────────────

    private void BuildHeader(Transform parent)
    {
        var title = CreateText(parent, "CALIBRACIÓN HORI TRUCK", 22f, FontStyles.Bold, Color.white, 340f);
        title.alignment = TextAlignmentOptions.Center;

        _manualMode = PlayerPrefs.GetInt("TransmisionManual", 0) == 1;
        var transmissionRow = CreateRow(parent, 300f, 40f);
        var label = CreateText(transmissionRow, "Transmisión: ", 14f, FontStyles.Normal, Color.white, 0f);
        label.rectTransform.anchoredPosition = new Vector2(-200f, 0f);

        var autoBtn = CreateButton(transmissionRow, "Automática", 14f, () => SetManual(false));
        autoBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(-50f, 0f);

        var manBtn = CreateButton(transmissionRow, "Manual", 14f, () => SetManual(true));
        manBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(80f, 0f);

        string fp = string.IsNullOrEmpty(_draft.deviceFingerprint) ? "(sin huella)" : _draft.deviceFingerprint;
        CreateText(parent, $"Fingerprint: {fp}", 11f, FontStyles.Italic, new Color(0.7f, 0.7f, 0.75f), 260f);

        _headerCalibratedAt = CreateText(parent, FormatCalibratedAt(), 11f, FontStyles.Italic, new Color(0.7f, 0.7f, 0.75f), 240f);
    }

    private void SetManual(bool manual)
    {
        _manualMode = manual;
        _draft.axes.clutch.required = manual;
        _draft.buttons.gear1.required = manual;
        _draft.buttons.gear2.required = manual;
        _draft.buttons.gear3.required = manual;
        _draft.buttons.gear4.required = manual;
        _draft.buttons.gear5.required = manual;
        _draft.buttons.gear6.required = manual;
        _hasChanges = true;
    }

    private string FormatCalibratedAt()
    {
        if (string.IsNullOrEmpty(_draft.calibratedAt)) return "Último cambio: (nunca)";
        if (System.DateTime.TryParse(_draft.calibratedAt, out var dt))
            return $"Último cambio: {dt.ToLocalTime():yyyy-MM-dd HH:mm} ({_draft.calibratedBy})";
        return $"Último cambio: {_draft.calibratedAt}";
    }

    // ─── Task 5.3: Control rows ──────────────────────────────────────

    private void BuildControlRows(Transform parent)
    {
        CreateSectionHeader(parent, "CONDUCCIÓN", 195f);
        BuildRow(parent, 170f, "Volante",     () => _draft.axes.steer.path, () => true, () => DetectSteer());
        BuildRow(parent, 140f, "Acelerador",  () => "reader: " + _draft.axes.gas.source, () => true, () => VerifyThrottle());
        BuildRow(parent, 110f, "Freno",       () => _draft.axes.brake.path, () => true, () => DetectPedalAxis("brake"));
        BuildRow(parent,  80f, "Clutch",      () => _draft.axes.clutch.path, () => _manualMode, () => DetectPedalAxis("clutch"));

        CreateSectionHeader(parent, "LUCES Y SEÑALES", 35f);
        BuildRow(parent,   10f, "Claxon",        () => _draft.buttons.horn.path,      () => true, () => DetectButton("horn"));
        BuildRow(parent,  -20f, "Intermitentes", () => _draft.buttons.hazards.path,   () => true, () => DetectButton("hazards", shifterOnly: true));
        BuildRow(parent,  -50f, "Flecha izq",    () => _draft.buttons.turnLeft.path,  () => true, () => DetectButton("turnLeft", wheelOnly: true));
        BuildRow(parent,  -80f, "Flecha der",    () => _draft.buttons.turnRight.path, () => true, () => DetectButton("turnRight", wheelOnly: true));

        CreateSectionHeader(parent, "REVERSA", -130f);
        BuildRow(parent, -160f, "Reversa (palanca)", () => _draft.buttons.reverse.path + " (pulse)", () => true, () => DetectButton("reverse", shifterOnly: true));

        CreateSectionHeader(parent, "MARCHAS MANUAL", -210f);
        BuildRow(parent, -240f, "1ª",  () => _draft.buttons.gear1.path, () => _manualMode, () => DetectButton("gear1", shifterOnly: true));
        BuildRow(parent, -270f, "2ª",  () => _draft.buttons.gear2.path, () => _manualMode, () => DetectButton("gear2", shifterOnly: true));
        BuildRow(parent, -300f, "3ª",  () => _draft.buttons.gear3.path, () => _manualMode, () => DetectButton("gear3", shifterOnly: true));
        BuildRow(parent, -330f, "4ª",  () => _draft.buttons.gear4.path, () => _manualMode, () => DetectButton("gear4", shifterOnly: true));
        BuildRow(parent, -360f, "5ª",  () => _draft.buttons.gear5.path, () => _manualMode, () => DetectButton("gear5", shifterOnly: true));
        BuildRow(parent, -390f, "6ª",  () => _draft.buttons.gear6.path, () => _manualMode, () => DetectButton("gear6", shifterOnly: true));
    }

    private void CreateSectionHeader(Transform parent, string title, float y)
    {
        var bg = new GameObject("SectionHeaderBg", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(parent, false);
        bg.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f, 1f);
        var rt = bg.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(820f, 22f);
        rt.anchoredPosition = new Vector2(0f, y);
        CreateText(bg.transform, title, 12f, FontStyles.Bold, new Color(0.85f, 0.85f, 0.95f), 0f);
    }

    private void BuildRow(Transform parent, float y, string label, System.Func<string> getValue, System.Func<bool> required, System.Action onDetect)
    {
        var row = new GameObject("Row_" + label, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        var rt = row.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(820f, 26f);
        rt.anchoredPosition = new Vector2(0f, y);

        var lbl = CreateText(row.transform, label, 12f, FontStyles.Normal, Color.white, 0f);
        lbl.rectTransform.sizeDelta = new Vector2(150f, 20f);
        lbl.rectTransform.anchoredPosition = new Vector2(-340f, 0f);

        string val = getValue();
        var valTxt = CreateText(row.transform, string.IsNullOrEmpty(val) ? "(vacío)" : val, 12f, FontStyles.Italic, RowStateColor(required(), val), 0f);
        valTxt.rectTransform.sizeDelta = new Vector2(380f, 20f);
        valTxt.rectTransform.anchoredPosition = new Vector2(60f, 0f);

        var btn = CreateButton(row.transform, "Detectar", 11f, onDetect);
        btn.GetComponent<RectTransform>().anchoredPosition = new Vector2(330f, 0f);
        btn.GetComponent<RectTransform>().sizeDelta = new Vector2(80f, 22f);
    }

    private Color RowStateColor(bool required, string value)
    {
        bool empty = string.IsNullOrEmpty(value);
        if (!required) return new Color(0.45f, 0.45f, 0.45f);   // gris (no aplica)
        if (empty) return new Color(0.95f, 0.4f, 0.4f);          // rojo (faltante required)
        return new Color(0.45f, 0.85f, 0.4f);                    // verde (asignado)
    }

    // ─── Task 5.4: DetectButton ─────────────────────────────────────

    private void DetectButton(string targetField, bool wheelOnly = false, bool shifterOnly = false)
    {
        if (_capturing)
        {
            Debug.LogWarning("[HoriCalibrationPanel] Ya hay una captura en curso");
            return;
        }
        _capturing = true;
        _captureTargetField = targetField;
        _captureWheelOnly = wheelOnly;
        _captureShifterOnly = shifterOnly;
        StartCoroutine(CoCaptureButton());
    }

    private System.Collections.IEnumerator CoCaptureButton()
    {
        Debug.Log($"[HoriCalibrationPanel] Capturing button for '{_captureTargetField}' (wheel={_captureWheelOnly} shifter={_captureShifterOnly})");
        float timeout = Time.unscaledTime + 8f;
        InputDevice wheel = null, shifter = null;
        foreach (var dev in InputSystem.devices)
        {
            if (Gley.UrbanSystem.UIInputNew.IsHORITruckWheel(dev)) wheel = dev;
            else if (Gley.UrbanSystem.UIInputNew.IsHORITruckShifter(dev)) shifter = dev;
        }

        while (Time.unscaledTime < timeout && _capturing)
        {
            if (!_captureShifterOnly && wheel != null)
            {
                foreach (var c in wheel.allControls)
                {
                    if (c is UnityEngine.InputSystem.Controls.ButtonControl bc && bc.isPressed)
                    {
                        ApplyCapturedButton("wheel:" + c.name);
                        _capturing = false;
                        yield break;
                    }
                }
            }
            if (!_captureWheelOnly && shifter != null)
            {
                foreach (var c in shifter.allControls)
                {
                    if (c is UnityEngine.InputSystem.Controls.ButtonControl bc && bc.isPressed)
                    {
                        ApplyCapturedButton("shifter:" + c.name);
                        _capturing = false;
                        yield break;
                    }
                }
            }
            yield return null;
        }

        if (_capturing)
        {
            Debug.LogWarning($"[HoriCalibrationPanel] Captura timeout para '{_captureTargetField}'");
            _capturing = false;
        }
    }

    private void ApplyCapturedButton(string path)
    {
        Debug.Log($"[HoriCalibrationPanel] Captured '{_captureTargetField}' = {path}");
        switch (_captureTargetField)
        {
            case "horn": _draft.buttons.horn.path = path; break;
            case "hazards": _draft.buttons.hazards.path = path; break;
            case "turnLeft": _draft.buttons.turnLeft.path = path; break;
            case "turnRight": _draft.buttons.turnRight.path = path; break;
            case "reverse": _draft.buttons.reverse.path = path; break;
            case "gear1": _draft.buttons.gear1.path = path; break;
            case "gear2": _draft.buttons.gear2.path = path; break;
            case "gear3": _draft.buttons.gear3.path = path; break;
            case "gear4": _draft.buttons.gear4.path = path; break;
            case "gear5": _draft.buttons.gear5.path = path; break;
            case "gear6": _draft.buttons.gear6.path = path; break;
        }
        _hasChanges = true;
        if (_panelRoot != null) Destroy(_panelRoot);
        BuildUI();
    }

    // ─── Task 5.5: DetectPedalAxis ──────────────────────────────────

    private void DetectPedalAxis(string which)
    {
        if (_capturing) return;
        _capturing = true;
        StartCoroutine(CoCapturePedalAxis(which));
    }

    private System.Collections.IEnumerator CoCapturePedalAxis(string which)
    {
        Debug.Log($"[HoriCalibrationPanel] Capturing pedal axis for '{which}' — sample 3s");
        InputDevice wheel = null;
        foreach (var d in InputSystem.devices) if (Gley.UrbanSystem.UIInputNew.IsHORITruckWheel(d)) wheel = d;
        if (wheel == null) { Debug.LogWarning("[HoriCalibrationPanel] HORI wheel not found"); _capturing = false; yield break; }

        var initial = new Dictionary<string, float>();
        var maxAbs = new Dictionary<string, float>();
        string[] candidates = { "rz", "slider", "slider1" };
        foreach (var name in candidates)
        {
            var ctrl = wheel.TryGetChildControl(name) as UnityEngine.InputSystem.Controls.AxisControl;
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
                var ctrl = wheel.TryGetChildControl(name) as UnityEngine.InputSystem.Controls.AxisControl;
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
            Debug.LogWarning("[HoriCalibrationPanel] Ningún pedal axis superó threshold 0.6 en 3s");
            _capturing = false;
            yield break;
        }

        Debug.Log($"[HoriCalibrationPanel] Captured pedal '{which}' = {chosen} (delta {chosenDelta:F2})");
        if (which == "brake") { _draft.axes.brake.path = chosen; _draft.axes.brake.rest = -1f; _draft.axes.brake.press = 1f; }
        else if (which == "clutch") { _draft.axes.clutch.path = chosen; _draft.axes.clutch.rest = -1f; _draft.axes.clutch.press = 1f; }

        _hasChanges = true;
        _capturing = false;
        if (_panelRoot != null) Destroy(_panelRoot);
        BuildUI();
    }

    // ─── Task 5.6: DetectSteer + VerifyThrottle ────────────────────

    private void DetectSteer()
    {
        if (_capturing) return;
        _capturing = true;
        StartCoroutine(CoCaptureSteer());
    }

    private System.Collections.IEnumerator CoCaptureSteer()
    {
        InputDevice wheel = null;
        foreach (var d in InputSystem.devices) if (Gley.UrbanSystem.UIInputNew.IsHORITruckWheel(d)) wheel = d;
        if (wheel == null) { _capturing = false; yield break; }
        var ctrl = wheel.TryGetChildControl("stick/x") as UnityEngine.InputSystem.Controls.AxisControl;
        if (ctrl == null) { _capturing = false; yield break; }

        Debug.Log("[HoriCalibrationPanel] Steer: gira IZQ a tope (3s)");
        yield return new WaitForSecondsRealtime(3f);
        float left = ctrl.ReadUnprocessedValue();

        Debug.Log("[HoriCalibrationPanel] Steer: gira DER a tope (3s)");
        yield return new WaitForSecondsRealtime(3f);
        float right = ctrl.ReadUnprocessedValue();

        Debug.Log("[HoriCalibrationPanel] Steer: centra (2s)");
        yield return new WaitForSecondsRealtime(2f);
        float center = ctrl.ReadUnprocessedValue();

        _draft.axes.steer.path = "stick/x";
        _draft.axes.steer.leftMax = left;
        _draft.axes.steer.rightMax = right;
        _draft.axes.steer.center = center;
        _hasChanges = true;
        Debug.Log($"[HoriCalibrationPanel] Steer captured: left={left:F2} right={right:F2} center={center:F2}");

        _capturing = false;
        if (_panelRoot != null) Destroy(_panelRoot);
        BuildUI();
    }

    private void VerifyThrottle()
    {
        if (_capturing) return;
        _capturing = true;
        StartCoroutine(CoVerifyThrottle());
    }

    private System.Collections.IEnumerator CoVerifyThrottle()
    {
        Debug.Log("[HoriCalibrationPanel] Pisa el ACELERADOR a fondo (8s timeout)");
        float deadline = Time.unscaledTime + 8f;
        var reader = HoriThrottleReader.Instance;
        if (reader == null || !reader.IsHandleOpen)
        {
            Debug.LogWarning("[HoriCalibrationPanel] HoriThrottleReader sin handle abierto");
            _capturing = false;
            yield break;
        }

        while (Time.unscaledTime < deadline)
        {
            if (reader.Value >= _draft.axes.gas.verifyThreshold)
            {
                Debug.Log($"[HoriCalibrationPanel] Throttle verificado: value={reader.Value:F2}");
                _hasChanges = true;
                _capturing = false;
                yield break;
            }
            yield return null;
        }
        Debug.LogWarning("[HoriCalibrationPanel] Throttle verify timeout — operador no pisó");
        _capturing = false;
    }

    // ─── Task 5.7: Footer + RestoreCanonical ───────────────────────

    private void BuildFooter(Transform parent)
    {
        var saveBtn = CreateButton(parent, "Guardar y salir", 13f, () => Close(save: true));
        saveBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(-180f, -330f);
        saveBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(180f, 32f);

        var resetBtn = CreateButton(parent, "Restaurar defaults canónicos", 12f, () => RestoreCanonicalDefaults());
        resetBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(80f, -330f);
        resetBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(240f, 32f);

        var hint = CreateText(parent, "Esc cancela sin guardar · F8 sostén cierra con guardar", 11f, FontStyles.Italic, new Color(0.6f, 0.6f, 0.7f), -360f);
        hint.alignment = TextAlignmentOptions.Center;
    }

    private void RestoreCanonicalDefaults()
    {
        // Mapping HPC-044U canónico (verified). horn path es wheel:button7 (Phase 0 SSH verify).
        string fp = _draft.deviceFingerprint;
        _draft = new HoriMapping
        {
            schemaVersion = 1,
            deviceFingerprint = fp,
            wheelVID = "0F0D", wheelPID = "017A",
            shifterVID = "0F0D", shifterPID = "0186",
            calibratedBy = "F8-panel",
        };
        _draft.axes.steer.path = "stick/x";
        _draft.axes.steer.leftMax = -1f;
        _draft.axes.steer.rightMax = 1f;
        _draft.axes.brake.path = "rz";
        _draft.axes.brake.rest = -1f;
        _draft.axes.brake.press = 1f;
        _draft.axes.brake.required = true;
        _draft.axes.clutch.path = "slider";
        _draft.axes.clutch.rest = -1f;
        _draft.axes.clutch.press = 1f;
        _draft.axes.clutch.required = _manualMode;
        _draft.buttons.horn.path = "wheel:button7";
        _draft.buttons.horn.required = true;
        _draft.buttons.hazards.path = "shifter:button27";
        _draft.buttons.hazards.required = true;
        _draft.buttons.turnLeft.path = "wheel:button40";
        _draft.buttons.turnLeft.required = true;
        _draft.buttons.turnRight.path = "wheel:button41";
        _draft.buttons.turnRight.required = true;
        _draft.buttons.reverse.path = "shifter:button7";
        _draft.buttons.reverse.required = true;
        _draft.buttons.reverse.kind = "pulse";
        _draft.buttons.gear1.path = "shifter:trigger"; _draft.buttons.gear1.required = _manualMode;
        _draft.buttons.gear2.path = "shifter:button2"; _draft.buttons.gear2.required = _manualMode;
        _draft.buttons.gear3.path = "shifter:button3"; _draft.buttons.gear3.required = _manualMode;
        _draft.buttons.gear4.path = "shifter:button4"; _draft.buttons.gear4.required = _manualMode;
        _draft.buttons.gear5.path = "shifter:button5"; _draft.buttons.gear5.required = _manualMode;
        _draft.buttons.gear6.path = "shifter:button6"; _draft.buttons.gear6.required = _manualMode;
        _hasChanges = true;
        if (_panelRoot != null) Destroy(_panelRoot);
        BuildUI();
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
        var rt = t.rectTransform;
        rt.sizeDelta = new Vector2(800f, 30f);
        rt.anchoredPosition = new Vector2(0f, y);
        return t;
    }

    private Transform CreateRow(Transform parent, float y, float height)
    {
        var go = new GameObject("Row", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(800f, height);
        rt.anchoredPosition = new Vector2(0f, y);
        return go.transform;
    }

    private Button CreateButton(Transform parent, string label, float fontSize, System.Action onClick)
    {
        var go = new GameObject("Btn", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = new Color(0.2f, 0.4f, 0.6f, 1f);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(100f, 30f);
        var txt = CreateText(go.transform, label, fontSize, FontStyles.Normal, Color.white, 0f);
        txt.alignment = TextAlignmentOptions.Center;
        var btn = go.GetComponent<Button>();
        btn.onClick.AddListener(() => onClick?.Invoke());
        return btn;
    }
}
