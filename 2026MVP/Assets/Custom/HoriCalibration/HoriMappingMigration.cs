using UnityEngine;

namespace TlaxSim.HoriCalibration
{
    // Interfaz de lectura de PlayerPrefs — permite inyectar fake en tests.
    public interface IPlayerPrefsReader
    {
        string GetString(string key, string defaultValue);
        float GetFloat(string key, float defaultValue);
    }

    // Wrapper sobre UnityEngine.PlayerPrefs (usado en runtime).
    public class UnityPlayerPrefsReader : IPlayerPrefsReader
    {
        public string GetString(string key, string def) => PlayerPrefs.GetString(key, def);
        public float GetFloat(string key, float def) => PlayerPrefs.GetFloat(key, def);
    }

    public static class HoriMappingMigration
    {
        private const float REST_EPSILON = 0.1f;
        private const float REST_CANON = -1f;
        private const float PRESS_CANON = 1f;

        // Overload de conveniencia para runtime — usa UnityPlayerPrefsReader real.
        public static bool TryMigrateFromPlayerPrefs(out HoriMapping mapping)
            => TryMigrateFromPlayerPrefs(new UnityPlayerPrefsReader(), out mapping);

        public static bool TryMigrateFromPlayerPrefs(IPlayerPrefsReader prefs, out HoriMapping mapping)
        {
            mapping = null;

            // Detección "es HORI?": fingerprint contains HORI OR Bind_reverse canónico.
            string fp = prefs.GetString("Cal_DeviceFingerprint", "");
            string reverse = prefs.GetString("Bind_reverse", "");
            bool fingerprintSaysHori = fp.IndexOf("HORI", System.StringComparison.OrdinalIgnoreCase) >= 0;
            bool reverseIsCanonical = reverse == "shifter:button7";
            if (!fingerprintSaysHori && !reverseIsCanonical)
            {
                Debug.Log("[HoriMappingMigration] No es HORI (fp/reverse no matchea) — skip");
                return false;
            }

            var m = new HoriMapping
            {
                deviceFingerprint = fp,
                calibratedBy = "auto-migrated",
                calibratedAt = System.DateTime.UtcNow.ToString("o"),
                wheelVID = "0F0D",
                wheelPID = "017A",
                shifterVID = "0F0D",
                shifterPID = "0186"
            };

            // Axes
            m.axes.steer.path = prefs.GetString("Bind_steerAxis", "stick/x");
            m.axes.steer.center = prefs.GetFloat("G923_SteerCenter", 0f);
            m.axes.steer.leftMax = prefs.GetFloat("G923_SteerMin", -1f);
            m.axes.steer.rightMax = prefs.GetFloat("G923_SteerMax", 1f);

            m.axes.gas.source = "HoriThrottleReader";
            m.axes.gas.verifyThreshold = 0.7f;

            m.axes.brake.path = prefs.GetString("G923_BrakeAxis", "rz");
            m.axes.brake.rest = prefs.GetFloat("G923_BrakeRest", REST_CANON);
            m.axes.brake.press = prefs.GetFloat("G923_BrakePress", PRESS_CANON);
            m.axes.brake.required = true;

            m.axes.clutch.path = prefs.GetString("G923_ClutchAxis", "slider");
            m.axes.clutch.rest = prefs.GetFloat("G923_ClutchRest", REST_CANON);
            m.axes.clutch.press = prefs.GetFloat("G923_ClutchPress", PRESS_CANON);
            m.axes.clutch.required = true;

            // Buttons
            m.buttons.horn.path = prefs.GetString("Bind_horn", "");
            m.buttons.hazards.path = prefs.GetString("Bind_hazard", "shifter:button27");
            m.buttons.turnLeft.path = prefs.GetString("Bind_paddleLeft", "wheel:button40");
            m.buttons.turnRight.path = prefs.GetString("Bind_paddleRight", "wheel:button41");
            m.buttons.reverse.path = prefs.GetString("Bind_reverse", "shifter:button7");

            m.buttons.gear1.path = prefs.GetString("Bind_gear1", "shifter:trigger");
            m.buttons.gear2.path = prefs.GetString("Bind_gear2", "shifter:button2");
            m.buttons.gear3.path = prefs.GetString("Bind_gear3", "shifter:button3");
            m.buttons.gear4.path = prefs.GetString("Bind_gear4", "shifter:button4");
            m.buttons.gear5.path = prefs.GetString("Bind_gear5", "shifter:button5");
            m.buttons.gear6.path = prefs.GetString("Bind_gear6", "shifter:button6");

            // Validar required fields. Abort si cualquiera falla.
            if (!ValidateRequired(m))
            {
                Debug.LogWarning("[HoriMappingMigration] Validación required falla — no migra");
                return false;
            }

            mapping = m;
            return true;
        }

        private static bool ValidateRequired(HoriMapping m)
        {
            // brake rest/press cerca de canónico ±EPSILON
            if (Mathf.Abs(m.axes.brake.rest - REST_CANON) > REST_EPSILON) return Fail("brake.rest");
            if (Mathf.Abs(m.axes.brake.press - PRESS_CANON) > REST_EPSILON) return Fail("brake.press");
            // brake path en allowlist
            if (!IsAllowedPedalPath(m.axes.brake.path)) return Fail("brake.path");

            // clutch idem
            if (Mathf.Abs(m.axes.clutch.rest - REST_CANON) > REST_EPSILON) return Fail("clutch.rest");
            if (Mathf.Abs(m.axes.clutch.press - PRESS_CANON) > REST_EPSILON) return Fail("clutch.press");
            if (!IsAllowedPedalPath(m.axes.clutch.path)) return Fail("clutch.path");

            // steer path hardcoded
            if (m.axes.steer.path != "stick/x") return Fail("steer.path");

            // reverse: must start with "shifter:"
            if (!m.buttons.reverse.path.StartsWith("shifter:")) return Fail("reverse.path");

            // turnLeft/turnRight: must start with "wheel:"
            if (!m.buttons.turnLeft.path.StartsWith("wheel:")) return Fail("turnLeft.path");
            if (!m.buttons.turnRight.path.StartsWith("wheel:")) return Fail("turnRight.path");

            // hazards: must start with "shifter:"
            if (!m.buttons.hazards.path.StartsWith("shifter:")) return Fail("hazards.path");

            // fingerprint contains HORI
            if (m.deviceFingerprint.IndexOf("HORI", System.StringComparison.OrdinalIgnoreCase) < 0)
                return Fail("deviceFingerprint");

            // Gears son opcional (required:false). No validamos contenidos.
            // Horn puede quedar vacío post-migración si no se descubrió antes — operador
            // tendrá que detectarlo en F8 manualmente. NO bloquea la migración aquí.

            return true;
        }

        private static bool IsAllowedPedalPath(string path)
            => path == "rz" || path == "slider" || path == "slider1";

        private static bool Fail(string field)
        {
            Debug.LogWarning($"[HoriMappingMigration] Validation failed: {field}");
            return false;
        }
    }
}
