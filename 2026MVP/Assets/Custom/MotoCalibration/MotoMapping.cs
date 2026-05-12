using System;
using UnityEngine;

namespace TlaxSim.MotoCalibration
{
    // Data-only struct: calibración Moto Simulator persistida en disco como JSON.
    // Target: ESP32-S3 USB HID custom firmware (simbt001, VID 0x303A / PID 0x4D54).
    // Esquema más simple que HORI/G923: sin gears, sin paddles, sin FFB, sin reverse.
    // Compatible con JsonUtility de Unity (campos públicos, no propiedades).
    //
    // Schema doc: docs/superpowers/specs/2026-05-12-moto-v190-immutable-calibration-design.md
    [Serializable]
    public class MotoMapping
    {
        public int schemaVersion = 1;
        public string vehicleType = "motorcycle";  // discriminator para portal admin
        public string deviceFingerprint = "";
        public string vid = "303A";
        public string pid = "4D54";
        public string calibratedAt = "";  // ISO 8601
        public string calibratedBy = "";  // "F8-panel" | "auto-migrated" | "manual-ssh"
        public MotoAxes axes = new MotoAxes();
        public MotoButtons buttons = new MotoButtons();
    }

    [Serializable]
    public class MotoAxes
    {
        public MotoAxisRange lean = new MotoAxisRange { path = "stick/x" };
        public MotoAxisRange handlebar = new MotoAxisRange { path = "stick/y" };
        public MotoPedal gas = new MotoPedal { path = "rz", rest = -1f, press = 1f };
    }

    // Axis con rango ± (lean, handlebar). center se computa al cargar/guardar.
    [Serializable]
    public class MotoAxisRange
    {
        public string path = "";
        public float min = -1f;
        public float max = 1f;
        public float center = 0f;
    }

    // Pedal/throttle estilo G923 (Hall throttle del simbt001, rest=-1 press=+1 canónico).
    [Serializable]
    public class MotoPedal
    {
        public string path = "";
        public float rest = -1f;
        public float press = 1f;
    }

    [Serializable]
    public class MotoButtons
    {
        public MotoButton brake = new MotoButton { path = "button1" };
        public MotoButton clutch = new MotoButton { path = "button2" };
    }

    [Serializable]
    public class MotoButton
    {
        public string path = "";
        public bool required = false;
        public string kind = "hold";  // "hold" para brake (sostener) / "pulse" si aplica
    }
}
