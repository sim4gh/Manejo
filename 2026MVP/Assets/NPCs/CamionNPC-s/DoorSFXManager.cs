using UnityEngine;

/// <summary>
/// Singleton bootstrapped que reproduce SFX de puerta del bus (open/close).
/// Mismo patrón que CollisionFeedback: AudioSource pre-warmed para evitar
/// cold-start hitch, clips referenciados via ScriptableObject en
/// Resources/Custom/DoorSFXClips.asset.
/// </summary>
public class DoorSFXManager : MonoBehaviour
{
    public static DoorSFXManager Instance { get; private set; }

    private AudioSource src;
    private AudioClip openClip;
    private AudioClip closeClip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("[DoorSFXManager]");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<DoorSFXManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;

        var clips = Resources.Load<DoorSFXClips>("Custom/DoorSFXClips");
        if (clips != null)
        {
            openClip = clips.open;
            closeClip = clips.close;
            if (openClip) src.PlayOneShot(openClip, 0f);
        }
        else
        {
            Debug.LogWarning("[DoorSFXManager] No se encontró Resources/Custom/DoorSFXClips.asset — SFX deshabilitado");
        }
    }

    public void PlayDoorOpen()
    {
        if (src != null && openClip != null) src.PlayOneShot(openClip, 0.7f);
    }

    public void PlayDoorClose()
    {
        if (src != null && closeClip != null) src.PlayOneShot(closeClip, 0.7f);
    }
}
