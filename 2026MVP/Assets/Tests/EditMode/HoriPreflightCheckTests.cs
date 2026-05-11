using NUnit.Framework;
using System.Collections.Generic;
using TlaxSim.HoriCalibration;

namespace TlaxSim.Tests
{
    public class HoriPreflightCheckTests
    {
        private HoriMapping _mapping;

        [SetUp]
        public void SetUp()
        {
            _mapping = new HoriMapping();
            _mapping.axes.brake.path = "rz";
            _mapping.axes.clutch.path = "slider";
            _mapping.axes.steer.path = "stick/x";
            _mapping.buttons.horn.path = "wheel:button7";
            _mapping.buttons.hazards.path = "shifter:button27";
            _mapping.buttons.turnLeft.path = "wheel:button40";
            _mapping.buttons.turnRight.path = "wheel:button41";
            _mapping.buttons.reverse.path = "shifter:button7";
            _mapping.buttons.gear1.path = "shifter:trigger";
            _mapping.buttons.gear2.path = "shifter:button2";
            _mapping.buttons.gear3.path = "shifter:button3";
            _mapping.buttons.gear4.path = "shifter:button4";
            _mapping.buttons.gear5.path = "shifter:button5";
            _mapping.buttons.gear6.path = "shifter:button6";
        }

        [Test]
        public void Validate_NullMapping_ReturnsMissingActiveOnly()
        {
            var result = HoriPreflightCheck.Validate(null, MakeResolverAllOk(), manual: false);
            Assert.IsFalse(result.IsOk);
            Assert.Contains("Active mapping", result.Missing);
        }

        [Test]
        public void Validate_AllResolveLive_Ok()
        {
            var result = HoriPreflightCheck.Validate(_mapping, MakeResolverAllOk(), manual: false);
            Assert.IsTrue(result.IsOk, "Esperado OK pero faltan: " + string.Join(",", result.Missing));
        }

        [Test]
        public void Validate_BrakeAxisDoesNotResolve_ReportsBrake()
        {
            var resolver = MakeResolverAllOk();
            resolver.AxisUnreachable("rz");
            var result = HoriPreflightCheck.Validate(_mapping, resolver, manual: false);
            Assert.IsFalse(result.IsOk);
            Assert.Contains("Freno (rz)", result.Missing);
        }

        [Test]
        public void Validate_AutoMode_IgnoresClutchAndGears()
        {
            var resolver = MakeResolverAllOk();
            resolver.AxisUnreachable("slider"); // clutch
            resolver.ButtonUnreachable("shifter:button2"); // gear2
            var result = HoriPreflightCheck.Validate(_mapping, resolver, manual: false);
            Assert.IsTrue(result.IsOk, "En Auto, clutch/gears no son required");
        }

        [Test]
        public void Validate_ManualMode_RequiresClutch()
        {
            var resolver = MakeResolverAllOk();
            resolver.AxisUnreachable("slider"); // clutch
            var result = HoriPreflightCheck.Validate(_mapping, resolver, manual: true);
            Assert.IsFalse(result.IsOk);
            Assert.Contains("Clutch (slider)", result.Missing);
        }

        [Test]
        public void Validate_ManualMode_AllGearsCounted()
        {
            var resolver = MakeResolverAllOk();
            resolver.ButtonUnreachable("shifter:button3");
            var result = HoriPreflightCheck.Validate(_mapping, resolver, manual: true);
            Assert.IsFalse(result.IsOk);
            Assert.Contains("Marcha 3 (shifter:button3)", result.Missing);
        }

        [Test]
        public void Validate_HornEmpty_ReportsHornMissing()
        {
            _mapping.buttons.horn.path = "";
            var result = HoriPreflightCheck.Validate(_mapping, MakeResolverAllOk(), manual: false);
            Assert.IsFalse(result.IsOk);
            Assert.Contains("Claxon", result.Missing);
        }

        // Helper: fake resolver que dice "resolves" para todo a menos que
        // se marque explícitamente unreachable.
        private FakeResolver MakeResolverAllOk() => new FakeResolver();
    }

    public class FakeResolver : IHoriDeviceResolver
    {
        private System.Collections.Generic.HashSet<string> _badAxes = new System.Collections.Generic.HashSet<string>();
        private System.Collections.Generic.HashSet<string> _badButtons = new System.Collections.Generic.HashSet<string>();
        public void AxisUnreachable(string path) => _badAxes.Add(path);
        public void ButtonUnreachable(string path) => _badButtons.Add(path);
        public bool AxisResolves(string path) => !string.IsNullOrEmpty(path) && !_badAxes.Contains(path);
        public bool ButtonResolves(string path) => !string.IsNullOrEmpty(path) && !_badButtons.Contains(path);
        public bool ThrottleReaderOk => true;
    }
}
