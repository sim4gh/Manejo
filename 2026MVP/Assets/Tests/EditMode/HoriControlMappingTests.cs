using System.IO;
using NUnit.Framework;
using TlaxSim.HoriCalibration;

namespace TlaxSim.Tests
{
    public class HoriControlMappingTests
    {
        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "hori-test-" + System.Guid.NewGuid());
            Directory.CreateDirectory(_tempDir);
            HoriControlMapping.OverrideStoragePathForTests(_tempDir);
            HoriControlMapping.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
            HoriControlMapping.OverrideStoragePathForTests(null);
        }

        [Test]
        public void LoadFromDisk_NoFile_ActiveIsNull()
        {
            HoriControlMapping.LoadFromDisk();
            Assert.IsNull(HoriControlMapping.Active);
        }

        [Test]
        public void SaveThenLoadFromDisk_ActiveMatches()
        {
            var m = new HoriMapping {
                deviceFingerprint = "TEST-FP",
                calibratedBy = "test"
            };
            m.axes.brake.path = "rz";
            HoriControlMapping.Save(m);
            HoriControlMapping.ResetForTests();
            HoriControlMapping.LoadFromDisk();
            Assert.IsNotNull(HoriControlMapping.Active);
            Assert.AreEqual("TEST-FP", HoriControlMapping.Active.deviceFingerprint);
            Assert.AreEqual("rz", HoriControlMapping.Active.axes.brake.path);
        }

        [Test]
        public void Save_AtomicWrite_NoLeftoverTempFile()
        {
            var m = new HoriMapping { deviceFingerprint = "FP" };
            HoriControlMapping.Save(m);
            string tmpPath = Path.Combine(_tempDir, "hori_mapping.json.tmp");
            Assert.IsFalse(File.Exists(tmpPath), "tmp file should not remain after Save");
        }

        [Test]
        public void LoadFromDisk_CorruptJson_ActiveIsNull()
        {
            File.WriteAllText(Path.Combine(_tempDir, "hori_mapping.json"), "{not valid json");
            HoriControlMapping.LoadFromDisk();
            Assert.IsNull(HoriControlMapping.Active);
        }

        [Test]
        public void LoadFromDisk_UnknownSchemaVersion_ActiveIsNull()
        {
            // Bypass Save() porque escribe siempre schemaVersion=CURRENT_SCHEMA.
            // Aquí queremos verificar que el LOADER rechaza schemas desconocidos.
            string path = Path.Combine(_tempDir, "hori_mapping.json");
            File.WriteAllText(path, "{\"schemaVersion\":99,\"deviceFingerprint\":\"\"}");
            HoriControlMapping.LoadFromDisk();
            Assert.IsNull(HoriControlMapping.Active);
        }
    }
}
