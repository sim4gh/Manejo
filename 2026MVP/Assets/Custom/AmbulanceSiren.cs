using UnityEngine;
using UnityEngine.SceneManagement;

public class AmbulanceSiren : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoCreate()
    {
        var go = new GameObject("[AmbulanceSiren]");
        go.AddComponent<AmbulanceSiren>();
        DontDestroyOnLoad(go);
    }

    private AudioSource _audio;
    private bool _active;

    private const string CLIP_PATH = "Audio/Siren/Ambulance_Siren";
    private const float VOLUME = 0.45f;

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        bool isAmbulance = scene.name == "Ambulancia";
        Debug.Log($"[AmbulanceSiren] sceneLoaded: {scene.name}, isAmbulance={isAmbulance}");

        if (isAmbulance)
            StartSiren();
        else
            StopSiren();
    }

    void StartSiren()
    {
        if (_active) return;

        var clip = Resources.Load<AudioClip>(CLIP_PATH);
        if (clip == null)
        {
            Debug.LogWarning($"[AmbulanceSiren] Clip no encontrado: {CLIP_PATH}");
            return;
        }

        if (_audio == null)
            _audio = gameObject.AddComponent<AudioSource>();

        _audio.clip = clip;
        _audio.loop = true;
        _audio.spatialBlend = 0f;
        _audio.playOnAwake = false;
        _audio.volume = VOLUME;
        _audio.Play();
        _active = true;
        Debug.Log($"[AmbulanceSiren] Sirena activa ({clip.length:F1}s loop)");
    }

    void StopSiren()
    {
        if (!_active) return;

        if (_audio != null && _audio.isPlaying)
            _audio.Stop();

        _active = false;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        StopSiren();
    }
}
