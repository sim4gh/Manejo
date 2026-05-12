using System.Collections.Generic;
using NUnit.Framework;
using TlaxSim.G923Calibration;

public class G923MappingMigrationTests
{
    private class FakePrefs : G923MappingMigration.IPlayerPrefsReader
    {
        public Dictionary<string, string> Strings = new Dictionary<string, string>();
        public Dictionary<string, float> Floats = new Dictionary<string, float>();
        public Dictionary<string, int> Ints = new Dictionary<string, int>();
        public string GetString(string k, string d) => Strings.TryGetValue(k, out var v) ? v : d;
        public float GetFloat(string k, float d) => Floats.TryGetValue(k, out var v) ? v : d;
        public int GetInt(string k, int d) => Ints.TryGetValue(k, out var v) ? v : d;
    }

    private FakePrefs PsPrefs()
    {
        var p = new FakePrefs();
        p.Strings["Cal_DeviceFingerprint"] = "Logitech G923 Racing Wheel for PlayStation 4 and PC|Logitech|abc";
        p.Strings["G923_GasAxis"] = "z";
        p.Strings["G923_BrakeAxis"] = "rz";
        p.Strings["G923_ClutchAxis"] = "stick/y";
        p.Strings["Bind_steerAxis"] = "stick/x";
        p.Strings["Bind_reverse"] = "button19";
        p.Floats["G923_GasRest"] = 1f;
        p.Floats["G923_GasPress"] = -1f;
        p.Floats["G923_BrakeRest"] = 1f;
        p.Floats["G923_BrakePress"] = -1f;
        p.Floats["G923_ClutchRest"] = -1f;
        p.Floats["G923_ClutchPress"] = 1f;
        return p;
    }

    private FakePrefs XboxPrefs()
    {
        var p = new FakePrefs();
        p.Strings["Cal_DeviceFingerprint"] = "Logitech G923 Racing Wheel for Xbox One and PC|Logitech|xyz";
        p.Strings["G923_GasAxis"] = "stick/y";
        p.Strings["G923_BrakeAxis"] = "z";
        p.Strings["G923_ClutchAxis"] = "rz";
        p.Strings["Bind_steerAxis"] = "stick/x";
        p.Strings["Bind_reverse"] = "button12";
        p.Floats["G923_GasRest"] = -1f;
        p.Floats["G923_GasPress"] = 1f;
        p.Floats["G923_BrakeRest"] = 1f;
        p.Floats["G923_BrakePress"] = -1f;
        p.Floats["G923_ClutchRest"] = 1f;
        p.Floats["G923_ClutchPress"] = -1f;
        return p;
    }

    [Test]
    public void TryMigrate_PsClean_ReturnsTrueWithPsVariant()
    {
        var ok = G923MappingMigration.TryMigrateFromPlayerPrefs(PsPrefs(), out var m);
        Assert.IsTrue(ok);
        Assert.AreEqual("PS", m.variant);
        Assert.AreEqual("z", m.axes.gas.path);
        Assert.AreEqual("rz", m.axes.brake.path);
        Assert.AreEqual("stick/y", m.axes.clutch.path);
        Assert.AreEqual("button19", m.buttons.reverse.path);
        Assert.AreEqual("auto-migrated", m.calibratedBy);
    }

    [Test]
    public void TryMigrate_XboxClean_ReturnsTrueWithXboxVariant()
    {
        var ok = G923MappingMigration.TryMigrateFromPlayerPrefs(XboxPrefs(), out var m);
        Assert.IsTrue(ok);
        Assert.AreEqual("Xbox", m.variant);
        Assert.AreEqual("stick/y", m.axes.gas.path);
        Assert.AreEqual("z", m.axes.brake.path);
        Assert.AreEqual("rz", m.axes.clutch.path);
        Assert.AreEqual("button12", m.buttons.reverse.path);
    }

    [Test]
    public void TryMigrate_NoFingerprint_FallbackByGasAxis_Ps()
    {
        var p = PsPrefs();
        p.Strings["Cal_DeviceFingerprint"] = "";  // fingerprint missing, gas=z → PS
        var ok = G923MappingMigration.TryMigrateFromPlayerPrefs(p, out var m);
        Assert.IsTrue(ok);
        Assert.AreEqual("PS", m.variant);
    }

    [Test]
    public void TryMigrate_NoFingerprint_FallbackByGasAxis_Xbox()
    {
        var p = XboxPrefs();
        p.Strings["Cal_DeviceFingerprint"] = "";  // gas=stick/y → Xbox
        var ok = G923MappingMigration.TryMigrateFromPlayerPrefs(p, out var m);
        Assert.IsTrue(ok);
        Assert.AreEqual("Xbox", m.variant);
    }

    [Test]
    public void TryMigrate_CorruptedBrake_Aborts()
    {
        var p = PsPrefs();
        p.Floats["G923_BrakeRest"] = 0f;
        p.Floats["G923_BrakePress"] = 0f;
        var ok = G923MappingMigration.TryMigrateFromPlayerPrefs(p, out var m);
        Assert.IsFalse(ok);
        Assert.IsNull(m);
    }

    [Test]
    public void TryMigrate_GasRestAndPressSameSign_Aborts()
    {
        var p = PsPrefs();
        p.Floats["G923_GasRest"] = 1f;
        p.Floats["G923_GasPress"] = 1f;  // mismo signo — corrupto
        var ok = G923MappingMigration.TryMigrateFromPlayerPrefs(p, out var m);
        Assert.IsFalse(ok);
    }

    [Test]
    public void TryMigrate_UndetectableVariant_Aborts()
    {
        var p = PsPrefs();
        p.Strings["Cal_DeviceFingerprint"] = "";  // sin fingerprint
        p.Strings["G923_GasAxis"] = "weird/axis";  // no z ni stick/y
        var ok = G923MappingMigration.TryMigrateFromPlayerPrefs(p, out var m);
        Assert.IsFalse(ok);
    }

    [Test]
    public void TryMigrate_ClutchAxisMissing_UsesCanonicalDefault()
    {
        var p = PsPrefs();
        p.Strings.Remove("G923_ClutchAxis");
        var ok = G923MappingMigration.TryMigrateFromPlayerPrefs(p, out var m);
        Assert.IsTrue(ok);
        Assert.AreEqual("stick/y", m.axes.clutch.path);
        Assert.IsFalse(m.axes.clutch.required);
    }
}
