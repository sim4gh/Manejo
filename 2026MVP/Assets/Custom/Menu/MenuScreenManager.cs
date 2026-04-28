using System.Collections;
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
    private Coroutine pollingCoroutine;

    // ── Escenas por licenseType ────────────────────────────────────────
    // Nota: "Camioneta" es el nombre real de la escena Unity, pero en UI
    // se muestra como "SUV" (más reconocible para el público de la prueba).
    private readonly string[] variantScenes = { "Sedan", "Camioneta" };

    // ── UI refs ────────────────────────────────────────────────────────
    private GameObject[] screens = new GameObject[4];
    private CanvasGroup[] screenGroups = new CanvasGroup[4];
    private int currentScreen = -1;

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
        "z", "rz", "stick/y", "stick/z", "ry", "rx"
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
    private Button continueBtn1;

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
        ShowScreen(0);
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
        // CachePedalCandidates fue llamado con el device viejo; al re-attach se
        // vuelve a llamar (ver AttachToDevice). No hace falta limpiarlo aquí
        // porque las fases de calibración usan el array recién cacheado.
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

        // Clima aleatorio
        PlayerPrefs.SetInt("Cargolluvia", Random.Range(0, 2));
        PlayerPrefs.Save();

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

        // 4 pantallas
        BuildScreen0_QR();
        BuildScreen1_Options();
        BuildScreen2_Wheel();
        BuildScreen3_Admin();
    }

    void BuildHeader()
    {
        // Título grande, bold, centrado — como en el wireframe
        // Versión leída dinámicamente de Application.version (Edit > Project Settings > Player > Version)
        MenuCardBuilder.CreateText(transform, "MainTitle", "Prueba de Manejo  v" + Application.version,
            MenuTheme.HeaderTitleSize, FontStyles.Bold, MenuTheme.TextPrimary,
            TextAlignmentOptions.Center)
            .GetComponent<RectTransform>().Set(
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
                new Vector2(0, -50), new Vector2(0, 100));
    }

    // ════════════════════════════════════════════════════════════════════
    // PANTALLA 0 — QR + CÓDIGO MANUAL
    // ════════════════════════════════════════════════════════════════════

    void BuildScreen0_QR()
    {
        GameObject screen = MenuCardBuilder.CreateScreenContainer(transform, "Screen0_QR");
        screens[0] = screen;
        screenGroups[0] = screen.GetComponent<CanvasGroup>();

        // ── LAYOUT: 2 columnas según wireframe ──

        // Columna izquierda: QR — empieza más abajo para espacio con título
        GameObject leftPanel = new GameObject("QRPanel");
        leftPanel.transform.SetParent(screen.transform, false);
        leftPanel.AddComponent<RectTransform>().Set(
            new Vector2(0.06f, 0.05f), new Vector2(0.47f, 0.72f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);

        // Título sección QR
        MenuCardBuilder.CreateText(leftPanel.transform, "QRTitle",
            "Escanea el QR",
            36f, FontStyles.Bold, MenuTheme.TextPrimary, TextAlignmentOptions.Left)
            .GetComponent<RectTransform>().Set(
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
                new Vector2(0, 0), new Vector2(0, 50));

        // QR grande
        GameObject qrContainer = MenuCardBuilder.CreateQRDisplay(leftPanel.transform, new Vector2(380, 380));
        qrContainer.GetComponent<RectTransform>().Set(
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(0, -70), new Vector2(380, 380));
        qrImage = qrContainer.transform.Find("QRImage").GetComponent<RawImage>();

        // Status debajo del QR
        statusText = MenuCardBuilder.CreateText(leftPanel.transform, "Status",
            "Esperando verificación...",
            22f, FontStyles.Normal, MenuTheme.TextSecondary, TextAlignmentOptions.Left)
            .GetComponent<TextMeshProUGUI>();
        statusText.GetComponent<RectTransform>().Set(
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
            new Vector2(0, -465), new Vector2(0, 30));

        // Botón nuevo QR (oculto)
        newQRButton = MenuCardBuilder.CreateButton(leftPanel.transform, "Generar nuevo QR", "secondary",
            new Vector2(300, 60), () => StartQRSession()).GetComponent<Button>();
        newQRButton.GetComponent<RectTransform>().Set(
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(150, -510), new Vector2(300, 60));
        newQRButton.gameObject.SetActive(false);

        // ── Divider vertical ──
        GameObject divider = new GameObject("VerticalDivider");
        divider.transform.SetParent(screen.transform, false);
        divider.AddComponent<RectTransform>().Set(
            new Vector2(0.5f, 0.10f), new Vector2(0.5f, 0.70f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(1, 0));
        divider.AddComponent<Image>().color = MenuTheme.DividerColor;

        // ── Columna derecha: Código manual ──
        GameObject rightPanel = new GameObject("CodePanel");
        rightPanel.transform.SetParent(screen.transform, false);
        rightPanel.AddComponent<RectTransform>().Set(
            new Vector2(0.53f, 0.05f), new Vector2(0.94f, 0.72f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);

        // Título sección código
        MenuCardBuilder.CreateText(rightPanel.transform, "CodeTitle",
            "Ingresa tu trámite",
            36f, FontStyles.Bold, MenuTheme.TextPrimary, TextAlignmentOptions.Left)
            .GetComponent<RectTransform>().Set(
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
                new Vector2(0, 0), new Vector2(0, 50));

        // Label "TLX-" arriba
        MenuCardBuilder.CreateText(rightPanel.transform, "Prefix", "TLX-",
            30f, FontStyles.Bold, MenuTheme.TextPrimary, TextAlignmentOptions.Left)
            .GetComponent<RectTransform>().Set(
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
                new Vector2(0, -80), new Vector2(0, 40));

        // PIN input: 5 cajas debajo del label
        var pinContainer = MenuCardBuilder.CreatePinInput(rightPanel.transform, 5, 70f, 12f);
        pinContainer.GetComponent<RectTransform>().Set(
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(0, -125), new Vector2(5 * 70f + 4 * 12f, 80f));
        codeInput = pinContainer.GetComponentInChildren<TMP_InputField>();

        // Cachear refs para d-pad (hijos creados por CreatePinInput)
        Transform boxRow = pinContainer.transform.Find("BoxRow");
        pinDigits = new int[] { -1, -1, -1, -1, -1 };
        pinDigitTexts = new TextMeshProUGUI[5];
        pinBoxBorders = new Image[5];
        for (int i = 0; i < 5; i++)
        {
            Transform border = boxRow.Find("Border_" + i);
            pinBoxBorders[i] = border.GetComponent<Image>();
            pinDigitTexts[i] = border.Find("Digit_" + i).GetComponent<TextMeshProUGUI>();
        }

        // Sync teclado → pinDigits (coexistencia con d-pad)
        codeInput.onValueChanged.AddListener((string val) =>
        {
            for (int j = 0; j < 5; j++)
                pinDigits[j] = (j < val.Length && char.IsDigit(val[j])) ? (val[j] - '0') : -1;
            pinCursor = Mathf.Clamp(val.Length, 0, 4);
            RefreshPinVisuals();
        });

        // Enter con código completo → verificar (equivalente a click en "Verificar Código")
        codeInput.onSubmit.AddListener((string val) =>
        {
            if (val != null && val.Trim().Length == 5)
                OnVerifyCode();
            else
                codeInput.ActivateInputField(); // mantener foco si aún faltan dígitos
        });

        // Botón verificar — ancho completo, debajo del PIN
        verifyButton = MenuCardBuilder.CreateButton(rightPanel.transform, "Verificar Código", "primary",
            new Vector2(0, 70), () => OnVerifyCode()).GetComponent<Button>();
        verifyButton.GetComponent<RectTransform>().Set(
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
            new Vector2(0, -225), new Vector2(0, 70));

        // Error text
        errorText0 = MenuCardBuilder.CreateText(rightPanel.transform, "ErrorText", "",
            20f, FontStyles.Normal, MenuTheme.TextError, TextAlignmentOptions.Left)
            .GetComponent<TextMeshProUGUI>();
        errorText0.textWrappingMode = TextWrappingModes.Normal;
        errorText0.GetComponent<RectTransform>().Set(
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
            new Vector2(0, -310), new Vector2(0, 50));
    }

    // ════════════════════════════════════════════════════════════════════
    // PANTALLA 1 — OPCIONES (solo particular)
    // ════════════════════════════════════════════════════════════════════

    void BuildScreen1_Options()
    {
        GameObject screen = MenuCardBuilder.CreateScreenContainer(transform, "Screen1_Options");
        screens[1] = screen;
        screenGroups[1] = screen.GetComponent<CanvasGroup>();

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

        // ── Fila 2: Modelo label + 3 cards (45%-80%) ──
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
            float cardW = 0.32f;
            float gap = 0.04f;
            float totalW = cardW * titles.Length + gap * (titles.Length - 1);
            float startX = (1f - totalW) / 2f;
            float left = startX + i * (cardW + gap);

            GameObject card = MenuCardBuilder.CreateIconCard(area.transform, null,
                titles[i], descs[i], new Vector2(100, 100), letters[i]);
            card.GetComponent<RectTransform>().Set(
                new Vector2(left, 0.45f), new Vector2(left + cardW, 0.78f),
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

        // Transmisión automática por defecto (modo manual no disponible)
        PlayerPrefs.SetInt("TransmisionManual", 0);
        PlayerPrefs.Save();

        // ── Fila 3: Continuar ──
        continueBtn1 = MenuCardBuilder.CreateButton(area.transform, "Continuar", "primary",
            new Vector2(100, 100), () => GoToScreen(2)).GetComponent<Button>();
        continueBtn1.GetComponent<RectTransform>().Set(
            new Vector2(0.3f, 0.22f), new Vector2(0.7f, 0.35f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);

        // Defaults DESPUÉS de crear el botón
        OnVariantSelected(0);
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
        screens[2] = screen;
        screenGroups[2] = screen.GetComponent<CanvasGroup>();

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
                req.Dispose();
                continue;
            }

            var response = JsonUtility.FromJson<StatusResponse>(req.downloadHandler.text);
            req.Dispose();

            if (response.status == "verified")
            {
                tramiteId = response.tramiteId;
                citizenName = response.citizenName;
                licenseType = response.licenseType;
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

    void OnVerifyCode()
    {
        string code = codeInput != null ? codeInput.text.Trim().ToUpper() : "";

        // Demo codes para testing sin backend
        if (code == "00000") { tramiteId = "TLX-DEMO00000"; citizenName = "Demo Automóvil"; licenseType = "particular"; OnSessionVerified(); return; }
        if (code == "11111") { tramiteId = "TLX-DEMO11111"; citizenName = "Demo Pasajeros"; licenseType = "publico"; OnSessionVerified(); return; }
        if (code == "22222") { tramiteId = "TLX-DEMO22222"; citizenName = "Demo Moto"; licenseType = "motocicleta"; OnSessionVerified(); return; }
        if (code == "33333") { tramiteId = "TLX-DEMO33333"; citizenName = "Demo Carga"; licenseType = "carga"; OnSessionVerified(); return; }
        if (code == "44444") { tramiteId = "TLX-DEMO44444"; citizenName = "Demo Ambulancia"; licenseType = "emergencia"; OnSessionVerified(); return; }

        if (string.IsNullOrEmpty(code))
        {
            ShowError0("Ingresa un código de cita.");
            return;
        }
        StartCoroutine(LookupByCode(code));
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
                GoToScreen(2); // Directo a verificación volante
                break;
            case "publico":
                selectedSceneName = "BusPasajeros";
                GoToScreen(2);
                break;
            case "carga":
                selectedSceneName = "CamionDCarga";
                GoToScreen(2);
                break;
            case "emergencia":
                selectedSceneName = "Ambulancia";
                GoToScreen(2);
                break;
            case "particular":
            default:
                GoToScreen(1); // Mostrar opciones
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
    private TextMeshProUGUI adminNetworkLabel;
    private TextMeshProUGUI adminSimulatorLabel;
    private Toggle adminDisplayToggle;
    private Toggle adminNotificationsToggle;
    private TextMeshProUGUI adminDisplayMapLabel;

    void BuildScreen3_Admin()
    {
        GameObject screen = MenuCardBuilder.CreateScreenContainer(transform, "Screen3_Admin");
        screens[3] = screen;
        screenGroups[3] = screen.GetComponent<CanvasGroup>();

        // Content container (centrado, sin scroll necesario)
        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(screen.transform, false);
        RectTransform contentRt = contentObj.AddComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0.15f, 0.15f);
        contentRt.anchorMax = new Vector2(0.85f, 0.85f);
        contentRt.offsetMin = Vector2.zero;
        contentRt.offsetMax = Vector2.zero;

        var layout = contentObj.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 12f;
        layout.padding = new RectOffset(40, 40, 20, 20);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        contentObj.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Transform ct = contentObj.transform;
        var config = SimulatorConfig.Instance?.data ?? new SimulatorConfig.ConfigData();

        // ── Header ──
        AdminAddHeader(ct, "Configuracion");

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
        displayToggleGo.AddComponent<LayoutElement>().preferredHeight = 45f;
        adminDisplayToggle = displayToggleGo.GetComponent<Toggle>();

        // ── Notificaciones en pantalla ──
        GameObject notifToggleGo = MenuCardBuilder.CreateToggle(ct,
            "Mostrar notificaciones en pantalla", config.showNotifications);
        notifToggleGo.AddComponent<LayoutElement>().preferredHeight = 45f;
        adminNotificationsToggle = notifToggleGo.GetComponent<Toggle>();

        // ── Intercambiar displays ──
        adminDisplayMapLabel = AdminAddLabel(ct, "Displays", DisplayMapString(config));

        GameObject swapRow = new GameObject("SwapRow");
        swapRow.transform.SetParent(ct, false);
        swapRow.AddComponent<RectTransform>();
        var swapLayout = swapRow.AddComponent<HorizontalLayoutGroup>();
        swapLayout.spacing = 10f;
        swapLayout.childAlignment = TextAnchor.MiddleCenter;
        swapLayout.childControlWidth = false;
        swapLayout.childControlHeight = false;
        swapLayout.childForceExpandWidth = false;
        swapRow.AddComponent<LayoutElement>().preferredHeight = 45f;

        MenuCardBuilder.CreateButton(swapRow.transform, "Izq / Centro", "ghost",
            new Vector2(160f, 40f), () => OnSwapDisplays("left", "center"));
        MenuCardBuilder.CreateButton(swapRow.transform, "Centro / Der", "ghost",
            new Vector2(160f, 40f), () => OnSwapDisplays("center", "right"));
        MenuCardBuilder.CreateButton(swapRow.transform, "Izq / Der", "ghost",
            new Vector2(160f, 40f), () => OnSwapDisplays("left", "right"));

        AdminAddSpacer(ct, 5f);

        // ── Estado ──
        adminNetworkLabel = AdminAddLabel(ct, "Estado", "Sin verificar");

        // ── Simulador asignado ──
        string simDisplay = !string.IsNullOrEmpty(config.simulatorId)
            ? $"{config.simulatorId} — {config.simulatorName}"
            : "Sin asignar";
        adminSimulatorLabel = AdminAddLabel(ct, "Simulador", simDisplay);

        AdminAddSpacer(ct, 15f);

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
            new Vector2(200f, 50f), OnAdminSave);
        MenuCardBuilder.CreateButton(btnRow.transform, "Volver", "ghost",
            new Vector2(150f, 50f), () => GoToScreen(0));

        // Auto-check network on panel open
        StartCoroutine(AdminCheckNetwork());

        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRt);
    }

    // ── Admin UI Helpers ─────────────────────────────────────────────

    void AdminAddHeader(Transform parent, string text)
    {
        var obj = MenuCardBuilder.CreateText(parent, "H_" + text, text,
            36f, FontStyles.Bold, MenuTheme.PrimaryPurple, TextAlignmentOptions.Center);
        obj.AddComponent<LayoutElement>().preferredHeight = 50f;
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
        container.AddComponent<LayoutElement>().preferredHeight = 75f;
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
        GoToScreen(0);
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

    IEnumerator AdminCheckNetwork()
    {
        if (adminNetworkLabel != null) adminNetworkLabel.text = "Verificando...";
        string url = (SimulatorConfig.Instance?.data.apiBaseUrl ?? "https://d6twaegbhg.execute-api.us-east-1.amazonaws.com")
            + "/simulator/lookup?code=test";

        using (var req = UnityEngine.Networking.UnityWebRequest.Get(url))
        {
            req.timeout = 5;
            yield return req.SendWebRequest();
            if (adminNetworkLabel != null)
                adminNetworkLabel.text = (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success
                    || req.responseCode == 400 || req.responseCode == 404) ? "Conectado" : "Sin conexion";
        }
    }

    /// <summary>Llamado desde AdminPanel.cs cuando F10 abre el admin.</summary>
    public void NavigateToAdmin() => GoToScreen(3);

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

        if (Time.timeScale != 1f)
            Debug.LogWarning($"[MenuScreenManager] PrepareWheelScreen: timeScale={Time.timeScale}");

        // ── 1) Detectar dispositivo y comparar huella con la calibración guardada ──
        InputDevice dev = TryAttachToDevice();

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
        if (currentScreen == 0)
            UpdatePinDpad();
        else if (currentScreen == 1)
            UpdateOptionsDpad();

        if (currentScreen != 2) return;

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
                    if (!SafeReadFloat(_steerCandidates[i], out var sv)) continue;
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
                steerMaxSeen = SafeReadFloat(_steerCandidates[bestIdx], out var smv) ? smv : steerCenter;
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
            if (pedalCandidates[i] != null)
                pedalCandidateRests[i] = pedalCandidates[i].ReadValue();
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
            if (!SafeReadFloat(pedalCandidates[i], out var v)) continue;
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
            _steerCandidateBaselines[i] = _steerCandidates[i].ReadValue();
    }

    void SnapshotReverseBaseline()
    {
        InputDevice wheel = TryAttachToDevice();
        if (wheel == null) { _reverseAxisBaseline = null; return; }

        _reverseAxisBaseline = new System.Collections.Generic.Dictionary<string, float>();
        // Snapshot baseline en wheel + shifter (si hay) — la palanca R puede
        // estar en cualquiera de los dos. Prefijos "wheel:"/"shifter:" en las
        // claves para resolver el control correcto al detectar.
        SnapshotBaselineForDevice(wheel, "wheel:");
        foreach (var d in InputSystem.devices)
        {
            if (d == wheel) continue;
            if (UIInputNew.IsShifterDevice(d))
                SnapshotBaselineForDevice(d, "shifter:");
        }

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
            _reverseAxisBaseline[prefix + p] = fc.ReadValue();
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
        var devices = new System.Collections.Generic.List<(InputDevice dev, string prefix)>();
        foreach (var d in InputSystem.devices)
        {
            if (d == wheel) continue;
            if (UIInputNew.IsShifterDevice(d))
                devices.Add((d, "shifter:"));
        }
        devices.Add((wheel, "wheel:"));

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
            if (!SafeReadFloat(fc, out var rxv)) continue;
            if (Mathf.Abs(rxv - baseline) >= REVERSE_AXIS_DETECT_DELTA)
                return prefixed;
        }
        return "";
    }

    float ReadSteerInput()
    {
        if (steerAction == null) TryAttachToDevice();
        if (steerAction == null) return 0f;
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

        float gasRest = PlayerPrefs.GetFloat("G923_GasRest", 0f);
        float brakeRest = PlayerPrefs.GetFloat("G923_BrakeRest", 0f);

        bool degraded = false;
        float t = 0f;
        while (t < duration)
        {
            // Durante el splash, el usuario no debería estar pisando nada.
            // Un delta grande contra el rest guardado indica hardware
            // desconectado o axis distinto al que se calibró.
            if (SafeReadFloat(gasC, out var gv) && Mathf.Abs(gv - gasRest) > 0.5f) { degraded = true; break; }
            if (SafeReadFloat(brakeC, out var bv) && Mathf.Abs(bv - brakeRest) > 0.5f) { degraded = true; break; }
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

        // Circle o Enter → confirmar selección o activar Continuar
        bool confirmPressed = (SafeReadFloat(circleBtn, out var ccv) && ccv > 0.5f) ||
                              (SafeReadFloat(enterBtn,  out var cev) && cev > 0.5f);
        if (confirmPressed)
        {
            if (!confirmBtnHeld)
            {
                confirmBtnHeld = true;
                if (screen1Row == 0) OnVariantSelected(screen1Col);
                else if (screen1Row == 1) GoToScreen(2);
                RefreshScreen1Visuals();
            }
        }
        else confirmBtnHeld = false;

        if (!up && !down && !left && !right) return;

        int maxCol = screen1Row == 0 ? 2 : 0;

        if (up && screen1Row > 0)
        {
            screen1Row--;
            maxCol = screen1Row == 0 ? 2 : 0;
            screen1Col = Mathf.Min(screen1Col, maxCol);
        }
        else if (down && screen1Row < 1)
        {
            screen1Row++;
            maxCol = screen1Row == 0 ? 2 : 0;
            screen1Col = Mathf.Min(screen1Col, maxCol);
        }
        else if (left && screen1Col > 0) screen1Col--;
        else if (right && screen1Col < maxCol) screen1Col++;

        RefreshScreen1Visuals();
    }

    void RefreshScreen1Visuals()
    {
        // Modelo cards: foco=Gold, selección=Purple, ambos=Gold+fondo morado
        for (int i = 0; i < variantCards.Length; i++)
        {
            bool focused = (screen1Row == 0 && screen1Col == i);
            bool selected = (i == selectedVariantIndex);
            variantBorders[i].color = focused ? MenuTheme.Gold :
                (selected ? MenuTheme.CardBorderGold : MenuTheme.CardBorder);
            variantCards[i].transform.Find("Background").GetComponent<Image>().color =
                selected ? MenuTheme.CardSelected : MenuTheme.CardBackground;
        }

        // Continuar button
        if (continueBtn1 != null)
        {
            Image img = continueBtn1.GetComponent<Image>();
            TextMeshProUGUI txt = continueBtn1.GetComponentInChildren<TextMeshProUGUI>();
            if (screen1Row == 1)
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
        pinCursor = Mathf.Clamp(pinCursor + direction, 0, 4);
        RefreshPinVisuals();
    }

    void SyncPinToField()
    {
        if (codeInput == null) return;
        string text = "";
        for (int i = 0; i < 5; i++)
        {
            if (pinDigits[i] < 0) break;
            text += pinDigits[i].ToString();
        }
        codeInput.SetTextWithoutNotify(text);
    }

    void RefreshPinVisuals()
    {
        if (pinDigitTexts == null) return;
        for (int i = 0; i < 5; i++)
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
