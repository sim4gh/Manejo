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
    private GameObject[] screens = new GameObject[3];
    private CanvasGroup[] screenGroups = new CanvasGroup[3];
    private int currentScreen = -1;

    // Pantalla 0
    private RawImage qrImage;
    private TMP_InputField codeInput;
    private TextMeshProUGUI statusText;
    private TextMeshProUGUI errorText0;
    private Button verifyButton;
    private Button newQRButton;

    // Pantalla 1
    private GameObject[] variantCards;
    private Image[] variantBorders;
    private GameObject[] transmissionCards;
    private Image[] transmissionBorders;
    private Button continueBtn1;

    // Pantalla 2
    private TextMeshProUGUI wheelPrompt;
    private Image rightIndicator;
    private Image leftIndicator;
    private TextMeshProUGUI examInfoText;
    private bool rightDone, leftDone;
    private Button skipButton;

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

        // 3 pantallas
        BuildScreen0_QR();
        BuildScreen1_Options();
        BuildScreen2_Wheel();
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

        // Fila: "TLX-" label + input
        GameObject inputRow = new GameObject("InputRow");
        inputRow.transform.SetParent(rightPanel.transform, false);
        inputRow.AddComponent<RectTransform>().Set(
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
            new Vector2(0, -90), new Vector2(0, 70));

        // Label "TLX-"
        MenuCardBuilder.CreateText(inputRow.transform, "Prefix", "TLX-",
            30f, FontStyles.Bold, MenuTheme.TextPrimary, TextAlignmentOptions.Left)
            .GetComponent<RectTransform>().Set(
                new Vector2(0, 0), new Vector2(0.18f, 1), new Vector2(0, 0.5f),
                Vector2.zero, Vector2.zero);

        // Input field (sin label)
        var inputContainer = MenuCardBuilder.CreateInputField(inputRow.transform,
            "", "XXXXXX", new Vector2(0, 60));
        inputContainer.GetComponent<RectTransform>().Set(
            new Vector2(0.18f, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        codeInput = inputContainer.GetComponentInChildren<TMP_InputField>();

        // Botón verificar — ancho completo, debajo del input
        verifyButton = MenuCardBuilder.CreateButton(rightPanel.transform, "Verificar Código", "primary",
            new Vector2(0, 70), () => OnVerifyCode()).GetComponent<Button>();
        verifyButton.GetComponent<RectTransform>().Set(
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
            new Vector2(0, -185), new Vector2(0, 70));

        // Error text
        errorText0 = MenuCardBuilder.CreateText(rightPanel.transform, "ErrorText", "",
            20f, FontStyles.Normal, MenuTheme.TextError, TextAlignmentOptions.Left)
            .GetComponent<TextMeshProUGUI>();
        errorText0.textWrappingMode = TextWrappingModes.Normal;
        errorText0.GetComponent<RectTransform>().Set(
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
            new Vector2(0, -270), new Vector2(0, 50));
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
        skipButton = MenuCardBuilder.CreateButton(area.transform, "Iniciar sin volante", "secondary",
            new Vector2(350, 65), () => LoadSelectedScene()).GetComponent<Button>();
        skipButton.GetComponent<RectTransform>().Set(
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
            new Vector2(0, y), new Vector2(350, 65));
        skipButton.gameObject.SetActive(false);
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
        if (code == "0000") { tramiteId = "TLX-DEMO0000"; citizenName = "Demo Automóvil"; licenseType = "particular"; OnSessionVerified(); return; }
        if (code == "1111") { tramiteId = "TLX-DEMO1111"; citizenName = "Demo Pasajeros"; licenseType = "publico"; OnSessionVerified(); return; }
        if (code == "2222") { tramiteId = "TLX-DEMO2222"; citizenName = "Demo Moto"; licenseType = "motocicleta"; OnSessionVerified(); return; }
        if (code == "3333") { tramiteId = "TLX-DEMO3333"; citizenName = "Demo Carga"; licenseType = "carga"; OnSessionVerified(); return; }

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
    }

    void PrepareWheelScreen()
    {
        rightDone = false;
        leftDone = false;
        rightIndicator.color = MenuTheme.IndicatorPending;
        leftIndicator.color = MenuTheme.IndicatorPending;
        wheelPrompt.text = "Para comenzar tu prueba de manejo,\ngira el volante hacia la DERECHA";
        skipButton.gameObject.SetActive(false);

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

        StartCoroutine(WheelCheckTimeout());
    }

    IEnumerator WheelCheckTimeout()
    {
        yield return new WaitForSeconds(15f);
        if (!rightDone || !leftDone)
            skipButton.gameObject.SetActive(true);
    }

    void Update()
    {
        if (currentScreen != 2) return;

        // Leer input del volante — buscar G923 por InputAction (no es Gamepad)
        float steer = ReadSteerInput();

        if (!rightDone && steer > 0.5f)
        {
            rightDone = true;
            rightIndicator.color = MenuTheme.IndicatorDone;
            wheelPrompt.text = "Para comenzar tu prueba de manejo,\ngira el volante hacia la IZQUIERDA";
        }

        if (rightDone && !leftDone && steer < -0.5f)
        {
            leftDone = true;
            leftIndicator.color = MenuTheme.IndicatorDone;
            wheelPrompt.text = "Iniciando prueba...";
            StartCoroutine(LoadSceneDelayed(1.5f));
        }
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
                    Debug.Log($"[MenuScreenManager] Volante detectado: {name} ({path})");
                    break;
                }
            }
            // Fallback: cualquier joystick o gamepad
            if (steerAction == null && Joystick.current != null)
            {
                steerAction = new InputAction("MenuSteer", InputActionType.Value);
                steerAction.AddBinding(Joystick.current.path + "/stick/x");
                steerAction.Enable();
                Debug.Log($"[MenuScreenManager] Joystick fallback: {Joystick.current.displayName}");
            }
            if (steerAction == null && Gamepad.current != null)
            {
                steerAction = new InputAction("MenuSteer", InputActionType.Value);
                steerAction.AddBinding("<Gamepad>/leftStick/x");
                steerAction.Enable();
            }
            // No encontró nada — retorna 0, intentará de nuevo el próximo frame
            if (steerAction == null) return 0f;
        }
        return steerAction.ReadValue<float>();
    }

    void LoadSelectedScene()
    {
        if (string.IsNullOrEmpty(selectedSceneName))
        {
            Debug.LogError("[MenuScreenManager] No hay escena seleccionada");
            return;
        }
        Debug.Log($"[MenuScreenManager] Cargando escena: {selectedSceneName} | Tramite: {tramiteId}");
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
