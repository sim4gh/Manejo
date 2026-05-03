using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using ShadedTechnology.WindshieldRainAsset.Demo;

/// <summary>
/// Maneja los wipers en escenas de manejo (Sedan, Camioneta, Ambulancia,
/// CamionDCarga). Tres responsabilidades:
///
/// 1) Auto-on cuando hay precipitación: al cargar escena, lee
///    <c>PlayerPrefs.GetInt("Clima")</c> (0=Sol, 1=Lluvia, 2=Granizo) y llama
///    <see cref="ControllWipers.SetMode"/> con modo 2 (latcheado medio) o 0.
///
/// 2) Anula el binding <c>&lt;Keyboard&gt;/e</c> heredado del demo del asset
///    WindshieldRainAsset (commit 486906e4 cabló un PlayerInput hacia el
///    inputactions del demo). Sin esto, pulsar E activa wiper Y direccional
///    derecha (PlayerCar.cs:286 lee eKey directo del Keyboard.current).
///
/// 3) Polling de HORI HPC-044U: button42 → wipers OFF, button43 → ON latcheado.
///    Acceso directo al device (no path strings) porque el HORI puede
///    registrarse como Joystick en vez de HID y un binding genérico fallaría.
///
/// Bootstrap singleton (AfterSceneLoad) — no requiere setup en escena.
/// Excluye MainMenu y Motocicleta.
/// </summary>
public class WiperAutoController : MonoBehaviour
{
    public static WiperAutoController Instance { get; private set; }

    private const int RAIN_MODE = 2;
    private const int OFF_MODE = 0;
    private const int HORI_BUTTON_OFF = 42;
    private const int HORI_BUTTON_ON = 43;

    private ControllWipers _cachedWipers;
    private InputDevice _horiWheel;
    private ButtonControl _btnOff;
    private ButtonControl _btnOn;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("[WiperAutoController]");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<WiperAutoController>();

        var active = SceneManager.GetActiveScene();
        if (active.IsValid()) Instance.HandleScene(active);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => HandleScene(scene);

    private void HandleScene(Scene scene)
    {
        _cachedWipers = null;
        if (scene.name == "MainMenu" || scene.name == "Motocicleta") return;
        StartCoroutine(ApplyAutoMode());
    }

    private IEnumerator ApplyAutoMode()
    {
        // 1-frame retry: ControllWipers a veces se instancia tarde dentro de
        // jerarquías de prefab del player.
        var wipers = FindFirstObjectByType<ControllWipers>();
        if (wipers == null) { yield return null; wipers = FindFirstObjectByType<ControllWipers>(); }
        if (wipers == null)
        {
            Debug.Log("[WiperAutoController] No hay ControllWipers en la escena — skip.");
            yield break;
        }

        _cachedWipers = wipers;

        int clima = PlayerPrefs.GetInt("Clima", 0);
        int targetMode = (clima == 1 || clima == 2) ? RAIN_MODE : OFF_MODE;
        wipers.SetMode(targetMode);
        Debug.Log($"[WiperAutoController] Auto-mode: Clima={clima} → wiper mode {targetMode}");

        OverrideWipeKeyboardBinding();
    }

    private void OverrideWipeKeyboardBinding()
    {
        var inputs = FindObjectsByType<PlayerInput>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var pi in inputs)
        {
            if (pi == null || pi.actions == null) continue;
            var wipeAction = pi.actions.FindAction("Wipe", throwIfNotFound: false);
            if (wipeAction == null) continue;
            var bindings = wipeAction.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                if (bindings[i].path == "<Keyboard>/e")
                {
                    wipeAction.ApplyBindingOverride(i, "");
                    Debug.Log($"[WiperAutoController] Binding <Keyboard>/e en acción Wipe anulado en {pi.name}.");
                }
            }
        }
    }

    private void Update()
    {
        EnsureHoriRefs();

        if (_btnOff != null && _btnOff.wasPressedThisFrame)
        {
            GetWipers()?.SetMode(OFF_MODE);
        }
        if (_btnOn != null && _btnOn.wasPressedThisFrame)
        {
            GetWipers()?.SetMode(RAIN_MODE);
        }
    }

    private ControllWipers GetWipers()
    {
        if (_cachedWipers != null) return _cachedWipers;
        _cachedWipers = FindFirstObjectByType<ControllWipers>();
        return _cachedWipers;
    }

    private void EnsureHoriRefs()
    {
        if (_horiWheel != null && _horiWheel.added && _btnOff != null && _btnOn != null) return;

        _horiWheel = null;
        _btnOff = null;
        _btnOn = null;

        foreach (var device in InputSystem.devices)
        {
            if (device == null || !device.added) continue;
            string name = (device.displayName ?? "").ToUpperInvariant();
            if (!name.Contains("HORI")) continue;
            if (name.Contains("SHIFTER")) continue;

            _horiWheel = device;
            _btnOff = device.TryGetChildControl<ButtonControl>("button" + HORI_BUTTON_OFF);
            _btnOn = device.TryGetChildControl<ButtonControl>("button" + HORI_BUTTON_ON);
            break;
        }
    }
}
