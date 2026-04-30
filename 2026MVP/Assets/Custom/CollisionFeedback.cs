using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Orquestador de feedback visceral de colisión: overlay de cristal roto, screen shake,
/// flash rojo, audio one-shot y force feedback en G923 (si conectado). Singleton
/// auto-instanciado al boot — no requiere setup manual en escena.
///
/// Se suscribe a <see cref="ViolationDetector.OnCollisionImpact"/>. Respeta el gate de
/// <see cref="SimulatorConfig"/>.showNotifications para no tapar la pantalla cuando el
/// examinador desactivó las notificaciones.
/// </summary>
public class CollisionFeedback : MonoBehaviour
{
    public static CollisionFeedback Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("[CollisionFeedback]");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<CollisionFeedback>();
    }

    // UI
    private Canvas canvas;
    private RawImage crackImage;
    private RawImage flashImage;
    private CanvasGroup crackGroup;

    // Audio
    private AudioSource audioSource;
    private AudioClip[] impactClips;

    // Camera shake
    private Vector3 shakeOriginalLocalPos;
    private Transform shakeTransform;
    private bool isShaking;

    // Coroutines tracker (cancelar en re-disparo si ocurre antes del cooldown — no debería con cooldown 3s)
    private Coroutine crackCoroutine;
    private Coroutine flashCoroutine;
    private Coroutine ffbCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        BuildCanvas();
        LoadImpactClips();
        SetupAudioSource();
        LogitechFFB.TryInitialize();
        Debug.Log("[CollisionFeedback] Inicializado.");
    }

    private void OnEnable()
    {
        ViolationDetector.OnCollisionImpact += HandleImpact;
    }

    private void OnDisable()
    {
        ViolationDetector.OnCollisionImpact -= HandleImpact;
    }

    private void OnDestroy()
    {
        LogitechFFB.StopConstantForce();
        LogitechFFB.StopBumpyRoad();
        LogitechFFB.Shutdown();
    }

    private void LateUpdate()
    {
        // El SDK Logitech requiere update por frame para mantener efectos vivos.
        LogitechFFB.Update();
    }

    private void BuildCanvas()
    {
        var canvasGo = new GameObject("CollisionFeedbackCanvas");
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000; // arriba del HUD pero debajo de paneles F7-F10 si los hay

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        // Cristal roto (encima del flash)
        var crackGo = new GameObject("CrackOverlay");
        crackGo.transform.SetParent(canvasGo.transform, false);
        crackImage = crackGo.AddComponent<RawImage>();
        crackImage.raycastTarget = false;
        crackImage.texture = Resources.Load<Texture2D>("Custom/CrackedGlass");
        if (crackImage.texture == null)
            Debug.LogWarning("[CollisionFeedback] Resources/Custom/CrackedGlass.png no encontrado — overlay funcionará vacío hasta que se agregue el asset.");

        var crackRect = crackGo.GetComponent<RectTransform>();
        crackRect.anchorMin = Vector2.zero;
        crackRect.anchorMax = Vector2.one;
        crackRect.offsetMin = Vector2.zero;
        crackRect.offsetMax = Vector2.zero;

        crackGroup = crackGo.AddComponent<CanvasGroup>();
        crackGroup.alpha = 0f;
        crackGroup.blocksRaycasts = false;
        crackGroup.interactable = false;

        // Flash rojo (debajo del cristal — el cristal pinta encima del flash)
        var flashGo = new GameObject("RedFlash");
        flashGo.transform.SetParent(canvasGo.transform, false);
        flashGo.transform.SetSiblingIndex(0); // detrás del crack
        flashImage = flashGo.AddComponent<RawImage>();
        flashImage.raycastTarget = false;
        flashImage.color = new Color(1f, 0f, 0f, 0f);
        var flashRect = flashGo.GetComponent<RectTransform>();
        flashRect.anchorMin = Vector2.zero;
        flashRect.anchorMax = Vector2.one;
        flashRect.offsetMin = Vector2.zero;
        flashRect.offsetMax = Vector2.zero;
    }

    private void LoadImpactClips()
    {
        var so = Resources.Load<CollisionImpactClips>("Custom/CollisionImpactClips");
        if (so != null && so.clips != null && so.clips.Length > 0)
        {
            impactClips = so.clips;
        }
        else
        {
            Debug.LogWarning("[CollisionFeedback] Resources/Custom/CollisionImpactClips.asset no encontrado o sin clips — audio de colisión deshabilitado.");
            impactClips = new AudioClip[0];
        }
    }

    private void SetupAudioSource()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0f; // 2D
        audioSource.playOnAwake = false;
        audioSource.loop = false;
    }

    private void HandleImpact(ViolationDetector.CollisionImpactInfo info)
    {
        // El feedback de colisión es esencial para el examen — el examinado debe saber
        // que chocó. NO se gatea con SimulatorConfig.showNotifications (eso solo afecta
        // textos pequeños del NotificationManager).
        Debug.Log($"[CollisionFeedback] {info.violationType} impulse={info.magnitude:F1} lateral={info.lateralLocal:F2} speed={info.speedKmh:F1}km/h");

        float t = Mathf.Clamp01(info.magnitude / 50f);

        // Overlay cristal roto
        if (crackCoroutine != null) StopCoroutine(crackCoroutine);
        crackCoroutine = StartCoroutine(CrackOverlayRoutine(
            alphaTarget: 0.85f + t * 0.15f,
            fadeIn: 0.05f,
            hold: 1.2f + t * 0.6f,
            fadeOut: 0.5f));

        // Camera shake
        StartCameraShake(
            amplitude: 0.03f + t * 0.05f,
            durationFrames: 6 + Mathf.RoundToInt(t * 8));

        // Flash rojo
        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(FlashRoutine(
            alphaTarget: 0.25f + t * 0.20f,
            fadeOut: 0.4f));

        // Audio
        PlayImpactSound(0.3f + t * 0.7f);

        // FFB G923 (no-op si no hay wheel Logitech)
        if (LogitechFFB.IsConnected())
        {
            int constantPctMagnitude = Mathf.RoundToInt(40f + t * 60f); // 40..100
            int sign = info.lateralLocal >= 0f ? +1 : -1;
            int constantPctSigned = sign * constantPctMagnitude;
            int bumpyPct = Mathf.RoundToInt(30f + t * 50f);

            if (ffbCoroutine != null) StopCoroutine(ffbCoroutine);
            ffbCoroutine = StartCoroutine(FFBImpactRoutine(constantPctSigned, 0.08f, bumpyPct, 0.25f));
        }
    }

    private IEnumerator CrackOverlayRoutine(float alphaTarget, float fadeIn, float hold, float fadeOut)
    {
        // Fade in
        float elapsed = 0f;
        while (elapsed < fadeIn)
        {
            elapsed += Time.unscaledDeltaTime;
            crackGroup.alpha = Mathf.Lerp(0f, alphaTarget, elapsed / fadeIn);
            yield return null;
        }
        crackGroup.alpha = alphaTarget;

        // Hold
        elapsed = 0f;
        while (elapsed < hold)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Fade out
        elapsed = 0f;
        while (elapsed < fadeOut)
        {
            elapsed += Time.unscaledDeltaTime;
            crackGroup.alpha = Mathf.Lerp(alphaTarget, 0f, elapsed / fadeOut);
            yield return null;
        }
        crackGroup.alpha = 0f;
        crackCoroutine = null;
    }

    private IEnumerator FlashRoutine(float alphaTarget, float fadeOut)
    {
        flashImage.color = new Color(1f, 0f, 0f, alphaTarget);
        float elapsed = 0f;
        while (elapsed < fadeOut)
        {
            elapsed += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(alphaTarget, 0f, elapsed / fadeOut);
            flashImage.color = new Color(1f, 0f, 0f, a);
            yield return null;
        }
        flashImage.color = new Color(1f, 0f, 0f, 0f);
        flashCoroutine = null;
    }

    private void StartCameraShake(float amplitude, int durationFrames)
    {
        if (Camera.main == null) return;
        if (isShaking)
        {
            // Restaurar antes de re-shake
            if (shakeTransform != null)
                shakeTransform.localPosition = shakeOriginalLocalPos;
        }
        shakeTransform = Camera.main.transform;
        shakeOriginalLocalPos = shakeTransform.localPosition;
        isShaking = true;
        StartCoroutine(ShakeRoutine(amplitude, durationFrames));
    }

    private IEnumerator ShakeRoutine(float amplitude, int durationFrames)
    {
        for (int i = 0; i < durationFrames; i++)
        {
            if (shakeTransform == null) break;
            float decay = 1f - (i / (float)durationFrames);
            Vector3 jitter = new Vector3(
                (Random.value - 0.5f) * 2f * amplitude * decay,
                (Random.value - 0.5f) * 2f * amplitude * decay,
                0f);
            shakeTransform.localPosition = shakeOriginalLocalPos + jitter;
            yield return null;
        }
        if (shakeTransform != null)
            shakeTransform.localPosition = shakeOriginalLocalPos;
        isShaking = false;
        shakeTransform = null;
    }

    private void PlayImpactSound(float volume)
    {
        if (audioSource == null || impactClips == null || impactClips.Length == 0) return;
        var clip = impactClips[Random.Range(0, impactClips.Length)];
        if (clip == null) return;
        audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    private IEnumerator FFBImpactRoutine(int constantPctSigned, float constantSec, int bumpyPct, float bumpySec)
    {
        LogitechFFB.PlayConstantForce(constantPctSigned);
        LogitechFFB.PlayBumpyRoad(bumpyPct);

        float elapsed = 0f;
        bool constantStopped = false;
        float total = Mathf.Max(constantSec, bumpySec);
        while (elapsed < total)
        {
            if (!constantStopped && elapsed >= constantSec)
            {
                LogitechFFB.StopConstantForce();
                constantStopped = true;
            }
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        if (!constantStopped) LogitechFFB.StopConstantForce();
        LogitechFFB.StopBumpyRoad();
        ffbCoroutine = null;
    }
}
