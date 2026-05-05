using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using TMPro;
using QRCoder;
using Gley.UrbanSystem;

/// <summary>
/// Menú principal del simulador — flujo QR/código → opciones → verificación volante.
/// Se auto-adjunta via MenuBootstrap.
/// </summary>
public class MenuScreenManager : MonoBehaviour
{
    // ── API ────────────────────────────────────────────────────────────
    private const string KIOSK_API = "https://d6twaegbhg.execute-api.us-east-1.amazonaws.com/kiosk/sessions";
    private const string SIMULATOR_API = "https://d6twaegbhg.execute-api.us-east-1.amazonaws.com/simulator/lookup";

    // ── Estado ─────────────────────────────────────────────────────────
    private string sessionId;
    private string tramiteId;
    private string citizenName;
    private string licenseType;
    private string selectedSceneName;
    private int selectedVariantIndex = -1;
    // Pantalla 1 — selector de transmisión Auto/Manual (solo licenseType=particular)
    private int selectedTransmisionIndex = 0; // 0=Auto, 1=Manual
    private Coroutine pollingCoroutine;

    // ── Escenas por licenseType ────────────────────────────────────────
    // Nota: "Camioneta" es el nombre real de la escena Unity, pero en UI
    // se muestra como "SUV" (más reconocible para el público de la prueba).
    private readonly string[] variantScenes = { "Sedan", "Camioneta" };

    // ── Constantes de pantallas ────────────────────────────────────────
    // Constantes nombradas en lugar de literales 0..N — antes había hardcodes
    // (ej. `currentScreen == 3` para admin, `GoToScreen(3)`) que rompen al
    // insertar pantallas nuevas. Cualquier nueva pantalla suma un slot al final.
    private const int SCREEN_QR = 0;
    private const int SCREEN_OPTIONS = 1;
    private const int SCREEN_WHEEL = 2;
    private const int SCREEN_ADMIN = 3;
    private const int SCREEN_PRACTICE = 4;
    private const int SCREEN_COUNT = 5;

    // ── UI refs ────────────────────────────────────────────────────────
    private GameObject[] screens = new GameObject[SCREEN_COUNT];
    private CanvasGroup[] screenGroups = new CanvasGroup[SCREEN_COUNT];
    private int currentScreen = -1;
    private GameObject mainTitleGo;

    // Pantalla 0
    private RawImage qrImage;
    private TMP_InputField codeInput;
    private TextMeshProUGUI statusText;
    private TextMeshProUGUI errorText0;
    private Button verifyButton;
    private Button newQRButton;

    // D-pad PIN navigation (volante G923)
    private int pinCursor;
    private int[] pinDigits;
    private TextMeshProUGUI[] pinDigitTexts;
    private Image[] pinBoxBorders;
    private InputControl<float> hatUp, hatDown, hatLeft, hatRight;
    private InputControl<float> circleBtn;
    private InputControl<float> enterBtn;   // button10 = Options/Enter
    private InputControl<float> gasCtrl;    // eje /z — acelerador (para calibración)
    private InputControl<float> brakeCtrl;  // eje /rz — freno (para calibración)
    // Ref explícita al device adjuntado — usada por OnDeviceChange para invalidar
    // el cache cuando el device se desconecta. NO depender de
    // steerAction.controls[0].device porque puede quedar stale si el device fue
    // removido pero el InputAction sigue con binding viejo.
    private InputDevice attachedDevice;
    private System.Action<InputDevice, InputDeviceChange> _deviceChangeHandler;

    // Detección dinámica del eje de cada pedal — lista unificada de candidatos.
    // En la fase gas gana el que más caiga; en la fase brake se excluye el
    // elegido para gas y gana otro distinto. Cubre mapeos cruzados (ej. en
    // este G923 Xbox: gas=stick/y, brake=z).
    private static readonly string[] PEDAL_AXIS_CANDIDATES = {
        "z", "rz", "stick/y", "stick/z", "ry", "rx", "slider", "slider1"
    };
    private InputControl<float>[] pedalCandidates;
    // Calibración por delta signado desde el reposo — funciona con ejes
    // cuyo rest sea +1, 0 o -1, y en cualquier dirección de press.
    private float[] pedalCandidateRests;      // valor al inicio de la fase (reposo)
    private float[] pedalCandidateMaxDeltas;  // delta signado máximo observado
    private int gasChosenIdx = -1; // índice elegido en fase 3; excluir de fase 4
    private const float PEDAL_DELTA_THRESHOLD = 0.85f; // |delta| mínimo para completar fase
    private bool confirmBtnHeld;
    private float dpadUpT, dpadDownT, dpadLeftT, dpadRightT;
    private bool dpadUpR, dpadDownR, dpadLeftR, dpadRightR;
    private const float DPAD_DELAY = 0.4f;
    private const float DPAD_RATE  = 0.15f;

    // D-pad Pantalla 1 — foco vs selección
    private int screen1Row;  // 0=modelo, 1=continuar
    private int screen1Col;  // columna de foco dentro de la fila

    // Pantalla 1
    private GameObject[] variantCards;
    private Image[] variantBorders;
    private GameObject[] transmisionCards;
    private Image[] transmisionBorders;
    private Button continueBtn1;

    // Pantalla Práctica
    // Nombres de escena (deben coincidir EXACTO con Build Settings)
    private static readonly string[] practiceVehicles =
        { "Sedan", "Camioneta", "BusPasajeros", "CamionDCarga", "Motocicleta", "Ambulancia" };
    private static readonly string[] practiceVehicleLabels =
        { "Sedán", "SUV", "Pasajeros", "Carga", "Moto", "Ambulancia" };
    private static readonly string[] practiceWeatherLabels = { "Sol", "Lluvia", "Granizo" };
    // "boxes" son los Image cuadrados a la izquierda de cada opción (estilo checkbox).
    // "rows" son los GameObjects de cada fila clickeable.
    private GameObject[] practiceVehicleRows;
    private Image[] practiceVehicleBoxes;
    private GameObject[] practiceTransmisionRows;
    private Image[] practiceTransmisionBoxes;
    private GameObject practiceTransmisionRow;
    private GameObject[] practiceWeatherRows;
    private Image[] practiceWeatherBoxes;
    private Button practiceContinueBtn;
    private int selectedPracticeVehicleIdx = 0;
    private int selectedPracticeTransmisionIdx = 0; // 0=Auto, 1=Manual
    private int selectedPracticeWeatherIdx = 0;     // 0=Sol
    // El escenario (LocationId) siempre se sortea al azar — no es configurable
    // por el usuario en modo práctica.
    private int practiceRow = 0;
    private int practiceCol = 0;

    // Pantalla 2
    private TextMeshProUGUI wheelPrompt;
    private Image rightIndicator;
    private Image rightFill;
    private RectTransform rightFillRT;
    private Image leftIndicator;
    private Image leftFill;
    private RectTransform leftFillRT;
    // Fases 3 y 4: barras dedicadas para acelerador y freno
    private Image gasIndicator;
    private Image gasFill;
    private RectTransform gasFillRT;
    private Image brakeIndicator;
    private Image brakeFill;
    private RectTransform brakeFillRT;
    // Fase 5: barra booleana para reversa
    private Image reverseIndicator;
    private Image reverseFill;
    private RectTransform reverseFillRT;
    private TextMeshProUGUI examInfoText;
    private bool rightDone, leftDone;
    private bool throttleDone, brakeDone;
    private bool reverseDone;
    private Button skipButton;
    private Button reassignButton;
    // Coroutine del sanity check del estado Verified (splash "Preparando
    // prueba..."). Se cancela si el usuario aprieta "Reasignar controles" o
    // si la pantalla se reabre durante el splash.
    private Coroutine sanityCheckCo;
    private const float WHEEL_THRESHOLD = 0.9f;
    private const float REVERSE_AXIS_DETECT_DELTA = 0.5f; // delta vs baseline para H-shifter en eje
    // Llave de PlayerPrefs con la huella (product|manufacturer|serial) del
    // dispositivo usado para calibrar. Si cambia el modelo de volante, la
    // calibración guardada se invalida y se fuerza descubrimiento completo.
    private const string PREF_CAL_FINGERPRINT = "Cal_DeviceFingerprint";
    // Marca de "fase de reversa completada (con asignación o saltada)" para
    // este device. Se usa porque Bind_reverse tiene un default no vacío
    // (button2), entonces HasKey no distingue "configurado por el usuario" vs
    // "default". Esta llave sí lo distingue.
    private const string PREF_CAL_REVERSE_DONE = "Cal_ReverseDone";

    // Descubrimiento del eje del volante en fase 1 — todos los axes del
    // device, baseline al entrar a la fase, gana el de mayor delta positivo.
    // Hace el flujo agnóstico al modelo (no asume "stick/x").
    private InputControl<float>[] _steerCandidates;
    private float[] _steerCandidateBaselines;
    private string[] _steerCandidatePaths;
    // Si el eje del volante coincide con uno de PEDAL_AXIS_CANDIDATES, se
    // excluye de la fase 3/4 para que no se asigne acelerador o freno al
    // propio volante.
    private int _steerExcludedPedalIdx = -1;
    // Baselines para detección de reversa por eje (H-shifter). Capturados
    // justo antes de la fase 5 — el usuario aún no movió la palanca.
    private System.Collections.Generic.Dictionary<string, float> _reverseAxisBaseline;
    private System.Collections.Generic.HashSet<string> _reverseExcludedPaths;
    // Calibración del steering durante fases 1 y 2:
    //   center = valor raw al entrar a la pantalla (volante centrado)
    //   maxSeen / minSeen = extremos físicos alcanzados en las fases de giro
    private float steerCenter = 0f;
    private float steerMaxSeen = 0f;
    private float steerMinSeen = 0f;
    private bool steerCenterCaptured = false;

    // ── Assets ─────────────────────────────────────────────────────────
    private Texture2D bgTexture;
    private Sprite tlaxcalaLogo;

    // ── Input (G923 como HID genérico) ─────────────────────────────────
    private InputAction steerAction;

    void Start()
    {
        Time.timeScale = 1f;
        LoadResources();
        SetupCanvas();
        ClearExistingChildren();
        BuildLayout();
        ShowScreen(SCREEN_QR);
        StartQRSession();

        // Listener para invalidar cache de InputControl<float> cuando el device
        // adjuntado se desconecta. Sin esto, ReadValue() en hatUp/hatDown/etc
        // lanza InvalidOperationException por frame (verificado en logs S3 —
        // 4995 exceptions/83s en DpadRepeat → UpdatePinDpad, FIX#19).
        _deviceChangeHandler = OnDeviceChange;
        InputSystem.onDeviceChange += _deviceChangeHandler;
    }

    // Reaccionamos solo a Removed/Disconnected/Disabled — UsageChanged es muy
    // amplio (cambios de "primary"/"secondary"), SoftReset/HardReset no
    // implican device inválido, solo resetean estado.
    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (device != attachedDevice) return;
        if (change != InputDeviceChange.Removed
            && change != InputDeviceChange.Disconnected
            && change != InputDeviceChange.Disabled) return;

        Debug.LogWarning($"[MenuScreenManager] Device {change}: {device.displayName}. Reseteando cache.");

