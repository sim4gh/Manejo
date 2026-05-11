using System.Collections.Generic;
using UnityEngine;

namespace TlaxSim.HoriCalibration
{
    // Interfaz para resolver paths a controles vivos del device.
    // En runtime, el resolver consulta los InputDevices reales via UIInputNew.
    public interface IHoriDeviceResolver
    {
        bool AxisResolves(string path);
        bool ButtonResolves(string path);
        bool ThrottleReaderOk { get; }
    }

    public class PreflightResult
    {
        public bool IsOk => Missing.Count == 0;
        public List<string> Missing = new List<string>();
    }

    public static class HoriPreflightCheck
    {
        // manual=true incluye clutch+gears como required. manual=false los ignora.
        public static PreflightResult Validate(HoriMapping m, IHoriDeviceResolver resolver, bool manual)
        {
            var r = new PreflightResult();
            if (m == null)
            {
                r.Missing.Add("Active mapping");
                return r;
            }

            // Steer
            if (!resolver.AxisResolves(m.axes.steer.path)) r.Missing.Add($"Volante ({m.axes.steer.path})");

            // Gas via HoriThrottleReader
            if (!resolver.ThrottleReaderOk) r.Missing.Add("Acelerador (HoriThrottleReader handle CLOSED)");

            // Brake siempre required
            if (!resolver.AxisResolves(m.axes.brake.path)) r.Missing.Add($"Freno ({m.axes.brake.path})");

            // Clutch solo en Manual
            if (manual && !resolver.AxisResolves(m.axes.clutch.path)) r.Missing.Add($"Clutch ({m.axes.clutch.path})");

            // Buttons always-required
            if (!resolver.ButtonResolves(m.buttons.horn.path)) r.Missing.Add("Claxon");
            if (!resolver.ButtonResolves(m.buttons.hazards.path)) r.Missing.Add("Intermitentes");
            if (!resolver.ButtonResolves(m.buttons.turnLeft.path)) r.Missing.Add("Flecha izquierda");
            if (!resolver.ButtonResolves(m.buttons.turnRight.path)) r.Missing.Add("Flecha derecha");
            if (!resolver.ButtonResolves(m.buttons.reverse.path)) r.Missing.Add("Reversa");

            // Gears solo en Manual
            if (manual)
            {
                if (!resolver.ButtonResolves(m.buttons.gear1.path)) r.Missing.Add($"Marcha 1 ({m.buttons.gear1.path})");
                if (!resolver.ButtonResolves(m.buttons.gear2.path)) r.Missing.Add($"Marcha 2 ({m.buttons.gear2.path})");
                if (!resolver.ButtonResolves(m.buttons.gear3.path)) r.Missing.Add($"Marcha 3 ({m.buttons.gear3.path})");
                if (!resolver.ButtonResolves(m.buttons.gear4.path)) r.Missing.Add($"Marcha 4 ({m.buttons.gear4.path})");
                if (!resolver.ButtonResolves(m.buttons.gear5.path)) r.Missing.Add($"Marcha 5 ({m.buttons.gear5.path})");
                if (!resolver.ButtonResolves(m.buttons.gear6.path)) r.Missing.Add($"Marcha 6 ({m.buttons.gear6.path})");
            }

            return r;
        }
    }
}
