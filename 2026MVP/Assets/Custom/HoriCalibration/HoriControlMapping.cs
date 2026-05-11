using System;
using System.IO;
using UnityEngine;

namespace TlaxSim.HoriCalibration
{
    // Static singleton: source-of-truth en memoria de la calibración HORI.
    // Solo HoriCalibrationPanel debe llamar Save(). UIInputNew lee Active al
    // boot vía AttachToWheelDevice.
    //
    // Ver docs/superpowers/specs/2026-05-11-hori-v170-immutable-calibration-design.md
    public static class HoriControlMapping
    {
        public const int CURRENT_SCHEMA = 1;
        public const string FILENAME = "hori_mapping.json";

        public static HoriMapping Active { get; private set; }

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

        public static void LoadFromDisk()
        {
            string path = StoragePath;
            if (!File.Exists(path))
            {
                Active = null;
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                var parsed = JsonUtility.FromJson<HoriMapping>(json);
                if (parsed == null || parsed.schemaVersion != CURRENT_SCHEMA)
                {
                    Debug.LogWarning($"[HoriControlMapping] Schema desconocido {parsed?.schemaVersion}, Active=null");
                    Active = null;
                    return;
                }
                Active = parsed;
                Debug.Log($"[HoriControlMapping] Loaded from {path} (fp={Active.deviceFingerprint})");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HoriControlMapping] JSON corrupto en {path}: {e.Message}");
                Active = null;
            }
        }

        // Write atómico: .tmp → rename. Garantiza que un crash mid-write
        // deja el archivo original intacto (sin partial state).
        public static void Save(HoriMapping mapping)
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
            Debug.Log($"[HoriControlMapping] Saved to {path}");
        }

        // Borra el archivo y deja Active=null. Usado por F8 "Reset" y por tests.
        public static void Clear()
        {
            string path = StoragePath;
            if (File.Exists(path)) File.Delete(path);
            Active = null;
        }
    }
}
