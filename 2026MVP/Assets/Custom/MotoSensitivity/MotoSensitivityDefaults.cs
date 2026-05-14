namespace TlaxSim.MotoSensitivity
{
    // Defaults hardcoded para los 3 presets nombrados. Fuente de verdad para
    // "Restaurar default" en F11 panel y bootstrap inicial del JSON.
    //
    // Ver docs/superpowers/specs/2026-05-13-moto-sensitivity-design.md sección 4.
    public static class MotoSensitivityDefaults
    {
        public static MotoPreset Principiante()
        {
            return new MotoPreset
            {
                lean   = new AxisSensitivity { deadzone = 0.15f, curveType = "pow",    curveParam = 2.0f, scale = 0.55f },
                hbar   = new AxisSensitivity { deadzone = 0.12f, curveType = "pow",    curveParam = 1.8f, scale = 0.60f },
                gas    = new PedalSensitivity{ deadzone = 0.02f, curveType = "pow",    curveParam = 2.0f, scale = 0.80f, rampUpPerSec = 1.5f },
                brake  = new ScaleOnly { scale = 0.70f },
                clutch = new ScaleOnly { scale = 1.0f },
                blendStartKmh = 35f, blendEndKmh = 70f, highSpeedLeanWeight = 0.35f
            };
        }

        public static MotoPreset Normal()
        {
            return new MotoPreset
            {
                lean   = new AxisSensitivity { deadzone = 0.08f, curveType = "pow",    curveParam = 1.5f, scale = 0.80f },
                hbar   = new AxisSensitivity { deadzone = 0.05f, curveType = "pow",    curveParam = 1.3f, scale = 0.85f },
                gas    = new PedalSensitivity{ deadzone = 0.01f, curveType = "pow",    curveParam = 1.4f, scale = 1.0f,  rampUpPerSec = 3.0f },
                brake  = new ScaleOnly { scale = 1.0f },
                clutch = new ScaleOnly { scale = 1.0f },
                blendStartKmh = 30f, blendEndKmh = 60f, highSpeedLeanWeight = 0.50f
            };
        }

        public static MotoPreset Realista()
        {
            return new MotoPreset
            {
                lean   = new AxisSensitivity { deadzone = 0.02f, curveType = "linear", curveParam = 1.0f, scale = 1.0f },
                hbar   = new AxisSensitivity { deadzone = 0.02f, curveType = "linear", curveParam = 1.0f, scale = 1.0f },
                gas    = new PedalSensitivity{ deadzone = 0.00f, curveType = "linear", curveParam = 1.0f, scale = 1.0f,  rampUpPerSec = 999f },
                brake  = new ScaleOnly { scale = 1.0f },
                clutch = new ScaleOnly { scale = 1.0f },
                blendStartKmh = 30f, blendEndKmh = 60f, highSpeedLeanWeight = 0.50f
            };
        }

        public static MotoSensitivity NewWithRealistaActive()
        {
            return new MotoSensitivity
            {
                schemaVersion = 1,
                vehicleType = "motorcycle",
                activePreset = "Realista",
                lastModifiedAt = System.DateTime.UtcNow.ToString("o"),
                lastModifiedBy = "auto-bootstrap",
                presets = new MotoSensitivityPresets
                {
                    Principiante = Principiante(),
                    Normal = Normal(),
                    Realista = Realista()
                },
                custom = null
            };
        }
    }
}
