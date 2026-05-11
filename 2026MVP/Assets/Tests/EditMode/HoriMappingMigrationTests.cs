using NUnit.Framework;
using TlaxSim.HoriCalibration;
using UnityEngine;

namespace TlaxSim.Tests
{
    // Tests de la lógica pura de migración. Inyectamos un PlayerPrefs-like
    // shim para no depender del registry real durante los tests.
    public class HoriMappingMigrationTests
    {
        private FakePlayerPrefs _prefs;

        [SetUp]
        public void SetUp()
        {
            _prefs = new FakePlayerPrefs();
        }

        [Test]
        public void TryMigrate_NotHori_ReturnsFalse()
        {
            _prefs.SetString("Cal_DeviceFingerprint", "Logitech G923");
            _prefs.SetString("Bind_reverse", "button19");
            Assert.IsFalse(HoriMappingMigration.TryMigrateFromPlayerPrefs(_prefs, out var _));
        }

        [Test]
        public void TryMigrate_CleanHoriPrefs_ReturnsTrue()
        {
            SetCleanHoriPrefs(_prefs);
            bool ok = HoriMappingMigration.TryMigrateFromPlayerPrefs(_prefs, out var mapping);
            Assert.IsTrue(ok);
            Assert.AreEqual("HORI TRUCK CONTROL SYSTEM", mapping.deviceFingerprint);
            Assert.AreEqual("rz", mapping.axes.brake.path);
            Assert.AreEqual(-1f, mapping.axes.brake.rest, 0.01f);
            Assert.AreEqual("shifter:button7", mapping.buttons.reverse.path);
        }

        [Test]
        public void TryMigrate_BrakeRestCorrupted_ReturnsFalse()
        {
            SetCleanHoriPrefs(_prefs);
            _prefs.SetFloat("G923_BrakeRest", 0.5f); // corruption: hardware-fixed -1
            Assert.IsFalse(HoriMappingMigration.TryMigrateFromPlayerPrefs(_prefs, out var _));
        }

        [Test]
        public void TryMigrate_ReversePathNotShifter_ReturnsFalse()
        {
            SetCleanHoriPrefs(_prefs);
            _prefs.SetString("Bind_reverse", "wheel:slider1"); // v1.5.11 contamination
            Assert.IsFalse(HoriMappingMigration.TryMigrateFromPlayerPrefs(_prefs, out var _));
        }

        [Test]
        public void TryMigrate_FingerprintNotHori_ReturnsFalse()
        {
            SetCleanHoriPrefs(_prefs);
            _prefs.SetString("Cal_DeviceFingerprint", "Random Wheel");
            Assert.IsFalse(HoriMappingMigration.TryMigrateFromPlayerPrefs(_prefs, out var _));
        }

        [Test]
        public void TryMigrate_PartialGearsManualMode_ReturnsTrueButMarksOptional()
        {
            SetCleanHoriPrefs(_prefs);
            _prefs.SetString("Bind_gear3", ""); // gear 3 missing
            bool ok = HoriMappingMigration.TryMigrateFromPlayerPrefs(_prefs, out var mapping);
            Assert.IsTrue(ok, "migración no aborta por gear opcional faltante");
            Assert.AreEqual("", mapping.buttons.gear3.path);
        }

        private static void SetCleanHoriPrefs(FakePlayerPrefs p)
        {
            p.SetString("Cal_DeviceFingerprint", "HORI TRUCK CONTROL SYSTEM|HORI CO.,LTD.|");
            p.SetString("Bind_steerAxis", "stick/x");
            p.SetString("G923_BrakeAxis", "rz");
            p.SetFloat("G923_BrakeRest", -1f);
            p.SetFloat("G923_BrakePress", 1f);
            p.SetString("G923_ClutchAxis", "slider");
            p.SetFloat("G923_ClutchRest", -1f);
            p.SetFloat("G923_ClutchPress", 1f);
            p.SetString("Bind_reverse", "shifter:button7");
            p.SetString("Bind_horn", "wheel:button7");
            p.SetString("Bind_hazard", "shifter:button27");
            p.SetString("Bind_paddleLeft", "wheel:button40");
            p.SetString("Bind_paddleRight", "wheel:button41");
            p.SetString("Bind_gear1", "shifter:trigger");
            p.SetString("Bind_gear2", "shifter:button2");
            p.SetString("Bind_gear3", "shifter:button3");
            p.SetString("Bind_gear4", "shifter:button4");
            p.SetString("Bind_gear5", "shifter:button5");
            p.SetString("Bind_gear6", "shifter:button6");
        }
    }

    // Shim para tests — implementación in-memory de la interfaz IPlayerPrefsReader
    // que usa HoriMappingMigration. En runtime usa el wrapper de UnityPlayerPrefs.
    public class FakePlayerPrefs : IPlayerPrefsReader
    {
        private System.Collections.Generic.Dictionary<string, string> _str = new System.Collections.Generic.Dictionary<string, string>();
        private System.Collections.Generic.Dictionary<string, float> _f = new System.Collections.Generic.Dictionary<string, float>();
        public void SetString(string key, string val) => _str[key] = val;
        public void SetFloat(string key, float val) => _f[key] = val;
        public string GetString(string key, string def) => _str.TryGetValue(key, out var v) ? v : def;
        public float GetFloat(string key, float def) => _f.TryGetValue(key, out var v) ? v : def;
    }
}
