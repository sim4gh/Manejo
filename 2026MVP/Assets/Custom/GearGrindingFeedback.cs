using UnityEngine;

/// <summary>
/// Singleton bootstrap para reproducir el sonido de "rechino" cuando el examinado
/// cambia de marcha sin presionar el clutch (manual mode + G923 PS). ViolationDetector
/// llama <see cref="PlayGrind"/> directamente; el cooldown de penalización se aplica
/// allá pero el audio es siempre inmediato (feedback continuo, sin acumular puntos).
///
/// Patrón calcado de <see cref="CollisionFeedback"/>: instancia BeforeSceneLoad,
/// AudioSource pre-warmed con PlayOneShot inaudible (volume=0) en Awake para
/// eliminar el first-play hitch (~50-200ms) típico de la primera reproducción
/// de Unity. La primera vez que el examinado rechine en gameplay debe sonar
/// instantáneo, sin lag perceptible.
/// </summary>
public class GearGrindingFeedback : MonoBehaviour
{
    public static GearGrindingFeedback Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("[GearGrindingFeedback]");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<GearGrindingFeedback>();
    }

    private AudioClip[] clips;
    private AudioSource audioSource;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        LoadClips();
        SetupAudioSource();
    }

    private void LoadClips()
    {
        var so = Resources.Load<GearGrindingClips>("Custom/GearGrindingClips");
        if (so != null && so.clips != null && so.clips.Length > 0)
        {
            clips = so.clips;
        }
        else
        {
            Debug.LogWarning("[GearGrindingFeedback] Resources/Custom/GearGrindingClips.asset no encontrado o sin clips — audio de rechino deshabilitado.");
            clips = new AudioClip[0];
        }
    }

    private void SetupAudioSource()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0f; // 2D
        audioSource.playOnAwake = false;
        audioSource.loop = false;

        // Pre-warm: ver CollisionFeedback.SetupAudioSource — PlayOneShot inaudible
        // al boot evita el cold-start hitch del primer play en gameplay.
        if (clips != null && clips.Length > 0 && clips[0] != null)
        {
            audioSource.PlayOneShot(clips[0], 0f);
        }
    }

    /// <summary>
    /// Reproduce un clip aleatorio del banco. Llamar SIN throttle desde
    /// ViolationDetector — el feedback debe ser inmediato cada vez que el
    /// examinado rechina, aunque la penalización tenga cooldown.
    /// </summary>
    public void PlayGrind()
    {
        if (audioSource == null || clips == null || clips.Length == 0) return;
        var clip = clips[Random.Range(0, clips.Length)];
        if (clip != null) audioSource.PlayOneShot(clip, 0.85f);
    }
}
