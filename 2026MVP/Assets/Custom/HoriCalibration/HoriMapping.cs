using System;
using UnityEngine;

namespace TlaxSim.HoriCalibration
{
    // Data-only struct representando la calibración HORI persistida en disco.
    // Compatible con JsonUtility de Unity (campos públicos, no propiedades).
    //
    // Schema documented en docs/superpowers/specs/2026-05-11-hori-v170-immutable-calibration-design.md
    [Serializable]
    public class HoriMapping
    {
        public int schemaVersion = 1;
        public string deviceFingerprint = "";
        public string wheelVID = "";
        public string wheelPID = "";
        public string shifterVID = "";
        public string shifterPID = "";
        public string calibratedAt = "";  // ISO 8601
        public string calibratedBy = "";  // "F8-panel" | "auto-migrated" | "manual-ssh"
        public HoriAxes axes = new HoriAxes();
        public HoriButtons buttons = new HoriButtons();
    }

    [Serializable]
    public class HoriAxes
    {
        public HoriSteer steer = new HoriSteer();
        public HoriGas gas = new HoriGas();
        public HoriPedal brake = new HoriPedal();
        public HoriPedal clutch = new HoriPedal { required = true };
    }

    [Serializable]
    public class HoriSteer
    {
        public string path = "stick/x";
        public float center = 0f;
        public float leftMax = -1f;
        public float rightMax = 1f;
    }

    [Serializable]
    public class HoriGas
    {
        // Source siempre "HoriThrottleReader" para HORI HPC-044U (HID byte 21-22).
        public string source = "HoriThrottleReader";
        public float verifyThreshold = 0.7f;
    }

    [Serializable]
    public class HoriPedal
    {
        public string path = "";
        public float rest = -1f;
        public float press = 1f;
        public bool required = false;
    }

    [Serializable]
    public class HoriButtons
    {
        public HoriButton horn      = new HoriButton { required = true };
        public HoriButton hazards   = new HoriButton { required = true };
        public HoriButton turnLeft  = new HoriButton { required = true };
        public HoriButton turnRight = new HoriButton { required = true };
        public HoriButton reverse   = new HoriButton { required = true, kind = "pulse" };
        public HoriButton gear1 = new HoriButton();
        public HoriButton gear2 = new HoriButton();
        public HoriButton gear3 = new HoriButton();
        public HoriButton gear4 = new HoriButton();
        public HoriButton gear5 = new HoriButton();
        public HoriButton gear6 = new HoriButton();
    }

    [Serializable]
    public class HoriButton
    {
        public string path = "";
        public bool required = false;
        public string kind = "hold";  // "hold" | "pulse"
    }
}
