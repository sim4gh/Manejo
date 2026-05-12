using System.Collections.Generic;
using NUnit.Framework;
using TlaxSim.G923Calibration;

public class G923PreflightCheckTests
{
    private class FakeResolver : IG923DeviceResolver
    {
        public HashSet<string> Axes = new HashSet<string>();
        public HashSet<string> Buttons = new HashSet<string>();
        public bool ResolveAxis(string p) => !string.IsNullOrEmpty(p) && Axes.Contains(p);
        public bool ResolveButton(string p) => !string.IsNullOrEmpty(p) && Buttons.Contains(p);
    }

    private G923Mapping PsMapping()
    {
        var m = new G923Mapping { variant = "PS" };
        m.axes.steer.path = "stick/x";
        m.axes.gas.path = "z";
        m.axes.brake.path = "rz";
        m.axes.clutch.path = "stick/y";
        m.buttons.horn.path = "button14";
        m.buttons.turnLeft.path = "button5";
        m.buttons.turnRight.path = "button6";
        m.buttons.reverse.path = "button19";
        return m;
    }

    private FakeResolver PsResolver()
    {
        var r = new FakeResolver();
        r.Axes.UnionWith(new[] { "stick/x", "z", "rz", "stick/y" });
        r.Buttons.UnionWith(new[] { "button5", "button6", "button14", "button19" });
        return r;
    }

    [Test]
    public void Validate_AllResolve_IsOk()
    {
        var result = G923PreflightCheck.Validate(PsMapping(), PsResolver(), manual: false);
        Assert.IsTrue(result.IsOk);
    }

    [Test]
    public void Validate_GasUnreachable_MissingGas()
    {
        var resolver = PsResolver();
        resolver.Axes.Remove("z");
        var result = G923PreflightCheck.Validate(PsMapping(), resolver, manual: false);
        Assert.IsFalse(result.IsOk);
        CollectionAssert.Contains(result.Missing, "gas");
    }

    [Test]
    public void Validate_ManualClutchMissing_MissingClutch()
    {
        var m = PsMapping();
        m.axes.clutch.path = "";  // sin clutch path
        var result = G923PreflightCheck.Validate(m, PsResolver(), manual: true);
        Assert.IsFalse(result.IsOk);
        CollectionAssert.Contains(result.Missing, "clutch");
    }

    [Test]
    public void Validate_AutoClutchMissing_StillOk()
    {
        var m = PsMapping();
        m.axes.clutch.path = "";
        var result = G923PreflightCheck.Validate(m, PsResolver(), manual: false);
        Assert.IsTrue(result.IsOk);
    }

    [Test]
    public void Validate_ReverseUnresolved_MissingReverse()
    {
        var resolver = PsResolver();
        resolver.Buttons.Remove("button19");
        var result = G923PreflightCheck.Validate(PsMapping(), resolver, manual: false);
        CollectionAssert.Contains(result.Missing, "reverse");
    }
}
