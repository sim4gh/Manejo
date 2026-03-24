using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;
using UnityEngine.InputSystem;
using TMPro;
using QRCoder;

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
    private bool isManualTransmission;
    private Coroutine pollingCoroutine;

    // ── Escenas por licenseType ────────────────────────────────────────
    private readonly string[] variantScenes = { "carretera", "Jetta", "Camioneta" };

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
    private bool confirmBtnHeld;
    private float dpadUpT, dpadDownT, dpadLeftT, dpadRightT;
    private bool dpadUpR, dpadDownR, dpadLeftR, dpadRightR;
    private const float DPAD_DELAY = 0.4f;
    private const float DPAD_RATE  = 0.15f;

    // D-pad Pantalla 1 — foco vs selección
    private int screen1Row;  // 0=modelo, 1=transmisión, 2=continuar
    private int screen1Col;  // columna de foco dentro de la fila

    // Pantalla 1
    private GameObject[] variantCards;
    private Image[] variantBorders;
    private GameObject[] transmissionCards;
    private Image[] transmissionBorders;
    private Button continueBtn1;

    // Pantalla 2
    private TextMeshProUGUI wheelPrompt;
    private Image rightIndicator;
    private Image rightFill;
    private RectTransform rightFillRT;
    private Image leftIndicator;
    private Image leftFill;
    private RectTransform leftFillRT;
    private TextMeshProUGUI examInfoText;
    private bool rightDone, leftDone;
    private Button skipButton;
    private const float WHEEL_THRESHOLD = 0.9f;

    // ── Assets ─────────────────────────────────────────────────────────
    private Texture2D bgTexture;
    private Sprite tlaxcalaLogo;

    // ── Input (G923 como HID genérico) ─────────────────────────────────
    private InputAction steerAction;

    void Start()
    {
        LoadResources();
        SetupCanvas();
        ClearExistingChildren();
        BuildLayout();
        ShowScreen(0);
        StartQRSession();
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
        MenuCardBuilder.CreateText(transform, "MainTitle", "Prueba de Manejo",
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

        string[] titles = { "Sedán", "Jetta", "Camioneta" };
        string[] descs = { "Compacto estándar", "Sedán mediano", "Familiar SUV" };
        string[] letters = { "S", "J", "C" };
        variantCards = new GameObject[3];
        variantBorders = new Image[3];

        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            float cardW = 0.28f;
            float gap = 0.03f;
            float totalW = cardW * 3 + gap * 2;
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

        // ── Fila 3: Transmisión label + 2 cards (15%-40%) ──
        MenuCardBuilder.CreateText(area.transform, "TransLabel", "Transmisión",
            24f, FontStyles.Bold, MenuTheme.TextPrimary, TextAlignmentOptions.Center)
            .GetComponent<RectTransform>().Set(
                new Vector2(0, 0.33f), new Vector2(1, 0.41f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);

        string[] transNames = { "Automática", "Manual" };
        transmissionCards = new GameObject[2];
        transmissionBorders = new Image[2];

        for (int i = 0; i < 2; i++)
        {
            int idx = i;
            float cardW = 0.35f;
            float gap = 0.04f;
            float totalW = cardW * 2 + gap;
            float startX = (1f - totalW) / 2f;
            float left = startX + i * (cardW + gap);

            GameObject card = MenuCardBuilder.CreateCard(area.transform, new Vector2(100, 100));
            card.GetComponent<RectTransform>().Set(
                new Vector2(left, 0.22f), new Vector2(left + cardW, 0.33f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            Transform content = card.transform.Find("Content");
            MenuCardBuilder.CreateText(content, "Text", transNames[i],
                24f, FontStyles.Bold, MenuTheme.TextOnCard, TextAlignmentOptions.Center)
                .GetComponent<RectTransform>().Set(
                    Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                    Vector2.zero, Vector2.zero);

            Button btn = card.AddComponent<Button>();
            btn.targetGraphic = card.transform.Find("Background").GetComponent<Image>();
            btn.onClick.AddListener(() => OnTransmissionSelected(idx));
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            cb.pressedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            cb.fadeDuration = 0.12f;
            btn.colors = cb;

            transmissionCards[i] = card;
            transmissionBorders[i] = card.transform.Find("Border").GetComponent<Image>();
        }
        OnTransmissionSelected(0);

        // ── Fila 4: Continuar (bottom, más arriba) ──
        continueBtn1 = MenuCardBuilder.CreateButton(area.transform, "Continuar", "primary",
            new Vector2(100, 100), () => GoToScreen(2)).GetComponent<Button>();
        continueBtn1.GetComponent<RectTransform>().Set(
            new Vector2(0.3f, 0.06f), new Vector2(0.7f, 0.19f), new Vector2(0.5f, 0.5f),
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

    void OnTransmissionSelected(int idx)
    {
        isManualTransmission = (idx == 1);
        PlayerPrefs.SetInt("TransmisionManual", isManualTransmission ? 1 : 0);
        PlayerPrefs.Save();

        for (int i = 0; i < transmissionCards.Length; i++)
        {
            bool sel = (i == idx);
            transmissionBorders[i].color = sel ? MenuTheme.CardBorderGold : MenuTheme.CardBorder;
            transmissionCards[i].transform.Find("Background").GetComponent<Image>().color =
                sel ? MenuTheme.CardSelected : MenuTheme.CardBackground;
        }
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

        // Prompt principal — grande y claro
        wheelPrompt = MenuCardBuilder.CreateText(area.transform, "Prompt",
            "Para comenzar tu prueba de manejo,\ngira el volante hacia la DERECHA",
            44f, FontStyles.Bold, MenuTheme.TextPrimary, TextAlignmentOptions.Center)
            .GetComponent<TextMeshProUGUI>();
        wheelPrompt.textWrappingMode = TextWrappingModes.Normal;
        wheelPrompt.GetComponent<RectTransform>().Set(
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
            new Vector2(0, y), new Vector2(0, 120));
        y -= 150;

        // Indicador derecha — más alto
        GameObject rightBar = new GameObject("RightIndicator");
        rightBar.transform.SetParent(area.transform, false);
        rightBar.AddComponent<RectTransform>().Set(
            new Vector2(0.15f, 1), new Vector2(0.85f, 1), new Vector2(0.5f, 1),
            new Vector2(0, y), new Vector2(0, 60));
        rightIndicator = rightBar.AddComponent<Image>();
        rightIndicator.sprite = MenuCardBuilder.GetRoundedSprite(MenuTheme.CornerRadiusSmall);
        rightIndicator.type = Image.Type.Sliced;
        rightIndicator.color = MenuTheme.IndicatorPending;

        // Fill dentro del track derecho
        GameObject rightFillObj = new GameObject("RightFill");
        rightFillObj.transform.SetParent(rightBar.transform, false);
        rightFillRT = rightFillObj.AddComponent<RectTransform>();
        rightFillRT.anchorMin = new Vector2(0, 0);
        rightFillRT.anchorMax = new Vector2(0, 1);
        rightFillRT.offsetMin = new Vector2(4, 4);
        rightFillRT.offsetMax = new Vector2(-4, -4);
        rightFill = rightFillObj.AddComponent<Image>();
        rightFill.sprite = MenuCardBuilder.GetRoundedSprite(MenuTheme.CornerRadiusSmall);
        rightFill.type = Image.Type.Sliced;
        rightFill.color = MenuTheme.SecondaryCrimson;
        rightFill.raycastTarget = false;
        y -= 90;

        // "y después hacia la IZQUIERDA"
        MenuCardBuilder.CreateText(area.transform, "ThenLabel",
            "y después hacia la IZQUIERDA",
            36f, FontStyles.Bold, MenuTheme.TextPrimary, TextAlignmentOptions.Center)
            .GetComponent<RectTransform>().Set(
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
                new Vector2(0, y), new Vector2(0, 50));
        y -= 70;

        // Indicador izquierda
        GameObject leftBar = new GameObject("LeftIndicator");
        leftBar.transform.SetParent(area.transform, false);
        leftBar.AddComponent<RectTransform>().Set(
            new Vector2(0.15f, 1), new Vector2(0.85f, 1), new Vector2(0.5f, 1),
            new Vector2(0, y), new Vector2(0, 60));
        leftIndicator = leftBar.AddComponent<Image>();
        leftIndicator.sprite = MenuCardBuilder.GetRoundedSprite(MenuTheme.CornerRadiusSmall);
        leftIndicator.type = Image.Type.Sliced;
        leftIndicator.color = MenuTheme.IndicatorPending;

        // Fill dentro del track izquierdo
        GameObject leftFillObj = new GameObject("LeftFill");
        leftFillObj.transform.SetParent(leftBar.transform, false);
        leftFillRT = leftFillObj.AddComponent<RectTransform>();
        leftFillRT.anchorMin = new Vector2(1, 0);
        leftFillRT.anchorMax = new Vector2(1, 1);
        leftFillRT.offsetMin = new Vector2(4, 4);
        leftFillRT.offsetMax = new Vector2(-4, -4);
        leftFill = leftFillObj.AddComponent<Image>();
        leftFill.sprite = MenuCardBuilder.GetRoundedSprite(MenuTheme.CornerRadiusSmall);
        leftFill.type = Image.Type.Sliced;
        leftFill.color = MenuTheme.SecondaryCrimson;
        leftFill.raycastTarget = false;
        y -= 100;

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
            yield return new WaitForSeconds(10f);

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

    // ════════════════════════════════════════════════════════════════════
    // PANTALLA 3 — ADMIN CONFIG (F10 para acceder)
    // ════════════════════════════════════════════════════════════════════

    // Admin screen refs
    private TMP_InputField adminStationIdInput;
    private TMP_InputField adminApiUrlInput;
    private TMP_InputField adminSerialFrameInput;
    private TMP_InputField adminSerialSeatInput;
    private TMP_InputField adminSerialComputerInput;
    private TMP_InputField adminSerialDofInput;
    private TMP_InputField adminSerialWheelInput;
    private TextMeshProUGUI adminVersionLabel;
    private TextMeshProUGUI adminPendingLabel;
    private TextMeshProUGUI adminNetworkLabel;

    void BuildScreen3_Admin()
    {
        GameObject screen = MenuCardBuilder.CreateScreenContainer(transform, "Screen3_Admin");
        screens[3] = screen;
        screenGroups[3] = screen.GetComponent<CanvasGroup>();

        // Scroll area (casi toda la pantalla)
        GameObject scrollObj = new GameObject("AdminScroll");
        scrollObj.transform.SetParent(screen.transform, false);
        scrollObj.AddComponent<RectTransform>().Set(
            new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.92f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        scrollObj.AddComponent<RectMask2D>();

        // Content
        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(scrollObj.transform, false);
        RectTransform contentRt = contentObj.AddComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0, 1);
        contentRt.anchorMax = new Vector2(1, 1);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = new Vector2(0, 0);

        var layout = contentObj.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.padding = new RectOffset(40, 40, 10, 20);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        contentObj.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = scrollObj.AddComponent<ScrollRect>();
        scroll.content = contentRt;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = 30f;

        Transform ct = contentObj.transform;
        var config = SimulatorConfig.Instance?.data ?? new SimulatorConfig.ConfigData();

        // ── Header ──
        AdminAddHeader(ct, "Configuracion del Simulador");

        // ── Identidad ──
        AdminAddSection(ct, "Identidad");
        adminStationIdInput = AdminAddField(ct, "Station ID", config.stationId, "SIM-001");
        adminApiUrlInput = AdminAddField(ct, "API Base URL", config.apiBaseUrl, "https://...");

        // ── Seriales ──
        AdminAddSection(ct, "Numeros de Serie");
        adminSerialFrameInput = AdminAddField(ct, "Herraje / Frame", config.serialNumbers.frame, "FRM-XXX");
        adminSerialSeatInput = AdminAddField(ct, "Silla / Seat", config.serialNumbers.seat, "SEAT-XXX");
        adminSerialComputerInput = AdminAddField(ct, "Computadora", config.serialNumbers.computer, "PC-XXX");
        adminSerialDofInput = AdminAddField(ct, "DOF Controller", config.serialNumbers.dofController, "sim-2dof-XXX");
        adminSerialWheelInput = AdminAddField(ct, "Volante / Controles", config.serialNumbers.wheel, "G923-XXX");

        // ── Actualizaciones ──
        AdminAddSection(ct, "Actualizaciones");
        adminVersionLabel = AdminAddLabel(ct, "Version", Application.version);

        // ── Scoring del servidor ──
        AdminAddSection(ct, "Calificacion (del servidor)");
        var scoring = ScoringConfig.Instance?.data ?? new ScoringConfig.ScoringData();
        AdminAddLabel(ct, "Calificacion minima", scoring.passingScore.ToString());
        AdminAddLabel(ct, "Duracion del examen", $"{scoring.examDurationSeconds}s ({scoring.examDurationSeconds / 60}min)");
        AdminAddLabel(ct, "Atropello", $"-{scoring.penalties.pedestrianHit}");
        AdminAddLabel(ct, "Semaforo en rojo", $"-{scoring.penalties.redLight}");
        AdminAddLabel(ct, "Sentido contrario", $"-{scoring.penalties.wrongWay}");
        AdminAddLabel(ct, "Exceso de velocidad", $"-{scoring.penalties.speeding}");

        // ── Diagnóstico ──
        AdminAddSection(ct, "Diagnostico");
        int pending = SimulatorApiClient.PendingCount;
        adminPendingLabel = AdminAddLabel(ct, "Resultados pendientes", pending > 0 ? $"{pending} sin enviar" : "Ninguno");
        adminNetworkLabel = AdminAddLabel(ct, "Red", "Sin verificar");

        // ── Botones ──
        AdminAddSpacer(ct, 15f);

        // Fila de botones
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
        MenuCardBuilder.CreateButton(btnRow.transform, "Verificar Red", "secondary",
            new Vector2(200f, 50f), () => StartCoroutine(AdminCheckNetwork()));
        MenuCardBuilder.CreateButton(btnRow.transform, "Volver", "ghost",
            new Vector2(150f, 50f), () => GoToScreen(0));

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

    void OnAdminSave()
    {
        if (SimulatorConfig.Instance == null) return;
        var data = SimulatorConfig.Instance.data;

        data.stationId = adminStationIdInput?.text ?? data.stationId;
        data.apiBaseUrl = adminApiUrlInput?.text ?? data.apiBaseUrl;

        if (!string.IsNullOrEmpty(data.stationId))
            data.thingName = "sim-pc-" + data.stationId.Replace("SIM-", "").ToLower();

        data.serialNumbers.frame = adminSerialFrameInput?.text ?? "";
        data.serialNumbers.seat = adminSerialSeatInput?.text ?? "";
        data.serialNumbers.computer = adminSerialComputerInput?.text ?? "";
        data.serialNumbers.dofController = adminSerialDofInput?.text ?? "";
        data.serialNumbers.wheel = adminSerialWheelInput?.text ?? "";

        SimulatorConfig.Instance.Save();

        if (GameManager.Instance != null)
            GameManager.Instance.ThingName = data.thingName;

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
            stationId = data.stationId,
            thingName = data.thingName,
            appVersion = Application.version,
            platform = "windows",
            serialNumbers = data.serialNumbers
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
                Debug.Log("[Admin] Registrado en backend");
            else
                Debug.LogWarning($"[Admin] Error registrando: {req.error}");
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
        public string stationId;
        public string thingName;
        public string appVersion;
        public string platform;
        public SimulatorConfig.SerialNumbers serialNumbers;
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
        rightDone = false;
        leftDone = false;
        rightIndicator.color = MenuTheme.IndicatorPending;
        leftIndicator.color = MenuTheme.IndicatorPending;

        // Resetear fills a ancho 0
        rightFillRT.anchorMax = new Vector2(0, 1);
        rightFill.color = MenuTheme.SecondaryCrimson;
        leftFillRT.anchorMin = new Vector2(1, 0);
        leftFill.color = MenuTheme.SecondaryCrimson;

        wheelPrompt.text = "Para comenzar tu prueba de manejo,\ngira el volante hacia la DERECHA";
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
        if (rightDone && leftDone) return;

        float steer = ReadSteerInput();

        if (!rightDone)
        {
            float progress = Mathf.Clamp01(steer);

            if (steer >= WHEEL_THRESHOLD)
            {
                rightDone = true;
                rightFillRT.anchorMax = new Vector2(1, 1);
                rightFill.color = MenuTheme.IndicatorDone;
                rightIndicator.color = MenuTheme.IndicatorDone;
                wheelPrompt.text = "Para comenzar tu prueba de manejo,\ngira el volante hacia la IZQUIERDA";
            }
            else if (Mathf.Abs(progress - rightFillRT.anchorMax.x) > 0.005f)
            {
                rightFillRT.anchorMax = new Vector2(progress, 1);
                rightFill.color = Color.Lerp(MenuTheme.SecondaryCrimson, MenuTheme.IndicatorDone, progress);
            }
            return;
        }

        // rightDone && !leftDone
        float leftProgress = Mathf.Clamp01(-steer);

        if (-steer >= WHEEL_THRESHOLD)
        {
            leftDone = true;
            leftFillRT.anchorMin = new Vector2(0, 0);
            leftFill.color = MenuTheme.IndicatorDone;
            leftIndicator.color = MenuTheme.IndicatorDone;
            wheelPrompt.text = "Cargando prueba...";
            skipButton.gameObject.SetActive(false);
            StartCoroutine(LoadSceneDelayed(1.5f));
        }
        else if (Mathf.Abs(leftProgress - (1 - leftFillRT.anchorMin.x)) > 0.005f)
        {
            leftFillRT.anchorMin = new Vector2(1 - leftProgress, 0);
            leftFill.color = Color.Lerp(MenuTheme.SecondaryCrimson, MenuTheme.IndicatorDone, leftProgress);
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
        yield return new WaitForSeconds(delay);
        LoadSelectedScene();
    }

    float ReadSteerInput()
    {
        // Intentar detectar G923 cada frame hasta encontrarlo
        if (steerAction == null)
        {
            foreach (var device in InputSystem.devices)
            {
                string name = device.displayName ?? "";
                string desc = device.description.product ?? "";
                if (name.IndexOf("G923", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    desc.IndexOf("G923", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Logitech", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    string path = device.path;
                    steerAction = new InputAction("MenuSteer", InputActionType.Value);
                    steerAction.AddBinding(path + "/stick/x");
                    steerAction.Enable();
                    // Cache d-pad hat + Circle para PIN input
                    hatUp    = device.TryGetChildControl("hat/up")    as InputControl<float>;
                    hatDown  = device.TryGetChildControl("hat/down")  as InputControl<float>;
                    hatLeft  = device.TryGetChildControl("hat/left")  as InputControl<float>;
                    hatRight = device.TryGetChildControl("hat/right") as InputControl<float>;
                    circleBtn = device.TryGetChildControl("button3")  as InputControl<float>;
                    enterBtn  = device.TryGetChildControl("button10") as InputControl<float>;
                    Debug.Log($"[MenuScreenManager] Volante detectado: {name} ({path}) hat={hatUp != null}");
                    break;
                }
            }
            // Fallback: cualquier joystick o gamepad
            if (steerAction == null && Joystick.current != null)
            {
                steerAction = new InputAction("MenuSteer", InputActionType.Value);
                steerAction.AddBinding(Joystick.current.path + "/stick/x");
                steerAction.Enable();
                var jDev = Joystick.current;
                hatUp    = jDev.TryGetChildControl("hat/up")    as InputControl<float>;
                hatDown  = jDev.TryGetChildControl("hat/down")  as InputControl<float>;
                hatLeft  = jDev.TryGetChildControl("hat/left")  as InputControl<float>;
                hatRight = jDev.TryGetChildControl("hat/right") as InputControl<float>;
                circleBtn = jDev.TryGetChildControl("button3")  as InputControl<float>;
                enterBtn  = jDev.TryGetChildControl("button10") as InputControl<float>;
                Debug.Log($"[MenuScreenManager] Joystick fallback: {jDev.displayName} hat={hatUp != null}");
            }
            if (steerAction == null && Gamepad.current != null)
            {
                steerAction = new InputAction("MenuSteer", InputActionType.Value);
                steerAction.AddBinding("<Gamepad>/leftStick/x");
                steerAction.Enable();
                var gp = Gamepad.current;
                hatUp    = gp.TryGetChildControl("dpad/up")    as InputControl<float>;
                hatDown  = gp.TryGetChildControl("dpad/down")  as InputControl<float>;
                hatLeft  = gp.TryGetChildControl("dpad/left")  as InputControl<float>;
                hatRight = gp.TryGetChildControl("dpad/right") as InputControl<float>;
                circleBtn = gp.TryGetChildControl("buttonEast") as InputControl<float>;
                enterBtn  = gp.TryGetChildControl("start")      as InputControl<float>;
            }
            // No encontró nada — retorna 0, intentará de nuevo el próximo frame
            if (steerAction == null) return 0f;
        }
        return steerAction.ReadValue<float>();
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
        bool confirmPressed = (circleBtn != null && circleBtn.ReadValue() > 0.5f) ||
                              (enterBtn  != null && enterBtn.ReadValue()  > 0.5f);
        if (confirmPressed)
        {
            if (!confirmBtnHeld)
            {
                confirmBtnHeld = true;
                if (screen1Row == 0) OnVariantSelected(screen1Col);
                else if (screen1Row == 1) OnTransmissionSelected(screen1Col);
                else if (screen1Row == 2) GoToScreen(2);
                RefreshScreen1Visuals();
            }
        }
        else confirmBtnHeld = false;

        if (!up && !down && !left && !right) return;

        int maxCol = screen1Row == 0 ? 2 : (screen1Row == 1 ? 1 : 0);

        if (up && screen1Row > 0)
        {
            screen1Row--;
            maxCol = screen1Row == 0 ? 2 : 1;
            screen1Col = Mathf.Min(screen1Col, maxCol);
        }
        else if (down && screen1Row < 2)
        {
            screen1Row++;
            maxCol = screen1Row == 0 ? 2 : (screen1Row == 1 ? 1 : 0);
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

        // Transmisión cards
        int transIdx = isManualTransmission ? 1 : 0;
        for (int i = 0; i < transmissionCards.Length; i++)
        {
            bool focused = (screen1Row == 1 && screen1Col == i);
            bool selected = (i == transIdx);
            transmissionBorders[i].color = focused ? MenuTheme.Gold :
                (selected ? MenuTheme.CardBorderGold : MenuTheme.CardBorder);
            transmissionCards[i].transform.Find("Background").GetComponent<Image>().color =
                selected ? MenuTheme.CardSelected : MenuTheme.CardBackground;
        }

        // Continuar button
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
        bool confirmPin = (circleBtn != null && circleBtn.ReadValue() > 0.5f) ||
                          (enterBtn  != null && enterBtn.ReadValue()  > 0.5f);
        if (confirmPin)
        {
            if (!confirmBtnHeld) { confirmBtnHeld = true; OnVerifyCode(); }
        }
        else confirmBtnHeld = false;
    }

    bool DpadRepeat(InputControl<float> ctrl, ref float timer, ref bool repeated, float dt)
    {
        if (ctrl == null) return false;
        if (ctrl.ReadValue() <= 0.5f) { timer = 0f; repeated = false; return false; }

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
        Debug.Log($"[MenuScreenManager] Iniciando sesión y cargando: {selectedSceneName} | Tramite: {tramiteId}");
        StartCoroutine(StartSessionAndLoadScene());
    }

    IEnumerator StartSessionAndLoadScene()
    {
        // Descargar scoring config del backend (timeout 5s, usa cache si falla)
        if (ScoringConfig.Instance != null)
            yield return ScoringConfig.Instance.LoadFromBackend();

        // Iniciar sesión en el backend (fire-and-forget: si falla, seguimos)
        string thingName = GameManager.Instance?.ThingName ?? "sim-pc-unconfigured";

        if (!string.IsNullOrEmpty(tramiteId))
        {
            yield return SimulatorApiClient.StartSession(tramiteId, thingName, (sid) =>
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
