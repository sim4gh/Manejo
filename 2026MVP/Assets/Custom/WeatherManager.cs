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
///   - Lluvia: LLuvia ON + Granizo OFF + AudioSource loop con RainLoop.
///   - Granizo: LLuvia OFF + Granizo ON + AudioSource loop con HailLoop.
///
/// El sorteo del clima se hace en <see cref="MenuScreenManager.PickAndSetWeather"/>
/// con pesos ponderados, o forzado por demo code <c>TTTXY</c> donde X∈{0,1,2}.
///
/// IMPORTANTE — Granizo en sorteo: en este iter el peso del granizo es 0%
/// (validación pendiente del tamaño de las partículas). Solo accesible vía
/// override de demo code <c>TTT2Y</c>. Una vez validado visualmente, ajustar
/// pesos en <see cref="MenuScreenManager"/>.
/// </summary>
public class WeatherManager : MonoBehaviour
{
    public static WeatherManager Instance { get; private set; }

    private const float WEATHER_VOLUME = 1.0f;
    private const float FADE_IN_SECONDS = 0.3f;
    private const float PLAYER_FIND_TIMEOUT = 2f;

    private AudioSource weatherAudio;
    private Coroutine fadeCoroutine;
    private Coroutine applyClimaCoroutine;

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

        if (applyClimaCoroutine != null) StopCoroutine(applyClimaCoroutine);
        applyClimaCoroutine = StartCoroutine(ApplyClimaWhenReady());
    }

    private IEnumerator ApplyClimaWhenReady()
    {
        int clima = PlayerPrefs.GetInt("Clima", 0);

        // Esperar a que el Player esté disponible (algunas escenas lo cargan tarde).
        // Aunque el clima en sí no necesita al Player (los GO LLuvia/Granizo ya
        // están en escena), esperarlo asegura que el log refleje un estado estable.
        Transform player = null;
        float t = 0f;
        while (t < PLAYER_FIND_TIMEOUT && player == null)
        {
            player = FindPlayer();
            if (player != null) break;
            yield return null;
            t += Time.unscaledDeltaTime;
        }

        // Buscar todos los GO "LLuvia" y "Granizo" filtrando por componente
        // ParticleSystem para evitar matches falsos por nombre. Algunas escenas
        // tienen 2 instancias de cada uno (BusPasajeros, CamionDCarga, Ambulancia).
        var allParticles = Object.FindObjectsByType<ParticleSystem>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        var rainObjects = new List<GameObject>();
        var hailObjects = new List<GameObject>();
        foreach (var ps in allParticles)
        {
            if (ps == null || ps.gameObject == null) continue;
            var nm = ps.gameObject.name;
            if (nm == "LLuvia") rainObjects.Add(ps.gameObject);
            else if (nm == "Granizo") hailObjects.Add(ps.gameObject);
        }

        switch (clima)
        {
            case 0: // Sol
                SetActiveAll(rainObjects, false);
                SetActiveAll(hailObjects, false);
                Debug.Log($"[WeatherManager] Clima=Sol. LLuvia({rainObjects.Count})=OFF Granizo({hailObjects.Count})=OFF.");
                break;
            case 1: // Lluvia
                SetActiveAll(rainObjects, true);
                SetActiveAll(hailObjects, false);
                PlayWeatherAudio("Custom/Weather/RainLoop");
                Debug.Log($"[WeatherManager] Clima=Lluvia. LLuvia({rainObjects.Count})=ON Granizo({hailObjects.Count})=OFF. Player tras {t:F2}s.");
                break;
            case 2: // Granizo
                SetActiveAll(rainObjects, false);
                SetActiveAll(hailObjects, true);
                PlayWeatherAudio("Custom/Weather/HailLoop");
                Debug.Log($"[WeatherManager] Clima=Granizo. LLuvia({rainObjects.Count})=OFF Granizo({hailObjects.Count})=ON. NOTA: validar visualmente — granizo aún no en sorteo random.");
                break;
            default:
                Debug.LogWarning($"[WeatherManager] Valor de Clima desconocido: {clima}, fallback a Sol.");
                SetActiveAll(rainObjects, false);
                SetActiveAll(hailObjects, false);
                break;
        }
    }

    private static void SetActiveAll(List<GameObject> gos, bool active)
    {
        foreach (var g in gos)
            if (g != null) g.SetActive(active);
    }

    private Transform FindPlayer()
    {
        var pc = Object.FindFirstObjectByType<Gley.UrbanSystem.PlayerCar>(FindObjectsInactive.Include);
        if (pc != null) return pc.transform;
        var tagged = GameObject.FindGameObjectWithTag("Player");
        if (tagged != null) return tagged.transform;
        var named = GameObject.Find("Player");
        if (named != null) return named.transform;
        return null;
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
