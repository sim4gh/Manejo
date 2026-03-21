namespace Gley.UrbanSystem
{
    public interface IUIInput
    {
        public float GetHorizontalInput();
        public float GetVerticalInput();
        public float GetBrakeInput() { return 0f; }
        // 0=N, 1-6=gears, -1=R
        public int GetCurrentGear() { return 0; }
    }
}
