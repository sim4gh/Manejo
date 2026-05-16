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
        public void Migration_V1ToV2_FillsHighSpeedLeanGain_PerNamedPreset()
        {
            // Construir JSON v1 explícito (sin el campo highSpeedLeanGain).
            // No podemos usar NewWithRealistaActive() porque ahora declara v2.
            var sens = MotoSensitivityDefaults.NewWithRealistaActive();
            sens.schemaVersion = 1;
            // Simular ausencia del campo: 0 (lo que JsonUtility puede haber dejado).
            sens.presets.Principiante.highSpeedLeanGain = 0f;
            sens.presets.Normal.highSpeedLeanGain       = 0f;
            sens.presets.Realista.highSpeedLeanGain     = 0f;
            File.WriteAllText(_jsonPath, JsonUtility.ToJson(sens, true));

            var loaded = MotoSensitivityProvider.LoadOrInitializeFromPath(_jsonPath);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(2, loaded.schemaVersion);
            Assert.AreEqual(MotoSensitivityDefaults.DefaultPrincipianteHighSpeedLeanGain, loaded.presets.Principiante.highSpeedLeanGain, 1e-5f);
            Assert.AreEqual(MotoSensitivityDefaults.DefaultNormalHighSpeedLeanGain,       loaded.presets.Normal.highSpeedLeanGain,       1e-5f);
            Assert.AreEqual(MotoSensitivityDefaults.DefaultRealistaHighSpeedLeanGain,     loaded.presets.Realista.highSpeedLeanGain,     1e-5f);
        }

        [Test]
        public void Migration_V1ToV2_CustomFilledWith_1_0_Conservative()
        {
            var sens = MotoSensitivityDefaults.NewWithRealistaActive();
            sens.schemaVersion = 1;
            sens.custom = MotoSensitivityDefaults.Normal();
            sens.custom.lean.scale = 1.5f;            // calibración intencional del usuario
            sens.custom.highSpeedLeanGain = 0f;       // ausente en v1
            File.WriteAllText(_jsonPath, JsonUtility.ToJson(sens, true));

            var loaded = MotoSensitivityProvider.LoadOrInitializeFromPath(_jsonPath);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(2, loaded.schemaVersion);
            Assert.IsNotNull(loaded.custom);
            Assert.AreEqual(1.5f, loaded.custom.lean.scale, 1e-5f, "calibración del usuario debe preservarse");
            Assert.AreEqual(MotoSensitivityDefaults.DefaultCustomHighSpeedLeanGain, loaded.custom.highSpeedLeanGain, 1e-5f,
                "Custom heredado debe quedar en 1.0 (sin atenuación nueva no pedida)");
        }

        [Test]
        public void MigrateV1ToV2_RebuildsNullNamedPresets()
        {
            // Llamamos MigrateV1ToV2 directo porque JsonUtility no preserva
            // null en campos Serializable (los rellena con defaults). El test
            // verifica el branch defensivo del código de migración.
            var sens = new MotoSensitivity
            {
                schemaVersion = 1,
                activePreset = "Realista",
                presets = new MotoSensitivityPresets
                {
                    Principiante = null,
                    Normal = null,
                    Realista = MotoSensitivityDefaults.Realista()
                },
                custom = null
            };

            MotoSensitivityProvider.MigrateV1ToV2(sens);

            Assert.AreEqual(2, sens.schemaVersion);
            Assert.IsNotNull(sens.presets.Principiante);
            Assert.IsNotNull(sens.presets.Normal);
            Assert.IsNotNull(sens.presets.Realista);
            Assert.AreEqual(MotoSensitivityDefaults.DefaultPrincipianteHighSpeedLeanGain, sens.presets.Principiante.highSpeedLeanGain, 1e-5f);
            Assert.AreEqual(MotoSensitivityDefaults.DefaultNormalHighSpeedLeanGain,       sens.presets.Normal.highSpeedLeanGain,       1e-5f);
            Assert.AreEqual(MotoSensitivityDefaults.DefaultRealistaHighSpeedLeanGain,     sens.presets.Realista.highSpeedLeanGain,     1e-5f);
        }

        [Test]
        public void Migration_V1ToV2_Idempotent_LoadingV2TwiceDoesNotChange()
        {
            var sens = MotoSensitivityDefaults.NewWithRealistaActive();
            sens.schemaVersion = 1;
            File.WriteAllText(_jsonPath, JsonUtility.ToJson(sens, true));

            var first = MotoSensitivityProvider.LoadOrInitializeFromPath(_jsonPath);
            Assert.AreEqual(2, first.schemaVersion);
            float principianteGain = first.presets.Principiante.highSpeedLeanGain;

            var second = MotoSensitivityProvider.LoadOrInitializeFromPath(_jsonPath);
            Assert.AreEqual(2, second.schemaVersion);
            Assert.AreEqual(principianteGain, second.presets.Principiante.highSpeedLeanGain, 1e-5f);
        }

        [Test]
        public void Load_V2_WithExplicitZeroHighSpeedLeanGain_NormalizesToDefault()
        {
            // Caso JsonUtility-puede-cargar-0: v2 JSON con campo a 0 explícito
            // (o el caso en que el campo falta y queda en default(float)=0).
            var sens = MotoSensitivityDefaults.NewWithRealistaActive();
            sens.presets.Principiante.highSpeedLeanGain = 0f;
            File.WriteAllText(_jsonPath, JsonUtility.ToJson(sens, true));

            var loaded = MotoSensitivityProvider.LoadOrInitializeFromPath(_jsonPath);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(MotoSensitivityDefaults.DefaultPrincipianteHighSpeedLeanGain, loaded.presets.Principiante.highSpeedLeanGain, 1e-5f);
        }

        [Test]
        public void Load_V2_WithNegativeHighSpeedLeanGain_NormalizesToFallback()
        {
            var sens = MotoSensitivityDefaults.NewWithRealistaActive();
            sens.presets.Normal.highSpeedLeanGain = -0.5f;
            File.WriteAllText(_jsonPath, JsonUtility.ToJson(sens, true));

            var loaded = MotoSensitivityProvider.LoadOrInitializeFromPath(_jsonPath);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(MotoSensitivityDefaults.DefaultNormalHighSpeedLeanGain, loaded.presets.Normal.highSpeedLeanGain, 1e-5f);
        }

        [Test]
        public void Migration_V1ToV2_SaveFailsButConfigStaysInMemory()
        {
            var sens = MotoSensitivityDefaults.NewWithRealistaActive();
            sens.schemaVersion = 1;
            File.WriteAllText(_jsonPath, JsonUtility.ToJson(sens, true));

            // Inyectar fake-save que tira IOException.
            MotoSensitivityProvider.SaveOverride = (_, __) => { throw new IOException("disk full (test)"); };
            try
            {
                var loaded = MotoSensitivityProvider.LoadOrInitializeFromPath(_jsonPath);
                Assert.IsNotNull(loaded, "config debe quedar en memoria aunque save haya fallado");
                Assert.AreEqual(2, loaded.schemaVersion);
                Assert.AreEqual(MotoSensitivityDefaults.DefaultPrincipianteHighSpeedLeanGain, loaded.presets.Principiante.highSpeedLeanGain, 1e-5f);
            }
            finally
            {
                MotoSensitivityProvider.SaveOverride = null;
            }
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
