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

    // Tamaño del granizo en modo granizo: 1.5 = 50% más grande en X/Y/Z (size3D=1
    // en el ParticleSystem serializado, así que el escalar `startSizeMultiplier` no
    // es confiable — hay que tocar X/Y/Z explícitamente).
    private const float HAIL_SIZE_MULTIPLIER = 1.5f;

    private AudioSource weatherAudio;
    private Coroutine fadeCoroutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("[WeatherManager]");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<WeatherManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Limpia audio anterior antes de aplicar el clima nuevo.
        CleanupAudio();

        // En MainMenu nunca llueve.
        if (scene.name == "MainMenu") return;

        // Aplicar clima INMEDIATAMENTE — los GO LLuvia/Granizo ya están en escena
        // (colocados en YAML), no dependen del Player. Antes esperábamos hasta 2s
        // al Player y eso metía latencia visible al sentir el clima.
        ApplyClima();
    }

    private void ApplyClima()
    {
        int clima = PlayerPrefs.GetInt("Clima", 0);

        // Buscar todos los GO "LLuvia" y "Granizo" filtrando por componente
        // ParticleSystem para evitar matches falsos por nombre. Algunas escenas
        // tienen 2 instancias de cada uno (BusPasajeros, CamionDCarga, Ambulancia).
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

    // Lluvia ligera de fondo (modo granizo): activa el PS y baja el rate de emisión
    // al `HAIL_RAIN_RATE_MULTIPLIER`. `Clear()` antes de modificar evita arrastrar
    // partículas residuales si el PS ya estaba vivo de un estado anterior.
    private static void ActivateRainLight(List<ParticleSystem> systems)
    {
        foreach (var ps in systems)
        {
            if (ps == null) continue;
            ps.gameObject.SetActive(true);
            ps.Clear();
            var emission = ps.emission;
            emission.rateOverTimeMultiplier = HAIL_RAIN_RATE_MULTIPLIER;
        }
    }

    // Granizo más grande: activa el PS y multiplica el startSize por
    // `HAIL_SIZE_MULTIPLIER` en X/Y/Z. Tocamos los tres ejes porque los PS tienen
    // `size3D=1` en YAML — el escalar `startSizeMultiplier` no es confiable en ese
    // modo (puede aplicar solo a X). `Clear()` defensivo igual que en lluvia.
    private static void ActivateHailLarge(List<ParticleSystem> systems)
    {
        foreach (var ps in systems)
        {
            if (ps == null) continue;
            ps.gameObject.SetActive(true);
            ps.Clear();
            var main = ps.main;
            main.startSizeXMultiplier = HAIL_SIZE_MULTIPLIER;
            main.startSizeYMultiplier = HAIL_SIZE_MULTIPLIER;
            main.startSizeZMultiplier = HAIL_SIZE_MULTIPLIER;
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
