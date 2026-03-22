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
    }
}
