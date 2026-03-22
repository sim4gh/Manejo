#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Gley.UrbanSystem
{
    public class UIInputNew : MonoBehaviour, IUIInput
    {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        public delegate void ButtonDown(string button);
        public static event ButtonDown onButtonDown;
        public static void TriggerButtonDownEvent(string button) { onButtonDown?.Invoke(button); }

        public delegate void ButtonUp(string button);
        public static event ButtonUp onButtonUp;
        public static void TriggerButtonUpEvent(string button) { onButtonUp?.Invoke(button); }

        private bool left, right, up, down;
#endif

        private float horizontalInput;
        private float verticalInput;
        private float brakeInput;
        private int _currentGear;

#if !(UNITY_ANDROID || UNITY_IOS) || UNITY_EDITOR
        private InputAction _moveAction;
        private InputAction _gasAction;
        private InputAction _brakeAction;
        private InputAction _steerAction;
        private InputDevice _wheelDevice;
        private bool _hasWheel;

        // Controles cacheados (evita TryGetChildControl cada frame)
        private InputControl<float>[] _gearControls; // [7] buttons 13-19
        private InputControl<float> _l2Ctrl, _r2Ctrl, _l3Ctrl, _r3Ctrl;
        private float _menuComboTimer;
        private float _restartComboTimer;
        private const float COMBO_HOLD_TIME = 1.5f;

        // Mapeo gear: indice 0-6 → gear 1-6, R(-1)
        private static readonly int[] GearValues = { 1, 2, 3, 4, 5, 6, -1 };
#endif

        public UIInputNew Initialize()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            onButtonDown += PointerDown;
            onButtonUp += PointerUp;
#else
            GameObject steeringUI = GameObject.Find("SteeringUI");
            if (steeringUI) steeringUI.SetActive(false);
            SetupDesktopInput();
#endif
            return this;
        }

#if !(UNITY_ANDROID || UNITY_IOS) || UNITY_EDITOR
        private void SetupDesktopInput()
        {
            _moveAction = new InputAction("Move", InputActionType.Value);
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            _moveAction.AddBinding("<Gamepad>/leftStick");
            _moveAction.Enable();

            // ---- Volante G923: busqueda dinamica ----
            foreach (var device in InputSystem.devices)
            {
                if (device.displayName.IndexOf("G923", System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                string wheel = "<" + device.layout + ">";
                _hasWheel = true;
                _wheelDevice = device;

                // Ejes via InputAction (funciona para ejes HID)
                _steerAction = new InputAction("G923_Steer", InputActionType.Value);
                _steerAction.AddBinding(wheel + "/stick/x");
                _steerAction.Enable();

                _gasAction = new InputAction("G923_Gas", InputActionType.Value);
                _gasAction.AddBinding(wheel + "/z");
                _gasAction.Enable();

                _brakeAction = new InputAction("G923_Brake", InputActionType.Value);
                _brakeAction.AddBinding(wheel + "/rz");
                _brakeAction.Enable();

                // Botones via acceso directo al device (cachear una sola vez)
                // H-shifter: buttons 13-19
                _gearControls = new InputControl<float>[7];
                for (int i = 0; i < 7; i++)
                    _gearControls[i] = CacheButton(13 + i);

                // Combos: L2(7), R2(8), L3(11), R3(12)
                _l2Ctrl = CacheButton(7);
                _r2Ctrl = CacheButton(8);
                _l3Ctrl = CacheButton(11);
                _r3Ctrl = CacheButton(12);

                Debug.Log("[UIInputNew] Volante detectado: " + device.displayName + " | Layout: " + wheel);
                break;
            }
        }

        private InputControl<float> CacheButton(int num)
        {
            var ctrl = _wheelDevice.TryGetChildControl("button" + num);
            return ctrl as InputControl<float>;
        }

        private bool IsPressed(InputControl<float> ctrl)
        {
            return ctrl != null && ctrl.ReadValue() > 0.5f;
        }
#endif

        public float GetHorizontalInput() => horizontalInput;
        public float GetVerticalInput() => verticalInput;
        public float GetBrakeInput() => brakeInput;
        public int GetCurrentGear() => _currentGear;

        private void Update()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            if (left) horizontalInput -= Time.deltaTime;
            else if (right) horizontalInput += Time.deltaTime;
            else horizontalInput = Mathf.MoveTowards(horizontalInput, 0, 5 * Time.deltaTime);
            horizontalInput = Mathf.Clamp(horizontalInput, -1f, 1f);

            if (up) verticalInput += Time.deltaTime;
            else if (down) verticalInput -= Time.deltaTime;
            else verticalInput = 0;
            verticalInput = Mathf.Clamp(verticalInput, -1f, 1f);
#else
            Vector2 kbInput = _moveAction.ReadValue<Vector2>();

            // ---- Steering ----
            if (_hasWheel)
            {
                float wheelSteer = _steerAction.ReadValue<float>();
                horizontalInput = Mathf.Abs(wheelSteer) > 0.01f ? wheelSteer : kbInput.x;
            }
            else
            {
                horizontalInput = kbInput.x;
            }

            // ---- Gas / Brake ----
            if (Mathf.Abs(kbInput.y) > 0.01f)
            {
                verticalInput = kbInput.y;
                brakeInput = kbInput.y < 0 ? -kbInput.y : 0f;
            }
            else if (_hasWheel)
            {
                float gas = (1f - _gasAction.ReadValue<float>()) / 2f;
                float brakeLinear = (1f - _brakeAction.ReadValue<float>()) / 2f;
                float brake = Mathf.Clamp01(brakeLinear * brakeLinear * 2f);
                verticalInput = gas;
                brakeInput = brake;
            }
            else
            {
                verticalInput = kbInput.y;
                brakeInput = kbInput.y < 0 ? -kbInput.y : 0f;
            }

            // ---- Botones G923 (acceso directo, sin InputAction) ----
            if (_hasWheel && _wheelDevice != null)
            {
                // H-shifter
                _currentGear = 0;
                if (_gearControls != null)
                {
                    for (int i = 0; i < _gearControls.Length; i++)
                    {
                        if (IsPressed(_gearControls[i]))
                        {
                            _currentGear = GearValues[i];
                            break;
                        }
                    }
                }

                // Combo L2+R2 hold → menu principal
                if (IsPressed(_l2Ctrl) && IsPressed(_r2Ctrl))
                {
                    _menuComboTimer += Time.deltaTime;
                    if (_menuComboTimer >= COMBO_HOLD_TIME)
                    {
                        _menuComboTimer = 0f;
                        Time.timeScale = 1f;
                        SceneManager.LoadScene("MainMenu");
                    }
                }
                else { _menuComboTimer = 0f; }

                // Combo L3+R3 hold → reiniciar escena
                if (IsPressed(_l3Ctrl) && IsPressed(_r3Ctrl))
                {
                    _restartComboTimer += Time.deltaTime;
                    if (_restartComboTimer >= COMBO_HOLD_TIME)
                    {
                        _restartComboTimer = 0f;
                        Time.timeScale = 1f;
                        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                    }
                }
                else { _restartComboTimer = 0f; }
            }
#endif
        }

#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        private void PointerDown(string name)
        {
            if (name == "Restart") SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            if (name == "Left")  { left = true; right = false; }
            if (name == "Right") { right = true; left = false; }
            if (name == "Up")    { up = true; down = false; }
            if (name == "Down")  { down = true; up = false; }
        }

        private void PointerUp(string name)
        {
            if (name == "Left")  left  = false;
            if (name == "Right") right = false;
            if (name == "Up")    up    = false;
            if (name == "Down")  down  = false;
        }
#endif

        private void OnDestroy()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            onButtonDown -= PointerDown;
            onButtonUp -= PointerUp;
#else
            _moveAction?.Disable(); _moveAction?.Dispose();
            _gasAction?.Disable();  _gasAction?.Dispose();
            _brakeAction?.Disable(); _brakeAction?.Dispose();
            _steerAction?.Disable(); _steerAction?.Dispose();
#endif
        }
    }
}
#endif
