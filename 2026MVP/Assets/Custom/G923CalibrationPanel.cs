using System.Collections.Generic;
using TlaxSim.G923Calibration;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Panel de calibración G923 (F8 sostén 1.5s cuando G923 conectado y flag G923_UseJsonMapping=1).
// Reemplaza al BindingsPanel cuando hay G923 + JSON mode; HORI sigue usando HoriCalibrationPanel,
// Moto sigue usando BindingsPanel legacy.
//
// v1.8.0 — único writer del JSON g923_mapping.json. Ver:
// docs/superpowers/specs/2026-05-11-g923-v180-immutable-calibration-design.md
public class G923CalibrationPanel : MonoBehaviour
{
    public static G923CalibrationPanel Instance { get; private set; }
    public bool IsOpen { get; private set; }

    private Canvas _canvas;
    private GameObject _panelRoot;
    private G923Mapping _draft; // copia editable del Active
    private bool _hasChanges;
    private bool _manualMode;
    private TMP_Text _headerCalibratedAt;
    private int _currentPage; // 0=Conducción, 1=Luces y Reversa, 2=Marchas

    private bool _capturing;
    private string _captureTargetField; // internal field name (e.g. "horn", "brake", "steer", "gas")
    // Fix A (UX): rastrear última captura exitosa para mostrar "✓ Guardado" 0.7s en esa fila.
    private string _lastSavedKey;
    private float _lastSavedAt = -999f;
    private const float SAVED_FEEDBACK_SECONDS = 0.7f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        if (FindFirstObjectByType<G923CalibrationPanel>() != null) return;
        var go = new GameObject("[G923CalibrationPanel]");
        var panel = go.AddComponent<G923CalibrationPanel>();
        DontDestroyOnLoad(go);
        Instance = panel;
        Debug.Log("[G923CalibrationPanel] AutoCreate OK · activa cuando G923 conectado + flag JSON=1");
    }

    private void Start()
    {
        // v1.8.0: Cargar JSON al boot del juego — corre una sola vez post-AutoCreate.
        // Si flag G923_UseJsonMapping=0, LoadFromDisk deja Active=null y los consumers
        // caen al path PlayerPrefs legacy.
        G923ControlMapping.LoadFromDisk();
        Debug.Log("[G923CalibrationPanel] Active mapping: " + (G923ControlMapping.Active != null ? "loaded variant=" + G923ControlMapping.Active.variant : "null"));
    }

    public static bool IsG923Connected()
    {
        foreach (var dev in InputSystem.devices)
        {
            if (Gley.UrbanSystem.UIInputNew.IsLogitechG923Family(dev)) return true;
        }
        return false;
    }

    public void Open()
    {
        if (IsOpen) return;
        // Snapshot del Active para edición (copia profunda via JSON round-trip)
        if (G923ControlMapping.Active != null)
        {
            string json = JsonUtility.ToJson(G923ControlMapping.Active);
            _draft = JsonUtility.FromJson<G923Mapping>(json);
        }
        else
        {
            _draft = new G923Mapping();
            // Detectar variant del device conectado: displayName contiene "Xbox" → Xbox, else PS.
            var dev = FindG923Device();
            if (dev != null)
            {
                _draft.variant = (dev.displayName.IndexOf("Xbox", System.StringComparison.OrdinalIgnoreCase) >= 0) ? "Xbox" : "PS";
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
            G923ControlMapping.Save(_draft);

            // v1.8.0: refresh defensivo de UIInputNew. AttachToWheelDevice
            // lee G923ControlMapping.Active al momento de attach (boot, device-change,
            // Pantalla 2 TryAttachToDevice). Save() acaba de actualizar Active en
            // memoria; los próximos attach lo verán. Para el frame actual no
            // hay garantía sin un re-attach. ReloadBindings() vuelve a leer
            // PlayerPrefs PREF_BIND_* — no refresca binds G923 desde Active JSON.
            // La solución confiable es volver al MainMenu (recarga de escena
            // re-attachea con Active fresco) o reconectar USB.
            // TODO(v1.8.x): exponer UIInputNew.ReattachWheelDevice() y llamar aquí.
            var uiInput = FindFirstObjectByType<Gley.UrbanSystem.UIInputNew>();
            if (uiInput != null)
            {
                uiInput.ReloadBindings(); // best-effort
                uiInput.ReloadTuning();
                Debug.Log("[G923CalibrationPanel] Save → ReloadBindings/ReloadTuning (refresh in-place del Active completo requiere reattach: volver a MainMenu).");
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
                return;
            }

            // Fix A (UX): auto-revertir "✓ Guardado" → "Detectar" tras ventana SAVED_FEEDBACK_SECONDS.
            // Solo si NO hay captura activa (no queremos rebuildar mid-capture).
            if (!_capturing && _lastSavedKey != null
                && Time.unscaledTime - _lastSavedAt > SAVED_FEEDBACK_SECONDS)
            {
                _lastSavedKey = null;
                if (_panelRoot != null) Destroy(_panelRoot);
                BuildUI();
            }
        }
    }

    private void BuildUI()
    {
        EnsureCanvas();
        _panelRoot = new GameObject("G923CalPanel");
        _panelRoot.transform.SetParent(_canvas.transform, false);
        var bg = _panelRoot.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.95f);
        // El bg SÍ debe interceptar clicks (para bloquear el fondo de Pantalla 2)
        // pero NO debe consumirlos: como el card y sus children están más arriba en
        // la jerarquía, los buttons capturan primero. raycastTarget=true es OK.
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;

        // Card central — full screen card con padding interno
        var card = new GameObject("Card", typeof(Image));
        card.transform.SetParent(_panelRoot.transform, false);
        card.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.14f, 1f);
        var cardRt = card.GetComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(1100f, 1020f);

        BuildHeader(card.transform);
        BuildControlRows(card.transform);
        BuildFooter(card.transform);
    }

    private void EnsureCanvas()
    {
        if (_canvas != null) return;
        var go = new GameObject("G923CalCanvas");
        go.transform.SetParent(transform, false);
        _canvas = go.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 9999;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // EventSystem dedup. Buscar incluso inactivos antes de crear
        // — evita duplicados si la escena tiene uno disabled. Sin DontDestroyOnLoad para
        // no contaminar future scenes con un EventSystem extra que las propias escenas
        // ya traen.
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            var existing = FindObjectsByType<UnityEngine.EventSystems.EventSystem>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (existing != null && existing.Length > 0)
            {
                var es = existing[0];
                if (!es.gameObject.activeSelf) es.gameObject.SetActive(true);
                if (!es.enabled) es.enabled = true;
                Debug.Log("[G923CalibrationPanel] EventSystem existente reutilizado (estaba inactive/disabled)");
            }
            else
            {
                var esGo = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
                Debug.Log("[G923CalibrationPanel] EventSystem ausente — creé uno (sin DontDestroyOnLoad)");
            }
        }
        go.AddComponent<GraphicRaycaster>();
    }

    // ─── Header ─────────────────────────────────────────────────────

    private void BuildHeader(Transform parent)
    {
        var title = CreateText(parent, "CALIBRACIÓN G923", 32f, FontStyles.Bold, Color.white, 420f);
        title.alignment = TextAlignmentOptions.Center;

        _manualMode = PlayerPrefs.GetInt("TransmisionManual", 0) == 1;
        var transmissionRow = CreateRow(parent, 370f, 40f);
        var label = CreateText(transmissionRow, "Transmisión:", 18f, FontStyles.Normal, Color.white, 0f);
        label.rectTransform.anchoredPosition = new Vector2(-180f, 0f);
        label.alignment = TextAlignmentOptions.Right;

        var autoBtn = CreateButton(transmissionRow, "Automática", 16f, () => SetManual(false));
        autoBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(-30f, 0f);
        autoBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(140f, 36f);
        autoBtn.GetComponent<Image>().color = _manualMode ? new Color(0.18f, 0.22f, 0.32f, 1f) : new Color(0.35f, 0.55f, 0.85f, 1f);

        var manBtn = CreateButton(transmissionRow, "Manual", 16f, () => SetManual(true));
        manBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(130f, 0f);
        manBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(140f, 36f);
        manBtn.GetComponent<Image>().color = _manualMode ? new Color(0.35f, 0.55f, 0.85f, 1f) : new Color(0.18f, 0.22f, 0.32f, 1f);

        // v1.8.0 rollback button: el operador puede desactivar JSON mode y volver
        // a PlayerPrefs Discovery legacy. PlayerPrefs no se borran, así que el
        // rollback es inmediato (próximo boot usa código legacy).
        var legacyBtn = CreateButton(transmissionRow, "Volver a modo legacy", 13f, () => {
            G923ControlMapping.SetJsonMode(false);
            Debug.Log("[G923CalibrationPanel] Modo legacy activado — próximo boot usará PlayerPrefs.");
            Close(save: false);
        });
        legacyBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(380f, 0f);
        legacyBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(220f, 36f);
        legacyBtn.GetComponent<Image>().color = new Color(0.55f, 0.35f, 0.15f, 1f);

        string fp = string.IsNullOrEmpty(_draft.deviceFingerprint) ? "(sin huella)" : _draft.deviceFingerprint;
        var fpTxt = CreateText(parent, $"Variant: {_draft.variant} · Fingerprint: {fp}", 13f, FontStyles.Italic, new Color(0.7f, 0.7f, 0.75f), 320f);
        fpTxt.alignment = TextAlignmentOptions.Center;

        _headerCalibratedAt = CreateText(parent, FormatCalibratedAt(), 13f, FontStyles.Italic, new Color(0.7f, 0.7f, 0.75f), 295f);
        _headerCalibratedAt.alignment = TextAlignmentOptions.Center;
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

    // ─── Control rows ───────────────────────────────────────────────

    private void BuildControlRows(Transform parent)
    {
        // Tabs (botones de paginación) en posición superior, debajo del header
        BuildPageTabs(parent);

        // Cada página ocupa el mismo espacio Y (200 a -300), con espaciado generoso entre rows.
        const float ROW_START_Y = 150f;
        const float ROW_SPACING = 55f;

        if (_currentPage == 0)
        {
            CreateSectionHeader(parent, "CONDUCCIÓN", ROW_START_Y + 30f);
            BuildRow(parent, ROW_START_Y,                  "Volante",    "steer", () => _draft.axes.steer.path,  () => true,        () => DetectSteer());
            BuildRow(parent, ROW_START_Y - ROW_SPACING,    "Acelerador", "gas",   () => _draft.axes.gas.path,    () => true,        () => DetectPedalAxis("gas"));
            BuildRow(parent, ROW_START_Y - ROW_SPACING*2,  "Freno",      "brake", () => _draft.axes.brake.path,  () => true,        () => DetectPedalAxis("brake"));
            BuildRow(parent, ROW_START_Y - ROW_SPACING*3,  "Clutch",     "clutch",() => _draft.axes.clutch.path, () => _manualMode, () => DetectPedalAxis("clutch"));
        }
        else if (_currentPage == 1)
        {
            CreateSectionHeader(parent, "LUCES Y SEÑALES", ROW_START_Y + 30f);
            BuildRow(parent, ROW_START_Y,                  "Claxon",        "horn",      () => _draft.buttons.horn.path,      () => true, () => DetectButton("horn"));
            BuildRow(parent, ROW_START_Y - ROW_SPACING,    "Intermitentes", "hazards",   () => _draft.buttons.hazards.path,   () => true, () => DetectButton("hazards"));
            BuildRow(parent, ROW_START_Y - ROW_SPACING*2,  "Flecha izq",    "turnLeft",  () => _draft.buttons.turnLeft.path,  () => true, () => DetectButton("turnLeft"));
            BuildRow(parent, ROW_START_Y - ROW_SPACING*3,  "Flecha der",    "turnRight", () => _draft.buttons.turnRight.path, () => true, () => DetectButton("turnRight"));

            CreateSectionHeader(parent, "REVERSA", ROW_START_Y - ROW_SPACING*4);
            BuildRow(parent, ROW_START_Y - ROW_SPACING*5,  "Reversa", "reverse", () => _draft.buttons.reverse.path + (" (" + _draft.buttons.reverse.kind + ")"), () => true, () => DetectButton("reverse"));
        }
        else if (_currentPage == 2)
        {
            CreateSectionHeader(parent, "MARCHAS MANUAL", ROW_START_Y + 30f);
            BuildRow(parent, ROW_START_Y,                  "1ª", "gear1", () => _draft.buttons.gear1.path, () => _manualMode, () => DetectButton("gear1"));
            BuildRow(parent, ROW_START_Y - ROW_SPACING,    "2ª", "gear2", () => _draft.buttons.gear2.path, () => _manualMode, () => DetectButton("gear2"));
            BuildRow(parent, ROW_START_Y - ROW_SPACING*2,  "3ª", "gear3", () => _draft.buttons.gear3.path, () => _manualMode, () => DetectButton("gear3"));
            BuildRow(parent, ROW_START_Y - ROW_SPACING*3,  "4ª", "gear4", () => _draft.buttons.gear4.path, () => _manualMode, () => DetectButton("gear4"));
            BuildRow(parent, ROW_START_Y - ROW_SPACING*4,  "5ª", "gear5", () => _draft.buttons.gear5.path, () => _manualMode, () => DetectButton("gear5"));
            BuildRow(parent, ROW_START_Y - ROW_SPACING*5,  "6ª", "gear6", () => _draft.buttons.gear6.path, () => _manualMode, () => DetectButton("gear6"));
        }
    }

    private void BuildPageTabs(Transform parent)
    {
        // Tabs centrados horizontalmente justo debajo del header.
        // y=240 deja gap claro con la section header de la página (que está en y=180).
        const float TABS_Y = 240f;
        string[] tabLabels = { "Conducción", "Luces y Reversa", "Marchas (Manual)" };
        float[] tabXOffsets = { -260f, 0f, 260f };
        for (int i = 0; i < tabLabels.Length; i++)
        {
            int pageIdx = i; // capture para closure
            var btn = CreateButton(parent, tabLabels[i], 13f, () => SwitchPage(pageIdx));
            var rt = btn.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(220f, 34f);
            rt.anchoredPosition = new Vector2(tabXOffsets[i], TABS_Y);
            // Highlight current tab
            var img = btn.GetComponent<Image>();
            img.color = (i == _currentPage)
                ? new Color(0.35f, 0.55f, 0.85f, 1f)   // activo
                : new Color(0.18f, 0.22f, 0.32f, 1f); // inactivo
        }
    }

    private void SwitchPage(int page)
    {
        if (page < 0 || page > 2) return;
        // Bloquear cambio de página mid-capture para no dejar coroutine fantasma
        // corriendo cuando el UI ya no muestra la fila relevante.
        if (_capturing)
        {
            Debug.LogWarning("[G923CalibrationPanel] SwitchPage ignorado: termina la detección primero (o presiona Esc para cancelar).");
            return;
        }
        _currentPage = page;
        if (_panelRoot != null) Destroy(_panelRoot);
        BuildUI();
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

    // captureKey identifica internamente la fila (e.g., "horn", "brake").
    // Permite que el botón cambie de "Detectar" → "Detectando..." (active) →
    // "✓ Guardado" (just saved) → "Esperando..." (otra row active).
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
        var valTxt = CreateText(row.transform, string.IsNullOrEmpty(val) ? "(vacío)" : val, 16f, FontStyles.Italic, RowStateColor(required(), val), 0f);
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
            btnLabel = "✓ Guardado";
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
        bool empty = string.IsNullOrEmpty(value);
        if (!required) return new Color(0.45f, 0.45f, 0.45f);   // gris (no aplica)
        if (empty) return new Color(0.95f, 0.4f, 0.4f);          // rojo (faltante required)
        return new Color(0.45f, 0.85f, 0.4f);                    // verde (asignado)
    }

    // ─── DetectButton ───────────────────────────────────────────────

    private void DetectButton(string targetField)
    {
        if (_capturing)
        {
            Debug.LogWarning("[G923CalibrationPanel] Ya hay una captura en curso");
            return;
        }
        _capturing = true;
        _captureTargetField = targetField;
        // Rebuildar para mostrar "Detectando..." en esta fila inmediatamente.
        if (_panelRoot != null) Destroy(_panelRoot);
        BuildUI();
        StartCoroutine(CoCaptureButton());
    }

    // Per-frame edge detect en el device G923. Mantiene `prevPressed`
    // actualizado cada frame; captura un path solo cuando transiciona OFF→ON.
    // Botones inicialmente held se marcan como prev=true para ignorarlos,
    // pero SI el operator los suelta y vuelve a presionar, se capturan
    // correctamente (a diferencia del snapshot fijo anterior).
    private System.Collections.IEnumerator CoCaptureButton()
    {
        Debug.Log($"[G923CalibrationPanel] Capturing button for '{_captureTargetField}' (per-frame edge detect)");
        float timeout = Time.unscaledTime + 10f;
        var prevPressed = new System.Collections.Generic.HashSet<string>();
        // Seed inicial: paths actualmente pressed se ignoran este frame, pero al
        // actualizar prevPressed cada iteración, un release+repress sí dispara.
        ScanPressed(prevPressed);

        while (Time.unscaledTime < timeout && _capturing)
        {
            var nowPressed = new System.Collections.Generic.HashSet<string>();
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
            Debug.LogWarning($"[G923CalibrationPanel] Captura timeout para '{_captureTargetField}' (10s sin transición)");
            _capturing = false;
            if (_panelRoot != null) Destroy(_panelRoot);
            BuildUI();
        }
    }

    private static void ScanPressed(System.Collections.Generic.HashSet<string> output)
    {
        foreach (var dev in InputSystem.devices)
        {
            if (!Gley.UrbanSystem.UIInputNew.IsLogitechG923Family(dev)) continue;
            foreach (var c in dev.allControls)
            {
                if (c is UnityEngine.InputSystem.Controls.ButtonControl bc && bc.isPressed)
                    output.Add(c.name);
            }
        }
    }

    private void ApplyCapturedButton(string path)
    {
        Debug.Log($"[G923CalibrationPanel] Captured '{_captureTargetField}' = {path}");
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
        // Marca "✓ Guardado" en esta fila durante SAVED_FEEDBACK_SECONDS.
        _lastSavedKey = _captureTargetField;
        _lastSavedAt = Time.unscaledTime;
        if (_panelRoot != null) Destroy(_panelRoot);
        BuildUI();
    }

    // ─── DetectPedalAxis ────────────────────────────────────────────

    // G923 gas usa un eje regular (z en PS, stick/y en Xbox). NO usa
    // HoriThrottleReader — esa abstracción solo aplica al HORI HPC-044U.
    // Sampling 3s y detecta el axis con mayor delta vs baseline.
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
        Debug.Log($"[G923CalibrationPanel] Capturing pedal axis for '{which}' — sample 3s");
        InputDevice wheel = FindG923Device();
        if (wheel == null) { Debug.LogWarning("[G923CalibrationPanel] G923 not found"); _capturing = false; if (_panelRoot != null) Destroy(_panelRoot); BuildUI(); yield break; }

        // Candidatos para pedales G923: PS usa z/rz/stick-y, Xbox rota los mismos
        // ejes. Probar los 3 y elegir el de mayor delta.
        var initial = new Dictionary<string, float>();
        var maxAbs = new Dictionary<string, float>();
        string[] candidates = { "z", "rz", "stick/y" };
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
            Debug.LogWarning("[G923CalibrationPanel] Ningún pedal axis superó threshold 0.6 en 3s");
            _capturing = false;
            if (_panelRoot != null) Destroy(_panelRoot);
            BuildUI();
            yield break;
        }

        // G923 pedales: rest/press son hardware-fijos por axis name, NO inferir
        // del baseline (bug v1.8.0: si InputSystem no había polled el axis al
        // momento del Detectar, ReadUnprocessedValue retornaba 0, y la inferencia
        // `restVal >= 0 ? -1 : 1` daba press=-1 con restVal=0 → NormalizePedal
        // dividía por -1 y reportaba valores incorrectos; gas no respondía).
        // Hardware reality:
        //   z, rz       → idle=+1, fullPress=-1  (rest=1, press=-1)
        //   stick/y     → idle=-1, fullPress=+1  (rest=-1, press=1)
        float restVal, pressVal;
        switch (chosen)
        {
            case "z":
            case "rz":
                restVal = 1f; pressVal = -1f;
                break;
            case "stick/y":
                restVal = -1f; pressVal = 1f;
                break;
            default:
                // Fallback defensivo si en el futuro otro axis se agrega al
                // candidate list: inferir por baseline (legacy behavior).
                restVal = initial[chosen];
                pressVal = restVal >= 0f ? -1f : 1f;
                Debug.LogWarning($"[G923CalibrationPanel] Axis '{chosen}' no canónico — fallback a baseline inference rest={restVal:F2} press={pressVal:F2}");
                break;
        }

        Debug.Log($"[G923CalibrationPanel] Captured pedal '{which}' = {chosen} (delta {chosenDelta:F2}, rest={restVal:F2}, press={pressVal:F2})");
        if (which == "gas") { _draft.axes.gas.path = chosen; _draft.axes.gas.rest = restVal; _draft.axes.gas.press = pressVal; }
        else if (which == "brake") { _draft.axes.brake.path = chosen; _draft.axes.brake.rest = restVal; _draft.axes.brake.press = pressVal; }
        else if (which == "clutch") { _draft.axes.clutch.path = chosen; _draft.axes.clutch.rest = restVal; _draft.axes.clutch.press = pressVal; }

        _hasChanges = true;
        _lastSavedKey = which; _lastSavedAt = Time.unscaledTime;
        _capturing = false;
        if (_panelRoot != null) Destroy(_panelRoot);
        BuildUI();
    }

    // ─── DetectSteer ────────────────────────────────────────────────

    private void DetectSteer()
    {
        if (_capturing) return;
        _capturing = true;
        _captureTargetField = "steer";
        if (_panelRoot != null) Destroy(_panelRoot);
        BuildUI();
        StartCoroutine(CoCaptureSteer());
    }

    private System.Collections.IEnumerator CoCaptureSteer()
    {
        InputDevice wheel = FindG923Device();
        if (wheel == null) { _capturing = false; if (_panelRoot != null) Destroy(_panelRoot); BuildUI(); yield break; }
        var ctrl = wheel.TryGetChildControl("stick/x") as UnityEngine.InputSystem.Controls.AxisControl;
        if (ctrl == null) { _capturing = false; if (_panelRoot != null) Destroy(_panelRoot); BuildUI(); yield break; }

        Debug.Log("[G923CalibrationPanel] Steer: gira IZQ a tope (3s)");
        yield return new WaitForSecondsRealtime(3f);
        float left = ctrl.ReadUnprocessedValue();

        Debug.Log("[G923CalibrationPanel] Steer: gira DER a tope (3s)");
        yield return new WaitForSecondsRealtime(3f);
        float right = ctrl.ReadUnprocessedValue();

        Debug.Log("[G923CalibrationPanel] Steer: centra (2s)");
        yield return new WaitForSecondsRealtime(2f);
        float center = ctrl.ReadUnprocessedValue();

        _draft.axes.steer.path = "stick/x";
        _draft.axes.steer.leftMax = left;
        _draft.axes.steer.rightMax = right;
        _draft.axes.steer.center = center;
        _hasChanges = true;
        _lastSavedKey = "steer"; _lastSavedAt = Time.unscaledTime;
        Debug.Log($"[G923CalibrationPanel] Steer captured: left={left:F2} right={right:F2} center={center:F2}");

        _capturing = false;
        if (_panelRoot != null) Destroy(_panelRoot);
        BuildUI();
    }

    // ─── Footer + RestoreCanonical ──────────────────────────────────

    private void BuildFooter(Transform parent)
    {
        var saveBtn = CreateButton(parent, "Guardar y salir", 13f, () => Close(save: true));
        saveBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(-180f, -440f);
        saveBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(180f, 36f);

        var resetBtn = CreateButton(parent, "Restaurar defaults canónicos", 12f, () => RestoreCanonicalDefaults());
        resetBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(80f, -440f);
        resetBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(260f, 36f);

        var hint = CreateText(parent, "Esc cancela sin guardar · F8 sostén cierra con guardar", 11f, FontStyles.Italic, new Color(0.6f, 0.6f, 0.7f), -470f);
        hint.alignment = TextAlignmentOptions.Center;
    }

    private void RestoreCanonicalDefaults()
    {
        // Mapping G923 canónico por variant. Ver CLAUDE.md sección
        // "Logitech G923 / Mapping por variante (FIX#26 ext. v1.5.7)".
        // Variant detectado por displayName.Contains("Xbox") en Open().
        string fp = _draft.deviceFingerprint;
        string variant = _draft.variant;
        bool isXbox = (variant == "Xbox");
        _draft = new G923Mapping
        {
            schemaVersion = 1,
            variant = variant,
            deviceFingerprint = fp,
            calibratedBy = "F8-panel",
        };

        // Steer (mismo eje en ambas variantes)
        _draft.axes.steer.path = "stick/x";
        _draft.axes.steer.center = 0f;
        _draft.axes.steer.leftMax = -0.95f;
        _draft.axes.steer.rightMax = 0.95f;

        // Pedales (ROTADOS entre variants — ver tabla CLAUDE.md)
        if (isXbox)
        {
            // Xbox: gas=stick/y, brake=z, clutch=rz
            _draft.axes.gas.path    = "stick/y"; _draft.axes.gas.rest    = -1f; _draft.axes.gas.press    = 1f;
            _draft.axes.brake.path  = "z";       _draft.axes.brake.rest  = 1f;  _draft.axes.brake.press  = -1f;
            _draft.axes.clutch.path = "rz";      _draft.axes.clutch.rest = 1f;  _draft.axes.clutch.press = -1f;
        }
        else
        {
            // PS: gas=z, brake=rz, clutch=stick/y
            _draft.axes.gas.path    = "z";       _draft.axes.gas.rest    = 1f;  _draft.axes.gas.press    = -1f;
            _draft.axes.brake.path  = "rz";      _draft.axes.brake.rest  = 1f;  _draft.axes.brake.press  = -1f;
            _draft.axes.clutch.path = "stick/y"; _draft.axes.clutch.rest = -1f; _draft.axes.clutch.press = 1f;
        }
        _draft.axes.brake.required = true;
        _draft.axes.clutch.required = _manualMode;

        // Reversa: PS=button19, Xbox=button12
        _draft.buttons.reverse.path = isXbox ? "button12" : "button19";
        _draft.buttons.reverse.required = true;
        _draft.buttons.reverse.kind = "hold";

        // Botones de luces/señales: dejar vacíos para que el operador los
        // capture vía F8 (G923 no tiene asignaciones hardware-fijas equivalentes
        // a HORI; la mapping de combos L1+R1, paddles, etc. es operacional).
        _draft.buttons.horn.required = true;
        _draft.buttons.hazards.required = true;
        _draft.buttons.turnLeft.required = true;
        _draft.buttons.turnRight.required = true;

        // Marchas H-shifter (vacías por default — G923 sin shifter externo usa
        // paddles, la asignación queda a discreción del operador).
        _draft.buttons.gear1.required = _manualMode;
        _draft.buttons.gear2.required = _manualMode;
        _draft.buttons.gear3.required = _manualMode;
        _draft.buttons.gear4.required = _manualMode;
        _draft.buttons.gear5.required = _manualMode;
        _draft.buttons.gear6.required = _manualMode;

        _hasChanges = true;
        if (_panelRoot != null) Destroy(_panelRoot);
        BuildUI();
    }

    // ─── Helpers ────────────────────────────────────────────────────

    private InputDevice FindG923Device()
    {
        foreach (var dev in InputSystem.devices)
            if (Gley.UrbanSystem.UIInputNew.IsLogitechG923Family(dev)) return dev;
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
        // bloquea clicks a los buttons que lo contienen (label text dentro del button).
        t.raycastTarget = false;
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(800f, 30f);
        rt.anchoredPosition = new Vector2(0f, y);
        return t;
    }

    private Transform CreateRow(Transform parent, float y, float height)
    {
        var go = new GameObject("Row", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
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
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(100f, 30f);
        var txt = CreateText(go.transform, label, fontSize, FontStyles.Normal, Color.white, 0f);
        txt.alignment = TextAlignmentOptions.Center;
        var btn = go.GetComponent<Button>();
        btn.onClick.AddListener(() => onClick?.Invoke());
        return btn;
    }
}
