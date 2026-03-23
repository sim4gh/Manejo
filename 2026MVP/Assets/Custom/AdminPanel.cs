using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections;

/// <summary>
/// Panel de configuración administrativo oculto.
/// Activación: Ctrl+Shift+A mantenido 1.5 segundos (solo en MainMenu).
/// Protegido por PIN de 6 dígitos.
/// </summary>
public class AdminPanel : MonoBehaviour
{
    public static AdminPanel Instance { get; private set; }

    private const float HOLD_DURATION = 1.5f;
    private const int PIN_LENGTH = 6;

    // Estado de activación
    private float holdTimer;
    private bool panelVisible;

    // UI references
    private GameObject overlayRoot;
    private GameObject pinScreen;
    private GameObject configScreen;

    // Config fields
    private TMP_InputField stationIdInput;
    private TMP_InputField apiUrlInput;
    private TMP_InputField pinInput;
    private TMP_InputField serialFrameInput;
    private TMP_InputField serialSeatInput;
    private TMP_InputField serialComputerInput;
    private TMP_InputField serialDofInput;
    private TMP_InputField serialWheelInput;
    private Toggle autoUpdateToggle;
    private TextMeshProUGUI versionLabel;
    private TextMeshProUGUI thingNameLabel;
    private TextMeshProUGUI pendingLabel;
    private TextMeshProUGUI statusLabel;

