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
        private int _indicatorInput; // -1=izq, 0=off, 1=der, 2=hazard
        private bool _isAutomaticMode;

#if !(UNITY_ANDROID || UNITY_IOS) || UNITY_EDITOR
        private InputAction _moveAction;
        private InputDevice _wheelDevice;
        private bool _hasWheel;

        // Polling (el HID del volante puede registrarse después de Initialize)
        private float _detectionTimer = 999f; // primer intento en el primer frame
        private float _redetectLogTimer = 0f;
        private float _debugLogTimer = 0f;
        private bool _debugOverlay = false;

        // Calibración de pedal por rest+press (capturados en pantalla del menú).
        // Fórmula: presionado_fracción = clamp((raw - rest) / (press - rest), 0, 1).
        // Funciona para cualquier rest (±1, 0) y cualquier dirección de press.
        private float _gasRest = 1f;
        private float _gasPress = -1f;
        private float _brakeRest = 1f;
        private float _brakePress = -1f;

        // Curva de respuesta del volante: f(x) = x / (a + (1-a)*x)  para x ∈ [0,1]
        // Amplifica mucho los ángulos pequeños (cambio de carril ≈ 5-10% del
        // rango físico del G923), y se aplana al acercarse al 100% — NO satura
        // prematuramente. Propiedades:
        //   f(0) = 0, f(1) = 1 siempre
        //   a ∈ (0, 1); menor a → más agresivo en pequeños giros
        //   a = 0.5 → f(0.1)=0.18, f(0.3)=0.46, f(0.5)=0.67 (moderado)
        //   a = 0.45 → f(0.1)=0.20, f(0.3)=0.49, f(0.5)=0.69 (mejor para carril)
        //   a = 1.0 → lineal (sin curva)
        // Pow(x, N) no sirve: o amplifica todo por igual o nada en pequeños.
        private const float STEER_CURVE_A = 0.45f;

        // Curva del freno por tramos lineales:
        //   [0, BRAKE_SOFT_END]  → freno suave, llega a BRAKE_SOFT_MAX_OUTPUT
        //   [BRAKE_SOFT_END, 1]  → freno de poder, sube rápido a 1.0
        // Defaults: 80% del pedal da 30% de freno; el 20% restante lleva 30%→100%.
        private const float BRAKE_SOFT_END = 0.8f;
        private const float BRAKE_SOFT_MAX_OUTPUT = 0.3f;

        // Calibración del steering por rango físico alcanzable (center/max/min).
        // Guardada en PlayerPrefs al completar las fases 1 y 2 del menú.
        // Si el volante solo llega a raw=±0.9 mecánicamente, normalizar contra
        // ese rango da ±1.0 al tope — sin este paso, el jugador pierde el 10%.
        private float _steerCenter = 0f;
        private float _steerMax = 1f;
        private float _steerMin = -1f;

        // Ejes cacheados (lectura directa del device — sin InputAction binding,
        // porque el layout HID del G923 trae "::" y espacios que a veces no resuelve)
        private InputControl<float> _steerCtrl; // stick/x
        private InputControl<float> _gasCtrl;   // z
        private InputControl<float> _brakeCtrl; // rz

        // Controles cacheados (evita TryGetChildControl cada frame)
        private InputControl<float>[] _gearControls; // [7] buttons 13-19
        private InputControl<float> _l2Ctrl, _r2Ctrl, _l3Ctrl, _r3Ctrl;
        private InputControl<float> _l1Ctrl, _r1Ctrl; // paddles para direccionales
        private InputControl<float> _crossCtrl;     // button2 = × (Cross) → Reversa
        private InputControl<float> _triangleCtrl;   // button4 = △ (Triangle) → Drive
        private bool _lastCrossPressed;
        private bool _lastTrianglePressed;
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
            _isAutomaticMode = PlayerPrefs.GetInt("TransmisionManual", 0) == 0;
            GameObject steeringUI = GameObject.Find("SteeringUI");
            if (steeringUI) steeringUI.SetActive(false);
            SetupDesktopInput();
            // Sin volante → forzar automático (teclado necesita funcionar)
            if (!_hasWheel) _isAutomaticMode = true;
            if (_isAutomaticMode) _currentGear = 1; // Start in Drive
