using UnityEngine;
using UnityEngine.SceneManagement;

// Claxón hold-to-honk. Lee Bind_horn vía UIInputNew.IsHornPressed() y reproduce
// el clip correspondiente al vehículo de la escena actual mientras el botón
// esté presionado. Default HORI Truck = wheel:button7 (set en
// UIInputNew.AttachToWheelDevice).
public class HornController : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoCreate()
    {
        if (FindObjectOfType<HornController>() != null) return;
        var go = new GameObject("[HornController]");
        go.AddComponent<HornController>();
        DontDestroyOnLoad(go);
    }

    private const string CAR_CLIP_PATH = "Audio/Horn/car-claxon";
    private const string BUS_CLIP_PATH = "Audio/Horn/bus-claxon";
    private const float VOLUME = 0.85f;

    private AudioClip _carClip;
    private AudioClip _busClip;
    private AudioSource _audio;
    private Gley.UrbanSystem.UIInputNew _input;
    private float _inputResolveTimer;
    private bool _enabledForScene;
    private bool _wasPressed;

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        _carClip = Resources.Load<AudioClip>(CAR_CLIP_PATH);
        _busClip = Resources.Load<AudioClip>(BUS_CLIP_PATH);
        if (_carClip == null) Debug.LogWarning($"[HornController] Clip no encontrado: {CAR_CLIP_PATH}");
        if (_busClip == null) Debug.LogWarning($"[HornController] Clip no encontrado: {BUS_CLIP_PATH}");

        _audio = gameObject.AddComponent<AudioSource>();
        _audio.loop = true;
        _audio.spatialBlend = 0f;
        _audio.playOnAwake = false;
        _audio.volume = VOLUME;

        // Bootstrap inicial — sceneLoaded no dispara para la escena ya activa.
        ApplyScene(SceneManager.GetActiveScene());
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyScene(scene);
    }

    // Selecciona el clip según el vehículo de la escena. Bus default según
    // pidió el usuario; sedanes/SUV usan car-claxon.
    void ApplyScene(Scene scene)
    {
        if (_audio != null && _audio.isPlaying) _audio.Stop();
        _wasPressed = false;
        _input = null;
        _inputResolveTimer = 0f;

        // MainMenu nunca tiene claxón — el botón 7 del HORI puede usarse en
        // pantalla 2 de calibración como cualquier otro botón.
        _enabledForScene = scene.name != "MainMenu";

        var name = scene.name;
        bool isCar = name == "Sedan" || name == "Camioneta";
        var clip = isCar ? _carClip : _busClip;
        if (clip == null && isCar) clip = _busClip; // fallback a bus si car no cargó
        if (_audio != null) _audio.clip = clip;
    }

    void Update()
    {
        if (!_enabledForScene || _audio == null || _audio.clip == null) return;

        // UIInputNew vive en el PlayerCar y aparece después del scene load.
        // Reintenta cada 0.5s mientras no esté disponible (evita
        // FindObjectOfType cada frame, que es lento).
        if (_input == null)
        {
            _inputResolveTimer -= Time.unscaledDeltaTime;
            if (_inputResolveTimer > 0f) return;
            _inputResolveTimer = 0.5f;
            _input = FindObjectOfType<Gley.UrbanSystem.UIInputNew>();
            if (_input == null) return;
        }

        bool pressed = _input.IsHornPressed();

        if (pressed && !_wasPressed)
        {
            _audio.Play();
        }
        else if (!pressed && _wasPressed)
        {
            _audio.Stop();
        }
        _wasPressed = pressed;
    }
}
