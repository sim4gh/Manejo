using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Aplica el clima de la sesión en escenas de manejo: Sol, Lluvia o Granizo.
/// Singleton auto-instanciado al boot — no requiere setup manual en escena.
///
/// Lee <c>PlayerPrefs.GetInt("Clima")</c> (0=Sol, 1=Lluvia, 2=Granizo, default 0)
/// al cargar cualquier escena que NO sea MainMenu, y aplica el efecto:
///   - Sol: desactiva los GameObjects "Granizo" preexistentes; sin lluvia.
///   - Lluvia: instancia <c>Resources/Custom/Weather/RainPrefab</c> parented al
///     Player y reproduce <c>RainLoop</c> en loop con fade-in.
///   - Granizo: activa los GameObjects "Granizo" ya colocados en cada escena
///     (ParticleSystem world-space que cae por VelocityOverLifetime).
///
/// El sorteo del clima se hace en <see cref="MenuScreenManager.PickAndSetWeather"/>
/// con pesos ponderados, o forzado por demo code <c>TTTXY</c> donde X∈{0,1,2}.
///
/// IMPORTANTE — Granizo en sorteo: en este iter el peso del granizo es 0%
/// (validación pendiente). Solo accesible vía override de demo code <c>TTT2Y</c>.
/// Una vez validado visualmente que cae bien en las 6 escenas, ajustar pesos
/// en <see cref="MenuScreenManager"/>.
/// </summary>
public class WeatherManager : MonoBehaviour
{
    public static WeatherManager Instance { get; private set; }

    private const float RAIN_VOLUME = 0.4f;
    private const float RAIN_FADE_IN = 0.3f;
    private const float PLAYER_FIND_TIMEOUT = 2f;

    private GameObject rainRig;
    private AudioSource rainAudio;
    private Coroutine rainFadeCoroutine;
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
        // Limpia cualquier rig viejo antes de aplicar el clima nuevo (al re-entrar al
        // menu y volver a una escena, el rig anterior debe destruirse).
        CleanupRain();

        // En MainMenu nunca llueve.
        if (scene.name == "MainMenu") return;

        if (applyClimaCoroutine != null) StopCoroutine(applyClimaCoroutine);
        applyClimaCoroutine = StartCoroutine(ApplyClimaWhenReady());
    }

    private IEnumerator ApplyClimaWhenReady()
    {
        int clima = PlayerPrefs.GetInt("Clima", 0);

        // Localizar Player con retry pattern (similar a SpawnLocationManager).
        // El Player a veces no está disponible en el primer frame post `sceneLoaded`.
        Transform player = null;
        float t = 0f;
        while (t < PLAYER_FIND_TIMEOUT && player == null)
        {
            player = FindPlayer();
            if (player != null) break;
            yield return null;
            t += Time.unscaledDeltaTime;
        }

        // Buscar todos los GameObjects "Granizo" (algunas escenas tienen 2). Filtrar por
        // ParticleSystem para evitar matches falsos por nombre. Incluir inactivos para
        // capturar GO que ya estaban apagados en la escena.
        var allParticles = Object.FindObjectsByType<ParticleSystem>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        var hailObjects = new List<GameObject>();
        foreach (var ps in allParticles)
        {
            if (ps != null && ps.gameObject != null && ps.gameObject.name == "Granizo")
                hailObjects.Add(ps.gameObject);
        }

        switch (clima)
        {
            case 0: // Sol
                foreach (var h in hailObjects) h.SetActive(false);
                Debug.Log($"[WeatherManager] Clima=Sol aplicado. {hailObjects.Count} GO 'Granizo' apagado(s).");
                break;
            case 1: // Lluvia
                foreach (var h in hailObjects) h.SetActive(false);
                SpawnRain(player);
                Debug.Log($"[WeatherManager] Clima=Lluvia aplicado. Player {(player != null ? "encontrado" : "NO encontrado")} tras {t:F2}s.");
                break;
            case 2: // Granizo
                foreach (var h in hailObjects) h.SetActive(true);
                Debug.Log($"[WeatherManager] Clima=Granizo aplicado. {hailObjects.Count} GO 'Granizo' activado(s). NOTA: validar visualmente — granizo aún no en sorteo random.");
                break;
            default:
                Debug.LogWarning($"[WeatherManager] Valor de Clima desconocido: {clima}, fallback a Sol.");
                foreach (var h in hailObjects) h.SetActive(false);
                break;
        }
    }

    private Transform FindPlayer()
    {
        // 1) Gley API
        var pc = Object.FindFirstObjectByType<Gley.UrbanSystem.PlayerCar>(FindObjectsInactive.Include);
        if (pc != null) return pc.transform;
        // 2) Tag
        var tagged = GameObject.FindGameObjectWithTag("Player");
        if (tagged != null) return tagged.transform;
        // 3) Name
        var named = GameObject.Find("Player");
        if (named != null) return named.transform;
        return null;
    }

    private void SpawnRain(Transform parent)
    {
        var prefab = Resources.Load<GameObject>("Custom/Weather/RainPrefab");
        if (prefab == null)
        {
            Debug.LogError("[WeatherManager] Resources/Custom/Weather/RainPrefab no encontrado — lluvia visual deshabilitada.");
        }
        else
        {
            rainRig = Instantiate(prefab);
            rainRig.name = "[Rain]";
            if (parent != null)
            {
                rainRig.transform.SetParent(parent, worldPositionStays: false);
                rainRig.transform.localPosition = Vector3.zero;
            }
            // Si no hay Player, el rig queda en world origin — no ideal pero funcional como fallback.
        }

        // AudioSource hijo del rig (o de un GameObject standalone si el rig no se creó).
        var clip = Resources.Load<AudioClip>("Custom/Weather/RainLoop");
        if (clip == null)
        {
            Debug.LogWarning("[WeatherManager] Resources/Custom/Weather/RainLoop no encontrado — lluvia visual sin audio.");
            return;
        }
        var audioParent = rainRig != null ? rainRig.transform : transform;
        var audioGo = new GameObject("RainAudio");
        audioGo.transform.SetParent(audioParent, false);
        rainAudio = audioGo.AddComponent<AudioSource>();
        rainAudio.clip = clip;
        rainAudio.loop = true;
        rainAudio.spatialBlend = 0f; // 2D — ambient, no posicional
        rainAudio.playOnAwake = false;
        rainAudio.volume = 0f;
        rainAudio.Play();

        if (rainFadeCoroutine != null) StopCoroutine(rainFadeCoroutine);
        rainFadeCoroutine = StartCoroutine(FadeAudio(rainAudio, 0f, RAIN_VOLUME, RAIN_FADE_IN));
    }

    private void CleanupRain()
    {
        if (rainFadeCoroutine != null)
        {
            StopCoroutine(rainFadeCoroutine);
            rainFadeCoroutine = null;
        }
        if (rainRig != null)
        {
            Destroy(rainRig);
            rainRig = null;
            rainAudio = null;
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
        rainFadeCoroutine = null;
    }
}