    private static string cachedSceneName = "";

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += (scene, _) => cachedSceneName = scene.name;
            cachedSceneName = SceneManager.GetActiveScene().name;
            Debug.Log($"[AdminPanel] Inicializado en escena: {cachedSceneName} (F10 mantenido 1.5s para abrir)");
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Update()
    {
        // Solo permitir activación en MainMenu
        if (cachedSceneName != "MainMenu") return;
        if (panelVisible) return;

        // F10 mantenido 1.5s — abre AdminPanel (cross-platform, no conflicta)
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.f10Key.isPressed)
        {
            holdTimer += Time.unscaledDeltaTime;
            if (holdTimer >= HOLD_DURATION)
            {
                holdTimer = 0f;
                ShowPanel();
            }
        }
        else
        {
            holdTimer = 0f;
        }
    }

    void ShowPanel()
    {
        panelVisible = true;
        BuildOverlay();
        ShowPinScreen();
    }

    void HidePanel()
    {
        panelVisible = false;
        if (overlayRoot != null) Destroy(overlayRoot);
        overlayRoot = null;
    }

    // ── UI Construction ──────────────────────────────────────────────

    private Canvas cachedCanvas;

    Canvas FindCanvas()
    {
        if (cachedCanvas != null) return cachedCanvas;
#pragma warning disable CS0618
        Canvas[] canvases = Object.FindObjectsOfType<Canvas>();
#pragma warning restore CS0618
        foreach (var c in canvases)
        {
            if (c.renderMode == RenderMode.ScreenSpaceOverlay && c.transform.parent == null)
            { cachedCanvas = c; return cachedCanvas; }
        }
        if (canvases.Length > 0) cachedCanvas = canvases[0];
        return cachedCanvas;
    }

    void BuildOverlay()
    {
        Canvas targetCanvas = FindCanvas();
        if (targetCanvas == null) { panelVisible = false; return; }

        // Root overlay
        overlayRoot = new GameObject("AdminPanelOverlay");
        overlayRoot.transform.SetParent(targetCanvas.transform, false);
        RectTransform rootRt = overlayRoot.AddComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        // Backdrop
        Image backdrop = overlayRoot.AddComponent<Image>();
        backdrop.color = new Color(0f, 0f, 0f, 0.9f);
        backdrop.raycastTarget = true;
    }

    // ── PIN Screen ───────────────────────────────────────────────────

    void ShowPinScreen()
    {
        if (configScreen != null) { configScreen.SetActive(false); }

        pinScreen = new GameObject("PinScreen");
        pinScreen.transform.SetParent(overlayRoot.transform, false);
        RectTransform rt = pinScreen.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        // Card central
        GameObject card = MenuCardBuilder.CreateCard(pinScreen.transform, new Vector2(450f, 350f));
        RectTransform cardRt = card.GetComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.anchoredPosition = Vector2.zero;

        Transform content = card.transform.Find("Content");

        // Titulo
        var title = MenuCardBuilder.CreateText(content, "Title", "Panel Administrativo",
            MenuTheme.CardTitleSize, FontStyles.Bold, MenuTheme.TextPrimary, TextAlignmentOptions.Center);
        title.GetComponent<RectTransform>().Set(
            new Vector2(0, 0.75f), new Vector2(1, 0.95f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);

        // Subtitulo
        var sub = MenuCardBuilder.CreateText(content, "Sub", "Ingrese PIN de administrador",
            MenuTheme.CardDescSize, FontStyles.Normal, MenuTheme.TextSecondary, TextAlignmentOptions.Center);
        sub.GetComponent<RectTransform>().Set(
            new Vector2(0, 0.62f), new Vector2(1, 0.75f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);

        // PIN input
        GameObject pinContainer = MenuCardBuilder.CreateInputField(content, "", "000000",
            new Vector2(200f, 50f), TMP_InputField.ContentType.Pin);
        pinContainer.GetComponent<RectTransform>().Set(
            new Vector2(0.5f, 0.45f), new Vector2(0.5f, 0.45f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(200f, 75f));
        pinInput = pinContainer.GetComponentInChildren<TMP_InputField>();
        if (pinInput != null) pinInput.characterLimit = PIN_LENGTH;

        // Botones
        GameObject enterBtn = MenuCardBuilder.CreateButton(content, "Entrar", "primary",
            new Vector2(150f, 45f), OnPinEnter);
        enterBtn.GetComponent<RectTransform>().Set(
            new Vector2(0.3f, 0.1f), new Vector2(0.3f, 0.1f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(150f, 45f));

        GameObject cancelBtn = MenuCardBuilder.CreateButton(content, "Cancelar", "secondary",
            new Vector2(150f, 45f), HidePanel);
        cancelBtn.GetComponent<RectTransform>().Set(
            new Vector2(0.7f, 0.1f), new Vector2(0.7f, 0.1f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(150f, 45f));
    }

    void OnPinEnter()
    {
        string enteredPin = pinInput != null ? pinInput.text : "";
        string correctPin = SimulatorConfig.Instance?.data.adminPin ?? "202626";

        if (enteredPin == correctPin)
        {
            Destroy(pinScreen);
            ShowConfigScreen();
        }
        else
        {
            if (pinInput != null) pinInput.text = "";
            // Flash error visual
            Debug.LogWarning("[AdminPanel] PIN incorrecto");
        }
    }

    // ── Config Screen ────────────────────────────────────────────────

    void ShowConfigScreen()
    {
        var config = SimulatorConfig.Instance?.data ?? new SimulatorConfig.ConfigData();

        configScreen = new GameObject("ConfigScreen");
        configScreen.transform.SetParent(overlayRoot.transform, false);
        RectTransform rt = configScreen.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        // Scroll area
        GameObject scrollObj = new GameObject("Scroll");
        scrollObj.transform.SetParent(configScreen.transform, false);
        RectTransform scrollRt = scrollObj.AddComponent<RectTransform>();
        scrollRt.anchorMin = new Vector2(0.1f, 0.05f);
        scrollRt.anchorMax = new Vector2(0.9f, 0.95f);
        scrollRt.offsetMin = Vector2.zero;
        scrollRt.offsetMax = Vector2.zero;

        // Content container (vertical layout)
        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(scrollObj.transform, false);
        RectTransform contentRt = contentObj.AddComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0, 1);
        contentRt.anchorMax = new Vector2(1, 1);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;

        var layout = contentObj.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 12f;
        layout.padding = new RectOffset(30, 30, 20, 20);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = contentObj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Scroll Rect
        ScrollRect scroll = scrollObj.AddComponent<ScrollRect>();
        scroll.content = contentRt;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = 30f;

        // Mask
        Image maskImg = scrollObj.AddComponent<Image>();
        maskImg.color = Color.clear;
        scrollObj.AddComponent<Mask>().showMaskGraphic = false;

        Transform ct = contentObj.transform;

        // ── Header ──
        AddSectionHeader(ct, "Panel Administrativo del Simulador");

        // ── Identidad ──
        AddSectionHeader(ct, "Identidad");
        stationIdInput = AddField(ct, "Station ID", config.stationId, "SIM-001");
        thingNameLabel = AddLabel(ct, "Thing Name", config.thingName);
        apiUrlInput = AddField(ct, "API Base URL", config.apiBaseUrl, "https://...");

        // ── Seriales ──
        AddSectionHeader(ct, "Numeros de Serie");
        serialFrameInput = AddField(ct, "Herraje / Frame", config.serialNumbers.frame, "FRM-XXX");
        serialSeatInput = AddField(ct, "Silla / Seat", config.serialNumbers.seat, "SEAT-XXX");
        serialComputerInput = AddField(ct, "Computadora", config.serialNumbers.computer, "PC-XXX");
        serialDofInput = AddField(ct, "DOF Controller", config.serialNumbers.dofController, "sim-2dof-XXX");
        serialWheelInput = AddField(ct, "Volante / Controles", config.serialNumbers.wheel, "G923-XXX");

        // ── Actualizaciones ──
        AddSectionHeader(ct, "Actualizaciones");
        versionLabel = AddLabel(ct, "Version Actual", Application.version);
        AddButton(ct, "Buscar Actualizaciones", "secondary", OnCheckUpdates);
        // Auto-update toggle placeholder
        autoUpdateToggle = AddToggle(ct, "Auto-update al final del dia", config.autoUpdate);

        // ── Seguridad ──
        AddSectionHeader(ct, "Seguridad");
        pinInput = AddField(ct, "PIN de Admin (6 digitos)", config.adminPin, "000000").GetComponentInChildren<TMP_InputField>();

        // ── Calificación (del servidor) ──
        AddSectionHeader(ct, "Calificacion (del servidor)");
        var scoring = ScoringConfig.Instance?.data ?? new ScoringConfig.ScoringData();
        string syncTime = !string.IsNullOrEmpty(scoring.lastSyncedAt) ? scoring.lastSyncedAt : "Sin sincronizar";
        AddLabel(ct, "Ultima sincronizacion", syncTime);
        AddLabel(ct, "Calificacion minima", scoring.passingScore.ToString());
        AddLabel(ct, "Duracion del examen", $"{scoring.examDurationSeconds}s ({scoring.examDurationSeconds / 60}min)");
        AddSectionHeader(ct, "Penalizaciones");
        AddLabel(ct, "Atropello (peaton)", $"-{scoring.penalties.pedestrianHit}");
        AddLabel(ct, "Colision bicicleta", $"-{scoring.penalties.bicycleCollision}");
        AddLabel(ct, "Colision vehiculo", $"-{scoring.penalties.vehicleCollision}");
        AddLabel(ct, "Colision senalamiento", $"-{scoring.penalties.signCollision}");
        AddLabel(ct, "Colision obstaculo", $"-{scoring.penalties.obstacleCollision}");
        AddLabel(ct, "Semaforo en rojo", $"-{scoring.penalties.redLight}");
        AddLabel(ct, "Sentido contrario", $"-{scoring.penalties.wrongWay}");
        AddLabel(ct, "Exceso de velocidad", $"-{scoring.penalties.speeding}");
        AddLabel(ct, "Cambio peligroso", $"-{scoring.penalties.dangerousGearChange}");
        AddSectionHeader(ct, "Umbrales de Resultado");
        AddLabel(ct, "APTO", $"{scoring.gradeThresholds.apto}+");
        AddLabel(ct, "APTO CONDICIONADO", $"{scoring.gradeThresholds.aptoCondicionado}+");
        AddLabel(ct, "Reentrenamiento", $"{scoring.gradeThresholds.aptoReentrenamiento}+");
        AddButton(ct, "Sincronizar Ahora", "secondary", OnSyncScoring);

        // ── Diagnóstico ──
        AddSectionHeader(ct, "Diagnostico");
        int pendingCount = SimulatorApiClient.PendingCount;
        pendingLabel = AddLabel(ct, "Resultados Pendientes", pendingCount > 0 ? $"{pendingCount} sin enviar" : "Ninguno");
        if (pendingCount > 0)
            AddButton(ct, "Reintentar Envio", "secondary", OnRetryPending);
        statusLabel = AddLabel(ct, "Estado de Red", "Sin verificar");
        AddButton(ct, "Verificar Conexion", "secondary", OnCheckNetwork);

        // ── Action buttons ──
        AddSpacer(ct, 20f);
        AddButton(ct, "Guardar Configuracion", "primary", OnSave);
        AddButton(ct, "Cerrar", "ghost", HidePanel);

        // Resize content
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRt);
    }

    // ── UI Helpers ───────────────────────────────────────────────────

    void AddSectionHeader(Transform parent, string text)
    {
        var obj = MenuCardBuilder.CreateText(parent, "Header_" + text, text,
            MenuTheme.SubtitleSize, FontStyles.Bold, MenuTheme.TextPrimary, TextAlignmentOptions.Left);
        var le = obj.AddComponent<LayoutElement>();
        le.preferredHeight = 40f;
        le.minHeight = 40f;
    }

    TMP_InputField AddField(Transform parent, string label, string value, string placeholder)
    {
        GameObject container = MenuCardBuilder.CreateInputField(parent, label, placeholder,
            new Vector2(0, 45f));
        var le = container.AddComponent<LayoutElement>();
        le.preferredHeight = 75f;
        le.minHeight = 75f;
        TMP_InputField input = container.GetComponentInChildren<TMP_InputField>();
        if (input != null) input.text = value ?? "";
        return input;
    }

    TextMeshProUGUI AddLabel(Transform parent, string label, string value)
    {
        GameObject row = new GameObject("Label_" + label);
        row.transform.SetParent(parent, false);
        row.AddComponent<RectTransform>();
        var hLayout = row.AddComponent<HorizontalLayoutGroup>();
        hLayout.spacing = 10f;
        hLayout.childAlignment = TextAnchor.MiddleLeft;
        hLayout.childControlWidth = true;
        hLayout.childControlHeight = true;
        hLayout.childForceExpandWidth = true;
        hLayout.childForceExpandHeight = false;
        var le = row.AddComponent<LayoutElement>();
        le.preferredHeight = 35f;
        le.minHeight = 35f;

        var labelObj = MenuCardBuilder.CreateText(row.transform, "Key", label + ":",
            MenuTheme.LabelSize, FontStyles.Normal, MenuTheme.TextSecondary, TextAlignmentOptions.Left);
        labelObj.AddComponent<LayoutElement>().flexibleWidth = 1f;

        var valueObj = MenuCardBuilder.CreateText(row.transform, "Value", value ?? "—",
            MenuTheme.LabelSize, FontStyles.Bold, MenuTheme.TextPrimary, TextAlignmentOptions.Right);
        valueObj.AddComponent<LayoutElement>().flexibleWidth = 1f;

        return valueObj.GetComponent<TextMeshProUGUI>();
    }

    void AddButton(Transform parent, string text, string style, System.Action onClick)
    {
        GameObject btn = MenuCardBuilder.CreateButton(parent, text, style,
            new Vector2(0, 48f), onClick);
        var le = btn.AddComponent<LayoutElement>();
        le.preferredHeight = 48f;
        le.minHeight = 48f;
    }

    Toggle AddToggle(Transform parent, string label, bool value)
    {
        GameObject toggle = MenuCardBuilder.CreateToggle(parent, label, value);
        var le = toggle.AddComponent<LayoutElement>();
        le.preferredHeight = 40f;
        le.minHeight = 40f;
        return toggle.GetComponent<Toggle>();
    }

    void AddSpacer(Transform parent, float height)
    {
        GameObject spacer = new GameObject("Spacer");
        spacer.transform.SetParent(parent, false);
        spacer.AddComponent<RectTransform>();
        var le = spacer.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;
    }

    // ── Actions ──────────────────────────────────────────────────────

    void OnSave()
    {
        if (SimulatorConfig.Instance == null) return;

        var data = SimulatorConfig.Instance.data;
        data.stationId = stationIdInput?.text ?? data.stationId;
        data.apiBaseUrl = apiUrlInput?.text ?? data.apiBaseUrl;

        // Auto-derive thingName from stationId
        if (!string.IsNullOrEmpty(data.stationId))
            data.thingName = "sim-pc-" + data.stationId.Replace("SIM-", "").ToLower();

        // Seriales
        data.serialNumbers.frame = serialFrameInput?.text ?? "";
        data.serialNumbers.seat = serialSeatInput?.text ?? "";
        data.serialNumbers.computer = serialComputerInput?.text ?? "";
        data.serialNumbers.dofController = serialDofInput?.text ?? "";
        data.serialNumbers.wheel = serialWheelInput?.text ?? "";

        // Auto-update
        if (autoUpdateToggle != null) data.autoUpdate = autoUpdateToggle.isOn;

        // PIN
        string newPin = pinInput?.text ?? "";
        if (newPin.Length == PIN_LENGTH) data.adminPin = newPin;

        SimulatorConfig.Instance.Save();

        // Actualizar GameManager
        if (GameManager.Instance != null)
            GameManager.Instance.ThingName = data.thingName;

        // Registrar en backend
        StartCoroutine(RegisterWithBackend(data));

        Debug.Log("[AdminPanel] Configuracion guardada");
        HidePanel();
    }

    IEnumerator RegisterWithBackend(SimulatorConfig.ConfigData data)
    {
        string url = $"{data.apiBaseUrl}/simulator/register";
        string json = JsonUtility.ToJson(new RegisterRequest
        {
            stationId = data.stationId,
            thingName = data.thingName,
            appVersion = Application.version,
            platform = "windows",
            serialNumbers = data.serialNumbers
        });

        Debug.Log($"[AdminPanel] POST {url}");

        using (var request = new UnityEngine.Networking.UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                Debug.Log("[AdminPanel] Registrado en backend exitosamente");
            else
                Debug.LogWarning($"[AdminPanel] Error registrando: {request.error}");
        }
    }

    void OnSyncScoring()
    {
        StartCoroutine(SyncScoringAndRefresh());
    }

    IEnumerator SyncScoringAndRefresh()
    {
        if (ScoringConfig.Instance == null) yield break;
        yield return ScoringConfig.Instance.LoadFromBackend();
        // Rebuild panel to show updated values
        HidePanel();
        ShowPanel();
    }

    void OnCheckUpdates()
    {
        if (AutoUpdater.Instance != null)
            StartCoroutine(AutoUpdater.Instance.CheckForUpdate((available, version) =>
            {
                if (versionLabel != null)
                    versionLabel.text = available ? $"{Application.version} (nueva: {version})" : Application.version + " (al dia)";
            }));
    }

    void OnRetryPending()
    {
        StartCoroutine(RetryAndUpdateLabel());
    }

    IEnumerator RetryAndUpdateLabel()
    {
        yield return SimulatorApiClient.RetryPendingResults();
        int remaining = SimulatorApiClient.PendingCount;
        if (pendingLabel != null)
            pendingLabel.text = remaining > 0 ? $"{remaining} sin enviar" : "Ninguno";
    }

    void OnCheckNetwork()
    {
        StartCoroutine(CheckNetworkStatus());
    }

    IEnumerator CheckNetworkStatus()
    {
        if (statusLabel != null) statusLabel.text = "Verificando...";

        string url = SimulatorConfig.Instance?.data.apiBaseUrl ?? "https://d6twaegbhg.execute-api.us-east-1.amazonaws.com";

        using (var request = UnityEngine.Networking.UnityWebRequest.Get(url + "/simulator/lookup?code=test"))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (statusLabel != null)
            {
                // 400 or 404 means API is reachable (just invalid params)
                if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success ||
                    request.responseCode == 400 || request.responseCode == 404)
                    statusLabel.text = "Conectado";
                else
                    statusLabel.text = $"Sin conexion ({request.error})";
            }
        }
    }

    [System.Serializable]
    private class RegisterRequest
    {
        public string stationId;
        public string thingName;
        public string appVersion;
        public string platform;
        public SimulatorConfig.SerialNumbers serialNumbers;
    }
}
