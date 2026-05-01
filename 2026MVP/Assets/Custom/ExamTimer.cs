using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Countdown de 5 minutos para el examen de manejo.
/// Crea su propia UI proceduralmente en el Canvas.
/// Al llegar a 0:00, exporta telemetría y muestra resultados.
/// </summary>
public class ExamTimer : MonoBehaviour
{
    public static ExamTimer Instance;

    [Header("Configuración")]
    [Tooltip("Duración del examen en segundos (300 = 5 min)")]
    public float examDuration = 300f;

    /// <summary>Evento disparado cuando el examen termina.</summary>
    public System.Action OnExamFinished;

    private float startTime;
    private bool examFinished;
    private TextMeshProUGUI timerText;
    private Image bgPanel;
    private GameObject canvasGo;
    private RectTransform containerRt;
    private LayoutElement layoutElement;

    private int lastDisplayedSecond = -1;
    private int lastKnownScore = 100;

    /// <summary>Disparado al final de CreateTimerUI; permite que TopHudRow adopte el container sin race con InitAfterFrame.</summary>
    public System.Action OnContainerReady;

    // Tamaño base del slot del timer cuando vive dentro de un HorizontalLayoutGroup.
    private const float SlotWidth = 180f;
    private const float SlotHeight = 70f;

    // Colores por fase
    private static readonly Color colorNormal  = Color.white;
    private static readonly Color colorWarning = Color.yellow;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        startTime = Time.time;
        // Defer 1 frame para que MultiPantallaManager.Start() corra primero y
        // las cámaras tengan ya su targetDisplay reasignado según config.
        StartCoroutine(InitAfterFrame());
    }

    System.Collections.IEnumerator InitAfterFrame()
    {
        yield return null;
        CreateTimerUI();
        HideExportTelemetryButton();
    }

    // Oculta el botón "Exportar Telemetría" (residuo de debug, hoy inútil
    // porque ExamTimer.EndExam() ya dispara ExportTelemetry() automáticamente).
    // Busca por texto del label en hijos activos+inactivos de la escena.
    void HideExportTelemetryButton()
    {
#pragma warning disable CS0618
        TextMeshProUGUI[] allTexts = Object.FindObjectsOfType<TextMeshProUGUI>(true);
#pragma warning restore CS0618
        foreach (var t in allTexts)
        {
            if (t == null || string.IsNullOrEmpty(t.text)) continue;
            if (t.text.IndexOf("Exportar Telemetr", System.StringComparison.OrdinalIgnoreCase) < 0) continue;

            Button btn = t.GetComponentInParent<Button>(true);
            GameObject target = btn != null ? btn.gameObject : t.transform.parent.gameObject;
            Destroy(target);
        }
    }

    void CreateTimerUI()
    {
        // Canvas dedicado, child de este GameObject — el HUD vive aquí, NO
        // parented a un Canvas externo. Si el ExamTimer se destruye (al cambiar
        // de escena), todo el árbol UI se va con él. Antes el container se hacía
        // child de un Canvas encontrado con FindObjectsOfType, y si ese Canvas
        // pertenecía a un GameObject persistente entre escenas, el HUD quedaba
        // colgado → al volver a iniciar el examen aparecían 2 timers acumulados.
        canvasGo = new GameObject("ExamTimerCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        canvas.targetDisplay = DisplayHelper.CockpitDisplay;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        // Sin GraphicRaycaster — el HUD del timer no es interactivo.

        // Contenedor del timer
        GameObject container = new GameObject("ExamTimerHUD");
        container.transform.SetParent(canvasGo.transform, false);
        containerRt = container.AddComponent<RectTransform>();
        ApplyStandaloneLayout(containerRt);

        // LayoutElement para que TopHudRow lo coloque dentro del HorizontalLayoutGroup
        // sin tener que cambiar anchors. Solo aplica cuando el container está bajo un row.
        layoutElement = container.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = SlotWidth;
        layoutElement.preferredHeight = SlotHeight;
        layoutElement.flexibleWidth = 0f;
        layoutElement.flexibleHeight = 0f;

        // Fondo semi-transparente oscuro con esquinas redondeadas
        bgPanel = container.AddComponent<Image>();
        bgPanel.sprite = MenuCardBuilder.GetRoundedSprite(MenuTheme.CornerRadiusSmall);
        bgPanel.type = Image.Type.Sliced;
        bgPanel.color = new Color(0f, 0f, 0f, 0.6f);
        bgPanel.raycastTarget = false;

        // Texto del timer
        GameObject textObj = new GameObject("TimerText");
        textObj.transform.SetParent(container.transform, false);
        RectTransform textRt = textObj.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        timerText = textObj.AddComponent<TextMeshProUGUI>();
        timerText.text = FormatTime(examDuration);
        timerText.fontSize = 42f;
        timerText.fontStyle = FontStyles.Bold;
        timerText.color = colorNormal;
        timerText.alignment = TextAlignmentOptions.Center;
        timerText.verticalAlignment = VerticalAlignmentOptions.Middle;
        timerText.raycastTarget = false;

        // Intentar usar Roboto Bold (misma fuente del menú)
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Roboto-Bold SDF");
        if (font != null) timerText.font = font;

        OnContainerReady?.Invoke();
    }

    // Anchors para modo standalone (top-center), independiente del row.
    static void ApplyStandaloneLayout(RectTransform rt)
    {
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -15f);
        rt.sizeDelta = new Vector2(SlotWidth, 55f);
    }

    /// <summary>
    /// Reparenta el container del timer al slot del TopHudRow. Si parent es null,
    /// reattach al ExamTimerCanvas propio (path simétrico para limpieza). Los
    /// overrides ajustan el LayoutElement cuando el slot del row tiene tamaño
    /// distinto al default standalone.
    /// </summary>
    public void AttachContainerTo(RectTransform parent, float? overrideWidth = null, float? overrideHeight = null)
    {
        if (containerRt == null) return;

        if (parent != null)
        {
            containerRt.SetParent(parent, false);
            // En modo layout-group: anchors stretch para que LayoutElement controle el tamaño.
            containerRt.anchorMin = Vector2.zero;
            containerRt.anchorMax = Vector2.one;
            containerRt.pivot = new Vector2(0.5f, 0.5f);
            containerRt.anchoredPosition = Vector2.zero;
            containerRt.sizeDelta = Vector2.zero;

            if (layoutElement != null)
            {
                if (overrideWidth.HasValue) layoutElement.preferredWidth = overrideWidth.Value;
                if (overrideHeight.HasValue) layoutElement.preferredHeight = overrideHeight.Value;
            }
        }
        else if (canvasGo != null)
        {
            containerRt.SetParent(canvasGo.transform, false);
            ApplyStandaloneLayout(containerRt);
            if (layoutElement != null)
            {
                layoutElement.preferredWidth = SlotWidth;
                layoutElement.preferredHeight = SlotHeight;
            }
        }
    }

    void Update()
    {
        if (examFinished || timerText == null) return;

        float remainingTime = examDuration - (Time.time - startTime);

        if (remainingTime <= 0f)
        {
            EndExam();
            return;
        }

        // Actualizar display solo cuando cambia el segundo (evita GC cada frame)
        int totalSec = Mathf.CeilToInt(remainingTime);
        if (totalSec != lastDisplayedSecond)
        {
            lastDisplayedSecond = totalSec;
            timerText.text = FormatTime(remainingTime);
        }

        // Feedback visual por fase
        if (remainingTime <= 10f)
        {
            // Rojo + parpadeo
            timerText.color = MenuTheme.TextError;
            float alpha = Mathf.Lerp(0.3f, 1f, Mathf.PingPong(Time.time * 3f, 1f));
            timerText.alpha = alpha;
        }
        else if (remainingTime <= 30f)
        {
            // Rojo fijo
            timerText.color = MenuTheme.TextError;
            timerText.alpha = 1f;
        }
        else if (remainingTime <= 60f)
        {
            // Amarillo
            timerText.color = colorWarning;
            timerText.alpha = 1f;
        }
        else
        {
            // Blanco normal
            timerText.color = colorNormal;
            timerText.alpha = 1f;
        }

        // Cachear score para interrupción (OnDestroy/OnApplicationQuit)
        ViolationDetector det = Object.FindFirstObjectByType<ViolationDetector>();
        if (det != null) lastKnownScore = det.totalScore;
    }

    void EndExam()
    {
        examFinished = true;
        timerText.text = "0:00";
        timerText.color = MenuTheme.TextError;
        timerText.alpha = 1f;

        // Log evento de fin
        TelemetryLogger.Instance?.LogEvent("FIN_EXAMEN", "Tiempo agotado", 0, 0f);

        // Exportar telemetría
        ViolationDetector detector = Object.FindFirstObjectByType<ViolationDetector>();
        int finalScore = 100;
        if (detector != null)
        {
            finalScore = detector.totalScore;
            detector.ExportTelemetry();
        }

        // Disparar evento
        OnExamFinished?.Invoke();

        // Enviar resultados al backend
        SendResultsToApi(finalScore);

        // Mostrar pantalla de resultados
        ExamResultsScreen.Show(finalScore);
    }

    void SendResultsToApi(int finalScore)
    {
        string sessionId = GameManager.Instance?.SessionId;
        if (string.IsNullOrEmpty(sessionId))
        {
            Debug.LogWarning("[ExamTimer] No hay sessionId — resultados solo guardados localmente");
            return;
        }

        bool passed = finalScore >= 70;
        var faults = SimulatorApiClient.BuildFaultsFromTelemetry();

        StartCoroutine(SimulatorApiClient.EndSession(sessionId, passed, finalScore, faults, false, (success) =>
        {
            if (!success)
            {
                Debug.LogWarning("[ExamTimer] Fallo al enviar resultados — guardando para retry");
                SimulatorApiClient.SavePendingResult(sessionId, passed, finalScore, faults, false);
            }
        }));
    }

    // ── Interrupción: guardar resultados parciales si el examen no terminó ──

    void OnApplicationQuit()
    {
        SavePartialIfNeeded();
    }

    void OnDestroy()
    {
        if (!Application.isPlaying) return;
        SavePartialIfNeeded();
    }

    void SavePartialIfNeeded()
    {
        if (examFinished) return;
        examFinished = true; // Primero — cerrar race window (OnDestroy + OnApplicationQuit)

        string sessionId = GameManager.Instance?.SessionId;
        if (string.IsNullOrEmpty(sessionId)) return;

        // Interrumpido: siempre passed=false, interrupted=true
        var faults = SimulatorApiClient.BuildFaultsFromTelemetry();
        SimulatorApiClient.SavePendingResult(sessionId, false, lastKnownScore, faults, true);
        Debug.Log($"[ExamTimer] Examen interrumpido (score={lastKnownScore}, interrupted=true)");
    }

    static string FormatTime(float seconds)
    {
        if (seconds < 0f) seconds = 0f;
        int min = (int)(seconds / 60f);
        int sec = (int)(seconds % 60f);
        return $"{min}:{sec:D2}";
    }

    /// <summary>
    /// Crea un Canvas overlay root + EventSystem para escenas que no exponen ningún
    /// Canvas activo (caso Motocicleta). Público para que otros HUD procedurales
    /// (e.g. fallback del velocímetro) puedan reutilizar el mismo Canvas.
    /// </summary>
    public static Canvas EnsureFallbackHudCanvas()
    {
        const string canvasName = "ExamHudCanvas";
        GameObject existing = GameObject.Find(canvasName);
        if (existing != null)
        {
            Canvas c = existing.GetComponent<Canvas>();
            if (c != null) return c;
        }
        return CreateFallbackRootCanvas();
    }

    static Canvas CreateFallbackRootCanvas()
    {
        Debug.Log("[ExamTimer] No había Canvas activo en la escena — creando ExamHudCanvas root.");

        GameObject canvasObj = new GameObject("ExamHudCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        canvas.targetDisplay = DisplayHelper.CockpitDisplay;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // Sin GraphicRaycaster — los HUDs procedurales que cuelgan de este Canvas
        // no son interactivos. Si en el futuro hace falta input, agregarlo bajo demanda.

        // EventSystem defensivo: otros canvases (paneles F7-F10, menús) sí lo necesitan.
#pragma warning disable CS0618
        if (Object.FindObjectOfType<EventSystem>() == null)
#pragma warning restore CS0618
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        return canvas;
    }
}
