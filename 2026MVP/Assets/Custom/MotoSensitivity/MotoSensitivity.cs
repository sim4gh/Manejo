using System;
using UnityEngine;

namespace TlaxSim.MotoSensitivity
{
    // Data-only schema para moto_sensitivity.json. Compatible con JsonUtility
    // de Unity (campos públicos, no properties). Documentado en
    // docs/superpowers/specs/2026-05-13-moto-sensitivity-design.md sección 4.
    [Serializable]
    public class MotoSensitivity
    {
        public int schemaVersion = 1;
        public string vehicleType = "motorcycle";
        public string activePreset = "Realista";
        public string lastModifiedAt = "";
        public string lastModifiedBy = "";

        public MotoSensitivityPresets presets = new MotoSensitivityPresets();
        public MotoPreset custom = null;  // null cuando no hay custom guardado
    }

    [Serializable]
    public class MotoSensitivityPresets
    {
        public MotoPreset Principiante = new MotoPreset();
        public MotoPreset Normal = new MotoPreset();
        public MotoPreset Realista = new MotoPreset();
    }

    [Serializable]
    public class MotoPreset
    {
        public AxisSensitivity lean = new AxisSensitivity();
        public AxisSensitivity hbar = new AxisSensitivity();
        public PedalSensitivity gas = new PedalSensitivity();
        public ScaleOnly brake = new ScaleOnly();
        public ScaleOnly clutch = new ScaleOnly();

        public float blendStartKmh = 30f;
        public float blendEndKmh = 60f;
        public float highSpeedLeanWeight = 0.5f;
    }

    [Serializable]
    public class AxisSensitivity
    {
        public float deadzone = 0.02f;
        public string curveType = "linear";  // "linear" | "pow"
        public float curveParam = 1.0f;
        public float scale = 1.0f;
    }

    [Serializable]
    public class PedalSensitivity
    {
        public float deadzone = 0.0f;
        public string curveType = "linear";
        public float curveParam = 1.0f;
        public float scale = 1.0f;
        public float rampUpPerSec = 999f;
    }

    [Serializable]
    public class ScaleOnly
    {
        public float scale = 1.0f;
    }
}
