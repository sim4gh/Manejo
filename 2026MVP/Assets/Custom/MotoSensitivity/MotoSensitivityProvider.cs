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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Bootstrap()
        {
            Instance = new MotoSensitivityProvider();
            Instance._jsonPath = DefaultJsonPath();
            Instance.Loaded = LoadOrInitializeFromPath(Instance._jsonPath);
            Instance.Active = Instance.Loaded != null ? ResolveActive(Instance.Loaded) : null;
            Debug.Log($"[MotoSensitivity] Bootstrap: path={Instance._jsonPath} loaded={Instance.Loaded != null} preset={Instance.Loaded?.activePreset ?? "<null>"} killSwitch={Instance.IsKillSwitchOn}");
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
}
