using NUnit.Framework;
using TlaxSim.Input;

namespace TlaxSim.Tests
{
    // Tests para InputMath.NormalizePedal — la función pura que normaliza un
    // pedal raw a [0,1] usando rest/press calibrados.
    //
    // Lección 4 de HORI_CALIBRATION_LESSONS.md: tener este suite hubiera
    // pillado el bug v1.6.3 (rest=1, press=0 corruptos → freno stuck en 1.0)
    // sin necesidad de un kiosko para reproducir.
    public class InputMathTests
    {
        const float EPS = 1e-4f;

        [Test]
        public void Standard_HalfPress_Returns05()
        {
            // Pedal estilo "0..1" (ej. HORI throttle via reader)
            Assert.AreEqual(0.5f, InputMath.NormalizePedal(0.5f, 0f, 1f), EPS);
        }

        [Test]
        public void Bipolar_Quarter_Returns025()
        {
            // Pedal -1..+1 (ej. HORI brake/clutch en rz/slider)
            Assert.AreEqual(0.25f, InputMath.NormalizePedal(-0.5f, -1f, 1f), EPS);
        }

        [Test]
        public void InvertedPolarity_G923PS_Center_Returns05()
        {
            // G923 PS gas: idle=1, press=-1 → en el medio (raw=0) debe dar 0.5
            Assert.AreEqual(0.5f, InputMath.NormalizePedal(0f, 1f, -1f), EPS);
        }

        [Test]
        public void DivByZeroGuard_RestEqualsPress_ReturnsZero()
        {
            // span=0 → return 0 (guard contra divide-by-zero / NaN)
            Assert.AreEqual(0f, InputMath.NormalizePedal(0.5f, 0f, 0f), EPS);
        }

        [Test]
        public void SpanExactly005_DoesNotTriggerGuard()
        {
            // El guard corta cuando |span| < 0.05f estrictamente, no <=.
            // Span=0.05 exacto: 0.05 < 0.05 es false → NO retorna 0.
            // Pasa al cálculo: (0.025 - 0) / 0.05 = 0.5
            Assert.AreEqual(0.5f, InputMath.NormalizePedal(0.025f, 0f, 0.05f), EPS);
        }

        [Test]
        public void SpanJustBelow005_ReturnsZero()
        {
            // span = 0.04999 → guard kicks in → 0
            Assert.AreEqual(0f, InputMath.NormalizePedal(0.025f, 0f, 0.04999f), EPS);
        }

        [Test]
        public void RawAboveRange_ClampsTo1()
        {
            // raw fuera de rango por arriba (operador apretó más de lo calibrado)
            Assert.AreEqual(1f, InputMath.NormalizePedal(2f, 0f, 1f), EPS);
        }

        [Test]
        public void RawBelowRange_ClampsTo0()
        {
            // raw fuera de rango por abajo (sensor emite negativo en rest)
            Assert.AreEqual(0f, InputMath.NormalizePedal(-1f, 0f, 1f), EPS);
        }

        [Test]
        public void V163RegressionBug_BrakeStuckPressed()
        {
            // Pasajeros 2 v1.6.3: PlayerPrefs corruptas dejaron
            // G923_BrakeRest=1.0, G923_BrakePress=0.0 (polaridad invertida + range parcial).
            // El raw idle del HORI brake (rz=−1) daba: (-1-1)/(0-1) = 2 → clamp01 = 1.0
            // → freno stuck pisado al 100% siempre → carro no avanzaba ni en Auto.
            // Este test atrapa la regresión si alguien rompe el clamp.
            float actual = InputMath.NormalizePedal(-1f, 1f, 0f);
            Assert.AreEqual(1f, actual, EPS,
                "Polaridad invertida con rest=1 press=0 raw=-1 da clamp01=1.0 (bug v1.6.3)");
        }

        [Test]
        public void HoriBrakeSano_RestNeg1_PressPos1()
        {
            // HORI brake calibración correcta (rest=-1, press=1):
            // - idle (raw=-1) → 0
            // - half (raw=0) → 0.5
            // - full (raw=1) → 1
            Assert.AreEqual(0f,   InputMath.NormalizePedal(-1f, -1f, 1f), EPS, "idle=0");
            Assert.AreEqual(0.5f, InputMath.NormalizePedal( 0f, -1f, 1f), EPS, "half=0.5");
            Assert.AreEqual(1f,   InputMath.NormalizePedal( 1f, -1f, 1f), EPS, "full=1");
        }
    }
}
