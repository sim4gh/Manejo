using System.Collections.Generic;
using NUnit.Framework;
using TlaxSim.MotoCalibration;

public class MotoMappingMigrationTests
{
    private class FakePrefs : MotoMappingMigration.IPlayerPrefsReader
    {
        public Dictionary<string, string> Strings = new Dictionary<string, string>();
        public Dictionary<string, float> Floats = new Dictionary<string, float>();
        public Dictionary<string, int> Ints = new Dictionary<string, int>();
        public string GetString(string k, string d) => Strings.TryGetValue(k, out var v) ? v : d;
        public float GetFloat(string k, float d) => Floats.TryGetValue(k, out var v) ? v : d;
        public int GetInt(string k, int d) => Ints.TryGetValue(k, out var v) ? v : d;
    }

    private FakePrefs CleanMotoPrefs()
    {
        var p = new FakePrefs();
        // Fingerprint canónico del simbt001 (Moto Simulator + SimuladoresTlax)
        p.Strings["MOTO_DeviceFingerprint"] = "Moto Simulator|SimuladoresTlax|simbt001";
        p.Strings["MOTO_LeanPath"] = "stick/x";
        p.Strings["MOTO_HbarPath"] = "stick/y";
        p.Strings["MOTO_GasPath"] = "rz";
        p.Strings["MOTO_BrakePath"] = "button1";
        p.Strings["MOTO_ClutchPath"] = "button2";
        p.Floats["MOTO_LeanMin"] = -1f;
        p.Floats["MOTO_LeanMax"] = 1f;
        p.Floats["MOTO_HbarMin"] = -1f;
        p.Floats["MOTO_HbarMax"] = 1f;
        return p;
    }

    [Test]
    public void TryMigrate_CleanPrefs_ReturnsTrueWithMotorcycleVehicleType()
    {
        var ok = MotoMappingMigration.TryMigrateFromPlayerPrefs(CleanMotoPrefs(), out var m);
        Assert.IsTrue(ok);
        Assert.AreEqual("motorcycle", m.vehicleType);
        Assert.AreEqual("stick/x", m.axes.lean.path);
        Assert.AreEqual("stick/y", m.axes.handlebar.path);
        Assert.AreEqual("rz", m.axes.gas.path);
        Assert.AreEqual("button1", m.buttons.brake.path);
        Assert.AreEqual("button2", m.buttons.clutch.path);
        Assert.AreEqual("auto-migrated", m.calibratedBy);
    }

    [Test]
    public void TryMigrate_PreservesLeanRange()
    {
        var p = CleanMotoPrefs();
        p.Floats["MOTO_LeanMin"] = -0.85f;
        p.Floats["MOTO_LeanMax"] = 0.92f;
        var ok = MotoMappingMigration.TryMigrateFromPlayerPrefs(p, out var m);
        Assert.IsTrue(ok);
        Assert.AreEqual(-0.85f, m.axes.lean.min, 0.001f);
        Assert.AreEqual(0.92f, m.axes.lean.max, 0.001f);
        // center se computa como midpoint
        Assert.AreEqual((m.axes.lean.min + m.axes.lean.max) * 0.5f, m.axes.lean.center, 0.001f);
    }

    [Test]
    public void TryMigrate_NoFingerprint_Aborts()
    {
        var p = CleanMotoPrefs();
        p.Strings["MOTO_DeviceFingerprint"] = "";
        var ok = MotoMappingMigration.TryMigrateFromPlayerPrefs(p, out var m);
        Assert.IsFalse(ok);
        Assert.IsNull(m);
    }

    [Test]
    public void TryMigrate_FingerprintWithoutMotoMarkers_Aborts()
    {
        var p = CleanMotoPrefs();
        // Algún wheel genérico — no debe migrarse como moto
        p.Strings["MOTO_DeviceFingerprint"] = "Logitech G923|Logitech|abc";
        var ok = MotoMappingMigration.TryMigrateFromPlayerPrefs(p, out var m);
        Assert.IsFalse(ok);
    }

    [Test]
    public void TryMigrate_NarrowLeanRange_Aborts()
    {
        var p = CleanMotoPrefs();
        p.Floats["MOTO_LeanMin"] = -0.1f;
        p.Floats["MOTO_LeanMax"] = 0.1f;  // span 0.2 < 0.5
        var ok = MotoMappingMigration.TryMigrateFromPlayerPrefs(p, out var m);
        Assert.IsFalse(ok);
    }

    [Test]
    public void TryMigrate_NarrowHbarRange_Aborts()
    {
        var p = CleanMotoPrefs();
        p.Floats["MOTO_HbarMin"] = -0.2f;
        p.Floats["MOTO_HbarMax"] = 0.1f;  // span 0.3 < 0.5
        var ok = MotoMappingMigration.TryMigrateFromPlayerPrefs(p, out var m);
        Assert.IsFalse(ok);
    }

    [Test]
    public void TryMigrate_NaNLeanRange_Aborts()
    {
        var p = CleanMotoPrefs();
        p.Floats["MOTO_LeanMin"] = float.NaN;
        var ok = MotoMappingMigration.TryMigrateFromPlayerPrefs(p, out var m);
        Assert.IsFalse(ok);
    }

    [Test]
    public void TryMigrate_PathEmpty_UsesCanonicalDefault()
    {
        var p = CleanMotoPrefs();
        p.Strings.Remove("MOTO_BrakePath");
        p.Strings.Remove("MOTO_ClutchPath");
        var ok = MotoMappingMigration.TryMigrateFromPlayerPrefs(p, out var m);
        Assert.IsTrue(ok);
        Assert.AreEqual("button1", m.buttons.brake.path);
        Assert.AreEqual("button2", m.buttons.clutch.path);
    }

    [Test]
    public void TryMigrate_PreservesVehicleTypeAlwaysMotorcycle()
    {
        var ok = MotoMappingMigration.TryMigrateFromPlayerPrefs(CleanMotoPrefs(), out var m);
        Assert.IsTrue(ok);
        Assert.AreEqual("motorcycle", m.vehicleType);
        Assert.AreEqual("303A", m.vid);
        Assert.AreEqual("4D54", m.pid);
    }

    [Test]
    public void TryMigrate_GasCanonicalRestPress()
    {
        // Hardcode canónico: rest=-1 press=+1 (NO inferir desde PlayerPrefs)
        var ok = MotoMappingMigration.TryMigrateFromPlayerPrefs(CleanMotoPrefs(), out var m);
        Assert.IsTrue(ok);
        Assert.AreEqual(-1f, m.axes.gas.rest, 0.001f);
        Assert.AreEqual(1f, m.axes.gas.press, 0.001f);
    }
}
