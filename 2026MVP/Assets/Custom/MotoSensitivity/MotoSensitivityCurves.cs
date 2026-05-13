using System;
using UnityEngine;

namespace TlaxSim.MotoSensitivity
{
    // Helpers puros para aplicar curvas, deadzone, scale, y ramp limit.
    // Sin dependencias de Unity runtime (solo Mathf.* y System.Math). Diseñados
    // para EditMode tests y reuso en MotoSensitivityPanel para preview gráfico.
    //
    // Ver docs/superpowers/specs/2026-05-13-moto-sensitivity-design.md sección 5.
    public static class MotoSensitivityCurves
    {
        public static float ApplyCurve(float x, string curveType, float param)
        {
            if (curveType == "pow" && param > 0f)
            {
                float ax = Mathf.Abs(x);
                float pow = Mathf.Pow(ax, param);
                return x < 0f ? -pow : pow;
            }
            return x;  // linear (default y fallback)
        }

        public static float ApplyDeadzone(float x, float dz)
        {
            if (dz <= 0f) return x;
            float ax = Mathf.Abs(x);
            if (ax <= dz) return 0f;
            float rescaled = (ax - dz) / (1f - dz);
            return x < 0f ? -rescaled : rescaled;
        }

        public static float ApplyScale(float x, float scale)
        {
            return x * scale;
        }

        // target = valor objetivo (post-curva), prev = valor del frame anterior,
        // ratePerSec = unidades por segundo, dt = Time.deltaTime.
        // Solo limita subida; bajadas son instantáneas (safety).
        public static float ApplyRampLimit(float target, float prev, float ratePerSec, float dt)
        {
            if (target <= prev) return target;
            float maxStep = ratePerSec * dt;
            return Mathf.Min(target, prev + maxStep);
        }

        // Composite para lean / hbar. Orden: deadzone → curve → scale → clamp[-1,+1].
        public static float ApplyAxis(float normalized, AxisSensitivity cfg)
        {
            float x = ApplyDeadzone(normalized, cfg.deadzone);
            x = ApplyCurve(x, cfg.curveType, cfg.curveParam);
            x = ApplyScale(x, cfg.scale);
            return Mathf.Clamp(x, -1f, +1f);
        }

        // Composite para gas. Orden: deadzone → curve → ramp → scale → clamp[0,1].
        // Devuelve el resultado Y actualiza prev para el siguiente frame.
        public static float ApplyPedal(float normalized, ref float prev, PedalSensitivity cfg, float dt)
        {
            float x = ApplyDeadzone(normalized, cfg.deadzone);
            x = ApplyCurve(x, cfg.curveType, cfg.curveParam);
            x = ApplyRampLimit(x, prev, cfg.rampUpPerSec, dt);
            prev = x;  // cachear ANTES del scale para que el ramp opere en espacio post-curve, pre-scale
            x = ApplyScale(x, cfg.scale);
            return Mathf.Clamp(x, 0f, 1f);
        }
    }
}
