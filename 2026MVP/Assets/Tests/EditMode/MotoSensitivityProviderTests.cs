using System.IO;
using NUnit.Framework;
using TlaxSim.MotoSensitivity;
using UnityEngine;

namespace TlaxSim.MotoSensitivity.Tests
{
    public class MotoSensitivityProviderTests
    {
        string _tmpDir;
        string _jsonPath;

        [SetUp]
        public void Setup()
        {
            _tmpDir = Path.Combine(Application.temporaryCachePath, "motosens-tests-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tmpDir);
            _jsonPath = Path.Combine(_tmpDir, "moto_sensitivity.json");
        }

        [TearDown]
        public void Teardown()
        {
            if (Directory.Exists(_tmpDir)) Directory.Delete(_tmpDir, true);
        }

        [Test]
        public void LoadOrInitialize_NoFile_CreatesJsonWithRealistaActive()
        {
            Assert.IsFalse(File.Exists(_jsonPath));
            var loaded = MotoSensitivityProvider.LoadOrInitializeFromPath(_jsonPath);
            Assert.IsNotNull(loaded);
            Assert.AreEqual("Realista", loaded.activePreset);
            Assert.IsTrue(File.Exists(_jsonPath));
        }

        [Test]
        public void LoadOrInitialize_ExistingValidJson_Returns()
        {
            var fresh = MotoSensitivityDefaults.NewWithRealistaActive();
            fresh.activePreset = "Principiante";
            File.WriteAllText(_jsonPath, JsonUtility.ToJson(fresh, true));

            var loaded = MotoSensitivityProvider.LoadOrInitializeFromPath(_jsonPath);
            Assert.IsNotNull(loaded);
            Assert.AreEqual("Principiante", loaded.activePreset);
        }

        [Test]
        public void LoadOrInitialize_CorruptJson_ReturnsNull()
        {
            File.WriteAllText(_jsonPath, "{not json");
            var loaded = MotoSensitivityProvider.LoadOrInitializeFromPath(_jsonPath);
            Assert.IsNull(loaded);
        }

        [Test]
        public void LoadOrInitialize_InvalidSchemaVersion_ReturnsNull()
        {
            var bad = MotoSensitivityDefaults.NewWithRealistaActive();
            bad.schemaVersion = 99;
            File.WriteAllText(_jsonPath, JsonUtility.ToJson(bad));
            var loaded = MotoSensitivityProvider.LoadOrInitializeFromPath(_jsonPath);
            Assert.IsNull(loaded);
        }

        [Test]
        public void LoadOrInitialize_InvalidActivePreset_ReturnsNull()
        {
            var bad = MotoSensitivityDefaults.NewWithRealistaActive();
            bad.activePreset = "Inexistente";
            File.WriteAllText(_jsonPath, JsonUtility.ToJson(bad));
            var loaded = MotoSensitivityProvider.LoadOrInitializeFromPath(_jsonPath);
            Assert.IsNull(loaded);
        }

        [Test]
        public void ResolveActivePreset_Realista()
        {
            var sens = MotoSensitivityDefaults.NewWithRealistaActive();
            sens.activePreset = "Realista";
            var p = MotoSensitivityProvider.ResolveActive(sens);
            Assert.AreEqual("linear", p.lean.curveType);
        }

        [Test]
        public void ResolveActivePreset_Principiante()
        {
            var sens = MotoSensitivityDefaults.NewWithRealistaActive();
            sens.activePreset = "Principiante";
            var p = MotoSensitivityProvider.ResolveActive(sens);
            Assert.AreEqual(0.55f, p.lean.scale, 1e-5f);
        }

        [Test]
        public void ResolveActivePreset_CustomFallsToNormalIfNull()
        {
            var sens = MotoSensitivityDefaults.NewWithRealistaActive();
            sens.activePreset = "Custom";
            sens.custom = null;
            var p = MotoSensitivityProvider.ResolveActive(sens);
            Assert.AreEqual(0.80f, p.lean.scale, 1e-5f, "Custom=null debe caer a Normal");
        }

        [Test]
        public void SaveAtomic_WritesValidJson()
        {
            var sens = MotoSensitivityDefaults.NewWithRealistaActive();
            sens.activePreset = "Normal";
            MotoSensitivityProvider.SaveAtomicToPath(_jsonPath, sens);

            Assert.IsTrue(File.Exists(_jsonPath));
            var loaded = MotoSensitivityProvider.LoadOrInitializeFromPath(_jsonPath);
            Assert.AreEqual("Normal", loaded.activePreset);
        }

        [Test]
        public void Json_RoundTripPreserves()
        {
            var original = MotoSensitivityDefaults.NewWithRealistaActive();
            original.activePreset = "Principiante";
            original.custom = MotoSensitivityDefaults.Normal();

            string json = JsonUtility.ToJson(original);
            var roundTrip = JsonUtility.FromJson<MotoSensitivity>(json);

            Assert.AreEqual(original.activePreset, roundTrip.activePreset);
            Assert.AreEqual(original.custom.lean.scale, roundTrip.custom.lean.scale, 1e-5f);
            Assert.AreEqual(original.presets.Realista.lean.deadzone, roundTrip.presets.Realista.lean.deadzone, 1e-5f);
        }
    }
}
