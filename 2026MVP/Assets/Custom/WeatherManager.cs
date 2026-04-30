using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Aplica el clima de la sesión en escenas de manejo: Sol, Lluvia o Granizo.
/// Singleton auto-instanciado al boot — no requiere setup manual en escena.
///
/// Lee <c>PlayerPrefs.GetInt("Clima")</c> (0=Sol, 1=Lluvia, 2=Granizo, default 0)
/// al cargar cualquier escena que NO sea MainMenu, y aplica el efecto activando
/// o desactivando los GameObjects "LLuvia" y "Granizo" preexistentes en cada
/// escena (ambos son ParticleSystem world-space ya colocados):
///   - Sol: LLuvia OFF + Granizo OFF.
///   - Lluvia: LLuvia ON (densidad normal) + Granizo OFF + RainLoop.
///   - Granizo: LLuvia ON (densidad reducida 30%) + Granizo ON con tamaño +50%
///     (en X/Y/Z, porque size3D=1) + RainLoop. Modificamos los multipliers del
///     ParticleSystem en runtime — Unity restaura defaults al recargar escena
///     (el flujo del juego siempre vuelve a MainMenu antes de re-entrar).
///
/// El sorteo del clima se hace en <see cref="MenuScreenManager.PickAndSetWeather"/>
/// con pesos ponderados, o forzado por demo code <c>TTTXY</c> donde X∈{0,1,2}.
/// </summary>
public class WeatherManager : MonoBehaviour
{
    public static WeatherManager Instance { get; private set; }

    private const float WEATHER_VOLUME = 1.0f;
    private const float FADE_IN_SECONDS = 0.3f;

    // Densidad de la lluvia ligera que acompaña al granizo (multiplicador del rate
    // de emisión de cada GO LLuvia). 0.3 = 30% del rate normal por instancia.
    private const float HAIL_RAIN_RATE_MULTIPLIER = 0.3f;

    // Tamaño del granizo en modo granizo. El render es Mesh (PiedraGranizo.fbx),
    // no billboard, así que el size escala el mesh 3D. 4.0 = bolas 4× más grandes
    // que el original (validado visualmente con el usuario en Sedan).
    private const float HAIL_SIZE_MULTIPLIER = 4.0f;

    private AudioSource weatherAudio;
    private Coroutine fadeCoroutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("[WeatherManager]");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<WeatherManager>();

