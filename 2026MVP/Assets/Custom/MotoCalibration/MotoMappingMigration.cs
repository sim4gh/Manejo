using System;
using UnityEngine;

namespace TlaxSim.MotoCalibration
{
    // Traduce calibración Moto legacy (PlayerPrefs MOTO_*) a JSON.
    //
    // Llamado por MotoControlMapping.LoadFromDisk() al primer flip de
    // Moto_UseJsonMapping=1 (si no hay JSON aún). Si validation falla, NO
    // produce JSON — el operator tiene que calibrar manual via F8.
    //
    // Validación estricta:
    // - fingerprint debe contener "Moto" o "SimuladoresTlax" (sino aborta)
    // - lean/hbar range span ≥ 0.5 (sino aborta — calibración corrupta)
    // - NaN en cualquier range → aborta
    // - gas rest/press: NO se leen de PlayerPrefs — hardcoded canónico -1/+1
    public static class MotoMappingMigration
    {
        public interface IPlayerPrefsReader
        {
            string GetString(string key, string defaultValue);
            float GetFloat(string key, float defaultValue);
            int GetInt(string key, int defaultValue);
        }

        private sealed class UnityPrefsReader : IPlayerPrefsReader
        {
            public string GetString(string k, string d) => PlayerPrefs.GetString(k, d);
            public float GetFloat(string k, float d) => PlayerPrefs.GetFloat(k, d);
            public int GetInt(string k, int d) => PlayerPrefs.GetInt(k, d);
        }

        public static bool TryMigrateFromPlayerPrefs(out MotoMapping migrated)
            => TryMigrateFromPlayerPrefs(new UnityPrefsReader(), out migrated);

        public static bool TryMigrateFromPlayerPrefs(IPlayerPrefsReader prefs, out MotoMapping migrated)
        {
            migrated = null;

            string fingerprint = prefs.GetString("MOTO_DeviceFingerprint", "");
            if (string.IsNullOrEmpty(fingerprint))
            {
                Debug.LogWarning("[MotoMigration] MOTO_DeviceFingerprint vacío — abort");
                return false;
            }

            // Validar que es Moto, no un wheel genérico que contaminó las prefs
            bool hasMotoMarker =
                fingerprint.IndexOf("Moto", StringComparison.OrdinalIgnoreCase) >= 0 ||
                fingerprint.IndexOf("SimuladoresTlax", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!hasMotoMarker)
            {
                Debug.LogWarning($"[MotoMigration] Fingerprint '{fingerprint}' no contiene marcador Moto — abort");
                return false;
            }

            float leanMin = prefs.GetFloat("MOTO_LeanMin", float.NaN);
            float leanMax = prefs.GetFloat("MOTO_LeanMax", float.NaN);
            float hbarMin = prefs.GetFloat("MOTO_HbarMin", float.NaN);
            float hbarMax = prefs.GetFloat("MOTO_HbarMax", float.NaN);

            if (!ValidateRange("lean", leanMin, leanMax)) return false;
            if (!ValidateRange("handlebar", hbarMin, hbarMax)) return false;

            var m = new MotoMapping
            {
                schemaVersion = MotoControlMapping.CURRENT_SCHEMA,
                vehicleType = "motorcycle",
                deviceFingerprint = fingerprint,
                vid = "303A",
                pid = "4D54",
                calibratedAt = DateTime.UtcNow.ToString("o"),
                calibratedBy = "auto-migrated"
            };

            m.axes.lean.path = prefs.GetString("MOTO_LeanPath", "stick/x");
            m.axes.lean.min = leanMin;
            m.axes.lean.max = leanMax;
            m.axes.lean.center = (leanMin + leanMax) * 0.5f;

            m.axes.handlebar.path = prefs.GetString("MOTO_HbarPath", "stick/y");
            m.axes.handlebar.min = hbarMin;
            m.axes.handlebar.max = hbarMax;
            m.axes.handlebar.center = (hbarMin + hbarMax) * 0.5f;

            // Gas: hardcoded canónico para Hall throttle del simbt001 firmware.
            // Lección v1.8.1 G923: NO inferir desde PlayerPrefs/ReadUnprocessedValue.
            m.axes.gas.path = prefs.GetString("MOTO_GasPath", "rz");
            m.axes.gas.rest = -1f;
            m.axes.gas.press = 1f;

            // Buttons: brake/clutch con defaults canónicos (botones digitales del simbt001).
            m.buttons.brake.path = prefs.GetString("MOTO_BrakePath", "button1");
            m.buttons.brake.required = false;
            m.buttons.brake.kind = "hold";

            m.buttons.clutch.path = prefs.GetString("MOTO_ClutchPath", "button2");
            m.buttons.clutch.required = false;
            m.buttons.clutch.kind = "hold";

            migrated = m;
            return true;
        }

        // Range válido si: no NaN, min < max, span (max-min) ≥ 0.5.
        // El threshold 0.5 evita migrar calibraciones corruptas (e.g. operator
        // no recostó la moto a tope, captura quedó {-0.1, +0.1} → no usable).
        private static bool ValidateRange(string axisName, float min, float max)
        {
            if (float.IsNaN(min) || float.IsNaN(max))
            {
                Debug.LogWarning($"[MotoMigration] {axisName} min/max NaN — abort");
                return false;
            }
            if (min >= max)
            {
                Debug.LogWarning($"[MotoMigration] {axisName} min={min} >= max={max} — abort");
                return false;
            }
            float span = max - min;
            if (span < 0.5f)
            {
                Debug.LogWarning($"[MotoMigration] {axisName} span={span} < 0.5 (rango muy estrecho, calibración corrupta) — abort");
                return false;
            }
            return true;
        }
    }
}
