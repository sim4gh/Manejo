using System.Collections.Generic;
using NUnit.Framework;
using TlaxSim.MotoCalibration;

public class MotoPreflightCheckTests
{
    private class FakeResolver : IMotoDeviceResolver
    {
        public HashSet<string> Axes = new HashSet<string>();
        public HashSet<string> Buttons = new HashSet<string>();
        public bool ResolveAxis(string p) => !string.IsNullOrEmpty(p) && Axes.Contains(p);
        public bool ResolveButton(string p) => !string.IsNullOrEmpty(p) && Buttons.Contains(p);
    }

    private MotoMapping CanonicalMapping()
    {
        var m = new MotoMapping();
        m.axes.lean.path = "stick/x";
        m.axes.handlebar.path = "stick/y";
        m.axes.gas.path = "rz";
        m.buttons.brake.path = "button1";
        m.buttons.clutch.path = "button2";
        return m;
    }

    private FakeResolver CanonicalResolver()
    {
        var r = new FakeResolver();
        r.Axes.UnionWith(new[] { "stick/x", "stick/y", "rz" });
        r.Buttons.UnionWith(new[] { "button1", "button2" });
        return r;
    }

    [Test]
    public void Validate_AllResolve_IsOk()
    {
        var result = MotoPreflightCheck.Validate(CanonicalMapping(), CanonicalResolver());
        Assert.IsTrue(result.IsOk);
        Assert.IsEmpty(result.Missing);
    }

    [Test]
    public void Validate_LeanUnreachable_MissingLean()
    {
        var resolver = CanonicalResolver();
        resolver.Axes.Remove("stick/x");
        var result = MotoPreflightCheck.Validate(CanonicalMapping(), resolver);
        Assert.IsFalse(result.IsOk);
        CollectionAssert.Contains(result.Missing, "lean");
    }

    [Test]
    public void Validate_HandlebarUnreachable_MissingHandlebar()
    {
        var resolver = CanonicalResolver();
        resolver.Axes.Remove("stick/y");
        var result = MotoPreflightCheck.Validate(CanonicalMapping(), resolver);
        CollectionAssert.Contains(result.Missing, "handlebar");
    }

    [Test]
    public void Validate_GasUnreachable_MissingGas()
    {
        var resolver = CanonicalResolver();
        resolver.Axes.Remove("rz");
        var result = MotoPreflightCheck.Validate(CanonicalMapping(), resolver);
        CollectionAssert.Contains(result.Missing, "gas");
    }

    [Test]
    public void Validate_BrakeButtonUnreachable_MissingBrake()
    {
        var resolver = CanonicalResolver();
        resolver.Buttons.Remove("button1");
        var result = MotoPreflightCheck.Validate(CanonicalMapping(), resolver);
        CollectionAssert.Contains(result.Missing, "brake");
    }

    [Test]
    public void Validate_ClutchOptional_StillOkIfUnreachable()
    {
        // Moto clutch es opcional (algunas escenas no lo usan)
        var resolver = CanonicalResolver();
        resolver.Buttons.Remove("button2");
        var result = MotoPreflightCheck.Validate(CanonicalMapping(), resolver);
        // brake debe resolver, clutch puede faltar
        Assert.IsTrue(result.IsOk);
    }

    [Test]
    public void Validate_NullMapping_MissingMapping()
    {
        var result = MotoPreflightCheck.Validate(null, CanonicalResolver());
        Assert.IsFalse(result.IsOk);
        CollectionAssert.Contains(result.Missing, "mapping=null");
    }

    [Test]
    public void Validate_NullResolver_MissingResolver()
    {
        var result = MotoPreflightCheck.Validate(CanonicalMapping(), null);
        Assert.IsFalse(result.IsOk);
        CollectionAssert.Contains(result.Missing, "resolver=null");
    }

    [Test]
    public void Validate_EmptyAxisPath_TreatedAsUnreachable()
    {
        var m = CanonicalMapping();
        m.axes.lean.path = "";
        var result = MotoPreflightCheck.Validate(m, CanonicalResolver());
        CollectionAssert.Contains(result.Missing, "lean");
    }
}
