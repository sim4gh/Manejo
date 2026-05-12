using System.Collections.Generic;
using UnityEngine;

namespace TlaxSim.MotoCalibration
{
    // Verifica que un MotoMapping cargado resuelve a controles existentes
    // del device conectado. Si algún control required no resuelve, lista la
    // falta — MenuScreenManager moto fast-path muestra modal bloqueante.
    //
    // Análogo a HoriPreflightCheck / G923PreflightCheck pero más simple
    // (sin manual/auto switching, sin gears, sin reverse).
    public interface IMotoDeviceResolver
    {
        bool ResolveAxis(string path);
        bool ResolveButton(string path);
    }

    public class MotoPreflightResult
    {
        public bool IsOk => Missing.Count == 0;
        public List<string> Missing = new List<string>();
    }

    public static class MotoPreflightCheck
    {
        public static MotoPreflightResult Validate(MotoMapping m, IMotoDeviceResolver resolver)
        {
            var r = new MotoPreflightResult();
            if (m == null) { r.Missing.Add("mapping=null"); return r; }
            if (resolver == null) { r.Missing.Add("resolver=null"); return r; }

            // Axes required: lean, handlebar, gas. Path vacío = unreachable.
            if (string.IsNullOrEmpty(m.axes.lean.path) || !resolver.ResolveAxis(m.axes.lean.path))
                r.Missing.Add("lean");
            if (string.IsNullOrEmpty(m.axes.handlebar.path) || !resolver.ResolveAxis(m.axes.handlebar.path))
                r.Missing.Add("handlebar");
            if (string.IsNullOrEmpty(m.axes.gas.path) || !resolver.ResolveAxis(m.axes.gas.path))
                r.Missing.Add("gas");

            // Buttons: brake required, clutch opcional (algunas escenas Moto no lo usan).
            // Si brake.required=true Y no resuelve → falta. Default required=false →
            // se valida cuando required=true ó cuando el escenario lo requiere.
            // Mantenemos comportamiento simple: brake siempre se valida, clutch nunca.
            if (string.IsNullOrEmpty(m.buttons.brake.path) || !resolver.ResolveButton(m.buttons.brake.path))
                r.Missing.Add("brake");
            // clutch: opcional, no se añade a Missing aunque no resuelva.

            return r;
        }
    }
}