        // CRÍTICO: dispose explícito de steerAction. Sin esto, TryAttachToDevice()
        // retorna temprano (chequea steerAction != null) y nunca re-bindea.
        if (steerAction != null) { steerAction.Disable(); steerAction.Dispose(); steerAction = null; }
        hatUp = null; hatDown = null; hatLeft = null; hatRight = null;
        circleBtn = null; enterBtn = null;
        gasCtrl = null; brakeCtrl = null;
        attachedDevice = null;
        // FIX: limpiar pedalCandidates explicitamente. Sin esto, las refs al
        // device viejo persisten tras un replug (ej. OTA del firmware que
        // reinicia USB). Si SnapshotPedalRests corre antes que la siguiente
        // CachePedalCandidates con el device nuevo, los reads fallan silencioso
        // (SafeReadFloatRaw retorna false porque dev.added=false) o tiran
        // InvalidOperationException, y el Discovery se atora — la fase del gas
        // nunca acumula delta aunque el axis Rz del device nuevo si responda.
        if (pedalCandidates != null)
        {
            for (int i = 0; i < pedalCandidates.Length; i++) pedalCandidates[i] = null;
        }
    }

    // Lectura segura: valida que el device esté en el sistema y atrapa la
    // InvalidOperationException que ocurre en la race entre OnDeviceChange y
    // los reads del frame en curso. Sin esto, una desconexión genera spam
    // de 60 exceptions/segundo.
    private static bool SafeReadFloat(InputControl<float> ctrl, out float value)
    {
        value = 0f;
        if (ctrl == null) return false;
        var dev = ctrl.device;
        if (dev == null || !dev.added) return false;
        try { value = ctrl.ReadValue(); return true; }
        catch (System.InvalidOperationException) { return false; }
    }

    private static bool SafeReadFloatRaw(InputControl<float> ctrl, out float value)
    {
        value = 0f;
        if (ctrl == null) return false;
        var dev = ctrl.device;
        if (dev == null || !dev.added) return false;
        try { value = ctrl.ReadUnprocessedValue(); return true; }
        catch (System.InvalidOperationException) { return false; }
    }

    // ════════════════════════════════════════════════════════════════════
    // RESOURCES
    // ════════════════════════════════════════════════════════════════════

    void LoadResources()
    {
        bgTexture = Resources.Load<Texture2D>("MenuBackground");

        Texture2D logoTex = Resources.Load<Texture2D>("TlaxcalaLogo");
        if (logoTex != null)
        {
            float ppu = logoTex.width / 2.0f;
            tlaxcalaLogo = Sprite.Create(logoTex,
                new Rect(0, 0, logoTex.width, logoTex.height),
                new Vector2(0.5f, 0.5f), ppu);
        }

        // El clima se sortea al verificar la sesión (ver PickAndSetWeather), no aquí.
        // Antes hacíamos `Cargolluvia = Random.Range(0,2)` en este punto, pero el sorteo
        // aplicaba al menu (no a la escena de manejo) y los demo codes lo sobreescribían
        // de todas formas.

        // Garantizar GameManager
        if (GameManager.Instance == null)
        {
            GameObject gmObj = new GameObject("GameManager");
            gmObj.AddComponent<GameManager>();
        }
    }

    void SetupCanvas()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null) canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
    }

    void ClearExistingChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);
    }

    // ════════════════════════════════════════════════════════════════════
    // LAYOUT
    // ════════════════════════════════════════════════════════════════════

    void BuildLayout()
    {
        // Background blanco con patrón de flores sutil
        MenuCardBuilder.CreateBackground(transform, bgTexture);

        // Header
        BuildHeader();

        // 5 pantallas
        BuildScreen0_QR();
        BuildScreen1_Options();
        BuildScreen2_Wheel();
        BuildScreen3_Admin();
        BuildScreen_Practice();
    }

    void BuildHeader()
    {
        // Título grande, bold, centrado — como en el wireframe
        // Versión leída dinámicamente de Application.version (Edit > Project Settings > Player > Version)
        mainTitleGo = MenuCardBuilder.CreateText(transform, "MainTitle", "Prueba de Manejo  v" + Application.version,
            MenuTheme.HeaderTitleSize, FontStyles.Bold, MenuTheme.TextPrimary,
            TextAlignmentOptions.Center);
        mainTitleGo.GetComponent<RectTransform>().Set(
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
            new Vector2(0, -50), new Vector2(0, 100));
    }

    // ════════════════════════════════════════════════════════════════════
    // PANTALLA 0 — QR + CÓDIGO MANUAL
    // ════════════════════════════════════════════════════════════════════

    void BuildScreen0_QR()
    {
        GameObject screen = MenuCardBuilder.CreateScreenContainer(transform, "Screen0_QR");
        screens[SCREEN_QR] = screen;
        screenGroups[SCREEN_QR] = screen.GetComponent<CanvasGroup>();

        // ── LAYOUT: 3 columnas iguales (QR · Código TLX · Modo Práctica) ──
        // Anchors X: izq 0.03-0.32, centro 0.35-0.64, der 0.67-0.96.
        // Dividers verticales en X=0.335 y X=0.665.

        // ─── Columna 1: QR ───
        GameObject leftPanel = new GameObject("QRPanel");
        leftPanel.transform.SetParent(screen.transform, false);
        leftPanel.AddComponent<RectTransform>().Set(
            new Vector2(0.03f, 0.05f), new Vector2(0.32f, 0.72f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);

        MenuCardBuilder.CreateText(leftPanel.transform, "QRTitle",
            "Escanea el QR",
            32f, FontStyles.Bold, MenuTheme.TextPrimary, TextAlignmentOptions.Left)
            .GetComponent<RectTransform>().Set(
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
                new Vector2(0, 0), new Vector2(0, 50));

        GameObject qrContainer = MenuCardBuilder.CreateQRDisplay(leftPanel.transform, new Vector2(360, 360));
        qrContainer.GetComponent<RectTransform>().Set(
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(0, -70), new Vector2(360, 360));
        qrImage = qrContainer.transform.Find("QRImage").GetComponent<RawImage>();

        statusText = MenuCardBuilder.CreateText(leftPanel.transform, "Status",
            "Esperando verificación...",
            20f, FontStyles.Normal, MenuTheme.TextSecondary, TextAlignmentOptions.Left)
            .GetComponent<TextMeshProUGUI>();
        statusText.GetComponent<RectTransform>().Set(
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
            new Vector2(0, -445), new Vector2(0, 30));

        newQRButton = MenuCardBuilder.CreateButton(leftPanel.transform, "Generar nuevo QR", "secondary",
            new Vector2(280, 56), () => StartQRSession()).GetComponent<Button>();
        newQRButton.GetComponent<RectTransform>().Set(
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(40, -490), new Vector2(280, 56));
        newQRButton.gameObject.SetActive(false);

        // ─── Divider 1 ───
        GameObject divider1 = new GameObject("VerticalDivider1");
        divider1.transform.SetParent(screen.transform, false);
        divider1.AddComponent<RectTransform>().Set(
            new Vector2(0.335f, 0.10f), new Vector2(0.335f, 0.70f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(1, 0));
        divider1.AddComponent<Image>().color = MenuTheme.DividerColor;

        // ─── Columna 2: Código TLX ───
        GameObject centerPanel = new GameObject("CodePanel");
        centerPanel.transform.SetParent(screen.transform, false);
        centerPanel.AddComponent<RectTransform>().Set(
            new Vector2(0.35f, 0.05f), new Vector2(0.64f, 0.72f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);

        MenuCardBuilder.CreateText(centerPanel.transform, "CodeTitle",
            "Ingresa tu trámite",
            32f, FontStyles.Bold, MenuTheme.TextPrimary, TextAlignmentOptions.Left)
            .GetComponent<RectTransform>().Set(
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
                new Vector2(0, 0), new Vector2(0, 50));

        MenuCardBuilder.CreateText(centerPanel.transform, "Prefix", "TLX-",
            28f, FontStyles.Bold, MenuTheme.TextPrimary, TextAlignmentOptions.Left)
            .GetComponent<RectTransform>().Set(
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
                new Vector2(0, -75), new Vector2(0, 40));

        // PIN input: 6 cajas, achicadas a 56px+10gap (360px total) para caber en columna 29% de 1920.
        const float pinBoxSize = 56f;
        const float pinBoxGap = 10f;
        var pinContainer = MenuCardBuilder.CreatePinInput(centerPanel.transform, 6, pinBoxSize, pinBoxGap);
        pinContainer.GetComponent<RectTransform>().Set(
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(0, -120), new Vector2(6 * pinBoxSize + 5 * pinBoxGap, 70f));
        codeInput = pinContainer.GetComponentInChildren<TMP_InputField>();

        Transform boxRow = pinContainer.transform.Find("BoxRow");
        pinDigits = new int[] { -1, -1, -1, -1, -1, -1 };
        pinDigitTexts = new TextMeshProUGUI[6];
        pinBoxBorders = new Image[6];
        for (int i = 0; i < 6; i++)
        {
            Transform border = boxRow.Find("Border_" + i);
            pinBoxBorders[i] = border.GetComponent<Image>();
            pinDigitTexts[i] = border.Find("Digit_" + i).GetComponent<TextMeshProUGUI>();
        }

        codeInput.onValueChanged.AddListener((string val) =>
        {
            for (int j = 0; j < 6; j++)
                pinDigits[j] = (j < val.Length && char.IsDigit(val[j])) ? (val[j] - '0') : -1;
            pinCursor = Mathf.Clamp(val.Length, 0, 5);
            RefreshPinVisuals();
        });

        codeInput.onSubmit.AddListener((string val) =>
        {
            if (val != null && val.Trim().Length == 6)
                OnVerifyCode();
            else
                codeInput.ActivateInputField();
        });

        verifyButton = MenuCardBuilder.CreateButton(centerPanel.transform, "Verificar Código", "primary",
            new Vector2(0, 64), () => OnVerifyCode()).GetComponent<Button>();
        verifyButton.GetComponent<RectTransform>().Set(
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
            new Vector2(0, -210), new Vector2(0, 64));

        errorText0 = MenuCardBuilder.CreateText(centerPanel.transform, "ErrorText", "",
            18f, FontStyles.Normal, MenuTheme.TextError, TextAlignmentOptions.Left)
            .GetComponent<TextMeshProUGUI>();
        errorText0.textWrappingMode = TextWrappingModes.Normal;
        errorText0.GetComponent<RectTransform>().Set(
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
            new Vector2(0, -290), new Vector2(0, 50));

        // ─── Divider 2 ───
        GameObject divider2 = new GameObject("VerticalDivider2");
        divider2.transform.SetParent(screen.transform, false);
        divider2.AddComponent<RectTransform>().Set(
            new Vector2(0.665f, 0.10f), new Vector2(0.665f, 0.70f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(1, 0));
        divider2.AddComponent<Image>().color = MenuTheme.DividerColor;

        // ─── Columna 3: Modo Práctica ───
        GameObject practicePanel = new GameObject("PracticePanel");
        practicePanel.transform.SetParent(screen.transform, false);
        practicePanel.AddComponent<RectTransform>().Set(
            new Vector2(0.67f, 0.05f), new Vector2(0.96f, 0.72f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);

        MenuCardBuilder.CreateText(practicePanel.transform, "PracticeTitle",
            "Modo Práctica",
            32f, FontStyles.Bold, MenuTheme.TextPrimary, TextAlignmentOptions.Left)
            .GetComponent<RectTransform>().Set(
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
                new Vector2(0, 0), new Vector2(0, 50));

        MenuCardBuilder.CreateText(practicePanel.transform, "PracticeSubtitle",
            "3 minutos · No cuenta como examen",
            18f, FontStyles.Normal, MenuTheme.TextSecondary, TextAlignmentOptions.Left)
            .GetComponent<RectTransform>().Set(
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
                new Vector2(0, -55), new Vector2(0, 30));

        // Blurb corto que cabe en 2 líneas — alineado al área entre el subtítulo
        // y el botón. textWrappingMode=Normal para que rompa líneas en vez de truncar.
        var blurb = MenuCardBuilder.CreateText(practicePanel.transform, "PracticeBlurb",
            "Familiarízate con el simulador antes de tu examen. Elige vehículo, clima y escenario.",
            18f, FontStyles.Normal, MenuTheme.TextSecondary, TextAlignmentOptions.TopLeft)
            .GetComponent<TextMeshProUGUI>();
        blurb.textWrappingMode = TextWrappingModes.Normal;
        blurb.GetComponent<RectTransform>().Set(
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
            new Vector2(0, -110), new Vector2(0, 90));

        // Botón "Practicar" alineado al mismo Y que "Verificar Código" (y=-210)
        // para que las dos CTAs primarias queden en la misma fila visual.
        Button practiceBtn = MenuCardBuilder.CreateButton(practicePanel.transform, "Practicar", "primary",
            new Vector2(0, 64), () => OnPracticeMode()).GetComponent<Button>();
        practiceBtn.GetComponent<RectTransform>().Set(
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
            new Vector2(0, -210), new Vector2(0, 64));
    }

    // ════════════════════════════════════════════════════════════════════
    // PANTALLA 1 — OPCIONES (solo particular)
    // ════════════════════════════════════════════════════════════════════

    void BuildScreen1_Options()
    {
        GameObject screen = MenuCardBuilder.CreateScreenContainer(transform, "Screen1_Options");
        screens[SCREEN_OPTIONS] = screen;
        screenGroups[SCREEN_OPTIONS] = screen.GetComponent<CanvasGroup>();

        // Área completa debajo del título
        GameObject area = new GameObject("CenterArea");
        area.transform.SetParent(screen.transform, false);
        area.AddComponent<RectTransform>().Set(
            new Vector2(0.08f, 0.03f), new Vector2(0.92f, 0.85f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);

        // ── Fila 1: Título + subtítulo (top 15%) ──
        MenuCardBuilder.CreateText(area.transform, "Title", "Configura tu Examen",
            42f, FontStyles.Bold, MenuTheme.PrimaryPurple, TextAlignmentOptions.Center)
            .GetComponent<RectTransform>().Set(
                new Vector2(0, 0.90f), new Vector2(1, 1f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);

        examInfoText = MenuCardBuilder.CreateText(area.transform, "ExamInfo", "",
            22f, FontStyles.Normal, MenuTheme.TextSecondary, TextAlignmentOptions.Center)
            .GetComponent<TextMeshProUGUI>();
        examInfoText.GetComponent<RectTransform>().Set(
            new Vector2(0, 0.84f), new Vector2(1, 0.90f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);

        // ── Fila 2: Modelo label + 2 cards (52%-80%) ──
        MenuCardBuilder.CreateText(area.transform, "ModelLabel", "Selecciona tu modelo",
            24f, FontStyles.Bold, MenuTheme.TextPrimary, TextAlignmentOptions.Center)
            .GetComponent<RectTransform>().Set(
                new Vector2(0, 0.78f), new Vector2(1, 0.86f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);

        string[] titles = { "Sedan", "SUV" };
        string[] descs = { "Compacto estándar", "Familiar SUV" };
        string[] letters = { "S", "U" };
        variantCards = new GameObject[titles.Length];
        variantBorders = new Image[titles.Length];

        for (int i = 0; i < titles.Length; i++)
        {
            int idx = i;
            float cardW = 0.30f;
            float gap = 0.04f;
            float totalW = cardW * titles.Length + gap * (titles.Length - 1);
            float startX = (1f - totalW) / 2f;
            float left = startX + i * (cardW + gap);

            GameObject card = MenuCardBuilder.CreateIconCard(area.transform, null,
                titles[i], descs[i], new Vector2(80, 80), letters[i]);
            card.GetComponent<RectTransform>().Set(
                new Vector2(left, 0.52f), new Vector2(left + cardW, 0.78f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            card.AddComponent<CanvasGroup>();

            Button btn = card.AddComponent<Button>();
            btn.targetGraphic = card.transform.Find("Background").GetComponent<Image>();
            btn.onClick.AddListener(() => OnVariantSelected(idx));
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            cb.pressedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            cb.fadeDuration = 0.12f;
            btn.colors = cb;

            variantCards[i] = card;
            variantBorders[i] = card.transform.Find("Border").GetComponent<Image>();
        }

        // ── Fila 3: Transmisión label + 2 cards (22%-48%) ──
        MenuCardBuilder.CreateText(area.transform, "TransmisionLabel", "Tipo de transmisión",
            24f, FontStyles.Bold, MenuTheme.TextPrimary, TextAlignmentOptions.Center)
            .GetComponent<RectTransform>().Set(
                new Vector2(0, 0.46f), new Vector2(1, 0.51f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);

        string[] tTitles = { "Automática", "Manual" };
        string[] tDescs = { "Sin clutch", "Con clutch + H-shifter" };
        string[] tLetters = { "A", "M" };
        transmisionCards = new GameObject[tTitles.Length];
        transmisionBorders = new Image[tTitles.Length];

        for (int i = 0; i < tTitles.Length; i++)
        {
            int idx = i;
            float cardW = 0.30f;
            float gap = 0.04f;
            float totalW = cardW * tTitles.Length + gap * (tTitles.Length - 1);
            float startX = (1f - totalW) / 2f;
            float left = startX + i * (cardW + gap);

            GameObject card = MenuCardBuilder.CreateIconCard(area.transform, null,
                tTitles[i], tDescs[i], new Vector2(60, 60), tLetters[i]);
            card.GetComponent<RectTransform>().Set(
                new Vector2(left, 0.22f), new Vector2(left + cardW, 0.45f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            card.AddComponent<CanvasGroup>();

            Button btn = card.AddComponent<Button>();
            btn.targetGraphic = card.transform.Find("Background").GetComponent<Image>();
            btn.onClick.AddListener(() => OnTransmisionSelected(idx));
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            cb.pressedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            cb.fadeDuration = 0.12f;
            btn.colors = cb;

            transmisionCards[i] = card;
            transmisionBorders[i] = card.transform.Find("Border").GetComponent<Image>();
        }

        // ── Fila 4: Continuar (parte baja) ──
        // OnContinueScreen1 persiste TransmisionManual antes de navegar a Pantalla 2.
        continueBtn1 = MenuCardBuilder.CreateButton(area.transform, "Continuar", "primary",
            new Vector2(100, 100), OnContinueScreen1).GetComponent<Button>();
        continueBtn1.GetComponent<RectTransform>().Set(
            new Vector2(0.3f, 0.04f), new Vector2(0.7f, 0.18f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);

        // Defaults DESPUÉS de crear los botones.
        OnVariantSelected(0);
        // Transmisión inicial: leer la última elección o default Automática.
        selectedTransmisionIndex = PlayerPrefs.GetInt("TransmisionManual", 0);
        OnTransmisionSelected(selectedTransmisionIndex);
    }

    void OnTransmisionSelected(int idx)
    {
        selectedTransmisionIndex = idx;
        for (int i = 0; i < transmisionCards.Length; i++)
        {
            bool sel = (i == idx);
            transmisionBorders[i].color = sel ? MenuTheme.CardBorderGold : MenuTheme.CardBorder;
            transmisionCards[i].transform.Find("Background").GetComponent<Image>().color =
                sel ? MenuTheme.CardSelected : MenuTheme.CardBackground;

            if (sel) StartCoroutine(MenuAnimator.ScalePunch(
                transmisionCards[i].GetComponent<RectTransform>(),
                MenuTheme.CardPunchScale, MenuTheme.CardPunchDuration));
        }
    }

    void OnContinueScreen1()
    {
        PlayerPrefs.SetInt("TransmisionManual", selectedTransmisionIndex);
        PlayerPrefs.Save();
        Debug.Log($"[MenuScreenManager] Transmisión: {(selectedTransmisionIndex == 1 ? "Manual" : "Automática")}");
        GoToScreen(SCREEN_WHEEL);
    }

    void OnVariantSelected(int idx)
    {
        selectedVariantIndex = idx;
        selectedSceneName = variantScenes[idx];

        for (int i = 0; i < variantCards.Length; i++)
        {
            bool sel = (i == idx);
            variantBorders[i].color = sel ? MenuTheme.CardBorderGold : MenuTheme.CardBorder;
            variantCards[i].transform.Find("Background").GetComponent<Image>().color =
                sel ? MenuTheme.CardSelected : MenuTheme.CardBackground;

            if (sel) StartCoroutine(MenuAnimator.ScalePunch(
                variantCards[i].GetComponent<RectTransform>(),
                MenuTheme.CardPunchScale, MenuTheme.CardPunchDuration));
        }

        EnableButton(continueBtn1, true);
    }

    // ════════════════════════════════════════════════════════════════════
    // PANTALLA 2 — VERIFICACIÓN VOLANTE
    // ════════════════════════════════════════════════════════════════════

    void BuildScreen2_Wheel()
    {
        GameObject screen = MenuCardBuilder.CreateScreenContainer(transform, "Screen2_Wheel");
        screens[SCREEN_WHEEL] = screen;
        screenGroups[SCREEN_WHEEL] = screen.GetComponent<CanvasGroup>();

        // Usar anchors amplios, centrado vertical
        GameObject area = new GameObject("CenterArea");
        area.transform.SetParent(screen.transform, false);
        area.AddComponent<RectTransform>().Set(
            new Vector2(0.1f, 0.08f), new Vector2(0.9f, 0.82f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);

        float y = 0;

        // Prompt principal — más compacto para dejar espacio a las 4 barras
        wheelPrompt = MenuCardBuilder.CreateText(area.transform, "Prompt",
            "Gira el volante hacia la DERECHA",
            38f, FontStyles.Bold, MenuTheme.TextPrimary, TextAlignmentOptions.Center)
            .GetComponent<TextMeshProUGUI>();
        wheelPrompt.textWrappingMode = TextWrappingModes.Normal;
        wheelPrompt.GetComponent<RectTransform>().Set(
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
            new Vector2(0, y), new Vector2(0, 80));
        y -= 100;

        // Barra DERECHA (fill izquierda → derecha)
        BuildLabeledBar(area.transform, "DERECHA", ref y, true,
            out rightIndicator, out rightFill, out rightFillRT);

        // Barra IZQUIERDA (fill derecha → izquierda)
        BuildLabeledBar(area.transform, "IZQUIERDA", ref y, false,
            out leftIndicator, out leftFill, out leftFillRT);

        // Barra ACELERADOR (fill izquierda → derecha)
        BuildLabeledBar(area.transform, "ACELERADOR", ref y, true,
            out gasIndicator, out gasFill, out gasFillRT);

        // Barra FRENO (fill izquierda → derecha)
        BuildLabeledBar(area.transform, "FRENO", ref y, true,
            out brakeIndicator, out brakeFill, out brakeFillRT);

        // Barra REVERSA — booleana (rellena al detectar botón o cambio de eje)
        BuildLabeledBar(area.transform, "REVERSA", ref y, true,
            out reverseIndicator, out reverseFill, out reverseFillRT);

        y -= 15; // separación antes de info

        // Exam info — legible
        var infoObj = MenuCardBuilder.CreateText(area.transform, "ExamInfo2", "",
            22f, FontStyles.Normal, MenuTheme.TextSecondary, TextAlignmentOptions.Center);
        infoObj.GetComponent<RectTransform>().Set(
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
            new Vector2(0, y), new Vector2(0, 35));
        examInfoText = infoObj.GetComponent<TextMeshProUGUI>();
        y -= 60;

        // Botón skip — más grande
        skipButton = MenuCardBuilder.CreateButton(area.transform, "Iniciar sin volante", "primary",
            new Vector2(350, 65), () => OnSkipWheel()).GetComponent<Button>();
        skipButton.GetComponent<RectTransform>().Set(
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
            new Vector2(0, y), new Vector2(350, 65));
        y -= 80;

        // Botón secundario — solo visible en estado Verified (splash). Permite
        // forzar descubrimiento si el sanity check no detectó axis stuck pero
        // el operador sabe que el mapeo está mal.
        reassignButton = MenuCardBuilder.CreateButton(area.transform, "Reasignar controles", "secondary",
            new Vector2(280, 50), () => OnReassignControls()).GetComponent<Button>();
        reassignButton.GetComponent<RectTransform>().Set(
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
            new Vector2(0, y), new Vector2(280, 50));
        reassignButton.gameObject.SetActive(false);
    }

    // ════════════════════════════════════════════════════════════════════
    // QR / API
    // ════════════════════════════════════════════════════════════════════

    void StartQRSession()
    {
        if (errorText0 != null) errorText0.text = "";
        if (statusText != null) statusText.text = "Generando código QR...";
        if (newQRButton != null) newQRButton.gameObject.SetActive(false);
        StartCoroutine(CreateKioskSession());
    }

    IEnumerator CreateKioskSession()
    {
        UnityWebRequest req = new UnityWebRequest(KIOSK_API, "POST");
        byte[] body = System.Text.Encoding.UTF8.GetBytes("{}");
        req.uploadHandler = new UploadHandlerRaw(body);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.timeout = 10;

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            ShowError0("Error de conexión. Verifica la red.");
            newQRButton.gameObject.SetActive(true);
            req.Dispose();
            yield break;
        }

        var response = JsonUtility.FromJson<SessionResponse>(req.downloadHandler.text);
        sessionId = response.sessionId;
        IdSesion._Mi_ID = sessionId;

        // Generar QR
        QRCodeGenerator qrGen = new QRCodeGenerator();
        QRCodeData qrData = qrGen.CreateQrCode(response.verifyUrl, QRCodeGenerator.ECCLevel.Q);
        PngByteQRCode qrCode = new PngByteQRCode(qrData);
        byte[] qrBytes = qrCode.GetGraphic(20);

        Texture2D qrTex = new Texture2D(2, 2);
        qrTex.LoadImage(qrBytes);
        qrTex.filterMode = FilterMode.Point;
        if (qrImage != null) qrImage.texture = qrTex;

        statusText.text = "Esperando verificación...";
        req.Dispose();

        // Iniciar polling
        if (pollingCoroutine != null) StopCoroutine(pollingCoroutine);
        pollingCoroutine = StartCoroutine(PollSessionStatus());
    }

    IEnumerator PollSessionStatus()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(10f);

            string url = KIOSK_API + "/" + sessionId + "/status";
            UnityWebRequest req = UnityWebRequest.Get(url);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                long code = req.responseCode;
                req.Dispose();
                // 404 = row purgada por TTL, 410 = expiresAt < now. Ambos = QR muerto:
                // regenerar silenciosamente (igual que el cliente Next.js del kiosk).
                // Sin este branch el coroutine se queda polleando un sid muerto cada
                // 10s para siempre — fuente histórica de >60k 404s/día en CloudWatch.
                if (code == 404 || code == 410)
                {
                    StartCoroutine(CreateKioskSession());
                    yield break;
                }
                // 5xx, network errors, timeouts → reintentar en el siguiente tick.
                continue;
            }

            var response = JsonUtility.FromJson<StatusResponse>(req.downloadHandler.text);
            req.Dispose();

            if (response.status == "verified")
            {
                tramiteId = response.tramiteId;
                citizenName = response.citizenName;
                licenseType = response.licenseType;
                GameManager.Instance.LocationId = 1;
                PickAndSetWeather(-1); // sin override en flujo backend
                OnSessionVerified();
                yield break;
            }
            else if (response.status == "expired" || response.status == "failed")
            {
                statusText.text = "Sesión expirada.";
                newQRButton.gameObject.SetActive(true);
                yield break;
            }
        }
    }

    // Demo codes formato `TTTTXY` (6 dígitos):
    //   TTTT = primeros 4 dígitos repetidos del tipo (0000=particular, 1111=pasajeros,
    //          2222=moto, 3333=carga, 4444=ambulancia)
    //   X    = override de clima (0=sol, 1=lluvia, 2=granizo); cualquier otro valor
    //          (3-9) = sin override, sortea random ponderado en PickAndSetWeather.
    //   Y    = ubicación de spawn (0=random, 1..5=waypoint fijo).
    static readonly Dictionary<string, (string id, string name, string type)> DEMO_PREFIXES = new Dictionary<string, (string, string, string)>
    {
        { "0000", ("TLX-DEMO00000", "Demo Automóvil",  "particular")  },
        { "1111", ("TLX-DEMO11111", "Demo Pasajeros",  "publico")     },
        { "2222", ("TLX-DEMO22222", "Demo Moto",       "motocicleta") },
        { "3333", ("TLX-DEMO33333", "Demo Carga",      "carga")       },
        { "4444", ("TLX-DEMO44444", "Demo Ambulancia", "emergencia")  },
    };

    void OnVerifyCode()
    {
        string code = codeInput != null ? codeInput.text.Trim().ToUpper() : "";

        // Demo codes para testing sin backend (formato TTTTXY, ver DEMO_PREFIXES).
        if (code.Length == 6 && DEMO_PREFIXES.TryGetValue(code.Substring(0, 4), out var demo)
            && code[5] >= '0' && code[5] <= '5')
        {
            tramiteId = demo.id;
            citizenName = demo.name;
            licenseType = demo.type;
            GameManager.Instance.LocationId = code[5] - '0';

            // Override de clima si el 5° dígito es 0/1/2; otro valor → random ponderado.
            int weatherOverride = (code[4] >= '0' && code[4] <= '2') ? (code[4] - '0') : -1;
            PickAndSetWeather(weatherOverride);

            OnSessionVerified();
            return;
        }

        if (string.IsNullOrEmpty(code))
        {
            ShowError0("Ingresa un código de cita.");
            return;
        }
        StartCoroutine(LookupByCode(code));
    }

    // Pesos del sorteo de clima.
    //   Iter actual (granizo desactivado, validación pendiente con TTT2Y):
    //     60% sol / 40% lluvia / 0% granizo
    //   Iter futuro (cuando granizo esté validado visualmente):
    //     ajustar a 40% sol / 30% lluvia / 30% granizo (cambiar las 3 constantes).
    private const float WEATHER_WEIGHT_SOL = 0.60f;
    private const float WEATHER_WEIGHT_RAIN = 0.40f;
    private const float WEATHER_WEIGHT_HAIL = 0.00f;

    // Sortea el clima de la sesión y lo persiste en PlayerPrefs para que WeatherManager
    // lo lea en la siguiente escena. `overrideClima` ∈ {0,1,2} fuerza valor; -1 sortea
    // random ponderado. Llamado desde los 3 paths de verificación (demo, QR poll, lookup).
    void PickAndSetWeather(int overrideClima)
    {
        int clima;
        if (overrideClima >= 0 && overrideClima <= 2)
        {
            clima = overrideClima;
        }
        else
        {
            float r = Random.value;
            if (r < WEATHER_WEIGHT_SOL) clima = 0;
            else if (r < WEATHER_WEIGHT_SOL + WEATHER_WEIGHT_RAIN) clima = 1;
            else clima = 2;
        }
        PlayerPrefs.SetInt("Clima", clima);
        // Mirror al PlayerPref legacy `Cargolluvia` para no romper diagnóstico
        // (LogConsolePanel y LogUploader lo leen). TODO: eliminar cuando esos
        // consumidores migren a leer "Clima" directamente.
        PlayerPrefs.SetInt("Cargolluvia", clima == 1 ? 1 : 0);
        PlayerPrefs.Save();
        Debug.Log($"[MenuScreenManager] Clima sorteado: {clima} (override={overrideClima})");
    }

    IEnumerator LookupByCode(string code)
    {
        errorText0.text = "";
        statusText.text = "Verificando código...";
        verifyButton.interactable = false;

        string url = SIMULATOR_API + "?code=" + UnityWebRequest.EscapeURL(code);
        UnityWebRequest req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        verifyButton.interactable = true;

        if (req.result != UnityWebRequest.Result.Success)
        {
            if (req.responseCode == 404)
                ShowError0("Trámite no encontrado. Verifica tu código e intenta de nuevo.");
            else if (req.responseCode == 400)
                ShowError0("Este trámite no tiene una cita agendada para el simulador.");
            else
                ShowError0("Error de conexión. Verifica la red.");
            statusText.text = "Esperando verificación...";
            req.Dispose();
            yield break;
        }

        var response = JsonUtility.FromJson<LookupResponse>(req.downloadHandler.text);
        req.Dispose();

        tramiteId = response.tramiteId;
        citizenName = response.citizenName;
        licenseType = response.licenseType;
        GameManager.Instance.LocationId = 1;
        PickAndSetWeather(-1); // sin override en flujo backend
        OnSessionVerified();
    }

    void OnSessionVerified()
    {
        if (pollingCoroutine != null) StopCoroutine(pollingCoroutine);

        GameManager.Instance.Expediente = tramiteId;
        GameManager.Instance.TramiteId = tramiteId;
        GameManager.Instance.CitizenName = citizenName;
        GameManager.Instance.LicenseType = licenseType;

        // Determinar escena según licenseType
        switch (licenseType)
        {
            case "motocicleta":
                selectedSceneName = "Motocicleta";
                GoToScreen(SCREEN_WHEEL); // Directo a verificación volante
                break;
            case "publico":
                selectedSceneName = "BusPasajeros";
                GoToScreen(SCREEN_WHEEL);
                break;
            case "carga":
                selectedSceneName = "CamionDCarga";
                GoToScreen(SCREEN_WHEEL);
                break;
            case "emergencia":
                selectedSceneName = "Ambulancia";
                GoToScreen(SCREEN_WHEEL);
                break;
            case "particular":
            default:
                GoToScreen(SCREEN_OPTIONS); // Mostrar opciones
                break;
        }
    }

    void ShowError0(string msg)
    {
        if (errorText0 != null) errorText0.text = msg;
    }

    // Helper: construye una fila con etiqueta + barra de progreso.
    // fillLeftToRight=true → rightFill (crece desde la izquierda).
    // fillLeftToRight=false → leftFill (crece desde la derecha).
    void BuildLabeledBar(Transform parent, string label, ref float y, bool fillLeftToRight,
        out Image indicator, out Image fill, out RectTransform fillRT)
    {
        const float labelHeight = 22f;
        const float barHeight = 38f;

        // Etiqueta
        MenuCardBuilder.CreateText(parent, label + "_Label", label,
            18f, FontStyles.Bold, MenuTheme.TextSecondary, TextAlignmentOptions.Center)
            .GetComponent<RectTransform>().Set(
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
                new Vector2(0, y), new Vector2(0, labelHeight));
        y -= labelHeight + 2;

        // Track de la barra
        GameObject bar = new GameObject(label + "_Bar");
        bar.transform.SetParent(parent, false);
        bar.AddComponent<RectTransform>().Set(
            new Vector2(0.15f, 1), new Vector2(0.85f, 1), new Vector2(0.5f, 1),
            new Vector2(0, y), new Vector2(0, barHeight));
        indicator = bar.AddComponent<Image>();
        indicator.sprite = MenuCardBuilder.GetRoundedSprite(MenuTheme.CornerRadiusSmall);
        indicator.type = Image.Type.Sliced;
        indicator.color = MenuTheme.IndicatorPending;

        // Fill dentro del track
        GameObject fillObj = new GameObject(label + "_Fill");
        fillObj.transform.SetParent(bar.transform, false);
        fillRT = fillObj.AddComponent<RectTransform>();
        if (fillLeftToRight)
        {
            fillRT.anchorMin = new Vector2(0, 0);
            fillRT.anchorMax = new Vector2(0, 1);
        }
        else
        {
            fillRT.anchorMin = new Vector2(1, 0);
            fillRT.anchorMax = new Vector2(1, 1);
        }
        fillRT.offsetMin = new Vector2(4, 4);
        fillRT.offsetMax = new Vector2(-4, -4);
        fill = fillObj.AddComponent<Image>();
        fill.sprite = MenuCardBuilder.GetRoundedSprite(MenuTheme.CornerRadiusSmall);
        fill.type = Image.Type.Sliced;
        fill.color = MenuTheme.SecondaryCrimson;
        fill.raycastTarget = false;

        y -= barHeight + 10; // separación entre filas
    }

    // ════════════════════════════════════════════════════════════════════
    // PANTALLA 3 — ADMIN CONFIG (F10 para acceder)
    // ════════════════════════════════════════════════════════════════════

    // Admin screen refs
    private TMP_InputField adminNameInput;
    private TMP_InputField adminApiUrlInput;
    private TextMeshProUGUI adminUidLabel;
    private TextMeshProUGUI adminSimulatorLabel;
    private Toggle adminDisplayToggle;
    private Toggle adminNotificationsToggle;
    private TextMeshProUGUI adminDisplayMapLabel;

    void BuildScreen3_Admin()
    {
        GameObject screen = MenuCardBuilder.CreateScreenContainer(transform, "Screen3_Admin");
        screens[SCREEN_ADMIN] = screen;
        screenGroups[SCREEN_ADMIN] = screen.GetComponent<CanvasGroup>();

        // Botón Cerrar flotante (esquina superior derecha — fuera del Content
        // para que la layout nunca pueda recortarlo).
        GameObject closeBtn = MenuCardBuilder.CreateButton(screen.transform, "Cerrar", "primary",
            new Vector2(140f, 50f), () => GoToScreen(SCREEN_QR));
        closeBtn.GetComponent<RectTransform>().Set(
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
            new Vector2(-30, -30), new Vector2(140, 50));

        // Content container (top-center, ancho fijo — ContentSizeFitter
        // controla la altura sin pelearse con anchors estirados).
        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(screen.transform, false);
        RectTransform contentRt = contentObj.AddComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0.5f, 1f);
        contentRt.anchorMax = new Vector2(0.5f, 1f);
        contentRt.pivot     = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = new Vector2(0, -40);
        contentRt.sizeDelta = new Vector2(900, 0);

        var layout = contentObj.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 4f;
        layout.padding = new RectOffset(30, 30, 4, 4);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        contentObj.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Transform ct = contentObj.transform;
        var config = SimulatorConfig.Instance?.data ?? new SimulatorConfig.ConfigData();

        // ── Header ──
        AdminAddHeader(ct, "Configuración  v" + Application.version);

        // ── UID (read-only) ──
        string uidDisplay = string.IsNullOrEmpty(config.pcId)
            ? "Generando..."
            : config.pcId.Length > 16
                ? config.pcId.Substring(0, 8) + "..." + config.pcId.Substring(config.pcId.Length - 4)
                : config.pcId;
        adminUidLabel = AdminAddLabel(ct, "UID", uidDisplay);

        // ── Nombre (editable) ──
        adminNameInput = AdminAddField(ct, "Nombre", config.name, "PC Simulador 1");

        // ── API URL (editable) ──
        adminApiUrlInput = AdminAddField(ct, "API URL", config.apiBaseUrl, "https://...");

        // ── Modo de pantallas (1 prueba / 3 producción) ──
        GameObject displayToggleGo = MenuCardBuilder.CreateToggle(ct,
            "Modo prueba (1 pantalla)", config.displayCount == 1);
        displayToggleGo.AddComponent<LayoutElement>().preferredHeight = 38f;
        adminDisplayToggle = displayToggleGo.GetComponent<Toggle>();

        // ── Notificaciones en pantalla ──
        GameObject notifToggleGo = MenuCardBuilder.CreateToggle(ct,
            "Mostrar notificaciones en pantalla", config.showNotifications);
        notifToggleGo.AddComponent<LayoutElement>().preferredHeight = 38f;
        adminNotificationsToggle = notifToggleGo.GetComponent<Toggle>();

        // ── Intercambiar displays ──
        adminDisplayMapLabel = AdminAddLabel(ct, "Displays", DisplayMapString(config));

        GameObject swapRow = new GameObject("SwapRow");
        swapRow.transform.SetParent(ct, false);
        swapRow.AddComponent<RectTransform>();
        var swapLayout = swapRow.AddComponent<HorizontalLayoutGroup>();
        swapLayout.spacing = 12f;
        swapLayout.childAlignment = TextAnchor.MiddleCenter;
        swapLayout.childControlWidth = false;
        swapLayout.childControlHeight = false;
        swapLayout.childForceExpandWidth = false;
        swapRow.AddComponent<LayoutElement>().preferredHeight = 45f;

        MenuCardBuilder.CreateButton(swapRow.transform, "Izq / Centro", "secondary",
            new Vector2(190f, 40f), () => OnSwapDisplays("left", "center"));
        MenuCardBuilder.CreateButton(swapRow.transform, "Centro / Der", "secondary",
            new Vector2(190f, 40f), () => OnSwapDisplays("center", "right"));
        MenuCardBuilder.CreateButton(swapRow.transform, "Izq / Der", "secondary",
            new Vector2(190f, 40f), () => OnSwapDisplays("left", "right"));

        // ── Simulador asignado ──
        adminSimulatorLabel = AdminAddLabel(ct, "Simulador", FormatSimulatorDisplay(config));

        AdminAddSpacer(ct, 8f);

        // ── Botones ──
        GameObject btnRow = new GameObject("ButtonRow");
        btnRow.transform.SetParent(ct, false);
        btnRow.AddComponent<RectTransform>();
        var hLayout = btnRow.AddComponent<HorizontalLayoutGroup>();
        hLayout.spacing = 20f;
        hLayout.childAlignment = TextAnchor.MiddleCenter;
        hLayout.childControlWidth = false;
        hLayout.childControlHeight = false;
        hLayout.childForceExpandWidth = false;
        var btnLe = btnRow.AddComponent<LayoutElement>();
        btnLe.preferredHeight = 55f;

        MenuCardBuilder.CreateButton(btnRow.transform, "Guardar", "primary",
            new Vector2(220f, 50f), OnAdminSave);

        // Refresh simulator assignment from backend on panel open
        StartCoroutine(AdminRefreshSimulator());

        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRt);
    }

    // ════════════════════════════════════════════════════════════════════
    // PANTALLA PRÁCTICA — Configuración del Modo Práctica (3 minutos)
    // ════════════════════════════════════════════════════════════════════

    void BuildScreen_Practice()
    {
        GameObject screen = MenuCardBuilder.CreateScreenContainer(transform, "Screen_Practice");
        screens[SCREEN_PRACTICE] = screen;
        screenGroups[SCREEN_PRACTICE] = screen.GetComponent<CanvasGroup>();

        GameObject area = new GameObject("CenterArea");
        area.transform.SetParent(screen.transform, false);
        area.AddComponent<RectTransform>().Set(
            new Vector2(0.06f, 0.03f), new Vector2(0.94f, 0.85f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);

        // ── Título (92-100%) ──
        MenuCardBuilder.CreateText(area.transform, "Title", "Configura tu Práctica",
            38f, FontStyles.Bold, MenuTheme.PrimaryPurple, TextAlignmentOptions.Center)
            .GetComponent<RectTransform>().Set(
                new Vector2(0, 0.92f), new Vector2(1, 1f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);

        MenuCardBuilder.CreateText(area.transform, "Subtitle",
            "3 minutos de manejo libre · No cuenta como examen",
            18f, FontStyles.Normal, MenuTheme.TextSecondary, TextAlignmentOptions.Center)
            .GetComponent<RectTransform>().Set(
                new Vector2(0, 0.86f), new Vector2(1, 0.92f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);

        // Layout vertical (top → bottom): Vehículo, Condiciones, Transmisión (último
        // porque es opcional y desaparece para vehículos sin transmisión configurable).
        // El escenario siempre se sortea aleatorio — no es configurable por el usuario.

        // ── Tipo de vehículo (68-83%) ──
        BuildPracticeSectionLabel(area.transform, "VehicleLabel", "Tipo de vehículo", 0.78f, 0.83f);
        practiceVehicleRows = new GameObject[practiceVehicles.Length];
        practiceVehicleBoxes = new Image[practiceVehicles.Length];
        BuildPracticeOptions(area.transform, "Vehicle", practiceVehicleLabels,
            0.66f, 0.77f,
            practiceVehicleRows, practiceVehicleBoxes,
            i => OnPracticeVehicleSelected(i));

        // ── Condiciones (44-59%) ──
        BuildPracticeSectionLabel(area.transform, "WeatherLabel", "Condiciones", 0.54f, 0.59f);
        practiceWeatherRows = new GameObject[3];
        practiceWeatherBoxes = new Image[3];
        BuildPracticeOptions(area.transform, "Weather", practiceWeatherLabels,
            0.42f, 0.53f,
            practiceWeatherRows, practiceWeatherBoxes,
            i => OnPracticeWeatherSelected(i));

        // ── Tipo de transmisión (20-35%) — visible solo si Sedan/SUV. Va al final
        // porque su visibilidad cambia según el vehículo y no debe causar que el resto
        // del layout salte cuando se oculta. ──
        practiceTransmisionRow = new GameObject("TransmisionRow");
        practiceTransmisionRow.transform.SetParent(area.transform, false);
        practiceTransmisionRow.AddComponent<RectTransform>().Set(
            new Vector2(0, 0.20f), new Vector2(1, 0.35f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        BuildPracticeSectionLabel(practiceTransmisionRow.transform, "TransmisionLabel",
            "Tipo de transmisión", 0.66f, 1f);
        practiceTransmisionRows = new GameObject[2];
        practiceTransmisionBoxes = new Image[2];
        BuildPracticeOptions(practiceTransmisionRow.transform, "Transmision",
            new[] { "Automática", "Manual" },
            0f, 0.60f,
            practiceTransmisionRows, practiceTransmisionBoxes,
            i => OnPracticeTransmisionSelected(i));

        // ── Continuar (3-15%) ──
        practiceContinueBtn = MenuCardBuilder.CreateButton(area.transform, "Iniciar Práctica", "primary",
            new Vector2(100, 70), OnContinuePractice).GetComponent<Button>();
        practiceContinueBtn.GetComponent<RectTransform>().Set(
            new Vector2(0.32f, 0.03f), new Vector2(0.68f, 0.15f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);

        // Defaults visuales
        OnPracticeVehicleSelected(0);
        OnPracticeTransmisionSelected(0);
        OnPracticeWeatherSelected(0);
    }

    /// <summary>Etiqueta de sección ("Tipo de vehículo", "Condiciones", etc.) — bold, izquierda.</summary>
    void BuildPracticeSectionLabel(Transform parent, string name, string text, float y0, float y1)
    {
        MenuCardBuilder.CreateText(parent, name, text,
            22f, FontStyles.Bold, MenuTheme.TextPrimary, TextAlignmentOptions.Left)
            .GetComponent<RectTransform>().Set(
                new Vector2(0, y0), new Vector2(1, y1), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
    }

    /// <summary>
    /// Construye una fila horizontal de opciones tipo checkbox (cuadrado + label).
    /// Usa HorizontalLayoutGroup para que cada opción ocupe sólo el ancho que necesita
    /// — sin estirarse para llenar el espacio del padre.
    /// y0/y1 son anchors verticales dentro del parent. La row se ancla a la izquierda.
    /// </summary>
    void BuildPracticeOptions(Transform parent, string namePrefix, string[] labels,
        float y0, float y1, GameObject[] outRows, Image[] outBoxes, System.Action<int> onClick)
    {
        // Contenedor del row de opciones — usa HorizontalLayoutGroup para distribuir
        // las opciones a la izquierda con espaciado fijo (NO estiradas).
        GameObject container = new GameObject(namePrefix + "_OptionsContainer");
        container.transform.SetParent(parent, false);
        container.AddComponent<RectTransform>().Set(
            new Vector2(0, y0), new Vector2(1, y1), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        var containerHlg = container.AddComponent<HorizontalLayoutGroup>();
        containerHlg.spacing = 32f;
        containerHlg.childAlignment = TextAnchor.MiddleLeft;
        containerHlg.childControlWidth = true;
        containerHlg.childControlHeight = true;
        containerHlg.childForceExpandWidth = false;  // ← clave: no estirar
        containerHlg.childForceExpandHeight = false;
        containerHlg.padding = new RectOffset(0, 0, 0, 0);

        for (int i = 0; i < labels.Length; i++)
        {
            int idx = i;

            // Row de opción (cuadrado + label) — auto-tamaño según contenido.
            GameObject row = new GameObject(namePrefix + "_Row_" + i);
            row.transform.SetParent(container.transform, false);
            row.AddComponent<RectTransform>(); // tamaño lo gestiona el HLG padre

            var rowHlg = row.AddComponent<HorizontalLayoutGroup>();
            rowHlg.spacing = 12f;
            rowHlg.childAlignment = TextAnchor.MiddleLeft;
            rowHlg.childControlWidth = true;
            rowHlg.childControlHeight = true;
            rowHlg.childForceExpandWidth = false;
            rowHlg.childForceExpandHeight = false;
            rowHlg.padding = new RectOffset(2, 2, 2, 2);

            var rowFitter = row.AddComponent<ContentSizeFitter>();
            rowFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            rowFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Image transparente para que toda la row capture clicks.
            var rowImg = row.AddComponent<Image>();
            rowImg.color = new Color(1f, 1f, 1f, 0.001f);
            rowImg.raycastTarget = true;

            Button btn = row.AddComponent<Button>();
            btn.targetGraphic = rowImg;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => onClick(idx));

            // Caja del checkbox (cuadrado).
            GameObject box = new GameObject("Box");
            box.transform.SetParent(row.transform, false);
            box.AddComponent<RectTransform>();
            var boxLayout = box.AddComponent<LayoutElement>();
            boxLayout.preferredWidth = 26f;
            boxLayout.preferredHeight = 26f;
            boxLayout.flexibleWidth = 0f;
            var boxImg = box.AddComponent<Image>();
            boxImg.color = MenuTheme.CardBorder;
            boxImg.raycastTarget = false;

            // Inner blanco (simula borde cuando unselected).
            GameObject inner = new GameObject("Inner");
            inner.transform.SetParent(box.transform, false);
            var innerRt = inner.AddComponent<RectTransform>();
            innerRt.anchorMin = Vector2.zero;
            innerRt.anchorMax = Vector2.one;
            innerRt.offsetMin = new Vector2(2, 2);
            innerRt.offsetMax = new Vector2(-2, -2);
            var innerImg = inner.AddComponent<Image>();
            innerImg.color = Color.white;
            innerImg.raycastTarget = false;

            // Label TMP a la derecha — auto-tamaño al texto.
            var label = MenuCardBuilder.CreateText(row.transform, "Label", labels[i],
                20f, FontStyles.Normal, MenuTheme.TextPrimary, TextAlignmentOptions.MidlineLeft);
            var labelTmp = label.GetComponent<TextMeshProUGUI>();
            labelTmp.raycastTarget = false;
            labelTmp.textWrappingMode = TextWrappingModes.NoWrap;
            // Reset anchors del label (CreateText puede setear stretch); aquí queremos
            // que el LayoutElement controle el tamaño preferido.
            var labelRt = label.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.zero;
            labelRt.pivot = new Vector2(0, 0.5f);
            labelRt.sizeDelta = Vector2.zero;
            var labelLayout = label.AddComponent<LayoutElement>();
            labelLayout.preferredWidth = labelTmp.GetPreferredValues(labels[i]).x;
            labelLayout.preferredHeight = 28f;
            labelLayout.flexibleWidth = 0f;

            outRows[i] = row;
            outBoxes[i] = boxImg;
        }
    }

    void OnPracticeVehicleSelected(int idx)
    {
        selectedPracticeVehicleIdx = idx;
        UpdatePracticeOptions(practiceVehicleBoxes, idx);
        // Transmisión solo aplica a Sedan (0) y Camioneta/SUV (1).
        bool transmisionVisible = (idx == 0 || idx == 1);
        if (practiceTransmisionRow != null) practiceTransmisionRow.SetActive(transmisionVisible);
    }

    void OnPracticeTransmisionSelected(int idx)
    {
        selectedPracticeTransmisionIdx = idx;
        UpdatePracticeOptions(practiceTransmisionBoxes, idx);
    }

    void OnPracticeWeatherSelected(int idx)
    {
        selectedPracticeWeatherIdx = idx;
        UpdatePracticeOptions(practiceWeatherBoxes, idx);
    }

    /// <summary>Pinta el cuadrado seleccionado morado y los demás en gris claro.</summary>
    void UpdatePracticeOptions(Image[] boxes, int selectedIdx)
    {
        if (boxes == null) return;
        for (int i = 0; i < boxes.Length; i++)
        {
            bool sel = (i == selectedIdx);
            boxes[i].color = sel ? MenuTheme.PrimaryPurple : MenuTheme.CardBorder;
            // Toggle inner: si seleccionado lo escondemos para que se vea sólido morado;
            // si no, queda blanco simulando el "borde" de una checkbox vacía.
            Transform inner = boxes[i].transform.Find("Inner");
            if (inner != null)
            {
                var innerImg = inner.GetComponent<Image>();
                if (innerImg != null) innerImg.color = sel ? MenuTheme.PrimaryPurple : Color.white;
            }
        }
    }

    /// <summary>Click en card "Modo Práctica" de pantalla 0 → setear contexto y abrir pantalla práctica.</summary>
    void OnPracticeMode()
    {
        // Limpiar estado local del menú — StartSessionAndLoadScene() lee tramiteId/citizenName/licenseType
        // del menu, NO de GameManager. Sin esto, una práctica heredaría el tramiteId de un examen previo.
        tramiteId = null;
        citizenName = "Práctica";
        licenseType = "practica";

        // Setear contexto cross-scene
        var gm = GameManager.Instance;
        gm.IsPracticeMode = true;
        gm.PracticeId = System.Guid.NewGuid().ToString();
        gm.TramiteId = null;
        gm.SessionId = null;
        gm.CitizenName = "Práctica";

        GoToScreen(SCREEN_PRACTICE);
    }

    /// <summary>"Iniciar Práctica" → persistir selecciones y avanzar a verificación de volante.</summary>
    void OnContinuePractice()
    {
        var gm = GameManager.Instance;
        gm.PracticeVehicleType = practiceVehicles[selectedPracticeVehicleIdx];
        bool isAuto = (selectedPracticeVehicleIdx == 0 || selectedPracticeVehicleIdx == 1);
        gm.PracticeTransmission = isAuto
            ? (selectedPracticeTransmisionIdx == 1 ? "Manual" : "Automatica")
            : null;
        gm.PracticeWeather = practiceWeatherLabels[selectedPracticeWeatherIdx];

        // El escenario en práctica siempre es aleatorio — convención de SpawnLocationManager:
        // LocationId=0 sortea waypoint random entre 1..5 al cargar la escena.
        gm.LocationId = 0;
        gm.PracticeSpawnLocation = "random";
        // PracticeStartedAt lo setea ExamTimer.Start() al cargar la escena — no aquí —
        // para no inflar el tiempo con la verificación de volante y el LoadScene.

        // Solo persistir TransmisionManual cuando el vehículo expone esa elección
        // (Sedan/SUV). Para Bus/Camión/Moto/Ambulancia forzamos Automática para que
        // la verificación de volante no requiera clutch por una elección oculta del
        // usuario en una práctica anterior con Sedan.
        PlayerPrefs.SetInt("TransmisionManual", isAuto ? selectedPracticeTransmisionIdx : 0);
        PlayerPrefs.SetInt("Clima", selectedPracticeWeatherIdx);
        // Mirror al PlayerPref legacy `Cargolluvia` (LogConsolePanel/LogUploader lo leen).
        PlayerPrefs.SetInt("Cargolluvia", selectedPracticeWeatherIdx == 1 ? 1 : 0);
        PlayerPrefs.Save();

        selectedSceneName = practiceVehicles[selectedPracticeVehicleIdx];
        GoToScreen(SCREEN_WHEEL);
    }

    // ── Admin UI Helpers ─────────────────────────────────────────────

    void AdminAddHeader(Transform parent, string text)
    {
        var obj = MenuCardBuilder.CreateText(parent, "H_" + text, text,
            36f, FontStyles.Bold, MenuTheme.PrimaryPurple, TextAlignmentOptions.Center);
        obj.AddComponent<LayoutElement>().preferredHeight = 56f;
    }

    void AdminAddSection(Transform parent, string text)
    {
        var obj = MenuCardBuilder.CreateText(parent, "S_" + text, text,
            MenuTheme.SubtitleSize, FontStyles.Bold, MenuTheme.TextPrimary, TextAlignmentOptions.Left);
        obj.AddComponent<LayoutElement>().preferredHeight = 35f;
    }

    TMP_InputField AdminAddField(Transform parent, string label, string value, string placeholder)
    {
        GameObject container = MenuCardBuilder.CreateInputField(parent, label, placeholder,
            new Vector2(0, 45f));
        // CreateInputField sets sizeDelta.y = size.y + 25 (label + input).
        // With childControlHeight=true, the layout rewrites height — match exactly
        // so the input doesn't get clipped by 2px.
        container.AddComponent<LayoutElement>().preferredHeight = 70f;
        TMP_InputField input = container.GetComponentInChildren<TMP_InputField>();
        if (input != null) input.text = value ?? "";
        return input;
    }

    TextMeshProUGUI AdminAddLabel(Transform parent, string label, string value)
    {
        GameObject row = new GameObject("L_" + label);
        row.transform.SetParent(parent, false);
        row.AddComponent<RectTransform>();
        var hl = row.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = 10f;
        hl.childAlignment = TextAnchor.MiddleLeft;
        hl.childControlWidth = true;
        hl.childControlHeight = true;
        hl.childForceExpandWidth = true;
        row.AddComponent<LayoutElement>().preferredHeight = 30f;

        var k = MenuCardBuilder.CreateText(row.transform, "Key", label + ":",
            MenuTheme.LabelSize, FontStyles.Normal, MenuTheme.TextSecondary, TextAlignmentOptions.Left);
        k.AddComponent<LayoutElement>().flexibleWidth = 1f;
        var v = MenuCardBuilder.CreateText(row.transform, "Val", value ?? "—",
            MenuTheme.LabelSize, FontStyles.Bold, MenuTheme.TextPrimary, TextAlignmentOptions.Right);
        v.AddComponent<LayoutElement>().flexibleWidth = 1f;

        return v.GetComponent<TextMeshProUGUI>();
    }

    void AdminAddSpacer(Transform parent, float h)
    {
        GameObject s = new GameObject("Spacer");
        s.transform.SetParent(parent, false);
        s.AddComponent<RectTransform>();
        s.AddComponent<LayoutElement>().preferredHeight = h;
    }

    // ── Admin Actions ────────────────────────────────────────────────

    string DisplayMapString(SimulatorConfig.ConfigData cfg)
    {
        return $"C={cfg.displayCenter}  I={cfg.displayLeft}  D={cfg.displayRight}";
    }

    void OnSwapDisplays(string a, string b)
    {
        if (SimulatorConfig.Instance == null) return;
        var data = SimulatorConfig.Instance.data;

        int va = a == "center" ? data.displayCenter : a == "left" ? data.displayLeft : data.displayRight;
        int vb = b == "center" ? data.displayCenter : b == "left" ? data.displayLeft : data.displayRight;

        if (a == "center") data.displayCenter = vb; else if (a == "left") data.displayLeft = vb; else data.displayRight = vb;
        if (b == "center") data.displayCenter = va; else if (b == "left") data.displayLeft = va; else data.displayRight = va;

        SimulatorConfig.Instance.Save();
        if (MultiPantallaManager.Instance != null)
            MultiPantallaManager.Instance.Apply();
        if (adminDisplayMapLabel != null)
            adminDisplayMapLabel.text = DisplayMapString(data);
        Debug.Log($"[Admin] Swap {a}/{b}: {DisplayMapString(data)}");
    }

    void OnAdminSave()
    {
        if (SimulatorConfig.Instance == null) return;
        var data = SimulatorConfig.Instance.data;

        data.name = adminNameInput?.text ?? data.name;
        data.apiBaseUrl = adminApiUrlInput?.text ?? data.apiBaseUrl;
        if (adminDisplayToggle != null)
            data.displayCount = adminDisplayToggle.isOn ? 1 : 3;
        if (adminNotificationsToggle != null)
            data.showNotifications = adminNotificationsToggle.isOn;

        SimulatorConfig.Instance.Save();

        if (MultiPantallaManager.Instance != null)
            MultiPantallaManager.Instance.Apply();

        // Registrar en backend
        StartCoroutine(AdminRegisterBackend(data));

        Debug.Log("[Admin] Configuracion guardada");
        GoToScreen(SCREEN_QR);
    }

    IEnumerator AdminRegisterBackend(SimulatorConfig.ConfigData data)
    {
        string url = $"{data.apiBaseUrl}/simulator/register";
        string json = JsonUtility.ToJson(new AdminRegisterRequest
        {
            pcId = data.pcId,
            name = data.name,
            appVersion = Application.version,
            platform = "windows"
        });

        using (var req = new UnityEngine.Networking.UnityWebRequest(url, "POST"))
        {
            req.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(
                System.Text.Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 10;
            yield return req.SendWebRequest();

            if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Debug.Log("[Admin] Registrado en backend");
                // Parse response to get simulatorId
                try
                {
                    var response = JsonUtility.FromJson<AdminRegisterResponse>(req.downloadHandler.text);
                    if (!string.IsNullOrEmpty(response.simulatorId))
                    {
                        data.simulatorId = response.simulatorId;
                        data.simulatorName = response.simulatorName ?? "";
                        SimulatorConfig.Instance.Save();

                        if (GameManager.Instance != null)
                            GameManager.Instance.SimulatorId = response.simulatorId;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[Admin] Error parseando respuesta: {e.Message}");
                }
            }
            else
            {
                Debug.LogWarning($"[Admin] Error registrando: {req.error}");
            }
        }
    }

    string FormatSimulatorDisplay(SimulatorConfig.ConfigData cfg)
    {
        if (cfg == null || string.IsNullOrEmpty(cfg.simulatorId))
            return "Sin asignar";
        return string.IsNullOrEmpty(cfg.simulatorName) ? cfg.simulatorId : cfg.simulatorName;
    }

    IEnumerator AdminRefreshSimulator()
    {
        // SendBootRegister consulta el backend (/simulator/register) y
        // sincroniza simulatorId/Name al config local sin pisar el name del PC.
        yield return SimulatorApiClient.SendBootRegister();

        if (adminSimulatorLabel != null)
            adminSimulatorLabel.text = FormatSimulatorDisplay(SimulatorConfig.Instance?.data);
    }

    /// <summary>Llamado desde AdminPanel.cs cuando F10 abre el admin.</summary>
    public void NavigateToAdmin() => GoToScreen(SCREEN_ADMIN);

    [System.Serializable]
    private class AdminRegisterRequest
    {
        public string pcId;
        public string name;
        public string appVersion;
        public string platform;
    }

    [System.Serializable]
    private class AdminRegisterResponse
    {
        public bool success;
        public string pcId;
        public string simulatorId;
        public string simulatorName;
        public bool created;
    }

    // ════════════════════════════════════════════════════════════════════
    // NAVEGACIÓN
    // ════════════════════════════════════════════════════════════════════

    void ShowScreen(int index)
    {
        if (index < 0 || index >= screens.Length) return;

        if (currentScreen >= 0)
            StartCoroutine(MenuAnimator.FadeCanvasGroup(
                screenGroups[currentScreen], 1f, 0f, MenuTheme.ScreenFadeDuration));

        currentScreen = index;

        if (mainTitleGo != null) mainTitleGo.SetActive(index != 3);

        // Preparar pantalla
        if (index == 1 && examInfoText != null)
        {
            string name = string.IsNullOrEmpty(citizenName) ? "" : $"Hola, {citizenName}. ";
            examInfoText.text = $"{name}Examen: Vehículo Particular";
        }
        if (index == 2)
        {
            PrepareWheelScreen();
        }
        StartCoroutine(ShowScreenDelayed(index));
    }

    void GoToScreen(int index) => ShowScreen(index);

    IEnumerator ShowScreenDelayed(int index)
    {
        yield return new WaitForSecondsRealtime(0.15f);
        StartCoroutine(MenuAnimator.FadeCanvasGroup(
            screenGroups[index], 0f, 1f, MenuTheme.ScreenFadeDuration));

        // Esperar a que el fade termine para que interactable=true
        yield return new WaitForSecondsRealtime(MenuTheme.ScreenFadeDuration + 0.05f);

        if (index == 0 && codeInput != null)
        {
            codeInput.Select();
            codeInput.ActivateInputField();
            RefreshPinVisuals();
        }
    }

    void PrepareWheelScreen()
    {
        // Cancelar splash en curso si la pantalla se reabre
        if (sanityCheckCo != null) { StopCoroutine(sanityCheckCo); sanityCheckCo = null; }

        // Migración de formato: los valores de calibración pre-v2 fueron capturados
        // con ReadValue() (con axisDeadzone de Unity). Ahora se usan lecturas raw
        // (ReadUnprocessedValue). Forzar re-discovery para capturar valores consistentes.
        const int CAL_FORMAT_VERSION = 2;
        if (PlayerPrefs.GetInt("Cal_FormatVersion", 0) < CAL_FORMAT_VERSION)
        {
            ClearWheelCalibration();
            PlayerPrefs.SetInt("Cal_FormatVersion", CAL_FORMAT_VERSION);
            PlayerPrefs.Save();
            Debug.Log("[MenuScreenManager] Calibration format upgraded → forcing re-discovery");
        }

        if (Time.timeScale != 1f)
            Debug.LogWarning($"[MenuScreenManager] PrepareWheelScreen: timeScale={Time.timeScale}");

        // ── 1) Detectar dispositivo y comparar huella con la calibración guardada ──
        InputDevice dev = TryAttachToDevice();

        // FAST-PATH: Moto Simulator (ESP32-S3 USB HID custom). Las 5 fases del
        // Discovery wheel-style no aplican a la moto: lean/handlebar son ejes
        // distintos, brake/clutch son botones (no ejes con rest/press), y la
        // moto ya tiene defaults razonables hardcoded en UIInputNew.AttachAsMoto-
        // Simulator. Saltamos directo al gameplay. Calibración por rango específica
        // de la moto se hace via firmware /calibrate (BNO chasis center) o, en
        // iteración futura, via MotoCalibrationController (UI Unity dedicada).
        //
        // Detección multicapa:
        //   1) IsMotoSimulator(dev): strings ("Moto Simulator"/"SimuladoresTlax") o VID/PID 0x4D54.
        //   2) Fallback session-context: si licenseType=="motocicleta" Y el device es
        //      Joystick con axis rz Y no es G923/HORI/Shifter — asumir moto sim.
        //      Cubre el caso edge donde el firmware enumere con strings/VID-PID
        //      default de TinyUSB (bug pre-v2.5.7) y Windows mantenga "TinyUSB HID".
        bool isMotoSession = string.Equals(licenseType, "motocicleta", System.StringComparison.OrdinalIgnoreCase);
        bool isMotoFallback = dev != null && dev is Joystick
            && dev.TryGetChildControl("rz") != null
            && !UIInputNew.IsLogitechG923Family(dev)
            && !UIInputNew.IsHORITruck(dev)
            && !UIInputNew.IsShifterDevice(dev);
        if (dev != null && (UIInputNew.IsMotoSimulator(dev) || (isMotoSession && isMotoFallback)))
        {
            if (!UIInputNew.IsMotoSimulator(dev))
                Debug.LogWarning($"[MenuScreenManager] Moto fast-path via session-context fallback (licenseType=motocicleta + Joystick con rz). Device: '{dev.displayName}'");
            string fp = ComputeDeviceFingerprint(dev);
            if (!string.IsNullOrEmpty(fp))
                PlayerPrefs.SetString(UIInputNew.PREF_MOTO_DEVICE_FINGERPRINT, fp);
            PlayerPrefs.Save();
            // Marcar todas las fases como completadas (visual feedback verde).
            rightDone = leftDone = throttleDone = brakeDone = reverseDone = true;
            steerCenter = 0f; steerMaxSeen = 1f; steerMinSeen = -1f; steerCenterCaptured = true;
            rightIndicator.color = leftIndicator.color = gasIndicator.color =
                brakeIndicator.color = reverseIndicator.color = MenuTheme.IndicatorDone;
            rightFillRT.anchorMax = new Vector2(1, 1); rightFill.color = MenuTheme.IndicatorDone;
            leftFillRT.anchorMin  = new Vector2(0, 0); leftFill.color  = MenuTheme.IndicatorDone;
            gasFillRT.anchorMax   = new Vector2(1, 1); gasFill.color   = MenuTheme.IndicatorDone;
            brakeFillRT.anchorMax = new Vector2(1, 1); brakeFill.color = MenuTheme.IndicatorDone;
            reverseFillRT.anchorMax = new Vector2(1, 1); reverseFill.color = MenuTheme.IndicatorDone;
            wheelPrompt.text = "Moto Simulator detectado. Cargando prueba...";
            if (skipButton != null) skipButton.gameObject.SetActive(false);
            if (reassignButton != null) reassignButton.gameObject.SetActive(false);
            StartCoroutine(LoadSceneDelayed(1.0f));
            return;
        }

        // FAST-PATH: si es Logitech/G923, aplicar mapping pre-Wednesday hardcoded
        // y saltar TODA la calibración dinámica. La calibración capturaba señales
        // fantasma (button19, stick/y, stick/down siempre on en este G923) y
        // tomó 3 días destrabarlo. El operador puede F9 para tunear curvas.
        if (dev != null && UIInputNew.IsLogitechG923Family(dev))
        {
            UIInputNew.EnsureG923PSDefaults(dev);
            string fp = ComputeDeviceFingerprint(dev);
            if (!string.IsNullOrEmpty(fp)) PlayerPrefs.SetString(PREF_CAL_FINGERPRINT, fp);
            PlayerPrefs.Save();
            // Marcar todas las fases como completadas para que la UI muestre verde.
            rightDone = leftDone = throttleDone = brakeDone = reverseDone = true;
            steerCenter = 0f; steerMaxSeen = 1f; steerMinSeen = -1f; steerCenterCaptured = true;
            // UI: indicadores y fills en estado "done".
            rightIndicator.color = leftIndicator.color = gasIndicator.color =
                brakeIndicator.color = reverseIndicator.color = MenuTheme.IndicatorDone;
            rightFillRT.anchorMax = new Vector2(1, 1); rightFill.color = MenuTheme.IndicatorDone;
            leftFillRT.anchorMin  = new Vector2(0, 0); leftFill.color  = MenuTheme.IndicatorDone;
            gasFillRT.anchorMax   = new Vector2(1, 1); gasFill.color   = MenuTheme.IndicatorDone;
            brakeFillRT.anchorMax = new Vector2(1, 1); brakeFill.color = MenuTheme.IndicatorDone;
            reverseFillRT.anchorMax = new Vector2(1, 1); reverseFill.color = MenuTheme.IndicatorDone;
            wheelPrompt.text = "Volante Logitech detectado. Cargando prueba...";
            if (skipButton != null) skipButton.gameObject.SetActive(false);
            if (reassignButton != null) reassignButton.gameObject.SetActive(false);
            StartCoroutine(LoadSceneDelayed(1.0f));
            return;
        }

        // HORI Truck: NO fast-path completo. Discovery sí corre para steering/
        // brake/clutch (esas axes Unity sí las expone). Solo el Phase 3 (gas)
        // se auto-pasa porque el byte del throttle quedó huérfano en el HID
        // parser y HoriThrottleReader.cs lo lee directo del state buffer.
        // Ver gas-phase block más abajo.

        string currentFp = ComputeDeviceFingerprint(dev);
        string savedFp = PlayerPrefs.GetString(PREF_CAL_FINGERPRINT, "");
        bool fingerprintMatches = !string.IsNullOrEmpty(currentFp) && currentFp == savedFp;

        // Si la huella cambió, la calibración guardada se hizo con otro modelo
        // de volante (axis distintos) → invalidar y forzar discovery completo.
        if (!string.IsNullOrEmpty(savedFp) && !fingerprintMatches)
        {
            Debug.Log($"[MenuScreenManager] Huella de dispositivo cambió ('{savedFp}' → '{currentFp}'), invalidando calibración guardada");
            ClearWheelCalibration();
        }

        // ── 2) Calcular qué fases ya tienen calibración válida ──
        bool steeringCal = fingerprintMatches
            && PlayerPrefs.HasKey("G923_SteerMax") && PlayerPrefs.HasKey("G923_SteerMin");
        bool pedalsCal = fingerprintMatches
            && PlayerPrefs.HasKey("G923_GasAxis")   && PlayerPrefs.HasKey("G923_GasRest")   && PlayerPrefs.HasKey("G923_GasPress")
            && PlayerPrefs.HasKey("G923_BrakeAxis") && PlayerPrefs.HasKey("G923_BrakeRest") && PlayerPrefs.HasKey("G923_BrakePress");
        bool reverseCal = fingerprintMatches && PlayerPrefs.GetInt(PREF_CAL_REVERSE_DONE, 0) == 1;

        rightDone = leftDone = steeringCal;
        throttleDone = brakeDone = pedalsCal;
        reverseDone = reverseCal;

        // Reset deltas de candidatos para esta sesión de calibración
        if (pedalCandidateMaxDeltas != null)
            for (int i = 0; i < pedalCandidateMaxDeltas.Length; i++) pedalCandidateMaxDeltas[i] = 0f;
        gasChosenIdx = -1;

        // Reset descubrimiento del eje del volante. Snapshot se hace en
        // Update al entrar a fase 1 (el usuario ya tiene las manos puestas).
        _steerCandidates = null;
        _steerCandidateBaselines = null;
        _steerCandidatePaths = null;
        _steerExcludedPedalIdx = -1;
        // Si el steering ya está calibrado, recuperar el índice excluido a
        // partir del path guardado para que las fases de pedal lo respeten.
        if (steeringCal)
        {
            string savedSteer = PlayerPrefs.GetString(UIInputNew.PREF_BIND_STEER_AXIS, UIInputNew.DEFAULT_BIND_STEER_AXIS);
            for (int i = 0; i < PEDAL_AXIS_CANDIDATES.Length; i++)
                if (PEDAL_AXIS_CANDIDATES[i] == savedSteer) { _steerExcludedPedalIdx = i; break; }
        }

        // Reset captura del centro del volante (extremos se capturan en Update)
        steerCenter = 0f;
        steerMaxSeen = 0f;
        steerMinSeen = 0f;
        steerCenterCaptured = false;

        // Indicadores y fills reflejan el estado por fase
        rightIndicator.color   = rightDone    ? MenuTheme.IndicatorDone : MenuTheme.IndicatorPending;
        leftIndicator.color    = leftDone     ? MenuTheme.IndicatorDone : MenuTheme.IndicatorPending;
        gasIndicator.color     = throttleDone ? MenuTheme.IndicatorDone : MenuTheme.IndicatorPending;
        brakeIndicator.color   = brakeDone    ? MenuTheme.IndicatorDone : MenuTheme.IndicatorPending;
        reverseIndicator.color = reverseDone  ? MenuTheme.IndicatorDone : MenuTheme.IndicatorPending;

        rightFillRT.anchorMax = new Vector2(rightDone ? 1f : 0f, 1);
        rightFill.color = rightDone ? MenuTheme.IndicatorDone : MenuTheme.SecondaryCrimson;
        leftFillRT.anchorMin = new Vector2(leftDone ? 0f : 1f, 0);
        leftFill.color = leftDone ? MenuTheme.IndicatorDone : MenuTheme.SecondaryCrimson;
        gasFillRT.anchorMax = new Vector2(throttleDone ? 1f : 0f, 1);
        gasFill.color = throttleDone ? MenuTheme.IndicatorDone : MenuTheme.SecondaryCrimson;
        brakeFillRT.anchorMax = new Vector2(brakeDone ? 1f : 0f, 1);
        brakeFill.color = brakeDone ? MenuTheme.IndicatorDone : MenuTheme.SecondaryCrimson;
        reverseFillRT.anchorMax = new Vector2(reverseDone ? 1f : 0f, 1);
        reverseFill.color = reverseDone ? MenuTheme.IndicatorDone : MenuTheme.SecondaryCrimson;

        // ── 3) Decidir el modo de la pantalla ──
        bool fullyCalibrated = steeringCal && pedalsCal && reverseCal;

        if (fullyCalibrated)
        {
            // Verified: nada que pedir al usuario, solo splash + sanity check.
            wheelPrompt.text = "Preparando prueba...";
            if (skipButton != null) skipButton.gameObject.SetActive(false);
            if (reassignButton != null) reassignButton.gameObject.SetActive(true);
            sanityCheckCo = StartCoroutine(SanityCheckThenLoad(1.5f));
        }
        else
        {
            // Discovery / Partial: prompt según primera fase pendiente.
            wheelPrompt.text = !rightDone    ? "Gira el volante hacia la DERECHA"
                             : !leftDone     ? "Gira el volante hacia la IZQUIERDA"
                             : !throttleDone ? "Pisa el ACELERADOR a fondo"
                             : !brakeDone    ? "Pisa el FRENO a fondo"
                             :                 "Activa la REVERSA";
            if (skipButton != null) skipButton.gameObject.SetActive(true);
            if (reassignButton != null) reassignButton.gameObject.SetActive(false);
        }

        string examType = licenseType switch
        {
            "particular" => "Vehículo Particular",
            "motocicleta" => "Motocicleta",
            "publico" => "Transporte Público",
            "carga" => "Carga Pesada",
            _ => licenseType
        };
        string name = string.IsNullOrEmpty(citizenName) ? "" : citizenName + " | ";
        if (examInfoText != null) examInfoText.text = $"{name}Examen: {examType}";
    }

    void Update()
    {
        if (currentScreen == SCREEN_ADMIN && Keyboard.current != null
            && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            GoToScreen(SCREEN_QR);
            return;
        }

        if (currentScreen == SCREEN_QR)
            UpdatePinDpad();
        else if (currentScreen == SCREEN_OPTIONS)
            UpdateOptionsDpad();
        else if (currentScreen == SCREEN_PRACTICE)
            UpdatePracticeDpad();

        if (currentScreen != SCREEN_WHEEL) return;

        // Leer input (dispara detección+cacheo de device/pedales) antes de pintar debug
        float steer = ReadSteerInput();

        // Capturar el centro del volante al primer frame en que steerAction esté listo.
        // El raw inicial puede no ser exactamente 0 (deadzone mecánico / calibración del HID).
        if (!steerCenterCaptured && steerAction != null)
        {
            steerCenter = steer;
            steerMaxSeen = steer;
            steerMinSeen = steer;
            steerCenterCaptured = true;
        }
        // Trackear extremos raw durante cualquier fase de giro
        if (steer > steerMaxSeen) steerMaxSeen = steer;
        if (steer < steerMinSeen) steerMinSeen = steer;

        if (rightDone && leftDone && throttleDone && brakeDone && reverseDone) return;

        if (!rightDone)
        {
            // Discovery agnóstico al modelo: scan de todos los axes contra su
            // baseline; gana el de mayor delta positivo. Independiente de si
            // el volante usa stick/x, rx, z, etc.
            if (_steerCandidates == null) SnapshotSteerCandidates();

            int bestIdx = -1;
            float bestPositiveDelta = 0f;
            if (_steerCandidates != null)
            {
                for (int i = 0; i < _steerCandidates.Length; i++)
                {
                    if (!SafeReadFloatRaw(_steerCandidates[i], out var sv)) continue;
                    float delta = sv - _steerCandidateBaselines[i];
                    if (delta > bestPositiveDelta) { bestPositiveDelta = delta; bestIdx = i; }
                }
            }

            float progress = Mathf.Clamp01(bestPositiveDelta);

            if (bestPositiveDelta >= WHEEL_THRESHOLD && bestIdx >= 0)
            {
                // Persistir el eje descubierto y reconstruir steerAction sobre él
                string chosenPath = _steerCandidatePaths[bestIdx];
                PlayerPrefs.SetString(UIInputNew.PREF_BIND_STEER_AXIS, chosenPath);
                _steerExcludedPedalIdx = -1;
                for (int i = 0; i < PEDAL_AXIS_CANDIDATES.Length; i++)
                    if (PEDAL_AXIS_CANDIDATES[i] == chosenPath) { _steerExcludedPedalIdx = i; break; }

                InputDevice dev = TryAttachToDevice();
                if (dev != null)
                {
                    if (steerAction != null) { steerAction.Disable(); steerAction.Dispose(); }
                    steerAction = new InputAction("MenuSteer", InputActionType.Value);
                    steerAction.AddBinding(dev.path + "/" + chosenPath);
                    steerAction.Enable();
                }
                // Capturar centro/extremos desde la baseline ya conocida —
                // el siguiente Update seguirá actualizando steerMaxSeen/MinSeen
                // a través del bloque genérico de tracking.
                steerCenter  = _steerCandidateBaselines[bestIdx];
                steerMaxSeen = SafeReadFloatRaw(_steerCandidates[bestIdx], out var smv) ? smv : steerCenter;
                steerMinSeen = steerCenter;
                steerCenterCaptured = true;

                rightDone = true;
                rightFillRT.anchorMax = new Vector2(1, 1);
                rightFill.color = MenuTheme.IndicatorDone;
                rightIndicator.color = MenuTheme.IndicatorDone;
                wheelPrompt.text = "Gira el volante hacia la IZQUIERDA";
                Debug.Log($"[MenuScreenManager] Eje del volante descubierto: '{chosenPath}'");
            }
            else if (Mathf.Abs(progress - rightFillRT.anchorMax.x) > 0.005f)
            {
                rightFillRT.anchorMax = new Vector2(progress, 1);
                rightFill.color = Color.Lerp(MenuTheme.SecondaryCrimson, MenuTheme.IndicatorDone, progress);
            }
            return;
        }

        if (!leftDone)
        {
            float leftProgress = Mathf.Clamp01(-steer);

            if (-steer >= WHEEL_THRESHOLD)
            {
                leftDone = true;
                leftFillRT.anchorMin = new Vector2(0, 0);
                leftFill.color = MenuTheme.IndicatorDone;
                leftIndicator.color = MenuTheme.IndicatorDone;

                // Persistir calibración del steering (se usó el rango físico completo)
                PlayerPrefs.SetFloat("G923_SteerCenter", steerCenter);
                PlayerPrefs.SetFloat("G923_SteerMax", steerMaxSeen);
                PlayerPrefs.SetFloat("G923_SteerMin", steerMinSeen);
                // Huella del dispositivo al que pertenece esta calibración —
                // si en una sesión futura se conecta otro modelo, la huella
                // diferirá y la calibración se invalidará automáticamente.
                string fp = ComputeDeviceFingerprint(TryAttachToDevice());
                if (!string.IsNullOrEmpty(fp)) PlayerPrefs.SetString(PREF_CAL_FINGERPRINT, fp);
                Debug.Log($"[MenuScreenManager] Steering calibrado: center={steerCenter:F3} max={steerMaxSeen:F3} min={steerMinSeen:F3} fp={fp}");

                // Sin pedales conectados o ya calibrado previamente → saltar
                // a la fase de reversa (o cargar escena si reverseDone).
                if (gasCtrl == null || brakeCtrl == null || (throttleDone && brakeDone))
                {
                    throttleDone = true;
                    brakeDone = true;
                    if (reverseDone)
                    {
                        PlayerPrefs.Save();
                        wheelPrompt.text = "Cargando prueba...";
                        skipButton.gameObject.SetActive(false);
                        StartCoroutine(LoadSceneDelayed(1.5f));
                    }
                    else
                    {
                        SnapshotReverseBaseline();
                        wheelPrompt.text = "Activa la REVERSA";
                    }
                }
                else
                {
                    // Capturar reposo de los ejes JUSTO antes de fase 3 —
                    // el usuario acaba de soltar el volante y no está pisando nada.
                    SnapshotPedalRests();
                    wheelPrompt.text = "Pisa el ACELERADOR a fondo";
                }
            }
            else if (Mathf.Abs(leftProgress - (1 - leftFillRT.anchorMin.x)) > 0.005f)
            {
                leftFillRT.anchorMin = new Vector2(1 - leftProgress, 0);
                leftFill.color = Color.Lerp(MenuTheme.SecondaryCrimson, MenuTheme.IndicatorDone, leftProgress);
            }
            return;
        }

        // ── Fase 3: calibración dinámica del ACELERADOR (barra gasFill) ──
        // Samplea la lista unificada de candidatos y gana el que tenga mayor
        // |delta| desde su reposo (capturado al inicio de la fase).
        if (!throttleDone)
        {
            #region HORI HPC-044U gas phase auto-pass — DO NOT MODIFY (ver HORI_THROTTLE_BUG_RESOLUTION.md, PR #127)
            // ⚠️ CRITICAL — Sin este auto-pass, Pantalla 2 Discovery se atora
            //   en Phase 3 con el HORI Truck Control System: el throttle del
            //   HORI vive en un byte huérfano del HID report (Unity HID parser
            //   bug por sliders duplicados Usage 0x36) y NINGÚN candidate de
            //   PEDAL_AXIS_CANDIDATES detecta el delta cuando el usuario pisa
            //   el acelerador. Sin auto-pass el operador queda bloqueado en la
            //   pantalla de calibración SIN poder avanzar al examen.
            // El throttle se lee en runtime via HoriThrottleReader (P/Invoke
            // directo al HID device). UIInputNew.AttachToWheelDevice setea el
            // sentinel también — esto es REDUNDANCIA defensiva por si Pantalla 2
            // corre antes que AttachToWheelDevice (orden de bootstrap).
            // NO eliminar.
            //
            // HORI Truck workaround: el byte del throttle (HID 21-22) quedó
            // huérfano en el HID parser de Unity (no hay AxisControl que lo
            // lea — verificado raw HID dump 2026-05-03). Auto-pasamos esta
            // fase y dejamos que HoriThrottleReader.cs lea el byte directo
            // via InputSystem.onEvent intercept. Sentinel HORI_RAW_GAS_PATH.
            var devForGas = TryAttachToDevice();
            if (devForGas != null && UIInputNew.IsHORITruck(devForGas))
            {
                throttleDone = true;
                gasFillRT.anchorMax = new Vector2(1, 1);
                gasFill.color = MenuTheme.IndicatorDone;
                gasIndicator.color = MenuTheme.IndicatorDone;
                PlayerPrefs.SetString("G923_GasAxis", UIInputNew.HORI_RAW_GAS_PATH);
                PlayerPrefs.SetFloat("G923_GasRest", 0f);
                PlayerPrefs.SetFloat("G923_GasPress", 1f);
                Debug.Log($"[MenuScreenManager] HORI Truck — gas phase auto-pasada, throttle vía HoriThrottleReader (sentinel '{UIInputNew.HORI_RAW_GAS_PATH}')");
                wheelPrompt.text = "Pisa el FRENO a fondo";
                return;
            }
            #endregion
            int bestIdx = SamplePedalCandidates(-1, out float bestAbsDelta);
            float gasProgress = Mathf.Clamp01(bestAbsDelta);

            if (bestAbsDelta >= PEDAL_DELTA_THRESHOLD && bestIdx >= 0)
            {
                throttleDone = true;
                gasChosenIdx = bestIdx;
                float rest = pedalCandidateRests[bestIdx];
                float press = rest + pedalCandidateMaxDeltas[bestIdx]; // preserva signo
                string gasAxisPath = PEDAL_AXIS_CANDIDATES[bestIdx];

                gasFillRT.anchorMax = new Vector2(1, 1);
                gasFill.color = MenuTheme.IndicatorDone;
                gasIndicator.color = MenuTheme.IndicatorDone;

                PlayerPrefs.SetString("G923_GasAxis", gasAxisPath);
                PlayerPrefs.SetFloat("G923_GasRest", rest);
                PlayerPrefs.SetFloat("G923_GasPress", press);
                Debug.Log($"[MenuScreenManager] Acelerador → eje '{gasAxisPath}' rest={rest:F3} press={press:F3}");

                wheelPrompt.text = "Pisa el FRENO a fondo";
            }
            else if (Mathf.Abs(gasProgress - gasFillRT.anchorMax.x) > 0.005f)
            {
                gasFillRT.anchorMax = new Vector2(gasProgress, 1);
                gasFill.color = Color.Lerp(MenuTheme.SecondaryCrimson, MenuTheme.IndicatorDone, gasProgress);
            }
            return;
        }

        // ── Fase 4: calibración dinámica del FRENO (barra brakeFill) ──
        // Excluye el eje que ya se eligió para acelerador.
        if (!brakeDone)
        {
            int bestIdx = SamplePedalCandidates(gasChosenIdx, out float bestAbsDelta);
            float brakeProgress = Mathf.Clamp01(bestAbsDelta);

            if (bestAbsDelta >= PEDAL_DELTA_THRESHOLD && bestIdx >= 0)
            {
                brakeDone = true;
                float rest = pedalCandidateRests[bestIdx];
                float press = rest + pedalCandidateMaxDeltas[bestIdx];
                string brakeAxisPath = PEDAL_AXIS_CANDIDATES[bestIdx];

                brakeFillRT.anchorMax = new Vector2(1, 1);
                brakeFill.color = MenuTheme.IndicatorDone;
                brakeIndicator.color = MenuTheme.IndicatorDone;

                PlayerPrefs.SetString("G923_BrakeAxis", brakeAxisPath);
                PlayerPrefs.SetFloat("G923_BrakeRest", rest);
                PlayerPrefs.SetFloat("G923_BrakePress", press);
                // Reescribir la huella aquí también garantiza que un Partial
                // que solo redescubrió pedales quede asociado al device actual.
                string fpBrake = ComputeDeviceFingerprint(TryAttachToDevice());
                if (!string.IsNullOrEmpty(fpBrake)) PlayerPrefs.SetString(PREF_CAL_FINGERPRINT, fpBrake);
                PlayerPrefs.Save();
                Debug.Log($"[MenuScreenManager] Freno → eje '{brakeAxisPath}' rest={rest:F3} press={press:F3} fp={fpBrake}");

                if (reverseDone)
                {
                    wheelPrompt.text = "Cargando prueba...";
                    skipButton.gameObject.SetActive(false);
                    StartCoroutine(LoadSceneDelayed(1.5f));
                }
                else
                {
                    SnapshotReverseBaseline();
                    wheelPrompt.text = "Activa la REVERSA";
                }
            }
            else if (Mathf.Abs(brakeProgress - brakeFillRT.anchorMax.x) > 0.005f)
            {
                brakeFillRT.anchorMax = new Vector2(brakeProgress, 1);
                brakeFill.color = Color.Lerp(MenuTheme.SecondaryCrimson, MenuTheme.IndicatorDone, brakeProgress);
            }
            return;
        }

        // ── Fase 5: descubrimiento de REVERSA ──
        // Detección dual: (1) primer botón presionado este frame que NO esté
        // ya bindeado a otra acción (drive/paddle/etc.), o (2) un eje discreto
        // (H-shifter) con delta grande respecto a su baseline. El path elegido
        // se persiste como Bind_reverse para que UIInputNew lo lea.
        if (!reverseDone)
        {
            string chosenReverse = TryDetectReverseInput(out bool isAxis);
            if (!string.IsNullOrEmpty(chosenReverse))
            {
                reverseDone = true;
                reverseFillRT.anchorMax = new Vector2(1, 1);
                reverseFill.color = MenuTheme.IndicatorDone;
                reverseIndicator.color = MenuTheme.IndicatorDone;

                PlayerPrefs.SetString(UIInputNew.PREF_BIND_REVERSE, chosenReverse);
                PlayerPrefs.SetInt(PREF_CAL_REVERSE_DONE, 1);
                string fpRev = ComputeDeviceFingerprint(TryAttachToDevice());
                if (!string.IsNullOrEmpty(fpRev)) PlayerPrefs.SetString(PREF_CAL_FINGERPRINT, fpRev);
                PlayerPrefs.Save();
                Debug.Log($"[MenuScreenManager] Reversa → '{chosenReverse}' (axis={isAxis}) fp={fpRev}");

                wheelPrompt.text = "Cargando prueba...";
                skipButton.gameObject.SetActive(false);
                StartCoroutine(LoadSceneDelayed(1.5f));
            }
        }
    }

    void OnSkipWheel()
    {
        skipButton.interactable = false;
        wheelPrompt.text = "Cargando prueba...";
        var txt = skipButton.GetComponentInChildren<TextMeshProUGUI>();
        if (txt != null) txt.text = "Cargando...";
        StartCoroutine(LoadSceneDelayed(0.5f));
    }

    IEnumerator LoadSceneDelayed(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        LoadSelectedScene();
    }

    // Cachea los candidatos de eje de pedal (lista unificada).
    void CachePedalCandidates(InputDevice device)
    {
        pedalCandidates = new InputControl<float>[PEDAL_AXIS_CANDIDATES.Length];
        pedalCandidateRests = new float[PEDAL_AXIS_CANDIDATES.Length];
        pedalCandidateMaxDeltas = new float[PEDAL_AXIS_CANDIDATES.Length];
        for (int i = 0; i < PEDAL_AXIS_CANDIDATES.Length; i++)
        {
            pedalCandidates[i] = device.TryGetChildControl(PEDAL_AXIS_CANDIDATES[i]) as InputControl<float>;
            pedalCandidateRests[i] = 1f; // placeholder hasta snapshot
            pedalCandidateMaxDeltas[i] = 0f;
        }
    }

    // Captura el valor de reposo de cada candidato. Llamar justo antes de la
    // fase de pedal, cuando se asume que el usuario no está pisando nada.
    void SnapshotPedalRests()
    {
        if (pedalCandidates == null) return;
        for (int i = 0; i < pedalCandidates.Length; i++)
        {
            // FIX: usar SafeReadFloatRaw para validar dev.added y atrapar la
            // InvalidOperationException si el control es stale (ej. tras un
            // replug del USB sin que CachePedalCandidates haya re-corrido aun).
            // Sin esto, ReadUnprocessedValue() directo puede tirar excepcion
            // no manejada o persistir un valor garbage como rest, rompiendo
            // SamplePedalCandidates en la fase 3 del Discovery.
            if (SafeReadFloatRaw(pedalCandidates[i], out float rest))
                pedalCandidateRests[i] = rest;
            pedalCandidateMaxDeltas[i] = 0f;
        }
    }

    // Samplea todos los candidatos, actualiza el delta signado máximo por
    // eje (delta = raw - rest) y devuelve el índice con mayor |delta|.
    // excludeIdx se usa en la fase brake para no elegir el mismo eje del gas.
    // Además se excluye el eje del volante (_steerExcludedPedalIdx) si éste
    // coincide con uno de los candidatos de pedal.
    int SamplePedalCandidates(int excludeIdx, out float bestAbsDelta)
    {
        bestAbsDelta = 0f;
        int bestIdx = -1;
        if (pedalCandidates == null) return -1;
        for (int i = 0; i < pedalCandidates.Length; i++)
        {
            if (pedalCandidates[i] == null) continue;
            if (!SafeReadFloatRaw(pedalCandidates[i], out var v)) continue;
            float delta = v - pedalCandidateRests[i];
            if (Mathf.Abs(delta) > Mathf.Abs(pedalCandidateMaxDeltas[i]))
                pedalCandidateMaxDeltas[i] = delta; // preserva signo
            if (i == excludeIdx || i == _steerExcludedPedalIdx) continue;
            float absD = Mathf.Abs(pedalCandidateMaxDeltas[i]);
            if (absD > bestAbsDelta) { bestAbsDelta = absD; bestIdx = i; }
        }
        return bestIdx;
    }

    // Snapshot de baselines de todos los axes del device para descubrir el
    // eje del volante por delta máxima en fase 1. Asume que el usuario tiene
    // las manos quietas en el centro al entrar a la pantalla.
    void SnapshotSteerCandidates()
    {
        InputDevice dev = TryAttachToDevice();
        if (dev == null) { _steerCandidates = null; return; }

        var ctrls = new System.Collections.Generic.List<InputControl<float>>();
        var paths = new System.Collections.Generic.List<string>();
        string devPath = dev.path ?? "";
        foreach (var c in dev.allControls)
        {
            // Sólo axes float continuos — ButtonControl hereda de AxisControl
            // pero queremos descartarlos.
            if (!(c is AxisControl) || c is ButtonControl) continue;
            if (!(c is InputControl<float> fc)) continue;
            string p = c.path ?? "";
            if (!string.IsNullOrEmpty(devPath) && p.StartsWith(devPath + "/"))
                p = p.Substring(devPath.Length + 1);
            // Defense-in-depth: excluir paths conocidos como pedales. Sin esto,
            // un pedal saturado en reposo (ej. rz=1.0 sin pisar) podía ganar
            // la fase 1 (DERECHA) por delta, dejando Bind_steerAxis apuntando
            // a un freno. Verificado en logs S3 (FIX#20).
            bool isPedalPath = false;
            for (int j = 0; j < PEDAL_AXIS_CANDIDATES.Length; j++)
                if (PEDAL_AXIS_CANDIDATES[j] == p) { isPedalPath = true; break; }
            if (isPedalPath) continue;
            ctrls.Add(fc);
            paths.Add(p);
        }
        _steerCandidates = ctrls.ToArray();
        _steerCandidatePaths = paths.ToArray();
        _steerCandidateBaselines = new float[_steerCandidates.Length];
        for (int i = 0; i < _steerCandidates.Length; i++)
            _steerCandidateBaselines[i] = _steerCandidates[i].ReadUnprocessedValue();
    }

    void SnapshotReverseBaseline()
    {
        InputDevice wheel = TryAttachToDevice();
        if (wheel == null) { _reverseAxisBaseline = null; return; }

        _reverseAxisBaseline = new System.Collections.Generic.Dictionary<string, float>();
        // Si no hay SHIFTER conectado, omitimos el prefijo "wheel:" — preservamos
        // el formato legacy (compat con calibraciones G923 que persisten paths
        // sin prefijo, y con scripts externos que lean PlayerPrefs). Solo
        // emitimos prefijos cuando realmente hay ambigüedad multi-device.
        InputDevice shifter = null;
        foreach (var d in InputSystem.devices)
        {
            if (d == wheel) continue;
            if (UIInputNew.IsShifterDevice(d)) { shifter = d; break; }
        }
        string wheelPrefix = shifter != null ? "wheel:" : "";

        SnapshotBaselineForDevice(wheel, wheelPrefix);
        if (shifter != null)
            SnapshotBaselineForDevice(shifter, "shifter:");

        // No reasignar reversa al volante, gas o freno. Excluir tanto el path
        // crudo como el path con prefijo "wheel:" — los pedales y steering
        // solo viven en el wheel, así que no exponemos variantes de shifter.
        _reverseExcludedPaths = new System.Collections.Generic.HashSet<string>();
        string steerP = PlayerPrefs.GetString(UIInputNew.PREF_BIND_STEER_AXIS, "");
        string gasP   = PlayerPrefs.GetString("G923_GasAxis", "");
        string brakeP = PlayerPrefs.GetString("G923_BrakeAxis", "");
        AddExclusion(_reverseExcludedPaths, steerP);
        AddExclusion(_reverseExcludedPaths, gasP);
        AddExclusion(_reverseExcludedPaths, brakeP);
    }

    void SnapshotBaselineForDevice(InputDevice dev, string prefix)
    {
        if (dev == null) return;
        string devPath = dev.path ?? "";
        foreach (var c in dev.allControls)
        {
            if (!(c is AxisControl) || c is ButtonControl) continue;
            if (!(c is InputControl<float> fc)) continue;
            string p = c.path ?? "";
            if (!string.IsNullOrEmpty(devPath) && p.StartsWith(devPath + "/"))
                p = p.Substring(devPath.Length + 1);
            _reverseAxisBaseline[prefix + p] = fc.ReadUnprocessedValue();
        }
    }

    static void AddExclusion(System.Collections.Generic.HashSet<string> set, string path)
    {
        if (set == null || string.IsNullOrEmpty(path)) return;
        // Cubrir variantes: el binding persistido puede venir con o sin prefijo
        // ("wheel:stick/x" vs "stick/x"). La detección compara ambas formas.
        string stripped = UIInputNew.StripDevicePrefix(path);
        set.Add(path);
        if (stripped != path) set.Add(stripped);
        else set.Add("wheel:" + stripped);
    }

    // Devuelve el path (relativo al device) del control que activa reversa, o
    // string.Empty si nada se ha activado todavía. Detecta primero botones y,
    // si nada presionado, mira axes con delta grande (H-shifter).
    string TryDetectReverseInput(out bool isAxis)
    {
        isAxis = false;
        InputDevice wheel = TryAttachToDevice();
        if (wheel == null) return "";

        // Tiebreaker: el SHIFTER tiene prioridad sobre el wheel para reverse.
        // Si el HORI Truck firma button5 en shifter y simultáneamente algo en el
        // wheel, queremos el shifter. Iteramos shifter primero.
        // Si no hay shifter, el wheel persiste paths sin prefijo (formato legacy).
        var devices = new System.Collections.Generic.List<(InputDevice dev, string prefix)>();
        InputDevice shifterDev = null;
        foreach (var d in InputSystem.devices)
        {
            if (d == wheel) continue;
            if (UIInputNew.IsShifterDevice(d)) { shifterDev = d; break; }
        }
        if (shifterDev != null) devices.Add((shifterDev, "shifter:"));
        devices.Add((wheel, shifterDev != null ? "wheel:" : ""));

        // 1) Botón recién presionado este frame (en cualquier device)
        foreach (var entry in devices)
        {
            string r = TryDetectReverseButton(entry.dev, entry.prefix);
            if (!string.IsNullOrEmpty(r)) return r;
        }

        // 2) Eje discreto con delta grande contra baseline (palanca H, en cualquier device)
        if (_reverseAxisBaseline != null)
        {
            foreach (var entry in devices)
            {
                string r = TryDetectReverseAxis(entry.dev, entry.prefix);
                if (!string.IsNullOrEmpty(r))
                {
                    isAxis = true;
                    return r;
                }
            }
        }

        return "";
    }

    string TryDetectReverseButton(InputDevice dev, string prefix)
    {
        if (dev == null) return "";
        string devPath = dev.path ?? "";
        foreach (var c in dev.allControls)
        {
            if (!(c is ButtonControl btn)) continue;
            if (!btn.wasPressedThisFrame) continue;
            string p = c.path ?? "";
            if (!string.IsNullOrEmpty(devPath) && p.StartsWith(devPath + "/"))
                p = p.Substring(devPath.Length + 1);
            string prefixed = prefix + p;
            if (_reverseExcludedPaths != null
                && (_reverseExcludedPaths.Contains(p) || _reverseExcludedPaths.Contains(prefixed))) continue;
            return prefixed;
        }
        return "";
    }

    string TryDetectReverseAxis(InputDevice dev, string prefix)
    {
        if (dev == null) return "";
        string devPath = dev.path ?? "";
        foreach (var c in dev.allControls)
        {
            if (!(c is AxisControl) || c is ButtonControl) continue;
            if (!(c is InputControl<float> fc)) continue;
            string p = c.path ?? "";
            if (!string.IsNullOrEmpty(devPath) && p.StartsWith(devPath + "/"))
                p = p.Substring(devPath.Length + 1);
            string prefixed = prefix + p;
            if (_reverseExcludedPaths != null
                && (_reverseExcludedPaths.Contains(p) || _reverseExcludedPaths.Contains(prefixed))) continue;
            if (!_reverseAxisBaseline.TryGetValue(prefixed, out float baseline)) continue;
            if (!SafeReadFloatRaw(fc, out var rxv)) continue;
            if (Mathf.Abs(rxv - baseline) >= REVERSE_AXIS_DETECT_DELTA)
                return prefixed;
        }
        return "";
    }

    float ReadSteerInput()
    {
        if (steerAction == null) TryAttachToDevice();
        if (steerAction == null) return 0f;
        var ctrls = steerAction.controls;
        if (ctrls.Count > 0 && ctrls[0] is InputControl<float> fc)
            return SafeReadFloatRaw(fc, out var v) ? v : 0f;
        return steerAction.ReadValue<float>();
    }

    // Detecta y conecta el dispositivo de volante una sola vez. Idempotente:
    // si ya hay steerAction, retorna el device asociado sin re-atar. El path
    // del eje del volante se lee de Bind_steerAxis (configurable vía F8) — por
    // defecto "stick/x" pero el operador puede reasignar a otro modelo.
    InputDevice TryAttachToDevice()
    {
        if (steerAction != null)
        {
            var ctrls = steerAction.controls;
            return ctrls.Count > 0 ? ctrls[0].device : null;
        }

        string steerSubpath = PlayerPrefs.GetString(UIInputNew.PREF_BIND_STEER_AXIS, UIInputNew.DEFAULT_BIND_STEER_AXIS);

        // El path del binding puede venir con prefijo "wheel:" o "shifter:" —
        // el AttachToDevice lo concatena al path del device, así que necesita
        // strip si existe para no construir "<HID>/wheel:button5" inválido.
        string steerSubpathRaw = UIInputNew.StripDevicePrefix(steerSubpath);

        // 1) Buscar wheel candidato por nombre (G923, Logitech, HORI, genéricos).
        //    Excluimos SHIFTER explícitamente — un device-shifter solo-botones no
        //    es un wheel y no debe adoptarse como tal en el menú.
        foreach (var device in InputSystem.devices)
        {
            if (UIInputNew.IsShifterDevice(device)) continue;
            if (UIInputNew.IsKnownWheelCandidate(device))
            {
                AttachToDevice(device, steerSubpathRaw, $"Volante detectado: {device.displayName}");
                return device;
            }
        }

        // 2) Fallback estable: el primer Joystick con axes >= 3 (heurística de
        //    wheel) que no sea un SHIFTER. NO usamos Joystick.current — con
        //    HORI hay dos HIDs y "último activo" suele ser el shifter.
        foreach (var device in InputSystem.devices)
        {
            if (!(device is Joystick)) continue;
            if (UIInputNew.IsShifterDevice(device)) continue;
            int axisCount = 0;
            foreach (var c in device.allControls)
                if (c is AxisControl) axisCount++;
            if (axisCount < 3) continue;
            AttachToDevice(device, steerSubpathRaw, $"Joystick fallback: {device.displayName}");
            return device;
        }

        // 3) Fallback final: Gamepad (el path del volante no aplica aquí, usa leftStick)
        if (Gamepad.current != null)
        {
            var gp = Gamepad.current;
            steerAction = new InputAction("MenuSteer", InputActionType.Value);
            steerAction.AddBinding("<Gamepad>/leftStick/x");
            steerAction.Enable();
            hatUp    = gp.TryGetChildControl("dpad/up")    as InputControl<float>;
            hatDown  = gp.TryGetChildControl("dpad/down")  as InputControl<float>;
            hatLeft  = gp.TryGetChildControl("dpad/left")  as InputControl<float>;
            hatRight = gp.TryGetChildControl("dpad/right") as InputControl<float>;
            circleBtn = gp.TryGetChildControl("buttonEast") as InputControl<float>;
            enterBtn  = gp.TryGetChildControl("start")      as InputControl<float>;
            attachedDevice = gp; // ref explícita para OnDeviceChange
            return gp;
        }
        return null;
    }

    void AttachToDevice(InputDevice device, string steerSubpath, string logPrefix)
    {
        steerAction = new InputAction("MenuSteer", InputActionType.Value);
        steerAction.AddBinding(device.path + "/" + steerSubpath);
        steerAction.Enable();
        hatUp    = device.TryGetChildControl("hat/up")    as InputControl<float>;
        hatDown  = device.TryGetChildControl("hat/down")  as InputControl<float>;
        hatLeft  = device.TryGetChildControl("hat/left")  as InputControl<float>;
        hatRight = device.TryGetChildControl("hat/right") as InputControl<float>;
        circleBtn = device.TryGetChildControl("button3")  as InputControl<float>;
        enterBtn  = device.TryGetChildControl("button10") as InputControl<float>;
        gasCtrl   = device.TryGetChildControl("z")  as InputControl<float>;
        brakeCtrl = device.TryGetChildControl("rz") as InputControl<float>;
        attachedDevice = device; // ref explícita para OnDeviceChange
        CachePedalCandidates(device);
        Debug.Log($"[MenuScreenManager] {logPrefix} (eje steer={steerSubpath}) hat={hatUp != null} pedals={gasCtrl != null && brakeCtrl != null}");
    }

    // Identidad estable del dispositivo: product + manufacturer + serial.
    // Si cambia, la calibración guardada se invalida (axis pueden ser otros).
    // Para volantes que reportan wheel + shifter como devices separados (HORI),
    // concatenamos las huellas con "||" para invalidar también si solo el
    // shifter cambia (el conjunto ya no es el mismo aunque el wheel sea idéntico).
    string ComputeDeviceFingerprint(InputDevice d)
    {
        if (d == null) return "";
        var desc = d.description;
        string wheelFp = $"{desc.product ?? ""}|{desc.manufacturer ?? ""}|{desc.serial ?? ""}";

        // Buscar shifter conectado y, si existe, anexarlo al fingerprint.
        string shifterFp = "";
        foreach (var dev in InputSystem.devices)
        {
            if (dev == d) continue;
            if (UIInputNew.IsShifterDevice(dev))
            {
                var sd = dev.description;
                shifterFp = $"{sd.product ?? ""}|{sd.manufacturer ?? ""}|{sd.serial ?? ""}";
                break;
            }
        }
        return string.IsNullOrEmpty(shifterFp) ? wheelFp : $"{wheelFp}||{shifterFp}";
    }

    void ClearWheelCalibration()
    {
        PlayerPrefs.DeleteKey("G923_SteerCenter");
        PlayerPrefs.DeleteKey("G923_SteerMax");
        PlayerPrefs.DeleteKey("G923_SteerMin");
        PlayerPrefs.DeleteKey("G923_GasAxis");
        PlayerPrefs.DeleteKey("G923_GasRest");
        PlayerPrefs.DeleteKey("G923_GasPress");
        PlayerPrefs.DeleteKey("G923_BrakeAxis");
        PlayerPrefs.DeleteKey("G923_BrakeRest");
        PlayerPrefs.DeleteKey("G923_BrakePress");
        PlayerPrefs.DeleteKey(UIInputNew.PREF_G923_CLUTCH_AXIS);
        PlayerPrefs.DeleteKey(UIInputNew.PREF_G923_CLUTCH_REST);
        PlayerPrefs.DeleteKey(UIInputNew.PREF_G923_CLUTCH_PRESS);
        PlayerPrefs.DeleteKey(PREF_CAL_FINGERPRINT);
        PlayerPrefs.DeleteKey(PREF_CAL_REVERSE_DONE);
        // El path del eje del volante y de reversa también se redescubren — si
        // los conservamos, un wheel nuevo intentaría leer del path viejo.
        PlayerPrefs.DeleteKey(UIInputNew.PREF_BIND_STEER_AXIS);
        PlayerPrefs.DeleteKey(UIInputNew.PREF_BIND_REVERSE);
        PlayerPrefs.Save();
    }

    void OnReassignControls()
    {
        Debug.Log("[MenuScreenManager] Reasignar controles solicitado por usuario");
        if (sanityCheckCo != null) { StopCoroutine(sanityCheckCo); sanityCheckCo = null; }
        ClearWheelCalibration();
        // Soltar steerAction para que TryAttachToDevice() vuelva a bindear
        // con el (ahora vacío/default) Bind_steerAxis. Sin esto, la pantalla
        // seguiría leyendo del eje viejo durante el discovery.
        if (steerAction != null) { steerAction.Disable(); steerAction.Dispose(); steerAction = null; }
        _steerExcludedPedalIdx = -1;
        PrepareWheelScreen();
    }

    // Splash de "Preparando prueba..." en estado Verified. Verifica que los
    // axes calibrados sigan existiendo y no estén pisados (axis stuck por
    // cable suelto, mapeo distinto). Si algo falla, degrada a discovery.
    IEnumerator SanityCheckThenLoad(float duration)
    {
        InputDevice dev = TryAttachToDevice();
        string gasPath = PlayerPrefs.GetString("G923_GasAxis", "");
        string brakePath = PlayerPrefs.GetString("G923_BrakeAxis", "");
        InputControl<float> gasC = (dev != null && !string.IsNullOrEmpty(gasPath))
            ? dev.TryGetChildControl(gasPath) as InputControl<float> : null;
        InputControl<float> brakeC = (dev != null && !string.IsNullOrEmpty(brakePath))
            ? dev.TryGetChildControl(brakePath) as InputControl<float> : null;

        // Si el axis previamente calibrado ya no existe en el device, la
        // calibración no sirve — degradar a discovery.
        if (dev != null && (gasC == null || brakeC == null))
        {
            Debug.Log("[MenuScreenManager] Sanity check: axis previamente calibrado no existe en el device → discovery");
            ClearWheelCalibration();
            PrepareWheelScreen();
            yield break;
        }

        // Clutch: solo valido en modo manual y si hay path guardado (G923 PS).
        // En Xbox y modo Auto el clutch no se evalúa — el axis no debería ser
        // requisito para arrancar.
        bool needClutch = PlayerPrefs.GetInt("TransmisionManual", 0) == 1
                       && PlayerPrefs.HasKey(UIInputNew.PREF_G923_CLUTCH_AXIS);
        string clutchPath = PlayerPrefs.GetString(UIInputNew.PREF_G923_CLUTCH_AXIS, "");
        InputControl<float> clutchC = (needClutch && dev != null && !string.IsNullOrEmpty(clutchPath))
            ? dev.TryGetChildControl(clutchPath) as InputControl<float> : null;
        if (needClutch && dev != null && clutchC == null)
        {
            Debug.Log("[MenuScreenManager] Sanity check: axis de clutch calibrado no existe en el device → limpiar prefs de clutch (modo manual seguirá sin desacople)");
            PlayerPrefs.DeleteKey(UIInputNew.PREF_G923_CLUTCH_AXIS);
            PlayerPrefs.DeleteKey(UIInputNew.PREF_G923_CLUTCH_REST);
            PlayerPrefs.DeleteKey(UIInputNew.PREF_G923_CLUTCH_PRESS);
            PlayerPrefs.Save();
            // No degradamos a discovery por esto — el manual sin clutch funciona
            // (modo didáctico). El operador puede recalibrar via Pantalla 2.
        }

        float gasRest = PlayerPrefs.GetFloat("G923_GasRest", 0f);
        float brakeRest = PlayerPrefs.GetFloat("G923_BrakeRest", 0f);
        float clutchRest = PlayerPrefs.GetFloat(UIInputNew.PREF_G923_CLUTCH_REST, -1f);

        bool degraded = false;
        float t = 0f;
        while (t < duration)
        {
            // Durante el splash, el usuario no debería estar pisando nada.
            // Un delta grande contra el rest guardado indica hardware
            // desconectado o axis distinto al que se calibró.
            if (SafeReadFloatRaw(gasC, out var gv) && Mathf.Abs(gv - gasRest) > 0.5f) { degraded = true; break; }
            if (SafeReadFloatRaw(brakeC, out var bv) && Mathf.Abs(bv - brakeRest) > 0.5f) { degraded = true; break; }
            if (clutchC != null && SafeReadFloatRaw(clutchC, out var cv) && Mathf.Abs(cv - clutchRest) > 0.5f) { degraded = true; break; }
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (degraded)
        {
            Debug.Log("[MenuScreenManager] Sanity check: pedal con valor anómalo respecto al rest guardado → discovery");
            ClearWheelCalibration();
            PrepareWheelScreen();
            yield break;
        }

        sanityCheckCo = null;
        LoadSelectedScene();
    }

    // ════════════════════════════════════════════════════════════════════
    // D-PAD PANTALLA 1 — OPCIONES (foco + selección)
    // ════════════════════════════════════════════════════════════════════

    void UpdateOptionsDpad()
    {
        if (hatUp == null) ReadSteerInput();
        if (hatUp == null) return;

        float dt = Time.unscaledDeltaTime;
        bool up    = DpadRepeat(hatUp,    ref dpadUpT,    ref dpadUpR,    dt);
        bool down  = DpadRepeat(hatDown,  ref dpadDownT,  ref dpadDownR,  dt);
        bool left  = DpadRepeat(hatLeft,  ref dpadLeftT,  ref dpadLeftR,  dt);
        bool right = DpadRepeat(hatRight, ref dpadRightT, ref dpadRightR, dt);

        // Circle o Enter → confirmar selección o activar Continuar.
        // Filas:
        //   0 = modelo (Sedan/SUV)
        //   1 = transmisión (Auto/Manual)
        //   2 = botón Continuar
        bool confirmPressed = (SafeReadFloat(circleBtn, out var ccv) && ccv > 0.5f) ||
                              (SafeReadFloat(enterBtn,  out var cev) && cev > 0.5f);
        if (confirmPressed)
        {
            if (!confirmBtnHeld)
            {
                confirmBtnHeld = true;
                if (screen1Row == 0) OnVariantSelected(screen1Col);
                else if (screen1Row == 1) OnTransmisionSelected(screen1Col);
                else if (screen1Row == 2) OnContinueScreen1();
                RefreshScreen1Visuals();
            }
        }
        else confirmBtnHeld = false;

        if (!up && !down && !left && !right) return;

        // Bug pre-existente arreglado: con 2 cards reales (índices 0 y 1) maxCol
        // debe ser 1, no 2 (permitía navegar a una col fantasma).
        int maxCol = (screen1Row == 0 || screen1Row == 1) ? 1 : 0;

        if (up && screen1Row > 0)
        {
            screen1Row--;
            maxCol = (screen1Row == 0 || screen1Row == 1) ? 1 : 0;
            screen1Col = Mathf.Min(screen1Col, maxCol);
        }
        else if (down && screen1Row < 2)
        {
            screen1Row++;
            maxCol = (screen1Row == 0 || screen1Row == 1) ? 1 : 0;
            screen1Col = Mathf.Min(screen1Col, maxCol);
        }
        else if (left && screen1Col > 0) screen1Col--;
        else if (right && screen1Col < maxCol) screen1Col++;

        RefreshScreen1Visuals();
    }

    void RefreshScreen1Visuals()
    {
        // Modelo cards (fila 0): foco=Gold, selección=Purple, ambos=Gold+fondo morado
        for (int i = 0; i < variantCards.Length; i++)
        {
            bool focused = (screen1Row == 0 && screen1Col == i);
            bool selected = (i == selectedVariantIndex);
            variantBorders[i].color = focused ? MenuTheme.Gold :
                (selected ? MenuTheme.CardBorderGold : MenuTheme.CardBorder);
            variantCards[i].transform.Find("Background").GetComponent<Image>().color =
                selected ? MenuTheme.CardSelected : MenuTheme.CardBackground;
        }

        // Transmisión cards (fila 1): mismo patrón visual
        if (transmisionCards != null)
        {
            for (int i = 0; i < transmisionCards.Length; i++)
            {
                bool focused = (screen1Row == 1 && screen1Col == i);
                bool selected = (i == selectedTransmisionIndex);
                transmisionBorders[i].color = focused ? MenuTheme.Gold :
                    (selected ? MenuTheme.CardBorderGold : MenuTheme.CardBorder);
                transmisionCards[i].transform.Find("Background").GetComponent<Image>().color =
                    selected ? MenuTheme.CardSelected : MenuTheme.CardBackground;
            }
        }

        // Continuar button (fila 2)
        if (continueBtn1 != null)
        {
            Image img = continueBtn1.GetComponent<Image>();
            TextMeshProUGUI txt = continueBtn1.GetComponentInChildren<TextMeshProUGUI>();
            if (screen1Row == 2)
            {
                img.color = MenuTheme.Gold;
                if (txt != null) txt.color = MenuTheme.TextPrimary;
            }
            else
            {
                img.color = MenuTheme.ButtonPrimary;
                if (txt != null) txt.color = MenuTheme.ButtonPrimaryText;
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // D-PAD PANTALLA PRÁCTICA
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Navegación d-pad para la pantalla de configuración de práctica.
    /// Filas: 0=vehículo, 1=clima, 2=escenario, 3=transmisión (skip si no aplica), 4=continuar.
    /// El orden coincide con el layout visual (transmisión es la última porque puede ocultarse).
    /// </summary>
    void UpdatePracticeDpad()
    {
        if (hatUp == null) ReadSteerInput();
        if (hatUp == null) return;

        float dt = Time.unscaledDeltaTime;
        bool up    = DpadRepeat(hatUp,    ref dpadUpT,    ref dpadUpR,    dt);
        bool down  = DpadRepeat(hatDown,  ref dpadDownT,  ref dpadDownR,  dt);
        bool left  = DpadRepeat(hatLeft,  ref dpadLeftT,  ref dpadLeftR,  dt);
        bool right = DpadRepeat(hatRight, ref dpadRightT, ref dpadRightR, dt);

        bool transmisionVisible = (selectedPracticeVehicleIdx == 0 || selectedPracticeVehicleIdx == 1);

        bool confirmPressed = (SafeReadFloat(circleBtn, out var ccv) && ccv > 0.5f) ||
                              (SafeReadFloat(enterBtn,  out var cev) && cev > 0.5f);
        if (confirmPressed)
        {
            if (!confirmBtnHeld)
            {
                confirmBtnHeld = true;
                if (practiceRow == 0) OnPracticeVehicleSelected(practiceCol);
                else if (practiceRow == 1) OnPracticeWeatherSelected(practiceCol);
                else if (practiceRow == 2 && transmisionVisible) OnPracticeTransmisionSelected(practiceCol);
                else if (practiceRow == 3) OnContinuePractice();
                RefreshPracticeVisuals();
            }
        }
        else confirmBtnHeld = false;

        if (!up && !down && !left && !right) return;

        int maxCol = PracticeMaxColForRow(practiceRow, transmisionVisible);

        if (up && practiceRow > 0)
        {
            practiceRow--;
            // Saltar fila transmisión cuando no aplica (vehículo no compatible).
            if (practiceRow == 2 && !transmisionVisible) practiceRow = 1;
            maxCol = PracticeMaxColForRow(practiceRow, transmisionVisible);
            practiceCol = Mathf.Min(practiceCol, maxCol);
        }
        else if (down && practiceRow < 3)
        {
            practiceRow++;
            if (practiceRow == 2 && !transmisionVisible) practiceRow = 3;
            maxCol = PracticeMaxColForRow(practiceRow, transmisionVisible);
            practiceCol = Mathf.Min(practiceCol, maxCol);
        }
        else if (left && practiceCol > 0) practiceCol--;
        else if (right && practiceCol < maxCol) practiceCol++;

        RefreshPracticeVisuals();
    }

    int PracticeMaxColForRow(int row, bool transmisionVisible)
    {
        // 0=vehículo, 1=clima, 2=transmisión (opcional), 3=continuar
        switch (row)
        {
            case 0: return practiceVehicles.Length - 1;        // 5 (6 opciones)
            case 1: return practiceWeatherLabels.Length - 1;   // 2 (3 opciones)
            case 2: return transmisionVisible ? 1 : 0;          // 1 si visible
            case 3: return 0;                                    // botón único
            default: return 0;
        }
    }

    void RefreshPracticeVisuals()
    {
        bool transmisionVisible = (selectedPracticeVehicleIdx == 0 || selectedPracticeVehicleIdx == 1);
        RefreshPracticeOptions(practiceVehicleBoxes, 0, selectedPracticeVehicleIdx);
        RefreshPracticeOptions(practiceWeatherBoxes, 1, selectedPracticeWeatherIdx);
        if (transmisionVisible)
            RefreshPracticeOptions(practiceTransmisionBoxes, 2, selectedPracticeTransmisionIdx);

        if (practiceContinueBtn != null)
        {
            Image img = practiceContinueBtn.GetComponent<Image>();
            TextMeshProUGUI txt = practiceContinueBtn.GetComponentInChildren<TextMeshProUGUI>();
            bool focused = (practiceRow == 3);
            if (img != null) img.color = focused ? MenuTheme.Gold : MenuTheme.ButtonPrimary;
            if (txt != null) txt.color = focused ? MenuTheme.TextPrimary : MenuTheme.ButtonPrimaryText;
        }
    }

    /// <summary>
    /// Repinta las cajas tipo checkbox aplicando estado de foco (d-pad) y selección.
    /// Foco: borde dorado. Selección: relleno morado. Ambos pueden coexistir.
    /// </summary>
    void RefreshPracticeOptions(Image[] boxes, int row, int selectedIdx)
    {
        if (boxes == null) return;
        for (int i = 0; i < boxes.Length; i++)
        {
            bool focused = (practiceRow == row && practiceCol == i);
            bool selected = (i == selectedIdx);
            boxes[i].color = selected
                ? MenuTheme.PrimaryPurple
                : (focused ? MenuTheme.Gold : MenuTheme.CardBorder);
            Transform inner = boxes[i].transform.Find("Inner");
            if (inner != null)
            {
                var innerImg = inner.GetComponent<Image>();
                if (innerImg != null) innerImg.color = selected ? MenuTheme.PrimaryPurple : Color.white;
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // D-PAD PIN INPUT
    // ════════════════════════════════════════════════════════════════════

    void UpdatePinDpad()
    {
        // Lazy init — reutiliza detección de ReadSteerInput
        if (hatUp == null) ReadSteerInput();
        if (hatUp == null) return;

        float dt = Time.unscaledDeltaTime;

        if (DpadRepeat(hatUp, ref dpadUpT, ref dpadUpR, dt))
            CycleDigit(+1);
        if (DpadRepeat(hatDown, ref dpadDownT, ref dpadDownR, dt))
            CycleDigit(-1);
        if (DpadRepeat(hatRight, ref dpadRightT, ref dpadRightR, dt))
            MoveCursor(+1);
        if (DpadRepeat(hatLeft, ref dpadLeftT, ref dpadLeftR, dt))
            MoveCursor(-1);

        // Circle o Enter → confirmar código
        bool confirmPin = (SafeReadFloat(circleBtn, out var pcv) && pcv > 0.5f) ||
                          (SafeReadFloat(enterBtn,  out var pev) && pev > 0.5f);
        if (confirmPin)
        {
            if (!confirmBtnHeld) { confirmBtnHeld = true; OnVerifyCode(); }
        }
        else confirmBtnHeld = false;
    }

    bool DpadRepeat(InputControl<float> ctrl, ref float timer, ref bool repeated, float dt)
    {
        // SafeReadFloat: si el device fue removido del system, devuelve false
        // sin spamear excepciones. Antes de FIX#19 esto crasheaba con
        // InvalidOperationException por frame (60/s). Sitio del crash original.
        if (!SafeReadFloat(ctrl, out var v)) { timer = 0f; repeated = false; return false; }
        if (v <= 0.5f) { timer = 0f; repeated = false; return false; }

        if (timer == 0f) { timer += dt; return true; } // primer press

        timer += dt;
        float threshold = repeated ? DPAD_RATE : DPAD_DELAY;
        if (timer >= threshold) { timer -= threshold; repeated = true; return true; }
        return false;
    }

    void CycleDigit(int direction)
    {
        if (pinDigits[pinCursor] < 0)
            pinDigits[pinCursor] = direction > 0 ? 0 : 9;
        else
            pinDigits[pinCursor] = (pinDigits[pinCursor] + direction + 10) % 10;

        SyncPinToField();
        RefreshPinVisuals();
    }

    void MoveCursor(int direction)
    {
        pinCursor = Mathf.Clamp(pinCursor + direction, 0, 5);
        RefreshPinVisuals();
    }

    void SyncPinToField()
    {
        if (codeInput == null) return;
        string text = "";
        for (int i = 0; i < 6; i++)
        {
            if (pinDigits[i] < 0) break;
            text += pinDigits[i].ToString();
        }
        codeInput.SetTextWithoutNotify(text);
    }

    void RefreshPinVisuals()
    {
        if (pinDigitTexts == null) return;
        for (int i = 0; i < 6; i++)
        {
            pinDigitTexts[i].text = pinDigits[i] >= 0 ? pinDigits[i].ToString() : "";

            if (i == pinCursor)
                pinBoxBorders[i].color = MenuTheme.Gold;
            else if (pinDigits[i] >= 0)
                pinBoxBorders[i].color = MenuTheme.PrimaryPurple;
            else
                pinBoxBorders[i].color = MenuTheme.InputBorder;
        }
    }

    void LoadSelectedScene()
    {
        if (string.IsNullOrEmpty(selectedSceneName))
        {
            Debug.LogError("[MenuScreenManager] No hay escena seleccionada");
            return;
        }
        if (Time.timeScale != 1f)
            Debug.LogWarning($"[MenuScreenManager] timeScale era {Time.timeScale} al cargar escena — reseteando a 1");
        Time.timeScale = 1f;
        Debug.Log($"[MenuScreenManager] Iniciando sesión y cargando: {selectedSceneName} | Tramite: {tramiteId}");
        StartCoroutine(StartSessionAndLoadScene());
    }

    IEnumerator StartSessionAndLoadScene()
    {
        // Descargar scoring config del backend (timeout 5s, usa cache si falla)
        if (ScoringConfig.Instance != null)
            yield return ScoringConfig.Instance.LoadFromBackend();

        // Iniciar sesión en el backend (fire-and-forget: si falla, seguimos)
        if (!string.IsNullOrEmpty(tramiteId))
        {
            yield return SimulatorApiClient.StartSession(tramiteId, (sid) =>
            {
                if (sid != null)
                {
                    GameManager.Instance.SessionId = sid;
                    Debug.Log($"[MenuScreenManager] Sesión backend: {sid}");
                }
                else
                {
                    Debug.LogWarning("[MenuScreenManager] No se pudo crear sesión en backend, continuando offline");
                }
            });
        }

        SceneManager.LoadScene(selectedSceneName);
    }

    void OnDestroy()
    {
        if (steerAction != null) { steerAction.Disable(); steerAction.Dispose(); }
        if (_deviceChangeHandler != null)
        {
            InputSystem.onDeviceChange -= _deviceChangeHandler;
            _deviceChangeHandler = null;
        }
    }

    void EnableButton(Button btn, bool enabled)
    {
        if (btn == null) return;
        btn.interactable = enabled;
        Image img = btn.GetComponent<Image>();
        TextMeshProUGUI txt = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (img != null) img.color = enabled ? MenuTheme.ButtonPrimary : MenuTheme.ButtonDisabled;
        if (txt != null) txt.color = enabled ? MenuTheme.ButtonPrimaryText : MenuTheme.ButtonDisabledText;
    }

    // ── JSON Models ────────────────────────────────────────────────────

    [System.Serializable]
    public class SessionResponse
    {
        public string sessionId;
        public string expiresAt;
        public string verifyUrl;
    }

    [System.Serializable]
    public class StatusResponse
    {
        public string status;
        public string tramiteId;
        public string citizenName;
        public string licenseType;
        public string verifiedAt;
    }

    [System.Serializable]
    public class LookupResponse
    {
        public string tramiteId;
        public string citizenName;
        public string licenseType;
        public string appointmentDate;
        public string appointmentTime;
    }
}
