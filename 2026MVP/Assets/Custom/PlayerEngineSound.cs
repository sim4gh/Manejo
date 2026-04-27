using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerEngineSound : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoCreate()
    {
        Debug.Log("[PlayerEngineSound] AutoCreate");
        var go = new GameObject("[PlayerEngineSound]");
        go.AddComponent<PlayerEngineSound>();
        DontDestroyOnLoad(go);
    }

    public enum AudioProfile { Sedan, SUV, Bus, Truck, None }

    private struct ProfileData
    {
        public string clipPath;
        public float maxSpeedKmh;
        public float minPitch, maxPitch;
        public float minVolume, maxVolume;
    }

    private static readonly Dictionary<AudioProfile, ProfileData> Profiles = new Dictionary<AudioProfile, ProfileData>
    {
        { AudioProfile.Sedan, new ProfileData {
            clipPath = "Audio/Engine/Engine_Generic_Idle",
            maxSpeedKmh = 140f, minPitch = 0.8f, maxPitch = 2.0f,
            minVolume = 0.25f, maxVolume = 0.6f }},
        { AudioProfile.SUV, new ProfileData {
            clipPath = "Audio/Engine/Engine_SUV_Idle",
            maxSpeedKmh = 120f, minPitch = 0.7f, maxPitch = 1.8f,
            minVolume = 0.3f, maxVolume = 0.65f }},
        { AudioProfile.Bus, new ProfileData {
            clipPath = "Audio/Engine/Engine_Bus_Idle",
            maxSpeedKmh = 80f, minPitch = 0.6f, maxPitch = 1.5f,
            minVolume = 0.3f, maxVolume = 0.7f }},
        { AudioProfile.Truck, new ProfileData {
            clipPath = "Audio/Engine/Engine_Truck",
            maxSpeedKmh = 90f, minPitch = 0.6f, maxPitch = 1.5f,
            minVolume = 0.3f, maxVolume = 0.7f }},
    };

    private AudioSource _audio;
    private Rigidbody _rb;
    private GameObject _player;
    private ProfileData _data;
    private AudioProfile _activeProfile;
    private float _currentPitch;
    private float _currentVolume;
    private float _pitchVelocity;
    private float _volumeVelocity;
    private float _maxMotorTorque;
    private WheelCollider _motorWheel;
    private bool _ready;

    private const float SMOOTH_TIME = 0.15f;

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        StopEngine();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[PlayerEngineSound] sceneLoaded: {scene.name}");
        StopEngine();
        _ready = false;
    }

    void StopEngine()
    {
        if (_audio != null && _audio.isPlaying)
            _audio.Stop();
        _player = null;
        _rb = null;
        _motorWheel = null;
    }

    bool TryAttach()
    {
        var player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            // Fallback: buscar por nombre comun
            var candidates = new[] { "Player", "player", "PlayerCar", "Car" };
            foreach (var name in candidates)
            {
                player = GameObject.Find(name);
                if (player != null) break;
            }
            // Fallback: buscar por componente PlayerCar
            if (player == null)
            {
                var allRbs = Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None);
                foreach (var r in allRbs)
                {
                    if (r.GetComponent("PlayerCar") != null)
                    {
                        player = r.gameObject;
                        break;
                    }
                }
            }
            if (player == null)
            {
                if (Time.frameCount % 300 == 0)
                    Debug.LogWarning("[PlayerEngineSound] No se encontro Player (ni por tag, ni por nombre, ni por PlayerCar)");
                return false;
            }
            Debug.Log($"[PlayerEngineSound] Player encontrado por fallback: '{player.name}'");
        }

        var rb = player.GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogWarning($"[PlayerEngineSound] Player '{player.name}' no tiene Rigidbody");
            _ready = true;
            return false;
        }

        string sceneName = SceneManager.GetActiveScene().name;
        var profile = DetectProfile(player.name, sceneName);
        Debug.Log($"[PlayerEngineSound] Player='{player.name}', escena='{sceneName}', perfil={profile}");

        if (profile == AudioProfile.None)
        {
            _ready = true;
            return false;
        }

        var data = Profiles[profile];
        var clip = Resources.Load<AudioClip>(data.clipPath);
        if (clip == null)
        {
            Debug.LogWarning($"[PlayerEngineSound] Clip no encontrado: {data.clipPath}");
            _ready = true;
            return false;
        }

        _player = player;
        _rb = rb;
        _data = data;
        _activeProfile = profile;

        if (_audio == null)
            _audio = gameObject.AddComponent<AudioSource>();

        _audio.clip = clip;
        _audio.loop = true;
        _audio.spatialBlend = 0f;
        _audio.playOnAwake = false;
        _audio.volume = _data.minVolume;
        _audio.pitch = _data.minPitch;
        _currentPitch = _data.minPitch;
        _currentVolume = _data.minVolume;

        CacheMotorWheel(player);

        _audio.Play();
        _ready = true;
        Debug.Log($"[PlayerEngineSound] Clip '{clip.name}' ({clip.length:F1}s), isPlaying={_audio.isPlaying}");
        return true;
    }

    void CacheMotorWheel(GameObject player)
    {
        _maxMotorTorque = 500f;
        _motorWheel = null;

        var pc = player.GetComponent("PlayerCar");
        if (pc != null)
        {
            var field = pc.GetType().GetField("maxMotorTorque");
            if (field != null)
                _maxMotorTorque = (float)field.GetValue(pc);
        }

        var wheels = player.GetComponentsInChildren<WheelCollider>();
        if (wheels.Length > 0)
            _motorWheel = wheels[0];
    }

    void Update()
    {
        if (!_ready)
        {
            TryAttach();
            return;
        }

        if (_player == null || _rb == null || _audio == null || !_audio.isPlaying)
            return;

#if UNITY_6000_0_OR_NEWER
        float speedKmh = _rb.linearVelocity.magnitude * 3.6f;
#else
        float speedKmh = _rb.velocity.magnitude * 3.6f;
#endif

        float throttle = 0f;
        if (_motorWheel != null)
            throttle = Mathf.Clamp01(Mathf.Abs(_motorWheel.motorTorque) / _maxMotorTorque);

        float speedFactor = Mathf.Clamp01(speedKmh / _data.maxSpeedKmh);
        float combined = speedFactor * 0.7f + throttle * 0.3f;

        float targetPitch = Mathf.Lerp(_data.minPitch, _data.maxPitch, combined);
        float targetVolume = Mathf.Lerp(_data.minVolume, _data.maxVolume, combined);

        _currentPitch = Mathf.SmoothDamp(_currentPitch, targetPitch, ref _pitchVelocity, SMOOTH_TIME);
        _currentVolume = Mathf.SmoothDamp(_currentVolume, targetVolume, ref _volumeVelocity, SMOOTH_TIME);

        _audio.pitch = _currentPitch;
        _audio.volume = _currentVolume;
    }

    static AudioProfile DetectProfile(string goName, string sceneName)
    {
        string lower = goName.ToLowerInvariant();

        if (lower.Contains("bus")) return AudioProfile.Bus;
        if (lower.Contains("camion") || lower.Contains("truck")) return AudioProfile.Truck;
        if (lower.Contains("camioneta") || lower.Contains("suv")) return AudioProfile.SUV;
        if (lower.Contains("moto")) return AudioProfile.None;

        switch (sceneName)
        {
            case "BusPasajeros": return AudioProfile.Bus;
            case "CamionDCarga": return AudioProfile.Truck;
            case "Camioneta": return AudioProfile.SUV;
            case "Motocicleta": return AudioProfile.None;
            default: return AudioProfile.Sedan;
        }
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        StopEngine();
    }
}
