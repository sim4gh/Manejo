using System;
using System.IO;
using UnityEngine;

namespace TlaxSim.MotoSensitivity
{
    // Singleton de runtime. Carga moto_sensitivity.json del persistentDataPath
    // y expone Active (resolved preset). Soporta hot-reload via FileSystemWatcher
    // (agregado en Task 5).
    //
    // Métodos *FromPath son test-only — el production path usa el persistentDataPath.
    //
    // Ver docs/superpowers/specs/2026-05-13-moto-sensitivity-design.md sección 3 + 7bis.
    public class MotoSensitivityProvider
    {
        public const string FILE_NAME = "moto_sensitivity.json";
        public const string KILL_SWITCH_PREF = "MotoSens_Disabled";
        public const int SUPPORTED_SCHEMA_VERSION = 1;

        static readonly string[] ValidPresets = { "Principiante", "Normal", "Realista", "Custom" };

        public static MotoSensitivityProvider Instance { get; private set; }

        public MotoSensitivity Loaded { get; private set; }
        public MotoPreset Active { get; private set; }
        public bool IsLoaded => Loaded != null;
        public bool IsKillSwitchOn => PlayerPrefs.GetInt(KILL_SWITCH_PREF, 0) != 0;

        // Notifica cada vez que Active cambia (Reload manual, Save desde F11,
        // o hot-reload de FileSystemWatcher en Task 5). Suscritos: UIInputNew.
        public event Action OnReloaded;

        string _jsonPath;

        public static string DefaultJsonPath()
        {
            return Path.Combine(Application.persistentDataPath, FILE_NAME);
        }

