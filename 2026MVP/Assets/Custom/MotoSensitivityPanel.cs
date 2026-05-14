using System.Collections.Generic;
using TlaxSim.MotoSensitivity;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Panel F11 (hold 1.5s) para tunear la sensibilidad del Moto Simulator.
// Solo se abre en escena "Motocicleta". Mutuamente exclusivo con F7/F8/F9/F10.
//
// Ver docs/superpowers/specs/2026-05-13-moto-sensitivity-design.md sección 6.
public class MotoSensitivityPanel : MonoBehaviour
{
    const float HOLD_SECONDS = 1.5f;
    const string TARGET_SCENE = "Motocicleta";

    public static MotoSensitivityPanel Instance { get; private set; }
    public static bool IsOpen { get; private set; }

    float _holdStart = -1f;
    float _previousTimeScale = 1f;
    MotoSensitivity _editing;  // copia editable, no aplica hasta Aplicar y cerrar
    string _selectedPreset;

    Gley.UrbanSystem.UIInputNew _cachedInput;

    Gley.UrbanSystem.UIInputNew GetInput()
    {
        if (_cachedInput == null)
            _cachedInput = UnityEngine.Object.FindObjectOfType<Gley.UrbanSystem.UIInputNew>();
        return _cachedInput;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("[MotoSensitivityPanel]");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<MotoSensitivityPanel>();
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // Esc cierra si está abierto.
        if (IsOpen && kb.escapeKey.wasPressedThisFrame)
        {
            ClosePanel(saveChanges: false);
            return;
        }

        // F11 hold 1.5s para abrir.
        if (!IsOpen && kb.f11Key.isPressed)
        {
            if (SceneManager.GetActiveScene().name != TARGET_SCENE)
            {
                if (_holdStart < 0f)
                {
                    Debug.Log("[MotoSensitivityPanel] F11 ignorado: no estás en escena Motocicleta.");
                    _holdStart = Time.unscaledTime;  // suppress log spam usando esto como flag
                }
                return;
            }

            if (_holdStart < 0f) _holdStart = Time.unscaledTime;
            if (Time.unscaledTime - _holdStart >= HOLD_SECONDS)
            {
                OpenPanel();
                _holdStart = -1f;
            }
        }
        else if (!IsOpen)
        {
            _holdStart = -1f;
        }
    }

    void OpenPanel()
    {
        // Mutex con F7-F10. Buscar las clases por nombre en TODOS los assemblies
        // cargados (Unity las puede tener en assemblies con namespace o sin).
        // `System.Type.GetType("X")` solo funciona para tipos en el assembly del
        // caller, así que iteramos AppDomain.
        string[] otherPanels = { "LogConsolePanel", "BindingsPanel", "AdvancedInputPanel", "AdminPanel" };
        foreach (var name in otherPanels)
        {
            System.Type t = null;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var candidate in asm.GetTypes())
                {
                    if (candidate.Name == name || candidate.FullName == name)
                    {
                        t = candidate;
                        break;
                    }
                }
                if (t != null) break;
            }
            if (t == null) continue;

            var prop = t.GetProperty("IsOpen",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (prop != null && prop.PropertyType == typeof(bool))
            {
                bool open = (bool)prop.GetValue(null);
                if (open)
                {
                    Debug.LogWarning($"[MotoSensitivityPanel] F11 ignorado: {t.FullName} ya está abierto.");
                    return;
                }
            }
        }

