#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine;
using UnityEngine.InputSystem;

#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
using UnityEngine.SceneManagement;
#endif

namespace Gley.UrbanSystem
{
    public class UIInputNew : MonoBehaviour, IUIInput
    {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        // Events used for UI buttons only on mobile devices
        public delegate void ButtonDown(string button);
        public static event ButtonDown onButtonDown;

        public static void TriggerButtonDownEvent(string button)
        {
            onButtonDown?.Invoke(button);
        }

        public delegate void ButtonUp(string button);
        public static event ButtonUp onButtonUp;

        public static void TriggerButtonUpEvent(string button)
        {
            onButtonUp?.Invoke(button);
        }

        private bool left, right, up, down;
#endif

        private float horizontalInput;
        private float verticalInput;
        private float brakeInput;

#if !(UNITY_ANDROID || UNITY_IOS) || UNITY_EDITOR
        private InputAction _moveAction;
        private InputAction _gasAction;
        private InputAction _brakeAction;
        private InputAction _steerAction;  // G923 steering (float, separate from Vector2 _moveAction)
        private InputAction[] _gearActions; // H-shifter buttons 13-19
        private bool _hasWheel = false;
        private int _currentGear = 0; // 0=N, 1-6=gears, -1=R
#endif

        /// <summary>
        /// Initializes the input system based on platform used
        /// </summary>
        public UIInputNew Initialize()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            onButtonDown += PointerDown;
            onButtonUp += PointerUp;
#else
            GameObject steeringUI = GameObject.Find("SteeringUI");
            if (steeringUI)
            {
                steeringUI.SetActive(false);
            }

