using System;
using UnityEngine;

namespace TlaxSim.G923Calibration
{
    // Data-only struct: calibración G923 persistida en disco como JSON.
    // Variants soportados: PS (button19=reverse) y Xbox (button12=reverse HOLD).
    // Compatible con JsonUtility de Unity (campos públicos, no propiedades).
    //
    // Schema doc: docs/superpowers/specs/2026-05-11-g923-v180-immutable-calibration-design.md
    [Serializable]
    public class G923Mapping
    {
        public int schemaVersion = 1;
        public string variant = "PS";  // "PS" | "Xbox"
        public string deviceFingerprint = "";
        public string calibratedAt = "";  // ISO 8601
        public string calibratedBy = "";  // "F8-panel" | "auto-migrated" | "manual-ssh"
        public G923Axes axes = new G923Axes();
        public G923Buttons buttons = new G923Buttons();
        public G923Ffb ffb = new G923Ffb();
    }

    [Serializable]
    public class G923Axes
    {
        public G923Steer steer = new G923Steer();
        public G923Pedal gas = new G923Pedal();
        public G923Pedal brake = new G923Pedal();
        public G923Pedal clutch = new G923Pedal { required = true };
    }

    [Serializable]
    public class G923Steer
    {
        public string path = "stick/x";
        public float center = 0f;
        public float leftMax = -0.95f;
        public float rightMax = 0.95f;
    }

    [Serializable]
    public class G923Pedal
    {
        public string path = "";
        public float rest = 1f;
        public float press = -1f;
        public bool required = false;
    }

    [Serializable]
    public class G923Buttons
    {
        public G923Button horn       = new G923Button { required = true };
        public G923Button hazards    = new G923Button { required = true };
        public G923Button turnLeft   = new G923Button { required = true };
        public G923Button turnRight  = new G923Button { required = true };
        public G923Button reverse    = new G923Button { required = true, kind = "hold" };
        public G923Button gear1 = new G923Button();
        public G923Button gear2 = new G923Button();
        public G923Button gear3 = new G923Button();
        public G923Button gear4 = new G923Button();
        public G923Button gear5 = new G923Button();
        public G923Button gear6 = new G923Button();
    }

    [Serializable]
    public class G923Button
    {
        public string path = "";
        public bool required = false;
        public string kind = "hold";  // "hold" | "pulse"
    }

    [Serializable]
    public class G923Ffb
    {
        public bool available = true;
        public float constantForceMaxPct = 1f;
        public float bumpyRoadMaxPct = 0.8f;
    }
}