        IsOpen = true;
        _previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        // Cargar snapshot editable del provider actual.
        var prov = MotoSensitivityProvider.Instance;
        if (prov == null || !prov.IsLoaded)
        {
            // Inicializar con defaults si no hay provider cargado.
            _editing = MotoSensitivityDefaults.NewWithRealistaActive();
        }
        else
        {
            _editing = DeepCopy(prov.Loaded);
        }
        _selectedPreset = _editing.activePreset;
        Debug.Log($"[MotoSensitivityPanel] Abierto. Preset actual: {_selectedPreset}");
    }

    void ClosePanel(bool saveChanges)
    {
        if (saveChanges)
        {
            _editing.activePreset = _selectedPreset;
            _editing.lastModifiedBy = "F11-panel";
            MotoSensitivityProvider.Instance?.Save(_editing);
            // El provider notifica a UIInputNew via OnReloaded (suscripción de Task 6).
            Debug.Log($"[MotoSensitivityPanel] Guardado. Preset: {_selectedPreset}");
        }
        IsOpen = false;
        Time.timeScale = _previousTimeScale;
    }

    Vector2 _scrollPos;

    void OnGUI()
    {
        if (!IsOpen) return;

        float w = 700, h = 600;
        float x = (Screen.width - w) / 2;
        float y = (Screen.height - h) / 2;
        GUI.Box(new Rect(x, y, w, h), "Sensibilidad de Moto");

        // Banner kill-switch.
        float cursorY = y + 30;
        if (MotoSensitivityProvider.Instance != null && MotoSensitivityProvider.Instance.IsKillSwitchOn)
        {
            GUI.color = Color.yellow;
            GUI.Label(new Rect(x + 10, cursorY, w - 20, 30),
                "Sistema deshabilitado por administrador — los cambios se guardarán pero no aplicarán hasta reactivar.");
            GUI.color = Color.white;
            cursorY += 30;
        }

        // Preset selector.
        GUI.Label(new Rect(x + 10, cursorY, 100, 25), "Preset:");
        string[] presetNames = { "Principiante", "Normal", "Realista", "Custom" };
        for (int i = 0; i < presetNames.Length; i++)
        {
            bool isActive = _selectedPreset == presetNames[i];
            bool clicked = GUI.Toggle(new Rect(x + 110 + i * 120, cursorY, 110, 25), isActive, presetNames[i]);
            if (clicked && !isActive)
            {
                ApplyPresetToEditing(presetNames[i]);
            }
        }
        cursorY += 30;

        // Scroll area para los sliders por canal.
        Rect scrollViewRect = new Rect(x + 10, cursorY, w - 20, h - (cursorY - y) - 50);
        Rect contentRect = new Rect(0, 0, w - 40, 900);
        _scrollPos = GUI.BeginScrollView(scrollViewRect, _scrollPos, contentRect);

        float innerY = 0;
        var preset = GetEditingPreset();

        innerY = DrawAxisSection("Inclinación (lean)", preset.lean, ref preset.lean, innerY);
        innerY = DrawAxisSection("Manubrio (hbar)", preset.hbar, ref preset.hbar, innerY);
        innerY = DrawPedalSection("Acelerador (gas)", preset.gas, ref preset.gas, innerY);
        innerY = DrawScaleOnlySection("Freno", preset.brake, ref preset.brake, innerY);
        innerY = DrawScaleOnlySection("Clutch", preset.clutch, ref preset.clutch, innerY);
        innerY = DrawBlendSection(preset, ref preset, innerY);

        GUI.EndScrollView();

        // Detección de edición → auto-Custom. UNA sola llamada por frame, después
        // de que TODAS las secciones mutaron `preset`. Llamarlo por-sección causaba
        // snapshot/restore mid-frame inconsistente (codex review T10).
        OnEditingChanged();

        // Footer.
        if (GUI.Button(new Rect(x + 10, y + h - 40, 200, 30), "Restaurar default del preset"))
            ApplyPresetToEditing(_selectedPreset);
        if (GUI.Button(new Rect(x + w - 320, y + h - 40, 100, 30), "Cancelar"))
            ClosePanel(saveChanges: false);
        if (GUI.Button(new Rect(x + w - 210, y + h - 40, 100, 30), "Guardar Custom"))
        {
            _editing.custom = DeepCopyPreset(GetEditingPreset());
            _selectedPreset = "Custom";
        }
        if (GUI.Button(new Rect(x + w - 100, y + h - 40, 90, 30), "Aplicar"))
            ClosePanel(saveChanges: true);
    }

    MotoPreset GetEditingPreset()
    {
        switch (_selectedPreset)
        {
            case "Principiante": return _editing.presets.Principiante;
            case "Normal":       return _editing.presets.Normal;
            case "Realista":     return _editing.presets.Realista;
            case "Custom":       return _editing.custom ?? (_editing.custom = MotoSensitivityDefaults.Normal());
            default:             return _editing.presets.Normal;
        }
    }

    void ApplyPresetToEditing(string presetName)
    {
        _selectedPreset = presetName;
        switch (presetName)
        {
            case "Principiante": _editing.presets.Principiante = MotoSensitivityDefaults.Principiante(); break;
            case "Normal":       _editing.presets.Normal       = MotoSensitivityDefaults.Normal();       break;
            case "Realista":     _editing.presets.Realista     = MotoSensitivityDefaults.Realista();     break;
            case "Custom":       _editing.custom               = _editing.custom ?? MotoSensitivityDefaults.Normal(); break;
        }
    }

    float DrawAxisSection(string title, AxisSensitivity src, ref AxisSensitivity dst, float startY)
    {
        GUI.Label(new Rect(10, startY, 660, 25), title);
        startY += 25;
        dst.deadzone   = DrawSlider("Deadzone",   dst.deadzone,   0f, 0.3f, startY); startY += 25;
        dst.curveType  = DrawCurveTypeDropdown(dst.curveType, startY); startY += 25;
        dst.curveParam = DrawSlider("Curva (n)",  dst.curveParam, 0.5f, 3f,  startY); startY += 25;
        dst.scale      = DrawSlider("Escala máx", dst.scale,      0.3f, 1f,  startY); startY += 30;
        var input = GetInput();
        if (input != null)
        {
            float live = title.StartsWith("Inclinación") ? input.MotoLeanProcessed : input.MotoHbarProcessed;
            GUI.Label(new Rect(10, startY, 660, 20), $"Live procesado: {live:F3}");
            startY += 20;
        }
        DrawCurvePreview(dst, new Rect(10, startY, 200, 80));
        startY += 90;
        return startY + 10;
    }

    float DrawPedalSection(string title, PedalSensitivity src, ref PedalSensitivity dst, float startY)
    {
        GUI.Label(new Rect(10, startY, 660, 25), title);
        startY += 25;
        dst.deadzone     = DrawSlider("Deadzone",       dst.deadzone,     0f, 0.2f, startY); startY += 25;
        dst.curveType    = DrawCurveTypeDropdown(dst.curveType, startY); startY += 25;
        dst.curveParam   = DrawSlider("Curva (n)",      dst.curveParam,   0.5f, 3f,    startY); startY += 25;
        dst.rampUpPerSec = DrawSlider("Ramp (1/s)",     dst.rampUpPerSec, 0.5f, 10f,   startY); startY += 25;
        dst.scale        = DrawSlider("Escala máx",     dst.scale,        0.3f, 1f,    startY); startY += 30;
        var input = GetInput();
        if (input != null)
        {
            GUI.Label(new Rect(10, startY, 660, 20), $"Live procesado: {input.MotoGasProcessed:F3}");
            startY += 20;
        }
        return startY + 10;
    }

    float DrawScaleOnlySection(string title, ScaleOnly src, ref ScaleOnly dst, float startY)
    {
        GUI.Label(new Rect(10, startY, 200, 25), title);
        dst.scale = DrawSlider("Escala", dst.scale, 0.3f, 1f, startY + 25);
        return startY + 65;
    }

    float DrawBlendSection(MotoPreset src, ref MotoPreset dst, float startY)
    {
        GUI.Label(new Rect(10, startY, 660, 25), "Mezcla por velocidad");
        startY += 25;
        dst.blendStartKmh       = DrawSlider("Inicio (km/h)", dst.blendStartKmh, 10f, 80f, startY); startY += 25;
        dst.blendEndKmh         = DrawSlider("Fin (km/h)",    dst.blendEndKmh,   30f, 120f, startY); startY += 25;
        dst.highSpeedLeanWeight = DrawSlider("Peso lean alta vel", dst.highSpeedLeanWeight, 0f, 1f, startY); startY += 30;
        return startY + 10;
    }

    float DrawSlider(string label, float current, float min, float max, float y)
    {
        GUI.Label(new Rect(10, y, 140, 20), label);
        float v = GUI.HorizontalSlider(new Rect(160, y + 5, 400, 20), current, min, max);
        GUI.Label(new Rect(570, y, 90, 20), v.ToString("F3"));
        return v;
    }

    string DrawCurveTypeDropdown(string current, float y)
    {
        GUI.Label(new Rect(10, y, 140, 20), "Curva");
        bool isPow = current == "pow";
        bool clickedLinear = GUI.Toggle(new Rect(160, y, 100, 20), !isPow, "linear");
        bool clickedPow    = GUI.Toggle(new Rect(270, y, 100, 20),  isPow, "pow");
        if (clickedLinear && isPow) return "linear";
        if (clickedPow && !isPow) return "pow";
        return current;
    }

    void OnEditingChanged()
    {
        // Si el operador modifica algún slider mientras un preset nombrado está activo,
        // detectar diferencia respecto al preset hardcoded y saltar a Custom para
        // preservar la fuente de verdad. Esto evita silently mutar Principiante/Normal/Realista.
        if (_selectedPreset == "Custom") return;

        // Capturar cuál preset se estaba editando ANTES de cualquier mutación.
        // Bug previo: usábamos _editing.activePreset (que no se actualiza con el selector
        // de radio) → restauraba el preset incorrecto. Ahora usamos _selectedPreset
        // capturado en una variable local.
        string editingPreset = _selectedPreset;

        MotoPreset hardcoded;
        switch (editingPreset)
        {
            case "Principiante": hardcoded = MotoSensitivityDefaults.Principiante(); break;
            case "Normal":       hardcoded = MotoSensitivityDefaults.Normal();       break;
            case "Realista":     hardcoded = MotoSensitivityDefaults.Realista();     break;
            default:             return;
        }
        var current = GetEditingPreset();
        if (!PresetsEqual(current, hardcoded))
        {
            // Snapshot el current como custom.
            _editing.custom = DeepCopyPreset(current);
            // Restaurar el preset nombrado QUE SE ESTABA EDITANDO a su hardcoded.
            switch (editingPreset)
            {
                case "Principiante": _editing.presets.Principiante = MotoSensitivityDefaults.Principiante(); break;
                case "Normal":       _editing.presets.Normal       = MotoSensitivityDefaults.Normal();       break;
                case "Realista":     _editing.presets.Realista     = MotoSensitivityDefaults.Realista();     break;
            }
            _selectedPreset = "Custom";
        }
    }

    static bool PresetsEqual(MotoPreset a, MotoPreset b)
    {
        // Comparación field-by-field con epsilon (1e-4 — más conservador que UI step).
        // JSON-string-compare causaba false-switch a Custom por float serialization drift.
        const float EPS = 1e-4f;
        return AxisEqual(a.lean, b.lean, EPS)
            && AxisEqual(a.hbar, b.hbar, EPS)
            && PedalEqual(a.gas, b.gas, EPS)
            && Mathf.Abs(a.brake.scale - b.brake.scale) < EPS
            && Mathf.Abs(a.clutch.scale - b.clutch.scale) < EPS
            && Mathf.Abs(a.blendStartKmh - b.blendStartKmh) < EPS
            && Mathf.Abs(a.blendEndKmh - b.blendEndKmh) < EPS
            && Mathf.Abs(a.highSpeedLeanWeight - b.highSpeedLeanWeight) < EPS;
    }

    static bool AxisEqual(AxisSensitivity x, AxisSensitivity y, float eps)
    {
        return x.curveType == y.curveType
            && Mathf.Abs(x.deadzone - y.deadzone) < eps
            && Mathf.Abs(x.curveParam - y.curveParam) < eps
            && Mathf.Abs(x.scale - y.scale) < eps;
    }

    static bool PedalEqual(PedalSensitivity x, PedalSensitivity y, float eps)
    {
        return x.curveType == y.curveType
            && Mathf.Abs(x.deadzone - y.deadzone) < eps
            && Mathf.Abs(x.curveParam - y.curveParam) < eps
            && Mathf.Abs(x.scale - y.scale) < eps
            && Mathf.Abs(x.rampUpPerSec - y.rampUpPerSec) < eps;
    }

    static MotoPreset DeepCopyPreset(MotoPreset src)
    {
        return JsonUtility.FromJson<MotoPreset>(JsonUtility.ToJson(src));
    }

    static MotoSensitivity DeepCopy(MotoSensitivity src)
    {
        // JsonUtility roundtrip = deep copy gratuito.
        return JsonUtility.FromJson<MotoSensitivity>(JsonUtility.ToJson(src));
    }

    static Texture2D _curveTex;
    static Texture2D GetCurveTexture()
    {
        if (_curveTex == null)
        {
            _curveTex = new Texture2D(1, 1);
            _curveTex.SetPixel(0, 0, Color.white);
            _curveTex.Apply();
        }
        return _curveTex;
    }

    void DrawCurvePreview(AxisSensitivity cfg, Rect rect)
    {
        GUI.Box(rect, "");
        // Eje diagonal de referencia (gris).
        int samples = 32;
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / (samples - 1);
            float px = rect.x + t * rect.width;
            float py = rect.y + (1f - t) * rect.height;
            GUI.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            GUI.DrawTexture(new Rect(px, py, 2, 2), GetCurveTexture());
        }
        // Curva resultante (cyan).
        GUI.color = Color.cyan;
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / (samples - 1);  // 0..1
            float xNorm = t;
            float y = MotoSensitivityCurves.ApplyAxis(xNorm, cfg);
            float px = rect.x + t * rect.width;
            float py = rect.y + (1f - y) * rect.height;
            GUI.DrawTexture(new Rect(px, py, 2, 2), GetCurveTexture());
        }
        GUI.color = Color.white;
    }
}
