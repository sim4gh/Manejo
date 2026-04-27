#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
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
        // Handler cacheado para poder unsubscribe en OnDestroy.
        private System.Action<InputDevice, InputDeviceChange> _deviceChangeHandler;

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
        //   f(0) = 0, f(1) = 1 siempre
        //   a = 1.0 → lineal puro (output = raw normalizado)
        //   a < 1.0 → amplifica ángulos pequeños, aplana grandes
        //   a = 0.5 → f(0.1)=0.18, f(0.3)=0.46, f(0.5)=0.67
        //   a = 0.45 → f(0.1)=0.20, f(0.3)=0.49, f(0.5)=0.69 (muy sensible)
        // El usuario pidió respuesta lineal — raw 0→0.2 = cambio de carril ligero,
        // raw 0.2→1.0 = dar vuelta progresivamente. A=1.0 cumple eso exactamente.
        // Parámetros tuneable en runtime via AdvancedInputPanel (F9 hold 1.5s).
        // Se persisten en PlayerPrefs con prefijo "Adv_". ReloadTuning() los relee.
        private float _steerCurveA = 0.65f;
        private float _steerDeadzone = 0.02f;
        private float _brakeSoftEnd = 0.5f;
        private float _brakeSoftMaxOutput = 0.5f;
        private float _gasCurveN = 1.0f; // exponente: <1 más respuesta inicial, >1 más control fino
        // Defaults de fábrica
        // 0.65 (vs 1.0 lineal puro) → ~40% más sensible cerca del centro,
        // mismo tope. f(0.1) pasa de 0.10 a 0.14. Resuelve el síntoma "muevo
        // 45° y no responde" sin tocar la calibración del rango.
        public const float DEFAULT_STEER_CURVE_A = 0.65f;
        public const float DEFAULT_STEER_DEADZONE = 0.02f;
        // Curva de freno más progresiva. Antes era 0-80% pedal → 0-30% freno
        // (zona muerta gigante) y luego salto al 100%. Ahora 0-50% pedal →
        // 0-50% freno (lineal) y 50-100% pedal → 50-100% freno (lineal).
        // Sigue siendo configurable por F9 panel.
        public const float DEFAULT_BRAKE_SOFT_END = 0.5f;
        public const float DEFAULT_BRAKE_SOFT_MAX_OUTPUT = 0.5f;
        public const float DEFAULT_GAS_CURVE_N = 1.0f;
        // Keys PlayerPrefs
        public const string PREF_STEER_CURVE_A = "Adv_SteerCurveA";
        public const string PREF_STEER_DEADZONE = "Adv_SteerDeadzone";
        public const string PREF_BRAKE_SOFT_END = "Adv_BrakeSoftEnd";
        public const string PREF_BRAKE_SOFT_MAX_OUTPUT = "Adv_BrakeSoftMaxOutput";
        public const string PREF_GAS_CURVE_N = "Adv_GasCurveN";

        /// <summary>
        /// Relee los parámetros de tuning desde PlayerPrefs. Llamar al iniciar
        /// y cuando el AdvancedInputPanel modifique valores (efecto en vivo).
        /// </summary>
        public void ReloadTuning()
        {
            _steerCurveA = PlayerPrefs.GetFloat(PREF_STEER_CURVE_A, DEFAULT_STEER_CURVE_A);
            _steerDeadzone = PlayerPrefs.GetFloat(PREF_STEER_DEADZONE, DEFAULT_STEER_DEADZONE);
            _brakeSoftEnd = PlayerPrefs.GetFloat(PREF_BRAKE_SOFT_END, DEFAULT_BRAKE_SOFT_END);
            _brakeSoftMaxOutput = PlayerPrefs.GetFloat(PREF_BRAKE_SOFT_MAX_OUTPUT, DEFAULT_BRAKE_SOFT_MAX_OUTPUT);
            _gasCurveN = PlayerPrefs.GetFloat(PREF_GAS_CURVE_N, DEFAULT_GAS_CURVE_N);
        }

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
        // Reversa soporta multi-path: el binding string puede contener varios
        // paths separados por '|' (OR). Útil cuando un mismo shifter firma
        // reversa en varios controles a la vez.
        private InputControl<float>[] _crossCtrls;   // reversa (multi-path OR)
        private InputControl<float> _triangleCtrl;   // drive
        private InputControl<float> _restartCtrl;    // botón único de reinicio (nuevo)
        private bool _lastCrossPressed;
        // Timestamp del último frame con señal de reversa activa.
        // Permite que la palanca H quede en R aunque el HID reporte button18
        // como pulsos transitorios (1 frame on, 1 off). Solo volvemos a D
        // cuando llevemos >REVERSE_HOLD_SECONDS sin ver señal.
        private float _reverseLastSeenTime = -999f;
        private const float REVERSE_HOLD_SECONDS = 0.3f;
        private bool _lastTrianglePressed;
        private bool _lastRestartPressed;
        private float _menuComboTimer;
        private float _restartComboTimer;
        private const float COMBO_HOLD_TIME = 1.5f;

        // Bindings configurables via BindingsPanel (F8 hold 1.5s).
        // Defaults = mapeo G923 PS (tenía hardcoded). El usuario puede
        // sobreescribir para otros volantes (ej. G923 Xbox usa otros paths).
        private string _bindSteerAxis = "stick/x";  // eje del volante
        private string _bindReverse = "button19";
        private string _bindDrive = "button4";
        private string _bindPaddleLeft = "button6";
        private string _bindPaddleRight = "button5";
        private string _bindRestart = "";           // vacío = deshabilitado
        private string _bindMenuA = "button7";      // L2
        private string _bindMenuB = "button8";      // R2
        private string _bindRestartA = "button11";  // L3
        private string _bindRestartB = "button12";  // R3

        public const string PREF_BIND_STEER_AXIS = "Bind_steerAxis";
        public const string PREF_BIND_REVERSE = "Bind_reverse";
        public const string PREF_BIND_DRIVE = "Bind_drive";
        public const string PREF_BIND_PADDLE_LEFT = "Bind_paddleLeft";
        public const string PREF_BIND_PADDLE_RIGHT = "Bind_paddleRight";
        public const string PREF_BIND_RESTART = "Bind_restart";
        public const string PREF_BIND_MENU_A = "Bind_menuA";
        public const string PREF_BIND_MENU_B = "Bind_menuB";
        public const string PREF_BIND_RESTART_A = "Bind_restartA";
        public const string PREF_BIND_RESTART_B = "Bind_restartB";

        public const string DEFAULT_BIND_STEER_AXIS = "stick/x";
        // En el G923 PS del kiosk de la demo, la posición R del H-shifter
        // dispara button19 (verificado en F7 múltiples veces). NO es phantom
        // — es la señal real de R. Solo aparece "siempre on" en F7 si el
        // operador deja el shifter en R durante el test.
        public const string DEFAULT_BIND_REVERSE = "button19";
        public const string DEFAULT_BIND_DRIVE = "button4";
        public const string DEFAULT_BIND_PADDLE_LEFT = "button6";
        public const string DEFAULT_BIND_PADDLE_RIGHT = "button5";
        public const string DEFAULT_BIND_RESTART = "";
        public const string DEFAULT_BIND_MENU_A = "button7";
        public const string DEFAULT_BIND_MENU_B = "button8";
        public const string DEFAULT_BIND_RESTART_A = "button11";
        public const string DEFAULT_BIND_RESTART_B = "button12";

        /// <summary>
        /// Relee los bindings configurables desde PlayerPrefs y re-cachea los
        /// controles del device activo. Llamar tras detectar volante y cada vez
        /// que BindingsPanel modifica algo.
        /// </summary>
        public void ReloadBindings()
        {
            // One-shot: el viejo default era "button2" (cross PS); ya no aplica al
            // H-shifter del G923 que firma reversa en button18. Si algún arranque
            // anterior lo dejó guardado, borrar para que tome el nuevo default.
            if (PlayerPrefs.GetString(PREF_BIND_REVERSE, "") == "button2")
            {
                PlayerPrefs.DeleteKey(PREF_BIND_REVERSE);
                PlayerPrefs.Save();
            }
            _bindSteerAxis    = PlayerPrefs.GetString(PREF_BIND_STEER_AXIS, DEFAULT_BIND_STEER_AXIS);
            _bindReverse      = PlayerPrefs.GetString(PREF_BIND_REVERSE, DEFAULT_BIND_REVERSE);
            _bindDrive        = PlayerPrefs.GetString(PREF_BIND_DRIVE, DEFAULT_BIND_DRIVE);
            _bindPaddleLeft   = PlayerPrefs.GetString(PREF_BIND_PADDLE_LEFT, DEFAULT_BIND_PADDLE_LEFT);
            _bindPaddleRight  = PlayerPrefs.GetString(PREF_BIND_PADDLE_RIGHT, DEFAULT_BIND_PADDLE_RIGHT);
            _bindRestart      = PlayerPrefs.GetString(PREF_BIND_RESTART, DEFAULT_BIND_RESTART);
            _bindMenuA        = PlayerPrefs.GetString(PREF_BIND_MENU_A, DEFAULT_BIND_MENU_A);
            _bindMenuB        = PlayerPrefs.GetString(PREF_BIND_MENU_B, DEFAULT_BIND_MENU_B);
            _bindRestartA     = PlayerPrefs.GetString(PREF_BIND_RESTART_A, DEFAULT_BIND_RESTART_A);
            _bindRestartB     = PlayerPrefs.GetString(PREF_BIND_RESTART_B, DEFAULT_BIND_RESTART_B);
            if (_wheelDevice != null) ReCacheBindings();
        }

        void ReCacheBindings()
        {
            _steerCtrl    = CacheBindingCtrl(_bindSteerAxis);
            _crossCtrls   = CacheBindingCtrls(_bindReverse);
            _triangleCtrl = CacheBindingCtrl(_bindDrive);
            _l1Ctrl       = CacheBindingCtrl(_bindPaddleLeft);
            _r1Ctrl       = CacheBindingCtrl(_bindPaddleRight);
            _restartCtrl  = CacheBindingCtrl(_bindRestart);
            _l2Ctrl       = CacheBindingCtrl(_bindMenuA);
            _r2Ctrl       = CacheBindingCtrl(_bindMenuB);
            _l3Ctrl       = CacheBindingCtrl(_bindRestartA);
            _r3Ctrl       = CacheBindingCtrl(_bindRestartB);
        }

        InputControl<float> CacheBindingCtrl(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            return _wheelDevice.TryGetChildControl(path) as InputControl<float>;
        }

        // Parsea un binding compuesto separado por '|' (ej: "button18|stick/down")
        // y cachea cada path como InputControl<float>. Paths vacíos o no resueltos
        // se ignoran en silencio — útil para defaults que cubren múltiples shifters.
        InputControl<float>[] CacheBindingCtrls(string composite)
        {
            if (string.IsNullOrEmpty(composite)) return System.Array.Empty<InputControl<float>>();
            string[] parts = composite.Split('|');
            var list = new System.Collections.Generic.List<InputControl<float>>(parts.Length);
            foreach (var p in parts)
            {
                var c = CacheBindingCtrl(p.Trim());
                if (c != null) list.Add(c);
            }
            return list.ToArray();
        }

        public InputDevice WheelDevice => _wheelDevice;

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

            // Listener para invalidar cache cuando el wheel se desconecta.
            // Sin esto, los InputControl<float> cacheados quedan apuntando a un
            // device con added=false y ReadValue() lanza InvalidOperationException
            // por frame (verificado en logs S3 — 4995 exceptions/83s, ver FIX#19).
            _deviceChangeHandler = OnDeviceChange;
            InputSystem.onDeviceChange += _deviceChangeHandler;

            TryDetectWheel(); // primer intento; Update() hace polling si aún no está
        }

        // Llamado por Unity Input System cuando algún device cambia de estado.
        // Reaccionamos solo a Removed/Disconnected/Disabled — UsageChanged es
        // demasiado amplio (cambios de "primary"/"secondary"), SoftReset/HardReset
        // no implican device inválido (solo resetean estado interno).
        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (device != _wheelDevice) return;
            if (change != InputDeviceChange.Removed
                && change != InputDeviceChange.Disconnected
                && change != InputDeviceChange.Disabled) return;

            Debug.LogWarning($"[UIInputNew] Wheel device {change}: {device.displayName}. Reseteando cache.");
            _hasWheel = false;
            _wheelDevice = null;
            _steerCtrl = null; _gasCtrl = null; _brakeCtrl = null;
            _crossCtrls = System.Array.Empty<InputControl<float>>();
            _triangleCtrl = null; _restartCtrl = null;
            _l1Ctrl = null; _r1Ctrl = null;
            _l2Ctrl = null; _r2Ctrl = null;
            _l3Ctrl = null; _r3Ctrl = null;
            _gearControls = System.Array.Empty<InputControl<float>>();
            _detectionTimer = 999f; // forzar re-detect en el próximo Update
        }

        // Lectura segura de InputControl<float>: valida que el device esté en el
        // sistema antes de leer; si la lectura tira InvalidOperationException
        // (race entre onDeviceChange y reads del frame en curso) la atrapamos
        // silenciosamente en lugar de spamear el log 60 veces/segundo.
        private static bool SafeReadFloat(InputControl<float> ctrl, out float value)
        {
            value = 0f;
            if (ctrl == null) return false;
            var dev = ctrl.device;
            if (dev == null || !dev.added) return false;
            try { value = ctrl.ReadValue(); return true; }
            catch (System.InvalidOperationException) { return false; }
        }

        // Detecta el volante y cachea todos los controles. Idempotente:
        // llamar múltiples veces es barato si ya se encontró.
        // Estrategia en dos pasos:
        //   1) Match por nombre conocido en displayName O description.product
        //      (G923 PS/Xbox, G920, Driving Force, etc).
        //   2) Fallback: enumerar Joysticks y validar firma de volante por
        //      conteo de axes/buttons. Esto cubre G923s con drivers raros
        //      cuyos nombres no calzan ningún patrón conocido — sin esto, el
        //      gameplay no veía el volante aunque Pantalla 2 sí lo calibrara.
        private bool TryDetectWheel()
        {
            if (_hasWheel) return true;

            // 1) Match por nombre conocido (preferido — más específico)
            foreach (var device in InputSystem.devices)
            {
                if (MatchesWheelName(device) && AttachToWheelDevice(device, "named match"))
                    return true;
            }

            // 2) Fallback: cualquier Joystick que pase la validación heurística.
            //    NO usamos Joystick.current — ese retorna "último activo", no
            //    "mejor candidato"; cualquier otro joystick conectado podría
            //    desplazar al volante real.
            foreach (var device in InputSystem.devices)
            {
                if (!(device is Joystick)) continue;
                if (AttachToWheelDevice(device, "Joystick fallback"))
                    return true;
            }

            return false;
        }

        // Hacer match por nombre tanto en displayName como en description.product.
        // Pantalla 2 ya hace esto — product suele ser más estable entre drivers.
        public static bool MatchesWheelName(InputDevice d)
        {
            string name = d.displayName ?? string.Empty;
            string product = d.description.product ?? string.Empty;
            string[] patterns = { "G923", "G920", "Driving Force", "Racing Wheel",
                                  "Steering Wheel", "Driving Wheel", "Logitech" };
            foreach (var p in patterns)
            {
                if (name.IndexOf(p, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (product.IndexOf(p, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        // Mapeo G923 PS conocido. Si la calibración guardada no calza con
        // estos paths, sobrescribir — es más confiable que la auto-detección
        // de Pantalla 2 que ha capturado paths equivocados (visto en logs S3:
        // brakeAxis="stick/y" cuando debería ser "rz"). Solo aplica para
        // Logitech/G923 (MatchesWheelName); otros wheels conservan calibración.
        public static void EnsureG923PSDefaults(InputDevice device)
        {
            // FORZAR pre-Wednesday mapping en CADA boot. La calibración de
            // Pantalla 2 capturó paths/rangos equivocados varias veces; el
            // mapping pre-Wed (commit 0ad7aaf) era simple y funcionaba:
            //   stick/x = volante, z = gas, rz = freno, button2 = reversa
            //   range pedal: rest=1, press=-1 (formula (1-raw)/2 daba 0..1)
            // Para overrides finos: F9 (curvas) o editar PlayerPrefs manual.
            Debug.Log($"[UIInputNew] Aplicando mapping pre-Wednesday G923 PS"
                + $" (sobrescribe calibración previa para predictibilidad).");

            PlayerPrefs.SetString("G923_GasAxis", "z");
            PlayerPrefs.SetString("G923_BrakeAxis", "rz");
            PlayerPrefs.SetString(PREF_BIND_STEER_AXIS, "stick/x");
            PlayerPrefs.SetString(PREF_BIND_REVERSE, "button19");
            // Pre-Wed pedales: range raw 1.0 (rest) → -1.0 (press completo).
            // NormalizePedal con rest=1, press=-1 da idéntico a (1-raw)/2.
            PlayerPrefs.SetFloat("G923_GasRest",    1.0f);
            PlayerPrefs.SetFloat("G923_GasPress",  -1.0f);
            PlayerPrefs.SetFloat("G923_BrakeRest",  1.0f);
            PlayerPrefs.SetFloat("G923_BrakePress",-1.0f);
            // Steering rango simétrico estándar.
            PlayerPrefs.SetFloat("G923_SteerCenter", 0.0f);
            PlayerPrefs.SetFloat("G923_SteerMax",    1.0f);
            PlayerPrefs.SetFloat("G923_SteerMin",   -1.0f);
            // Limpiar el flag que hace que Pantalla 2 fuerce Discovery.
            PlayerPrefs.SetInt("Cal_ReverseDone", 1);
            PlayerPrefs.Save();
        }

        // Intenta adjuntar al device como volante. Solo retorna true si parece
        // volante por firma (axes y buttons suficientes). NO valida paths
        // específicos — G923s distintos reportan paths distintos (`stick/x` vs
        // `x`, `z`/`rz` vs otro layout). Pantalla 2 los descubre dinámicamente.
        // Sin validación mínima, un mouse o gamepad podría agarrar el slot;
        // con validación demasiado estricta, un G923 raro queda fuera.
        private bool AttachToWheelDevice(InputDevice device, string reason)
        {
            int axisCount = 0, buttonCount = 0;
            foreach (var c in device.allControls)
            {
                if (c is ButtonControl) buttonCount++;
                else if (c is AxisControl) axisCount++;
            }
            // Mínimo razonable para volante con pedales + algún botón:
            // 3+ axes (steer + gas + brake típicos) y 8+ buttons.
            bool ok = axisCount >= 3 && buttonCount >= 8;
            if (!ok)
            {
                Debug.LogWarning($"[UIInputNew] Candidato rechazado ({reason}): {device.displayName}"
                    + $" | layout={device.layout} | product='{device.description.product}'"
                    + $" | axes={axisCount} buttons={buttonCount}");
                return false;
            }

            _hasWheel = true;
            _wheelDevice = device;

            // Si el device es Logitech/G923, asegurar que la calibración
            // guardada calza con el mapeo G923 PS conocido. Si no, sobrescribir
            // con defaults. Esto cubre dos casos: primer boot sin calibración,
            // y calibración corrupta (Pantalla 2 puede elegir paths equivocados
            // — verificado en logs S3 con G923_BrakeAxis="stick/y" cuando debería
            // ser "rz"). El operador puede recalibrar via Pantalla 2 si quiere.
            if (MatchesWheelName(device)) EnsureG923PSDefaults(device);

            // Ejes — paths calibrados dinámicamente en la pantalla del menú
            // (pedales). El steering path viene de PlayerPrefs via ReloadBindings().
            string gasPath   = PlayerPrefs.GetString("G923_GasAxis", "z");
            string brakePath = PlayerPrefs.GetString("G923_BrakeAxis", "rz");
            _gasCtrl   = CacheControl(gasPath);
            _brakeCtrl = CacheControl(brakePath);

            // H-shifter: buttons 13-19 (no configurable — hardcoded)
            _gearControls = new InputControl<float>[7];
            for (int i = 0; i < 7; i++)
                _gearControls[i] = CacheButton(13 + i);

            // Bindings configurables (reversa, paddles, combos, restart)
            ReloadBindings();

            // Volante apareció después de Initialize → respetar PlayerPrefs de transmisión
            _isAutomaticMode = PlayerPrefs.GetInt("TransmisionManual", 0) == 0;
            if (_isAutomaticMode && _currentGear == 0) _currentGear = 1;

            // Cargar calibración de pedales (rest+press) hecha en el menú.
            _gasRest    = PlayerPrefs.GetFloat("G923_GasRest",  1f);
            _gasPress   = PlayerPrefs.GetFloat("G923_GasPress", -1f);
            _brakeRest  = PlayerPrefs.GetFloat("G923_BrakeRest",  1f);
            _brakePress = PlayerPrefs.GetFloat("G923_BrakePress", -1f);

            // Calibración del steering (si no existe, rango ideal -1..1 sin offset)
            _steerCenter = PlayerPrefs.GetFloat("G923_SteerCenter", 0f);
            _steerMax    = PlayerPrefs.GetFloat("G923_SteerMax",   1f);
            _steerMin    = PlayerPrefs.GetFloat("G923_SteerMin",  -1f);

            // Parámetros tuneable (panel F9)
            ReloadTuning();

            // Reportar qué paths de _crossCtrls realmente resolvieron en este device.
            // Si Bind_reverse="button2|button18" pero solo button2 resolvió, el log
            // dirá "[button2]" — señal que button18 vive en otro device (separate
            // shifter HID) y necesita FIX multi-device.
            string crossPathsResolved = "";
            if (_crossCtrls != null)
            {
                string[] requestedPaths = (_bindReverse ?? "").Split('|');
                int idx = 0;
                foreach (var rp in requestedPaths)
                {
                    string trimmed = rp.Trim();
                    bool resolved = idx < _crossCtrls.Length && _crossCtrls[idx] != null;
                    crossPathsResolved += $"{trimmed}={resolved} ";
                    if (resolved) idx++;
                }
            }
            Debug.Log($"[UIInputNew] Volante adjuntado ({reason}): {device.displayName}"
                + $" | layout={device.layout} | product='{device.description.product}'"
                + $" | manufacturer='{device.description.manufacturer}' | deviceId={device.deviceId}"
                + $" | axes={axisCount} buttons={buttonCount}"
                + $" | steer={_steerCtrl != null} gas[{gasPath}]={_gasCtrl != null}"
                + $" brake[{brakePath}]={_brakeCtrl != null}"
                + $" reverse_paths_count={(_crossCtrls != null ? _crossCtrls.Length : 0)}"
                + $" reverse_resolved=[{crossPathsResolved.Trim()}]");
            return true;
        }

        // Normaliza pedal a [0,1] usando rest+press calibrados en pantalla.
        // Funciona sin importar la dirección del eje (rest puede ser > o < press).
        private float NormalizePedal(float raw, float rest, float press)
        {
            float span = press - rest;
            if (Mathf.Abs(span) < 0.05f) return 0f;
            return Mathf.Clamp01((raw - rest) / span);
        }

        // Curva del freno: tramo 1 suave (0.._brakeSoftEnd pedal →
        // 0.._brakeSoftMaxOutput freno), tramo 2 fuerte ("freno de poder").
        // Parámetros tuneable via AdvancedInputPanel.
        private float BrakeCurve(float x)
        {
            if (x <= 0f) return 0f;
            if (x >= 1f) return 1f;
            if (x < _brakeSoftEnd)
                return (x / _brakeSoftEnd) * _brakeSoftMaxOutput;
            float hard = (x - _brakeSoftEnd) / (1f - _brakeSoftEnd);
            return _brakeSoftMaxOutput + hard * (1f - _brakeSoftMaxOutput);
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
            return SafeReadFloat(ctrl, out var v) && v > 0.5f;
        }

        private bool IsAnyPressed(InputControl<float>[] arr)
        {
            if (arr == null) return false;
            for (int i = 0; i < arr.Length; i++) if (IsPressed(arr[i])) return true;
            return false;
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
                            // Diagnóstico remoto: incluir product/manufacturer/deviceId
                            // de cada device para que vía LogUploader → S3 podamos
                            // identificar G923s con nombres raros que no calzan ningún patrón.
                            string all = "";
                            foreach (var d in InputSystem.devices)
                            {
                                all += $"\n  - displayName='{d.displayName}' layout={d.layout}"
                                    + $" product='{d.description.product}'"
                                    + $" manufacturer='{d.description.manufacturer}'"
                                    + $" deviceId={d.deviceId}";
                            }
                            Debug.LogWarning("[UIInputNew] Sin volante aún. Devices:" + all);
                        }
                    }
                }
            }

            // ---- Toggle debug overlay (F10) ----
            if (Keyboard.current != null && Keyboard.current.f10Key.wasPressedThisFrame)
                _debugOverlay = !_debugOverlay;

            Vector2 kbInput = _moveAction.ReadValue<Vector2>();

            // ---- Steering: calibrado + deadzone + curva racional ----
            if (_hasWheel)
            {
                float rawSteer = SafeReadFloat(_steerCtrl, out var rs) ? rs : _steerCenter;
                float norm = NormalizeSteer(rawSteer); // [-1, 1] sobre rango físico real

                // Deadzone: si |norm| < deadzone, considerar centro (elimina micro-ruido)
                if (Mathf.Abs(norm) < _steerDeadzone) norm = 0f;

                if (Mathf.Abs(norm) > 0.01f)
                {
                    float absN = Mathf.Abs(norm);
                    // f(x) = x / (a + (1-a)x) — agresiva en pequeños, aplanada arriba
                    float curved = absN / (_steerCurveA + (1f - _steerCurveA) * absN);
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
                    float gasRaw = SafeReadFloat(_gasCtrl, out var rg) ? rg : _gasRest;
                    float brakeRaw = SafeReadFloat(_brakeCtrl, out var rb) ? rb : _brakeRest;
                    float gasLinear = NormalizePedal(gasRaw, _gasRest, _gasPress);
                    // Curva del gas: pow(x, N). N<1 = más respuesta inicial, N>1 = más control fino.
                    float gas = Mathf.Approximately(_gasCurveN, 1f) ? gasLinear : Mathf.Pow(gasLinear, _gasCurveN);
                    float brakeLinear = NormalizePedal(brakeRaw, _brakeRest, _brakePress);
                    // Curva por tramos: suave hasta 80% pedal, "freno de poder" en el último 20%
                    float brake = BrakeCurve(brakeLinear);
                    verticalInput = gas;
                    brakeInput = brake;

                    // Reversa híbrida: edge-trigger para entrar (robusto a pulsos
                    // transitorios del HID) + debounce de "última señal vista" para
                    // salir (vuelve a Drive cuando llevemos >300ms sin reversa).
                    // FIX#23 era posicional puro, pero si el driver reporta button18
                    // como pulsos de 1 frame, gear oscilaba entre -1 y 1 cada frame
                    // y se percibía como "no se reconoce reversa".
                    // Triangle solo override Drive si la reversa NO está activa
                    // ni vista recientemente — evita que un pulso espurio de
                    // Triangle saque al usuario de R.
                    bool crossNow = IsAnyPressed(_crossCtrls);
                    bool triNow   = IsPressed(_triangleCtrl);
                    if (crossNow) _reverseLastSeenTime = Time.realtimeSinceStartup;
                    bool reverseRecentlySeen =
                        Time.realtimeSinceStartup - _reverseLastSeenTime <= REVERSE_HOLD_SECONDS;
                    if (crossNow && !_lastCrossPressed) _currentGear = -1; // edge engage R
                    if (triNow && !_lastTrianglePressed && !reverseRecentlySeen)
                        _currentGear = 1; // edge engage D solo si no estamos en R
                    if (_currentGear == -1 && !crossNow && !triNow && !reverseRecentlySeen)
                        _currentGear = 1; // auto-exit a Drive tras 300ms sin señal
                    _lastCrossPressed = crossNow;
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
                    float gasRaw = SafeReadFloat(_gasCtrl, out var rg) ? rg : _gasRest;
                    float brakeRaw = SafeReadFloat(_brakeCtrl, out var rb) ? rb : _brakeRest;
                    float gasLinear = NormalizePedal(gasRaw, _gasRest, _gasPress);
                    // Curva del gas: pow(x, N). N<1 = más respuesta inicial, N>1 = más control fino.
                    float gas = Mathf.Approximately(_gasCurveN, 1f) ? gasLinear : Mathf.Pow(gasLinear, _gasCurveN);
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

            // ---- Botón único de reinicio (configurable en BindingsPanel F8) ----
            if (_hasWheel && _restartCtrl != null)
            {
                bool restartNow = IsPressed(_restartCtrl);
                if (restartNow && !_lastRestartPressed)
                {
                    Time.timeScale = 1f;
                    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                }
                _lastRestartPressed = restartNow;
            }

            // ---- Log periódico de valores crudos/calculados (diagnóstico kiosko) ----
            if (_hasWheel)
            {
                _debugLogTimer += Time.deltaTime;
                if (_debugLogTimer >= 2f)
                {
                    _debugLogTimer = 0f;
                    float st = SafeReadFloat(_steerCtrl, out var rds) ? rds : 0f;
                    float gr = SafeReadFloat(_gasCtrl, out var rdg) ? rdg : 1f;
                    float br = SafeReadFloat(_brakeCtrl, out var rdb) ? rdb : 1f;
                    bool crossDbg = IsAnyPressed(_crossCtrls);
                    int crossLen = _crossCtrls != null ? _crossCtrls.Length : 0;
                    float reverseAge = Time.realtimeSinceStartup - _reverseLastSeenTime;
                    Debug.Log($"[UIInputNew] raw steer={st:F3} gas={gr:F3} brake={br:F3} | V={verticalInput:F3} B={brakeInput:F3} | gasR/P={_gasRest:F2}/{_gasPress:F2} brakeR/P={_brakeRest:F2}/{_brakePress:F2} | auto={_isAutomaticMode} gear={_currentGear} | crossCtrls={crossLen} crossNow={crossDbg} revAge={reverseAge:F2}s");
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
                float st = SafeReadFloat(_steerCtrl, out var ovs) ? ovs : 0f;
                float gr = SafeReadFloat(_gasCtrl, out var ovg) ? ovg : 1f;
                float br = SafeReadFloat(_brakeCtrl, out var ovb) ? ovb : 1f;
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
            if (_deviceChangeHandler != null)
            {
                InputSystem.onDeviceChange -= _deviceChangeHandler;
                _deviceChangeHandler = null;
            }
#endif
        }
    }
}
#endif