            SetupDesktopInput();
#endif
            return this;
        }

#if !(UNITY_ANDROID || UNITY_IOS) || UNITY_EDITOR
        private void SetupDesktopInput()
        {
            // ---- Teclado + Gamepad (igual que antes) ----
            _moveAction = new InputAction("Move", InputActionType.Value);

            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");

            _moveAction.AddBinding("<Gamepad>/leftStick");

            _moveAction.Enable();

            // ---- Volante G923: busqueda dinamica ----
            foreach (var device in InputSystem.devices)
            {
                if (device.displayName.IndexOf("G923", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    string wheel = "<" + device.layout + ">";
                    _hasWheel = true;

                    // Direccion: accion float separada (NO agregar a _moveAction que es Vector2)
                    _steerAction = new InputAction("G923_Steer", InputActionType.Value);
                    _steerAction.AddBinding(wheel + "/stick/x");
                    _steerAction.Enable();

                    // Acelerador (eje Z): raw 1=suelto, -1=pisado
                    _gasAction = new InputAction("G923_Gas", InputActionType.Value);
                    _gasAction.AddBinding(wheel + "/z");
                    _gasAction.Enable();

                    // Freno (eje RZ): raw 1=suelto, -1=pisado
                    _brakeAction = new InputAction("G923_Brake", InputActionType.Value);
                    _brakeAction.AddBinding(wheel + "/rz");
                    _brakeAction.Enable();

                    // H-shifter: buttons 13-18 = gears 1-6, button19 = R
                    _gearActions = new InputAction[7];
                    int[] gearValues = { 1, 2, 3, 4, 5, 6, -1 }; // 1-6 + R
                    for (int i = 0; i < 7; i++)
                    {
                        int buttonNum = 13 + i; // button13 to button19
                        int gearVal = gearValues[i];
                        _gearActions[i] = new InputAction("G923_Gear" + buttonNum, InputActionType.Button);
                        _gearActions[i].AddBinding(wheel + "/button" + buttonNum);
                        _gearActions[i].performed += ctx => _currentGear = gearVal;
                        _gearActions[i].canceled += ctx => _currentGear = 0;
                        _gearActions[i].Enable();
                    }

                    Debug.Log("[UIInputNew] Volante detectado: " + device.displayName + " | Layout: " + wheel);
                    break;
                }
            }
        }
#endif

        /// <summary>
        /// Get steering input
        /// </summary>
        public float GetHorizontalInput()
        {
            return horizontalInput;
        }

        /// <summary>
        /// Get acceleration input (0 to 1 for gas, no brake mixed in when wheel connected)
        /// </summary>
        public float GetVerticalInput()
        {
            return verticalInput;
        }

        /// <summary>
        /// Get brake input (0 to 1)
        /// </summary>
        public float GetBrakeInput()
        {
            return brakeInput;
        }

        /// <summary>
        /// Get current gear: 0=N, 1-6=gears, -1=R
        /// </summary>
        public int GetCurrentGear()
        {
            return _currentGear;
        }

        /// <summary>
        /// Read input
        /// </summary>
        private void Update()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            // Horizontal
            if (left)
            {
                horizontalInput -= Time.deltaTime;
            }
            else if (right)
            {
                horizontalInput += Time.deltaTime;
            }
            else
            {
                horizontalInput = Mathf.MoveTowards(horizontalInput, 0, 5 * Time.deltaTime);
            }

            horizontalInput = Mathf.Clamp(horizontalInput, -1f, 1f);

            // Vertical
            if (up)
            {
                verticalInput += Time.deltaTime;
            }
            else if (down)
            {
                verticalInput -= Time.deltaTime;
            }
            else
            {
                verticalInput = 0;
            }

            verticalInput = Mathf.Clamp(verticalInput, -1f, 1f);
#else
            // G923 steering tiene prioridad si esta conectado
            if (_hasWheel && _steerAction != null)
            {
                float wheelSteer = _steerAction.ReadValue<float>();
                // Si el volante se esta moviendo, usarlo
                if (Mathf.Abs(wheelSteer) > 0.01f)
                    horizontalInput = wheelSteer;
                else
                    horizontalInput = _moveAction.ReadValue<Vector2>().x; // fallback teclado
            }
            else
            {
                horizontalInput = _moveAction.ReadValue<Vector2>().x;
            }

            // Pedales G923 o teclado para vertical
            Vector2 kbInput = _moveAction.ReadValue<Vector2>();
            if (Mathf.Abs(kbInput.y) > 0.01f)
            {
                // Teclado/gamepad: gas y freno combinados en vertical
                verticalInput = kbInput.y;
                brakeInput = kbInput.y < 0 ? -kbInput.y : 0f;
            }
            else if (_hasWheel && _gasAction != null && _brakeAction != null)
            {
                // Pedales G923: raw va de 1 (suelto) a -1 (pisado)
                // Normalizar a 0 (suelto) - 1 (pisado): (1 - raw) / 2
                float rawGas = _gasAction.ReadValue<float>();
                float rawBrake = _brakeAction.ReadValue<float>();
                float gas = (1f - rawGas) / 2f;
                float brakeLinear = (1f - rawBrake) / 2f;
                // Curva exponencial para el freno:
                // Primera mitad del pedal (0-0.5): frenado suave/progresivo
                // Segunda mitad (0.5-1.0): frenado agresivo, escala rapido
                float brake = brakeLinear * brakeLinear * 2f;
                brake = Mathf.Clamp01(brake);
                verticalInput = gas;
                brakeInput = brake;
            }
            else
            {
                verticalInput = kbInput.y;
                brakeInput = kbInput.y < 0 ? -kbInput.y : 0f;
            }
#endif
        }

#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        private void PointerDown(string name)
        {
            if (name == "Restart")
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }

            if (name == "Left")
            {
                left = true;
                right = false;
            }
            if (name == "Right")
            {
                right = true;
                left = false;
            }
            if (name == "Up")
            {
                up = true;
                down = false;
            }
            if (name == "Down")
            {
                down = true;
                up = false;
            }
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
            _moveAction?.Disable();
            _gasAction?.Disable();
            _brakeAction?.Disable();
            _steerAction?.Disable();
            if (_gearActions != null)
                foreach (var a in _gearActions) a?.Disable();
#endif
        }
    }
}
#endif