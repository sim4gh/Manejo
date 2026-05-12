using System;
using UnityEngine;

namespace TlaxSim.G923Calibration
{
    // Traduce calibración G923 legacy (PlayerPrefs G923_* + Bind_*) a JSON.
    // Detecta variant por displayName en Cal_DeviceFingerprint o, en su
    // ausencia, por el path de gas (z=PS, stick/y=Xbox).
    //
    // Llamado por G923ControlMapping.LoadFromDisk() al primer flip de
    // G923_UseJsonMapping=1 (si no hay JSON aún). Si validation falla, NO
    // produce JSON — el operator tiene que calibrar manual via F8.
    public static class G923MappingMigration
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

        public static bool TryMigrateFromPlayerPrefs(out G923Mapping migrated)
            => TryMigrateFromPlayerPrefs(new UnityPrefsReader(), out migrated);

        public static bool TryMigrateFromPlayerPrefs(IPlayerPrefsReader prefs, out G923Mapping migrated)
        {
            migrated = null;

            string fingerprint = prefs.GetString("Cal_DeviceFingerprint", "");
            string gasAxis = prefs.GetString("G923_GasAxis", "");
            string brakeAxis = prefs.GetString("G923_BrakeAxis", "");
            string clutchAxis = prefs.GetString("G923_ClutchAxis", "");

            // Detectar variant. Preferimos fingerprint; fallback al gasAxis.
            string variant;
            if (fingerprint.IndexOf("Xbox", StringComparison.OrdinalIgnoreCase) >= 0)
                variant = "Xbox";
            else if (fingerprint.IndexOf("PlayStation", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     fingerprint.IndexOf("Logitech", StringComparison.OrdinalIgnoreCase) >= 0)
                variant = "PS";
            else if (gasAxis == "stick/y")
                variant = "Xbox";
            else if (gasAxis == "z")
                variant = "PS";
            else
            {
                Debug.LogWarning($"[G923Migration] No puedo detectar variant (fp='{fingerprint}', gas='{gasAxis}') — abort");
                return false;
            }

            // Validar rest/press contra canonicals per variant. ±0.1 de tolerance.
            float gasRest = prefs.GetFloat("G923_GasRest", float.NaN);
            float gasPress = prefs.GetFloat("G923_GasPress", float.NaN);
            float brakeRest = prefs.GetFloat("G923_BrakeRest", float.NaN);
            float brakePress = prefs.GetFloat("G923_BrakePress", float.NaN);

            if (!ValidatePedal(variant, "gas", gasRest, gasPress)) return false;
            if (!ValidatePedal(variant, "brake", brakeRest, brakePress)) return false;

            var m = new G923Mapping
            {
                schemaVersion = G923ControlMapping.CURRENT_SCHEMA,
                variant = variant,
                deviceFingerprint = fingerprint,
                calibratedAt = DateTime.UtcNow.ToString("o"),
                calibratedBy = "auto-migrated"
            };

            m.axes.steer.path = prefs.GetString("Bind_steerAxis", "stick/x");
            m.axes.steer.center = prefs.GetFloat("G923_SteerCenter", 0f);
            m.axes.steer.leftMax = prefs.GetFloat("G923_SteerMin", -0.95f);
            m.axes.steer.rightMax = prefs.GetFloat("G923_SteerMax", 0.95f);

            m.axes.gas.path = gasAxis;
            m.axes.gas.rest = gasRest;
            m.axes.gas.press = gasPress;

            m.axes.brake.path = brakeAxis;
            m.axes.brake.rest = brakeRest;
            m.axes.brake.press = brakePress;

            // Clutch puede no estar calibrado (modo Auto). Solo migra si existe.
            if (!string.IsNullOrEmpty(clutchAxis))
            {
                m.axes.clutch.path = clutchAxis;
                m.axes.clutch.rest = prefs.GetFloat("G923_ClutchRest", variant == "Xbox" ? 1f : -1f);
                m.axes.clutch.press = prefs.GetFloat("G923_ClutchPress", variant == "Xbox" ? -1f : 1f);
                m.axes.clutch.required = false;  // solo required en modo Manual; el preflight check lo evalúa según TransmisionManual
            }
            else
            {
                // Default canonical por variant para que F8 panel tenga sugerencia
                m.axes.clutch.path = variant == "Xbox" ? "rz" : "stick/y";
                m.axes.clutch.rest = variant == "Xbox" ? 1f : -1f;
                m.axes.clutch.press = variant == "Xbox" ? -1f : 1f;
                m.axes.clutch.required = false;
            }

            // Buttons: leer Bind_* legacy con defaults canónicos per variant
            string defaultReverse = variant == "Xbox" ? "button12" : "button19";
            m.buttons.reverse.path = prefs.GetString("Bind_reverse", defaultReverse);
            m.buttons.reverse.kind = "hold";

            m.buttons.horn.path = prefs.GetString("Bind_horn", "button14");
            m.buttons.hazards.path = prefs.GetString("Bind_hazard", "");  // combo L1+R1 en G923 — vacío si no se setea
            m.buttons.turnLeft.path = prefs.GetString("Bind_paddleLeft", "button5");
            m.buttons.turnRight.path = prefs.GetString("Bind_paddleRight", "button6");

            m.buttons.gear1.path = prefs.GetString("Bind_gear1", "button13");
            m.buttons.gear2.path = prefs.GetString("Bind_gear2", "button14");
            m.buttons.gear3.path = prefs.GetString("Bind_gear3", "button15");
            m.buttons.gear4.path = prefs.GetString("Bind_gear4", "button16");
            m.buttons.gear5.path = prefs.GetString("Bind_gear5", "button17");
            m.buttons.gear6.path = prefs.GetString("Bind_gear6", "button18");

            // FFB: defaults canónicos. Adv_* PlayerPrefs son F9 territory (out of scope v1.8.0).
            m.ffb.available = true;
            m.ffb.constantForceMaxPct = 1f;
            m.ffb.bumpyRoadMaxPct = 0.8f;

            migrated = m;
            return true;
        }

        // Tolerance ±0.1 vs canonical per variant.
        private static bool ValidatePedal(string variant, string role, float rest, float press)
        {
            if (float.IsNaN(rest) || float.IsNaN(press))
            {
                Debug.LogWarning($"[G923Migration] {role} rest/press NaN — abort");
                return false;
            }

            // PS: gas/brake/clutch all idle=+1 except clutch (idle=-1)
            // Xbox: brake idle=+1, gas/clutch idle=-1 / variations
            // Aplicamos chequeo blando: rest|press deben ser cercanos a ±1 (no 0).
            if (Mathf.Abs(Mathf.Abs(rest) - 1f) > 0.1f || Mathf.Abs(Mathf.Abs(press) - 1f) > 0.1f)
            {
                Debug.LogWarning($"[G923Migration] {role} rest={rest} press={press} fuera de ±1±0.1 — corrupto, abort");
                return false;
            }
            if (Mathf.Sign(rest) == Mathf.Sign(press))
            {
                Debug.LogWarning($"[G923Migration] {role} rest y press mismo signo — abort");
                return false;
            }
            return true;
        }
    }
}
