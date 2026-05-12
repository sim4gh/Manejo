using System.IO;
using NUnit.Framework;
using UnityEngine;
using TlaxSim.MotoCalibration;

public class MotoControlMappingTests
{
    private string _tempDir;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "moto-tests-" + System.Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        MotoControlMapping.OverrideStoragePathForTests(_tempDir);
        MotoControlMapping.ResetForTests();
        // Tests por defecto corren con flag=1 (JSON mode). Cada test puede flip.
        PlayerPrefs.SetInt(MotoControlMapping.PREF_USE_JSON, 1);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
        PlayerPrefs.DeleteKey(MotoControlMapping.PREF_USE_JSON);
        MotoControlMapping.OverrideStoragePathForTests(null);
    }

    [Test]
    public void LoadFromDisk_FlagDisabled_ActiveIsNullEvenIfJsonExists()
    {
        // Setup: flag=0 (legacy mode) + JSON file presente
        PlayerPrefs.SetInt(MotoControlMapping.PREF_USE_JSON, 0);
        var m = new MotoMapping { deviceFingerprint = "Moto Simulator|SimuladoresTlax|simbt001" };
        File.WriteAllText(Path.Combine(_tempDir, MotoControlMapping.FILENAME),
            JsonUtility.ToJson(m));

        MotoControlMapping.LoadFromDisk();

        Assert.IsNull(MotoControlMapping.Active);
    }

    [Test]
    public void SaveThenLoad_ActiveMatches()
    {
        var m = new MotoMapping
        {
            deviceFingerprint = "Moto Simulator|SimuladoresTlax|simbt001"
        };
        m.axes.lean.path = "stick/x";
        m.axes.handlebar.path = "stick/y";
        m.axes.gas.path = "rz";
        m.buttons.brake.path = "button1";
        m.buttons.clutch.path = "button2";

        MotoControlMapping.Save(m);
        MotoControlMapping.ResetForTests();
        MotoControlMapping.LoadFromDisk();

        Assert.IsNotNull(MotoControlMapping.Active);
        Assert.AreEqual("motorcycle", MotoControlMapping.Active.vehicleType);
        Assert.AreEqual("rz", MotoControlMapping.Active.axes.gas.path);
        Assert.AreEqual("button1", MotoControlMapping.Active.buttons.brake.path);
    }

    [Test]
    public void Save_AtomicWrite_NoLeftoverTempFile()
    {
        var m = new MotoMapping();
        MotoControlMapping.Save(m);

        string tmpPath = Path.Combine(_tempDir, MotoControlMapping.FILENAME + ".tmp");
        Assert.IsFalse(File.Exists(tmpPath), "tmp file no debe sobrevivir Save");
    }

    [Test]
    public void LoadFromDisk_CorruptJson_ActiveIsNull()
    {
        File.WriteAllText(Path.Combine(_tempDir, MotoControlMapping.FILENAME), "{not json");
        MotoControlMapping.LoadFromDisk();
        Assert.IsNull(MotoControlMapping.Active);
    }

    [Test]
    public void LoadFromDisk_UnknownSchemaVersion_ActiveIsNull()
    {
        var bad = new MotoMapping { schemaVersion = 99 };
        File.WriteAllText(Path.Combine(_tempDir, MotoControlMapping.FILENAME),
            JsonUtility.ToJson(bad));

        MotoControlMapping.LoadFromDisk();
        Assert.IsNull(MotoControlMapping.Active);
    }

    [Test]
    public void Clear_DeletesJsonButPreservesFlag()
    {
        var m = new MotoMapping();
        MotoControlMapping.Save(m);
        Assert.IsTrue(File.Exists(Path.Combine(_tempDir, MotoControlMapping.FILENAME)));

        MotoControlMapping.Clear();

        Assert.IsFalse(File.Exists(Path.Combine(_tempDir, MotoControlMapping.FILENAME)));
        Assert.AreEqual(1, PlayerPrefs.GetInt(MotoControlMapping.PREF_USE_JSON));
        Assert.IsNull(MotoControlMapping.Active);
    }
}
