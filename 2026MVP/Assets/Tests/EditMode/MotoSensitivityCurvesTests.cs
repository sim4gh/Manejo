using NUnit.Framework;
using TlaxSim.MotoSensitivity;

namespace TlaxSim.MotoSensitivity.Tests
{
    public class MotoSensitivityCurvesTests
    {
        const float EPS = 1e-5f;

        [Test]
        public void Linear_Identity()
        {
            for (int i = -10; i <= 10; i++)
            {
                float x = i / 10f;
                Assert.AreEqual(x, MotoSensitivityCurves.ApplyCurve(x, "linear", 1f), EPS);
            }
        }

        [Test]
        public void Pow_PreservesEndpoints()
        {
            float[] ns = { 1f, 1.5f, 2f, 3f };
            foreach (var n in ns)
            {
                Assert.AreEqual(+1f, MotoSensitivityCurves.ApplyCurve(+1f, "pow", n), EPS);
                Assert.AreEqual(-1f, MotoSensitivityCurves.ApplyCurve(-1f, "pow", n), EPS);
            }
        }

        [Test]
        public void Pow_CentersFlattenWhenNGt1()
        {
            float result = MotoSensitivityCurves.ApplyCurve(0.5f, "pow", 2f);
            Assert.Less(result, 0.5f);
            Assert.Greater(result, 0f);
        }

        [Test]
        public void Pow_PreservesSign()
        {
            float result = MotoSensitivityCurves.ApplyCurve(-0.5f, "pow", 2f);
            Assert.Less(result, 0f);
        }

        [Test]
        public void Deadzone_ZeroBelow()
        {
            Assert.AreEqual(0f, MotoSensitivityCurves.ApplyDeadzone(0.05f, 0.1f), EPS);
            Assert.AreEqual(0f, MotoSensitivityCurves.ApplyDeadzone(-0.05f, 0.1f), EPS);
        }

        [Test]
        public void Deadzone_ContinuityAtEdge()
        {
            float result = MotoSensitivityCurves.ApplyDeadzone(0.1f, 0.1f);
            Assert.AreEqual(0f, result, EPS);
        }

        [Test]
        public void Deadzone_RescaleToOne()
        {
            Assert.AreEqual(+1f, MotoSensitivityCurves.ApplyDeadzone(+1f, 0.1f), EPS);
            Assert.AreEqual(-1f, MotoSensitivityCurves.ApplyDeadzone(-1f, 0.1f), EPS);
        }

        [Test]
        public void Deadzone_ZeroIsIdentity()
        {
            Assert.AreEqual(0.5f, MotoSensitivityCurves.ApplyDeadzone(0.5f, 0f), EPS);
        }

        [Test]
        public void Scale_LimitsMaxOutput()
        {
            Assert.AreEqual(0.7f, MotoSensitivityCurves.ApplyScale(+1f, 0.7f), EPS);
            Assert.AreEqual(-0.7f, MotoSensitivityCurves.ApplyScale(-1f, 0.7f), EPS);
        }

        [Test]
        public void RampLimit_FrameAware()
        {
            Assert.AreEqual(1f, MotoSensitivityCurves.ApplyRampLimit(1f, 0f, 2f, 0.5f), EPS);
            Assert.AreEqual(0.2f, MotoSensitivityCurves.ApplyRampLimit(1f, 0f, 2f, 0.1f), EPS);
        }

        [Test]
        public void RampLimit_DownIsInstant()
        {
            Assert.AreEqual(0f, MotoSensitivityCurves.ApplyRampLimit(0f, 1f, 0.1f, 0.01f), EPS);
        }

        [Test]
        public void ApplyAxis_Composite_Idempotent()
        {
            var cfg = new AxisSensitivity { deadzone = 0f, curveType = "linear", curveParam = 1f, scale = 1f };
            for (int i = -10; i <= 10; i++)
            {
                float x = i / 10f;
                Assert.AreEqual(x, MotoSensitivityCurves.ApplyAxis(x, cfg), EPS);
            }
        }

        [Test]
        public void ApplyAxis_FullPipeline_PreservesSignAndBounds()
        {
            var cfg = new AxisSensitivity { deadzone = 0.05f, curveType = "pow", curveParam = 1.5f, scale = 0.8f };
            for (int i = -10; i <= 10; i++)
            {
                float x = i / 10f;
                float y = MotoSensitivityCurves.ApplyAxis(x, cfg);
                Assert.That(y, Is.InRange(-0.81f, 0.81f));
                if (x > 0.1f) Assert.Greater(y, 0f);
                if (x < -0.1f) Assert.Less(y, 0f);
            }
        }

        [Test]
        public void Defaults_RealistaIsIdentity()
        {
            var realista = MotoSensitivityDefaults.Realista();
            for (int i = -10; i <= 10; i++)
            {
                float x = i / 10f;
                Assert.AreEqual(x, MotoSensitivityCurves.ApplyAxis(x, realista.lean), 0.05f);
                Assert.AreEqual(x, MotoSensitivityCurves.ApplyAxis(x, realista.hbar), 0.05f);
            }
        }

        [Test]
        public void Defaults_PrincipianteFlatensCenter()
        {
            var principiante = MotoSensitivityDefaults.Principiante();
            float center = MotoSensitivityCurves.ApplyAxis(0.5f, principiante.lean);
            Assert.Less(center, 0.5f, "Principiante debe aplanar el centro");
        }

        [Test]
        public void Defaults_AllPresetsHaveValidCurveType()
        {
            string[] valid = { "linear", "pow" };
            var presets = new[] {
                MotoSensitivityDefaults.Principiante(),
                MotoSensitivityDefaults.Normal(),
                MotoSensitivityDefaults.Realista()
            };
            foreach (var p in presets)
            {
                CollectionAssert.Contains(valid, p.lean.curveType);
                CollectionAssert.Contains(valid, p.hbar.curveType);
                CollectionAssert.Contains(valid, p.gas.curveType);
            }
        }
    }
}
