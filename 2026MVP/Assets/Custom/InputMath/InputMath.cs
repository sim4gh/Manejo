using UnityEngine;

namespace TlaxSim.Input
{
    // Funciones puras de matemática de input. Aisladas de Unity Input System y
    // de MonoBehaviour para que sean testeables en EditMode (no Mono lifecycle,
    // no hardware, no async). El espejo en Gley.UrbanSystem.UIInputNew lo usa
    // vía delegate injection (mismo patrón que HoriThrottleProvider) — ver
    // RegisterImpl() abajo.
    //
    // Por qué este archivo existe: lección 4 de HORI_CALIBRATION_LESSONS.md
    // ("Test unitario de NormalizePedal con casos extremos: divide-by-zero,
    // span negativo, raw fuera de [rest, press], inversión de polaridad.
    // Hubiera pillado el bug original sin necesidad de un kiosko").
    public static class InputMath
    {
        /// <summary>
        /// Normaliza un pedal raw a [0,1] usando rest+press calibrados.
        /// Funciona sin importar dirección del eje (rest puede ser &gt; o &lt; press).
        /// Retorna 0 si span (|press-rest|) es &lt; 0.05 (guard de divide-by-zero).
        /// </summary>
        public static float NormalizePedal(float raw, float rest, float press)
        {
            float span = press - rest;
            if (Mathf.Abs(span) < 0.05f) return 0f;
            return Mathf.Clamp01((raw - rest) / span);
        }

        // Inyecta esta implementación como la que usa UIInputNew en runtime, así
        // garantizamos que tests y producción usan la misma fórmula. Si en
        // futuro alguien cambia uno sin el otro, esto los re-sincroniza al boot.
        // Cross-asmdef Custom→Gley está permitido (Custom referencia
        // Gley.UrbanSystem; lo inverso no, por eso usamos delegate injection).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterImpl()
        {
            Gley.UrbanSystem.UIInputNew.NormalizePedalImpl = NormalizePedal;
        }
    }
}
