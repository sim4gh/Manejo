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
    // no billboard, así que el size escala el mesh 3D. Subido a 2.0 para que el
    // cambio sea claramente visible — si con 2.0 igual no se nota, el problema no
    // es el factor sino que el multiplier no aplica.
    private const float HAIL_SIZE_MULTIPLIER = 2.0f;

    private AudioSource weatherAudio;
    private Coroutine fadeCoroutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("[WeatherManager]");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<WeatherManager>();
        Debug.Log($"[WeatherManager] Bootstrap creó singleton (instanceId={Instance.GetInstanceID()})");

        // La escena inicial ya está cargada cuando AfterSceneLoad se ejecuta, así
        // que sceneLoaded NO va a disparar para ella. Aplicamos clima manualmente
        // si esa escena no es MainMenu.
        var active = SceneManager.GetActiveScene();
        if (active.IsValid() && active.name != "MainMenu")
        {
            Debug.Log($"[WeatherManager] Escena inicial '{active.name}' ya cargada — aplicando clima inmediatamente.");
            Instance.ApplyClima();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[WeatherManager] Awake — Instance ya existe ({Instance.GetInstanceID()}); destruyo el duplicado ({GetInstanceID()}).");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Debug.Log($"[WeatherManager] Awake — suscrito a sceneLoaded (instanceId={GetInstanceID()})");
    }

    private void OnDestroy()
    {
        Debug.LogWarning($"[WeatherManager] OnDestroy — desuscrito (instanceId={GetInstanceID()}, eraInstance={Instance == this})");
        if (Instance == this) Instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[WeatherManager] OnSceneLoaded scene='{scene.name}' mode={mode}");
        // Limpia audio anterior antes de aplicar el clima nuevo.
        CleanupAudio();

        // En MainMenu nunca llueve.
        if (scene.name == "MainMenu")
        {
            Debug.Log("[WeatherManager] Skip MainMenu.");
            return;
        }

        // Aplicar clima INMEDIATAMENTE — los GO LLuvia/Granizo ya están en escena
        // (colocados en YAML), no dependen del Player. Antes esperábamos hasta 2s
        // al Player y eso metía latencia visible al sentir el clima.
        ApplyClima();
    }

    private void ApplyClima()
    {
        int clima = PlayerPrefs.GetInt("Clima", 0);
        Debug.Log($"[WeatherManager] ApplyClima clima={clima} (PlayerPref 'Clima')");

        // Buscar todos los GO "LLuvia" y "Granizo" filtrando por componente
        // ParticleSystem para evitar matches falsos por nombre. Algunas escenas
        // tienen 2 instancias de cada uno (BusPasajeros, CamionDCarga, Ambulancia).
        var allParticles = Object.FindObjectsByType<ParticleSystem>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        Debug.Log($"[WeatherManager] FindObjectsByType<ParticleSystem> encontró {allParticles.Length} PS totales en escena.");

        var rainSystems = new List<ParticleSystem>();
        var hailSystems = new List<ParticleSystem>();
        foreach (var ps in allParticles)
        {
            if (ps == null || ps.gameObject == null) continue;
            var nm = ps.gameObject.name;
            if (nm == "LLuvia") rainSystems.Add(ps);
            else if (nm == "Granizo") hailSystems.Add(ps);
        }
        Debug.Log($"[WeatherManager] Filtrados por nombre: LLuvia={rainSystems.Count}, Granizo={hailSystems.Count}.");

        // Log de estado pre-cambio (activeSelf, activeInHierarchy, parent name)
        for (int i = 0; i < rainSystems.Count; i++)
        {
            var go = rainSystems[i].gameObject;
            var p = go.transform.parent != null ? go.transform.parent.name : "(root)";
            Debug.Log($"[WeatherManager]   LLuvia[{i}] activeSelf={go.activeSelf} activeInHierarchy={go.activeInHierarchy} parent='{p}'");
        }
        for (int i = 0; i < hailSystems.Count; i++)
        {
            var go = hailSystems[i].gameObject;
            var p = go.transform.parent != null ? go.transform.parent.name : "(root)";
            Debug.Log($"[WeatherManager]   Granizo[{i}] activeSelf={go.activeSelf} activeInHierarchy={go.activeInHierarchy} parent='{p}'");
        }

        switch (clima)
        {
            case 0: // Sol
                SetActiveAll(rainSystems, false, "LLuvia");
                SetActiveAll(hailSystems, false, "Granizo");
                Debug.Log($"[WeatherManager] Clima=Sol APLICADO.");
                break;
            case 1: // Lluvia normal
                SetActiveAll(rainSystems, true, "LLuvia");
                SetActiveAll(hailSystems, false, "Granizo");
                PlayWeatherAudio("Custom/Weather/RainLoop");
                Debug.Log($"[WeatherManager] Clima=Lluvia APLICADO.");
                break;
            case 2: // Granizo + lluvia ligera
                ActivateRainLight(rainSystems);
                ActivateHailLarge(hailSystems);
                PlayWeatherAudio("Custom/Weather/RainLoop");
                Debug.Log($"[WeatherManager] Clima=Granizo APLICADO.");
                break;
            default:
                Debug.LogWarning($"[WeatherManager] Valor de Clima desconocido: {clima}, fallback a Sol.");
                SetActiveAll(rainSystems, false, "LLuvia");
                SetActiveAll(hailSystems, false, "Granizo");
                break;
        }

        // Log post-cambio
        for (int i = 0; i < rainSystems.Count; i++)
        {
            var go = rainSystems[i].gameObject;
            Debug.Log($"[WeatherManager]   POST LLuvia[{i}] activeSelf={go.activeSelf} activeInHierarchy={go.activeInHierarchy}");
        }
        for (int i = 0; i < hailSystems.Count; i++)
        {
            var go = hailSystems[i].gameObject;
            Debug.Log($"[WeatherManager]   POST Granizo[{i}] activeSelf={go.activeSelf} activeInHierarchy={go.activeInHierarchy}");
        }
    }

    private static void SetActiveAll(List<ParticleSystem> systems, bool active, string label)
    {
        for (int i = 0; i < systems.Count; i++)
        {
            var ps = systems[i];
            if (ps == null) continue;
            ps.gameObject.SetActive(active);
            Debug.Log($"[WeatherManager]   SetActive({active}) en {label}[{i}] '{ps.gameObject.name}' (parent='{(ps.transform.parent != null ? ps.transform.parent.name : "root")}')");
        }
    }

    // Lluvia ligera de fondo (modo granizo): activa el GO y baja el rate de emisión.
    // Orden: SetActive ANTES de aplicar el multiplier, sino el SetActive reinicializa
    // el módulo emission desde el valor serializado y pierde nuestro multiplier.
    private static void ActivateRainLight(List<ParticleSystem> systems)
    {
        for (int i = 0; i < systems.Count; i++)
        {
            var ps = systems[i];
            if (ps == null) continue;
            ps.gameObject.SetActive(true);
            var emission = ps.emission;
            float beforeMul = emission.rateOverTimeMultiplier;
            emission.rateOverTimeMultiplier = HAIL_RAIN_RATE_MULTIPLIER;
            float rateConst = emission.rateOverTime.constant;
            Debug.Log($"[WeatherManager]   LLuvia[{i}] rate={rateConst} mul: {beforeMul} → {emission.rateOverTimeMultiplier}");
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
            float beforeMul = main.startSizeMultiplier;
            main.startSize = new ParticleSystem.MinMaxCurve(
                min * HAIL_SIZE_MULTIPLIER,
                max * HAIL_SIZE_MULTIPLIER);
            // Re-leer para confirmar que la asignación pegó.
            var after = ps.main.startSize;
            float afterMin = after.mode == ParticleSystemCurveMode.TwoConstants
                ? after.constantMin : after.constant;
            float afterMax = after.mode == ParticleSystemCurveMode.TwoConstants
                ? after.constantMax : after.constant;
            Debug.Log($"[WeatherManager]   Granizo[{i}] startSize mode={current.mode} {min}..{max} → wrote new MinMaxCurve({min*HAIL_SIZE_MULTIPLIER}..{max*HAIL_SIZE_MULTIPLIER}) → after-read mode={after.mode} {afterMin}..{afterMax}");
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