        // La escena inicial ya está cargada cuando AfterSceneLoad se ejecuta, así
        // que sceneLoaded NO va a disparar para ella. Aplicamos clima manualmente
        // si esa escena no es MainMenu (caso "play directamente desde Sedan" en Editor).
        var active = SceneManager.GetActiveScene();
        if (active.IsValid() && active.name != "MainMenu")
            Instance.ApplyClima();
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

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CleanupAudio();
        if (scene.name == "MainMenu") return;
        ApplyClima();
    }

    private void ApplyClima()
    {
        int clima = PlayerPrefs.GetInt("Clima", 0);

        var allParticles = Object.FindObjectsByType<ParticleSystem>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        var rainSystems = new List<ParticleSystem>();
        var hailSystems = new List<ParticleSystem>();
        foreach (var ps in allParticles)
        {
            if (ps == null || ps.gameObject == null) continue;
            var nm = ps.gameObject.name;
            if (nm == "LLuvia") rainSystems.Add(ps);
            else if (nm == "Granizo") hailSystems.Add(ps);
        }

        switch (clima)
        {
            case 0: // Sol
                SetActiveAll(rainSystems, false);
                SetActiveAll(hailSystems, false);
                Debug.Log($"[WeatherManager] Clima=Sol. LLuvia({rainSystems.Count})=OFF Granizo({hailSystems.Count})=OFF.");
                break;
            case 1: // Lluvia normal
                SetActiveAll(rainSystems, true);
                SetActiveAll(hailSystems, false);
                PlayWeatherAudio("Custom/Weather/RainLoop");
                Debug.Log($"[WeatherManager] Clima=Lluvia. LLuvia({rainSystems.Count})=ON Granizo({hailSystems.Count})=OFF.");
                break;
            case 2: // Granizo + lluvia ligera
                ActivateRainLight(rainSystems);
                ActivateHailLarge(hailSystems);
                PlayWeatherAudio("Custom/Weather/RainLoop");
                Debug.Log($"[WeatherManager] Clima=Granizo. LLuvia({rainSystems.Count})=ON@{HAIL_RAIN_RATE_MULTIPLIER:F2}x Granizo({hailSystems.Count})=ON@{HAIL_SIZE_MULTIPLIER:F2}x.");
                break;
            default:
                Debug.LogWarning($"[WeatherManager] Valor de Clima desconocido: {clima}, fallback a Sol.");
                SetActiveAll(rainSystems, false);
                SetActiveAll(hailSystems, false);
                break;
        }
    }

    private static void SetActiveAll(List<ParticleSystem> systems, bool active)
    {
        foreach (var ps in systems)
            if (ps != null) ps.gameObject.SetActive(active);
    }

    // Lluvia ligera de fondo (modo granizo): activa el GO y reasigna `rateOverTime`
    // con un nuevo MinMaxCurve escalado. Mismo approach que `ActivateHailLarge`
    // porque el multiplier escalar no aplicaba en pruebas previas (la lluvia se
    // veía igual de densa o no se veía).
    private static void ActivateRainLight(List<ParticleSystem> systems)
    {
        for (int i = 0; i < systems.Count; i++)
        {
            var ps = systems[i];
            if (ps == null) continue;
            ps.gameObject.SetActive(true);
            var emission = ps.emission;
            var current = emission.rateOverTime;
            float baseRate = current.mode == ParticleSystemCurveMode.TwoConstants
                ? current.constantMax : current.constant;
            float newRate = baseRate * HAIL_RAIN_RATE_MULTIPLIER;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(newRate);
            Debug.Log($"[WeatherManager]   LLuvia[{i}] rate {baseRate} → {newRate}");
        }
    }

    // Granizo más grande: activa el GO y reasigna `startSize` con un nuevo
    // MinMaxCurve escalado. Importante:
    // - El render es Mesh (PiedraGranizo.fbx), no billboard. startSize escala el mesh.
    // - Reasignamos `main.startSize = new MinMaxCurve(...)` en vez de mutar la
    //   instancia existente, porque MinMaxCurve es struct y la mutación in-place
    //   no siempre persiste por valor en MainModule.
    // - Forzamos modo TwoConstants en el nuevo MinMaxCurve, igual al modo serializado.
    private static void ActivateHailLarge(List<ParticleSystem> systems)
    {
        for (int i = 0; i < systems.Count; i++)
        {
            var ps = systems[i];
            if (ps == null) continue;
            ps.gameObject.SetActive(true);
            var main = ps.main;
            var current = main.startSize;
            float min = current.mode == ParticleSystemCurveMode.TwoConstants
                ? current.constantMin : current.constant;
            float max = current.mode == ParticleSystemCurveMode.TwoConstants
                ? current.constantMax : current.constant;
            // Desactivar startSize3D para que startSize aplique a los 3 ejes
            // uniformemente. Si lo dejamos en true, startSize solo escala X y las
            // partículas (mesh) salen como discos aplastados (X grande, Y/Z normal).
            main.startSize3D = false;
            main.startSize = new ParticleSystem.MinMaxCurve(
                min * HAIL_SIZE_MULTIPLIER,
                max * HAIL_SIZE_MULTIPLIER);
            Debug.Log($"[WeatherManager]   Granizo[{i}] size {min}..{max} → {min*HAIL_SIZE_MULTIPLIER}..{max*HAIL_SIZE_MULTIPLIER} (size3D=false)");
        }
    }

    private void PlayWeatherAudio(string resourcePath)
    {
        var clip = Resources.Load<AudioClip>(resourcePath);
        if (clip == null)
        {
            Debug.LogWarning($"[WeatherManager] Resources/{resourcePath} no encontrado — sin audio.");
            return;
        }
        // AudioSource hijo del WeatherManager (DontDestroyOnLoad) — sobrevive cambios
        // de escena igual que el manager. CleanupAudio() lo destruye al cambiar clima.
        var audioGo = new GameObject("WeatherAudio");
        audioGo.transform.SetParent(transform, false);
        weatherAudio = audioGo.AddComponent<AudioSource>();
        weatherAudio.clip = clip;
        weatherAudio.loop = true;
        weatherAudio.spatialBlend = 0f; // 2D ambient
        weatherAudio.playOnAwake = false;
        weatherAudio.volume = 0f;
        weatherAudio.Play();

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeAudio(weatherAudio, 0f, WEATHER_VOLUME, FADE_IN_SECONDS));
    }

    private void CleanupAudio()
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }
        if (weatherAudio != null)
        {
            Destroy(weatherAudio.gameObject);
            weatherAudio = null;
        }
    }

    private IEnumerator FadeAudio(AudioSource src, float from, float to, float duration)
    {
        if (src == null) yield break;
        float elapsed = 0f;
        src.volume = from;
        while (elapsed < duration)
        {
            if (src == null) yield break;
            elapsed += Time.unscaledDeltaTime;
            src.volume = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        if (src != null) src.volume = to;
        fadeCoroutine = null;
    }
}
