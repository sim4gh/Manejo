using System.Collections.Generic;
using UnityEngine;

namespace TlaxSim.G923Calibration
{
    // Verifica que un G923Mapping cargado resuelve a controles existentes
    // del device conectado. Si algún control required no resuelve, lista la
    // falta — Pantalla 2 muestra modal bloqueante.
    //
    // Análogo a HoriPreflightCheck.
    public interface IG923DeviceResolver
    {
        bool ResolveAxis(string path);
        bool ResolveButton(string path);
    }

    public class G923PreflightResult
    {
        public bool IsOk => Missing.Count == 0;
        public List<string> Missing = new List<string>();
    }

    public static class G923PreflightCheck
    {
        public static G923PreflightResult Validate(G923Mapping m, IG923DeviceResolver resolver, bool manual)
        {
            var r = new G923PreflightResult();
            if (m == null) { r.Missing.Add("mapping=null"); return r; }
            if (resolver == null) { r.Missing.Add("resolver=null"); return r; }

            if (!resolver.ResolveAxis(m.axes.steer.path)) r.Missing.Add("steer");
            if (!resolver.ResolveAxis(m.axes.gas.path)) r.Missing.Add("gas");
            if (!resolver.ResolveAxis(m.axes.brake.path)) r.Missing.Add("brake");

            // Clutch required solo en modo Manual.
            if (manual && (!resolver.ResolveAxis(m.axes.clutch.path) || string.IsNullOrEmpty(m.axes.clutch.path)))
                r.Missing.Add("clutch");

            if (m.buttons.horn.required && !resolver.ResolveButton(m.buttons.horn.path)) r.Missing.Add("horn");
            if (m.buttons.turnLeft.required && !resolver.ResolveButton(m.buttons.turnLeft.path)) r.Missing.Add("turnLeft");
            if (m.buttons.turnRight.required && !resolver.ResolveButton(m.buttons.turnRight.path)) r.Missing.Add("turnRight");
            if (m.buttons.reverse.required && !resolver.ResolveButton(m.buttons.reverse.path)) r.Missing.Add("reverse");

            // hazards en G923 puede ser combo L1+R1 (no un solo button) — relajar si vacío
            if (m.buttons.hazards.required && !string.IsNullOrEmpty(m.buttons.hazards.path))
            {
                if (!resolver.ResolveButton(m.buttons.hazards.path)) r.Missing.Add("hazards");
            }

            return r;
        }
    }
}
