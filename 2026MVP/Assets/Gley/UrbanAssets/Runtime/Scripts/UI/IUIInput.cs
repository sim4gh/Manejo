namespace Gley.UrbanSystem
{
    public interface IUIInput
    {
        public float GetHorizontalInput();
        public float GetVerticalInput();
        public float GetBrakeInput() { return 0f; }
        // 0=N, 1-6=gears, -1=R
        public int GetCurrentGear() { return 0; }
        // Direccionales: -1=izq, 0=off, 1=der, 2=hazard
        public int GetIndicatorInput() { return 0; }
        // Clutch [0,1]: 0=liberado (motor acoplado), 1=pisado (motor desacoplado).
        public float GetClutchInput() { return 0f; }
        // Devuelve el conteo acumulado de cambios de marcha sin clutch desde la
        // última lectura, y resetea el contador a 0. Solo aplica en modo manual.
        public int ConsumeGearShiftsWithoutClutch() { return 0; }
        // True solo si hay un pedal de clutch físico mapeado (G923 PS).
        public bool HasPhysicalClutch() { return false; }
    }
}
