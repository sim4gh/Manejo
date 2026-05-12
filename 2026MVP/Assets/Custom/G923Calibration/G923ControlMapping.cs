using System;
using System.IO;
using UnityEngine;

namespace TlaxSim.G923Calibration
{
    // Static singleton: source-of-truth en memoria de la calibración G923.
    // Solo G923CalibrationPanel debe llamar Save(). UIInputNew lee Active al
    // boot vía AttachToWheelDevice CUANDO flag G923_UseJsonMapping == 1.
    //
    // Spec: docs/superpowers/specs/2026-05-11-g923-v180-immutable-calibration-design.md
    public static class G923ControlMapping
    {
        public const int CURRENT_SCHEMA = 1;
        public const string FILENAME = "g923_mapping.json";
        public const string PREF_USE_JSON = "G923_UseJsonMapping";  // 0=legacy, 1=JSON mode

        public static G923Mapping Active { get; private set; }

        private static string _storagePathOverride;
        private static string StoragePath
        {
            get
            {
                string root = _storagePathOverride ?? Application.persistentDataPath;
                return Path.Combine(root, FILENAME);
            }
        }

        // Test hooks (no usar en runtime).
        public static void OverrideStoragePathForTests(string dir) => _storagePathOverride = dir;
        public static void ResetForTests() => Active = null;

        // Helper centralizado para el feature flag. Cualquier consumidor (runtime
        // o test) debe ir por aquí en vez de leer PlayerPrefs directo.
        public static bool IsJsonModeEnabled() => PlayerPrefs.GetInt(PREF_USE_JSON, 0) == 1;
        public static void SetJsonMode(bool enabled)
        {
            PlayerPrefs.SetInt(PREF_USE_JSON, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void LoadFromDisk()
        {
            // Si flag=0 (legacy), nunca leemos JSON — Active queda null y los
            // consumers caen al path PlayerPrefs.
            if (!IsJsonModeEnabled())
            {
                Active = null;
                return;
            }

            string path = StoragePath;
            if (!File.Exists(path))
            {
                // Primera vez con flag=1: intentar migración desde PlayerPrefs.
                if (G923MappingMigration.TryMigrateFromPlayerPrefs(out var migrated))
                {
                    Save(migrated);
                    Debug.Log("[G923ControlMapping] Migrated from PlayerPrefs to JSON");
                    return;
                }
                Active = null;
                Debug.LogWarning("[G923ControlMapping] Flag=1 pero no hay JSON y migración falló — Active=null");
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                var parsed = JsonUtility.FromJson<G923Mapping>(json);
                if (parsed == null || parsed.schemaVersion != CURRENT_SCHEMA)
                {
                    Debug.LogWarning($"[G923ControlMapping] Schema desconocido {parsed?.schemaVersion}, Active=null");
                    Active = null;
                    return;
                }
                Active = parsed;
                Debug.Log($"[G923ControlMapping] Loaded from {path} (variant={Active.variant}, fp={Active.deviceFingerprint})");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[G923ControlMapping] JSON corrupto en {path}: {e.Message}");
                Active = null;
            }
        }

        // Write atómico: .tmp → rename. Garantiza que un crash mid-write
        // deja el archivo original intacto.
        public static void Save(G923Mapping mapping)
        {
            if (mapping == null) throw new ArgumentNullException(nameof(mapping));
            mapping.schemaVersion = CURRENT_SCHEMA;
            if (string.IsNullOrEmpty(mapping.calibratedAt))
                mapping.calibratedAt = DateTime.UtcNow.ToString("o");

            string path = StoragePath;
            string tmpPath = path + ".tmp";

            Directory.CreateDirectory(Path.GetDirectoryName(path));

            string json = JsonUtility.ToJson(mapping, prettyPrint: true);
            File.WriteAllText(tmpPath, json);

            if (File.Exists(path)) File.Delete(path);
            File.Move(tmpPath, path);

            Active = mapping;
            Debug.Log($"[G923ControlMapping] Saved to {path}");
        }

        // Borra el JSON file. Active queda null. Útil para F8 "Reset" y tests.
        // NO toca PlayerPrefs (rollback siempre disponible).
        public static void Clear()
        {
            string path = StoragePath;
            if (File.Exists(path)) File.Delete(path);
            Active = null;
        }
    }
}
