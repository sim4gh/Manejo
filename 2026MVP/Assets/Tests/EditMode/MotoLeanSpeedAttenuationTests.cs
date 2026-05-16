using NUnit.Framework;
using TlaxSim.MotoSensitivity;
using UnityEngine;

namespace TlaxSim.MotoSensitivity.Tests
{
    public class MotoLeanSpeedAttenuationTests
    {
        // Fórmula v1 (antes de highSpeedLeanGain), replicada aquí para regression.
        static float V1Formula(float lean, float hbar, float wHigh, float blend)
        {
            float steerHigh = wHigh * lean + (1f - wHigh) * hbar;
            return Mathf.Clamp(Mathf.Lerp(hbar, steerHigh, blend), -1f, +1f);
        }

        [Test]
        public void Regression_HighSpeedLeanGain_1_MatchesV1Formula()
        {
            float[] leans  = { -1f, -0.5f, 0f, 0.5f, 1f };
            float[] hbars  = { -1f, -0.3f, 0f, 0.3f, 1f };
            float[] wHighs = { 0.35f, 0.50f };
            float[] blends = { 0f, 0.25f, 0.5f, 0.75f, 1f };

            foreach (var lean in leans)
                foreach (var hbar in hbars)
                    foreach (var w in wHighs)
                        foreach (var b in blends)
                        {
                            float v1   = V1Formula(lean, hbar, w, b);
                            float v2g1 = MotoSensitivityCurves.ComputeMotoSteering(lean, hbar, w, 1f, b);
                            Assert.AreEqual(v1, v2g1, 1e-5f,
                                $"lean={lean} hbar={hbar} w={w} blend={b}: v1={v1} v2(g=1)={v2g1}");
                        }
        }

        [Test]
        public void Attenuation_HighSpeedLeanGain_0_4_At_Blend_1_LeanOnly()
        {
            // lean=1, hbar=0, wHigh=0.5, hsLG=0.4, blend=1
            // leanGain = 0.4 → effectiveLean = 0.4
            // steerHigh = 0.5 * 0.4 + 0.5 * 0 = 0.2
            // output = Lerp(0, 0.2, 1) = 0.2
            float result = MotoSensitivityCurves.ComputeMotoSteering(1f, 0f, 0.5f, 0.4f, 1f);
            Assert.AreEqual(0.2f, result, 1e-5f);
        }

        [Test]
        public void Attenuation_BlendZero_HorizontalEqualsHbar_RegardlessOfLean()
        {
            // En blend=0, lean no influye (cualquier highSpeedLeanGain).
            float r1 = MotoSensitivityCurves.ComputeMotoSteering(1f,    0.3f, 0.5f, 0.4f, 0f);
            float r2 = MotoSensitivityCurves.ComputeMotoSteering(-1f,   0.3f, 0.5f, 0.4f, 0f);
            float r3 = MotoSensitivityCurves.ComputeMotoSteering(0.7f,  0.3f, 0.5f, 1.0f, 0f);
            Assert.AreEqual(0.3f, r1, 1e-5f);
            Assert.AreEqual(0.3f, r2, 1e-5f);
            Assert.AreEqual(0.3f, r3, 1e-5f);
        }

        [Test]
        public void Attenuation_HighSpeedLeanGain_0_AtBlend_1_KillsLeanContribution()
        {
            // hsLG=0 → leanGain=0 → effectiveLean=0 → steerHigh=(1-w)*hbar
            // Con hbar=0: output debe ser 0.
            float r = MotoSensitivityCurves.ComputeMotoSteering(1f, 0f, 0.5f, 0f, 1f);
            Assert.AreEqual(0f, r, 1e-5f);
        }

        [Test]
        public void Normalize_TreatsZeroAsFallback()
        {
            float r = MotoSensitivityProvider.NormalizeHighSpeedLeanGain(0f, 0.6f);
            Assert.AreEqual(0.6f, r, 1e-5f);
        }

        [Test]
        public void Normalize_ClampsAbove1ToOne()
        {
            float r = MotoSensitivityProvider.NormalizeHighSpeedLeanGain(2.5f, 0.6f);
            Assert.AreEqual(1f, r, 1e-5f);
        }

        [Test]
        public void Normalize_NaNFallback()
        {
            float r = MotoSensitivityProvider.NormalizeHighSpeedLeanGain(float.NaN, 0.6f);
            Assert.AreEqual(0.6f, r, 1e-5f);
        }

        [Test]
        public void Normalize_PositiveInfinityFallback()
        {
            float r = MotoSensitivityProvider.NormalizeHighSpeedLeanGain(float.PositiveInfinity, 0.6f);
            Assert.AreEqual(0.6f, r, 1e-5f);
        }

        [Test]
        public void Normalize_NegativeInfinityFallback()
        {
            float r = MotoSensitivityProvider.NormalizeHighSpeedLeanGain(float.NegativeInfinity, 0.6f);
            Assert.AreEqual(0.6f, r, 1e-5f);
        }

        [Test]
        public void Normalize_NegativeFallback()
        {
            float r = MotoSensitivityProvider.NormalizeHighSpeedLeanGain(-0.3f, 0.6f);
            Assert.AreEqual(0.6f, r, 1e-5f);
        }

        [Test]
        public void Normalize_ValidValuePassesThrough()
        {
            float r = MotoSensitivityProvider.NormalizeHighSpeedLeanGain(0.42f, 0.6f);
            Assert.AreEqual(0.42f, r, 1e-5f);
        }

        [Test]
        public void NormalizeHighSpeedLeanGains_FillsAllPresetsAndCustom()
        {
            var sens = MotoSensitivityDefaults.NewWithRealistaActive();
            sens.presets.Principiante.highSpeedLeanGain = 0f;       // ausente
            sens.presets.Normal.highSpeedLeanGain       = float.NaN; // malformado
            sens.presets.Realista.highSpeedLeanGain     = 5f;        // fuera de rango
            sens.custom = MotoSensitivityDefaults.Normal();
            sens.custom.highSpeedLeanGain = -1f;                     // negativo

            MotoSensitivityProvider.NormalizeHighSpeedLeanGains(sens);

            Assert.AreEqual(MotoSensitivityDefaults.DefaultPrincipianteHighSpeedLeanGain, sens.presets.Principiante.highSpeedLeanGain, 1e-5f);
            Assert.AreEqual(MotoSensitivityDefaults.DefaultNormalHighSpeedLeanGain,       sens.presets.Normal.highSpeedLeanGain,       1e-5f);
            Assert.AreEqual(1f,                                                            sens.presets.Realista.highSpeedLeanGain,     1e-5f); // clamp >1
            Assert.AreEqual(MotoSensitivityDefaults.DefaultCustomHighSpeedLeanGain,        sens.custom.highSpeedLeanGain,               1e-5f);
        }
    }
}
