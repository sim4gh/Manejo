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
        // Shifter device separado (HORI Truck reporta wheel + shifter como dos
        // HIDs USB independientes). null si no aplica. Bindings con prefijo
        // "shifter:" resuelven aquí; sin prefijo, fallback desde el wheel.
        private InputDevice _shifterDevice;
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

        // Clutch — solo presente en G923 PS variant (3er pedal físico, eje stick/y
        // con rest=-1, press=+1). En Xbox stick/y ya está usado por gas → no hay
        // clutch físico; _clutchCtrl queda null y clutchInput se mantiene en 0.
        private float _clutchRest = -1f;
        private float _clutchPress = 1f;
        private float clutchInput;
        // Edge-trigger del cambio de marcha sin clutch presionado. Comparamos
        // contra el último gear NO-neutral, no contra el del frame anterior:
        // si el shifter pasa por neutral entre frames (1→0→2), no perdemos la
        // detección. ViolationDetector consume el contador via
        // ConsumeGearShiftsWithoutClutch() (latched, no bool).
        private int _pendingGearShiftWithoutClutchCount;
        private int _lastNonNeutralGear = 0;

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
        // Curva del gas: pow(pedal, N). N=1.0 lineal se siente muy sensible al inicio
        // en wheels analógicos (un toque suave dispara el coche). N=1.7 da control fino
        // en 0-50% del pedal y mantiene 100% torque al fondo. Equivalente a curvas
        // típicas de simuladores reales ("eco/cruise" en zona baja, full al fondo).
        // Tuneable en runtime desde F9 (AdvancedInputPanel).
        public const float DEFAULT_GAS_CURVE_N = 1.7f;
        // Keys PlayerPrefs
        public const string PREF_STEER_CURVE_A = "Adv_SteerCurveA";
        public const string PREF_STEER_DEADZONE = "Adv_SteerDeadzone";
        public const string PREF_BRAKE_SOFT_END = "Adv_BrakeSoftEnd";
        public const string PREF_BRAKE_SOFT_MAX_OUTPUT = "Adv_BrakeSoftMaxOutput";
        public const string PREF_GAS_CURVE_N = "Adv_GasCurveN";

        // Versión del default de gas curve. Bumpear cuando cambiemos
        // DEFAULT_GAS_CURVE_N para que kioskos con el viejo default reciban el
        // nuevo. Kioskos que tunearon explícitamente desde F9 también se migran
        // una vez — la próxima vez que tuneen F9 queda lo que ellos elijan.
        private const int GAS_CURVE_DEFAULT_VERSION = 2;
        private const string PREF_GAS_CURVE_DEFAULT_VERSION = "Adv_GasCurveDefault_v";

        /// <summary>
        /// Migra el default de gas curve si la versión guardada es vieja. Llamar
        /// antes de ReloadTuning. Idempotente — solo escribe la primera vez por versión.
        /// </summary>
        private void MigrateGasCurveDefault()
        {
            int storedVersion = PlayerPrefs.GetInt(PREF_GAS_CURVE_DEFAULT_VERSION, 1);
            if (storedVersion < GAS_CURVE_DEFAULT_VERSION)
            {
                PlayerPrefs.SetFloat(PREF_GAS_CURVE_N, DEFAULT_GAS_CURVE_N);
                PlayerPrefs.SetInt(PREF_GAS_CURVE_DEFAULT_VERSION, GAS_CURVE_DEFAULT_VERSION);
                PlayerPrefs.Save();
                Debug.Log($"[UIInputNew] Migrated gas curve default to {DEFAULT_GAS_CURVE_N} (v{storedVersion}→{GAS_CURVE_DEFAULT_VERSION})");
            }
        }

        /// <summary>
        /// Relee los parámetros de tuning desde PlayerPrefs. Llamar al iniciar
        /// y cuando el AdvancedInputPanel modifique valores (efecto en vivo).
        /// </summary>
        public void ReloadTuning()
        {
            MigrateGasCurveDefault();
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

        #region HORI HPC-044U throttle bypass — DO NOT MODIFY (ver HORI_THROTTLE_BUG_RESOLUTION.md, PR #127)
        // ⚠️ CRITICAL — el throttle del HORI Truck Control System (HPC-044U)
        //   funciona SOLO gracias a este bypass. NO eliminar, NO simplificar,
        //   NO reemplazar con InputControl normal. Ver HoriThrottleReader.cs
        //   para el bug completo. Tomó 6h debugging + 4 builds para llegar aquí.
        // Cuando _useHoriRawGas=true, ReadGasRawValue() bypassa _gasCtrl y lee
        // el throttle directo del HID raw via HoriThrottleProvider delegate.
        private bool _useHoriRawGas;
        #endregion

        private InputControl<float> _brakeCtrl; // rz
        private InputControl<float> _clutchCtrl; // stick/y en PS, null en Xbox

        // Controles cacheados (evita TryGetChildControl cada frame)
        private InputControl<float>[] _gearControls; // [7] buttons 13-19
        private InputControl<float> _l2Ctrl, _r2Ctrl, _l3Ctrl, _r3Ctrl;
        private InputControl<float> _l1Ctrl, _r1Ctrl; // paddles para direccionales
        private InputControl<float> _hazardCtrl;       // botón dedicado de intermitentes
        private InputControl<float> _hornCtrl;         // claxón (hold-to-honk)
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
        private string _bindHazard = "";
        private string _bindHorn = "";
        private string _bindRestart = "";           // vacío = deshabilitado
        private string _bindMenuA = "button7";      // L2
        private string _bindMenuB = "button8";      // R2
        private string _bindRestartA = "button11";  // L3
        private string _bindRestartB = "button12";  // R3
        // Gears 1-6: vacío → legacy fallback (buttons 13-19).
        private string _bindGear1 = "";
        private string _bindGear2 = "";
        private string _bindGear3 = "";
        private string _bindGear4 = "";
        private string _bindGear5 = "";
        private string _bindGear6 = "";

        public const string PREF_BIND_STEER_AXIS = "Bind_steerAxis";
        public const string PREF_BIND_REVERSE = "Bind_reverse";

        #region HORI HPC-044U throttle bypass — DO NOT MODIFY (ver HORI_THROTTLE_BUG_RESOLUTION.md, PR #127)
        // ⚠️ CRITICAL — Sentinel + delegate del bypass del throttle HORI.
        //   NO eliminar, NO renombrar, NO cambiar semántica. Si tocas algo aquí
        //   ROMPES el acelerador en TODOS los kioskos productivos con HORI Truck
        //   (Pasajeros 1, Pasajeros 2, Carga, Casa Aramis y todos los futuros).
        //
        // Sentinel value para G923_GasAxis: el throttle del HORI HPC-044U queda
        // huérfano en el HID parser de Unity (los 2 sliders están aliased al
        // mismo byte y el byte del throttle 21-22 no tiene AxisControl que lo
        // lea). HoriThrottleReader.cs (Assets/Custom/) abre el HID device path
        // crudo con CreateFile + ReadFile thread y lee el byte directo. Cuando
        // G923_GasAxis == este path, ReadGasRawValue() bypassa _gasCtrl y
        // devuelve HoriThrottleProvider() (que apunta al reader).
        public const string HORI_RAW_GAS_PATH = "__HORI_RAW_HID_THROTTLE__";

        // Inyección desde Assembly-CSharp (HoriThrottleReader.cs en Assets/
        // Custom/) — cross-asmdef no permite que Gley referencie Custom, pero
        // sí al revés. HoriThrottleReader se registra aquí en su Bootstrap.
        // Devuelve el throttle 0..1 leído raw del HID byte 21-22.
        public static System.Func<float> HoriThrottleProvider;
        #endregion
        public const string PREF_BIND_DRIVE = "Bind_drive";
        public const string PREF_BIND_PADDLE_LEFT = "Bind_paddleLeft";
        public const string PREF_BIND_PADDLE_RIGHT = "Bind_paddleRight";
        public const string PREF_BIND_HAZARD = "Bind_hazard";
        public const string PREF_BIND_HORN = "Bind_horn";
        public const string PREF_BIND_RESTART = "Bind_restart";
        public const string PREF_BIND_MENU_A = "Bind_menuA";
        public const string PREF_BIND_MENU_B = "Bind_menuB";
        public const string PREF_BIND_RESTART_A = "Bind_restartA";
        public const string PREF_BIND_RESTART_B = "Bind_restartB";
        // Gears 1-6 — opcionales. Si los 6 están vacíos, _gearControls cae al
        // mapeo legacy hardcoded buttons 13-19 (preserva G923 sin recalibrar).
        // Al persistirse desde Discovery o F8 deberían venir con prefijo
        // "wheel:" o "shifter:" para evitar ambigüedad cross-device (HORI Truck
        // tiene H-pattern en device separado del volante).
        public const string PREF_BIND_GEAR1 = "Bind_gear1";
        public const string PREF_BIND_GEAR2 = "Bind_gear2";
        public const string PREF_BIND_GEAR3 = "Bind_gear3";
        public const string PREF_BIND_GEAR4 = "Bind_gear4";
        public const string PREF_BIND_GEAR5 = "Bind_gear5";
        public const string PREF_BIND_GEAR6 = "Bind_gear6";

        // Clutch (3er pedal G923 PS). Defaults se escriben/borran en
        // EnsureG923PSDefaults según variante. ClearWheelCalibration también
        // los limpia para evitar basura persistente entre devices distintos.
        public const string PREF_G923_CLUTCH_AXIS  = "G923_ClutchAxis";
        public const string PREF_G923_CLUTCH_REST  = "G923_ClutchRest";
        public const string PREF_G923_CLUTCH_PRESS = "G923_ClutchPress";

        // ====== Moto Simulator (ESP32-S3 USB HID) ======
        // Controlador físico de la moto: 4 ejes (X=lean chasis, Y=pitch chasis,
        // Z=handlebar, Rz=throttle) + 2 botones (A=brake, B=clutch). USB HID
        // nativo desde firmware moto-controller. Detectado por displayName/product
        // "Moto Simulator" (ver IsMotoSimulator). Bypassa la heurística mínima
        // axisCount>=3 buttonCount>=8 de AttachToWheelDevice porque solo expone
        // 2 botones. Usa ruta de lectura propia en UpdateMotoSimulator() con
        // steering blend velocidad-dependiente (Opción C, codex 2026-05-01):
        // a baja velocidad domina handlebar, a alta velocidad domina lean.
        public const string PREF_MOTO_LEAN_PATH   = "MOTO_LeanPath";   // X axis (BNO chasis)
        public const string PREF_MOTO_HBAR_PATH   = "MOTO_HbarPath";   // Z axis (BNO volante)
        public const string PREF_MOTO_GAS_PATH    = "MOTO_GasPath";    // Rz axis (Hall throttle)
        public const string PREF_MOTO_BRAKE_PATH  = "MOTO_BrakePath";  // botón A (switch)
        public const string PREF_MOTO_CLUTCH_PATH = "MOTO_ClutchPath"; // botón B (switch)
        public const string PREF_MOTO_LEAN_MIN    = "MOTO_LeanMin";
        public const string PREF_MOTO_LEAN_MAX    = "MOTO_LeanMax";
        public const string PREF_MOTO_HBAR_MIN    = "MOTO_HbarMin";
        public const string PREF_MOTO_HBAR_MAX    = "MOTO_HbarMax";
        public const string PREF_MOTO_GAS_REST    = "MOTO_GasRest";
        public const string PREF_MOTO_GAS_PRESS   = "MOTO_GasPress";
        public const string PREF_MOTO_HIGH_SPEED_LEAN_WEIGHT = "MOTO_HighSpeedLeanWeight";
        public const string PREF_MOTO_BLEND_START_KMH        = "MOTO_BlendStartKmh";
        public const string PREF_MOTO_BLEND_END_KMH          = "MOTO_BlendEndKmh";
        public const string PREF_MOTO_CALIBRATION_DONE       = "MOTO_CalibrationDone";
        public const string PREF_MOTO_DEVICE_FINGERPRINT     = "MOTO_DeviceFingerprint";

        // Defaults asumidos. Verificar via F7 (LogConsolePanel) en MX y ajustar
        // PlayerPrefs si los paths reales difieren. HID buttons en Unity Input
        // System suelen ser 1-indexed (button1 = primer botón del descriptor),
        // por eso brake=button1 (firmware bit 0 = HID button #1) y clutch=button2.
        public const string DEFAULT_MOTO_LEAN_PATH   = "x";
        public const string DEFAULT_MOTO_HBAR_PATH   = "z";
        public const string DEFAULT_MOTO_GAS_PATH    = "rz";
        public const string DEFAULT_MOTO_BRAKE_PATH  = "button1";
        public const string DEFAULT_MOTO_CLUTCH_PATH = "button2";
        // Steering blend (Opción C codex). Tunable via PlayerPrefs sin recompilar.
        public const float  DEFAULT_MOTO_HIGH_SPEED_LEAN_WEIGHT = 0.8f;
        public const float  DEFAULT_MOTO_BLEND_START_KMH = 15f;
        public const float  DEFAULT_MOTO_BLEND_END_KMH   = 30f;

        private bool _isMotoSimulator = false;
        private InputControl<float> _leanCtrl;   // X axis BNO chasis
        private InputControl<float> _hbarCtrl;   // Z axis BNO volante
        // _gasCtrl/_brakeCtrl/_clutchCtrl reusados con paths/normalización de moto.
        private Rigidbody _bikeRigidbody;        // null si UIInputNew no está en el Player de la moto
        private float _motoLeanMin = -1f, _motoLeanMax = +1f;
        private float _motoHbarMin = -1f, _motoHbarMax = +1f;

        public const string DEFAULT_BIND_STEER_AXIS = "stick/x";
        // En el G923 PS del kiosk de la demo, la posición R del H-shifter
        // dispara button19 (verificado en F7 múltiples veces). NO es phantom
        // — es la señal real de R. Solo aparece "siempre on" en F7 si el
        // operador deja el shifter en R durante el test.
        public const string DEFAULT_BIND_REVERSE = "button19";
        public const string DEFAULT_BIND_DRIVE = "button4";
        public const string DEFAULT_BIND_PADDLE_LEFT = "button6";
        public const string DEFAULT_BIND_PADDLE_RIGHT = "button5";
        public const string DEFAULT_BIND_HAZARD = "";
        public const string DEFAULT_BIND_HORN = "";
        public const string DEFAULT_BIND_RESTART = "";
        public const string DEFAULT_BIND_MENU_A = "button7";
        public const string DEFAULT_BIND_MENU_B = "button8";
        public const string DEFAULT_BIND_RESTART_A = "button11";
        public const string DEFAULT_BIND_RESTART_B = "button12";
        // Gears default vacío: si no hay calibración explícita, _gearControls
        // usa el legacy buttons 13-19 (G923 H-shifter).
        public const string DEFAULT_BIND_GEAR1 = "";
        public const string DEFAULT_BIND_GEAR2 = "";
        public const string DEFAULT_BIND_GEAR3 = "";
        public const string DEFAULT_BIND_GEAR4 = "";
        public const string DEFAULT_BIND_GEAR5 = "";
        public const string DEFAULT_BIND_GEAR6 = "";

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
            _bindHazard       = PlayerPrefs.GetString(PREF_BIND_HAZARD, DEFAULT_BIND_HAZARD);
            _bindHorn         = PlayerPrefs.GetString(PREF_BIND_HORN, DEFAULT_BIND_HORN);
            _bindRestart      = PlayerPrefs.GetString(PREF_BIND_RESTART, DEFAULT_BIND_RESTART);
            _bindMenuA        = PlayerPrefs.GetString(PREF_BIND_MENU_A, DEFAULT_BIND_MENU_A);
            _bindMenuB        = PlayerPrefs.GetString(PREF_BIND_MENU_B, DEFAULT_BIND_MENU_B);
            _bindRestartA     = PlayerPrefs.GetString(PREF_BIND_RESTART_A, DEFAULT_BIND_RESTART_A);
            _bindRestartB     = PlayerPrefs.GetString(PREF_BIND_RESTART_B, DEFAULT_BIND_RESTART_B);
            _bindGear1        = PlayerPrefs.GetString(PREF_BIND_GEAR1, DEFAULT_BIND_GEAR1);
            _bindGear2        = PlayerPrefs.GetString(PREF_BIND_GEAR2, DEFAULT_BIND_GEAR2);
            _bindGear3        = PlayerPrefs.GetString(PREF_BIND_GEAR3, DEFAULT_BIND_GEAR3);
            _bindGear4        = PlayerPrefs.GetString(PREF_BIND_GEAR4, DEFAULT_BIND_GEAR4);
            _bindGear5        = PlayerPrefs.GetString(PREF_BIND_GEAR5, DEFAULT_BIND_GEAR5);
            _bindGear6        = PlayerPrefs.GetString(PREF_BIND_GEAR6, DEFAULT_BIND_GEAR6);
            if (_wheelDevice != null) ReCacheBindings();
        }

        void ReCacheBindings()
        {
            _steerCtrl    = CacheBindingCtrl(_bindSteerAxis);
            _crossCtrls   = CacheBindingCtrls(_bindReverse);
            _triangleCtrl = CacheBindingCtrl(_bindDrive);
            _l1Ctrl       = CacheBindingCtrl(_bindPaddleLeft);
            _r1Ctrl       = CacheBindingCtrl(_bindPaddleRight);
            _hazardCtrl   = CacheBindingCtrl(_bindHazard);
            _hornCtrl     = CacheBindingCtrl(_bindHorn);
            _restartCtrl  = CacheBindingCtrl(_bindRestart);
            _l2Ctrl       = CacheBindingCtrl(_bindMenuA);
            _r2Ctrl       = CacheBindingCtrl(_bindMenuB);
            _l3Ctrl       = CacheBindingCtrl(_bindRestartA);
            _r3Ctrl       = CacheBindingCtrl(_bindRestartB);
            ReCacheGearControls();
        }

        // Construye _gearControls desde los Bind_gear1..6 si hay al menos uno
        // configurado; si todos están vacíos, fallback a los buttons 13-19
        // hardcoded (preserva el comportamiento G923 sin recalibrar).
        // _gearValues queda en sync: índice i → marcha en gearValues[i].
        void ReCacheGearControls()
        {
            string[] gearBinds = { _bindGear1, _bindGear2, _bindGear3, _bindGear4, _bindGear5, _bindGear6 };
            bool anyConfigured = false;
            foreach (var b in gearBinds) if (!string.IsNullOrEmpty(b)) { anyConfigured = true; break; }

            if (!anyConfigured)
            {
                // Legacy: buttons 13-19 → gears 1-6, R (mapeo G923 H-shifter).
                _gearControls = new InputControl<float>[7];
                for (int i = 0; i < 7; i++) _gearControls[i] = CacheButton(13 + i);
                _gearValues = new int[] { 1, 2, 3, 4, 5, 6, -1 };
                return;
            }

            // Modo configurado: cachear cada gear que tenga binding (los vacíos
            // se omiten). La reversa se sigue leyendo del _crossCtrls existente,
            // así que solo cacheamos gears D aquí; R se entiende por separado.
            var ctrls = new System.Collections.Generic.List<InputControl<float>>();
            var vals = new System.Collections.Generic.List<int>();
            for (int i = 0; i < gearBinds.Length; i++)
            {
                if (string.IsNullOrEmpty(gearBinds[i])) continue;
                var c = CacheBindingCtrl(gearBinds[i]);
                if (c == null) continue;
                ctrls.Add(c);
                vals.Add(i + 1);
            }
            _gearControls = ctrls.ToArray();
            _gearValues = vals.ToArray();
        }

        // Tabla de gears en sincronía con _gearControls.
        private int[] _gearValues = { 1, 2, 3, 4, 5, 6, -1 };

        InputControl<float> CacheBindingCtrl(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            return ResolveControlPath(path);
        }

        // Resuelve un path en el device correcto:
        //   "wheel:button5"   → solo en _wheelDevice (path stripped)
        //   "shifter:button5" → solo en _shifterDevice (path stripped)
        //   "button5"         → wheel primero, fallback shifter (legacy prefs)
        // Devuelve null si el control no existe o el device target no está.
        private InputControl<float> ResolveControlPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            const string WHEEL_PREFIX = "wheel:";
            const string SHIFTER_PREFIX = "shifter:";

            if (path.StartsWith(WHEEL_PREFIX, System.StringComparison.OrdinalIgnoreCase))
            {
                string sub = path.Substring(WHEEL_PREFIX.Length);
                if (_wheelDevice == null) return null;
                return _wheelDevice.TryGetChildControl(sub) as InputControl<float>;
            }
            if (path.StartsWith(SHIFTER_PREFIX, System.StringComparison.OrdinalIgnoreCase))
            {
                string sub = path.Substring(SHIFTER_PREFIX.Length);
                if (_shifterDevice == null) return null;
                return _shifterDevice.TryGetChildControl(sub) as InputControl<float>;
            }
            // Sin prefijo: legacy. Busca en wheel; si no resuelve y hay shifter,
            // intenta ahí. Útil para PlayerPrefs viejas que no tienen prefijo.
            InputControl<float> ctrl = null;
            if (_wheelDevice != null)
                ctrl = _wheelDevice.TryGetChildControl(path) as InputControl<float>;
            if (ctrl == null && _shifterDevice != null)
                ctrl = _shifterDevice.TryGetChildControl(path) as InputControl<float>;
            return ctrl;
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
            // Manejar añadido de shifter después de Initialize (ej. usuario
            // conecta el USB del shifter HORI más tarde) — re-cachear bindings
            // que tengan paths "shifter:" pendientes de resolver.
            if (change == InputDeviceChange.Added && _hasWheel
                && _shifterDevice == null && IsShifterDevice(device))
            {
                TryDetectShifter();
                return;
            }

            bool isWheel = device == _wheelDevice;
            bool isShifter = device == _shifterDevice;
            if (!isWheel && !isShifter) return;
            if (change != InputDeviceChange.Removed
                && change != InputDeviceChange.Disconnected
                && change != InputDeviceChange.Disabled) return;

            if (isShifter)
            {
                Debug.LogWarning($"[UIInputNew] Shifter device {change}: {device.displayName}. Limpiando shifter cache.");
                _shifterDevice = null;
                // Re-cachear: paths "shifter:" caen a null, paths sin prefijo
                // que vivían en el shifter también dejan de resolver.
                if (_hasWheel) ReCacheBindings();
                return;
            }

            Debug.LogWarning($"[UIInputNew] Wheel device {change}: {device.displayName}. Reseteando cache.");
            _hasWheel = false;
            _wheelDevice = null;
            _shifterDevice = null;
            _steerCtrl = null; _gasCtrl = null; _brakeCtrl = null;
            _clutchCtrl = null;
            // ⚠️ HORI bypass detach reset — DO NOT REMOVE (ver HORI_THROTTLE_BUG_RESOLUTION.md)
            // Si no se resetea aquí, al desconectar el HORI el flag persiste y
            // el siguiente wheel adoptado intenta usar HoriThrottleProvider que
            // está apuntando a un device muerto.
            _useHoriRawGas = false;
            _crossCtrls = System.Array.Empty<InputControl<float>>();
            _triangleCtrl = null; _restartCtrl = null;
            _l1Ctrl = null; _r1Ctrl = null;
            _l2Ctrl = null; _r2Ctrl = null;
            _l3Ctrl = null; _r3Ctrl = null;
            _gearControls = System.Array.Empty<InputControl<float>>();
            // Moto Simulator state cleanup. Inputs caen a 0 en el próximo Update
            // (sin _hasWheel, kbInput governs y por default es 0).
            _isMotoSimulator = false;
            _leanCtrl = null;
            _hbarCtrl = null;
            _bikeRigidbody = null;
            // Forzar a 0 explícitamente para evitar que un valor stuck en
            // verticalInput/brakeInput/clutchInput se quede entre el momento
            // del disconnect y el próximo Update (1 frame de gap).
            horizontalInput = 0f;
            verticalInput = 0f;
            brakeInput = 0f;
            clutchInput = 0f;
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

        // Lectura sin procesadores de Unity (bypassa axisDeadzone/stickDeadzone).
        // Usar para ejes de volante/pedales donde el proyecto tiene su propio
        // pipeline de calibración, deadzone y curvas.
        private static bool SafeReadFloatRaw(InputControl<float> ctrl, out float value)
        {
            value = 0f;
            if (ctrl == null) return false;
            var dev = ctrl.device;
            if (dev == null || !dev.added) return false;
            try { value = ctrl.ReadUnprocessedValue(); return true; }
            catch (System.InvalidOperationException) { return false; }
        }

        #region HORI HPC-044U throttle bypass — DO NOT MODIFY (ver HORI_THROTTLE_BUG_RESOLUTION.md, PR #127)
        // ⚠️ CRITICAL — TODA lectura del gas en el frame loop del wheel pasa
        //   por aquí. Si conviertes esto a `SafeReadFloatRaw(_gasCtrl, ...)`
        //   directo (asumiendo que _gasCtrl siempre está bien cacheado),
        //   ROMPES el throttle del HORI Truck. El bug de Unity HID parser deja
        //   el byte del throttle huérfano para el HORI HPC-044U — Unity NO
        //   tiene un AxisControl que lo lea. _gasCtrl es null para HORI por
        //   diseño; la lectura va via HoriThrottleProvider delegate.
        //
        // Lectura del gas raw que respeta el bypass HORI: si _useHoriRawGas
        // está set, lee de HoriThrottleReader (raw HID byte 21-22 → 0..1).
        // Caso contrario, lee de _gasCtrl como antes (Logitech/moto/etc.).
        private float ReadGasRawValue()
        {
            if (_useHoriRawGas)
            {
                // HoriThrottleProvider puede ser null en boot temprano si el
                // singleton no se ha bootstrapped todavía — return rest.
                var p = HoriThrottleProvider;
                return p != null ? p() : _gasRest;
            }
            return SafeReadFloatRaw(_gasCtrl, out var v) ? v : _gasRest;
        }
        #endregion

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

            // Scene-aware preference: en escena Motocicleta priorizamos el
            // Moto Simulator sobre cualquier wheel. Evita que un G923 conectado
            // accidentalmente al PC de la moto tome el slot. En escenas auto/
            // camión, prioritizamos wheels y excluimos Moto Simulator del match
            // de wheels (cae al fallback Joystick si hace falta).
            string sceneName = SceneManager.GetActiveScene().name;
            bool isMotoScene = !string.IsNullOrEmpty(sceneName)
                && sceneName.IndexOf("Motocicleta", System.StringComparison.OrdinalIgnoreCase) >= 0;

            if (isMotoScene)
            {
                foreach (var device in InputSystem.devices)
                {
                    if (IsMotoSimulator(device) && AttachToWheelDevice(device, "moto-scene priority"))
                        return true;
                }
            }

            // 1) Match por nombre conocido (preferido — más específico).
            //    Excluir devices SHIFTER por nombre antes de la validación
            //    heurística: el HORI SHIFTER tiene 0 axes así que falla por
            //    AttachToWheelDevice de todos modos, pero ser explícito evita
            //    falsos positivos si Unity reporta axes ficticios.
            foreach (var device in InputSystem.devices)
            {
                if (IsShifterDevice(device)) continue;
                // En escenas no-moto, ignoramos el Moto Simulator (queremos un wheel real).
                if (!isMotoScene && IsMotoSimulator(device)) continue;
                if (IsKnownWheelCandidate(device) && AttachToWheelDevice(device, "named match"))
                {
                    TryDetectShifter();
                    return true;
                }
            }

            // 2) Fallback: cualquier Joystick que pase la validación heurística.
            //    NO usamos Joystick.current — ese retorna "último activo", no
            //    "mejor candidato"; cualquier otro joystick conectado podría
            //    desplazar al volante real.
            foreach (var device in InputSystem.devices)
            {
                if (!(device is Joystick)) continue;
                if (IsShifterDevice(device)) continue;
                if (!isMotoScene && IsMotoSimulator(device)) continue;
                if (AttachToWheelDevice(device, "Joystick fallback"))
                {
                    TryDetectShifter();
                    return true;
                }
            }

            return false;
        }

        // Adopta el SHIFTER del HORI Truck (device USB separado) si está conectado.
        // No es fatal si falla — el HORI Truck wheel solo también es usable en
        // modo automático. ReCacheBindings se llama luego para resolver paths
        // con prefijo "shifter:" contra este device.
        private bool TryDetectShifter()
        {
            if (_shifterDevice != null && _shifterDevice.added) return true;
            foreach (var device in InputSystem.devices)
            {
                if (device == _wheelDevice) continue;
                if (!IsShifterDevice(device)) continue;
                _shifterDevice = device;
                int axisCount = 0, buttonCount = 0;
                foreach (var c in device.allControls)
                {
                    if (c is ButtonControl) buttonCount++;
                    else if (c is AxisControl) axisCount++;
                }
                Debug.Log($"[UIInputNew] Shifter adjuntado: {device.displayName}"
                    + $" | layout={device.layout} | product='{device.description.product}'"
                    + $" | deviceId={device.deviceId}"
                    + $" | axes={axisCount} buttons={buttonCount}");
                ReCacheBindings();
                return true;
            }
            return false;
        }

        // Detección con dos responsabilidades distintas:
        //   - IsLogitechG923Family: gatilla el fast-path G923 (EnsureG923PSDefaults
        //     y skip de Pantalla 2). Solo familias Logitech con mapping conocido.
        //   - IsKnownWheelCandidate: superset usado para adopción al boot. Incluye
        //     HORI y descriptores genéricos. NO debe gatillar el fast-path G923 —
        //     un HORI matcheado por el superset cae en Discovery normal.
        // Mantener separadas evita el bug silencioso: agregar "HORI" al matcher
        // único forzaría EnsureG923PSDefaults sobre un device con mapping distinto.
        public static bool IsLogitechG923Family(InputDevice d)
        {
            string name = d.displayName ?? string.Empty;
            string product = d.description.product ?? string.Empty;
            string[] patterns = { "G923", "G920", "Driving Force", "Logitech" };
            foreach (var p in patterns)
            {
                if (name.IndexOf(p, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (product.IndexOf(p, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        public static bool IsKnownWheelCandidate(InputDevice d)
        {
            if (IsLogitechG923Family(d)) return true;
            string name = d.displayName ?? string.Empty;
            string product = d.description.product ?? string.Empty;
            string[] patterns = { "HORI", "Truck Control", "Racing Wheel",
                                  "Steering Wheel", "Driving Wheel" };
            foreach (var p in patterns)
            {
                if (name.IndexOf(p, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (product.IndexOf(p, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        public static bool IsHORITruck(InputDevice d)
        {
            string name = d.displayName ?? string.Empty;
            string product = d.description.product ?? string.Empty;
            return name.IndexOf("HORI", System.StringComparison.OrdinalIgnoreCase) >= 0
                || product.IndexOf("HORI", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // Detecta el Moto Simulator (ESP32-S3 USB HID custom de SimuladoresTlax).
        // No matchea G923/HORI ni gamepads genéricos. Match defensivo por
        // displayName, product, y manufacturer — Unity Input System en Windows
        // a veces popula uno y no los otros según driver.
        public static bool IsMotoSimulator(InputDevice d)
        {
            if (d == null) return false;
            string name = d.displayName ?? string.Empty;
            string product = d.description.product ?? string.Empty;
            string mfr = d.description.manufacturer ?? string.Empty;
            return name.IndexOf("Moto Simulator", System.StringComparison.OrdinalIgnoreCase) >= 0
                || product.IndexOf("Moto Simulator", System.StringComparison.OrdinalIgnoreCase) >= 0
                || mfr.IndexOf("SimuladoresTlax", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // Backwards-compat: callers viejos. Equivalente a IsKnownWheelCandidate.
        // Mantenerlo evita romper código de terceros o scripts editor sin recompilar.
        public static bool MatchesWheelName(InputDevice d) => IsKnownWheelCandidate(d);

        // Quita el prefijo "wheel:" o "shifter:" de un path si lo tiene.
        // Útil cuando un binding persistido con prefijo se concatena al path
        // del device para construir un InputAction binding (ahí no aplica el
        // prefijo, hay que pasar el subpath crudo).
        public static string StripDevicePrefix(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            const string WHEEL_PREFIX = "wheel:";
            const string SHIFTER_PREFIX = "shifter:";
            if (path.StartsWith(WHEEL_PREFIX, System.StringComparison.OrdinalIgnoreCase))
                return path.Substring(WHEEL_PREFIX.Length);
            if (path.StartsWith(SHIFTER_PREFIX, System.StringComparison.OrdinalIgnoreCase))
                return path.Substring(SHIFTER_PREFIX.Length);
            return path;
        }

        // Detecta el SHIFTER del HORI Truck (device USB independiente del wheel,
        // contiene los buttons del H-pattern + sequential). Heurística:
        // displayName/product contiene "SHIFTER" y NO contiene "WHEEL".
        public static bool IsShifterDevice(InputDevice d)
        {
            if (d == null) return false;
            string name = d.displayName ?? string.Empty;
            string product = d.description.product ?? string.Empty;
            bool hasShifter =
                name.IndexOf("SHIFTER", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                product.IndexOf("SHIFTER", System.StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasWheel =
                name.IndexOf("WHEEL", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                product.IndexOf("WHEEL", System.StringComparison.OrdinalIgnoreCase) >= 0;
            return hasShifter && !hasWheel;
        }

        // Mapeo G923 conocido por modo. El switch físico del G923 (PS/Xbox)
        // cambia el HID layout — diferentes axes y buttons exponen las mismas
        // funciones físicas. Detectamos por displayName y aplicamos defaults
        // específicos. Verificado en F7 en ambos kiosks (FIX#26).
        //   PS mode  ("Logitech G923 Racing Wheel for PlayStation 4 and PC"):
        //     gas=z, brake=rz, reverse=button19
        //   Xbox mode ("Logitech G923 Racing Wheel for Xbox One and PC"):
        //     gas=stick/y, brake=z, reverse=button12
        public static void EnsureG923PSDefaults(InputDevice device)
        {
            string name = device.displayName ?? "";
            bool isXboxMode = name.IndexOf("Xbox", System.StringComparison.OrdinalIgnoreCase) >= 0;
            string mode = isXboxMode ? "Xbox" : "PS";

            Debug.Log($"[UIInputNew] Aplicando mapping G923 {mode} mode"
                + $" (displayName='{name}')");

            // Steering: stick/x en ambos modos (verificado).
            PlayerPrefs.SetString(PREF_BIND_STEER_AXIS, "stick/x");
            PlayerPrefs.SetFloat("G923_SteerCenter", 0.0f);
            PlayerPrefs.SetFloat("G923_SteerMax",    1.0f);
            PlayerPrefs.SetFloat("G923_SteerMin",   -1.0f);

            // Botón dedicado de intermitentes (luces de emergencia). En Logitech
            // G923 ambas variantes (PS/Xbox) exponen button24 — alternativa al
            // combo L1+R1 (paddles) que sigue funcionando como fallback.
            PlayerPrefs.SetString(PREF_BIND_HAZARD, "button24");

            if (isXboxMode)
            {
                // Xbox mode: gas=stick/y (idle=-1, pressed va hacia +1),
                // brake=z (idle=1, pressed va hacia -1), reverse=button12.
                PlayerPrefs.SetString("G923_GasAxis", "stick/y");
                PlayerPrefs.SetString("G923_BrakeAxis", "z");
                PlayerPrefs.SetString(PREF_BIND_REVERSE, "button12");
                PlayerPrefs.SetFloat("G923_GasRest",   -1.0f);
                PlayerPrefs.SetFloat("G923_GasPress",   1.0f);
                PlayerPrefs.SetFloat("G923_BrakeRest",  1.0f);
                PlayerPrefs.SetFloat("G923_BrakePress",-1.0f);
                // Sin clutch físico en Xbox (stick/y ya es gas). Limpiar prefs
                // viejas si quedaron de una sesión anterior con PS variant.
                PlayerPrefs.DeleteKey(PREF_G923_CLUTCH_AXIS);
                PlayerPrefs.DeleteKey(PREF_G923_CLUTCH_REST);
                PlayerPrefs.DeleteKey(PREF_G923_CLUTCH_PRESS);
            }
            else
            {
                // PS mode: gas=z, brake=rz, reverse=button19, clutch=stick/y.
                // NormalizePedal con rest=1, press=-1 da idéntico a (1-raw)/2.
                PlayerPrefs.SetString("G923_GasAxis", "z");
                PlayerPrefs.SetString("G923_BrakeAxis", "rz");
                PlayerPrefs.SetString(PREF_BIND_REVERSE, "button19");
                PlayerPrefs.SetFloat("G923_GasRest",    1.0f);
                PlayerPrefs.SetFloat("G923_GasPress",  -1.0f);
                PlayerPrefs.SetFloat("G923_BrakeRest",  1.0f);
                PlayerPrefs.SetFloat("G923_BrakePress",-1.0f);
                // Clutch: 3er pedal físico, stick/y idle=-1, press=+1.
                // Sobreescribimos siempre (consistente con gas/brake/reverse).
                PlayerPrefs.SetString(PREF_G923_CLUTCH_AXIS, "stick/y");
                PlayerPrefs.SetFloat(PREF_G923_CLUTCH_REST,  -1.0f);
                PlayerPrefs.SetFloat(PREF_G923_CLUTCH_PRESS,  1.0f);
            }
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
            // Moto Simulator: bypass de la heurística mínima axisCount/buttonCount.
            // El device solo expone 2 botones (brake + clutch), así que fallaría
            // buttonCount>=8. Detección explícita por displayName/manufacturer.
            if (IsMotoSimulator(device))
            {
                return AttachAsMotoSimulator(device, reason);
            }

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
            if (IsLogitechG923Family(device)) EnsureG923PSDefaults(device);

            #region HORI HPC-044U detection + throttle bypass — DO NOT MODIFY (ver HORI_THROTTLE_BUG_RESOLUTION.md, PR #127)
            // ⚠️ CRITICAL — Este bloque setea G923_GasAxis al sentinel HORI_RAW_GAS_PATH
            //   y habilita _useHoriRawGas. SIN ESTE BLOQUE, los kioskos HORI tienen
            //   throttle MUERTO. Pantalla 2 Discovery NO puede descubrir el throttle
            //   del HORI (byte huérfano en Unity HID parser) — por eso hardcodeamos
            //   aquí. NO eliminar, NO mover bajo otro IsXxx() bloque.
            //   Botones (paddle/hazard/horn/reverse) son safe para ajustar; el
            //   throttle bypass NO.
            if (IsHORITruck(device))
            {
                Debug.Log("[UIInputNew] HORI Truck detectado — defaults paddle/hazard/horn/reverse + throttle vía HoriThrottleReader (raw HID byte intercept)");
                PlayerPrefs.SetString(PREF_BIND_PADDLE_LEFT, "button40");
                PlayerPrefs.SetString(PREF_BIND_PADDLE_RIGHT, "button41");
                PlayerPrefs.SetString(PREF_BIND_HAZARD, "shifter:button27");
                PlayerPrefs.SetString(PREF_BIND_HORN, "wheel:button7");
                PlayerPrefs.SetString(PREF_BIND_REVERSE, "shifter:button7");
                // Throttle: el HID parser de Unity tiene un bug con el descriptor
                // del HORI HPC-044U (sliders aliased al mismo byte) y deja el
                // byte del throttle (21-22 del input report) huérfano — ningún
                // AxisControl lo lee. Solución: HoriThrottleReader.cs (Assets/
                // Custom/) intercepta los state events del wheel via InputSystem.
                // onEvent y lee el byte directo. UIInputNew detecta este path
                // sentinel y bypass el _gasCtrl read normal.
                PlayerPrefs.SetString("G923_GasAxis", HORI_RAW_GAS_PATH);
                PlayerPrefs.SetFloat("G923_GasRest",   0f);
                PlayerPrefs.SetFloat("G923_GasPress",  1f);
                _useHoriRawGas = true;
                // Brake y clutch SÍ se leen via Unity (slider/slider1/rz aliased
                // pero al menos brake y clutch responden a sus respectivos
                // pedales). Discovery los descubre en Pantalla 2.
                PlayerPrefs.Save();
            }
            #endregion

            // Ejes — paths calibrados dinámicamente en la pantalla del menú
            // (pedales). El steering path viene de PlayerPrefs via ReloadBindings().
            string gasPath   = PlayerPrefs.GetString("G923_GasAxis", "z");
            string brakePath = PlayerPrefs.GetString("G923_BrakeAxis", "rz");
            #region HORI HPC-044U sentinel guard + reload — DO NOT MODIFY (ver HORI_THROTTLE_BUG_RESOLUTION.md, PR #127)
            // ⚠️ CRITICAL — Este guard previene que el sentinel HORI envenene
            //   un G923 si el usuario cambia de wheel sin re-discovery. Codex
            //   review 2026-05-03 lo identificó como HIGH risk. NO eliminar.
            //   Y la línea `_gasCtrl = _useHoriRawGas ? null : ...` deja
            //   _gasCtrl=null intencionalmente para HORI — es lo que activa el
            //   bypass via ReadGasRawValue() en el frame loop.
            // Sentinel HORI: si gasPath quedó persistido como HORI_RAW_GAS_PATH
            // pero el device actual NO es HORI (ej. usuario cambió de HORI a
            // G923 sin pasar por re-discovery), invalida el sentinel para que
            // no envenene la lectura del gas. Codex review 2026-05-03.
            if (gasPath == HORI_RAW_GAS_PATH && !IsHORITruck(device))
            {
                Debug.LogWarning($"[UIInputNew] gasPath sentinel HORI persistido pero device es {device.displayName} — limpiando.");
                PlayerPrefs.DeleteKey("G923_GasAxis");
                PlayerPrefs.DeleteKey("G923_GasRest");
                PlayerPrefs.DeleteKey("G923_GasPress");
                PlayerPrefs.Save();
                gasPath = "z";
            }
            _useHoriRawGas = (gasPath == HORI_RAW_GAS_PATH);
            _gasCtrl   = _useHoriRawGas ? null : CacheControl(gasPath);
            #endregion
            _brakeCtrl = CacheControl(brakePath);
            // Clutch (opcional — solo G923 PS). Si no hay path en PlayerPrefs,
            // _clutchCtrl queda null → clutchInput=0 → manual sin desacople.
            string clutchPath = PlayerPrefs.GetString(PREF_G923_CLUTCH_AXIS, "");
            _clutchCtrl = string.IsNullOrEmpty(clutchPath) ? null : CacheControl(clutchPath);

            // Bindings configurables (reversa, paddles, combos, restart, gears).
            // ReloadBindings() llama a ReCacheBindings() que también construye
            // _gearControls (Bind_gear1..6 si configurados, fallback buttons 13-19).
            ReloadBindings();

            // Volante apareció después de Initialize → respetar PlayerPrefs de transmisión
            _isAutomaticMode = PlayerPrefs.GetInt("TransmisionManual", 0) == 0;
            if (_isAutomaticMode && _currentGear == 0) _currentGear = 1;

            // Cargar calibración de pedales (rest+press) hecha en el menú.
            _gasRest    = PlayerPrefs.GetFloat("G923_GasRest",  1f);
            _gasPress   = PlayerPrefs.GetFloat("G923_GasPress", -1f);
            _brakeRest  = PlayerPrefs.GetFloat("G923_BrakeRest",  1f);
            _brakePress = PlayerPrefs.GetFloat("G923_BrakePress", -1f);
            _clutchRest  = PlayerPrefs.GetFloat(PREF_G923_CLUTCH_REST,  -1f);
            _clutchPress = PlayerPrefs.GetFloat(PREF_G923_CLUTCH_PRESS,  1f);

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

        // Adopta un Moto Simulator (ESP32-S3 USB HID) como wheelDevice. Bypassa
        // la heurística axisCount/buttonCount (solo expone 2 botones). Cachea
        // ctrls específicos (lean, hbar) y reusa los slots _gasCtrl/_brakeCtrl/
        // _clutchCtrl para Rz axis y los dos botones. Lectura en UpdateMotoSimulator.
        private bool AttachAsMotoSimulator(InputDevice device, string reason)
        {
            int axisCount = 0, buttonCount = 0;
            foreach (var c in device.allControls)
            {
                if (c is ButtonControl) buttonCount++;
                else if (c is AxisControl) axisCount++;
            }

            _hasWheel = true;
            _wheelDevice = device;
            _shifterDevice = null;
            _isMotoSimulator = true;
            // Moto sin H-shifter: forzar automático sin importar la preferencia
            // de PlayerPrefs "TransmisionManual" (que aplica a auto/camión).
            _isAutomaticMode = true;
            _currentGear = 1;
            // Rigidbody del Player de la moto para steering blend velocidad-dependiente.
            // Búsqueda en cascada: GO actual → ancestros → Player tag. Si nada
            // matchea, queda null y blend=0 (steerInput = handlebar puro). En esce-
            // narios donde UIInputNew se instancia en MainMenu o en un GO sin física,
            // el speed-blend queda inactivo y el comportamiento cae al equivalente
            // de Opción A (handlebar puro), que es funcional aunque no físicamente
            // correcto a alta velocidad.
            _bikeRigidbody = GetComponent<Rigidbody>();
            if (_bikeRigidbody == null) _bikeRigidbody = GetComponentInParent<Rigidbody>();
            if (_bikeRigidbody == null)
            {
                var playerGO = GameObject.FindWithTag("Player");
                if (playerGO != null) _bikeRigidbody = playerGO.GetComponentInChildren<Rigidbody>();
            }

            // Cachear ctrls de los 5 inputs físicos. Paths configurables via PlayerPrefs
            // (override post-F7 si los reales difieren). Defaults asumidos en DEFAULT_MOTO_*.
            string leanPath  = PlayerPrefs.GetString(PREF_MOTO_LEAN_PATH,  DEFAULT_MOTO_LEAN_PATH);
            string hbarPath  = PlayerPrefs.GetString(PREF_MOTO_HBAR_PATH,  DEFAULT_MOTO_HBAR_PATH);
            string gasPath   = PlayerPrefs.GetString(PREF_MOTO_GAS_PATH,   DEFAULT_MOTO_GAS_PATH);
            string brakePath = PlayerPrefs.GetString(PREF_MOTO_BRAKE_PATH, DEFAULT_MOTO_BRAKE_PATH);
            string clutchPath= PlayerPrefs.GetString(PREF_MOTO_CLUTCH_PATH,DEFAULT_MOTO_CLUTCH_PATH);
            _leanCtrl   = CacheControl(leanPath);
            _hbarCtrl   = CacheControl(hbarPath);
            _gasCtrl    = CacheControl(gasPath);
            _brakeCtrl  = CacheControl(brakePath);
            _clutchCtrl = CacheControl(clutchPath);
            // Para que F7/OnGUI/debug reads de _steerCtrl (que asumen wheel-style)
            // muestren el handlebar — sin este alias, los overlays mostrarían null.
            _steerCtrl = _hbarCtrl;

            // Normalización de los controles de la moto:
            //   - Throttle (Rz): firmware envía int8 0..127. Unity típicamente
            //     normaliza axes 8-bit unsigned a [-1,+1] (idle=-1 en raw=0,
            //     full=+1 en raw=127). Si F7 revela layout distinto, override
            //     via PREF_MOTO_GAS_REST/PRESS sin recompilar.
            //   - Brake/clutch: HID buttons en Unity son [0,1] (0=libre, 1=press).
            //     NormalizePedal con rest=0/press=1 da idéntico a leer raw directo.
            _gasRest    = PlayerPrefs.GetFloat(PREF_MOTO_GAS_REST,  -1f);
            _gasPress   = PlayerPrefs.GetFloat(PREF_MOTO_GAS_PRESS, +1f);
            _brakeRest   = 0f; _brakePress  = 1f;
            _clutchRest  = 0f; _clutchPress = 1f;

            // Calibración del rango de lean/handlebar/gas. Defaults [-1,+1] /
            // press=1 asumen que el firmware ya entrega valores normalizados.
            // Si el sensor del chasis está mal montado o el rango efectivo es
            // menor, recalibrar via firmware /calibrate o guardar PREF_MOTO_*
            // dinámicamente desde una pantalla de cal Unity (Fase futura).
            _motoLeanMin = PlayerPrefs.GetFloat(PREF_MOTO_LEAN_MIN, -1f);
            _motoLeanMax = PlayerPrefs.GetFloat(PREF_MOTO_LEAN_MAX, +1f);
            _motoHbarMin = PlayerPrefs.GetFloat(PREF_MOTO_HBAR_MIN, -1f);
            _motoHbarMax = PlayerPrefs.GetFloat(PREF_MOTO_HBAR_MAX, +1f);

            // Reusar el _steerCenter/Max/Min que existe para wheel-style — algunos
            // overlays (F8 BindingsPanel, F10 OnGUI) los leen directo.
            _steerCenter = (_motoHbarMin + _motoHbarMax) * 0.5f;
            _steerMax = _motoHbarMax;
            _steerMin = _motoHbarMin;

            ReloadTuning();

            Debug.Log($"[UIInputNew] Moto Simulator adjuntado ({reason}): {device.displayName}"
                + $" | layout={device.layout} | product='{device.description.product}'"
                + $" | manufacturer='{device.description.manufacturer}' | deviceId={device.deviceId}"
                + $" | axes={axisCount} buttons={buttonCount}"
                + $" | lean[{leanPath}]={_leanCtrl != null} hbar[{hbarPath}]={_hbarCtrl != null}"
                + $" gas[{gasPath}]={_gasCtrl != null}"
                + $" brake[{brakePath}]={_brakeCtrl != null} clutch[{clutchPath}]={_clutchCtrl != null}"
                + $" | bikeRigidbody={_bikeRigidbody != null}");
            return true;
        }

        // Mapea raw ∈ [min, max] a [-1, +1] respetando el centro entre min y max.
        // Útil para lean/handlebar donde el rango efectivo se calibra dinámicamente
        // y el centro físico no necesariamente está en 0 (sensor montado off-axis).
        private static float NormalizeRange(float raw, float min, float max)
        {
            if (Mathf.Abs(max - min) < 0.05f) return 0f;
            float center = (min + max) * 0.5f;
            if (raw >= center)
            {
                float span = max - center;
                if (span < 0.025f) return 0f;
                return Mathf.Clamp01((raw - center) / span);
            }
            else
            {
                float span = center - min;
                if (span < 0.025f) return 0f;
                return -Mathf.Clamp01((center - raw) / span);
            }
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
            return ResolveControlPath(path);
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
        public float GetClutchInput() => clutchInput;
        // True si hay un pedal de clutch físico mapeado (G923 PS variant).
        // ViolationDetector lo consulta antes de evaluar la penalización por
        // "rechino": en Xbox no hay forma de presionar clutch → no penalizar.
#if !(UNITY_ANDROID || UNITY_IOS) || UNITY_EDITOR
        public bool HasPhysicalClutch() => _clutchCtrl != null;
        public bool IsHornPressed() => IsPressed(_hornCtrl);
#else
        public bool HasPhysicalClutch() => false;
        public bool IsHornPressed() => false;
#endif
        // Devuelve y resetea el contador de cambios de marcha sin clutch.
        // Patrón latched (no bool con reset-on-read) para que el orden de
        // Update entre UIInputNew y ViolationDetector no pueda perder eventos.
        public int ConsumeGearShiftsWithoutClutch()
        {
            int n = _pendingGearShiftWithoutClutchCount;
            _pendingGearShiftWithoutClutchCount = 0;
            return n;
        }

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

            // ---- Moto Simulator: ruta de lectura propia (steering blend con
            // velocidad, throttle Rz, brake/clutch como botones). Saltamos toda
            // la lógica wheel-style (gear, paddles, combos H-shifter, etc) que
            // no aplica a la moto.
            if (_isMotoSimulator)
            {
                UpdateMotoSimulator();
                return;
            }

            Vector2 kbInput = _moveAction.ReadValue<Vector2>();

            // ---- Steering: calibrado + deadzone + curva racional ----
            if (_hasWheel)
            {
                float rawSteer = SafeReadFloatRaw(_steerCtrl, out var rs) ? rs : _steerCenter;
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

            // ---- Clutch (solo G923 PS, _clutchCtrl != null) ----
            // En Xbox y sin volante el pedal queda en 0 (motor siempre acoplado).
            // Se calcula antes del bloque gas/brake/gear para que el edge-trigger
            // del H-shifter manual pueda evaluar el estado del clutch del frame.
            if (_hasWheel && _clutchCtrl != null)
            {
                float rawClutch = SafeReadFloatRaw(_clutchCtrl, out var rc) ? rc : _clutchRest;
                clutchInput = NormalizePedal(rawClutch, _clutchRest, _clutchPress);
            }
            else
            {
                clutchInput = 0f;
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
                    float gasRaw = ReadGasRawValue();
                    float brakeRaw = SafeReadFloatRaw(_brakeCtrl, out var rb) ? rb : _brakeRest;
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
                    float gasRaw = ReadGasRawValue();
                    float brakeRaw = SafeReadFloatRaw(_brakeCtrl, out var rb) ? rb : _brakeRest;
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
                                _currentGear = (_gearValues != null && i < _gearValues.Length)
                                    ? _gearValues[i] : 0;
                                break;
                            }
                        }
                    }
                }

                // Edge-trigger: detectar cambio de gear NO-neutral sin clutch
                // presionado (rechino). Comparamos contra el último gear NO-neutral
                // en lugar del gear del frame anterior — si el shifter pasa por
                // neutral entre frames (1→0→2), no perdemos la transición.
                // El contador se acumula hasta que ViolationDetector lo consume,
                // así no importa el orden de Update entre los dos componentes.
                if (_currentGear != 0 && _currentGear != _lastNonNeutralGear && _lastNonNeutralGear != 0)
                {
                    if (clutchInput < 0.5f) _pendingGearShiftWithoutClutchCount++;
                }
                if (_currentGear != 0) _lastNonNeutralGear = _currentGear;
            }

            // ---- Paddles G923 (solo con volante) ----
            if (_hasWheel && _wheelDevice != null)
            {
                bool l1 = IsPressed(_l1Ctrl);
                bool r1 = IsPressed(_r1Ctrl);
                bool hazard = IsPressed(_hazardCtrl);
                if (hazard || (l1 && r1)) _indicatorInput = 2;
                else if (l1) _indicatorInput = -1;
                else if (r1) _indicatorInput = 1;
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
                    float st = SafeReadFloatRaw(_steerCtrl, out var rds) ? rds : 0f;
                    float gr = SafeReadFloatRaw(_gasCtrl, out var rdg) ? rdg : 1f;
                    float br = SafeReadFloatRaw(_brakeCtrl, out var rdb) ? rdb : 1f;
                    bool crossDbg = IsAnyPressed(_crossCtrls);
                    int crossLen = _crossCtrls != null ? _crossCtrls.Length : 0;
                    float reverseAge = Time.realtimeSinceStartup - _reverseLastSeenTime;
                    float clRaw = SafeReadFloatRaw(_clutchCtrl, out var rdc) ? rdc : _clutchRest;
                    Debug.Log($"[UIInputNew] raw steer={st:F3} gas={gr:F3} brake={br:F3} clutch={clRaw:F3} | V={verticalInput:F3} B={brakeInput:F3} C={clutchInput:F3} | gasR/P={_gasRest:F2}/{_gasPress:F2} brakeR/P={_brakeRest:F2}/{_brakePress:F2} clutchR/P={_clutchRest:F2}/{_clutchPress:F2} | auto={_isAutomaticMode} gear={_currentGear} lastNonN={_lastNonNeutralGear} pendingShifts={_pendingGearShiftWithoutClutchCount} | crossCtrls={crossLen} crossNow={crossDbg} revAge={reverseAge:F2}s");
                }
            }
#endif
        }

#if !(UNITY_ANDROID || UNITY_IOS) || UNITY_EDITOR
        // Lectura de input para Moto Simulator. Reemplaza el camino wheel-style
        // del Update() principal — la moto no usa H-shifter, paddles, ni los
        // combos L2+R2/L3+R3.
        //
        // Steering (Opción C, codex 2026-05-01): blend velocidad-dependiente.
        //   - <15 km/h : 100% handlebar (parking, baja velocidad).
        //   - 15-30 km/h : transición SmoothStep entre handlebar y mix lean+hbar.
        //   - >30 km/h : 80% lean + 20% handlebar (asistencia del manubrio).
        // Tunable via PREF_MOTO_HIGH_SPEED_LEAN_WEIGHT y PREF_MOTO_BLEND_*.
        //
        // Throttle: Rz axis [_gasRest..gasPress] → [0,1] via NormalizePedal,
        // luego curva _gasCurveN (heredada de F9 AdvancedInputPanel). Si clutch
        // está apretado, gas → 0 al motor (motor libre, audio puede revs).
        //
        // Brake/Clutch: HID buttons leídos como float 0/1 directo.
        private void UpdateMotoSimulator()
        {
            // Raw reads
            float leanRaw = SafeReadFloatRaw(_leanCtrl, out var rl) ? rl : 0f;
            float hbarRaw = SafeReadFloatRaw(_hbarCtrl, out var rh) ? rh : 0f;
            float gasRaw  = ReadGasRawValue();
            bool brakePressed  = SafeReadFloat(_brakeCtrl,  out var rb) && rb > 0.5f;
            bool clutchPressed = SafeReadFloat(_clutchCtrl, out var rc) && rc > 0.5f;

            // Normalize lean/hbar a [-1, +1] usando rangos calibrados (defaults [-1,+1]
            // si no hay calibración guardada en PlayerPrefs). NormalizeRange respeta
            // el centro entre min y max, útil si el sensor está montado off-axis.
            float lean = NormalizeRange(leanRaw, _motoLeanMin, _motoLeanMax);
            float hbar = NormalizeRange(hbarRaw, _motoHbarMin, _motoHbarMax);

            // Deadzone fino (heredado de F9). Filtra micro-ruido del sensor en reposo.
            if (Mathf.Abs(lean) < _steerDeadzone) lean = 0f;
            if (Mathf.Abs(hbar) < _steerDeadzone) hbar = 0f;

            // Speed-blend (Opción C). Si no hay Rigidbody (e.g. moto sin Player asignado)
            // speedKmh queda 0 → blend=0 → steer = handlebar puro. Funcional pero no
            // físicamente correcto a alta velocidad.
            float speedKmh = 0f;
            if (_bikeRigidbody != null)
            {
#if UNITY_6000_0_OR_NEWER || UNITY_2023_3_OR_NEWER
                speedKmh = _bikeRigidbody.linearVelocity.magnitude * 3.6f;
#else
                speedKmh = _bikeRigidbody.velocity.magnitude * 3.6f;
#endif
            }
            float blendStart = PlayerPrefs.GetFloat(PREF_MOTO_BLEND_START_KMH, DEFAULT_MOTO_BLEND_START_KMH);
            float blendEnd   = PlayerPrefs.GetFloat(PREF_MOTO_BLEND_END_KMH,   DEFAULT_MOTO_BLEND_END_KMH);
            float blend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(blendStart, blendEnd, speedKmh));
            float wHigh = PlayerPrefs.GetFloat(PREF_MOTO_HIGH_SPEED_LEAN_WEIGHT, DEFAULT_MOTO_HIGH_SPEED_LEAN_WEIGHT);
            // A alta velocidad: wHigh*lean + (1-wHigh)*hbar. wHigh=0.8 → 80% lean, 20% hbar.
            // Handlebar mantiene un rol residual de estabilidad/corrección fina.
            float steerHigh = wHigh * lean + (1f - wHigh) * hbar;
            horizontalInput = Mathf.Clamp(Mathf.Lerp(hbar, steerHigh, blend), -1f, +1f);

            // Throttle. Aplica curva _gasCurveN si != 1.0 (config F9).
            float gasNorm = NormalizePedal(gasRaw, _gasRest, _gasPress);
            float gas = Mathf.Approximately(_gasCurveN, 1f) ? gasNorm : Mathf.Pow(gasNorm, _gasCurveN);
            // Clutch apretado → motor libre (sin drive). Útil para revs sin moverse.
            verticalInput = clutchPressed ? 0f : gas;

            // Brake / clutch como botones digitales.
            brakeInput  = brakePressed  ? 1f : 0f;
            clutchInput = clutchPressed ? 1f : 0f;

            // Moto siempre en automático Drive. Sin H-shifter ni reversa explícita.
            _currentGear = 1;
            _indicatorInput = 0;

            // Log periódico (cada 2s) para diagnóstico remoto via LogUploader → S3.
            _debugLogTimer += Time.deltaTime;
            if (_debugLogTimer >= 2f)
            {
                _debugLogTimer = 0f;
                Debug.Log($"[UIInputNew/Moto] raw lean={leanRaw:F3} hbar={hbarRaw:F3}"
                    + $" gas={gasRaw:F3} brakeBtn={(brakePressed ? 1 : 0)} clutchBtn={(clutchPressed ? 1 : 0)}"
                    + $" | norm lean={lean:F3} hbar={hbar:F3} gas={gas:F3}"
                    + $" | speedKmh={speedKmh:F1} blend={blend:F2} wHigh={wHigh:F2}"
                    + $" | H={horizontalInput:F3} V={verticalInput:F3} B={brakeInput:F3} C={clutchInput:F3}");
            }
        }

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
                float st = SafeReadFloatRaw(_steerCtrl, out var ovs) ? ovs : 0f;
                float gr = SafeReadFloatRaw(_gasCtrl, out var ovg) ? ovg : 1f;
                float br = SafeReadFloatRaw(_brakeCtrl, out var ovb) ? ovb : 1f;
                float cl = SafeReadFloatRaw(_clutchCtrl, out var ovc) ? ovc : _clutchRest;
                info += $"RAW  steer={st:F3} gas={gr:F3} brake={br:F3} clutch={cl:F3}\n";
                info += $"CALC V={verticalInput:F3} B={brakeInput:F3} C={clutchInput:F3} gear={_currentGear}\n";
                info += $"CAL  gas rest={_gasRest:F2} press={_gasPress:F2}\n";
                info += $"     brake rest={_brakeRest:F2} press={_brakePress:F2}\n";
                info += $"     clutch ctrl={_clutchCtrl != null} rest={_clutchRest:F2} press={_clutchPress:F2}\n";
                info += $"     pendingShifts={_pendingGearShiftWithoutClutchCount} lastNonN={_lastNonNeutralGear}";
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
