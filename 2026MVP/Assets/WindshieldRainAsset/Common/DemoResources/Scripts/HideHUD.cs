using UnityEngine;


namespace ShadedTechnology.WindshieldRainAsset.Demo
{
    public class HideHUD : MonoBehaviour
    {
        public GameObject canvas;


        private bool canvasEnabled = true;


#if !ENABLE_INPUT_SYSTEM
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.H))
            {
                canvasEnabled = !canvasEnabled;
                canvas.SetActive(canvasEnabled);
            }
        }
#endif
#if ENABLE_INPUT_SYSTEM
        public void OnHideHUDKey(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                canvasEnabled = !canvasEnabled;
                canvas.SetActive(canvasEnabled);
            }
        }

#endif
    }
}
