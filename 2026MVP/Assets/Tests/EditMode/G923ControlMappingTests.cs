using System.IO;
using NUnit.Framework;
using UnityEngine;
using TlaxSim.G923Calibration;

public class G923ControlMappingTests
{
    private string _tempDir;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "g923-tests-" + System.Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        G923ControlMapping.OverrideStoragePathForTests(_tempDir);
        G923ControlMapping.ResetForTests();
        // Tests por defecto corren con flag=1 (JSON mode). Cada test puede flip.
        PlayerPrefs.SetInt(G923ControlMapping.PREF_USE_JSON, 1);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
        PlayerPrefs.DeleteKey(G923ControlMapping.PREF_USE_JSON);
        G923ControlMapping.OverrideStoragePathForTests(null);
    }

    [Test]
    public void LoadFromDisk_FlagDisabled_ActiveIsNullEvenIfJsonExists()
    {
        // Setup: flag=0 (legacy mode) + JSON file presente
        PlayerPrefs.SetInt(G923ControlMapping.PREF_USE_JSON, 0);
        var m = new G923Mapping { variant = "PS" };
        File.WriteAllText(Path.Combine(_tempDir, G923ControlMapping.FILENAME),
            JsonUtility.ToJson(m));

        G923ControlMapping.LoadFromDisk();

        Assert.IsNull(G923ControlMapping.Active);
    }

    [Test]
    public void SaveThenLoad_ActiveMatches()
    {
        var m = new G923Mapping
        {
            variant = "PS",
            deviceFingerprint = "Logitech G923|PS|abc"
        };
        m.axes.steer.path = "stick/x";
        m.axes.gas.path = "z";
        m.axes.brake.path = "rz";

        G923ControlMapping.Save(m);
        G923ControlMapping.ResetForTests();
        G923ControlMapping.LoadFromDisk();

        Assert.IsNotNull(G923ControlMapping.Active);
        Assert.AreEqual("PS", G923ControlMapping.Active.variant);
        Assert.AreEqual("z", G923ControlMapping.Active.axes.gas.path);
    }

    [Test]
    public void Save_AtomicWrite_NoLeftoverTempFile()
    {
        var m = new G923Mapping { variant = "Xbox" };
        G923ControlMapping.Save(m);

        string tmpPath = Path.Combine(_tempDir, G923ControlMapping.FILENAME + ".tmp");
        Assert.IsFalse(File.Exists(tmpPath), "tmp file no debe sobrevivir Save");
    }

    [Test]
    public void LoadFromDisk_CorruptJson_ActiveIsNull()
    {
        File.WriteAllText(Path.Combine(_tempDir, G923ControlMapping.FILENAME), "{not json");
        G923ControlMapping.LoadFromDisk();
        Assert.IsNull(G923ControlMapping.Active);
    }

    [Test]
    public void LoadFromDisk_UnknownSchemaVersion_ActiveIsNull()
    {
        var bad = new G923Mapping { variant = "PS", schemaVersion = 99 };
        File.WriteAllText(Path.Combine(_tempDir, G923ControlMapping.FILENAME),
            JsonUtility.ToJson(bad));

        G923ControlMapping.LoadFromDisk();
        Assert.IsNull(G923ControlMapping.Active);
    }

    [Test]
    public void Clear_DeletesJsonButPreservesFlag()
    {
        var m = new G923Mapping { variant = "PS" };
        G923ControlMapping.Save(m);
        Assert.IsTrue(File.Exists(Path.Combine(_tempDir, G923ControlMapping.FILENAME)));

        G923ControlMapping.Clear();

        Assert.IsFalse(File.Exists(Path.Combine(_tempDir, G923ControlMapping.FILENAME)));
        Assert.AreEqual(1, PlayerPrefs.GetInt(G923ControlMapping.PREF_USE_JSON));
        Assert.IsNull(G923ControlMapping.Active);
    }
}