#endif
            return this;
        }

        public bool IsAutomaticMode() => _isAutomaticMode;

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
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/k").With("Down", "<Keyboard>/j")
                .With("Left", "<Keyboard>/h").With("Right", "<Keyboard>/l");
            _moveAction.AddBinding("<Gamepad>/leftStick");
            _moveAction.Enable();

            TryDetectWheel(); // primer intento; Update() hace polling si aún no está
        }

        // Detecta el volante y cachea todos los controles. Idempotente:
        // llamar múltiples veces es barato si ya se encontró.
        // El G923 reporta distinto según el modo (PS/Xbox) y el driver:
        //   - PS:    "Logitech G923 Racing Wheel for PS4"
        //   - Xbox:  "Logitech G923 Racing Wheel for Xbox One and PC"
        //            o "Steering Wheel for Xbox One and PC" (sin GHUB)
        //   - G920:  "Logitech G920 Driving Force Racing Wheel"
        private bool TryDetectWheel()
        {
            if (_hasWheel) return true;

            foreach (var device in InputSystem.devices)
            {
                string name = device.displayName ?? string.Empty;
                bool isWheel =
                    name.IndexOf("G923", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("G920", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Driving Force", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Racing Wheel", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Steering Wheel", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Driving Wheel", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (!isWheel) continue;

                _hasWheel = true;
                _wheelDevice = device;

                // Ejes — paths calibrados dinámicamente en la pantalla del menú.
                // Si no hay pref, defaults razonables (algunos volantes funcionan así).
                _steerCtrl = CacheControl("stick/x");
                string gasPath   = PlayerPrefs.GetString("G923_GasAxis", "z");
                string brakePath = PlayerPrefs.GetString("G923_BrakeAxis", "rz");
                _gasCtrl   = CacheControl(gasPath);
                _brakeCtrl = CacheControl(brakePath);

                // H-shifter: buttons 13-19
                _gearControls = new InputControl<float>[7];
                for (int i = 0; i < 7; i++)
                    _gearControls[i] = CacheButton(13 + i);

                // Combos: L2(7), R2(8), L3(11), R3(12)
                _l2Ctrl = CacheButton(7);
                _r2Ctrl = CacheButton(8);
                _l3Ctrl = CacheButton(11);
                _r3Ctrl = CacheButton(12);

                // Paddles: direccionales (button5=R1=der, button6=L1=izq en G923 PS)
                _r1Ctrl = CacheButton(5);
                _l1Ctrl = CacheButton(6);

                // Face buttons: gear automático
                _crossCtrl   = CacheButton(2); // × → Reversa
                _triangleCtrl = CacheButton(4); // △ → Drive

                // Volante apareció después de Initialize → respetar PlayerPrefs de transmisión
                _isAutomaticMode = PlayerPrefs.GetInt("TransmisionManual", 0) == 0;
                if (_isAutomaticMode && _currentGear == 0) _currentGear = 1;

                // Cargar calibración de pedales (rest+press) hecha en el menú.
                // Defaults: rest=1, press=-1 (convención común, pero puede fallar
                // en G923s con mapeo invertido — la pantalla de calibración lo cubre).
                _gasRest   = PlayerPrefs.GetFloat("G923_GasRest",  1f);
                _gasPress  = PlayerPrefs.GetFloat("G923_GasPress", -1f);
                _brakeRest = PlayerPrefs.GetFloat("G923_BrakeRest",  1f);
                _brakePress = PlayerPrefs.GetFloat("G923_BrakePress", -1f);

                // Calibración del steering (si no existe, rango ideal -1..1 sin offset)
                _steerCenter = PlayerPrefs.GetFloat("G923_SteerCenter", 0f);
                _steerMax    = PlayerPrefs.GetFloat("G923_SteerMax",   1f);
                _steerMin    = PlayerPrefs.GetFloat("G923_SteerMin",  -1f);

                Debug.Log("[UIInputNew] Volante detectado: " + device.displayName
                    + " | steer=" + (_steerCtrl != null)
                    + " gas[" + gasPath + "]=" + (_gasCtrl != null)
                    + " brake[" + brakePath + "]=" + (_brakeCtrl != null));
                return true;
            }
            return false;
        }

        // Normaliza pedal a [0,1] usando rest+press calibrados en pantalla.
        // Funciona sin importar la dirección del eje (rest puede ser > o < press).
        private float NormalizePedal(float raw, float rest, float press)
        {
            float span = press - rest;
            if (Mathf.Abs(span) < 0.05f) return 0f;
            return Mathf.Clamp01((raw - rest) / span);
        }

        // Curva del freno: tramo 1 suave (0..BRAKE_SOFT_END pedal →
        // 0..BRAKE_SOFT_MAX_OUTPUT freno), tramo 2 fuerte ("freno de poder").
        private float BrakeCurve(float x)
        {
            if (x <= 0f) return 0f;
            if (x >= 1f) return 1f;
            if (x < BRAKE_SOFT_END)
                return (x / BRAKE_SOFT_END) * BRAKE_SOFT_MAX_OUTPUT;
            float hard = (x - BRAKE_SOFT_END) / (1f - BRAKE_SOFT_END);
            return BRAKE_SOFT_MAX_OUTPUT + hard * (1f - BRAKE_SOFT_MAX_OUTPUT);
        }

        // Normaliza steering a [-1, 1] mapeando el rango físico (center, max, min)
        // alcanzable por el volante. Si el volante solo llega a raw=+0.9, giro
        // completo derecha → 1.0. Preserva la dirección del signo al convertir.
        private float NormalizeSteer(float raw)
        {
            if (raw >= _steerCenter)
            {
                float span = _steerMax - _steerCenter;
                if (span < 0.05f) return 0f;
                return Mathf.Clamp01((raw - _steerCenter) / span);
            }
            else
            {
                float span = _steerCenter - _steerMin;
                if (span < 0.05f) return 0f;
                return -Mathf.Clamp01((_steerCenter - raw) / span);
            }
        }

        private InputControl<float> CacheButton(int num)
        {
            return CacheControl("button" + num);
        }

        private InputControl<float> CacheControl(string path)
        {
            var ctrl = _wheelDevice.TryGetChildControl(path);
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
        public int GetIndicatorInput() => _indicatorInput;

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
            // ---- Polling del volante (puede aparecer después de Initialize) ----
            if (!_hasWheel)
            {
                _detectionTimer += Time.deltaTime;
                if (_detectionTimer >= 0.5f)
                {
                    _detectionTimer = 0f;
                    if (!TryDetectWheel())
                    {
                        _redetectLogTimer += 0.5f;
                        if (_redetectLogTimer >= 3f)
                        {
                            _redetectLogTimer = 0f;
                            string all = "";
                            foreach (var d in InputSystem.devices)
                                all += "\n  - " + d.displayName + " [" + d.layout + "]";
                            Debug.LogWarning("[UIInputNew] Sin volante aún. Devices:" + all);
                        }
                    }
                }
            }

            // ---- Toggle debug overlay (F10) ----
            if (Keyboard.current != null && Keyboard.current.f10Key.wasPressedThisFrame)
                _debugOverlay = !_debugOverlay;

            Vector2 kbInput = _moveAction.ReadValue<Vector2>();

            // ---- Steering: calibrado (rango físico) + curva racional (respuesta) ----
            if (_hasWheel)
            {
                float rawSteer = _steerCtrl != null ? _steerCtrl.ReadValue() : _steerCenter;
                float norm = NormalizeSteer(rawSteer); // [-1, 1] sobre rango físico real
                if (Mathf.Abs(norm) > 0.01f)
                {
                    float absN = Mathf.Abs(norm);
                    // f(x) = x / (a + (1-a)x) — agresiva en pequeños, aplanada arriba
                    float curved = absN / (STEER_CURVE_A + (1f - STEER_CURVE_A) * absN);
                    horizontalInput = Mathf.Sign(norm) * curved;
                }
                else
                {
                    horizontalInput = kbInput.x;
                }
            }
            else
            {
                horizontalInput = kbInput.x;
            }

            // ---- Gas / Brake + Gear ----
            if (_isAutomaticMode)
            {
                // Teclado: R para toggle D↔R en automático
                if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                    _currentGear = (_currentGear == -1) ? 1 : -1;

                // Automático: arriba=Drive(A), abajo=Reversa(R) directa
                if (kbInput.y > 0.01f)
                {
                    _currentGear = 1;
                    verticalInput = kbInput.y;
                    brakeInput = 0f;
                }
                else if (kbInput.y < -0.01f)
                {
                    _currentGear = -1;
                    verticalInput = -kbInput.y; // flip a positivo para gas en reversa
                    brakeInput = 0f;
                }
                else if (_hasWheel)
                {
                    // Sin teclado: pedales del volante, mantener último gear
                    float gasRaw = _gasCtrl != null ? _gasCtrl.ReadValue() : _gasRest;
                    float brakeRaw = _brakeCtrl != null ? _brakeCtrl.ReadValue() : _brakeRest;
                    float gas = NormalizePedal(gasRaw, _gasRest, _gasPress);
                    float brakeLinear = NormalizePedal(brakeRaw, _brakeRest, _brakePress);
                    // Curva por tramos: suave hasta 80% pedal, "freno de poder" en el último 20%
                    float brake = BrakeCurve(brakeLinear);
                    verticalInput = gas;
                    brakeInput = brake;

                    // × = Reversa, △ = Drive (solo automático con volante)
                    bool crossNow = IsPressed(_crossCtrl);
                    if (crossNow && !_lastCrossPressed) _currentGear = -1;
                    _lastCrossPressed = crossNow;

                    bool triNow = IsPressed(_triangleCtrl);
                    if (triNow && !_lastTrianglePressed) _currentGear = 1;
                    _lastTrianglePressed = triNow;
                }
                else
                {
                    verticalInput = 0f;
                    brakeInput = 0f;
                }
            }
            else
            {
                // Manual: gas/brake existente
                if (Mathf.Abs(kbInput.y) > 0.01f)
                {
                    verticalInput = kbInput.y;
                    brakeInput = kbInput.y < 0 ? -kbInput.y : 0f;
                }
                else if (_hasWheel)
                {
                    float gasRaw = _gasCtrl != null ? _gasCtrl.ReadValue() : _gasRest;
                    float brakeRaw = _brakeCtrl != null ? _brakeCtrl.ReadValue() : _brakeRest;
                    float gas = NormalizePedal(gasRaw, _gasRest, _gasPress);
                    float brakeLinear = NormalizePedal(brakeRaw, _brakeRest, _brakePress);
                    float brake = BrakeCurve(brakeLinear);
                    verticalInput = gas;
                    brakeInput = brake;
                }
                else
                {
                    verticalInput = kbInput.y;
                    brakeInput = kbInput.y < 0 ? -kbInput.y : 0f;
                }

                // Manual: H-shifter del volante
                if (_hasWheel && _wheelDevice != null)
                {
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
                }
            }

            // ---- Paddles G923 (solo con volante) ----
            if (_hasWheel && _wheelDevice != null)
            {
                // Paddles → direccionales
                bool l1 = IsPressed(_l1Ctrl);
                bool r1 = IsPressed(_r1Ctrl);
                if (l1 && r1) _indicatorInput = 2;       // ambos = hazard
                else if (l1) _indicatorInput = -1;        // izquierda
                else if (r1) _indicatorInput = 1;         // derecha
                else _indicatorInput = 0;
            }

            // ---- Combos: volante + teclado (ambas fuentes, con o sin volante) ----
            bool menuCombo = false;
            bool restartCombo = false;

            if (_hasWheel && _wheelDevice != null)
            {
                if (IsPressed(_l2Ctrl) && IsPressed(_r2Ctrl)) menuCombo = true;
                if (IsPressed(_l3Ctrl) && IsPressed(_r3Ctrl)) restartCombo = true;
            }

            if (Keyboard.current != null)
            {
                bool ctrl = Keyboard.current.leftCtrlKey.isPressed
                         || Keyboard.current.rightCtrlKey.isPressed;
                bool shift = Keyboard.current.leftShiftKey.isPressed
                          || Keyboard.current.rightShiftKey.isPressed;
                if (ctrl && shift && Keyboard.current.mKey.isPressed) menuCombo = true;
                if (ctrl && shift && Keyboard.current.sKey.isPressed) restartCombo = true;
            }

            if (menuCombo)
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

            if (restartCombo)
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

            // ---- Log periódico de valores crudos/calculados (diagnóstico kiosko) ----
            if (_hasWheel)
            {
                _debugLogTimer += Time.deltaTime;
                if (_debugLogTimer >= 2f)
                {
                    _debugLogTimer = 0f;
                    float st = _steerCtrl != null ? _steerCtrl.ReadValue() : 0f;
                    float gr = _gasCtrl != null ? _gasCtrl.ReadValue() : 1f;
                    float br = _brakeCtrl != null ? _brakeCtrl.ReadValue() : 1f;
                    Debug.Log($"[UIInputNew] raw steer={st:F3} gas={gr:F3} brake={br:F3} | V={verticalInput:F3} B={brakeInput:F3} | gasR/P={_gasRest:F2}/{_gasPress:F2} brakeR/P={_brakeRest:F2}/{_brakePress:F2} | auto={_isAutomaticMode} gear={_currentGear}");
                }
            }
#endif
        }

#if !(UNITY_ANDROID || UNITY_IOS) || UNITY_EDITOR
        private void OnGUI()
        {
            if (!_debugOverlay) return;
            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.alignment = TextAnchor.UpperLeft;
            style.fontSize = 13;
            style.normal.textColor = Color.white;

            string info = "[G923 Debug] F10=toggle\n";
            info += "Wheel: " + (_hasWheel ? "DETECTADO" : "NO DETECTADO") + "\n";
            if (_hasWheel && _wheelDevice != null)
                info += "Device: " + _wheelDevice.displayName + "\n";
            info += "Mode: " + (_isAutomaticMode ? "AUTO" : "MANUAL") + "\n";
            if (_hasWheel)
            {
                float st = _steerCtrl != null ? _steerCtrl.ReadValue() : 0f;
                float gr = _gasCtrl != null ? _gasCtrl.ReadValue() : 1f;
                float br = _brakeCtrl != null ? _brakeCtrl.ReadValue() : 1f;
                info += $"RAW  steer={st:F3} gas={gr:F3} brake={br:F3}\n";
                info += $"CALC V={verticalInput:F3} B={brakeInput:F3} gear={_currentGear}\n";
                info += $"CAL  gas rest={_gasRest:F2} press={_gasPress:F2}\n";
                info += $"     brake rest={_brakeRest:F2} press={_brakePress:F2}";
            }
            else
            {
                info += "Devices enumerados:\n";
                foreach (var d in InputSystem.devices)
                    info += "  - " + d.displayName + "\n";
            }

            GUI.Box(new Rect(10, 10, 520, 260), info, style);
        }
#endif

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
#endif
        }
    }
}
#endif