        FileSystemWatcher _watcher;
        // _lastWatcherEventTicks se actualiza en thread-pool y se lee en main thread.
        // Usar Interlocked.Exchange/Read para acceso atómico en ambos lados.
        long _lastWatcherEventTicks;
        const float DEBOUNCE_SECONDS = 0.5f;
        // El parse debe correr en main thread (Unity API). El watcher fires en
        // thread-pool — encolamos un flag y main-thread lo consume en Tick().
        volatile bool _pendingReload;
        // Estado entre frames del retry loop. Permite spread de reintentos sin
        // bloquear el main thread con Thread.Sleep.
        int _reloadAttempts;
        float _nextRetryAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Bootstrap()
        {
            Instance = new MotoSensitivityProvider();
            Instance._jsonPath = DefaultJsonPath();
            Instance.Loaded = LoadOrInitializeFromPath(Instance._jsonPath);
            Instance.Active = Instance.Loaded != null ? ResolveActive(Instance.Loaded) : null;
            Debug.Log($"[MotoSensitivity] Bootstrap: path={Instance._jsonPath} loaded={Instance.Loaded != null} preset={Instance.Loaded?.activePreset ?? "<null>"} killSwitch={Instance.IsKillSwitchOn}");
            Instance.StartWatcher();

            // Crear GameObject Persistent para tickear el reload pendiente cada frame.
            var go = new GameObject("[MotoSensitivityProvider]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<MotoSensitivityTicker>();
        }

        void StartWatcher()
        {
            try
            {
                string dir = Path.GetDirectoryName(_jsonPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                _watcher = new FileSystemWatcher(dir, FILE_NAME)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
                };
                _watcher.Changed += OnFileChangedThreadPool;
                _watcher.Created += OnFileChangedThreadPool;
                _watcher.Renamed += (s, e) => OnFileChangedThreadPool(s, null);
                _watcher.EnableRaisingEvents = true;
                Debug.Log($"[MotoSensitivity] FileSystemWatcher activo en {dir}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MotoSensitivity] No se pudo iniciar FileSystemWatcher: {e.Message}. SCP hot-reload deshabilitado; cambios requerirán restart de Unity.");
            }
        }

        void OnFileChangedThreadPool(object sender, FileSystemEventArgs e)
        {
            System.Threading.Interlocked.Exchange(ref _lastWatcherEventTicks, DateTime.UtcNow.Ticks);
            _pendingReload = true;
        }

        // Llamado cada frame por MotoSensitivityTicker (main thread).
        // Implementa debounce + reintentos frame-spread (sin Thread.Sleep en main thread).
        //
        // Race protection: capturamos los ticks observados ANTES del reload y al
        // limpiar _pendingReload chequeamos que no haya llegado un evento nuevo
        // durante el reload. Si llegó uno nuevo, dejamos _pendingReload=true para
        // re-procesar en el próximo Tick.
        public void Tick()
        {
            if (!_pendingReload) return;

            long observedTicks = System.Threading.Interlocked.Read(ref _lastWatcherEventTicks);
            var lastEvent = new DateTime(observedTicks, DateTimeKind.Utc);

            // Debounce: si el último evento fue muy reciente, esperar.
            if ((DateTime.UtcNow - lastEvent).TotalSeconds < DEBOUNCE_SECONDS) return;

            // Si estamos en cooldown entre reintentos, esperar.
            if (Time.unscaledTime < _nextRetryAt) return;

            _reloadAttempts++;
            const int MAX_ATTEMPTS = 3;
            const float RETRY_DELAY_SECONDS = 0.1f;

            var reloaded = LoadOrInitializeFromPath(_jsonPath);
            if (reloaded != null)
            {
                _reloadAttempts = 0;
                Loaded = reloaded;
                Active = ResolveActive(reloaded);
                // Solo limpiar _pendingReload si no llegó un evento nuevo durante el reload.
                long currentTicks = System.Threading.Interlocked.Read(ref _lastWatcherEventTicks);
                if (currentTicks == observedTicks)
                {
                    _pendingReload = false;
                }
                else
                {
                    Debug.Log("[MotoSensitivity] Evento nuevo durante reload; reprocesará próximo Tick.");
                }
                Debug.Log($"[MotoSensitivity] Hot-reload OK: preset={reloaded.activePreset} lastModBy={reloaded.lastModifiedBy}");
                OnReloaded?.Invoke();
                return;
            }

            if (_reloadAttempts >= MAX_ATTEMPTS)
            {
                Debug.LogWarning($"[MotoSensitivity] Reload por FileSystemWatcher falló tras {MAX_ATTEMPTS} intentos. Manteniendo Active anterior.");
                _reloadAttempts = 0;
                // Igual: solo limpiar si no hay evento nuevo en cola.
                long currentTicks = System.Threading.Interlocked.Read(ref _lastWatcherEventTicks);
                if (currentTicks == observedTicks)
                {
                    _pendingReload = false;
                }
                return;
            }

            // Schedule next retry en próximo frame + delay.
            _nextRetryAt = Time.unscaledTime + RETRY_DELAY_SECONDS;
        }

        public void Reload()
        {
            Loaded = LoadOrInitializeFromPath(_jsonPath);
            Active = Loaded != null ? ResolveActive(Loaded) : null;
            Debug.Log($"[MotoSensitivity] Reload: loaded={Loaded != null} preset={Loaded?.activePreset ?? "<null>"}");
            OnReloaded?.Invoke();
        }

        public void Save(MotoSensitivity sens)
        {
            sens.lastModifiedAt = DateTime.UtcNow.ToString("o");
            SaveAtomicToPath(_jsonPath, sens);
            Loaded = sens;
            Active = ResolveActive(sens);
            // CRÍTICO: notificar a los suscriptores (UIInputNew.RecacheMotoSensitivity)
            // para que el cambio del F11 panel se aplique en el siguiente frame.
            OnReloaded?.Invoke();
        }

        // ---- Static helpers (testable in EditMode) ----

        public static MotoSensitivity LoadOrInitializeFromPath(string path)
        {
            if (!File.Exists(path))
            {
                var fresh = MotoSensitivityDefaults.NewWithRealistaActive();
                try
                {
                    SaveAtomicToPath(path, fresh);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[MotoSensitivity] No se pudo escribir JSON inicial en {path}: {e.Message}. Continuando con defaults en memoria.");
                }
                return fresh;
            }

            try
            {
                string text = File.ReadAllText(path);
                var loaded = JsonUtility.FromJson<MotoSensitivity>(text);
                if (loaded == null) return null;
                if (loaded.schemaVersion != SUPPORTED_SCHEMA_VERSION)
                {
                    Debug.LogWarning($"[MotoSensitivity] schemaVersion={loaded.schemaVersion} no soportado (esperado {SUPPORTED_SCHEMA_VERSION}). Fallback legacy.");
                    return null;
                }
                if (System.Array.IndexOf(ValidPresets, loaded.activePreset) < 0)
                {
                    Debug.LogWarning($"[MotoSensitivity] activePreset='{loaded.activePreset}' inválido. Fallback legacy.");
                    return null;
                }
                if (loaded.presets == null || loaded.presets.Realista == null)
                {
                    Debug.LogWarning("[MotoSensitivity] presets faltantes. Fallback legacy.");
                    return null;
                }
                return loaded;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MotoSensitivity] Parse falló en {path}: {e.Message}. Fallback legacy.");
                return null;
            }
        }

        public static void SaveAtomicToPath(string path, MotoSensitivity sens)
        {
            string tmp = path + ".tmp";
            string text = JsonUtility.ToJson(sens, true);
            File.WriteAllText(tmp, text);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        public static MotoPreset ResolveActive(MotoSensitivity sens)
        {
            if (sens == null || sens.presets == null) return MotoSensitivityDefaults.Normal();
            switch (sens.activePreset)
            {
                case "Principiante": return sens.presets.Principiante ?? MotoSensitivityDefaults.Principiante();
                case "Normal":       return sens.presets.Normal       ?? MotoSensitivityDefaults.Normal();
                case "Realista":     return sens.presets.Realista     ?? MotoSensitivityDefaults.Realista();
                case "Custom":       return sens.custom               ?? MotoSensitivityDefaults.Normal();
                default:             return MotoSensitivityDefaults.Normal();
            }
        }
    }

    // Component invisible que tickea el reload pendiente del provider en main thread.
    public class MotoSensitivityTicker : MonoBehaviour
    {
        void Update()
        {
            if (MotoSensitivityProvider.Instance != null)
                MotoSensitivityProvider.Instance.Tick();
        }
    }
}
