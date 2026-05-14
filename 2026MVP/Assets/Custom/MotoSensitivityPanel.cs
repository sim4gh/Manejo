using System.Collections.Generic;
using TlaxSim.MotoSensitivity;
using UnityEngine;
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
        // Esc cierra si está abierto.
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            ClosePanel(saveChanges: false);
            return;
        }

        // F11 hold 1.5s para abrir.
        if (!IsOpen && Input.GetKey(KeyCode.F11))
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

    void OnGUI()
    {
        if (!IsOpen) return;

        // Layout básico — Task 10 + 11 + 12 + 13 lo extienden.
        float w = 700, h = 200;
        float x = (Screen.width - w) / 2;
        float y = (Screen.height - h) / 2;
        GUI.Box(new Rect(x, y, w, h), "Sensibilidad de Moto");

        // Banner si kill-switch activo.
        if (MotoSensitivityProvider.Instance != null && MotoSensitivityProvider.Instance.IsKillSwitchOn)
        {
            GUI.color = Color.yellow;
            GUI.Label(new Rect(x + 10, y + 30, w - 20, 30),
                "Sistema deshabilitado por administrador — los cambios se guardarán pero no aplicarán hasta reactivar.");
            GUI.color = Color.white;
        }

        // Placeholder para Task 10 (preset selector).
        GUI.Label(new Rect(x + 10, y + 80, w - 20, 30), $"Preset activo: {_selectedPreset}");

        // Footer (placeholder; Task 13 implementa save/reset).
        if (GUI.Button(new Rect(x + w - 220, y + h - 40, 100, 30), "Cancelar"))
            ClosePanel(saveChanges: false);
        if (GUI.Button(new Rect(x + w - 110, y + h - 40, 100, 30), "Aplicar"))
            ClosePanel(saveChanges: true);
    }

    static MotoSensitivity DeepCopy(MotoSensitivity src)
    {
        // JsonUtility roundtrip = deep copy gratuito.
        return JsonUtility.FromJson<MotoSensitivity>(JsonUtility.ToJson(src));
    }
}
