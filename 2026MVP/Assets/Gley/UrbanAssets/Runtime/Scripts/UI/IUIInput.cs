namespace Gley.UrbanSystem
{
    public interface IUIInput
    {
        public float GetHorizontalInput();
        public float GetVerticalInput();
        public float GetBrakeInput() { return 0f; }
    }
}
